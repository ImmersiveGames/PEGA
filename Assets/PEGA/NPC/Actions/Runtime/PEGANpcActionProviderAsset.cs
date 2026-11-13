using System;
using Immersive.Framework.Actors;
using Immersive.Foundation.Events;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.GOAP;
using PEGA.Movement.Actor;
using PEGA.Movement.NPC;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Goals.Conditions;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Group;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
namespace PEGA.NPC.Actions.Runtime
{
    // Cada Action concreta é uma definição compartilhada; PEGANpcAutonomy cria um clone runtime por Actor.
    public abstract class PegaNpcActionProviderAsset : PegaNpcActionDefinitionAsset,
        IPegaNpcActionProvider,
        IPegaNpcExecutionTargetUpdater,
        IPegaNpcRemainingExecutionCostProvider,
        IPegaNpcCausalActionStepEvaluator,
        IPegaNpcObservedEffectCheckpointValidator
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.Actions";

        [SerializeField, Min(0f)]
        [FormerlySerializedAs("arrivalRadiusMeters")]
        [Tooltip("Default maximum distance from a selected destination that counts as arrival (m). A stationary plan may provide an authorized target-specific radius.")]
        private float arrivalToleranceMeters = 1.25f;

        private ScopedLogger _log;
        private long _executionId;
        private long _navigationRequestId;
        private bool _hasNavigationSnapshot;
        private PegaNpcNavigationSnapshot _navigationSnapshot;
        private bool _hasEffectTarget;
        private Vector3 _effectTarget;
        private PegaNpcPlanningTargetId _effectTargetId;
        private float _executionTargetRadiusOverrideMeters;
        private bool _hasExecutionTargetRadiusOverride;
        private double _completedAtTimeSeconds;
        private double _executionStartedAtTimeSeconds;
        private string _diagnostic = string.Empty;

        [NonSerialized]
        private bool _loggerInitialized;

        [NonSerialized]
        private EventBus<PegaNpcActionTerminalOutcomeEvent> _terminalOutcomeEvents;

        protected PegaNpcNavMeshMovementIntentSource NavigationSource =>
            RuntimeContext != null ? RuntimeContext.NavigationSource : null;

        public float ConfiguredArrivalRadiusMeters => arrivalToleranceMeters;

        protected abstract PegaNpcActionEffectKind ActionEffectKind { get; }

        public abstract PegaNpcActionContract Contract { get; }

        public PegaNpcActionOutcome Outcome { get; private set; } = PegaNpcActionOutcome.NotStarted;

        private float EffectiveTargetRadiusMeters =>
            _hasExecutionTargetRadiusOverride
                ? _executionTargetRadiusOverrideMeters
                : arrivalToleranceMeters;

        public PegaNpcActionExecutionSnapshot CurrentExecution
        {
            get
            {
                bool effectObserved = TryGetObservedEffect(out _);
                return new PegaNpcActionExecutionSnapshot(
                    Outcome,
                    _executionId,
                    DeclaredEffect,
                    effectObserved,
                    _diagnostic);
            }
        }

        /// <summary>Assina resultados terminais desta Action.</summary>
        /// <remarks>
        /// A entrega é síncrona e ocorre depois que o snapshot terminal foi gravado e registrado. Exceções de
        /// assinantes são registradas e isoladas para preservar o resultado e a entrega aos demais assinantes.
        /// </remarks>
        public IEventBinding SubscribeToTerminalOutcomes(Action<PegaNpcActionTerminalOutcomeEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureTerminalOutcomeEventsInitialized();
            return _terminalOutcomeEvents.Subscribe(outcome =>
            {
                try
                {
                    handler(outcome);
                }
                catch (Exception exception)
                {
                    LogSubscriberFailure(handler, outcome, exception);
                }
            });
        }

        public bool UnsubscribeFromTerminalOutcomes(IEventBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            return _terminalOutcomeEvents != null && _terminalOutcomeEvents.Unsubscribe(binding);
        }

        protected override void OnRuntimeDestroyed()
        {
            _terminalOutcomeEvents?.Clear();
            _terminalOutcomeEvents = null;
            base.OnRuntimeDestroyed();
        }

        public PegaNpcActionEffectDeclaration DeclaredEffect => new PegaNpcActionEffectDeclaration(
            ActionEffectKind,
            _effectTargetId,
            Contract.TargetKind,
            _hasEffectTarget,
            _effectTarget,
            EffectiveTargetRadiusMeters);

        public bool TryGetObservedEffect(out PegaNpcObservedActionEffect observedEffect)
        {
            observedEffect = default;
            if (Outcome != PegaNpcActionOutcome.Success ||
                (Contract.RequiresNavigation && !_hasNavigationSnapshot))
            {
                return false;
            }

            PegaNpcActionEffectDeclaration declaration = DeclaredEffect;
            float actualDistance = Contract.RequiresNavigation
                ? _navigationSnapshot.ActualDistanceToTargetMeters
                : GetPlanarDistance(ActorTransform.position, declaration.TargetPosition);
            if (!declaration.Kind.IsValid ||
                !declaration.IsTargetResolved ||
                !declaration.TargetId.IsValid ||
                !declaration.TargetKind.IsValid ||
                _executionId <= 0 ||
                !IsFinite(declaration.TargetPosition) ||
                !IsNonNegativeFinite(declaration.TargetRadiusMeters) ||
                !IsNonNegativeFinite(actualDistance) ||
                actualDistance > declaration.TargetRadiusMeters ||
                (Contract.RequiresNavigation &&
                 (_navigationSnapshot.RequestId != _navigationRequestId ||
                  (_navigationSnapshot.TargetPosition - declaration.TargetPosition).sqrMagnitude > 0.000001f)) ||
                !IsActionSpecificEffectObserved(out _))
            {
                return false;
            }

            observedEffect = new PegaNpcObservedActionEffect(
                declaration.Kind,
                declaration.TargetId,
                declaration.TargetKind,
                _executionId,
                declaration.TargetPosition,
                declaration.TargetRadiusMeters,
                actualDistance,
                _completedAtTimeSeconds);
            return true;
        }

        public virtual bool TryGetRemainingExecutionCostSeconds(
            long requestId,
            out float remainingSeconds,
            out string diagnostic)
        {
            remainingSeconds = 0f;
            diagnostic = string.Empty;
            if (Outcome != PegaNpcActionOutcome.Running ||
                requestId <= 0 ||
                requestId != _executionId)
            {
                diagnostic = "The requested Action execution is not the current running request.";
                return false;
            }

            float intrinsicRemaining = IntrinsicDurationSeconds;
            if (!IsNonNegativeFinite(intrinsicRemaining))
            {
                diagnostic = "The Action has no finite non-negative intrinsic duration estimate.";
                return false;
            }

            if (Contract.RequiresNavigation)
            {
                if (NavigationSource == null ||
                    !NavigationSource.IsReady ||
                    _navigationRequestId <= 0 ||
                    !NavigationSource.TryGetSnapshot(_navigationRequestId, out PegaNpcNavigationSnapshot snapshot) ||
                    snapshot.Outcome != PegaNpcNavigationOutcome.Running ||
                    !NavigationSource.TryGetRemainingRouteLengthMeters(
                        _navigationRequestId,
                        out float currentRemainingRouteMeters,
                        out diagnostic) ||
                    !IsPositiveFinite(snapshot.EffectiveSpeedMetersPerSecond))
                {
                    if (string.IsNullOrWhiteSpace(diagnostic))
                    {
                        diagnostic = "The active navigation request has no current route-distance and speed estimate.";
                    }

                    return false;
                }

                float remainingRouteMeters = currentRemainingRouteMeters;
                remainingSeconds = remainingRouteMeters / snapshot.EffectiveSpeedMetersPerSecond +
                    intrinsicRemaining;
                if (!IsNonNegativeFinite(remainingSeconds))
                {
                    remainingSeconds = 0f;
                    diagnostic = "The active navigation request produced a non-finite remaining cost.";
                    return false;
                }

                diagnostic = string.Empty;
                return true;
            }

            double elapsedSeconds = Math.Max(0d, Time.timeAsDouble - _executionStartedAtTimeSeconds);
            remainingSeconds = Mathf.Max(0f, intrinsicRemaining - (float)elapsedSeconds);
            if (!IsNonNegativeFinite(remainingSeconds))
            {
                remainingSeconds = 0f;
                diagnostic = "The active non-navigation Action produced a non-finite remaining cost.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        public virtual float IntrinsicDurationSeconds => 0f;

        public abstract bool TryCreatePlanningCandidates(
            PegaNpcActionPlanningContext context,
            out System.Collections.Generic.IReadOnlyList<PegaNpcPlanningTarget> targets,
            out System.Collections.Generic.IReadOnlyList<PegaNpcPlanningActionCandidate> candidates,
            out string diagnostic);

        /// <summary>
        /// Candidate generation may defer a route that is unavailable from the current position. The causal
        /// step evaluator checks route availability and cost against each preceding hypothetical position.
        /// Success still projects only generic arrival at the bound Target.
        /// </summary>
        public virtual bool TryCreateCausalActionDefinition(
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcGoapKnownTarget target,
            out GoapActionDefinition actionDefinition,
            out string diagnostic)
        {
            actionDefinition = null;
            if (!object.ReferenceEquals(candidate.Action, this) ||
                candidate.ActionId != Contract.ActionId ||
                candidate.RequiredTargetKind != Contract.TargetKind ||
                target == null ||
                candidate.TargetId != target.Target.TargetId ||
                candidate.RequiredTargetKind != target.Target.Kind)
            {
                diagnostic = "The navigation candidate is not a currently valid application to this known Target.";
                return false;
            }

            PegaNpcTargetReachedEffect reachedEffect = IsObjectEntryPositionIdentity(
                target.Target.TargetId.IdentityKind)
                ? new PegaNpcTargetReachedEffect(
                    target.Target.TargetId,
                    target.Target.Kind,
                    target.Target.EvidenceTimeSeconds,
                    target.Target.KnownPosition)
                : new PegaNpcTargetReachedEffect(
                    target.Target.TargetId,
                    target.Target.Kind,
                    target.Target.EvidenceTimeSeconds);
            actionDefinition = new GoapActionDefinition(
                Array.Empty<IGoapCondition>(),
                new IGoapEffect[] { reachedEffect });
            diagnostic = string.Empty;
            return true;
        }

        public virtual PegaNpcCausalStepEvaluationStatus EvaluateCausalStep(
            PegaNpcPlanningActionCandidate candidate,
            IGoapHypotheticalStateView stateBeforeStep,
            PegaNpcCausalPlanningKnowledge knowledge,
            out PegaNpcNavigationRouteEstimate routeEstimate,
            out float estimatedSeconds,
            out string diagnostic)
        {
            routeEstimate = candidate.RouteEstimate;
            estimatedSeconds = 0f;
            diagnostic = string.Empty;
            if (!object.ReferenceEquals(candidate.Action, this) ||
                !CandidateMatchesContract(candidate))
            {
                diagnostic = "The causal step does not match this Action provider's contract.";
                return PegaNpcCausalStepEvaluationStatus.Unavailable;
            }

            if (stateBeforeStep == null ||
                stateBeforeStep.Source != GoapStateSource.HypotheticalState ||
                knowledge == null ||
                !knowledge.TryGetKnownTarget(candidate.TargetId, out PegaNpcGoapKnownTarget target) ||
                target.Target.Kind != candidate.RequiredTargetKind)
            {
                diagnostic = "The causal step has no valid hypothetical state or legitimate known Target.";
                return PegaNpcCausalStepEvaluationStatus.Unknown;
            }

            if (!Contract.RequiresNavigation)
            {
                if (stateBeforeStep.ProjectedEffects.Count > 0)
                {
                    diagnostic = "This Action has no state-aware duration evaluator for a projected state.";
                    return PegaNpcCausalStepEvaluationStatus.Unknown;
                }

                routeEstimate = PegaNpcNavigationRouteEstimate.NotRequired;
                estimatedSeconds = candidate.IntrinsicDurationSeconds;
                if (IsNonNegativeFinite(estimatedSeconds))
                {
                    return PegaNpcCausalStepEvaluationStatus.Available;
                }

                diagnostic = "The Action's intrinsic duration is not finite and non-negative.";
                return PegaNpcCausalStepEvaluationStatus.Unknown;
            }

            if (stateBeforeStep.ProjectedEffects.Count > 0 &&
                candidate.TargetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.CharacterSeparationDestination)
            {
                diagnostic = "The separation Target is derived from the current physical position and cannot be reused after a projected move.";
                return PegaNpcCausalStepEvaluationStatus.Unknown;
            }

            if (!TryGetHypotheticalNavigationStart(
                    stateBeforeStep,
                    knowledge,
                    out Vector3 sourcePosition,
                    out bool hasProjectedPosition,
                    out diagnostic))
            {
                return PegaNpcCausalStepEvaluationStatus.Unknown;
            }

            if (NavigationSource == null || NavigationSource.gameObject != ActorRoot)
            {
                diagnostic = "The Navigation Action has no local route evaluator.";
                return PegaNpcCausalStepEvaluationStatus.Unknown;
            }

            bool routeAvailable = hasProjectedPosition
                ? NavigationSource.TryEstimateRouteFrom(
                    sourcePosition,
                    target.Target.KnownPosition,
                    candidate.SpeedRegime,
                    ArrivalRadiusMeters,
                    out routeEstimate,
                    out diagnostic)
                : NavigationSource.TryEstimateRoute(
                    target.Target.KnownPosition,
                    candidate.SpeedRegime,
                    ArrivalRadiusMeters,
                    out routeEstimate,
                    out diagnostic);
            if (!routeAvailable)
            {
                return GetRouteEvaluationStatus(routeEstimate.Status);
            }

            if (routeEstimate.Status != PegaNpcPlanningRouteStatus.Complete ||
                !IsPositiveFinite(routeEstimate.EffectiveSpeedMetersPerSecond))
            {
                diagnostic = "The route evaluator did not return a complete route and usable speed.";
                return PegaNpcCausalStepEvaluationStatus.Unknown;
            }

            PegaNpcCausalStepEvaluationStatus routeConstraintStatus = EvaluateCausalNavigationConstraints(
                target,
                sourcePosition,
                routeEstimate,
                out diagnostic);
            if (routeConstraintStatus != PegaNpcCausalStepEvaluationStatus.Available)
            {
                return routeConstraintStatus;
            }

            float travelSeconds = routeEstimate.RouteLengthMeters /
                routeEstimate.EffectiveSpeedMetersPerSecond;
            estimatedSeconds = travelSeconds + candidate.IntrinsicDurationSeconds;
            return IsNonNegativeFinite(estimatedSeconds)
                ? PegaNpcCausalStepEvaluationStatus.Available
                : PegaNpcCausalStepEvaluationStatus.Unknown;
        }

        private static PegaNpcCausalStepEvaluationStatus GetRouteEvaluationStatus(
            PegaNpcPlanningRouteStatus routeStatus) =>
            routeStatus == PegaNpcPlanningRouteStatus.Unreachable ||
            routeStatus == PegaNpcPlanningRouteStatus.Partial ||
            routeStatus == PegaNpcPlanningRouteStatus.Invalid
                ? PegaNpcCausalStepEvaluationStatus.Unavailable
                : PegaNpcCausalStepEvaluationStatus.Unknown;

        protected virtual PegaNpcCausalStepEvaluationStatus EvaluateCausalNavigationConstraints(
            PegaNpcGoapKnownTarget target,
            Vector3 sourcePosition,
            PegaNpcNavigationRouteEstimate routeEstimate,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            return PegaNpcCausalStepEvaluationStatus.Available;
        }

        protected bool TryGetHypotheticalNavigationStart(
            IGoapHypotheticalStateView state,
            PegaNpcCausalPlanningKnowledge knowledge,
            out Vector3 sourcePosition,
            out bool hasProjectedPosition,
            out string diagnostic)
        {
            sourcePosition = default;
            hasProjectedPosition = false;
            diagnostic = string.Empty;
            if (state.ProjectedEffects.Count == 0)
            {
                sourcePosition = ActorTransform.position;
                return true;
            }

            for (int effectIndex = 0; effectIndex < state.ProjectedEffects.Count; effectIndex++)
            {
                if (!(state.ProjectedEffects[effectIndex] is PegaNpcTargetReachedEffect reachedEffect) ||
                    !knowledge.TryGetKnownTarget(reachedEffect.TargetId, out PegaNpcGoapKnownTarget reachedTarget) ||
                    reachedTarget.Target.Kind != reachedEffect.TargetKind ||
                    (IsObjectEntryPositionIdentity(reachedTarget.Target.TargetId.IdentityKind) &&
                     !reachedEffect.HasTargetPosition) ||
                    (reachedEffect.HasTargetPosition &&
                     !reachedTarget.Target.KnownPosition.Equals(reachedEffect.TargetPosition)) ||
                    !IsEffectEvidenceTimeCompatible(
                        reachedTarget.Target.TargetId.IdentityKind,
                        reachedTarget.Target.EvidenceTimeSeconds,
                        reachedEffect.EvidenceTimeSeconds))
                {
                    diagnostic = "The projected state contains Effects that the navigation evaluator cannot resolve to a legitimate known position.";
                    return false;
                }

                sourcePosition = reachedTarget.Target.KnownPosition;
                hasProjectedPosition = true;
            }

            if (!hasProjectedPosition)
            {
                diagnostic = "The projected state does not establish a hypothetical navigation position.";
                return false;
            }

            return true;
        }

        public virtual bool TryGetObservedCausalEffects(
            PegaNpcPlanSnapshot plan,
            PegaNpcObservedActionEffect observedEffect,
            out System.Collections.Generic.IReadOnlyList<IGoapEffect> observedEffects,
            out string diagnostic)
        {
            observedEffects = Array.Empty<IGoapEffect>();
            PegaNpcActionContract contract = Contract;
            if (plan.ActionId != contract.ActionId ||
                !observedEffect.IsValid ||
                observedEffect.RequestId <= 0 ||
                observedEffect.Kind != contract.EffectKind ||
                observedEffect.TargetId != plan.TargetId ||
                observedEffect.TargetKind != plan.TargetKind ||
                !IsNonNegativeFinite(observedEffect.ActualDistanceToTargetMeters) ||
                !IsNonNegativeFinite(observedEffect.TargetRadiusMeters) ||
                observedEffect.ActualDistanceToTargetMeters > observedEffect.TargetRadiusMeters)
            {
                diagnostic = "The observed navigation result does not confirm the selected causal Target-reached Effect.";
                return false;
            }

            for (int index = 0; index < plan.CausalEffects.Count; index++)
            {
                if (plan.CausalEffects[index] is PegaNpcTargetReachedEffect declaredEffect &&
                    declaredEffect.TargetId == observedEffect.TargetId &&
                    declaredEffect.TargetKind == observedEffect.TargetKind &&
                    (declaredEffect.HasTargetPosition
                        ? declaredEffect.TargetPosition.Equals(observedEffect.TargetPosition)
                        : !IsObjectEntryPositionIdentity(observedEffect.TargetId.IdentityKind)) &&
                    Math.Abs(declaredEffect.EvidenceTimeSeconds - plan.EvidenceTimeSeconds) <= 0.000001d)
                {
                    observedEffects = Array.AsReadOnly<IGoapEffect>(new[] { declaredEffect });
                    diagnostic = string.Empty;
                    return true;
                }
            }

            diagnostic = "The successful navigation outcome has no matching Target-reached Effect in the selected Action definition.";
            return false;
        }

        public abstract bool CanOfferCandidate(
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcPlanContinuation? continuation);

        protected bool CandidateMatchesContract(PegaNpcPlanningActionCandidate candidate) =>
            candidate.ActionId == Contract.ActionId &&
            candidate.PredictedEffect == Contract.EffectKind &&
            candidate.RequiredTargetKind == Contract.TargetKind;

        public abstract bool IsCurrentPlanValid(
            PegaNpcPlanSnapshot plan,
            PegaNpcPlanContinuation? continuation,
            out Vector3 currentKnownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic);

        public abstract bool TryStart(
            PegaNpcPlanSnapshot plan,
            PegaNpcPlanContinuation? continuation,
            out string diagnostic);

        public virtual bool IsEffectAlreadySatisfiedAtPosition(PegaNpcPlanSnapshot plan, Vector3 actorPosition)
        {
            PegaNpcActionEffectDeclaration effect = DeclaredEffect;
            float targetRadiusMeters = !Contract.RequiresNavigation &&
                plan.TargetRadiusOverrideMeters.HasValue
                    ? plan.TargetRadiusOverrideMeters.Value
                    : ConfiguredArrivalRadiusMeters;
            if (!effect.IsTargetResolved || effect.Kind != plan.PredictedEffect ||
                !IsNonNegativeFinite(targetRadiusMeters))
            {
                return false;
            }

            Vector3 delta = actorPosition - plan.TargetPosition;
            delta.y = 0f;
            return delta.magnitude <= targetRadiusMeters;
        }

        public bool CanPreserveObservedEffect(IGoapEffect effect)
        {
            if (!(effect is PegaNpcTargetReachedEffect reached) ||
                reached.TargetKind != Contract.TargetKind ||
                !reached.TargetId.ObjectEntryId.IsValid ||
                !reached.HasTargetPosition)
            {
                return false;
            }

            return Contract.TargetKind == PegaNpcPlanningTargetKind.GroupObjectEntryPosition
                ? reached.TargetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition &&
                  reached.KnowledgeGroupId.IsValid
                : Contract.TargetKind == PegaNpcPlanningTargetKind.IndividualObjectEntryPosition &&
                  reached.TargetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition &&
                  reached.TargetId.KnowledgeActorId.IsValid;
        }

        public bool IsObservedEffectCheckpointValid(
            IGoapEffect effect,
            IGoapKnowledgeStateView knowledgeState,
            Vector3 actorPosition,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!(effect is PegaNpcTargetReachedEffect reached) || !CanPreserveObservedEffect(effect))
            {
                diagnostic = "The checkpoint is not a typed Object Entry arrival backed by this Action's Knowledge scope.";
                return false;
            }

            Vector3 knownPosition = default;
            double currentEvidenceTimeSeconds;
            bool isAuthorized;
            if (Contract.TargetKind == PegaNpcPlanningTargetKind.GroupObjectEntryPosition)
            {
                if (!PegaPoiGroupKnowledge.TryGetReader(knowledgeState, out PegaActorGroupKnowledgeReader reader))
                {
                    diagnostic = "The Group position checkpoint has no composed Group Knowledge Reader.";
                    return false;
                }

                PegaActorGroupKnowledgeReadResult<Vector3> position = reader.ReadGroupFact(
                    reached.KnowledgeGroupId,
                    PegaObjectEntryKnowledgeFacts.Position.For(reached.ObjectEntryId),
                    Time.timeAsDouble);
                isAuthorized = position.IsAuthorized && position.Fact.Scope == PegaKnowledgeScope.Group &&
                               position.Fact.TryGetValue(out knownPosition) && position.Fact.HasObservationTime;
                currentEvidenceTimeSeconds = isAuthorized ? position.Fact.ObservationTimeSeconds : 0d;
            }
            else
            {
                if (!TryGetComposedIndividualKnowledge(knowledgeState, out PegaNpcKnowledgeState individualKnowledge) ||
                    !TryGetActorId(out ActorId actorId) || reached.TargetId.KnowledgeActorId != actorId)
                {
                    diagnostic = "The Individual position checkpoint is not authorized by this Actor's composed Individual Knowledge and identity.";
                    return false;
                }

                PegaKnowledgeFact<Vector3> position = individualKnowledge.ReadFact(
                    PegaObjectEntryKnowledgeFacts.Position.For(reached.ObjectEntryId),
                    Time.timeAsDouble);
                isAuthorized = position.Scope == PegaKnowledgeScope.Individual &&
                               position.TryGetValue(out knownPosition) &&
                               position.HasObservationTime &&
                               position.HasOrigin(PegaNpcEvidenceOrigin.Perception);
                currentEvidenceTimeSeconds = isAuthorized ? position.ObservationTimeSeconds : 0d;
            }

            Vector3 delta = actorPosition - reached.TargetPosition;
            delta.y = 0f;
            if (!isAuthorized ||
                !knownPosition.Equals(reached.TargetPosition) ||
                !IsEffectEvidenceTimeCompatible(
                    reached.TargetId.IdentityKind,
                    currentEvidenceTimeSeconds,
                    reached.EvidenceTimeSeconds) ||
                delta.magnitude > EffectiveTargetRadiusMeters)
            {
                diagnostic = "The Knowledge authority, current position evidence or Actor's physical arrival checkpoint is no longer current.";
                return false;
            }

            return true;
        }

        public bool IsTargetPositionWithinTolerance(Vector3 left, Vector3 right)
        {
            float toleranceMeters = DeclaredEffect.TargetRadiusMeters;
            if (!IsNonNegativeFinite(toleranceMeters))
            {
                return false;
            }

            Vector3 delta = left - right;
            delta.y = 0f;
            return delta.magnitude <= toleranceMeters;
        }

        public bool TryUpdateExecutionTarget(
            PegaNpcPlanSnapshot currentPlan,
            PegaNpcPlanSnapshot updatedPlan,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (Outcome != PegaNpcActionOutcome.Running || _navigationRequestId <= 0 ||
                !_hasNavigationSnapshot || _navigationSnapshot.RequestId != _navigationRequestId ||
                currentPlan.ActionId != Contract.ActionId || updatedPlan.ActionId != Contract.ActionId ||
                currentPlan.DesireId != updatedPlan.DesireId ||
                currentPlan.Purpose != updatedPlan.Purpose ||
                currentPlan.PredictedEffect != updatedPlan.PredictedEffect ||
                currentPlan.TargetId != updatedPlan.TargetId ||
                currentPlan.TargetKind != updatedPlan.TargetKind ||
                currentPlan.TargetProvenance != updatedPlan.TargetProvenance ||
                currentPlan.SpeedRegime != updatedPlan.SpeedRegime ||
                updatedPlan.EvidenceTimeSeconds < currentPlan.EvidenceTimeSeconds ||
                Mathf.Abs(currentPlan.MaximumRouteDistanceFromAnchorMeters -
                          updatedPlan.MaximumRouteDistanceFromAnchorMeters) > 0.0001f ||
                !IsFinite(updatedPlan.TargetPosition) ||
                (_effectTarget - currentPlan.TargetPosition).sqrMagnitude > 0.000001f)
            {
                diagnostic = "The target update does not preserve the current Action decision, target identity and correlated navigation request.";
                return false;
            }

            if (NavigationSource == null || !NavigationSource.IsReady ||
                !NavigationSource.TryUpdateNavigationTarget(
                    _navigationRequestId,
                    updatedPlan.TargetPosition,
                    EffectiveTargetRadiusMeters,
                    out diagnostic))
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The active navigation source could not update its current request target.";
                }

                return false;
            }

            _effectTarget = updatedPlan.TargetPosition;
            _effectTargetId = updatedPlan.TargetId;
            _hasEffectTarget = true;
            _log.Debug(
                "NPC navigation Action updated the destination of its current execution request.",
                LogFields.Field("action", GetType().Name),
                LogFields.Field("actorRoot", ActorRoot.name),
                LogFields.Field("requestId", _executionId),
                LogFields.Field("navigationRequestId", _navigationRequestId),
                LogFields.Field("previousTargetPosition", currentPlan.TargetPosition),
                LogFields.Field("updatedTargetPosition", updatedPlan.TargetPosition));
            return true;
        }

        protected float ArrivalRadiusMeters => EffectiveTargetRadiusMeters;

        protected bool TryDeferNavigationRoute(
            PegaActorMovementSpeedRegime speedRegime,
            out PegaNpcNavigationRouteEstimate estimate,
            out string diagnostic)
        {
            estimate = default;
            if (!IsRuntimeActive || NavigationSource == null ||
                NavigationSource.gameObject != ActorRoot || !NavigationSource.IsReady)
            {
                diagnostic = "The Action requires an active NavMesh movement source on the same NPC root.";
                return false;
            }

            if (!IsNonNegativeFinite(arrivalToleranceMeters))
            {
                diagnostic = "Arrival radius must be finite and non-negative.";
                return false;
            }

            return NavigationSource.TryCreateDeferredRouteEstimate(
                speedRegime,
                out estimate,
                out diagnostic);
        }

        protected bool TryEstimateNavigationRoute(
            Vector3 targetPosition,
            PegaActorMovementSpeedRegime speedRegime,
            out PegaNpcNavigationRouteEstimate estimate,
            out string diagnostic)
        {
            estimate = default;
            if (!IsRuntimeActive || NavigationSource == null ||
                NavigationSource.gameObject != ActorRoot || !NavigationSource.IsReady)
            {
                diagnostic = "The Action requires an active NavMesh movement source on the same NPC root.";
                return false;
            }

            return NavigationSource.TryEstimateRoute(
                targetPosition,
                speedRegime,
                arrivalToleranceMeters,
                out estimate,
                out diagnostic);
        }

        protected virtual bool ArePreconditionsStillValid(out string diagnostic)
        {
            diagnostic = string.Empty;
            return true;
        }

        protected virtual bool IsActionSpecificEffectObserved(out string diagnostic)
        {
            diagnostic = string.Empty;
            return true;
        }

        protected virtual bool IsExpectedPreconditionInvalidation() => false;

        protected virtual void OnExecutionTerminated(
            PegaNpcActionOutcome outcome,
            string diagnostic)
        {
        }

        protected virtual void TickNonNavigationAction()
        {
        }

        protected long CurrentExecutionId => _executionId;

        protected bool TryStartStationaryAction(
            PegaNpcPlanningTargetId targetId,
            Vector3 targetPosition,
            out string diagnostic)
        {
            return TryStartStationaryAction(
                targetId,
                targetPosition,
                arrivalToleranceMeters,
                out diagnostic);
        }

        protected bool TryStartStationaryAction(
            PegaNpcPlanningTargetId targetId,
            Vector3 targetPosition,
            float targetRadiusOverrideMeters,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (Contract.RequiresNavigation)
            {
                diagnostic = "A stationary Action cannot start through a navigation-required contract.";
                return false;
            }

            if (Outcome == PegaNpcActionOutcome.Running)
            {
                diagnostic = "This Action already owns an active execution.";
                return false;
            }

            ResetExecution();
            _executionTargetRadiusOverrideMeters = targetRadiusOverrideMeters;
            _hasExecutionTargetRadiusOverride = true;
            _executionId = RuntimeContext.Autonomy.AllocateActionExecutionId();
            _effectTargetId = targetId;
            _effectTarget = targetPosition;
            _hasEffectTarget = targetId.IsValid && IsFinite(targetPosition);
            if (!IsRuntimeActive || !_hasEffectTarget ||
                !IsNonNegativeFinite(EffectiveTargetRadiusMeters))
            {
                diagnostic = "The stationary Action requires an active runtime, valid known Target and finite arrival radius.";
                CompleteWithoutNavigation(PegaNpcActionOutcome.Invalid, targetPosition, diagnostic);
                return false;
            }

            float distance = GetPlanarDistance(ActorTransform.position, targetPosition);
            if (!IsNonNegativeFinite(distance) || distance > EffectiveTargetRadiusMeters)
            {
                diagnostic = "The Actor must physically be within the configured POI radius before this Action starts.";
                CompleteWithoutNavigation(PegaNpcActionOutcome.Invalid, targetPosition, diagnostic);
                return false;
            }

            Outcome = PegaNpcActionOutcome.Running;
            _executionStartedAtTimeSeconds = Time.timeAsDouble;
            _diagnostic = string.Empty;
            _log.Debug(
                "NPC non-navigation Action started.",
                LogFields.Field("action", GetType().Name),
                LogFields.Field("actorRoot", ActorRoot.name),
                LogFields.Field("requestId", _executionId),
                LogFields.Field("arrivalRadiusMeters", EffectiveTargetRadiusMeters),
                LogFields.Field("declaredEffect", ActionEffectKind),
                LogFields.Field("targetX", targetPosition.x),
                LogFields.Field("targetY", targetPosition.y),
                LogFields.Field("targetZ", targetPosition.z));
            return true;
        }

        protected void CompleteAction(
            PegaNpcActionOutcome outcome,
            string diagnostic,
            bool expectedPreconditionInvalidation = false)
        {
            if (Contract.RequiresNavigation)
            {
                throw new InvalidOperationException("CompleteAction is only valid for a non-navigation Action contract.");
            }

            Complete(
                outcome,
                diagnostic,
                PegaNpcNavigationOutcome.Invalid,
                expectedPreconditionInvalidation);
        }

        protected override bool OnRuntimeInitialize(out string diagnostic)
        {
            EnsureLoggerInitialized();
            if (Contract.RequiresNavigation &&
                (NavigationSource == null || NavigationSource.gameObject != ActorRoot))
            {
                diagnostic = "The navigation Action requires the Actor's NavMesh movement source.";
                return false;
            }

            diagnostic = string.Empty;
            return base.OnRuntimeInitialize(out diagnostic);
        }

        protected override void OnRuntimeDisabled()
        {
            if (Outcome == PegaNpcActionOutcome.Running)
            {
                Terminate(
                    PegaNpcActionOutcome.Cancelled,
                    "Action runtime was disabled while executing.",
                    PegaNpcNavigationOutcome.Cancelled);
            }

            base.OnRuntimeDisabled();
        }

        protected bool TryStartNavigation(
            PegaNpcPlanningTargetId targetId,
            Vector3 targetPosition,
            PegaActorMovementSpeedRegime speedRegime,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (Outcome == PegaNpcActionOutcome.Running)
            {
                diagnostic = "This Action already owns an active navigation request.";
                return false;
            }

            ResetExecution();
            _effectTargetId = targetId;
            _effectTarget = targetPosition;
            _hasEffectTarget = IsFinite(targetPosition);
            if (!IsRuntimeActive || NavigationSource == null ||
                NavigationSource.gameObject != ActorRoot || !NavigationSource.IsReady)
            {
                diagnostic = "The Action requires an active NavMesh movement source on the same NPC root.";
                CompleteWithoutNavigation(PegaNpcActionOutcome.Invalid, targetPosition, diagnostic);
                return false;
            }

            if (!IsNonNegativeFinite(arrivalToleranceMeters))
            {
                diagnostic = "Arrival radius must be finite and non-negative.";
                CompleteWithoutNavigation(PegaNpcActionOutcome.Invalid, targetPosition, diagnostic);
                return false;
            }

            bool started = NavigationSource.TryBeginNavigation(
                targetPosition,
                speedRegime,
                arrivalToleranceMeters,
                out _navigationRequestId,
                out diagnostic);

            _executionId = RuntimeContext.Autonomy.AllocateActionExecutionId();

            if (_navigationRequestId > 0 && NavigationSource.TryGetSnapshot(
                    _navigationRequestId,
                    out _navigationSnapshot))
            {
                _hasNavigationSnapshot = true;
            }

            if (!started)
            {
                if (_navigationRequestId <= 0)
                {
                    CompleteWithoutNavigation(
                        PegaNpcActionOutcome.Invalid,
                        targetPosition,
                        string.IsNullOrEmpty(diagnostic)
                            ? "Navigation request was rejected."
                            : diagnostic);
                    return false;
                }

                Complete(
                    PegaNpcActionOutcome.Invalid,
                    string.IsNullOrEmpty(diagnostic)
                        ? "Navigation request was rejected."
                        : diagnostic,
                    PegaNpcNavigationOutcome.Invalid);
                return false;
            }

            Outcome = PegaNpcActionOutcome.Running;
            _executionStartedAtTimeSeconds = Time.timeAsDouble;
            _diagnostic = string.Empty;
            _log.Debug(
                "NPC navigation Action started.",
                LogFields.Field("action", GetType().Name),
                LogFields.Field("actorRoot", ActorRoot.name),
                LogFields.Field("requestId", _executionId),
                LogFields.Field("navigationRequestId", _navigationRequestId),
                LogFields.Field("intrinsicDurationSeconds", IntrinsicDurationSeconds),
                LogFields.Field("arrivalRadiusMeters", arrivalToleranceMeters),
                LogFields.Field("speedRegime", speedRegime),
                LogFields.Field("declaredEffect", ActionEffectKind),
                LogFields.Field("targetX", targetPosition.x),
                LogFields.Field("targetY", targetPosition.y),
                LogFields.Field("targetZ", targetPosition.z));
            return true;
        }

        protected void RejectStart(
            string diagnostic,
            bool expectedPreconditionInvalidation = false)
        {
            if (Outcome == PegaNpcActionOutcome.Running)
            {
                return;
            }

            ResetExecution();
            CompleteWithoutNavigation(
                PegaNpcActionOutcome.Invalid,
                Vector3.zero,
                string.IsNullOrEmpty(diagnostic) ? "Action preconditions are not satisfied." : diagnostic,
                expectedPreconditionInvalidation);
        }

        public bool CancelExecution(string diagnostic = "Action owner cancelled execution.") =>
            Terminate(PegaNpcActionOutcome.Cancelled, diagnostic, PegaNpcNavigationOutcome.Cancelled);

        public bool InterruptExecution(string diagnostic = "Configured event interrupted execution.") =>
            Terminate(PegaNpcActionOutcome.Interrupted, diagnostic, PegaNpcNavigationOutcome.Interrupted);

        protected override void OnRuntimeTick()
        {
            if (Outcome != PegaNpcActionOutcome.Running)
            {
                return;
            }

            if (!ArePreconditionsStillValid(out string preconditionDiagnostic))
            {
                Complete(
                    PegaNpcActionOutcome.Invalid,
                    string.IsNullOrEmpty(preconditionDiagnostic)
                        ? "Action preconditions stopped being valid during execution."
                        : preconditionDiagnostic,
                    PegaNpcNavigationOutcome.Invalid,
                    IsExpectedPreconditionInvalidation());
                return;
            }

            if (!Contract.RequiresNavigation)
            {
                TickNonNavigationAction();
                return;
            }

            if (NavigationSource == null || !NavigationSource.IsReady || !NavigationSource.TryGetSnapshot(
                    _navigationRequestId,
                    out _navigationSnapshot))
            {
                Complete(
                    PegaNpcActionOutcome.Invalid,
                    "The active navigation request no longer has an observable snapshot.",
                    PegaNpcNavigationOutcome.Invalid);
                return;
            }

            _hasNavigationSnapshot = true;
            if (_navigationSnapshot.Outcome == PegaNpcNavigationOutcome.Invalid)
            {
                Complete(
                    PegaNpcActionOutcome.Invalid,
                    _navigationSnapshot.Diagnostic,
                    PegaNpcNavigationOutcome.Invalid);
                return;
            }

            if (_navigationSnapshot.Outcome != PegaNpcNavigationOutcome.Running)
            {
                PegaNpcActionOutcome outcome = _navigationSnapshot.Outcome switch
                {
                    PegaNpcNavigationOutcome.Success => PegaNpcActionOutcome.Success,
                    PegaNpcNavigationOutcome.Cancelled => PegaNpcActionOutcome.Cancelled,
                    PegaNpcNavigationOutcome.Interrupted => PegaNpcActionOutcome.Interrupted,
                    _ => PegaNpcActionOutcome.Invalid
                };
                if (outcome == PegaNpcActionOutcome.Success &&
                    !IsActionSpecificEffectObserved(out string effectDiagnostic))
                {
                    Complete(
                        PegaNpcActionOutcome.Invalid,
                        string.IsNullOrWhiteSpace(effectDiagnostic)
                            ? "The Action-specific completion condition was not observed."
                            : effectDiagnostic,
                        PegaNpcNavigationOutcome.Invalid);
                    return;
                }

                Complete(outcome, _navigationSnapshot.Diagnostic, _navigationSnapshot.Outcome);
                return;
            }

            if (_navigationSnapshot.ActualDistanceToTargetMeters <= arrivalToleranceMeters)
            {
                if (!IsActionSpecificEffectObserved(out string effectDiagnostic))
                {
                    Complete(
                        PegaNpcActionOutcome.Invalid,
                        string.IsNullOrWhiteSpace(effectDiagnostic)
                            ? "The Action-specific completion condition was not observed."
                            : effectDiagnostic,
                        PegaNpcNavigationOutcome.Invalid);
                    return;
                }

                Complete(
                    PegaNpcActionOutcome.Success,
                    "The physical Actor reached the selected navigation Target.",
                    PegaNpcNavigationOutcome.Success);
            }
        }

        private bool Terminate(
            PegaNpcActionOutcome outcome,
            string diagnostic,
            PegaNpcNavigationOutcome navigationOutcome)
        {
            if (Outcome != PegaNpcActionOutcome.Running)
            {
                return false;
            }

            Complete(outcome, diagnostic, navigationOutcome);
            return true;
        }

        private void Complete(
            PegaNpcActionOutcome outcome,
            string diagnostic,
            PegaNpcNavigationOutcome navigationOutcome,
            bool expectedPreconditionInvalidation = false)
        {
            _diagnostic = diagnostic ?? string.Empty;
            _completedAtTimeSeconds = Time.timeAsDouble;
            if (NavigationSource != null && _navigationRequestId > 0)
            {
                NavigationSource.FinishRequest(_navigationRequestId, navigationOutcome, _diagnostic);
            }

            Outcome = outcome;
            OnExecutionTerminated(outcome, _diagnostic);
            PegaNpcActionExecutionSnapshot result = CurrentExecution;
            if (outcome == PegaNpcActionOutcome.Invalid && !expectedPreconditionInvalidation)
            {
                _log.Warning(
                    Contract.RequiresNavigation
                        ? "NPC navigation Action failed before reaching its target."
                        : "NPC non-navigation Action failed before confirming its Effect.",
                    LogFields.Field("actorRoot", ActorRoot.name),
                    LogFields.Field("action", GetType().Name),
                    LogFields.Field("diagnostic", result.Diagnostic));
            }

            _log.Debug(
                "NPC Action finished.",
                LogFields.Field("action", GetType().Name),
                LogFields.Field("actorRoot", ActorRoot.name),
                LogFields.Field("requestId", result.RequestId),
                LogFields.Field("result", result.Outcome),
                LogFields.Field("declaredEffect", result.DeclaredEffect.Kind),
                LogFields.Field("effectTargetResolved", result.DeclaredEffect.IsTargetResolved),
                LogFields.Field("effectObserved", result.EffectObserved),
                LogFields.Field("effectTargetRadiusMeters", result.DeclaredEffect.TargetRadiusMeters),
                LogFields.Field("effectTargetX", result.DeclaredEffect.TargetPosition.x),
                LogFields.Field("effectTargetY", result.DeclaredEffect.TargetPosition.y),
                LogFields.Field("effectTargetZ", result.DeclaredEffect.TargetPosition.z),
                LogFields.Field("targetX", result.TargetPosition.x),
                LogFields.Field("targetY", result.TargetPosition.y),
                LogFields.Field("targetZ", result.TargetPosition.z),
                LogFields.Field("hasNavigationSnapshot", _hasNavigationSnapshot),
                LogFields.Field("hasPath", _hasNavigationSnapshot && _navigationSnapshot.HasPath),
                LogFields.Field("pathPending", _hasNavigationSnapshot && _navigationSnapshot.PathPending),
                LogFields.Field("pathStatus", _hasNavigationSnapshot ? _navigationSnapshot.PathStatus : NavMeshPathStatus.PathInvalid),
                LogFields.Field("routeLengthMeters", _hasNavigationSnapshot ? _navigationSnapshot.RouteLengthMeters : 0f),
                LogFields.Field("effectiveSpeedMps", _hasNavigationSnapshot ? _navigationSnapshot.EffectiveSpeedMetersPerSecond : 0f),
                LogFields.Field("actualDistanceToTargetMeters", _hasNavigationSnapshot ? _navigationSnapshot.ActualDistanceToTargetMeters : 0f),
                LogFields.Field("actualSpeedMps", _hasNavigationSnapshot ? _navigationSnapshot.ActualSpeedMetersPerSecond : 0f),
                LogFields.Field("maximumUnsynchronizedPositionOffsetMeters", _hasNavigationSnapshot ? _navigationSnapshot.MaximumUnsynchronizedPositionOffsetMeters : 0f),
                LogFields.Field("maximumPositionReconciliationResidualMeters", _hasNavigationSnapshot ? _navigationSnapshot.MaximumPositionReconciliationResidualMeters : 0f),
                LogFields.Field("diagnostic", result.Diagnostic));
            PublishTerminalOutcome(result);
        }

        private void CompleteWithoutNavigation(
            PegaNpcActionOutcome outcome,
            Vector3 targetPosition,
            string diagnostic,
            bool expectedPreconditionInvalidation = false)
        {
            _diagnostic = diagnostic ?? string.Empty;
            _completedAtTimeSeconds = Time.timeAsDouble;
            _navigationSnapshot = default;
            _hasNavigationSnapshot = false;
            Outcome = outcome;
            OnExecutionTerminated(outcome, _diagnostic);
            PegaNpcActionEffectDeclaration declaredEffect = DeclaredEffect;
            LogField[] fields =
            {
                LogFields.Field("action", GetType().Name),
                LogFields.Field("actorRoot", ActorRoot.name),
                LogFields.Field("result", outcome),
                LogFields.Field("declaredEffect", declaredEffect.Kind),
                LogFields.Field("effectTargetResolved", declaredEffect.IsTargetResolved),
                LogFields.Field("effectObserved", false),
                LogFields.Field("effectTargetRadiusMeters", declaredEffect.TargetRadiusMeters),
                LogFields.Field("targetX", targetPosition.x),
                LogFields.Field("targetY", targetPosition.y),
                LogFields.Field("targetZ", targetPosition.z),
                LogFields.Field("expectedPreconditionInvalidation", expectedPreconditionInvalidation),
                LogFields.Field("diagnostic", _diagnostic)
            };
            if (outcome == PegaNpcActionOutcome.Invalid && expectedPreconditionInvalidation)
            {
                _log.Info("NPC Action start was rejected by a current gameplay precondition.", fields);
            }
            else
            {
                _log.Error("NPC Action could not start.", fields);
            }
            PublishTerminalOutcome(CurrentExecution);
        }

        private void PublishTerminalOutcome(PegaNpcActionExecutionSnapshot execution)
        {
            if (_terminalOutcomeEvents == null)
            {
                return;
            }

            _terminalOutcomeEvents.Publish(new PegaNpcActionTerminalOutcomeEvent(
                execution,
                _completedAtTimeSeconds));
        }

        private void EnsureTerminalOutcomeEventsInitialized()
        {
            _terminalOutcomeEvents ??= new EventBus<PegaNpcActionTerminalOutcomeEvent>();
        }

        private void LogSubscriberFailure(
            Action<PegaNpcActionTerminalOutcomeEvent> handler,
            PegaNpcActionTerminalOutcomeEvent outcome,
            Exception exception)
        {
            EnsureLoggerInitialized();
            _log.Error(
                "NPC terminal Action subscriber failed; remaining subscribers will still be notified.",
                LogFields.Field("action", GetType().Name),
                LogFields.Field("actorRoot", ActorRoot.name),
                LogFields.Field("requestId", outcome.Execution.RequestId),
                LogFields.Field("result", outcome.Execution.Outcome),
                LogFields.Field("subscriberMethod", handler.Method.Name),
                LogFields.Field("exceptionType", exception.GetType().FullName),
                LogFields.Field("exceptionMessage", exception.Message));
        }

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PegaLogging.CreateScopedLogger<PegaNpcActionProviderAsset>(LogCategory, ActorRoot.name);
            _loggerInitialized = true;
        }

        private void ResetExecution()
        {
            _executionId = 0;
            _navigationRequestId = 0;
            _hasNavigationSnapshot = false;
            _navigationSnapshot = default;
            _hasEffectTarget = false;
            _effectTarget = default;
            _effectTargetId = default;
            _executionTargetRadiusOverrideMeters = 0f;
            _hasExecutionTargetRadiusOverride = false;
            _completedAtTimeSeconds = 0d;
            _executionStartedAtTimeSeconds = 0d;
            _diagnostic = string.Empty;
            Outcome = PegaNpcActionOutcome.NotStarted;
        }

        private static bool IsObjectEntryPositionIdentity(PegaNpcPlanningTargetIdentityKind identityKind) =>
            identityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition ||
            identityKind == PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition;

        private static bool IsEffectEvidenceTimeCompatible(
            PegaNpcPlanningTargetIdentityKind identityKind,
            double currentEvidenceTimeSeconds,
            double effectEvidenceTimeSeconds) =>
            identityKind == PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition
                ? currentEvidenceTimeSeconds >= effectEvidenceTimeSeconds - 0.000001d
                : Math.Abs(currentEvidenceTimeSeconds - effectEvidenceTimeSeconds) <= 0.000001d;

        private bool TryGetComposedIndividualKnowledge(
            IGoapKnowledgeStateView composedKnowledgeState,
            out PegaNpcKnowledgeState knowledgeState)
        {
            knowledgeState = null;
            if (composedKnowledgeState == null || composedKnowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                return false;
            }

            for (int index = 0; index < composedKnowledgeState.Capabilities.Count; index++)
            {
                if (!(composedKnowledgeState.Capabilities[index] is PegaNpcKnowledgeState candidate))
                {
                    continue;
                }

                if (knowledgeState != null && knowledgeState != candidate)
                {
                    return false;
                }

                knowledgeState = candidate;
            }

            return knowledgeState != null &&
                   knowledgeState.gameObject == ActorRoot &&
                   knowledgeState.Scope == PegaKnowledgeScope.Individual;
        }

        private bool TryGetActorId(out ActorId actorId)
        {
            actorId = default;
            ActorDeclaration declaration = ActorRoot != null
                ? ActorRoot.GetComponent<ActorDeclaration>()
                : null;
            if (declaration == null)
            {
                return false;
            }

            try
            {
                actorId = declaration.ActorId;
                return actorId.IsValid;
            }
            catch (ArgumentException)
            {
                actorId = default;
                return false;
            }
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static float GetPlanarDistance(Vector3 left, Vector3 right)
        {
            Vector3 delta = left - right;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

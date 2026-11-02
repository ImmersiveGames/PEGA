using System;
using Immersive.Foundation.Events;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using ImmersiveGames.Goap;
using PEGA.Actors;
using PEGA.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace PEGA.NPC
{
    // Cada Action concreta é uma definição compartilhada; PEGANpcAutonomy cria um clone runtime por Actor.
    public abstract class PEGANpcNavigationAction : PEGANpcActionDefinitionAsset,
        IPEGANpcActionProvider,
        IPEGANpcExecutionTargetUpdater,
        IPEGANpcCausalActionStepEvaluator
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.Actions";

        [SerializeField, Min(0f)]
        [FormerlySerializedAs("arrivalRadiusMeters")]
        [Tooltip("Maximum distance from the selected destination that counts as arrival (m).")]
        private float arrivalToleranceMeters = 1.25f;

        private ScopedLogger _log;
        private long _requestId;
        private bool _hasNavigationSnapshot;
        private PEGANpcNavigationSnapshot _navigationSnapshot;
        private bool _hasEffectTarget;
        private Vector3 _effectTarget;
        private PEGANpcPlanningTargetId _effectTargetId;
        private double _completedAtTimeSeconds;
        private string _diagnostic = string.Empty;

        [NonSerialized]
        private bool _loggerInitialized;

        [NonSerialized]
        private EventBus<PEGANpcActionTerminalOutcomeEvent> _terminalOutcomeEvents;

        protected PEGANpcNavMeshMovementIntentSource NavigationSource =>
            RuntimeContext != null ? RuntimeContext.NavigationSource : null;

        public float ConfiguredArrivalRadiusMeters => arrivalToleranceMeters;

        protected abstract PEGANpcActionEffectKind ActionEffectKind { get; }

        public abstract PEGANpcActionContract Contract { get; }

        public PEGANpcActionOutcome Outcome { get; private set; } = PEGANpcActionOutcome.NotStarted;

        public PEGANpcActionExecutionSnapshot CurrentExecution
        {
            get
            {
                bool effectObserved = TryGetObservedEffect(out _);
                return new PEGANpcActionExecutionSnapshot(
                    Outcome,
                    _requestId,
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
        public IEventBinding SubscribeToTerminalOutcomes(Action<PEGANpcActionTerminalOutcomeEvent> handler)
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

        public PEGANpcActionEffectDeclaration DeclaredEffect => new PEGANpcActionEffectDeclaration(
            ActionEffectKind,
            _effectTargetId,
            Contract.TargetKind,
            _hasEffectTarget,
            _effectTarget,
            arrivalToleranceMeters);

        public bool TryGetObservedEffect(out PEGANpcObservedActionEffect observedEffect)
        {
            observedEffect = default;
            if (Outcome != PEGANpcActionOutcome.Success || !_hasNavigationSnapshot)
            {
                return false;
            }

            PEGANpcActionEffectDeclaration declaration = DeclaredEffect;
            float actualDistance = _navigationSnapshot.ActualDistanceToTargetMeters;
            if (!declaration.Kind.IsValid ||
                !declaration.IsTargetResolved ||
                !declaration.TargetId.IsValid ||
                !declaration.TargetKind.IsValid ||
                _navigationSnapshot.RequestId <= 0 ||
                !IsFinite(declaration.TargetPosition) ||
                !IsNonNegativeFinite(declaration.TargetRadiusMeters) ||
                !IsNonNegativeFinite(actualDistance) ||
                actualDistance > declaration.TargetRadiusMeters ||
                (_navigationSnapshot.TargetPosition - declaration.TargetPosition).sqrMagnitude > 0.000001f ||
                !IsActionSpecificEffectObserved(out _))
            {
                return false;
            }

            observedEffect = new PEGANpcObservedActionEffect(
                declaration.Kind,
                declaration.TargetId,
                declaration.TargetKind,
                _navigationSnapshot.RequestId,
                _navigationSnapshot.TargetPosition,
                declaration.TargetRadiusMeters,
                actualDistance,
                _completedAtTimeSeconds);
            return true;
        }

        public float IntrinsicDurationSeconds => 0f;

        public abstract bool TryCreatePlanningCandidates(
            PEGANpcActionPlanningContext context,
            out System.Collections.Generic.IReadOnlyList<PEGANpcPlanningTarget> targets,
            out System.Collections.Generic.IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
            out string diagnostic);

        /// <summary>
        /// Candidate generation may defer a route that is unavailable from the current position. The causal
        /// step evaluator checks route availability and cost against each preceding hypothetical position.
        /// Success still projects only generic arrival at the bound Target.
        /// </summary>
        public virtual bool TryCreateCausalActionDefinition(
            PEGANpcPlanningActionCandidate candidate,
            PEGANpcGoapKnownTarget target,
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

            actionDefinition = new GoapActionDefinition(
                Array.Empty<IGoapCondition>(),
                new IGoapEffect[]
                {
                    new PEGANpcTargetReachedEffect(
                        target.Target.TargetId,
                        target.Target.Kind,
                        target.Target.EvidenceTimeSeconds)
                });
            diagnostic = string.Empty;
            return true;
        }

        public virtual PEGANpcCausalStepEvaluationStatus EvaluateCausalStep(
            PEGANpcPlanningActionCandidate candidate,
            IGoapHypotheticalStateView stateBeforeStep,
            PEGANpcCausalPlanningKnowledge knowledge,
            out PEGANpcNavigationRouteEstimate routeEstimate,
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
                return PEGANpcCausalStepEvaluationStatus.Unavailable;
            }

            if (stateBeforeStep == null ||
                stateBeforeStep.Source != GoapStateSource.HypotheticalState ||
                knowledge == null ||
                !knowledge.TryGetKnownTarget(candidate.TargetId, out PEGANpcGoapKnownTarget target) ||
                target.Target.Kind != candidate.RequiredTargetKind)
            {
                diagnostic = "The causal step has no valid hypothetical state or legitimate known Target.";
                return PEGANpcCausalStepEvaluationStatus.Unknown;
            }

            if (!Contract.RequiresNavigation)
            {
                if (stateBeforeStep.ProjectedEffects.Count > 0)
                {
                    diagnostic = "This Action has no state-aware duration evaluator for a projected state.";
                    return PEGANpcCausalStepEvaluationStatus.Unknown;
                }

                routeEstimate = PEGANpcNavigationRouteEstimate.NotRequired;
                estimatedSeconds = candidate.IntrinsicDurationSeconds;
                if (IsNonNegativeFinite(estimatedSeconds))
                {
                    return PEGANpcCausalStepEvaluationStatus.Available;
                }

                diagnostic = "The Action's intrinsic duration is not finite and non-negative.";
                return PEGANpcCausalStepEvaluationStatus.Unknown;
            }

            if (stateBeforeStep.ProjectedEffects.Count > 0 &&
                candidate.TargetId.IdentityKind == PEGANpcPlanningTargetIdentityKind.CharacterSeparationDestination)
            {
                diagnostic = "The separation Target is derived from the current physical position and cannot be reused after a projected move.";
                return PEGANpcCausalStepEvaluationStatus.Unknown;
            }

            if (!TryGetHypotheticalNavigationStart(
                    stateBeforeStep,
                    knowledge,
                    out Vector3 sourcePosition,
                    out bool hasProjectedPosition,
                    out diagnostic))
            {
                return PEGANpcCausalStepEvaluationStatus.Unknown;
            }

            if (NavigationSource == null || NavigationSource.gameObject != ActorRoot)
            {
                diagnostic = "The Navigation Action has no local route evaluator.";
                return PEGANpcCausalStepEvaluationStatus.Unknown;
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

            if (routeEstimate.Status != PEGANpcPlanningRouteStatus.Complete ||
                !IsPositiveFinite(routeEstimate.EffectiveSpeedMetersPerSecond))
            {
                diagnostic = "The route evaluator did not return a complete route and usable speed.";
                return PEGANpcCausalStepEvaluationStatus.Unknown;
            }

            PEGANpcCausalStepEvaluationStatus routeConstraintStatus = EvaluateCausalNavigationConstraints(
                target,
                sourcePosition,
                routeEstimate,
                out diagnostic);
            if (routeConstraintStatus != PEGANpcCausalStepEvaluationStatus.Available)
            {
                return routeConstraintStatus;
            }

            float travelSeconds = routeEstimate.RouteLengthMeters /
                routeEstimate.EffectiveSpeedMetersPerSecond;
            estimatedSeconds = travelSeconds + candidate.IntrinsicDurationSeconds;
            return IsNonNegativeFinite(estimatedSeconds)
                ? PEGANpcCausalStepEvaluationStatus.Available
                : PEGANpcCausalStepEvaluationStatus.Unknown;
        }

        private static PEGANpcCausalStepEvaluationStatus GetRouteEvaluationStatus(
            PEGANpcPlanningRouteStatus routeStatus) =>
            routeStatus == PEGANpcPlanningRouteStatus.Unreachable ||
            routeStatus == PEGANpcPlanningRouteStatus.Partial ||
            routeStatus == PEGANpcPlanningRouteStatus.Invalid
                ? PEGANpcCausalStepEvaluationStatus.Unavailable
                : PEGANpcCausalStepEvaluationStatus.Unknown;

        protected virtual PEGANpcCausalStepEvaluationStatus EvaluateCausalNavigationConstraints(
            PEGANpcGoapKnownTarget target,
            Vector3 sourcePosition,
            PEGANpcNavigationRouteEstimate routeEstimate,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            return PEGANpcCausalStepEvaluationStatus.Available;
        }

        protected bool TryGetHypotheticalNavigationStart(
            IGoapHypotheticalStateView state,
            PEGANpcCausalPlanningKnowledge knowledge,
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
                if (!(state.ProjectedEffects[effectIndex] is PEGANpcTargetReachedEffect reachedEffect) ||
                    !knowledge.TryGetKnownTarget(reachedEffect.TargetId, out PEGANpcGoapKnownTarget reachedTarget) ||
                    reachedTarget.Target.Kind != reachedEffect.TargetKind ||
                    Math.Abs(reachedTarget.Target.EvidenceTimeSeconds - reachedEffect.EvidenceTimeSeconds) > 0.000001d)
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
            PEGANpcPlanSnapshot plan,
            PEGANpcObservedActionEffect observedEffect,
            out System.Collections.Generic.IReadOnlyList<IGoapEffect> observedEffects,
            out string diagnostic)
        {
            observedEffects = Array.Empty<IGoapEffect>();
            PEGANpcActionContract contract = Contract;
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
                if (plan.CausalEffects[index] is PEGANpcTargetReachedEffect declaredEffect &&
                    declaredEffect.TargetId == observedEffect.TargetId &&
                    declaredEffect.TargetKind == observedEffect.TargetKind &&
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
            PEGANpcPlanningActionCandidate candidate,
            PEGANpcPlanContinuation? continuation);

        protected bool CandidateMatchesContract(PEGANpcPlanningActionCandidate candidate) =>
            candidate.ActionId == Contract.ActionId &&
            candidate.PredictedEffect == Contract.EffectKind &&
            candidate.RequiredTargetKind == Contract.TargetKind;

        public abstract bool IsCurrentPlanValid(
            PEGANpcPlanSnapshot plan,
            PEGANpcPlanContinuation? continuation,
            out Vector3 currentKnownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic);

        public abstract bool TryStart(
            PEGANpcPlanSnapshot plan,
            PEGANpcPlanContinuation? continuation,
            out string diagnostic);

        public virtual bool IsEffectAlreadySatisfiedAtPosition(PEGANpcPlanSnapshot plan, Vector3 actorPosition)
        {
            PEGANpcActionEffectDeclaration effect = DeclaredEffect;
            if (!effect.IsTargetResolved || effect.Kind != plan.PredictedEffect ||
                !IsNonNegativeFinite(effect.TargetRadiusMeters))
            {
                return false;
            }

            Vector3 delta = actorPosition - plan.TargetPosition;
            delta.y = 0f;
            return delta.magnitude <= effect.TargetRadiusMeters;
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
            PEGANpcPlanSnapshot currentPlan,
            PEGANpcPlanSnapshot updatedPlan,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (Outcome != PEGANpcActionOutcome.Running || _requestId <= 0 ||
                !_hasNavigationSnapshot || _navigationSnapshot.RequestId != _requestId ||
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
                    _requestId,
                    updatedPlan.TargetPosition,
                    arrivalToleranceMeters,
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
                LogFields.Field("requestId", _requestId),
                LogFields.Field("previousTargetPosition", currentPlan.TargetPosition),
                LogFields.Field("updatedTargetPosition", updatedPlan.TargetPosition));
            return true;
        }

        protected float ArrivalRadiusMeters => arrivalToleranceMeters;

        protected bool TryDeferNavigationRoute(
            PEGAActorMovementSpeedRegime speedRegime,
            out PEGANpcNavigationRouteEstimate estimate,
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
            PEGAActorMovementSpeedRegime speedRegime,
            out PEGANpcNavigationRouteEstimate estimate,
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

        protected override bool OnRuntimeInitialize(out string diagnostic)
        {
            EnsureLoggerInitialized();
            if (NavigationSource == null || NavigationSource.gameObject != ActorRoot)
            {
                diagnostic = "The navigation Action requires the Actor's NavMesh movement source.";
                return false;
            }

            diagnostic = string.Empty;
            return base.OnRuntimeInitialize(out diagnostic);
        }

        protected override void OnRuntimeDisabled()
        {
            if (Outcome == PEGANpcActionOutcome.Running)
            {
                Terminate(
                    PEGANpcActionOutcome.Cancelled,
                    "Action runtime was disabled while executing.",
                    PEGANpcNavigationOutcome.Cancelled);
            }

            base.OnRuntimeDisabled();
        }

        protected bool TryStartNavigation(
            PEGANpcPlanningTargetId targetId,
            Vector3 targetPosition,
            PEGAActorMovementSpeedRegime speedRegime,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (Outcome == PEGANpcActionOutcome.Running)
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
                CompleteWithoutNavigation(PEGANpcActionOutcome.Invalid, targetPosition, diagnostic);
                return false;
            }

            if (!IsNonNegativeFinite(arrivalToleranceMeters))
            {
                diagnostic = "Arrival radius must be finite and non-negative.";
                CompleteWithoutNavigation(PEGANpcActionOutcome.Invalid, targetPosition, diagnostic);
                return false;
            }

            bool started = NavigationSource.TryBeginNavigation(
                targetPosition,
                speedRegime,
                arrivalToleranceMeters,
                out _requestId,
                out diagnostic);

            if (_requestId > 0 && NavigationSource.TryGetSnapshot(
                    _requestId,
                    out _navigationSnapshot))
            {
                _hasNavigationSnapshot = true;
            }

            if (!started)
            {
                if (_requestId <= 0)
                {
                    CompleteWithoutNavigation(
                        PEGANpcActionOutcome.Invalid,
                        targetPosition,
                        string.IsNullOrEmpty(diagnostic)
                            ? "Navigation request was rejected."
                            : diagnostic);
                    return false;
                }

                Complete(
                    PEGANpcActionOutcome.Invalid,
                    string.IsNullOrEmpty(diagnostic)
                        ? "Navigation request was rejected."
                        : diagnostic,
                    PEGANpcNavigationOutcome.Invalid);
                return false;
            }

            Outcome = PEGANpcActionOutcome.Running;
            _diagnostic = string.Empty;
            _log.Debug(
                "NPC navigation Action started.",
                LogFields.Field("action", GetType().Name),
                LogFields.Field("actorRoot", ActorRoot.name),
                LogFields.Field("requestId", _requestId),
                LogFields.Field("intrinsicDurationSeconds", IntrinsicDurationSeconds),
                LogFields.Field("arrivalRadiusMeters", arrivalToleranceMeters),
                LogFields.Field("speedRegime", speedRegime),
                LogFields.Field("declaredEffect", ActionEffectKind),
                LogFields.Field("targetX", targetPosition.x),
                LogFields.Field("targetY", targetPosition.y),
                LogFields.Field("targetZ", targetPosition.z));
            return true;
        }

        protected void RejectStart(string diagnostic)
        {
            if (Outcome == PEGANpcActionOutcome.Running)
            {
                return;
            }

            ResetExecution();
            CompleteWithoutNavigation(
                PEGANpcActionOutcome.Invalid,
                Vector3.zero,
                string.IsNullOrEmpty(diagnostic) ? "Action preconditions are not satisfied." : diagnostic);
        }

        public bool CancelExecution(string diagnostic = "Action owner cancelled execution.") =>
            Terminate(PEGANpcActionOutcome.Cancelled, diagnostic, PEGANpcNavigationOutcome.Cancelled);

        public bool InterruptExecution(string diagnostic = "Configured event interrupted execution.") =>
            Terminate(PEGANpcActionOutcome.Interrupted, diagnostic, PEGANpcNavigationOutcome.Interrupted);

        protected override void OnRuntimeTick()
        {
            if (Outcome != PEGANpcActionOutcome.Running)
            {
                return;
            }

            if (!ArePreconditionsStillValid(out string preconditionDiagnostic))
            {
                Complete(
                    PEGANpcActionOutcome.Invalid,
                    string.IsNullOrEmpty(preconditionDiagnostic)
                        ? "Action preconditions stopped being valid during execution."
                        : preconditionDiagnostic,
                    PEGANpcNavigationOutcome.Invalid,
                    IsExpectedPreconditionInvalidation());
                return;
            }

            if (NavigationSource == null || !NavigationSource.IsReady || !NavigationSource.TryGetSnapshot(
                    _requestId,
                    out _navigationSnapshot))
            {
                Complete(
                    PEGANpcActionOutcome.Invalid,
                    "The active navigation request no longer has an observable snapshot.",
                    PEGANpcNavigationOutcome.Invalid);
                return;
            }

            _hasNavigationSnapshot = true;
            if (_navigationSnapshot.Outcome == PEGANpcNavigationOutcome.Invalid)
            {
                Complete(
                    PEGANpcActionOutcome.Invalid,
                    _navigationSnapshot.Diagnostic,
                    PEGANpcNavigationOutcome.Invalid);
                return;
            }

            if (_navigationSnapshot.Outcome != PEGANpcNavigationOutcome.Running)
            {
                PEGANpcActionOutcome outcome = _navigationSnapshot.Outcome switch
                {
                    PEGANpcNavigationOutcome.Success => PEGANpcActionOutcome.Success,
                    PEGANpcNavigationOutcome.Cancelled => PEGANpcActionOutcome.Cancelled,
                    PEGANpcNavigationOutcome.Interrupted => PEGANpcActionOutcome.Interrupted,
                    _ => PEGANpcActionOutcome.Invalid
                };
                if (outcome == PEGANpcActionOutcome.Success &&
                    !IsActionSpecificEffectObserved(out string effectDiagnostic))
                {
                    Complete(
                        PEGANpcActionOutcome.Invalid,
                        string.IsNullOrWhiteSpace(effectDiagnostic)
                            ? "The Action-specific completion condition was not observed."
                            : effectDiagnostic,
                        PEGANpcNavigationOutcome.Invalid);
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
                        PEGANpcActionOutcome.Invalid,
                        string.IsNullOrWhiteSpace(effectDiagnostic)
                            ? "The Action-specific completion condition was not observed."
                            : effectDiagnostic,
                        PEGANpcNavigationOutcome.Invalid);
                    return;
                }

                Complete(
                    PEGANpcActionOutcome.Success,
                    "The physical Actor reached the selected navigation Target.",
                    PEGANpcNavigationOutcome.Success);
            }
        }

        private bool Terminate(
            PEGANpcActionOutcome outcome,
            string diagnostic,
            PEGANpcNavigationOutcome navigationOutcome)
        {
            if (Outcome != PEGANpcActionOutcome.Running)
            {
                return false;
            }

            Complete(outcome, diagnostic, navigationOutcome);
            return true;
        }

        private void Complete(
            PEGANpcActionOutcome outcome,
            string diagnostic,
            PEGANpcNavigationOutcome navigationOutcome,
            bool expectedPreconditionInvalidation = false)
        {
            _diagnostic = diagnostic ?? string.Empty;
            _completedAtTimeSeconds = Time.timeAsDouble;
            if (NavigationSource != null && _requestId > 0)
            {
                NavigationSource.FinishRequest(_requestId, navigationOutcome, _diagnostic);
            }

            Outcome = outcome;
            PEGANpcActionExecutionSnapshot result = CurrentExecution;
            if (outcome == PEGANpcActionOutcome.Invalid && !expectedPreconditionInvalidation)
            {
                _log.Warning(
                    "NPC navigation Action failed before reaching its target.",
                    LogFields.Field("actorRoot", ActorRoot.name),
                    LogFields.Field("action", GetType().Name),
                    LogFields.Field("diagnostic", result.Diagnostic));
            }

            _log.Debug(
                "NPC navigation Action finished.",
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
            PEGANpcActionOutcome outcome,
            Vector3 targetPosition,
            string diagnostic)
        {
            _diagnostic = diagnostic ?? string.Empty;
            _completedAtTimeSeconds = Time.timeAsDouble;
            _navigationSnapshot = default;
            _hasNavigationSnapshot = false;
            Outcome = outcome;
            PEGANpcActionEffectDeclaration declaredEffect = DeclaredEffect;
            _log.Error(
                "NPC navigation Action could not start.",
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
                LogFields.Field("diagnostic", _diagnostic));
            PublishTerminalOutcome(CurrentExecution);
        }

        private void PublishTerminalOutcome(PEGANpcActionExecutionSnapshot execution)
        {
            if (_terminalOutcomeEvents == null)
            {
                return;
            }

            _terminalOutcomeEvents.Publish(new PEGANpcActionTerminalOutcomeEvent(
                execution,
                _completedAtTimeSeconds));
        }

        private void EnsureTerminalOutcomeEventsInitialized()
        {
            _terminalOutcomeEvents ??= new EventBus<PEGANpcActionTerminalOutcomeEvent>();
        }

        private void LogSubscriberFailure(
            Action<PEGANpcActionTerminalOutcomeEvent> handler,
            PEGANpcActionTerminalOutcomeEvent outcome,
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

            _log = PEGALogging.CreateScopedLogger<PEGANpcNavigationAction>(LogCategory, ActorRoot.name);
            _loggerInitialized = true;
        }

        private void ResetExecution()
        {
            _requestId = 0;
            _hasNavigationSnapshot = false;
            _navigationSnapshot = default;
            _hasEffectTarget = false;
            _effectTarget = default;
            _effectTargetId = default;
            _completedAtTimeSeconds = 0d;
            _diagnostic = string.Empty;
            Outcome = PEGANpcActionOutcome.NotStarted;
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

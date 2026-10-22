using System;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Actors;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// Selects, starts and reconciles this NPC's valid Normal-context GOAP plan from its own Perception and Knowledge State.
    /// Evaluation is event-driven; physical Action execution remains owned by the selected Action.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Autonomy")]
    [RequireComponent(typeof(ActorOccurrenceReferenceBinding))]
    [RequireComponent(typeof(PEGANpcKnowledgeState))]
    [RequireComponent(typeof(PEGANpcDesireState))]
    [RequireComponent(typeof(PEGANpcPerceptionSource))]
    [RequireComponent(typeof(PEGANpcApproachKnownRickAction))]
    public sealed class PEGANpcAutonomy : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.Autonomy";

        [SerializeField]
        [Tooltip("Profile de Desejos atribuído a este NPC. O profile atual de N01 é provisório para a demonstração na Mission.")]
        private PEGANpcDesireProfile desireProfile;

        [System.NonSerialized]
        private ActorOccurrenceReferenceBinding _actorReference;

        [System.NonSerialized]
        private PEGANpcKnowledgeState _knowledgeState;

        [System.NonSerialized]
        private PEGANpcPerceptionSource _perceptionSource;

        [System.NonSerialized]
        private PEGANpcDesireState _desireState;

        [System.NonSerialized]
        private PEGANpcApproachKnownRickAction _approachAction;

        [System.NonSerialized]
        private PEGANpcDesireContextDefinition _normalContext;

        [System.NonSerialized]
        private IEventBinding _knowledgeChangeBinding;

        [System.NonSerialized]
        private IEventBinding _planReevaluationBinding;

        [System.NonSerialized]
        private IEventBinding _actionOutcomeBinding;

        [System.NonSerialized]
        private ScopedLogger _log;

        [System.NonSerialized]
        private bool _bootstrapPending;

        [System.NonSerialized]
        private bool _planEvaluationQueued;

        [System.NonSerialized]
        private EvaluationCause _pendingEvaluationCauses;

        [System.NonSerialized]
        private PEGANpcPlanReevaluationRequestedEvent _latestPlanReevaluationRequest;

        [System.NonSerialized]
        private bool _hasPendingPlanReevaluationRequest;

        [System.NonSerialized]
        private long _pendingTerminalRequestId;

        [System.NonSerialized]
        private bool _initialSelectionConsumed;

        [System.NonSerialized]
        private bool _hasActivePlan;

        [System.NonSerialized]
        private PEGANpcPlanSnapshot _activePlan;

        [System.NonSerialized]
        private long _activeActionRequestId;

        [System.NonSerialized]
        private PEGANpcPlanSelectionResult _latestSelection;

        [System.NonSerialized]
        private bool _isReplacingPlan;

        [System.NonSerialized]
        private bool _hasRecentTerminalPlan;

        [System.NonSerialized]
        private PEGANpcPlanSnapshot _recentTerminalPlan;

        [System.NonSerialized]
        private PEGANpcActionOutcome _recentTerminalOutcome;

        [System.Flags]
        private enum EvaluationCause
        {
            None = 0,
            InitialKnowledge = 1,
            RelevantKnowledgeChange = 2,
            PlanReevaluationRequest = 4,
            ActionTerminalOutcome = 8
        }

        private void OnEnable()
        {
            EnsureLoggerInitialized();
            if (!TryInitialize(out string diagnostic))
            {
                _log.Error(
                    "NPC autonomy could not initialize its Normal-context GOAP inputs.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("diagnostic", diagnostic));
                enabled = false;
                return;
            }

            _knowledgeChangeBinding ??= _knowledgeState.SubscribeToKnowledgeChanges(OnKnowledgeChanged);
            _planReevaluationBinding ??= _perceptionSource.SubscribeToPlanReevaluationRequests(OnPlanReevaluationRequested);
            _actionOutcomeBinding ??= _approachAction.SubscribeToTerminalOutcomes(OnActionTerminalOutcome);
            _bootstrapPending = !_initialSelectionConsumed;
        }

        private void OnDisable()
        {
            if (_knowledgeState != null && _knowledgeChangeBinding != null)
            {
                _knowledgeState.UnsubscribeFromKnowledgeChanges(_knowledgeChangeBinding);
                _knowledgeChangeBinding = null;
            }

            if (_perceptionSource != null && _planReevaluationBinding != null)
            {
                _perceptionSource.UnsubscribeFromPlanReevaluationRequests(_planReevaluationBinding);
                _planReevaluationBinding = null;
            }

            _bootstrapPending = false;
            ClearQueuedEvaluation();
        }

        private void OnDestroy()
        {
            if (_knowledgeState != null && _knowledgeChangeBinding != null)
            {
                _knowledgeState.UnsubscribeFromKnowledgeChanges(_knowledgeChangeBinding);
                _knowledgeChangeBinding = null;
            }

            if (_perceptionSource != null && _planReevaluationBinding != null)
            {
                _perceptionSource.UnsubscribeFromPlanReevaluationRequests(_planReevaluationBinding);
                _planReevaluationBinding = null;
            }

            if (_approachAction != null && _actionOutcomeBinding != null)
            {
                _approachAction.UnsubscribeFromTerminalOutcomes(_actionOutcomeBinding);
                _actionOutcomeBinding = null;
            }
        }

        private void LateUpdate()
        {
            if (!_bootstrapPending && !_planEvaluationQueued)
            {
                return;
            }

            EvaluationCause causes = _pendingEvaluationCauses;
            if (_bootstrapPending)
            {
                causes |= EvaluationCause.InitialKnowledge;
            }

            PEGANpcPlanReevaluationRequestedEvent request = _latestPlanReevaluationRequest;
            bool hasRequest = _hasPendingPlanReevaluationRequest;
            long terminalRequestId = _pendingTerminalRequestId;
            _bootstrapPending = false;
            ClearQueuedEvaluation();

            if ((causes & EvaluationCause.InitialKnowledge) != 0 &&
                (causes & ~EvaluationCause.InitialKnowledge) == 0 &&
                !TryGetCurrentPerceptionBackedRick(out _))
            {
                return;
            }

            EvaluateAndApplyPlan(causes, hasRequest ? request : null, terminalRequestId);
        }

        private bool TryInitialize(out string diagnostic)
        {
            diagnostic = string.Empty;
            _actorReference = GetComponent<ActorOccurrenceReferenceBinding>();
            _knowledgeState = GetComponent<PEGANpcKnowledgeState>();
            _perceptionSource = GetComponent<PEGANpcPerceptionSource>();
            _desireState = GetComponent<PEGANpcDesireState>();
            _approachAction = GetComponent<PEGANpcApproachKnownRickAction>();

            if (_actorReference == null || _knowledgeState == null || _perceptionSource == null ||
                _desireState == null || _approachAction == null)
            {
                diagnostic = "Actor reference, Knowledge State, Perception Source, Desire State and Approach Known Rick Action must exist on this NPC root.";
                return false;
            }

            if (desireProfile == null || !desireProfile.TryGetContext(
                    PEGANpcDesireContext.Normal,
                    out _normalContext,
                    out diagnostic))
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "A Desire Profile with an explicit Normal context is required."
                    : diagnostic;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private void OnKnowledgeChanged(PEGANpcKnowledgeFactChangedEvent change)
        {
            if (change == null ||
                !change.HasValueOrStatusChanged ||
                change.EvidenceOrigin != PEGANpcEvidenceOrigin.Perception ||
                !change.SubjectActorId.HasValue ||
                !TryGetCurrentActorId(out ActorId rickActorId) ||
                change.SubjectActorId.Value != rickActorId)
            {
                return;
            }

            if (!_initialSelectionConsumed)
            {
                if (change.FactKind != PEGANpcKnowledgeFactKind.RickVisualContact ||
                    change.CurrentStatus != PEGANpcKnowledgeStatus.Known)
                {
                    return;
                }

                PEGANpcKnowledgeFact<bool> contact = _knowledgeState.GetRickVisualContact(rickActorId);
                if (contact.TryGetValue(out bool hasVisualContact) &&
                    hasVisualContact &&
                    contact.HasObservationTime &&
                    Math.Abs(contact.ObservationTimeSeconds - change.ObservationTimeSeconds) <= 0.000001d &&
                    (contact.Origins & PEGANpcEvidenceOrigin.Perception) != 0)
                {
                    QueueEvaluation(EvaluationCause.InitialKnowledge);
                }

                return;
            }

            if (_latestSelection == null || !_latestSelection.IsValid)
            {
                return;
            }

            PEGANpcPlanChangeAssessment assessment =
                PEGANpcPlanChangeRelevanceEvaluator.EvaluateKnowledgeChange(
                    _latestSelection,
                    change,
                    rickActorId);
            if (!assessment.RequiresReevaluation)
            {
                return;
            }

            _log.Info(
                "Perception-backed Knowledge State change queued event-driven GOAP reevaluation.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("subjectActorId", rickActorId.StableText),
                LogFields.Field("factKind", change.FactKind),
                LogFields.Field("observationTimeSeconds", change.ObservationTimeSeconds),
                LogFields.Field("relevance", assessment.Reason),
                LogFields.Field("activeRequestId", _activeActionRequestId));
            QueueEvaluation(EvaluationCause.RelevantKnowledgeChange);
        }

        private void OnPlanReevaluationRequested(PEGANpcPlanReevaluationRequestedEvent request)
        {
            if (request == null)
            {
                return;
            }

            if (!TryValidatePlanReevaluationRequest(request, out string diagnostic))
            {
                _log.Warning(
                    "NPC ignored a plan reevaluation request that did not match current Perception-backed evidence.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("trigger", request.Trigger),
                    LogFields.Field("targetActorId", request.TargetActorId.StableText),
                    LogFields.Field("observationTimeSeconds", request.ObservationTimeSeconds),
                    LogFields.Field("diagnostic", diagnostic));
                return;
            }

            _log.Info(
                "NPC received a legitimate plan reevaluation request from Perception.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("trigger", request.Trigger),
                LogFields.Field("targetActorId", request.TargetActorId.StableText),
                LogFields.Field("observationTimeSeconds", request.ObservationTimeSeconds),
                LogFields.Field("requestedAtTimeSeconds", request.RequestedAtTimeSeconds),
                LogFields.Field("activeRequestId", _activeActionRequestId));
            QueueEvaluation(EvaluationCause.PlanReevaluationRequest, request);
        }

        private bool TryGetCurrentPerceptionBackedRick(out ActorId rickActorId)
        {
            if (!TryGetCurrentActorId(out rickActorId))
            {
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                _knowledgeState.GetObservedCharacterIdentity(rickActorId);
            PEGANpcKnowledgeFact<bool> contact = _knowledgeState.GetRickVisualContact(rickActorId);
            PEGANpcKnowledgeFact<Vector3> position = _knowledgeState.GetLastObservedRickPosition(rickActorId);
            return identity.TryGetValue(out _) &&
                   (identity.Origins & PEGANpcEvidenceOrigin.Perception) != 0 &&
                   contact.TryGetValue(out bool hasVisualContact) &&
                   hasVisualContact &&
                   (contact.Origins & PEGANpcEvidenceOrigin.Perception) != 0 &&
                   position.TryGetValue(out _) &&
                   position.HasObservationTime &&
                   (position.Origins & PEGANpcEvidenceOrigin.Perception) != 0;
        }

        private bool TryGetCurrentActorId(out ActorId actorId)
        {
            actorId = default;
            if (_actorReference == null ||
                !_actorReference.TryGetCurrent(out ActorOccurrenceResolution resolution))
            {
                return false;
            }

            actorId = resolution.Occurrence.ActorId;
            return actorId.IsValid;
        }

        private bool TryValidatePlanReevaluationRequest(
            PEGANpcPlanReevaluationRequestedEvent request,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!TryGetCurrentActorId(out ActorId actorId) || actorId != request.TargetActorId)
            {
                diagnostic = "The request target is not the current Player Actor known to this NPC.";
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                _knowledgeState.GetObservedCharacterIdentity(actorId);
            PEGANpcKnowledgeFact<bool> contact = _knowledgeState.GetRickVisualContact(actorId);
            PEGANpcKnowledgeFact<Vector3> position = _knowledgeState.GetLastObservedRickPosition(actorId);
            if (!identity.TryGetValue(out _) ||
                !identity.HasObservationTime ||
                (identity.Origins & PEGANpcEvidenceOrigin.Perception) == 0 ||
                !position.TryGetValue(out _) ||
                !position.HasObservationTime ||
                (position.Origins & PEGANpcEvidenceOrigin.Perception) == 0 ||
                !contact.TryGetValue(out bool hasVisualContact) ||
                !contact.HasObservationTime ||
                (contact.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                diagnostic = "The NPC does not have the required Perception-backed identity, contact and last-position facts.";
                return false;
            }

            switch (request.Trigger)
            {
                case PEGANpcPlanReevaluationTrigger.RickReacquired:
                    if (!hasVisualContact ||
                        !HasObservationAt(identity, request.ObservationTimeSeconds) ||
                        !HasObservationAt(contact, request.ObservationTimeSeconds) ||
                        !HasObservationAt(position, request.ObservationTimeSeconds))
                    {
                        diagnostic = "RickReacquired requires consolidated current identity, contact and position evidence.";
                        return false;
                    }

                    return true;

                case PEGANpcPlanReevaluationTrigger.RickConsideredEvaded:
                    if (hasVisualContact ||
                        !HasObservationAt(contact, request.ObservationTimeSeconds) ||
                        position.ObservationTimeSeconds > request.ObservationTimeSeconds + 0.000001d ||
                        identity.ObservationTimeSeconds > request.ObservationTimeSeconds + 0.000001d)
                    {
                        diagnostic = "RickConsideredEvaded requires the published real contact-loss sample and preserved earlier Perception evidence.";
                        return false;
                    }

                    return true;

                default:
                    diagnostic = "The request uses an unsupported Perception reevaluation trigger.";
                    return false;
            }
        }

        private static bool HasObservationAt<T>(PEGANpcKnowledgeFact<T> fact, double observationTimeSeconds) =>
            fact.HasObservationTime &&
            Math.Abs(fact.ObservationTimeSeconds - observationTimeSeconds) <= 0.000001d;

        private void EvaluateAndApplyPlan(
            EvaluationCause causes,
            PEGANpcPlanReevaluationRequestedEvent request,
            long terminalRequestId)
        {
            if (!TryBuildLiveSelection(
                    out ActorId rickActorId,
                    out PEGANpcPlanningActionCandidate candidate,
                    out PEGANpcPlanSelectionResult selection,
                    out string diagnostic))
            {
                _log.Warning(
                    "NPC GOAP reevaluation had no valid live target/action input.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("causes", causes),
                    LogFields.Field("targetActorId", rickActorId.IsValid ? rickActorId.StableText : string.Empty),
                    LogFields.Field("activeRequestId", _activeActionRequestId),
                    LogFields.Field("reevaluationTrigger", request != null ? request.Trigger : default),
                    LogFields.Field("terminalRequestId", terminalRequestId),
                    LogFields.Field("diagnostic", diagnostic));
                HandleNoSelectedPlan(null, causes, diagnostic);
                return;
            }

            _latestSelection = selection;
            _log.Info(
                "NPC consolidated event-driven GOAP reevaluation against current Perception-backed knowledge.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("causes", causes),
                LogFields.Field("targetActorId", rickActorId.StableText),
                LogFields.Field("reevaluationTrigger", request != null ? request.Trigger : default),
                LogFields.Field("reevaluationObservationTimeSeconds", request != null ? request.ObservationTimeSeconds : -1d),
                LogFields.Field("terminalRequestId", terminalRequestId),
                LogFields.Field("selectionValid", selection.IsValid),
                LogFields.Field("candidatePlanCount", selection.CandidatePlans.Count),
                LogFields.Field("hasSelectedPlan", selection.HasSelectedPlan),
                LogFields.Field("diagnostic", selection.Diagnostic));

            if (!selection.IsValid ||
                !selection.HasSelectedPlan ||
                selection.SelectedPlan.ActionId != PEGANpcPlanningActionId.ApproachKnownRick ||
                selection.SelectedPlan.TargetProvenance != PEGANpcPlanningTargetProvenance.KnowledgeState ||
                selection.SelectedPlan.TargetId != PEGANpcPlanningTargetId.ForActor(rickActorId))
            {
                HandleNoSelectedPlan(
                    selection,
                    causes,
                    string.IsNullOrEmpty(selection.Diagnostic)
                        ? "No compatible Desire/Action/Target plan is selectable."
                        : selection.Diagnostic);
                return;
            }

            PEGANpcPlanSnapshot selectedPlan = selection.SelectedPlan;
            if (_hasActivePlan)
            {
                if (_approachAction.Outcome == PEGANpcActionOutcome.Running &&
                    AreEquivalentPlans(_activePlan, selectedPlan))
                {
                    _log.Info(
                        "NPC preserved the running Action because the selected GOAP plan and its evidence remain equivalent.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("causes", causes),
                        LogFields.Field("requestId", _activeActionRequestId),
                        LogFields.Field("desireId", selectedPlan.DesireId),
                        LogFields.Field("actionId", selectedPlan.ActionId),
                        LogFields.Field("targetActorId", selectedPlan.TargetId),
                        LogFields.Field("targetPosition", selectedPlan.TargetPosition),
                        LogFields.Field("targetEvidenceDeltaMeters", PlanarDistance(_activePlan.TargetPosition, selectedPlan.TargetPosition)),
                        LogFields.Field("targetChangeToleranceMeters", _approachAction.DeclaredEffect.TargetRadiusMeters),
                        LogFields.Field("evidenceTimeAdvanced", selectedPlan.EvidenceTimeSeconds > _activePlan.EvidenceTimeSeconds),
                        LogFields.Field("positionEvidenceTimeSeconds", selectedPlan.EvidenceTimeSeconds),
                        LogFields.Field("predictedEffect", selectedPlan.PredictedEffect),
                        LogFields.Field("predictedGoal", selectedPlan.PredictedGoal));
                    return;
                }

                if (!CancelActivePlanForReplacement(
                        "GOAP selected a different Action, Target evidence or Desire objective."))
                {
                    return;
                }
            }
            else if (_hasRecentTerminalPlan &&
                     AreEquivalentPlans(_recentTerminalPlan, selectedPlan) &&
                     (_recentTerminalOutcome == PEGANpcActionOutcome.Success ||
                      !IsNewReacquisitionOpportunity(request)))
            {
                _log.Info(
                    "NPC retained the terminal result and did not repeat an equivalent plan without a materially new target or objective.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("causes", causes),
                    LogFields.Field("previousOutcome", _recentTerminalOutcome),
                    LogFields.Field("desireId", selectedPlan.DesireId),
                    LogFields.Field("actionId", selectedPlan.ActionId),
                    LogFields.Field("targetActorId", selectedPlan.TargetId),
                    LogFields.Field("targetPosition", selectedPlan.TargetPosition),
                    LogFields.Field("targetEvidenceDeltaMeters", PlanarDistance(_recentTerminalPlan.TargetPosition, selectedPlan.TargetPosition)),
                    LogFields.Field("targetChangeToleranceMeters", _approachAction.DeclaredEffect.TargetRadiusMeters),
                    LogFields.Field("evidenceTimeAdvanced", selectedPlan.EvidenceTimeSeconds > _recentTerminalPlan.EvidenceTimeSeconds),
                    LogFields.Field("newReacquisitionOpportunity", IsNewReacquisitionOpportunity(request)),
                    LogFields.Field("positionEvidenceTimeSeconds", selectedPlan.EvidenceTimeSeconds),
                    LogFields.Field("predictedEffect", selectedPlan.PredictedEffect),
                    LogFields.Field("predictedGoal", selectedPlan.PredictedGoal));
                return;
            }

            StartSelectedPlan(selection, candidate, causes);
        }

        private bool TryBuildLiveSelection(
            out ActorId rickActorId,
            out PEGANpcPlanningActionCandidate candidate,
            out PEGANpcPlanSelectionResult selection,
            out string diagnostic)
        {
            rickActorId = default;
            candidate = default;
            selection = null;
            if (!TryGetCurrentActorId(out rickActorId))
            {
                diagnostic = "The NPC has no current Player Actor occurrence for live Target resolution.";
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                _knowledgeState.GetObservedCharacterIdentity(rickActorId);
            PEGANpcKnowledgeFact<Vector3> position =
                _knowledgeState.GetLastObservedRickPosition(rickActorId);
            if (!identity.TryGetValue(out _) ||
                (identity.Origins & PEGANpcEvidenceOrigin.Perception) == 0 ||
                !position.TryGetValue(out _) ||
                !position.HasObservationTime ||
                (position.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                diagnostic = "Live GOAP selection requires this NPC's Perception-backed Rick identity and last known position.";
                return false;
            }

            if (!_approachAction.TryCreatePlanningCandidate(
                    out PEGANpcPlanningTarget target,
                    out candidate,
                    out diagnostic))
            {
                return false;
            }

            if (target.Provenance != PEGANpcPlanningTargetProvenance.KnowledgeState ||
                target.TargetId != PEGANpcPlanningTargetId.ForActor(rickActorId) ||
                candidate.ActionId != PEGANpcPlanningActionId.ApproachKnownRick)
            {
                diagnostic = "The live Action candidate is not bound to this NPC's current Knowledge State Target.";
                return false;
            }

            PEGANpcPlanningInput input = new PEGANpcPlanningInput(
                PEGANpcPlanningValidationMode.LiveKnowledge,
                PEGANpcDesireContext.Normal,
                new[] { target },
                new[] { candidate });
            selection = PEGANpcPlanSelector.Select(
                input,
                _normalContext,
                _desireState.GetSnapshot());
            diagnostic = selection.Diagnostic;
            return selection.IsValid;
        }

        private void HandleNoSelectedPlan(
            PEGANpcPlanSelectionResult selection,
            EvaluationCause causes,
            string diagnostic)
        {
            if (selection != null && selection.IsValid)
            {
                _latestSelection = selection;
            }

            if (_hasActivePlan && CanPreserveActiveExecution(
                    out Vector3 knownPosition,
                    out double positionEvidenceTimeSeconds))
            {
                _log.Info(
                    "NPC preserved its running Action because its current Desire, Perception evidence and execution remain valid despite no replacement plan being selectable.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("causes", causes),
                    LogFields.Field("requestId", _activeActionRequestId),
                    LogFields.Field("desireId", _activePlan.DesireId),
                    LogFields.Field("targetActorId", _activePlan.TargetId),
                    LogFields.Field("targetEvidenceDeltaMeters", PlanarDistance(knownPosition, _activePlan.TargetPosition)),
                    LogFields.Field("targetChangeToleranceMeters", _approachAction.DeclaredEffect.TargetRadiusMeters),
                    LogFields.Field("evidenceTimeAdvanced", positionEvidenceTimeSeconds > _activePlan.EvidenceTimeSeconds),
                    LogFields.Field("positionEvidenceTimeSeconds", positionEvidenceTimeSeconds),
                    LogFields.Field("diagnostic", diagnostic));
                return;
            }

            if (_hasActivePlan)
            {
                if (!CancelActivePlanForReplacement("The active GOAP plan is no longer supported by current knowledge or applicable Desire state."))
                {
                    return;
                }
            }

            _log.Info(
                "NPC reevaluated the current Desire set and found no viable GOAP plan.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("causes", causes),
                LogFields.Field("candidatePlanCount", selection != null ? selection.CandidatePlans.Count : 0),
                LogFields.Field("diagnostic", diagnostic));
        }

        private bool CanPreserveActiveExecution(
            out Vector3 knownPosition,
            out double positionEvidenceTimeSeconds)
        {
            knownPosition = default;
            positionEvidenceTimeSeconds = 0d;
            PEGANpcActionExecutionSnapshot execution = _approachAction != null
                ? _approachAction.CurrentExecution
                : default;
            PEGANpcActionEffectDeclaration activeEffect = execution.DeclaredEffect;
            if (!_hasActivePlan ||
                execution.Outcome != PEGANpcActionOutcome.Running ||
                execution.RequestId != _activeActionRequestId ||
                !activeEffect.IsTargetResolved ||
                activeEffect.Kind != _activePlan.PredictedEffect ||
                PlanarDistance(activeEffect.TargetPosition, _activePlan.TargetPosition) > 0.001f ||
                !TryGetCurrentActorId(out ActorId currentActorId) ||
                _activePlan.TargetId != PEGANpcPlanningTargetId.ForActor(currentActorId))
            {
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                _knowledgeState.GetObservedCharacterIdentity(currentActorId);
            PEGANpcKnowledgeFact<Vector3> position =
                _knowledgeState.GetLastObservedRickPosition(currentActorId);
            if (!identity.TryGetValue(out _) ||
                (identity.Origins & PEGANpcEvidenceOrigin.Perception) == 0 ||
                !position.TryGetValue(out knownPosition) ||
                !position.HasObservationTime ||
                (position.Origins & PEGANpcEvidenceOrigin.Perception) == 0 ||
                position.ObservationTimeSeconds < _activePlan.EvidenceTimeSeconds ||
                !_approachAction.TryGetKnownRickPosition(out _, out _) ||
                !IsTargetPositionWithinActionTolerance(knownPosition, _activePlan.TargetPosition))
            {
                return false;
            }

            positionEvidenceTimeSeconds = position.ObservationTimeSeconds;

            PEGANpcDesireStateSnapshot satisfaction = _desireState.GetSnapshot();
            for (int index = 0; index < _normalContext.Desires.Count; index++)
            {
                PEGANpcDesireDefinition definition = _normalContext.Desires[index];
                if (string.Equals(definition.DesireId, _activePlan.DesireId, StringComparison.Ordinal))
                {
                    satisfaction.TryGet(definition.DesireId, out PEGANpcDesireSatisfactionSnapshot current);
                    return definition.IsApplicable(current);
                }
            }

            return false;
        }

        private bool CancelActivePlanForReplacement(string diagnostic)
        {
            if (!_hasActivePlan)
            {
                return true;
            }

            if (_approachAction.Outcome != PEGANpcActionOutcome.Running)
            {
                ClearActivePlan();
                return true;
            }

            long previousRequestId = _activeActionRequestId;
            _isReplacingPlan = true;
            bool cancelled;
            try
            {
                cancelled = _approachAction.CancelExecution(diagnostic);
            }
            finally
            {
                _isReplacingPlan = false;
            }

            if (!cancelled || _hasActivePlan)
            {
                _log.Error(
                    "NPC could not release the previous Action before applying its newly selected GOAP plan.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("activeRequestId", previousRequestId),
                    LogFields.Field("actionOutcome", _approachAction.Outcome),
                    LogFields.Field("cancelled", cancelled),
                    LogFields.Field("diagnostic", diagnostic));
                return false;
            }

            _log.Info(
                "NPC cancelled the previous Action through its existing lifecycle before replacing the plan.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("requestId", previousRequestId),
                LogFields.Field("diagnostic", diagnostic));
            return true;
        }

        private void StartSelectedPlan(
            PEGANpcPlanSelectionResult selection,
            PEGANpcPlanningActionCandidate candidate,
            EvaluationCause causes)
        {
            PEGANpcPlanSnapshot plan = selection.SelectedPlan;
            _initialSelectionConsumed = true;
            _log.Info(
                "NPC GOAP selected a valid plan from its live Normal Desire set.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("causes", causes),
                LogFields.Field("context", selection.Context),
                LogFields.Field("desireId", plan.DesireId),
                LogFields.Field("actionId", plan.ActionId),
                LogFields.Field("targetActorId", plan.TargetId),
                LogFields.Field("targetProvenance", plan.TargetProvenance),
                LogFields.Field("targetPosition", plan.TargetPosition),
                LogFields.Field("positionEvidenceTimeSeconds", plan.EvidenceTimeSeconds),
                LogFields.Field("predictedEffect", plan.PredictedEffect),
                LogFields.Field("predictedGoal", plan.PredictedGoal),
                LogFields.Field("routeLengthMeters", plan.RouteLengthMeters),
                LogFields.Field("estimatedSeconds", plan.EstimatedSeconds),
                LogFields.Field("effectiveDesireWeight", plan.EffectiveDesireWeight),
                LogFields.Field("costPreference", plan.CostPreference),
                LogFields.Field("score", plan.Score));

            _activePlan = plan;
            _activeActionRequestId = 0;
            _hasActivePlan = true;
            if (!_approachAction.TryStart(out string startDiagnostic))
            {
                if (_hasActivePlan)
                {
                    PEGANpcActionExecutionSnapshot execution = _approachAction.CurrentExecution;
                    _log.Error(
                        "NPC selected a valid GOAP plan, but no terminal Action result was delivered for the rejected start.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("actionOutcome", execution.Outcome),
                        LogFields.Field("diagnostic", startDiagnostic));
                    ClearActivePlan();
                }

                return;
            }

            if (_hasActivePlan)
            {
                _activeActionRequestId = _approachAction.CurrentExecution.RequestId;
                _hasRecentTerminalPlan = false;
                _recentTerminalPlan = default;
            }

            _log.Info(
                "NPC autonomously started the selected GOAP Action through its existing physical navigation path.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("causes", causes),
                LogFields.Field("requestId", _activeActionRequestId),
                LogFields.Field("desireId", plan.DesireId),
                LogFields.Field("actionId", plan.ActionId),
                LogFields.Field("actionOutcome", _approachAction.Outcome),
                LogFields.Field("targetPosition", plan.TargetPosition),
                LogFields.Field("speedRegime", candidate.SpeedRegime));
        }

        private void OnActionTerminalOutcome(PEGANpcActionTerminalOutcomeEvent terminalEvent)
        {
            if (terminalEvent == null)
            {
                return;
            }

            PEGANpcActionExecutionSnapshot execution = terminalEvent.Execution;
            if (!_hasActivePlan)
            {
                _log.Warning(
                    "NPC received a terminal Action result without an active GOAP plan owner; no Effect or Desire satisfaction was applied.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("requestId", execution.RequestId),
                    LogFields.Field("actionOutcome", execution.Outcome));
                return;
            }

            if (_activeActionRequestId > 0 && execution.RequestId != _activeActionRequestId)
            {
                _log.Error(
                    "NPC ignored a terminal Action result that does not match its active request.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("activeRequestId", _activeActionRequestId),
                    LogFields.Field("terminalRequestId", execution.RequestId),
                    LogFields.Field("actionOutcome", execution.Outcome));
                return;
            }

            if (execution.Outcome != PEGANpcActionOutcome.Success &&
                execution.Outcome != PEGANpcActionOutcome.Invalid &&
                execution.Outcome != PEGANpcActionOutcome.Interrupted &&
                execution.Outcome != PEGANpcActionOutcome.Cancelled)
            {
                _log.Warning(
                    "NPC received a non-terminal Action snapshot for its active plan; no Effect or Desire satisfaction was applied.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("requestId", execution.RequestId),
                    LogFields.Field("actionOutcome", execution.Outcome));
                return;
            }

            PEGANpcPlanSnapshot completedPlan = _activePlan;
            if (_isReplacingPlan)
            {
                _log.Info(
                    "NPC Action cancellation released the previous plan for a selected replacement; no Effect or satisfaction was applied.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("requestId", execution.RequestId),
                    LogFields.Field("actionOutcome", execution.Outcome),
                    LogFields.Field("desireId", completedPlan.DesireId),
                    LogFields.Field("actionId", completedPlan.ActionId),
                    LogFields.Field("targetActorId", completedPlan.TargetId),
                    LogFields.Field("diagnostic", execution.Diagnostic));
                ClearActivePlan();
                return;
            }

            PEGANpcPlanChangeAssessment relevance = _latestSelection != null && _latestSelection.IsValid
                ? PEGANpcPlanChangeRelevanceEvaluator.EvaluateActionOutcome(
                    _latestSelection,
                    _activeActionRequestId,
                    terminalEvent)
                : default;
            bool observedEffectMatched = false;
            try
            {
                if (execution.Outcome != PEGANpcActionOutcome.Success)
                {
                    _log.Info(
                        "NPC Action plan terminated without applying an Effect or Desire satisfaction.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("desireId", completedPlan.DesireId),
                        LogFields.Field("actionId", completedPlan.ActionId),
                        LogFields.Field("targetActorId", completedPlan.TargetId),
                        LogFields.Field("targetPosition", completedPlan.TargetPosition),
                        LogFields.Field("positionEvidenceTimeSeconds", completedPlan.EvidenceTimeSeconds),
                        LogFields.Field("requestId", execution.RequestId),
                        LogFields.Field("actionOutcome", execution.Outcome),
                        LogFields.Field("completedAtTimeSeconds", terminalEvent.CompletedAtTimeSeconds),
                        LogFields.Field("effectApplied", false),
                        LogFields.Field("satisfactionUpdatedCount", 0),
                        LogFields.Field("diagnostic", execution.Diagnostic));
                    return;
                }

                if (!TryGetEffectMatchingActivePlan(
                        execution,
                        completedPlan,
                        out PEGANpcObservedActionEffect observedEffect,
                        out string effectDiagnostic))
                {
                    _log.Error(
                        "NPC Action reported Success without an observed Effect matching the active GOAP plan; Desire satisfaction was not changed.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("desireId", completedPlan.DesireId),
                        LogFields.Field("actionId", completedPlan.ActionId),
                        LogFields.Field("targetActorId", completedPlan.TargetId),
                        LogFields.Field("targetPosition", completedPlan.TargetPosition),
                        LogFields.Field("positionEvidenceTimeSeconds", completedPlan.EvidenceTimeSeconds),
                        LogFields.Field("requestId", execution.RequestId),
                        LogFields.Field("actionOutcome", execution.Outcome),
                        LogFields.Field("completedAtTimeSeconds", terminalEvent.CompletedAtTimeSeconds),
                        LogFields.Field("terminalEffectReported", execution.EffectObserved),
                        LogFields.Field("effectApplied", false),
                        LogFields.Field("satisfactionUpdatedCount", 0),
                        LogFields.Field("diagnostic", effectDiagnostic));
                    return;
                }

                observedEffectMatched = true;
                PEGANpcDesireSatisfactionUpdate satisfaction =
                    _desireState.RecordCompletedAction(observedEffect, _normalContext);
                _log.Info(
                    "NPC applied a physically observed Action Effect to the matching Desire predicates.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("desireId", completedPlan.DesireId),
                    LogFields.Field("actionId", completedPlan.ActionId),
                    LogFields.Field("targetActorId", completedPlan.TargetId),
                    LogFields.Field("targetPosition", completedPlan.TargetPosition),
                    LogFields.Field("positionEvidenceTimeSeconds", completedPlan.EvidenceTimeSeconds),
                    LogFields.Field("requestId", execution.RequestId),
                    LogFields.Field("actionOutcome", execution.Outcome),
                    LogFields.Field("completedAtTimeSeconds", terminalEvent.CompletedAtTimeSeconds),
                    LogFields.Field("effect", observedEffect.Kind),
                    LogFields.Field("goal", observedEffect.GoalKind),
                    LogFields.Field("effectTargetPosition", observedEffect.TargetPosition),
                    LogFields.Field("actualDistanceToTargetMeters", observedEffect.ActualDistanceToTargetMeters),
                    LogFields.Field("effectApplied", true),
                    LogFields.Field("satisfactionUpdatedCount", satisfaction.UpdatedCount),
                    LogFields.Field("satisfiedDesireIds", JoinDesireIds(satisfaction)),
                    LogFields.Field("satisfactionLedger", DescribeSatisfaction(satisfaction)));
            }
            finally
            {
                _hasRecentTerminalPlan = true;
                _recentTerminalPlan = completedPlan;
                _recentTerminalOutcome = execution.Outcome;
                ClearActivePlan();

                bool terminalResultRequiresReevaluation = relevance.RequiresReevaluation ||
                    execution.Outcome == PEGANpcActionOutcome.Invalid ||
                    execution.Outcome == PEGANpcActionOutcome.Interrupted ||
                    observedEffectMatched;
                if (terminalResultRequiresReevaluation && isActiveAndEnabled)
                {
                    _log.Info(
                        "Terminal Action result changed GOAP-relevant state and queued one event-driven reevaluation.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("requestId", execution.RequestId),
                        LogFields.Field("actionOutcome", execution.Outcome),
                        LogFields.Field("relevance", relevance.Reason),
                        LogFields.Field("completedAtTimeSeconds", terminalEvent.CompletedAtTimeSeconds));
                    QueueEvaluation(EvaluationCause.ActionTerminalOutcome, terminalRequestId: execution.RequestId);
                }
                else
                {
                    _log.Info(
                        "Terminal Action result did not produce a new plan-selection opportunity.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("requestId", execution.RequestId),
                        LogFields.Field("actionOutcome", execution.Outcome),
                        LogFields.Field("relevance", relevance.Reason));
                }
            }
        }

        private bool TryGetEffectMatchingActivePlan(
            PEGANpcActionExecutionSnapshot execution,
            PEGANpcPlanSnapshot plan,
            out PEGANpcObservedActionEffect observedEffect,
            out string diagnostic)
        {
            observedEffect = default;
            if (plan.ActionId != PEGANpcPlanningActionId.ApproachKnownRick ||
                !_approachAction.TryGetObservedEffect(out observedEffect))
            {
                diagnostic = "The active Approach Known Rick Action has no physically observed Effect.";
                return false;
            }

            if (!observedEffect.IsValid ||
                observedEffect.RequestId <= 0 ||
                observedEffect.RequestId != execution.RequestId ||
                observedEffect.Kind != plan.PredictedEffect ||
                observedEffect.GoalKind != plan.PredictedGoal ||
                (observedEffect.TargetPosition - plan.TargetPosition).sqrMagnitude > 0.000001f)
            {
                diagnostic = "The observed Action Effect does not match the active plan's request, predicted Effect, goal or known Target.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private void ClearActivePlan()
        {
            _hasActivePlan = false;
            _activePlan = default;
            _activeActionRequestId = 0;
        }

        private void QueueEvaluation(
            EvaluationCause cause,
            PEGANpcPlanReevaluationRequestedEvent request = null,
            long terminalRequestId = 0)
        {
            _planEvaluationQueued = true;
            _pendingEvaluationCauses |= cause;
            if (request != null &&
                (!_hasPendingPlanReevaluationRequest ||
                 request.RequestedAtTimeSeconds > _latestPlanReevaluationRequest.RequestedAtTimeSeconds ||
                 (request.RequestedAtTimeSeconds == _latestPlanReevaluationRequest.RequestedAtTimeSeconds &&
                  (int)request.Trigger > (int)_latestPlanReevaluationRequest.Trigger)))
            {
                _latestPlanReevaluationRequest = request;
                _hasPendingPlanReevaluationRequest = true;
            }

            if (terminalRequestId > _pendingTerminalRequestId)
            {
                _pendingTerminalRequestId = terminalRequestId;
            }
        }

        private void ClearQueuedEvaluation()
        {
            _planEvaluationQueued = false;
            _pendingEvaluationCauses = EvaluationCause.None;
            _latestPlanReevaluationRequest = null;
            _hasPendingPlanReevaluationRequest = false;
            _pendingTerminalRequestId = 0;
        }

        private bool AreEquivalentPlans(PEGANpcPlanSnapshot left, PEGANpcPlanSnapshot right) =>
            left.ActionId == right.ActionId &&
            left.PredictedEffect == right.PredictedEffect &&
            left.PredictedGoal == right.PredictedGoal &&
            left.TargetId == right.TargetId &&
            left.TargetKind == right.TargetKind &&
            left.TargetProvenance == right.TargetProvenance &&
            right.EvidenceTimeSeconds >= left.EvidenceTimeSeconds &&
            IsTargetPositionWithinActionTolerance(left.TargetPosition, right.TargetPosition) &&
            left.SpeedRegime == right.SpeedRegime &&
            string.Equals(left.DesireId, right.DesireId, StringComparison.Ordinal);

        private bool IsTargetPositionWithinActionTolerance(Vector3 left, Vector3 right)
        {
            float toleranceMeters = _approachAction != null
                ? _approachAction.DeclaredEffect.TargetRadiusMeters
                : 0f;
            return toleranceMeters >= 0f &&
                   !float.IsNaN(toleranceMeters) &&
                   !float.IsInfinity(toleranceMeters) &&
                   PlanarDistance(left, right) <= toleranceMeters;
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            Vector3 delta = left - right;
            delta.y = 0f;
            return delta.magnitude;
        }

        private bool IsNewReacquisitionOpportunity(PEGANpcPlanReevaluationRequestedEvent request) =>
            request != null &&
            request.Trigger == PEGANpcPlanReevaluationTrigger.RickReacquired &&
            request.ObservationTimeSeconds > _recentTerminalPlan.EvidenceTimeSeconds + 0.000001d &&
            _recentTerminalOutcome != PEGANpcActionOutcome.Success;

        private static string JoinDesireIds(PEGANpcDesireSatisfactionUpdate satisfaction)
        {
            if (satisfaction == null || satisfaction.UpdatedCount == 0)
            {
                return string.Empty;
            }

            string[] desireIds = new string[satisfaction.UpdatedCount];
            for (int index = 0; index < satisfaction.UpdatedCount; index++)
            {
                desireIds[index] = satisfaction.UpdatedValues[index].DesireId;
            }

            return string.Join(",", desireIds);
        }

        private static string DescribeSatisfaction(PEGANpcDesireSatisfactionUpdate satisfaction)
        {
            if (satisfaction == null || satisfaction.UpdatedCount == 0)
            {
                return string.Empty;
            }

            string[] values = new string[satisfaction.UpdatedCount];
            for (int index = 0; index < satisfaction.UpdatedCount; index++)
            {
                PEGANpcDesireSatisfactionSnapshot entry = satisfaction.UpdatedValues[index];
                values[index] = $"{entry.DesireId}:{entry.Satisfaction:0.###}:{entry.CompletedExecutions}";
            }

            return string.Join(",", values);
        }

        private void EnsureLoggerInitialized()
        {
            _log ??= PEGALogging.CreateScopedLogger<PEGANpcAutonomy>(LogCategory, gameObject.name);
        }
    }
}

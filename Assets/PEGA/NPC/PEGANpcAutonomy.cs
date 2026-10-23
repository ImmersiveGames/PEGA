using System;
using System.Collections.Generic;
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
    [RequireComponent(typeof(PEGANpcInvestigateLastKnownRickPositionAction))]
    [RequireComponent(typeof(PEGANpcTravelToEscapeAction))]
    public sealed class PEGANpcAutonomy : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.Autonomy";
        private const PEGAActorMovementSpeedRegime EscapeMovementSpeedRegime =
            PEGAActorMovementSpeedRegime.Pursuit;

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
        private PEGANpcInvestigateLastKnownRickPositionAction _investigateAction;

        [System.NonSerialized]
        private PEGANpcTravelToEscapeAction _travelToEscapeAction;

        [System.NonSerialized]
        private PEGANpcGangMembership _gangMembership;

        [System.NonSerialized]
        private PEGANpcGangKnowledgeState _gangKnowledgeState;

        [System.NonSerialized]
        private PEGANpcDesireContextDefinition _normalContext;

        [System.NonSerialized]
        private IEventBinding _knowledgeChangeBinding;

        [System.NonSerialized]
        private IEventBinding _planReevaluationBinding;

        [System.NonSerialized]
        private IEventBinding _lostContactPursuitIntentBinding;

        [System.NonSerialized]
        private IEventBinding _actionOutcomeBinding;

        [System.NonSerialized]
        private IEventBinding _investigateActionOutcomeBinding;

        [System.NonSerialized]
        private IEventBinding _gangKnowledgeChangeBinding;

        [System.NonSerialized]
        private IEventBinding _travelActionOutcomeBinding;

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
        private PEGANpcLastKnownRickPursuitIntent _latestLostContactPursuitIntent;

        [System.NonSerialized]
        private bool _hasPendingLostContactPursuitIntent;

        [System.NonSerialized]
        private OpportunityStamp _lastLostContactOpportunity;

        [System.NonSerialized]
        private bool _hasLastLostContactOpportunity;

        [System.NonSerialized]
        private OpportunityStamp _lastReacquisitionOpportunity;

        [System.NonSerialized]
        private bool _hasLastReacquisitionOpportunity;

        [System.NonSerialized]
        private OpportunityStamp _lastEvadedOpportunity;

        [System.NonSerialized]
        private bool _hasLastEvadedOpportunity;

        [System.NonSerialized]
        private bool _initialSelectionConsumed;

        [System.NonSerialized]
        private bool _hasBeenEnabled;

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
        private bool _isStoppingForLifecycle;

        [System.NonSerialized]
        private bool _hasRecentTerminalPlan;

        [System.NonSerialized]
        private PEGANpcPlanSnapshot _recentTerminalPlan;

        [System.NonSerialized]
        private PEGANpcActionOutcome _recentTerminalOutcome;

        [System.NonSerialized]
        private OpportunityStamp _activePlanOpportunity;

        [System.NonSerialized]
        private PEGANpcPursuitContinuation _activePursuitContinuation;

        [System.NonSerialized]
        private bool _hasActivePursuitContinuation;

        [System.NonSerialized]
        private readonly HashSet<PEGANpcPlanningTargetId> _attemptedInvestigationTargets =
            new HashSet<PEGANpcPlanningTargetId>();

        [System.NonSerialized]
        private OpportunityStamp _recentTerminalOpportunity;

        [System.NonSerialized]
        private bool _hasRecentTerminalOpportunity;

        private readonly struct OpportunityStamp
        {
            public OpportunityStamp(int kind, ActorId actorId, double observationTimeSeconds)
            {
                Kind = kind;
                ActorId = actorId;
                ObservationTimeSeconds = observationTimeSeconds;
            }

            public int Kind { get; }
            public ActorId ActorId { get; }
            public double ObservationTimeSeconds { get; }

            public bool IsSameAs(OpportunityStamp other) =>
                Kind == other.Kind &&
                ActorId == other.ActorId &&
                Math.Abs(ObservationTimeSeconds - other.ObservationTimeSeconds) <= 0.000001d;

        }

        [System.Flags]
        private enum EvaluationCause
        {
            None = 0,
            InitialKnowledge = 1,
            RelevantKnowledgeChange = 2,
            PlanReevaluationRequest = 4,
            ActionTerminalOutcome = 8,
            LastKnownPursuitIntent = 16,
            AutonomyLifecycle = 32,
            GangKnowledgeChange = 64
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
            _lostContactPursuitIntentBinding ??= _approachAction.SubscribeToLostContactPursuitIntents(OnLostContactPursuitIntentRequested);
            _actionOutcomeBinding ??= _approachAction.SubscribeToTerminalOutcomes(OnApproachActionTerminalOutcome);
            _investigateActionOutcomeBinding ??= _investigateAction.SubscribeToTerminalOutcomes(OnInvestigationActionTerminalOutcome);
            _travelActionOutcomeBinding ??= _travelToEscapeAction.SubscribeToTerminalOutcomes(OnTravelActionTerminalOutcome);
            if (_gangKnowledgeState != null)
            {
                _gangKnowledgeChangeBinding ??= _gangKnowledgeState.SubscribeToChanges(OnGangKnowledgeChanged);
            }

            if (_hasBeenEnabled)
            {
                QueueEvaluation(EvaluationCause.AutonomyLifecycle);
            }
            else
            {
                _bootstrapPending = !_initialSelectionConsumed;
                _hasBeenEnabled = true;
            }
        }

        private void OnDisable()
        {
            _bootstrapPending = false;
            // Os EventBus não fazem replay; os bindings e a fila preservam transições da Perception durante a pausa.
            CancelActivePlanForLifecycle();
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

            if (_investigateAction != null && _investigateActionOutcomeBinding != null)
            {
                _investigateAction.UnsubscribeFromTerminalOutcomes(_investigateActionOutcomeBinding);
                _investigateActionOutcomeBinding = null;
            }

            if (_approachAction != null && _lostContactPursuitIntentBinding != null)
            {
                _approachAction.UnsubscribeFromLostContactPursuitIntents(_lostContactPursuitIntentBinding);
                _lostContactPursuitIntentBinding = null;
            }

            if (_travelToEscapeAction != null && _travelActionOutcomeBinding != null)
            {
                _travelToEscapeAction.UnsubscribeFromTerminalOutcomes(_travelActionOutcomeBinding);
                _travelActionOutcomeBinding = null;
            }

            if (_gangKnowledgeState != null && _gangKnowledgeChangeBinding != null)
            {
                _gangKnowledgeState.UnsubscribeFromChanges(_gangKnowledgeChangeBinding);
                _gangKnowledgeChangeBinding = null;
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
            PEGANpcLastKnownRickPursuitIntent lostContactIntent = _latestLostContactPursuitIntent;
            bool hasLostContactIntent = _hasPendingLostContactPursuitIntent;
            long terminalRequestId = _pendingTerminalRequestId;
            _bootstrapPending = false;
            ClearQueuedEvaluation();

            if (hasLostContactIntent &&
                !_approachAction.IsCurrentLostContactPursuitIntent(lostContactIntent, out string lostIntentDiagnostic))
            {
                causes &= ~EvaluationCause.LastKnownPursuitIntent;
                hasLostContactIntent = false;
                _log.Info(
                    "NPC discarded a superseded last-known pursuit intent before consolidated GOAP evaluation.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", lostContactIntent.SubjectActorId.StableText),
                    LogFields.Field("contactLossObservationTimeSeconds", lostContactIntent.ContactLossObservationTimeSeconds),
                    LogFields.Field("diagnostic", lostIntentDiagnostic));
            }

            if (!_bootstrapPending && causes == EvaluationCause.None && !_planEvaluationQueued)
            {
                return;
            }

            if ((causes & EvaluationCause.InitialKnowledge) != 0 &&
                (causes & ~EvaluationCause.InitialKnowledge) == 0 &&
                !TryGetCurrentPerceptionBackedRick(out _) &&
                !HasCurrentGangEscapeTargets())
            {
                return;
            }

            EvaluateAndApplyPlan(
                causes,
                hasRequest ? request : null,
                hasLostContactIntent ? lostContactIntent : (PEGANpcLastKnownRickPursuitIntent?)null,
                terminalRequestId);
        }

        private bool TryInitialize(out string diagnostic)
        {
            diagnostic = string.Empty;
            _actorReference = GetComponent<ActorOccurrenceReferenceBinding>();
            _knowledgeState = GetComponent<PEGANpcKnowledgeState>();
            _perceptionSource = GetComponent<PEGANpcPerceptionSource>();
            _desireState = GetComponent<PEGANpcDesireState>();
            _approachAction = GetComponent<PEGANpcApproachKnownRickAction>();
            _investigateAction = GetComponent<PEGANpcInvestigateLastKnownRickPositionAction>();
            _travelToEscapeAction = GetComponent<PEGANpcTravelToEscapeAction>();
            _gangMembership = GetComponent<PEGANpcGangMembership>();
            _gangKnowledgeState = _gangMembership != null ? _gangMembership.GangKnowledgeState : null;

            if (_actorReference == null || _knowledgeState == null || _perceptionSource == null ||
                _desireState == null || _approachAction == null || _investigateAction == null ||
                _travelToEscapeAction == null)
            {
                diagnostic = "Actor reference, Knowledge State, Perception Source, Desire State, pursuit Actions and Travel to Escape Action must exist on this NPC root.";
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

        private void OnGangKnowledgeChanged(PEGANpcGangKnowledgeFactChangedEvent change)
        {
            if (change == null ||
                change.EvidenceOrigin != PEGANpcGangKnowledgeEvidenceOrigin.SpawnPointPhysicalIngress ||
                _gangKnowledgeState == null ||
                _gangMembership == null ||
                _gangMembership.GangKnowledgeState != _gangKnowledgeState ||
                change.Target.Kind != PEGANpcPlanningTargetKind.EscapeDestination ||
                change.Target.Provenance != PEGANpcPlanningTargetProvenance.KnowledgeState ||
                !IsCurrentGangEscapeTarget(
                    change.Target.TargetId,
                    change.Target.KnownPosition,
                    change.Target.EvidenceTimeSeconds))
            {
                return;
            }

            _log.Info(
                "Legitimate Gang Spawn Point knowledge queued event-driven GOAP reevaluation.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("spawnPointId", change.SpawnPointId),
                LogFields.Field("targetId", change.Target.TargetId),
                LogFields.Field("targetKind", change.Target.Kind),
                LogFields.Field("targetProvenance", change.Target.Provenance),
                LogFields.Field("evidenceOrigin", change.EvidenceOrigin),
                LogFields.Field("evidenceTimeSeconds", change.EvidenceTimeSeconds));
            QueueEvaluation(EvaluationCause.GangKnowledgeChange);
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

            OpportunityStamp currentOpportunity = new OpportunityStamp(
                (int)request.Trigger,
                request.TargetActorId,
                request.ObservationTimeSeconds);
            bool acceptedOpportunity = request.Trigger == PEGANpcPlanReevaluationTrigger.RickReacquired
                ? TryAcceptOpportunity(
                    currentOpportunity,
                    ref _lastReacquisitionOpportunity,
                    ref _hasLastReacquisitionOpportunity)
                : TryAcceptOpportunity(
                    currentOpportunity,
                    ref _lastEvadedOpportunity,
                    ref _hasLastEvadedOpportunity);
            if (!acceptedOpportunity)
            {
                _log.Info(
                    "NPC ignored a duplicate or stale Perception reevaluation opportunity.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("trigger", request.Trigger),
                    LogFields.Field("targetActorId", request.TargetActorId.StableText),
                    LogFields.Field("observationTimeSeconds", request.ObservationTimeSeconds));
                return;
            }

            ClosePursuitContinuation($"Perception published {request.Trigger}.");

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

        private void OnLostContactPursuitIntentRequested(
            PEGANpcLastKnownRickPursuitIntentRequestedEvent intentEvent)
        {
            if (intentEvent == null || !intentEvent.Intent.IsValid)
            {
                _log.Warning(
                    "NPC ignored a lost-contact pursuit intent that did not match its current legitimate Knowledge State.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", intentEvent != null ? intentEvent.Intent.SubjectActorId.StableText : string.Empty),
                    LogFields.Field("diagnostic", intentEvent == null ? "The intent event was null." : "The intent data was invalid."));
                return;
            }

            if (!_approachAction.IsCurrentLostContactPursuitIntent(intentEvent.Intent, out string diagnostic))
            {
                _log.Warning(
                    "NPC ignored a lost-contact pursuit intent that did not match its current legitimate Knowledge State.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", intentEvent.Intent.SubjectActorId.StableText),
                    LogFields.Field("diagnostic", diagnostic));
                return;
            }

            OpportunityStamp opportunity = new OpportunityStamp(
                10,
                intentEvent.Intent.SubjectActorId,
                intentEvent.Intent.ContactLossObservationTimeSeconds);
            if (!TryAcceptOpportunity(
                    opportunity,
                    ref _lastLostContactOpportunity,
                    ref _hasLastLostContactOpportunity))
            {
                _log.Info(
                    "NPC ignored a duplicate or stale last-known pursuit opportunity.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", intentEvent.Intent.SubjectActorId.StableText),
                    LogFields.Field("contactLossObservationTimeSeconds", intentEvent.Intent.ContactLossObservationTimeSeconds));
                return;
            }

            if (!TryGetPursuitDesireOwner(intentEvent.Intent.SubjectActorId, out string desireId))
            {
                _log.Info(
                    "NPC did not open a pursuit continuation because no selected GOAP plan currently owns Approach Known Rick.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", intentEvent.Intent.SubjectActorId.StableText),
                    LogFields.Field("contactLossObservationTimeSeconds", intentEvent.Intent.ContactLossObservationTimeSeconds));
                return;
            }

            bool approachAlreadyRunning = _hasActivePlan &&
                _activePlan.ActionId == PEGANpcPlanningActionId.ApproachKnownRick &&
                _approachAction.Outcome == PEGANpcActionOutcome.Running;
            bool alreadyAtLastKnown = PlanarDistance(transform.position, intentEvent.Intent.LastKnownPosition) <=
                _approachAction.DeclaredEffect.TargetRadiusMeters;
            bool lastKnownPositionAlreadyReached = alreadyAtLastKnown;
            _activePursuitContinuation = new PEGANpcPursuitContinuation(
                desireId,
                intentEvent.Intent.SubjectActorId,
                intentEvent.Intent.ContactLossObservationTimeSeconds,
                intentEvent.Intent.LastKnownPosition,
                intentEvent.Intent.LastKnownObservationTimeSeconds,
                false,
                lastKnownPositionAlreadyReached);
            _hasActivePursuitContinuation = _activePursuitContinuation.IsValid;
            _attemptedInvestigationTargets.Clear();
            if (!_hasActivePursuitContinuation)
            {
                _log.Error(
                    "NPC could not create a valid local pursuit continuation from the accepted Perception loss.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("desireId", desireId),
                    LogFields.Field("subjectActorId", intentEvent.Intent.SubjectActorId.StableText));
                return;
            }

            _log.Info(
                "NPC opened a bounded GOAP pursuit continuation from its legitimate last-known position.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("desireId", desireId),
                LogFields.Field("subjectActorId", intentEvent.Intent.SubjectActorId.StableText),
                LogFields.Field("targetPosition", intentEvent.Intent.LastKnownPosition),
                LogFields.Field("positionEvidenceTimeSeconds", intentEvent.Intent.LastKnownObservationTimeSeconds),
                LogFields.Field("contactLossObservationTimeSeconds", intentEvent.Intent.ContactLossObservationTimeSeconds),
                LogFields.Field("lastKnownApproachAlreadyRunning", approachAlreadyRunning),
                LogFields.Field("alreadyAtLastKnownPosition", alreadyAtLastKnown),
                LogFields.Field("lastKnownPositionAlreadyReached", lastKnownPositionAlreadyReached),
                LogFields.Field("lastKnownPositionReached", _activePursuitContinuation.LastKnownPositionReached),
                LogFields.Field("despiseTimeoutOwner", nameof(PEGANpcPerceptionSource)),
                LogFields.Field("activeRequestId", _activeActionRequestId));
            QueueEvaluation(EvaluationCause.LastKnownPursuitIntent, lostContactIntent: intentEvent.Intent);
        }

        private bool TryGetPursuitDesireOwner(ActorId subjectActorId, out string desireId)
        {
            desireId = string.Empty;
            if (_hasActivePlan &&
                _activePlan.ActionId == PEGANpcPlanningActionId.ApproachKnownRick &&
                _activePlan.TargetId == PEGANpcPlanningTargetId.ForActor(subjectActorId))
            {
                desireId = _activePlan.DesireId;
                return !string.IsNullOrWhiteSpace(desireId);
            }

            if (_latestSelection == null || !_latestSelection.IsValid || !_latestSelection.HasSelectedPlan)
            {
                return false;
            }

            PEGANpcPlanSnapshot selected = _latestSelection.SelectedPlan;
            if (selected.ActionId != PEGANpcPlanningActionId.ApproachKnownRick ||
                selected.TargetId != PEGANpcPlanningTargetId.ForActor(subjectActorId))
            {
                return false;
            }

            desireId = selected.DesireId;
            return !string.IsNullOrWhiteSpace(desireId);
        }

        private void ClosePursuitContinuation(string reason)
        {
            if (!_hasActivePursuitContinuation)
            {
                return;
            }

            _log.Info(
                "NPC closed its local pursuit continuation after a legitimate Perception transition.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("desireId", _activePursuitContinuation.DesireId),
                LogFields.Field("subjectActorId", _activePursuitContinuation.SubjectActorId.StableText),
                LogFields.Field("contactLossObservationTimeSeconds", _activePursuitContinuation.ContactLossObservationTimeSeconds),
                LogFields.Field("attemptedLocalTargetCount", _attemptedInvestigationTargets.Count),
                LogFields.Field("reason", reason));
            _hasActivePursuitContinuation = false;
            _activePursuitContinuation = default;
            _attemptedInvestigationTargets.Clear();
            _latestLostContactPursuitIntent = default;
            _hasPendingLostContactPursuitIntent = false;
            _pendingEvaluationCauses &= ~EvaluationCause.LastKnownPursuitIntent;
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
            PEGANpcLastKnownRickPursuitIntent? lostContactIntent,
            long terminalRequestId)
        {
            if (!TryBuildLiveSelection(
                    out ActorId rickActorId,
                    out IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
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
                "NPC consolidated event-driven GOAP reevaluation against its current individual and Gang knowledge.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("causes", causes),
                LogFields.Field("targetActorId", rickActorId.IsValid ? rickActorId.StableText : string.Empty),
                LogFields.Field("reevaluationTrigger", request != null ? request.Trigger : default),
                LogFields.Field("reevaluationObservationTimeSeconds", request != null ? request.ObservationTimeSeconds : -1d),
                LogFields.Field("terminalRequestId", terminalRequestId),
                LogFields.Field("selectionValid", selection.IsValid),
                LogFields.Field("actionCandidateCount", candidates.Count),
                LogFields.Field("candidatePlanCount", selection.CandidatePlans.Count),
                LogFields.Field("hasSelectedPlan", selection.HasSelectedPlan),
                LogFields.Field("pursuitContinuationActive", _hasActivePursuitContinuation),
                LogFields.Field("lastKnownPositionReached", _hasActivePursuitContinuation &&
                    _activePursuitContinuation.LastKnownPositionReached),
                LogFields.Field("distanceToLastKnownMeters", _hasActivePursuitContinuation
                    ? PlanarDistance(transform.position, _activePursuitContinuation.LastKnownPosition)
                    : -1f),
                LogFields.Field("approachRunning", _approachAction.Outcome == PEGANpcActionOutcome.Running),
                LogFields.Field("diagnostic", selection.Diagnostic));

            if (!selection.IsValid ||
                !selection.HasSelectedPlan)
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
            if (!TryFindCandidate(candidates, selectedPlan, out PEGANpcPlanningActionCandidate candidate))
            {
                HandleNoSelectedPlan(
                    selection,
                    causes,
                    "The selected GOAP plan has no corresponding live Action candidate.");
                return;
            }

            bool selectedTargetIsValid = selectedPlan.ActionId switch
            {
                PEGANpcPlanningActionId.ApproachKnownRick =>
                    selectedPlan.TargetProvenance == PEGANpcPlanningTargetProvenance.KnowledgeState &&
                    selectedPlan.TargetKind == PEGANpcPlanningTargetKind.CharacterPosition &&
                    selectedPlan.TargetId == PEGANpcPlanningTargetId.ForActor(rickActorId),
                PEGANpcPlanningActionId.TravelToEscapeTarget =>
                    !_hasActivePursuitContinuation &&
                    selectedPlan.Purpose == PEGANpcPlanPurpose.DesireGoal &&
                    selectedPlan.TargetProvenance == PEGANpcPlanningTargetProvenance.KnowledgeState &&
                    selectedPlan.TargetKind == PEGANpcPlanningTargetKind.EscapeDestination &&
                    IsCurrentGangEscapeTarget(
                        selectedPlan.TargetId,
                        selectedPlan.TargetPosition,
                        selectedPlan.EvidenceTimeSeconds),
                PEGANpcPlanningActionId.InvestigateLastKnownRickPosition =>
                    _hasActivePursuitContinuation &&
                    selectedPlan.Purpose == PEGANpcPlanPurpose.PursuitContinuationStep &&
                    selectedPlan.TargetProvenance == PEGANpcPlanningTargetProvenance.DerivedFromKnowledge &&
                    selectedPlan.TargetKind == PEGANpcPlanningTargetKind.LocalObservationPosition,
                _ => false
            };
            if (!selectedTargetIsValid)
            {
                HandleNoSelectedPlan(selection, causes, "The selected GOAP plan has an unsupported Action/Target contract.");
                return;
            }

            OpportunityStamp selectedOpportunity = CreateOpportunityStamp(
                rickActorId,
                selectedPlan,
                request,
                lostContactIntent,
                causes);
            if (_hasActivePlan)
            {
                if (GetActionOutcome(_activePlan.ActionId) == PEGANpcActionOutcome.Running &&
                    AreEquivalentPlans(_activePlan, selectedPlan))
                {
                    // O plano em execução continua vinculado ao destino físico do request original.
                    // Uma evidência equivalente pode atualizar a oportunidade, mas não o Effect declarado pela Action.
                    _activePlanOpportunity = selectedOpportunity;
                    _log.Info(
                        "NPC preserved the running Action because the selected GOAP plan and its evidence remain equivalent.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("causes", causes),
                        LogFields.Field("requestId", _activeActionRequestId),
                        LogFields.Field("desireId", selectedPlan.DesireId),
                        LogFields.Field("actionId", selectedPlan.ActionId),
                        LogFields.Field("targetId", selectedPlan.TargetId),
                        LogFields.Field("executingTargetPosition", _activePlan.TargetPosition),
                        LogFields.Field("selectedTargetPosition", selectedPlan.TargetPosition),
                        LogFields.Field("planPurpose", selectedPlan.Purpose),
                        LogFields.Field("maximumRouteDistanceFromAnchorMeters", selectedPlan.MaximumRouteDistanceFromAnchorMeters),
                        LogFields.Field("executingPositionEvidenceTimeSeconds", _activePlan.EvidenceTimeSeconds),
                        LogFields.Field("selectedPositionEvidenceTimeSeconds", selectedPlan.EvidenceTimeSeconds),
                        LogFields.Field("predictedEffect", selectedPlan.PredictedEffect),
                        LogFields.Field("predictedGoal", selectedPlan.PredictedGoal));
                    return;
                }

                if (GetActionOutcome(_activePlan.ActionId) == PEGANpcActionOutcome.Running &&
                    CanPromoteApproachToContinuation(_activePlan, selectedPlan))
                {
                    _activePlan = selectedPlan;
                    _activePlanOpportunity = selectedOpportunity;
                    _log.Info(
                        "NPC retained its valid Approach Known Rick navigation while the GOAP plan entered its lost-contact continuation.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("requestId", _activeActionRequestId),
                        LogFields.Field("desireId", selectedPlan.DesireId),
                        LogFields.Field("targetPosition", selectedPlan.TargetPosition),
                        LogFields.Field("contactLossObservationTimeSeconds", _activePursuitContinuation.ContactLossObservationTimeSeconds));
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
                     ((_recentTerminalOutcome == PEGANpcActionOutcome.Success &&
                       IsEffectAlreadySatisfiedAtCurrentPosition(selectedPlan)) ||
                      (_hasRecentTerminalOpportunity &&
                       selectedOpportunity.IsSameAs(_recentTerminalOpportunity))))
            {
                _log.Info(
                    "NPC retained the terminal result for the same opportunity or because its physical Effect is already satisfied.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("causes", causes),
                    LogFields.Field("previousOutcome", _recentTerminalOutcome),
                    LogFields.Field("desireId", selectedPlan.DesireId),
                    LogFields.Field("actionId", selectedPlan.ActionId),
                    LogFields.Field("targetId", selectedPlan.TargetId),
                    LogFields.Field("targetPosition", selectedPlan.TargetPosition),
                    LogFields.Field("planPurpose", selectedPlan.Purpose),
                    LogFields.Field("maximumRouteDistanceFromAnchorMeters", selectedPlan.MaximumRouteDistanceFromAnchorMeters),
                    LogFields.Field("opportunityKind", selectedOpportunity.Kind),
                    LogFields.Field("opportunityTimeSeconds", selectedOpportunity.ObservationTimeSeconds),
                    LogFields.Field("sameOpportunity", _hasRecentTerminalOpportunity && selectedOpportunity.IsSameAs(_recentTerminalOpportunity)),
                    LogFields.Field("effectAlreadySatisfied", _recentTerminalOutcome == PEGANpcActionOutcome.Success && IsEffectAlreadySatisfiedAtCurrentPosition(selectedPlan)),
                    LogFields.Field("positionEvidenceTimeSeconds", selectedPlan.EvidenceTimeSeconds),
                    LogFields.Field("predictedEffect", selectedPlan.PredictedEffect),
                    LogFields.Field("predictedGoal", selectedPlan.PredictedGoal));
                return;
            }

            StartSelectedPlan(selection, candidate, causes, selectedOpportunity);
        }

        private static bool TryFindCandidate(
            IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
            PEGANpcPlanSnapshot plan,
            out PEGANpcPlanningActionCandidate candidate)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                PEGANpcPlanningActionCandidate current = candidates[index];
                if (current.ActionId == plan.ActionId && current.TargetId == plan.TargetId)
                {
                    candidate = current;
                    return true;
                }
            }

            candidate = default;
            return false;
        }

        private OpportunityStamp CreateOpportunityStamp(
            ActorId actorId,
            PEGANpcPlanSnapshot selectedPlan,
            PEGANpcPlanReevaluationRequestedEvent request,
            PEGANpcLastKnownRickPursuitIntent? lostContactIntent,
            EvaluationCause causes)
        {
            if (_hasActivePursuitContinuation)
            {
                return new OpportunityStamp(
                    1,
                    _activePursuitContinuation.SubjectActorId,
                    _activePursuitContinuation.ContactLossObservationTimeSeconds);
            }

            if (lostContactIntent.HasValue)
            {
                return new OpportunityStamp(
                    1,
                    lostContactIntent.Value.SubjectActorId,
                    lostContactIntent.Value.ContactLossObservationTimeSeconds);
            }

            if (request != null)
            {
                int kind = request.Trigger == PEGANpcPlanReevaluationTrigger.RickConsideredEvaded ? 1 : 2;
                return new OpportunityStamp(kind, request.TargetActorId, request.ObservationTimeSeconds);
            }

            if ((causes & EvaluationCause.ActionTerminalOutcome) != 0 &&
                (causes & EvaluationCause.RelevantKnowledgeChange) == 0 &&
                _hasRecentTerminalOpportunity)
            {
                return _recentTerminalOpportunity;
            }

            return new OpportunityStamp(3, actorId, selectedPlan.EvidenceTimeSeconds);
        }

        private bool IsEffectAlreadySatisfiedAtCurrentPosition(PEGANpcPlanSnapshot plan)
        {
            PEGANpcNavigationAction action = GetAction(plan.ActionId);
            PEGANpcActionEffectDeclaration effect = action != null ? action.DeclaredEffect : default;
            return effect.IsTargetResolved &&
                   effect.Kind == plan.PredictedEffect &&
                   IsFiniteNonNegative(effect.TargetRadiusMeters) &&
                   PlanarDistance(transform.position, plan.TargetPosition) <= effect.TargetRadiusMeters;
        }

        private static bool IsFiniteNonNegative(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private bool TryBuildLiveSelection(
            out ActorId rickActorId,
            out IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
            out PEGANpcPlanSelectionResult selection,
            out string diagnostic)
        {
            rickActorId = default;
            var targets = new List<PEGANpcPlanningTarget>();
            var actionCandidates = new List<PEGANpcPlanningActionCandidate>();
            candidates = Array.AsReadOnly(Array.Empty<PEGANpcPlanningActionCandidate>());
            selection = null;
            diagnostic = string.Empty;
            if (_hasActivePursuitContinuation)
            {
                if (!TryGetCurrentActorId(out rickActorId))
                {
                    diagnostic = "The active pursuit continuation has no current Player Actor occurrence.";
                    return false;
                }

                PEGANpcKnowledgeFact<ActorProfileId> continuationIdentity =
                    _knowledgeState.GetObservedCharacterIdentity(rickActorId);
                PEGANpcKnowledgeFact<Vector3> continuationPosition =
                    _knowledgeState.GetLastObservedRickPosition(rickActorId);
                if (!continuationIdentity.TryGetValue(out _) ||
                    (continuationIdentity.Origins & PEGANpcEvidenceOrigin.Perception) == 0 ||
                    !continuationPosition.TryGetValue(out Vector3 continuationKnownPosition) ||
                    !continuationPosition.HasObservationTime ||
                    (continuationPosition.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
                {
                    diagnostic = "Pursuit continuation requires this NPC's Perception-backed Rick identity and last known position.";
                    return false;
                }

                if (_activePursuitContinuation.SubjectActorId != rickActorId ||
                    !_approachAction.IsCurrentLostContactPursuitIntent(
                        _activePursuitContinuation.ToIntent(),
                        out diagnostic))
                {
                    diagnostic = string.IsNullOrEmpty(diagnostic)
                        ? "The active pursuit continuation no longer matches this NPC's current Rick evidence."
                        : diagnostic;
                    return false;
                }

                if (!_activePursuitContinuation.LastKnownPositionReached &&
                    PlanarDistance(transform.position, _activePursuitContinuation.LastKnownPosition) <=
                    _approachAction.DeclaredEffect.TargetRadiusMeters)
                {
                    _activePursuitContinuation = _activePursuitContinuation.WithLastKnownPositionReached();
                }

                if (TryCreateApproachContinuationCandidate(
                        rickActorId,
                        continuationKnownPosition,
                        continuationPosition.ObservationTimeSeconds,
                        out PEGANpcPlanningTarget approachTarget,
                        out PEGANpcPlanningActionCandidate approachCandidate,
                        out string approachDiagnostic))
                {
                    targets.Add(approachTarget);
                    actionCandidates.Add(approachCandidate);
                }
                else if (string.IsNullOrEmpty(approachDiagnostic))
                {
                    diagnostic = "No last-known approach candidate is required for this continuation phase.";
                }

                if (_activePursuitContinuation.LastKnownPositionReached)
                {
                    if (!_investigateAction.TryCreatePlanningCandidates(
                            _activePursuitContinuation,
                            _attemptedInvestigationTargets,
                            out IReadOnlyList<PEGANpcPlanningTarget> localTargets,
                            out IReadOnlyList<PEGANpcPlanningActionCandidate> localCandidates,
                            out string localDiagnostic))
                    {
                        diagnostic = localDiagnostic;
                        return false;
                    }

                    if (localCandidates.Count == 0)
                    {
                        _log.Info(
                            "NPC has no usable local investigation route for the current lost-contact opportunity.",
                            LogFields.Field("actorRoot", gameObject.name),
                            LogFields.Field("distanceToLastKnownMeters",
                                PlanarDistance(transform.position, _activePursuitContinuation.LastKnownPosition)),
                            LogFields.Field("attemptedLocalTargetCount", _attemptedInvestigationTargets.Count),
                            LogFields.Field("diagnostic", localDiagnostic));
                    }

                    for (int index = 0; index < localTargets.Count; index++)
                    {
                        targets.Add(localTargets[index]);
                    }

                    for (int index = 0; index < localCandidates.Count; index++)
                    {
                        actionCandidates.Add(localCandidates[index]);
                    }
                }
            }
            else
            {
                if (TryGetCurrentActorId(out rickActorId) &&
                    HasPerceptionBackedRickPosition(rickActorId))
                {
                    PEGANpcKnowledgeFact<bool> contact = _knowledgeState.GetRickVisualContact(rickActorId);
                    if (contact.TryGetValue(out bool hasVisualContact) &&
                        hasVisualContact &&
                        (contact.Origins & PEGANpcEvidenceOrigin.Perception) != 0)
                    {
                        if (_approachAction.TryCreatePlanningCandidate(
                                out PEGANpcPlanningTarget target,
                                out PEGANpcPlanningActionCandidate candidate,
                                out string approachDiagnostic))
                        {
                            targets.Add(target);
                            actionCandidates.Add(candidate);
                        }
                        else if (!string.IsNullOrWhiteSpace(approachDiagnostic))
                        {
                            _log.Info(
                                "NPC excluded Approach Known Rick because its real Perception-backed route candidate is unavailable.",
                                LogFields.Field("actorRoot", gameObject.name),
                                LogFields.Field("targetActorId", rickActorId.StableText),
                                LogFields.Field("diagnostic", approachDiagnostic));
                        }
                    }
                }

                AddKnownGangEscapeCandidates(targets, actionCandidates);
            }

            candidates = Array.AsReadOnly(actionCandidates.ToArray());
            PEGANpcPlanningInput input = new PEGANpcPlanningInput(
                PEGANpcPlanningValidationMode.LiveKnowledge,
                PEGANpcDesireContext.Normal,
                targets,
                candidates,
                _hasActivePursuitContinuation
                    ? _activePursuitContinuation
                    : (PEGANpcPursuitContinuation?)null);
            selection = PEGANpcPlanSelector.Select(
                input,
                _normalContext,
                _desireState.GetSnapshot());
            diagnostic = selection.Diagnostic;
            return selection.IsValid;
        }

        private bool HasPerceptionBackedRickPosition(ActorId actorId)
        {
            PEGANpcKnowledgeFact<ActorProfileId> identity =
                _knowledgeState.GetObservedCharacterIdentity(actorId);
            PEGANpcKnowledgeFact<Vector3> position =
                _knowledgeState.GetLastObservedRickPosition(actorId);
            return identity.TryGetValue(out _) &&
                   (identity.Origins & PEGANpcEvidenceOrigin.Perception) != 0 &&
                   position.TryGetValue(out _) &&
                   position.HasObservationTime &&
                   (position.Origins & PEGANpcEvidenceOrigin.Perception) != 0;
        }

        private void AddKnownGangEscapeCandidates(
            List<PEGANpcPlanningTarget> targets,
            List<PEGANpcPlanningActionCandidate> actionCandidates)
        {
            if (_gangMembership == null)
            {
                return;
            }

            if (!_gangMembership.TryGetKnownEscapeTargets(
                    out IReadOnlyList<PEGANpcPlanningTarget> knownTargets,
                    out string membershipDiagnostic))
            {
                if (!string.IsNullOrWhiteSpace(membershipDiagnostic))
                {
                    _log.Info(
                        "NPC has no Gang knowledge membership from which an escape Target can be resolved.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("diagnostic", membershipDiagnostic));
                }

                return;
            }

            for (int index = 0; index < knownTargets.Count; index++)
            {
                PEGANpcPlanningTarget target = knownTargets[index];
                if (!target.IsValid ||
                    !target.HasEvidenceTime ||
                    target.Kind != PEGANpcPlanningTargetKind.EscapeDestination ||
                    target.Provenance != PEGANpcPlanningTargetProvenance.KnowledgeState)
                {
                    _log.Warning(
                        "NPC ignored a Gang escape Target that did not satisfy the live Knowledge State contract.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("targetId", target.TargetId),
                        LogFields.Field("targetKind", target.Kind),
                        LogFields.Field("targetProvenance", target.Provenance));
                    continue;
                }

                targets.Add(target);
                if (_travelToEscapeAction.TryCreatePlanningCandidate(
                        target,
                        EscapeMovementSpeedRegime,
                        out PEGANpcPlanningActionCandidate candidate,
                        out string routeDiagnostic))
                {
                    actionCandidates.Add(candidate);
                    continue;
                }

                _log.Info(
                    "NPC knows a Gang escape Target, but its current NavMesh route is not a complete usable plan candidate.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetId", target.TargetId),
                    LogFields.Field("targetPosition", target.KnownPosition),
                    LogFields.Field("targetEvidenceTimeSeconds", target.EvidenceTimeSeconds),
                    LogFields.Field("speedRegime", EscapeMovementSpeedRegime),
                    LogFields.Field("diagnostic", routeDiagnostic));
            }
        }

        private bool HasCurrentGangEscapeTargets()
        {
            if (_gangMembership == null ||
                !_gangMembership.TryGetKnownEscapeTargets(
                    out IReadOnlyList<PEGANpcPlanningTarget> knownTargets,
                    out _))
            {
                return false;
            }

            for (int index = 0; index < knownTargets.Count; index++)
            {
                PEGANpcPlanningTarget target = knownTargets[index];
                if (target.IsValid &&
                    target.Kind == PEGANpcPlanningTargetKind.EscapeDestination &&
                    target.Provenance == PEGANpcPlanningTargetProvenance.KnowledgeState)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCurrentGangEscapeTarget(
            PEGANpcPlanningTargetId targetId,
            Vector3 targetPosition,
            double evidenceTimeSeconds)
        {
            if (_gangMembership == null ||
                !_gangMembership.TryGetKnownEscapeTargets(
                    out IReadOnlyList<PEGANpcPlanningTarget> knownTargets,
                    out _))
            {
                return false;
            }

            for (int index = 0; index < knownTargets.Count; index++)
            {
                PEGANpcPlanningTarget target = knownTargets[index];
                if (target.TargetId == targetId &&
                    target.IsValid &&
                    target.HasEvidenceTime &&
                    target.Kind == PEGANpcPlanningTargetKind.EscapeDestination &&
                    target.Provenance == PEGANpcPlanningTargetProvenance.KnowledgeState &&
                    target.KnownPosition.Equals(targetPosition) &&
                    Math.Abs(target.EvidenceTimeSeconds - evidenceTimeSeconds) <= 0.000001d)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryCreateApproachContinuationCandidate(
            ActorId rickActorId,
            Vector3 knownPosition,
            double evidenceTimeSeconds,
            out PEGANpcPlanningTarget target,
            out PEGANpcPlanningActionCandidate candidate,
            out string diagnostic)
        {
            target = default;
            candidate = default;
            diagnostic = string.Empty;
            if (_activePursuitContinuation.LastKnownApproachAttempted ||
                _activePursuitContinuation.LastKnownPositionReached ||
                PlanarDistance(transform.position, _activePursuitContinuation.LastKnownPosition) <=
                _approachAction.DeclaredEffect.TargetRadiusMeters)
            {
                return false;
            }

            if (!_approachAction.TryCreatePlanningCandidate(
                    out target,
                    out candidate,
                    out diagnostic))
            {
                return false;
            }

            if (target.Provenance != PEGANpcPlanningTargetProvenance.KnowledgeState ||
                target.TargetId != PEGANpcPlanningTargetId.ForActor(rickActorId) ||
                candidate.ActionId != PEGANpcPlanningActionId.ApproachKnownRick ||
                !target.KnownPosition.Equals(_activePursuitContinuation.LastKnownPosition) ||
                Math.Abs(target.EvidenceTimeSeconds - _activePursuitContinuation.PositionEvidenceTimeSeconds) > 0.000001d ||
                !knownPosition.Equals(_activePursuitContinuation.LastKnownPosition) ||
                Math.Abs(evidenceTimeSeconds - _activePursuitContinuation.PositionEvidenceTimeSeconds) > 0.000001d)
            {
                diagnostic = "The continuation Approach candidate is not bound to the preserved last Perception position.";
                return false;
            }

            return true;
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
                PEGANpcNavigationAction preservedAction = GetAction(_activePlan.ActionId);
                _log.Info(
                    "NPC preserved its running Action because its current Desire, legitimate Knowledge evidence and execution remain valid despite no replacement plan being selectable.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("causes", causes),
                    LogFields.Field("requestId", _activeActionRequestId),
                    LogFields.Field("desireId", _activePlan.DesireId),
                    LogFields.Field("targetActorId", _activePlan.TargetId),
                    LogFields.Field("targetEvidenceDeltaMeters", PlanarDistance(knownPosition, _activePlan.TargetPosition)),
                    LogFields.Field("targetChangeToleranceMeters", preservedAction != null
                        ? preservedAction.DeclaredEffect.TargetRadiusMeters
                        : 0f),
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
                LogFields.Field("lastKnownPositionReached", _hasActivePursuitContinuation &&
                    _activePursuitContinuation.LastKnownPositionReached),
                LogFields.Field("diagnostic", diagnostic));
        }

        private bool CanPreserveActiveExecution(
            out Vector3 knownPosition,
            out double positionEvidenceTimeSeconds)
        {
            knownPosition = default;
            positionEvidenceTimeSeconds = 0d;
            PEGANpcNavigationAction activeAction = _hasActivePlan ? GetAction(_activePlan.ActionId) : null;
            PEGANpcActionExecutionSnapshot execution = activeAction != null
                ? activeAction.CurrentExecution
                : default;
            PEGANpcActionEffectDeclaration activeEffect = execution.DeclaredEffect;
            if (!_hasActivePlan ||
                activeAction == null ||
                execution.Outcome != PEGANpcActionOutcome.Running ||
                execution.RequestId != _activeActionRequestId ||
                !activeEffect.IsTargetResolved ||
                activeEffect.Kind != _activePlan.PredictedEffect ||
                PlanarDistance(activeEffect.TargetPosition, _activePlan.TargetPosition) > 0.001f)
            {
                return false;
            }

            if (_hasActivePursuitContinuation &&
                _activePlan.Purpose != PEGANpcPlanPurpose.PursuitContinuationStep)
            {
                return false;
            }

            if (!_hasActivePursuitContinuation &&
                _activePlan.ActionId == PEGANpcPlanningActionId.TravelToEscapeTarget)
            {
                if (activeAction != _travelToEscapeAction ||
                    _activePlan.Purpose != PEGANpcPlanPurpose.DesireGoal ||
                    _activePlan.TargetKind != PEGANpcPlanningTargetKind.EscapeDestination ||
                    _activePlan.TargetProvenance != PEGANpcPlanningTargetProvenance.KnowledgeState ||
                    !IsCurrentGangEscapeTarget(
                        _activePlan.TargetId,
                        _activePlan.TargetPosition,
                        _activePlan.EvidenceTimeSeconds) ||
                    !IsDesireApplicable(_activePlan.DesireId))
                {
                    return false;
                }

                knownPosition = _activePlan.TargetPosition;
                positionEvidenceTimeSeconds = _activePlan.EvidenceTimeSeconds;
                return true;
            }

            if (!TryGetCurrentActorId(out ActorId currentActorId))
            {
                return false;
            }

            if (_activePlan.Purpose == PEGANpcPlanPurpose.PursuitContinuationStep)
            {
                if (!_hasActivePursuitContinuation ||
                    _activePursuitContinuation.SubjectActorId != currentActorId ||
                    !_approachAction.IsCurrentLostContactPursuitIntent(
                        _activePursuitContinuation.ToIntent(),
                        out _))
                {
                    return false;
                }

                if (_activePlan.ActionId == PEGANpcPlanningActionId.ApproachKnownRick)
                {
                    if (activeAction != _approachAction ||
                        _activePlan.TargetId != PEGANpcPlanningTargetId.ForActor(currentActorId) ||
                        _activePlan.TargetKind != PEGANpcPlanningTargetKind.CharacterPosition ||
                        !_approachAction.TryGetKnownRickPosition(out Vector3 currentKnownPosition, out _) ||
                        !IsTargetPositionWithinActionTolerance(
                            currentKnownPosition,
                            _activePlan.TargetPosition,
                            _activePlan.ActionId))
                    {
                        return false;
                    }
                }
                else if (_activePlan.ActionId == PEGANpcPlanningActionId.InvestigateLastKnownRickPosition)
                {
                    if (activeAction != _investigateAction ||
                        _activePlan.TargetKind != PEGANpcPlanningTargetKind.LocalObservationPosition ||
                        !_investigateAction.IsLocalTargetUsable(
                            _activePlan,
                            _activePursuitContinuation,
                            out _,
                            requireFurtherPhysicalMovement: false))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                knownPosition = _activePursuitContinuation.LastKnownPosition;
                positionEvidenceTimeSeconds = _activePursuitContinuation.PositionEvidenceTimeSeconds;
                return true;
            }

            if (_activePlan.TargetId != PEGANpcPlanningTargetId.ForActor(currentActorId))
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
                !IsTargetPositionWithinActionTolerance(
                    knownPosition,
                    _activePlan.TargetPosition,
                    _activePlan.ActionId))
            {
                return false;
            }

            positionEvidenceTimeSeconds = position.ObservationTimeSeconds;

            return IsDesireApplicable(_activePlan.DesireId);
        }

        private bool CancelActivePlanForReplacement(string diagnostic)
        {
            if (!_hasActivePlan)
            {
                return true;
            }

            PEGANpcNavigationAction action = GetAction(_activePlan.ActionId);
            if (action == null || action.Outcome != PEGANpcActionOutcome.Running)
            {
                ClearActivePlan();
                return true;
            }

            long previousRequestId = _activeActionRequestId;
            _isReplacingPlan = true;
            bool cancelled;
            try
            {
                cancelled = action.CancelExecution(diagnostic);
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
                    LogFields.Field("actionOutcome", action.Outcome),
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

        private void CancelActivePlanForLifecycle()
        {
            if (!_hasActivePlan)
            {
                return;
            }

            PEGANpcPlanSnapshot pausedPlan = _activePlan;
            long pausedRequestId = _activeActionRequestId;
            PEGANpcNavigationAction action = GetAction(pausedPlan.ActionId);
            if (action == null || action.Outcome != PEGANpcActionOutcome.Running)
            {
                ClearActivePlan();
                return;
            }

            _isStoppingForLifecycle = true;
            bool cancelled;
            try
            {
                cancelled = action.CancelExecution(
                    "NPC autonomy was disabled; the active physical Action was released until the next lifecycle evaluation.");
            }
            finally
            {
                _isStoppingForLifecycle = false;
            }

            if (_hasActivePlan)
            {
                ClearActivePlan();
            }

            _log.Info(
                "NPC autonomy released its active Action for a later lifecycle evaluation.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("requestId", pausedRequestId),
                LogFields.Field("actionId", pausedPlan.ActionId),
                LogFields.Field("actionOutcome", action.Outcome),
                LogFields.Field("cancelled", cancelled),
                LogFields.Field("pursuitContinuationPreserved", _hasActivePursuitContinuation));
        }

        private void StartSelectedPlan(
            PEGANpcPlanSelectionResult selection,
            PEGANpcPlanningActionCandidate candidate,
            EvaluationCause causes,
            OpportunityStamp opportunity)
        {
            PEGANpcPlanSnapshot plan = selection.SelectedPlan;
            _initialSelectionConsumed = true;
            if (_hasActivePursuitContinuation &&
                plan.ActionId == PEGANpcPlanningActionId.InvestigateLastKnownRickPosition)
            {
                _attemptedInvestigationTargets.Add(plan.TargetId);
            }

            _log.Info(
                "NPC GOAP selected a valid plan from its live Normal Desire set.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("causes", causes),
                LogFields.Field("context", selection.Context),
                LogFields.Field("desireId", plan.DesireId),
                LogFields.Field("actionId", plan.ActionId),
                LogFields.Field("targetId", plan.TargetId),
                LogFields.Field("targetProvenance", plan.TargetProvenance),
                LogFields.Field("planPurpose", plan.Purpose),
                LogFields.Field("targetPosition", plan.TargetPosition),
                LogFields.Field("positionEvidenceTimeSeconds", plan.EvidenceTimeSeconds),
                LogFields.Field("maximumRouteDistanceFromAnchorMeters", plan.MaximumRouteDistanceFromAnchorMeters),
                LogFields.Field("predictedEffect", plan.PredictedEffect),
                LogFields.Field("predictedGoal", plan.PredictedGoal),
                LogFields.Field("routeLengthMeters", plan.RouteLengthMeters),
                LogFields.Field("estimatedSeconds", plan.EstimatedSeconds),
                LogFields.Field("effectiveDesireWeight", plan.EffectiveDesireWeight),
                LogFields.Field("costPreference", plan.CostPreference),
                LogFields.Field("score", plan.Score),
                LogFields.Field("opportunityKind", opportunity.Kind),
                LogFields.Field("opportunityTimeSeconds", opportunity.ObservationTimeSeconds));

            _activePlan = plan;
            _activePlanOpportunity = opportunity;
            _activeActionRequestId = 0;
            _hasActivePlan = true;
            PEGANpcNavigationAction action = GetAction(plan.ActionId);
            bool started;
            string startDiagnostic = string.Empty;
            switch (plan.ActionId)
            {
                case PEGANpcPlanningActionId.ApproachKnownRick:
                    started = _approachAction.TryStart(out startDiagnostic);
                    break;
                case PEGANpcPlanningActionId.TravelToEscapeTarget:
                    started = _travelToEscapeAction.TryStart(
                        plan.TargetPosition,
                        candidate.SpeedRegime,
                        out startDiagnostic);
                    break;
                case PEGANpcPlanningActionId.InvestigateLastKnownRickPosition:
                    started = _hasActivePursuitContinuation && _investigateAction.TryStart(
                        plan,
                        _activePursuitContinuation,
                        out startDiagnostic);
                    if (!_hasActivePursuitContinuation)
                    {
                        startDiagnostic = "Local investigation requires an active pursuit continuation.";
                    }

                    break;
                default:
                    started = false;
                    startDiagnostic = "The selected GOAP Action is not supported by this autonomy component.";
                    break;
            }

            if (!started)
            {
                if (_hasActivePlan)
                {
                    PEGANpcActionExecutionSnapshot execution = action != null
                        ? action.CurrentExecution
                        : default;
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
                _activeActionRequestId = action.CurrentExecution.RequestId;
                _hasRecentTerminalPlan = false;
                _recentTerminalPlan = default;
                _hasRecentTerminalOpportunity = false;
                _recentTerminalOpportunity = default;
            }

            _log.Info(
                "NPC autonomously started the selected GOAP Action through its existing physical navigation path.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("causes", causes),
                LogFields.Field("requestId", _activeActionRequestId),
                LogFields.Field("desireId", plan.DesireId),
                LogFields.Field("actionId", plan.ActionId),
                LogFields.Field("planPurpose", plan.Purpose),
                LogFields.Field("actionOutcome", action.Outcome),
                LogFields.Field("targetPosition", plan.TargetPosition),
                LogFields.Field("speedRegime", candidate.SpeedRegime),
                LogFields.Field("attemptedInvestigationTargetCount", _attemptedInvestigationTargets.Count));
        }

        private void OnApproachActionTerminalOutcome(PEGANpcActionTerminalOutcomeEvent terminalEvent) =>
            HandleActionTerminalOutcome(_approachAction, terminalEvent);

        private void OnInvestigationActionTerminalOutcome(PEGANpcActionTerminalOutcomeEvent terminalEvent) =>
            HandleActionTerminalOutcome(_investigateAction, terminalEvent);

        private void OnTravelActionTerminalOutcome(PEGANpcActionTerminalOutcomeEvent terminalEvent) =>
            HandleActionTerminalOutcome(_travelToEscapeAction, terminalEvent);

        private void HandleActionTerminalOutcome(
            PEGANpcNavigationAction sourceAction,
            PEGANpcActionTerminalOutcomeEvent terminalEvent)
        {
            if (terminalEvent == null)
            {
                return;
            }

            PEGANpcActionExecutionSnapshot execution = terminalEvent.Execution;
            if (sourceAction == null || !_hasActivePlan || GetAction(_activePlan.ActionId) != sourceAction)
            {
                _log.Info(
                    "NPC ignored a terminal result from an Action that does not own its active GOAP plan.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("sourceAction", sourceAction != null ? sourceAction.GetType().Name : string.Empty),
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

            bool cancelledWhileInactive = !isActiveAndEnabled &&
                execution.Outcome == PEGANpcActionOutcome.Cancelled;
            if (_isStoppingForLifecycle || cancelledWhileInactive)
            {
                PEGANpcPlanSnapshot pausedPlan = _activePlan;
                _log.Info(
                    "NPC lifecycle pause cancelled its active Action without applying an Effect or Desire satisfaction.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("requestId", execution.RequestId),
                    LogFields.Field("actionOutcome", execution.Outcome),
                    LogFields.Field("cancelledWhileInactive", cancelledWhileInactive),
                    LogFields.Field("desireId", pausedPlan.DesireId),
                    LogFields.Field("actionId", pausedPlan.ActionId),
                    LogFields.Field("diagnostic", execution.Diagnostic));
                ClearActivePlan();
                return;
            }

            PEGANpcPlanSnapshot completedPlan = _activePlan;
            bool approachBelongsToLostContactOpportunity = _hasActivePursuitContinuation &&
                completedPlan.ActionId == PEGANpcPlanningActionId.ApproachKnownRick &&
                completedPlan.TargetId ==
                PEGANpcPlanningTargetId.ForActor(_activePursuitContinuation.SubjectActorId);
            if (approachBelongsToLostContactOpportunity)
            {
                bool reachedLastKnownPosition =
                    PlanarDistance(transform.position, _activePursuitContinuation.LastKnownPosition) <=
                    _approachAction.DeclaredEffect.TargetRadiusMeters;
                if (reachedLastKnownPosition)
                {
                    _activePursuitContinuation = _activePursuitContinuation.WithLastKnownPositionReached();
                }
                else if (completedPlan.Purpose == PEGANpcPlanPurpose.PursuitContinuationStep)
                {
                    _activePursuitContinuation = _activePursuitContinuation.WithLastKnownApproachAttempted();
                }
                _log.Info(
                    "NPC reconciled last-known pursuit progress from the terminal physical Action result.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("requestId", execution.RequestId),
                    LogFields.Field("actionOutcome", execution.Outcome),
                    LogFields.Field("lastKnownApproachAttempted", _activePursuitContinuation.LastKnownApproachAttempted),
                    LogFields.Field("lastKnownPositionReached", _activePursuitContinuation.LastKnownPositionReached));
            }

            if (_isReplacingPlan)
            {
                _log.Info(
                    "NPC Action cancellation released the previous plan for a selected replacement; no Effect or satisfaction was applied.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("requestId", execution.RequestId),
                    LogFields.Field("actionOutcome", execution.Outcome),
                    LogFields.Field("desireId", completedPlan.DesireId),
                    LogFields.Field("actionId", completedPlan.ActionId),
                    LogFields.Field("targetId", completedPlan.TargetId),
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
                        LogFields.Field("targetId", completedPlan.TargetId),
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
                        sourceAction,
                        out PEGANpcObservedActionEffect observedEffect,
                        out string effectDiagnostic))
                {
                    _log.Error(
                        "NPC Action reported Success without an observed Effect matching the active GOAP plan; Desire satisfaction was not changed.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("desireId", completedPlan.DesireId),
                        LogFields.Field("actionId", completedPlan.ActionId),
                        LogFields.Field("targetId", completedPlan.TargetId),
                        LogFields.Field("targetPosition", completedPlan.TargetPosition),
                        LogFields.Field("executionTargetPosition", execution.TargetPosition),
                        LogFields.Field("declaredEffectTargetPosition", execution.DeclaredEffect.TargetPosition),
                        LogFields.Field("observedEffectTargetPosition", observedEffect.TargetPosition),
                        LogFields.Field("latestSelectedTargetPosition", _latestSelection != null && _latestSelection.HasSelectedPlan
                            ? _latestSelection.SelectedPlan.TargetPosition
                            : default),
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
                if (completedPlan.Purpose == PEGANpcPlanPurpose.PursuitContinuationStep ||
                    approachBelongsToLostContactOpportunity)
                {
                    _log.Info(
                        "NPC observed physical progress within its GOAP pursuit continuation without changing Desire satisfaction or Knowledge State.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("desireId", completedPlan.DesireId),
                        LogFields.Field("actionId", completedPlan.ActionId),
                        LogFields.Field("targetId", completedPlan.TargetId),
                        LogFields.Field("targetPosition", completedPlan.TargetPosition),
                        LogFields.Field("positionEvidenceTimeSeconds", completedPlan.EvidenceTimeSeconds),
                        LogFields.Field("requestId", execution.RequestId),
                        LogFields.Field("actionOutcome", execution.Outcome),
                        LogFields.Field("completedAtTimeSeconds", terminalEvent.CompletedAtTimeSeconds),
                        LogFields.Field("effect", observedEffect.Kind),
                        LogFields.Field("goal", observedEffect.GoalKind),
                        LogFields.Field("effectTargetPosition", observedEffect.TargetPosition),
                        LogFields.Field("actualDistanceToTargetMeters", observedEffect.ActualDistanceToTargetMeters),
                        LogFields.Field("continuationProgressObserved", true),
                        LogFields.Field("desireSatisfactionUpdatedCount", 0),
                        LogFields.Field("knowledgeStateUpdated", false),
                        LogFields.Field("maximumRouteDistanceFromAnchorMeters", completedPlan.MaximumRouteDistanceFromAnchorMeters));
                }
                else
                {
                    PEGANpcDesireSatisfactionUpdate satisfaction =
                        _desireState.RecordCompletedAction(observedEffect, _normalContext);
                    _log.Info(
                        "NPC applied a physically observed Action Effect to the matching Desire predicates.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("desireId", completedPlan.DesireId),
                        LogFields.Field("actionId", completedPlan.ActionId),
                        LogFields.Field("targetId", completedPlan.TargetId),
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
            }
            finally
            {
                _hasRecentTerminalPlan = true;
                _recentTerminalPlan = completedPlan;
                _recentTerminalOutcome = execution.Outcome;
                _recentTerminalOpportunity = _activePlanOpportunity;
                _hasRecentTerminalOpportunity = true;
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
            PEGANpcNavigationAction action,
            out PEGANpcObservedActionEffect observedEffect,
            out string diagnostic)
        {
            observedEffect = default;
            if (action == null || GetAction(plan.ActionId) != action ||
                !action.TryGetObservedEffect(out observedEffect))
            {
                diagnostic = "The active GOAP Action has no physically observed Effect.";
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
            _activePlanOpportunity = default;
        }

        private void QueueEvaluation(
            EvaluationCause cause,
            PEGANpcPlanReevaluationRequestedEvent request = null,
            PEGANpcLastKnownRickPursuitIntent? lostContactIntent = null,
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

            if (lostContactIntent.HasValue &&
                (!_hasPendingLostContactPursuitIntent ||
                 lostContactIntent.Value.ContactLossObservationTimeSeconds >
                 _latestLostContactPursuitIntent.ContactLossObservationTimeSeconds + 0.000001d))
            {
                _latestLostContactPursuitIntent = lostContactIntent.Value;
                _hasPendingLostContactPursuitIntent = true;
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
            _latestLostContactPursuitIntent = default;
            _hasPendingLostContactPursuitIntent = false;
            _pendingTerminalRequestId = 0;
        }

        private static bool TryAcceptOpportunity(
            OpportunityStamp opportunity,
            ref OpportunityStamp latestOpportunity,
            ref bool hasLatestOpportunity)
        {
            if (!opportunity.ActorId.IsValid ||
                double.IsNaN(opportunity.ObservationTimeSeconds) ||
                double.IsInfinity(opportunity.ObservationTimeSeconds) ||
                opportunity.ObservationTimeSeconds < 0d)
            {
                return false;
            }

            if (hasLatestOpportunity &&
                latestOpportunity.ActorId == opportunity.ActorId &&
                opportunity.ObservationTimeSeconds <= latestOpportunity.ObservationTimeSeconds + 0.000001d)
            {
                return false;
            }

            latestOpportunity = opportunity;
            hasLatestOpportunity = true;
            return true;
        }

        private bool AreEquivalentPlans(PEGANpcPlanSnapshot left, PEGANpcPlanSnapshot right) =>
            left.ActionId == right.ActionId &&
            left.Purpose == right.Purpose &&
            left.PredictedEffect == right.PredictedEffect &&
            left.PredictedGoal == right.PredictedGoal &&
            left.TargetId == right.TargetId &&
            left.TargetKind == right.TargetKind &&
            left.TargetProvenance == right.TargetProvenance &&
            right.EvidenceTimeSeconds >= left.EvidenceTimeSeconds &&
            IsTargetPositionWithinActionTolerance(left.TargetPosition, right.TargetPosition, left.ActionId) &&
            left.SpeedRegime == right.SpeedRegime &&
            string.Equals(left.DesireId, right.DesireId, StringComparison.Ordinal);

        private bool CanPromoteApproachToContinuation(
            PEGANpcPlanSnapshot current,
            PEGANpcPlanSnapshot selected) =>
            _hasActivePursuitContinuation &&
            current.ActionId == PEGANpcPlanningActionId.ApproachKnownRick &&
            current.Purpose == PEGANpcPlanPurpose.DesireGoal &&
            selected.ActionId == PEGANpcPlanningActionId.ApproachKnownRick &&
            selected.Purpose == PEGANpcPlanPurpose.PursuitContinuationStep &&
            current.PredictedEffect == selected.PredictedEffect &&
            current.PredictedGoal == selected.PredictedGoal &&
            current.TargetId == selected.TargetId &&
            current.TargetKind == selected.TargetKind &&
            current.TargetProvenance == selected.TargetProvenance &&
            selected.EvidenceTimeSeconds >= current.EvidenceTimeSeconds &&
            current.TargetPosition.Equals(selected.TargetPosition) &&
            current.SpeedRegime == selected.SpeedRegime &&
            string.Equals(current.DesireId, selected.DesireId, StringComparison.Ordinal) &&
            string.Equals(selected.DesireId, _activePursuitContinuation.DesireId, StringComparison.Ordinal);

        private bool IsTargetPositionWithinActionTolerance(
            Vector3 left,
            Vector3 right,
            PEGANpcPlanningActionId actionId)
        {
            PEGANpcNavigationAction action = GetAction(actionId);
            float toleranceMeters = action != null ? action.DeclaredEffect.TargetRadiusMeters : 0f;
            return toleranceMeters >= 0f &&
                   !float.IsNaN(toleranceMeters) &&
                   !float.IsInfinity(toleranceMeters) &&
                   PlanarDistance(left, right) <= toleranceMeters;
        }

        private PEGANpcNavigationAction GetAction(PEGANpcPlanningActionId actionId) => actionId switch
        {
            PEGANpcPlanningActionId.ApproachKnownRick => _approachAction,
            PEGANpcPlanningActionId.TravelToEscapeTarget => _travelToEscapeAction,
            PEGANpcPlanningActionId.InvestigateLastKnownRickPosition => _investigateAction,
            _ => null
        };

        private PEGANpcActionOutcome GetActionOutcome(PEGANpcPlanningActionId actionId)
        {
            PEGANpcNavigationAction action = GetAction(actionId);
            return action != null ? action.Outcome : PEGANpcActionOutcome.NotStarted;
        }

        private bool IsDesireApplicable(string desireId)
        {
            if (_normalContext == null || _desireState == null)
            {
                return false;
            }

            PEGANpcDesireStateSnapshot satisfaction = _desireState.GetSnapshot();
            for (int index = 0; index < _normalContext.Desires.Count; index++)
            {
                PEGANpcDesireDefinition definition = _normalContext.Desires[index];
                if (string.Equals(definition.DesireId, desireId, StringComparison.Ordinal))
                {
                    satisfaction.TryGet(desireId, out PEGANpcDesireSatisfactionSnapshot current);
                    return definition.IsApplicable(current);
                }
            }

            return false;
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            Vector3 delta = left - right;
            delta.y = 0f;
            return delta.magnitude;
        }

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

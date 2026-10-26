using System;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.GOAP;
using PEGA.Actors;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// Selects, starts and reconciles this NPC's valid Normal-context GOAP plan from composed Action and input capabilities.
    /// Evaluation is event-driven; physical Action execution remains owned by the selected Action.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Autonomy")]
    [RequireComponent(typeof(PEGANpcKnowledgeState))]
    [RequireComponent(typeof(PEGANpcDesireState))]
    public sealed class PEGANpcAutonomy : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.Autonomy";
        [SerializeField]
        [Tooltip("Profile de Desejos atribuído a este NPC. O profile atual de N01 é provisório para a demonstração na Mission.")]
        private PEGANpcDesireProfile desireProfile;

        [SerializeField, Min(1)]
        [Tooltip("Máximo de avaliações de Action por busca de Goal/Target na coleta causal de alternativas.")]
        private int causalSearchCandidateEvaluationBudget = 128;

        [System.NonSerialized]
        private PEGANpcKnowledgeState _knowledgeState;

        [System.NonSerialized]
        private IPEGAGoapKnowledgeStateView _goapKnowledgeStateView;

        [System.NonSerialized]
        private IPEGANpcTargetObservationSource _targetObservationSource;

        [System.NonSerialized]
        private PEGANpcDesireState _desireState;

        [System.NonSerialized]
        private readonly List<IPEGANpcActionProvider> _actionProviders = new List<IPEGANpcActionProvider>();

        [System.NonSerialized]
        private readonly List<IPEGAGoapKnowledgeCapability> _goapKnowledgeCapabilities =
            new List<IPEGAGoapKnowledgeCapability>();

        [System.NonSerialized]
        private readonly Dictionary<PEGANpcPlanningActionId, IPEGANpcActionProvider> _actionProvidersById =
            new Dictionary<PEGANpcPlanningActionId, IPEGANpcActionProvider>();

        [System.NonSerialized]
        private readonly Dictionary<IPEGANpcActionProvider, IEventBinding> _actionOutcomeBindings =
            new Dictionary<IPEGANpcActionProvider, IEventBinding>();

        [System.NonSerialized]
        private readonly List<IPEGANpcPlanningInputChangeSource> _planningInputChangeSources =
            new List<IPEGANpcPlanningInputChangeSource>();

        [System.NonSerialized]
        private readonly Dictionary<IPEGANpcPlanningInputChangeSource, IEventBinding> _planningInputChangeBindings =
            new Dictionary<IPEGANpcPlanningInputChangeSource, IEventBinding>();

        [System.NonSerialized]
        private IPEGANpcActionProvider _continuationAction;

        [System.NonSerialized]
        private IPEGANpcLastKnownTargetContinuationProvider _continuationProvider;

        [System.NonSerialized]
        private PEGANpcDesireContextDefinition _normalContext;

        [System.NonSerialized]
        private IEventBinding _knowledgeChangeBinding;

        [System.NonSerialized]
        private IEventBinding _planReevaluationBinding;

        [System.NonSerialized]
        private IEventBinding _continuationIntentBinding;

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
        private PEGANpcLastKnownTargetContinuationIntent _latestContinuationIntent;

        [System.NonSerialized]
        private bool _hasPendingContinuationIntent;

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
        private bool _hasLoggedObjective;

        [System.NonSerialized]
        private string _lastLoggedDesireId;

        [System.NonSerialized]
        private PEGANpcPlanningTargetId _lastLoggedTargetId;

        [System.NonSerialized]
        private bool _hasLoggedObjectiveCompletion;

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
        private PEGANpcPlanContinuation _activePlanContinuation;

        [System.NonSerialized]
        private bool _hasActivePlanContinuation;

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
            LastKnownTargetContinuationIntent = 16,
            AutonomyLifecycle = 32,
            PlanningInputsChanged = 64
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
            if (_targetObservationSource != null && _planReevaluationBinding == null)
            {
                _planReevaluationBinding = _targetObservationSource
                    .SubscribeToPlanReevaluationRequests(OnPlanReevaluationRequested);
            }

            if (_continuationIntentBinding == null && _continuationProvider != null)
            {
                _continuationIntentBinding = _continuationProvider
                    .SubscribeToLastKnownTargetContinuationIntents(OnLastKnownTargetContinuationIntentRequested);
            }

            for (int index = 0; index < _planningInputChangeSources.Count; index++)
            {
                IPEGANpcPlanningInputChangeSource source = _planningInputChangeSources[index];
                if (!_planningInputChangeBindings.ContainsKey(source))
                {
                    _planningInputChangeBindings.Add(
                        source,
                        source.SubscribeToPlanningInputChanges(OnPlanningInputsChanged));
                }
            }

            for (int index = 0; index < _actionProviders.Count; index++)
            {
                IPEGANpcActionProvider provider = _actionProviders[index];
                if (!_actionOutcomeBindings.ContainsKey(provider))
                {
                    _actionOutcomeBindings.Add(provider, provider.SubscribeToTerminalOutcomes(
                        terminalEvent => HandleActionTerminalOutcome(provider, terminalEvent)));
                }
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
            // Event buses do PEGA não fazem replay; a fila preserva reavaliações pendentes durante a pausa.
            CancelActivePlanForLifecycle();
        }

        private void OnDestroy()
        {
            if (_knowledgeState != null && _knowledgeChangeBinding != null)
            {
                _knowledgeState.UnsubscribeFromKnowledgeChanges(_knowledgeChangeBinding);
                _knowledgeChangeBinding = null;
            }

            if (_targetObservationSource != null && _planReevaluationBinding != null)
            {
                _targetObservationSource.UnsubscribeFromPlanReevaluationRequests(_planReevaluationBinding);
                _planReevaluationBinding = null;
            }

            if (_continuationProvider != null && _continuationIntentBinding != null)
            {
                _continuationProvider.UnsubscribeFromLastKnownTargetContinuationIntents(_continuationIntentBinding);
                _continuationIntentBinding = null;
            }

            foreach (KeyValuePair<IPEGANpcActionProvider, IEventBinding> binding in _actionOutcomeBindings)
            {
                binding.Key.UnsubscribeFromTerminalOutcomes(binding.Value);
            }
            _actionOutcomeBindings.Clear();

            foreach (KeyValuePair<IPEGANpcPlanningInputChangeSource, IEventBinding> binding in _planningInputChangeBindings)
            {
                binding.Key.UnsubscribeFromPlanningInputChanges(binding.Value);
            }
            _planningInputChangeBindings.Clear();

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
            PEGANpcLastKnownTargetContinuationIntent continuationIntent = _latestContinuationIntent;
            bool hasContinuationIntent = _hasPendingContinuationIntent;
            long terminalRequestId = _pendingTerminalRequestId;
            _bootstrapPending = false;
            ClearQueuedEvaluation();

            string lostIntentDiagnostic = string.Empty;
            bool continuationIntentStillCurrent = !hasContinuationIntent ||
                (_continuationProvider != null &&
                 _continuationProvider.IsCurrentContinuationIntent(
                     continuationIntent,
                     out lostIntentDiagnostic));
            if (hasContinuationIntent && !continuationIntentStillCurrent)
            {
                causes &= ~EvaluationCause.LastKnownTargetContinuationIntent;
                hasContinuationIntent = false;
                _log.Debug(
                    "NPC discarded a superseded last-known target continuation intent before consolidated GOAP evaluation.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", continuationIntent.SubjectActorId.StableText),
                    LogFields.Field("contactLossObservationTimeSeconds", continuationIntent.ContactLossObservationTimeSeconds),
                    LogFields.Field("diagnostic", _continuationProvider == null
                        ? "No local provider owns the plan continuation."
                        : lostIntentDiagnostic));
            }

            if (!_bootstrapPending && causes == EvaluationCause.None && !_planEvaluationQueued)
            {
                return;
            }

            EvaluateAndApplyPlan(
                causes,
                hasRequest ? request : null,
                hasContinuationIntent ? continuationIntent : (PEGANpcLastKnownTargetContinuationIntent?)null,
                terminalRequestId);
        }

        private bool TryInitialize(out string diagnostic)
        {
            diagnostic = string.Empty;
            _knowledgeState = GetComponent<PEGANpcKnowledgeState>();
            _desireState = GetComponent<PEGANpcDesireState>();
            if (!TryDiscoverGoapKnowledgeCapabilities(out diagnostic))
            {
                return false;
            }
            if (!TryDiscoverTargetObservationSource(out diagnostic))
            {
                return false;
            }
            if (!TryDiscoverPlanningInputChangeSources(out diagnostic))
            {
                return false;
            }
            if (!TryDiscoverActionProviders(out diagnostic))
            {
                return false;
            }
            if (_knowledgeState == null || _desireState == null || _actionProviders.Count == 0)
            {
                diagnostic = "Knowledge State, Desire State and at least one local Action provider must exist on this NPC root.";
                return false;
            }

            _goapKnowledgeStateView = new PEGANpcGoapKnowledgeStateView(
                _knowledgeState,
                _goapKnowledgeCapabilities);

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

            if (causalSearchCandidateEvaluationBudget <= 0)
            {
                diagnostic = "The causal search candidate-evaluation budget must be greater than zero.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryDiscoverTargetObservationSource(out string diagnostic)
        {
            _targetObservationSource = null;
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                if (!(components[index] is IPEGANpcTargetObservationSource candidate))
                {
                    continue;
                }

                if (_targetObservationSource != null)
                {
                    diagnostic = "The NPC Actor may compose at most one target observation source.";
                    return false;
                }

                _targetObservationSource = candidate;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryDiscoverPlanningInputChangeSources(out string diagnostic)
        {
            _planningInputChangeSources.Clear();
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                MonoBehaviour component = components[index];
                if (!(component is IPEGANpcPlanningInputChangeSource source))
                {
                    continue;
                }

                if (component.gameObject != gameObject)
                {
                    diagnostic = $"Planning input change source at component index {index} is not on this NPC root.";
                    return false;
                }

                _planningInputChangeSources.Add(source);
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryDiscoverGoapKnowledgeCapabilities(out string diagnostic)
        {
            _goapKnowledgeCapabilities.Clear();
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                MonoBehaviour component = components[index];
                if (!(component is IPEGAGoapKnowledgeCapability capability))
                {
                    continue;
                }

                if (component.gameObject != gameObject)
                {
                    diagnostic = $"GOAP Knowledge capability at component index {index} is not on this NPC root.";
                    return false;
                }

                _goapKnowledgeCapabilities.Add(capability);
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryDiscoverActionProviders(out string diagnostic)
        {
            _actionProviders.Clear();
            _actionProvidersById.Clear();
            _continuationAction = null;
            _continuationProvider = null;
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                MonoBehaviour component = components[index];
                if (!(component is IPEGANpcActionProvider provider))
                {
                    continue;
                }

                PEGANpcActionContract contract = provider.Contract;
                if (component.gameObject != gameObject || !contract.IsValid)
                {
                    diagnostic = $"Local Action provider at component index {index} has an invalid root or Action contract.";
                    return false;
                }

                if (_actionProvidersById.ContainsKey(contract.ActionId))
                {
                    diagnostic = $"More than one local Action provider uses stable Action ID '{contract.ActionId}'.";
                    return false;
                }

                _actionProviders.Add(provider);
                _actionProvidersById.Add(contract.ActionId, provider);
                if (component is IPEGANpcLastKnownTargetContinuationProvider continuationProvider)
                {
                    if (_continuationProvider != null)
                    {
                        diagnostic = "Only one local Action provider may own the current plan-continuation capability.";
                        return false;
                    }

                    _continuationProvider = continuationProvider;
                    _continuationAction = provider;
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        private void OnKnowledgeChanged(PEGANpcKnowledgeFactChangedEvent change)
        {
            if (change == null || !change.HasValueOrStatusChanged)
            {
                return;
            }

            bool isRelevantToAnyProvider = false;
            for (int index = 0; index < _actionProviders.Count; index++)
            {
                if (_actionProviders[index] is IPEGANpcKnowledgeChangeRelevanceProvider relevanceProvider &&
                    relevanceProvider.IsRelevantKnowledgeChange(change))
                {
                    isRelevantToAnyProvider = true;
                    break;
                }
            }

            if (!isRelevantToAnyProvider)
            {
                _log.Debug(
                    "Knowledge State change did not affect any registered Action provider's planning inputs.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", change.SubjectActorId.HasValue
                        ? change.SubjectActorId.Value.StableText
                        : string.Empty),
                    LogFields.Field("factKind", change.FactKind),
                    LogFields.Field("evidenceOrigin", change.EvidenceOrigin));
                return;
            }

            _log.Debug(
                "Knowledge State change queued provider-driven GOAP reevaluation.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("subjectActorId", change.SubjectActorId.HasValue
                    ? change.SubjectActorId.Value.StableText
                    : string.Empty),
                LogFields.Field("factKind", change.FactKind),
                LogFields.Field("observationTimeSeconds", change.ObservationTimeSeconds),
                LogFields.Field("evidenceOrigin", change.EvidenceOrigin),
                LogFields.Field("activeRequestId", _activeActionRequestId));
            QueueEvaluation(EvaluationCause.RelevantKnowledgeChange);
        }

        private void OnPlanningInputsChanged(PEGANpcPlanningInputsChangedEvent change)
        {
            if (change == null)
            {
                return;
            }

            _log.Debug(
                "A composed planning input changed and queued event-driven GOAP reevaluation.",
                LogFields.Field("actorRoot", gameObject.name));
            QueueEvaluation(EvaluationCause.PlanningInputsChanged);
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
            bool acceptedOpportunity = request.Trigger == PEGANpcPlanReevaluationTrigger.TargetReacquired
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
                _log.Debug(
                    "NPC ignored a duplicate or stale Perception reevaluation opportunity.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("trigger", request.Trigger),
                    LogFields.Field("targetActorId", request.TargetActorId.StableText),
                    LogFields.Field("observationTimeSeconds", request.ObservationTimeSeconds));
                return;
            }

            ClosePlanContinuation($"Perception published {request.Trigger}.");

            _log.Debug(
                "NPC received a legitimate plan reevaluation request from Perception.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("trigger", request.Trigger),
                LogFields.Field("targetActorId", request.TargetActorId.StableText),
                LogFields.Field("observationTimeSeconds", request.ObservationTimeSeconds),
                LogFields.Field("requestedAtTimeSeconds", request.RequestedAtTimeSeconds),
                LogFields.Field("activeRequestId", _activeActionRequestId));
            QueueEvaluation(EvaluationCause.PlanReevaluationRequest, request);
        }

        private void OnLastKnownTargetContinuationIntentRequested(
            PEGANpcLastKnownTargetContinuationIntentRequestedEvent intentEvent)
        {
            if (_continuationProvider == null || _continuationAction == null ||
                intentEvent == null || !intentEvent.Intent.IsValid)
            {
                _log.Warning(
                    "NPC ignored a lost-contact pursuit intent that did not match its current legitimate Knowledge State.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", intentEvent != null ? intentEvent.Intent.SubjectActorId.StableText : string.Empty),
                    LogFields.Field("diagnostic", intentEvent == null ? "The intent event was null." : "The intent data was invalid."));
                return;
            }

            if (!_continuationProvider.IsCurrentContinuationIntent(intentEvent.Intent, out string diagnostic))
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
                _log.Debug(
                    "NPC ignored a duplicate or stale last-known pursuit opportunity.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", intentEvent.Intent.SubjectActorId.StableText),
                    LogFields.Field("contactLossObservationTimeSeconds", intentEvent.Intent.ContactLossObservationTimeSeconds));
                return;
            }

            if (!TryGetContinuationDesireOwner(intentEvent.Intent.SubjectActorId, out string desireId))
            {
                _log.Debug(
                    "NPC did not open a pursuit continuation because no selected GOAP plan currently owns Approach Known target.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", intentEvent.Intent.SubjectActorId.StableText),
                    LogFields.Field("contactLossObservationTimeSeconds", intentEvent.Intent.ContactLossObservationTimeSeconds));
                return;
            }

            bool approachAlreadyRunning = _hasActivePlan &&
                _activePlan.ActionId == _continuationAction.Contract.ActionId &&
                _continuationAction.Outcome == PEGANpcActionOutcome.Running;
            bool alreadyAtLastKnown = PlanarDistance(transform.position, intentEvent.Intent.LastKnownPosition) <=
                _continuationAction.DeclaredEffect.TargetRadiusMeters;
            bool lastKnownPositionAlreadyReached = alreadyAtLastKnown;
            _activePlanContinuation = new PEGANpcPlanContinuation(
                desireId,
                intentEvent.Intent.SubjectActorId,
                intentEvent.Intent.ContactLossObservationTimeSeconds,
                intentEvent.Intent.LastKnownPosition,
                intentEvent.Intent.LastKnownObservationTimeSeconds,
                false,
                lastKnownPositionAlreadyReached);
            _hasActivePlanContinuation = _activePlanContinuation.IsValid;
            _attemptedInvestigationTargets.Clear();
            if (!_hasActivePlanContinuation)
            {
                _log.Error(
                    "NPC could not create a valid local pursuit continuation from the accepted Perception loss.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("desireId", desireId),
                    LogFields.Field("subjectActorId", intentEvent.Intent.SubjectActorId.StableText));
                return;
            }

            _log.Debug(
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
                LogFields.Field("lastKnownPositionReached", _activePlanContinuation.LastKnownPositionReached),
                LogFields.Field("continuationInputOwner", "composed target observation capability"),
                LogFields.Field("activeRequestId", _activeActionRequestId));
            _log.Info(
                "NPC began searching target's last known position.",
                LogFields.Field("actorRoot", gameObject.name));
            QueueEvaluation(EvaluationCause.LastKnownTargetContinuationIntent, lostContactIntent: intentEvent.Intent);
        }

        private bool TryGetContinuationDesireOwner(ActorId subjectActorId, out string desireId)
        {
            desireId = string.Empty;
            if (_continuationAction == null || _continuationProvider == null)
            {
                return false;
            }
            if (_hasActivePlan &&
                _activePlan.ActionId == _continuationAction.Contract.ActionId &&
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
            if (selected.ActionId != _continuationAction.Contract.ActionId ||
                selected.TargetId != PEGANpcPlanningTargetId.ForActor(subjectActorId))
            {
                return false;
            }

            desireId = selected.DesireId;
            return !string.IsNullOrWhiteSpace(desireId);
        }

        private void ClosePlanContinuation(string reason)
        {
            if (!_hasActivePlanContinuation)
            {
                return;
            }

            _log.Debug(
                "NPC closed its local pursuit continuation after a legitimate Perception transition.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("desireId", _activePlanContinuation.DesireId),
                LogFields.Field("subjectActorId", _activePlanContinuation.SubjectActorId.StableText),
                LogFields.Field("contactLossObservationTimeSeconds", _activePlanContinuation.ContactLossObservationTimeSeconds),
                LogFields.Field("attemptedLocalTargetCount", _attemptedInvestigationTargets.Count),
                LogFields.Field("reason", reason));
            _hasActivePlanContinuation = false;
            _activePlanContinuation = default;
            _attemptedInvestigationTargets.Clear();
            _latestContinuationIntent = default;
            _hasPendingContinuationIntent = false;
            _pendingEvaluationCauses &= ~EvaluationCause.LastKnownTargetContinuationIntent;
        }

        private bool TryGetCurrentTargetActorId(out ActorId actorId)
        {
            actorId = default;
            if (_targetObservationSource == null ||
                !_targetObservationSource.TryGetCurrentTargetActorId(out actorId, out _))
            {
                return false;
            }

            return actorId.IsValid;
        }

        private bool TryValidatePlanReevaluationRequest(
            PEGANpcPlanReevaluationRequestedEvent request,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!TryGetCurrentTargetActorId(out ActorId actorId) || actorId != request.TargetActorId)
            {
                diagnostic = "The request target is not the current Actor occurrence selected by this NPC's target-observation composition.";
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                _knowledgeState.GetObservedCharacterIdentity(actorId);
            PEGANpcKnowledgeFact<bool> contact = _knowledgeState.GetCharacterVisualContact(actorId);
            PEGANpcKnowledgeFact<Vector3> position = _knowledgeState.GetLastObservedCharacterPosition(actorId);
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
                case PEGANpcPlanReevaluationTrigger.TargetReacquired:
                    if (!hasVisualContact ||
                        !HasObservationAt(identity, request.ObservationTimeSeconds) ||
                        !HasObservationAt(contact, request.ObservationTimeSeconds) ||
                        !HasObservationAt(position, request.ObservationTimeSeconds))
                    {
                        diagnostic = "TargetReacquired requires consolidated current identity, contact and position evidence.";
                        return false;
                    }

                    return true;

                case PEGANpcPlanReevaluationTrigger.TargetContactLostBeyondWindow:
                    if (hasVisualContact ||
                        !HasObservationAt(contact, request.ObservationTimeSeconds) ||
                        position.ObservationTimeSeconds > request.ObservationTimeSeconds + 0.000001d ||
                        identity.ObservationTimeSeconds > request.ObservationTimeSeconds + 0.000001d)
                    {
                        diagnostic = "TargetContactLostBeyondWindow requires the published real contact-loss sample and preserved earlier Perception evidence.";
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
            PEGANpcLastKnownTargetContinuationIntent? lostContactIntent,
            long terminalRequestId)
        {
            if (!TryBuildLiveSelection(
                    out ActorId targetActorId,
                    out IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
                    out PEGANpcPlanSelectionResult selection,
                    out string diagnostic))
            {
                _log.Warning(
                    "NPC GOAP reevaluation had no valid live target/action input.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("causes", causes),
                    LogFields.Field("targetActorId", targetActorId.IsValid ? targetActorId.StableText : string.Empty),
                    LogFields.Field("activeRequestId", _activeActionRequestId),
                    LogFields.Field("reevaluationTrigger", request != null ? request.Trigger : default),
                    LogFields.Field("terminalRequestId", terminalRequestId),
                    LogFields.Field("diagnostic", diagnostic));
                HandleNoSelectedPlan(null, causes, diagnostic);
                return;
            }

            _latestSelection = selection;
            _log.Debug(
                "NPC consolidated event-driven GOAP reevaluation against its current individual and Gang knowledge.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("causes", causes),
                LogFields.Field("targetActorId", targetActorId.IsValid ? targetActorId.StableText : string.Empty),
                LogFields.Field("reevaluationTrigger", request != null ? request.Trigger : default),
                LogFields.Field("reevaluationObservationTimeSeconds", request != null ? request.ObservationTimeSeconds : -1d),
                LogFields.Field("terminalRequestId", terminalRequestId),
                LogFields.Field("selectionValid", selection.IsValid),
                LogFields.Field("actionCandidateCount", candidates.Count),
                LogFields.Field("candidatePlanCount", selection.CandidatePlans.Count),
                LogFields.Field("hasSelectedPlan", selection.HasSelectedPlan),
                LogFields.Field("pursuitContinuationActive", _hasActivePlanContinuation),
                LogFields.Field("lastKnownPositionReached", _hasActivePlanContinuation &&
                    _activePlanContinuation.LastKnownPositionReached),
                LogFields.Field("distanceToLastKnownMeters", _hasActivePlanContinuation
                    ? PlanarDistance(transform.position, _activePlanContinuation.LastKnownPosition)
                    : -1f),
                LogFields.Field("pursuitActionRunning", _continuationAction != null &&
                    _continuationAction.Outcome == PEGANpcActionOutcome.Running),
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

            IPEGANpcActionProvider selectedProvider = GetAction(selectedPlan.ActionId);
            string targetDiagnostic = string.Empty;
            if (selectedProvider == null || !selectedProvider.IsCurrentPlanValid(
                    selectedPlan,
                    _hasActivePlanContinuation
                        ? _activePlanContinuation
                        : (PEGANpcPlanContinuation?)null,
                    out _,
                    out _,
                    out targetDiagnostic))
            {
                HandleNoSelectedPlan(selection, causes, string.IsNullOrWhiteSpace(targetDiagnostic)
                    ? "The selected GOAP plan has no current Action provider or its target is no longer valid."
                    : targetDiagnostic);
                return;
            }

            OpportunityStamp selectedOpportunity = CreateOpportunityStamp(
                targetActorId,
                selectedPlan,
                request,
                lostContactIntent,
                causes);
            if (_hasActivePlan)
            {
                bool activeActionIsRunning =
                    GetActionOutcome(_activePlan.ActionId) == PEGANpcActionOutcome.Running;
                if (activeActionIsRunning &&
                    AreEquivalentPlans(_activePlan, selectedPlan))
                {
                    // O plano em execução continua vinculado ao destino físico do request original.
                    // Uma evidência equivalente pode atualizar a oportunidade, mas não o Effect declarado pela Action.
                    _activePlanOpportunity = selectedOpportunity;
                    _log.Debug(
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
                        LogFields.Field("predictedEffect", selectedPlan.PredictedEffect));
                    return;
                }

                if (activeActionIsRunning && AreSamePlanDecision(_activePlan, selectedPlan))
                {
                    IPEGANpcActionProvider activeProvider = GetAction(_activePlan.ActionId);
                    long requestIdBeforeUpdate = _activeActionRequestId;
                    string updateDiagnostic = "The Action provider does not support updating a running execution target.";
                    if (activeProvider is IPEGANpcExecutionTargetUpdater targetUpdater &&
                        activeProvider.CurrentExecution.RequestId == requestIdBeforeUpdate &&
                        targetUpdater.TryUpdateExecutionTarget(
                            _activePlan,
                            selectedPlan,
                            out updateDiagnostic) &&
                        activeProvider.CurrentExecution.RequestId == requestIdBeforeUpdate)
                    {
                        Vector3 previousTargetPosition = _activePlan.TargetPosition;
                        _activePlan = selectedPlan;
                        _activePlanOpportunity = selectedOpportunity;
                        _log.Debug(
                            "NPC updated the target of the same running GOAP decision without replacing its execution request.",
                            LogFields.Field("actorRoot", gameObject.name),
                            LogFields.Field("causes", causes),
                            LogFields.Field("requestId", requestIdBeforeUpdate),
                            LogFields.Field("desireId", selectedPlan.DesireId),
                            LogFields.Field("actionId", selectedPlan.ActionId),
                            LogFields.Field("targetId", selectedPlan.TargetId),
                            LogFields.Field("previousTargetPosition", previousTargetPosition),
                            LogFields.Field("updatedTargetPosition", selectedPlan.TargetPosition),
                            LogFields.Field("evidenceTimeSeconds", selectedPlan.EvidenceTimeSeconds));
                        return;
                    }

                    string targetUpdateDiagnostic = activeProvider is IPEGANpcExecutionTargetUpdater
                        ? (activeProvider.CurrentExecution.RequestId != requestIdBeforeUpdate
                            ? "The active execution request changed while its target update was being applied."
                            : updateDiagnostic)
                        : "The Action provider does not support updating a running execution target.";
                    _log.Debug(
                        "NPC will replace the active request because its provider could not apply the selected target update.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("requestId", requestIdBeforeUpdate),
                        LogFields.Field("desireId", selectedPlan.DesireId),
                        LogFields.Field("actionId", selectedPlan.ActionId),
                        LogFields.Field("targetId", selectedPlan.TargetId),
                        LogFields.Field("diagnostic", targetUpdateDiagnostic));
                }

                if (GetActionOutcome(_activePlan.ActionId) == PEGANpcActionOutcome.Running &&
                    CanPromoteApproachToContinuation(_activePlan, selectedPlan))
                {
                    _activePlan = selectedPlan;
                    _activePlanOpportunity = selectedOpportunity;
                    _log.Debug(
                        "NPC retained its valid Approach Known target navigation while the GOAP plan entered its lost-contact continuation.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("requestId", _activeActionRequestId),
                        LogFields.Field("desireId", selectedPlan.DesireId),
                        LogFields.Field("targetPosition", selectedPlan.TargetPosition),
                        LogFields.Field("contactLossObservationTimeSeconds", _activePlanContinuation.ContactLossObservationTimeSeconds));
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
                _log.Debug(
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
                    LogFields.Field("predictedEffect", selectedPlan.PredictedEffect));
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
            PEGANpcLastKnownTargetContinuationIntent? lostContactIntent,
            EvaluationCause causes)
        {
            if (_hasActivePlanContinuation)
            {
                return new OpportunityStamp(
                    1,
                    _activePlanContinuation.SubjectActorId,
                    _activePlanContinuation.ContactLossObservationTimeSeconds);
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
                int kind = request.Trigger == PEGANpcPlanReevaluationTrigger.TargetContactLostBeyondWindow ? 1 : 2;
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
            IPEGANpcActionProvider action = GetAction(plan.ActionId);
            return action != null && action.IsEffectAlreadySatisfiedAtPosition(plan, transform.position);
        }

        private bool TryBuildLiveSelection(
            out ActorId targetActorId,
            out IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
            out PEGANpcPlanSelectionResult selection,
            out string diagnostic)
        {
            targetActorId = default;
            var targets = new List<PEGANpcPlanningTarget>();
            var actionCandidates = new List<PEGANpcPlanningActionCandidate>();
            candidates = Array.AsReadOnly(Array.Empty<PEGANpcPlanningActionCandidate>());
            selection = null;
            diagnostic = string.Empty;
            if (_hasActivePlanContinuation)
            {
                if (_continuationProvider == null || _continuationAction == null ||
                    !TryGetCurrentTargetActorId(out targetActorId))
                {
                    diagnostic = "The active plan continuation has no current observed target occurrence.";
                    return false;
                }

                PEGANpcKnowledgeFact<ActorProfileId> continuationIdentity =
                    _knowledgeState.GetObservedCharacterIdentity(targetActorId);
                PEGANpcKnowledgeFact<Vector3> continuationPosition =
                    _knowledgeState.GetLastObservedCharacterPosition(targetActorId);
                if (!continuationIdentity.TryGetValue(out _) ||
                    (continuationIdentity.Origins & PEGANpcEvidenceOrigin.Perception) == 0 ||
                    !continuationPosition.TryGetValue(out Vector3 continuationKnownPosition) ||
                    !continuationPosition.HasObservationTime ||
                    (continuationPosition.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
                {
                    diagnostic = "Plan continuation requires this NPC's Perception-backed target identity and last known position.";
                    return false;
                }

                if (_activePlanContinuation.SubjectActorId != targetActorId ||
                    !_continuationProvider.IsCurrentContinuationIntent(
                        _activePlanContinuation.ToIntent(),
                        out diagnostic))
                {
                    diagnostic = string.IsNullOrEmpty(diagnostic)
                        ? "The active plan continuation no longer matches this NPC's current target evidence."
                        : diagnostic;
                    return false;
                }

                if (!_activePlanContinuation.LastKnownPositionReached &&
                    PlanarDistance(transform.position, _activePlanContinuation.LastKnownPosition) <=
                    _continuationAction.DeclaredEffect.TargetRadiusMeters)
                {
                    _activePlanContinuation = _activePlanContinuation.WithLastKnownPositionReached();
                }
            }

            if (!TryGetCurrentTargetActorId(out targetActorId))
            {
                targetActorId = default;
            }

            PEGANpcPlanContinuation? planContinuation = _hasActivePlanContinuation
                ? _activePlanContinuation
                : (PEGANpcPlanContinuation?)null;
            bool hasPerceptionBackedTargetContact = false;
            if (targetActorId.IsValid)
            {
                PEGANpcKnowledgeFact<bool> targetContact =
                    _knowledgeState.GetCharacterVisualContact(targetActorId);
                hasPerceptionBackedTargetContact = targetContact.TryGetValue(out bool hasContact) &&
                    hasContact &&
                    (targetContact.Origins & PEGANpcEvidenceOrigin.Perception) != 0;
            }

            PEGANpcPlanningTargetId[] attemptedInvestigationTargets =
                new PEGANpcPlanningTargetId[_attemptedInvestigationTargets.Count];
            _attemptedInvestigationTargets.CopyTo(attemptedInvestigationTargets);
            Array.Sort(attemptedInvestigationTargets);
            var actionContext = new PEGANpcActionPlanningContext(
                _knowledgeState,
                targetActorId,
                hasPerceptionBackedTargetContact,
                planContinuation,
                Array.AsReadOnly(attemptedInvestigationTargets));
            for (int providerIndex = 0; providerIndex < _actionProviders.Count; providerIndex++)
            {
                IPEGANpcActionProvider provider = _actionProviders[providerIndex];
                if (!provider.TryCreatePlanningCandidates(
                        actionContext,
                        out IReadOnlyList<PEGANpcPlanningTarget> providerTargets,
                        out IReadOnlyList<PEGANpcPlanningActionCandidate> providerCandidates,
                        out string providerDiagnostic))
                {
                    if (!string.IsNullOrWhiteSpace(providerDiagnostic))
                    {
                        _log.Debug(
                            "NPC Action provider did not offer candidates for the current planning inputs.",
                            LogFields.Field("actorRoot", gameObject.name),
                            LogFields.Field("actionId", provider.Contract.ActionId),
                            LogFields.Field("diagnostic", providerDiagnostic));
                    }

                    continue;
                }

                if (providerTargets == null || providerCandidates == null)
                {
                    diagnostic = $"Action provider '{provider.Contract.ActionId}' returned null candidate collections.";
                    return false;
                }

                for (int targetIndex = 0; targetIndex < providerTargets.Count; targetIndex++)
                {
                    if (!TryAddTargetUnique(targets, providerTargets[targetIndex]))
                    {
                        diagnostic = $"Action provider '{provider.Contract.ActionId}' returned conflicting data for Target '{providerTargets[targetIndex].TargetId}'.";
                        return false;
                    }
                }

                for (int candidateIndex = 0; candidateIndex < providerCandidates.Count; candidateIndex++)
                {
                    PEGANpcPlanningActionCandidate candidate = providerCandidates[candidateIndex];
                    if (candidate.Action != provider || !provider.CanOfferCandidate(
                            candidate,
                            _hasActivePlanContinuation
                                ? _activePlanContinuation
                                : (PEGANpcPlanContinuation?)null))
                    {
                        _log.Warning(
                            "NPC ignored a candidate that was not offered by its registered Action provider contract.",
                            LogFields.Field("actorRoot", gameObject.name),
                            LogFields.Field("actionId", provider.Contract.ActionId),
                            LogFields.Field("candidateActionId", candidate.ActionId));
                        continue;
                    }

                    actionCandidates.Add(candidate);
                }
            }

            candidates = Array.AsReadOnly(actionCandidates.ToArray());
            PEGANpcPlanningInput input = new PEGANpcPlanningInput(
                PEGANpcPlanningValidationMode.LiveKnowledge,
                PEGANpcDesireContext.Normal,
                targets,
                candidates,
                _hasActivePlanContinuation
                    ? _activePlanContinuation
                    : (PEGANpcPlanContinuation?)null,
                _goapKnowledgeStateView,
                causalSearchCandidateEvaluationBudget);
            selection = PEGANpcPlanSelector.Select(
                input,
                _normalContext,
                _desireState.GetSnapshot());
            diagnostic = selection.Diagnostic;
            return selection.IsValid;
        }

        private static bool TryAddTargetUnique(
            List<PEGANpcPlanningTarget> targets,
            PEGANpcPlanningTarget candidate)
        {
            for (int index = 0; index < targets.Count; index++)
            {
                if (targets[index].TargetId == candidate.TargetId)
                {
                    PEGANpcPlanningTarget existing = targets[index];
                    return existing.Kind == candidate.Kind &&
                           existing.Provenance == candidate.Provenance &&
                           existing.KnownPosition.Equals(candidate.KnownPosition) &&
                           existing.HasEvidenceTime == candidate.HasEvidenceTime &&
                           (!existing.HasEvidenceTime || Math.Abs(existing.EvidenceTimeSeconds - candidate.EvidenceTimeSeconds) <= 0.000001d) &&
                           Math.Abs(existing.MaximumRouteDistanceFromAnchorMeters - candidate.MaximumRouteDistanceFromAnchorMeters) <= 0.000001f;
                }
            }

            targets.Add(candidate);
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
                IPEGANpcActionProvider preservedAction = GetAction(_activePlan.ActionId);
                _log.Debug(
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

            _log.Debug(
                "NPC reevaluated the current Desire set and found no viable GOAP plan.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("causes", causes),
                LogFields.Field("candidatePlanCount", selection != null ? selection.CandidatePlans.Count : 0),
                LogFields.Field("lastKnownPositionReached", _hasActivePlanContinuation &&
                    _activePlanContinuation.LastKnownPositionReached),
                LogFields.Field("diagnostic", diagnostic));
            if (_hasLoggedObjective)
            {
                _log.Info(
                    "NPC stopped its current behavior.",
                    LogFields.Field("actorRoot", gameObject.name));
            }

            ClearLoggedObjective();
        }

        private bool CanPreserveActiveExecution(
            out Vector3 knownPosition,
            out double positionEvidenceTimeSeconds)
        {
            knownPosition = default;
            positionEvidenceTimeSeconds = 0d;
            IPEGANpcActionProvider activeAction = _hasActivePlan ? GetAction(_activePlan.ActionId) : null;
            PEGANpcActionExecutionSnapshot execution = activeAction != null
                ? activeAction.CurrentExecution
                : default;
            PEGANpcActionEffectDeclaration activeEffect = execution.DeclaredEffect;
            if (!_hasActivePlan ||
                activeAction == null ||
                execution.Outcome != PEGANpcActionOutcome.Running ||
                execution.RequestId != _activeActionRequestId ||
                activeEffect.Kind != _activePlan.PredictedEffect ||
                (activeEffect.IsTargetResolved &&
                 PlanarDistance(activeEffect.TargetPosition, _activePlan.TargetPosition) > 0.001f))
            {
                return false;
            }

            bool isContinuationStep = _activePlan.Purpose == PEGANpcPlanPurpose.PlanContinuationStep;
            if (_hasActivePlanContinuation != isContinuationStep)
            {
                return false;
            }

            if (_hasActivePlanContinuation &&
                (_activePlanContinuation.SubjectActorId != _activePlan.TargetId.InvestigationSubjectActorId &&
                 _activePlan.TargetId.IdentityKind == PEGANpcPlanningTargetIdentityKind.InvestigationSample))
            {
                return false;
            }

            if (_hasActivePlanContinuation &&
                !_continuationProvider.IsCurrentContinuationIntent(
                    _activePlanContinuation.ToIntent(),
                    out _))
            {
                return false;
            }

            PEGANpcPlanContinuation? continuation = _hasActivePlanContinuation
                ? _activePlanContinuation
                : (PEGANpcPlanContinuation?)null;
            if (!activeAction.IsCurrentPlanValid(
                    _activePlan,
                    continuation,
                    out knownPosition,
                    out positionEvidenceTimeSeconds,
                    out _))
            {
                return false;
            }

            return isContinuationStep || IsDesireApplicable(_activePlan.DesireId);
        }

        private bool CancelActivePlanForReplacement(string diagnostic)
        {
            if (!_hasActivePlan)
            {
                return true;
            }

            IPEGANpcActionProvider action = GetAction(_activePlan.ActionId);
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

            _log.Debug(
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
            IPEGANpcActionProvider action = GetAction(pausedPlan.ActionId);
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

            _log.Debug(
                "NPC autonomy released its active Action for a later lifecycle evaluation.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("requestId", pausedRequestId),
                LogFields.Field("actionId", pausedPlan.ActionId),
                LogFields.Field("actionOutcome", action.Outcome),
                LogFields.Field("cancelled", cancelled),
                LogFields.Field("pursuitContinuationPreserved", _hasActivePlanContinuation));
            ClearLoggedObjective();
        }

        private void StartSelectedPlan(
            PEGANpcPlanSelectionResult selection,
            PEGANpcPlanningActionCandidate candidate,
            EvaluationCause causes,
            OpportunityStamp opportunity)
        {
            PEGANpcPlanSnapshot plan = selection.SelectedPlan;
            _initialSelectionConsumed = true;
            IPEGANpcActionProvider action = candidate.Action ?? GetAction(plan.ActionId);
            bool recordAttemptedTarget = action is IPEGANpcAttemptedTargetProvider attemptedTargetProvider &&
                attemptedTargetProvider.ShouldRecordAttemptedTarget(candidate);
            if (recordAttemptedTarget)
            {
                _attemptedInvestigationTargets.Add(plan.TargetId);
            }

            _log.Debug(
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
                LogFields.Field("routeLengthMeters", plan.RouteLengthMeters),
                LogFields.Field("estimatedSeconds", plan.EstimatedSeconds),
                LogFields.Field("effectiveDesireWeight", plan.EffectiveDesireWeight),
                LogFields.Field("costPreference", plan.CostPreference),
                LogFields.Field("score", plan.Score),
                LogFields.Field("opportunityKind", opportunity.Kind),
                LogFields.Field("opportunityTimeSeconds", opportunity.ObservationTimeSeconds));

            bool objectiveChanged = plan.Purpose != PEGANpcPlanPurpose.PlanContinuationStep &&
                (!_hasLoggedObjective ||
                 !string.Equals(_lastLoggedDesireId, plan.DesireId, StringComparison.Ordinal) ||
                 !_lastLoggedTargetId.Equals(plan.TargetId));
            _activePlan = plan;
            _activePlanOpportunity = opportunity;
            _activeActionRequestId = 0;
            _hasActivePlan = true;
            PEGANpcPlanContinuation? continuation = _hasActivePlanContinuation
                ? _activePlanContinuation
                : (PEGANpcPlanContinuation?)null;
            string startDiagnostic = string.Empty;
            bool started = action != null && action.TryStart(plan, continuation, out startDiagnostic);
            if (action == null)
            {
                startDiagnostic = "The selected Action provider is no longer attached to this Actor.";
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

            if (objectiveChanged && _hasActivePlan)
            {
                _log.Info(
                    action.Contract.StartMessage,
                    LogFields.Field("actorRoot", gameObject.name));
                _hasLoggedObjective = true;
                _lastLoggedDesireId = plan.DesireId;
                _lastLoggedTargetId = plan.TargetId;
                _hasLoggedObjectiveCompletion = false;
            }

            _log.Debug(
                "NPC autonomously started the selected Action through its provider contract.",
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

        private void HandleActionTerminalOutcome(
            IPEGANpcActionProvider sourceAction,
            PEGANpcActionTerminalOutcomeEvent terminalEvent)
        {
            if (terminalEvent == null)
            {
                return;
            }

            PEGANpcActionExecutionSnapshot execution = terminalEvent.Execution;
            if (sourceAction == null || !_hasActivePlan || GetAction(_activePlan.ActionId) != sourceAction)
            {
                _log.Debug(
                    "NPC ignored a terminal result from an Action that does not own its active GOAP plan.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("sourceAction", sourceAction != null ? sourceAction.Contract.ActionId : default),
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
                _log.Debug(
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
            bool approachBelongsToLostContactOpportunity = _hasActivePlanContinuation &&
                completedPlan.ActionId == _continuationAction.Contract.ActionId &&
                completedPlan.TargetId ==
                PEGANpcPlanningTargetId.ForActor(_activePlanContinuation.SubjectActorId);
            if (approachBelongsToLostContactOpportunity)
            {
                bool reachedLastKnownPosition =
                    PlanarDistance(transform.position, _activePlanContinuation.LastKnownPosition) <=
                    _continuationAction.DeclaredEffect.TargetRadiusMeters;
                if (reachedLastKnownPosition)
                {
                    _activePlanContinuation = _activePlanContinuation.WithLastKnownPositionReached();
                }
                else if (completedPlan.Purpose == PEGANpcPlanPurpose.PlanContinuationStep)
                {
                    _activePlanContinuation = _activePlanContinuation.WithLastKnownApproachAttempted();
                }
                _log.Debug(
                    "NPC reconciled last-known pursuit progress from the terminal physical Action result.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("requestId", execution.RequestId),
                    LogFields.Field("actionOutcome", execution.Outcome),
                    LogFields.Field("lastKnownApproachAttempted", _activePlanContinuation.LastKnownApproachAttempted),
                    LogFields.Field("lastKnownPositionReached", _activePlanContinuation.LastKnownPositionReached));
            }

            if (_isReplacingPlan)
            {
                _log.Debug(
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
                    _log.Debug(
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
                        LogFields.Field("declaredEffectTargetId", execution.DeclaredEffect.TargetId),
                        LogFields.Field("declaredEffectTargetPosition", execution.DeclaredEffect.TargetPosition),
                        LogFields.Field("observedEffectTargetId", observedEffect.TargetId),
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
                IReadOnlyList<IPEGAGoapEffect> observedCausalEffects = Array.Empty<IPEGAGoapEffect>();
                string causalEffectDiagnostic = "The selected Action has no causal observed-effect provider.";
                bool hasObservedCausalEffects = sourceAction != null &&
                    sourceAction.TryGetObservedCausalEffects(
                        completedPlan,
                        observedEffect,
                        out observedCausalEffects,
                        out causalEffectDiagnostic) &&
                    observedCausalEffects != null &&
                    AreDeclaredCausalEffects(completedPlan, observedCausalEffects);
                if (!hasObservedCausalEffects)
                {
                    _log.Error(
                        "NPC Action reported a valid physical outcome but did not provide confirmed causal Effects; Desire satisfaction was not changed.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("desireId", completedPlan.DesireId),
                        LogFields.Field("actionId", completedPlan.ActionId),
                        LogFields.Field("requestId", execution.RequestId),
                        LogFields.Field("effect", observedEffect.Kind),
                        LogFields.Field("causalEffectDiagnostic", causalEffectDiagnostic));
                }
                else
                {
                    PEGAGoapObservedStateView observedState = new PEGAGoapObservedStateView(
                        _goapKnowledgeStateView,
                        observedCausalEffects);
                    PEGANpcDesireSatisfactionUpdate satisfaction =
                        _desireState.RecordCompletedAction(
                            observedEffect,
                            observedState,
                            _normalContext);
                    _log.Debug(
                        "NPC evaluated the confirmed Action Effects against every active Goal condition.",
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
                        LogFields.Field("effectTargetId", observedEffect.TargetId),
                        LogFields.Field("effectTargetKind", observedEffect.TargetKind),
                        LogFields.Field("effectTargetPosition", observedEffect.TargetPosition),
                        LogFields.Field("actualDistanceToTargetMeters", observedEffect.ActualDistanceToTargetMeters),
                        LogFields.Field("observedCausalEffectCount", observedCausalEffects.Count),
                        LogFields.Field("satisfactionUpdatedCount", satisfaction.UpdatedCount),
                        LogFields.Field("satisfiedDesireIds", JoinDesireIds(satisfaction)),
                        LogFields.Field("satisfactionLedger", DescribeSatisfaction(satisfaction)));
                    if (HasUpdatedDesire(satisfaction, completedPlan.DesireId) &&
                        !_hasLoggedObjectiveCompletion &&
                        IsCurrentLoggedObjective(completedPlan))
                    {
                        _log.Info(
                            "NPC achieved its current gameplay goal.",
                            LogFields.Field("actorRoot", gameObject.name));
                        _hasLoggedObjectiveCompletion = true;
                    }
                }

                if (completedPlan.Purpose == PEGANpcPlanPurpose.PlanContinuationStep ||
                    approachBelongsToLostContactOpportunity)
                {
                    _log.Debug(
                        "NPC reconciled intermediate pursuit progress from the observed Action outcome; satisfaction was determined independently by each Goal condition.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("requestId", execution.RequestId),
                        LogFields.Field("actionOutcome", execution.Outcome),
                        LogFields.Field("targetId", completedPlan.TargetId),
                        LogFields.Field("continuationProgressObserved", true),
                        LogFields.Field("maximumRouteDistanceFromAnchorMeters", completedPlan.MaximumRouteDistanceFromAnchorMeters));
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
                    _log.Debug(
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
                    _log.Debug(
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
            IPEGANpcActionProvider action,
            out PEGANpcObservedActionEffect observedEffect,
            out string diagnostic)
        {
            observedEffect = default;
            if (action == null || GetAction(plan.ActionId) != action ||
                !action.TryGetObservedEffect(out observedEffect))
            {
                diagnostic = "The active GOAP Action provider has no observed Effect.";
                return false;
            }

            if (!observedEffect.IsValid ||
                !execution.EffectObserved ||
                observedEffect.RequestId <= 0 ||
                observedEffect.RequestId != execution.RequestId ||
                execution.DeclaredEffect.Kind != plan.PredictedEffect ||
                observedEffect.Kind != plan.PredictedEffect ||
                !execution.DeclaredEffect.IsTargetResolved ||
                observedEffect.TargetId != execution.DeclaredEffect.TargetId ||
                observedEffect.TargetKind != execution.DeclaredEffect.TargetKind ||
                (observedEffect.TargetPosition - execution.DeclaredEffect.TargetPosition).sqrMagnitude > 0.000001f)
            {
                diagnostic = "The observed Action Effect does not match the active request, predicted Effect or Action-declared Target.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool AreDeclaredCausalEffects(
            PEGANpcPlanSnapshot plan,
            IReadOnlyList<IPEGAGoapEffect> observedEffects)
        {
            if (observedEffects.Count == 0)
            {
                return false;
            }

            for (int observedIndex = 0; observedIndex < observedEffects.Count; observedIndex++)
            {
                IPEGAGoapEffect observedEffect = observedEffects[observedIndex];
                if (observedEffect == null)
                {
                    return false;
                }

                bool wasDeclared = false;
                for (int declaredIndex = 0; declaredIndex < plan.CausalEffects.Count; declaredIndex++)
                {
                    if (object.ReferenceEquals(observedEffect, plan.CausalEffects[declaredIndex]))
                    {
                        wasDeclared = true;
                        break;
                    }
                }

                if (!wasDeclared)
                {
                    return false;
                }
            }

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
            PEGANpcLastKnownTargetContinuationIntent? lostContactIntent = null,
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
                (!_hasPendingContinuationIntent ||
                 lostContactIntent.Value.ContactLossObservationTimeSeconds >
                 _latestContinuationIntent.ContactLossObservationTimeSeconds + 0.000001d))
            {
                _latestContinuationIntent = lostContactIntent.Value;
                _hasPendingContinuationIntent = true;
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
            _latestContinuationIntent = default;
            _hasPendingContinuationIntent = false;
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
            AreSamePlanDecision(left, right) &&
            IsTargetPositionWithinActionTolerance(left.TargetPosition, right.TargetPosition, left.ActionId);

        private static bool AreSamePlanDecision(PEGANpcPlanSnapshot left, PEGANpcPlanSnapshot right) =>
            left.ActionId == right.ActionId &&
            left.Purpose == right.Purpose &&
            left.PredictedEffect == right.PredictedEffect &&
            left.TargetId == right.TargetId &&
            left.TargetKind == right.TargetKind &&
            left.TargetProvenance == right.TargetProvenance &&
            right.EvidenceTimeSeconds >= left.EvidenceTimeSeconds &&
            Mathf.Abs(left.MaximumRouteDistanceFromAnchorMeters - right.MaximumRouteDistanceFromAnchorMeters) <= 0.0001f &&
            left.SpeedRegime == right.SpeedRegime &&
            string.Equals(left.DesireId, right.DesireId, StringComparison.Ordinal);

        private bool CanPromoteApproachToContinuation(
            PEGANpcPlanSnapshot current,
            PEGANpcPlanSnapshot selected) =>
            _hasActivePlanContinuation &&
            current.ActionId == _continuationAction.Contract.ActionId &&
            current.Purpose == PEGANpcPlanPurpose.DesireGoal &&
            selected.ActionId == _continuationAction.Contract.ActionId &&
            selected.Purpose == PEGANpcPlanPurpose.PlanContinuationStep &&
            current.PredictedEffect == selected.PredictedEffect &&
            current.TargetId == selected.TargetId &&
            current.TargetKind == selected.TargetKind &&
            current.TargetProvenance == selected.TargetProvenance &&
            selected.EvidenceTimeSeconds >= current.EvidenceTimeSeconds &&
            current.TargetPosition.Equals(selected.TargetPosition) &&
            current.SpeedRegime == selected.SpeedRegime &&
            string.Equals(current.DesireId, selected.DesireId, StringComparison.Ordinal) &&
            string.Equals(selected.DesireId, _activePlanContinuation.DesireId, StringComparison.Ordinal);

        private bool IsTargetPositionWithinActionTolerance(Vector3 left, Vector3 right, PEGANpcPlanningActionId actionId)
        {
            IPEGANpcActionProvider action = GetAction(actionId);
            return action != null && (!action.Contract.RequiresNavigation ||
                                      action.IsTargetPositionWithinTolerance(left, right));
        }

        private IPEGANpcActionProvider GetAction(PEGANpcPlanningActionId actionId) =>
            _actionProvidersById.TryGetValue(actionId, out IPEGANpcActionProvider action) ? action : null;

        private PEGANpcActionOutcome GetActionOutcome(PEGANpcPlanningActionId actionId)
        {
            IPEGANpcActionProvider action = GetAction(actionId);
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
                PEGANpcDesireConfiguration configuration = _normalContext.Desires[index];
                PEGANpcDesireDefinition definition = configuration != null
                    ? configuration.Definition
                    : null;
                if (definition != null &&
                    string.Equals(definition.DesireId, desireId, StringComparison.Ordinal))
                {
                    satisfaction.TryGet(desireId, out PEGANpcDesireSatisfactionSnapshot current);
                    return configuration.IsApplicable(current);
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

        private static bool HasUpdatedDesire(
            PEGANpcDesireSatisfactionUpdate satisfaction,
            string desireId)
        {
            if (satisfaction == null || string.IsNullOrWhiteSpace(desireId))
            {
                return false;
            }

            for (int index = 0; index < satisfaction.UpdatedCount; index++)
            {
                if (string.Equals(
                        satisfaction.UpdatedValues[index].DesireId,
                        desireId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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

        private void ClearLoggedObjective()
        {
            _hasLoggedObjective = false;
            _lastLoggedDesireId = string.Empty;
            _lastLoggedTargetId = default;
            _hasLoggedObjectiveCompletion = false;
        }

        private bool IsCurrentLoggedObjective(PEGANpcPlanSnapshot plan) =>
            _hasLoggedObjective &&
            string.Equals(_lastLoggedDesireId, plan.DesireId, StringComparison.Ordinal) &&
            _lastLoggedTargetId.Equals(plan.TargetId);

        private void EnsureLoggerInitialized()
        {
            _log ??= PEGALogging.CreateScopedLogger<PEGANpcAutonomy>(LogCategory, gameObject.name);
        }
    }
}

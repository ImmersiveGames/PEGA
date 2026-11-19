using System;
using System.Collections;
using Immersive.Foundation.Events;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.Actors;
using Immersive.Framework.Authoring;
using Immersive.Framework.GameFlow;
using Immersive.Framework.PlayerParticipation;
using Immersive.Framework.RouteLifecycle;
using Immersive.Framework.RuntimeContent;
using Immersive.Logging.Loggers;
using PEGA.Diagnostics;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Global;
using UnityEngine;

namespace PEGA.GameFlow
{
    public enum PegaMainMission
    {
        FindRelic,
        EscapeWithGroupRelic
    }

    public readonly struct PegaMissionSnapshot
    {
        public ActivityAsset Activity { get; }
        public PegaMainMission? Mission { get; }
        public bool IsReady { get; }
        public int ReadinessOccurrence { get; }
        public double PreparationStartedAt { get; }
        public double PreparationDeadline { get; }
        public double PreparationRemainingSeconds { get; }
        public double AssaultStartedAt { get; }
        public double AssaultDeadline { get; }
        public double RemainingSeconds { get; }
        public PegaRelicDiscovery? Discovery { get; }

        internal PegaMissionSnapshot(
            ActivityAsset activity,
            PegaMainMission? mission,
            ActivityReadinessSnapshot readiness,
            double preparationStart,
            double preparationDeadline,
            double assaultStart,
            double assaultDeadline,
            PegaRelicDiscovery? discovery)
        {
            Activity = activity;
            Mission = mission;
            IsReady = readiness.IsReady;
            ReadinessOccurrence = readiness.Occurrence;
            PreparationStartedAt = preparationStart;
            PreparationDeadline = preparationDeadline;
            PreparationRemainingSeconds = double.IsNaN(preparationDeadline)
                ? double.NaN
                : Math.Max(0d, preparationDeadline - Time.timeAsDouble);
            AssaultStartedAt = assaultStart;
            AssaultDeadline = assaultDeadline;
            RemainingSeconds = double.IsNaN(assaultDeadline)
                ? double.NaN
                : Math.Max(0d, assaultDeadline - Time.timeAsDouble);
            Discovery = discovery;
        }

        public override string ToString()
        {
            string mission = Mission.HasValue ? Mission.Value.ToString() : "none";
            string history = Discovery.HasValue
                ? $"{Discovery.Value.RelicId} discovered by {Discovery.Value.DiscovererActorId}/{Discovery.Value.DiscovererOccurrenceId} at {Discovery.Value.Position} t={Discovery.Value.TimeSeconds:R}"
                : "none";
            return $"activity={Activity.ActivityName} mission={mission} ready={IsReady} occurrence={ReadinessOccurrence} " +
                   $"preparationStart={PreparationStartedAt:R} preparationDeadline={PreparationDeadline:R} preparationRemaining={PreparationRemainingSeconds:R} " +
                   $"assaultStart={AssaultStartedAt:R} assaultDeadline={AssaultDeadline:R} assaultRemaining={RemainingSeconds:R} discovery={history}";
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RouteContentContribution))]
    public sealed class PegaMissionLifecycle : RouteContentBehaviour, IRouteActivityTransitionObserver
    {
        [SerializeField] private ActivityAsset preparation;
        [SerializeField] private ActivityAsset assault;
        [SerializeField] private ActivityAsset dispute;
        [SerializeField] private ActivityRequestTrigger requestAssault;
        [SerializeField] private ActivityRequestTrigger requestDispute;
        [SerializeField] private ActivityReadinessEvents readiness;
        [SerializeField] private PlayerSessionObserver playerSessionObserver;
        [SerializeField] private ActorOccurrenceQueryBinding actorOccurrenceQuery;
        [SerializeField] private PegaGlobalKnowledgeState generalKnowledge;
        [SerializeField, Min(0.01f), Tooltip("Duração fixa da Preparação desta Route, em segundos de jogo.")]
        private float preparationSeconds = 30f;
        [SerializeField, Min(0.01f), Tooltip("Duração do Assalto em segundos de jogo, compartilhada com a Disputa.")]
        private float assaultSeconds = 180f;

        private ActivityAsset _current;
        private ActivityReadinessSnapshot _readiness;
        private double _preparationStart = double.NaN;
        private double _preparationDeadline = double.NaN;
        private double _assaultStart = double.NaN;
        private double _assaultDeadline = double.NaN;
        private PegaRelicDiscovery? _discovery;
        private Coroutine _playerControlWait;
        private Coroutine _preparationCountdown;
        private int _playerControlWaitGeneration;
        private int _preparationCountdownGeneration;
        private ActivityAsset _playerControlWaitActivity;
        private int _playerControlWaitReadinessOccurrence;
        private bool _preparationElapsed;
        private IEventBinding _assaultRequestBinding;
        private IEventBinding _disputeRequestBinding;
        private ScopedLogger _log;
        private bool _bound;

        public event Action<string> Observed;

        public bool IsAssaultReady =>
            _bound && _current == assault && _readiness.IsReady;

        public bool IsAssaultOperational =>
            IsAssaultReady && !double.IsNaN(_assaultStart);

        protected override void OnRouteContentEntered(RouteContentLifecycleContext context)
        {
            if (context.Binding == null || context.Binding.gameObject != gameObject || _bound)
                return;

            _log = PegaLogging.CreateScopedLogger<PegaMissionLifecycle>(
                "PEGA.Gameplay.Mission",
                gameObject.name);

            if (preparation == null || assault == null || dispute == null ||
                preparation == assault || preparation == dispute || assault == dispute ||
                !preparation.HasValidActivityId || !assault.HasValidActivityId || !dispute.HasValidActivityId ||
                readiness == null || readiness.gameObject.scene != gameObject.scene ||
                generalKnowledge == null || generalKnowledge.gameObject.scene != gameObject.scene ||
                playerSessionObserver == null ||
                playerSessionObserver.gameObject.scene != gameObject.scene ||
                playerSessionObserver.Scope != LocalPlayerProvisioningConsumerScope.Route ||
                actorOccurrenceQuery == null ||
                actorOccurrenceQuery.gameObject.scene != gameObject.scene ||
                requestAssault == null ||
                requestAssault.gameObject.scene != gameObject.scene ||
                requestAssault.TargetActivity != assault ||
                requestDispute == null ||
                requestDispute.gameObject.scene != gameObject.scene ||
                requestDispute.TargetActivity != dispute ||
                !IsPositiveFinite(preparationSeconds) || !IsPositiveFinite(assaultSeconds))
            {
                throw new InvalidOperationException(
                    "Mission requires distinct Preparation, Assault and Dispute Activities, matching Route requests, Route-scoped Player observation, an Actor query, readiness, Global Knowledge and positive finite durations.");
            }

            _bound = true;
            _assaultRequestBinding = requestAssault.SubscribeRequestEvents(OnRequestEvent);
            _disputeRequestBinding = requestDispute.SubscribeRequestEvents(OnRequestEvent);
            readiness.Preparing.AddListener(OnReadiness);
            readiness.Ready.AddListener(OnReadiness);
            readiness.NotReady.AddListener(OnReadiness);
        }

        protected override void OnRouteContentExited(RouteContentLifecycleContext context) => Release();

        private void OnDestroy() => Release();

        private void Release()
        {
            if (!_bound) return;
            _bound = false;
            StopPlayerControlWait();
            StopPreparationCountdown();
            _assaultRequestBinding?.Dispose();
            _assaultRequestBinding = null;
            _disputeRequestBinding?.Dispose();
            _disputeRequestBinding = null;
            if (readiness != null)
            {
                readiness.Preparing.RemoveListener(OnReadiness);
                readiness.Ready.RemoveListener(OnReadiness);
                readiness.NotReady.RemoveListener(OnReadiness);
            }
            _current = null;
            _readiness = default;
            _discovery = null;
            _preparationStart = double.NaN;
            _preparationDeadline = double.NaN;
            _assaultStart = double.NaN;
            _assaultDeadline = double.NaN;
            _preparationElapsed = false;
        }

        public void OnActivityTransitionCommitted(RouteActivityTransitionContext context)
        {
            if (!_bound) return;

            StopPlayerControlWait();
            if (context.CurrentActivity != preparation)
                StopPreparationCountdown();

            _current = context.CurrentActivity;
            _readiness = default;
            PublishObservation("commit");
        }

        private void OnReadiness()
        {
            ActivityReadinessSnapshot update = readiness.LastSnapshot;
            if (!_bound || _current == null || update.Activity != _current || !update.HasOccurrence)
                return;

            _readiness = update;
            if (!update.IsReady)
            {
                StopPlayerControlWait();
            }
            else if (_current == preparation && double.IsNaN(_preparationStart))
            {
                BeginWaitingForPlayerControl(preparation, update.Occurrence);
            }
            else if (_current == assault && double.IsNaN(_assaultStart))
            {
                BeginWaitingForPlayerControl(assault, update.Occurrence);
            }

            PublishObservation(update.IsReady ? "ready" : update.IsPreparing ? "preparing" : "not-ready");
        }

        private void BeginWaitingForPlayerControl(ActivityAsset activity, int readinessOccurrence)
        {
            if (_playerControlWait != null &&
                _playerControlWaitActivity == activity &&
                _playerControlWaitReadinessOccurrence == readinessOccurrence)
                return;

            StopPlayerControlWait();
            int waitGeneration = _playerControlWaitGeneration;
            _playerControlWaitActivity = activity;
            _playerControlWaitReadinessOccurrence = readinessOccurrence;
            RuntimeContentOwner expectedOwner = CreateActivityOwner(activity);
            _playerControlWait = StartCoroutine(
                WaitForPlayerControl(activity, readinessOccurrence, expectedOwner, waitGeneration));
        }

        private IEnumerator WaitForPlayerControl(
            ActivityAsset activity,
            int readinessOccurrence,
            RuntimeContentOwner expectedOwner,
            int waitGeneration)
        {
            bool waitingLogged = false;
            bool hasSessionActivityOccurrence = false;
            int sessionActivityOccurrence = 0;

            while (waitGeneration == _playerControlWaitGeneration &&
                   IsCurrentReadyOccurrence(activity, readinessOccurrence))
            {
                bool hasCurrentSessionContext = TryReadJoinedPlayerInputAvailability(
                    expectedOwner,
                    out bool allJoinedPlayersAvailable,
                    out int joinedPlayerCount,
                    out int observedSessionActivityOccurrence,
                    out string diagnostic);

                if (hasCurrentSessionContext)
                {
                    if (!hasSessionActivityOccurrence)
                    {
                        sessionActivityOccurrence = observedSessionActivityOccurrence;
                        hasSessionActivityOccurrence = true;
                    }
                    else if (sessionActivityOccurrence != observedSessionActivityOccurrence)
                    {
                        break;
                    }

                    if (allJoinedPlayersAvailable && joinedPlayerCount > 0)
                    {
                        CompletePlayerControlWait(waitGeneration);
                        if (activity == preparation)
                            StartPreparationClock(joinedPlayerCount, readinessOccurrence, sessionActivityOccurrence);
                        else if (activity == assault)
                            StartAssaultClock(joinedPlayerCount, readinessOccurrence, sessionActivityOccurrence);
                        yield break;
                    }

                    if (!waitingLogged)
                    {
                        _log.Debug(
                            $"Waiting for RuntimeGameplayAvailable from every joined Player before starting {activity.ActivityName}. " +
                            $"joinedPlayers={joinedPlayerCount}; activityOccurrence={sessionActivityOccurrence}; {diagnostic}");
                        waitingLogged = true;
                    }
                }
                else if (!waitingLogged)
                {
                    _log.Debug(
                        $"Waiting for current Route-scoped Player and Activity evidence before starting {activity.ActivityName}. {diagnostic}");
                    waitingLogged = true;
                }

                yield return null;
            }

            CompletePlayerControlWait(waitGeneration);
        }

        private bool TryReadJoinedPlayerInputAvailability(
            RuntimeContentOwner expectedActivityOwner,
            out bool allJoinedPlayersAvailable,
            out int joinedPlayerCount,
            out int activityOccurrence,
            out string diagnostic)
        {
            allJoinedPlayersAvailable = false;
            joinedPlayerCount = 0;
            activityOccurrence = 0;
            diagnostic = string.Empty;

            if (playerSessionObserver == null ||
                !playerSessionObserver.TryGetObservation(out PlayerSessionScopedObservationSnapshot observation))
            {
                diagnostic = playerSessionObserver != null
                    ? playerSessionObserver.Diagnostic
                    : "PlayerSessionObserver is missing.";
                return false;
            }

            if (!observation.HasCurrentActivityOccurrence ||
                observation.ActivityOwner != expectedActivityOwner)
            {
                diagnostic = "Player Session observation does not yet match the current Activity owner.";
                return false;
            }

            PlayerParticipationSnapshot participation = observation.Participation;
            if (participation == null || !participation.IsInitialized)
            {
                diagnostic = "Player Session participation is not initialized.";
                return false;
            }

            activityOccurrence = observation.ActivityOccurrence;
            bool allAvailable = true;
            for (int index = 0; index < participation.Slots.Count; index++)
            {
                PlayerSlotRuntimeSnapshot slot = participation.Slots[index];
                if (!slot.IsJoined)
                    continue;

                joinedPlayerCount++;
                if (!slot.PlayerSlotId.IsValid)
                {
                    allAvailable = false;
                    diagnostic = "A joined Player Slot has no valid identity.";
                    continue;
                }

                ActorOccurrenceResolution resolution = actorOccurrenceQuery.Resolve(
                    ActorOccurrenceReference.ForPlayerSlot(slot.PlayerSlotId));
                if (!resolution.Succeeded || resolution.Occurrence.ActorRoot == null)
                {
                    allAvailable = false;
                    if (string.IsNullOrEmpty(diagnostic))
                        diagnostic = $"Joined Player Slot '{slot.PlayerSlotId}' has no current Actor occurrence: {resolution.Diagnostic}";
                    continue;
                }

                GameObject actorRoot = resolution.Occurrence.ActorRoot;
                if (!actorRoot.TryGetComponent(out PlayerGameplayInputReader inputReader))
                {
                    allAvailable = false;
                    if (string.IsNullOrEmpty(diagnostic))
                        diagnostic = $"Current Player Actor for Slot '{slot.PlayerSlotId}' has no PlayerGameplayInputReader.";
                    continue;
                }

                PlayerGameplayInputBindingToken binding = inputReader.CurrentBindingToken;
                if (!binding.IsValid ||
                    binding.Owner != observation.ActivityOwner ||
                    binding.PlayerSlotId != slot.PlayerSlotId)
                {
                    allAvailable = false;
                    if (string.IsNullOrEmpty(diagnostic))
                        diagnostic = $"Player input binding for Slot '{slot.PlayerSlotId}' does not belong to the observed current Activity.";
                    continue;
                }

                if (!inputReader.GameplayReady || !inputReader.RuntimeGameplayAvailable)
                {
                    allAvailable = false;
                    if (string.IsNullOrEmpty(diagnostic))
                        diagnostic = $"Player input for Slot '{slot.PlayerSlotId}' is not operational yet.";
                }
            }

            if (joinedPlayerCount == 0)
            {
                diagnostic = "No joined Player Slot is available for this Activity.";
                return true;
            }

            allJoinedPlayersAvailable = allAvailable;
            return true;
        }

        private bool IsCurrentReadyOccurrence(ActivityAsset activity, int occurrence) =>
            _bound &&
            _current == activity &&
            _readiness.Activity == activity &&
            _readiness.IsReady &&
            _readiness.HasOccurrence &&
            _readiness.Occurrence == occurrence;

        private void CompletePlayerControlWait(int waitGeneration)
        {
            if (waitGeneration != _playerControlWaitGeneration)
                return;

            _playerControlWait = null;
            _playerControlWaitActivity = null;
            _playerControlWaitReadinessOccurrence = 0;
        }

        private void StopPlayerControlWait()
        {
            _playerControlWaitGeneration++;
            if (_playerControlWait != null)
            {
                StopCoroutine(_playerControlWait);
                _playerControlWait = null;
            }
            _playerControlWaitActivity = null;
            _playerControlWaitReadinessOccurrence = 0;
        }

        private void StartPreparationClock(
            int joinedPlayerCount,
            int readinessOccurrence,
            int sessionActivityOccurrence)
        {
            if (!_bound || _current != preparation || !double.IsNaN(_preparationStart))
                return;

            _preparationStart = Time.timeAsDouble;
            _preparationDeadline = _preparationStart + preparationSeconds;
            _preparationElapsed = false;
            int countdownGeneration = ++_preparationCountdownGeneration;
            _preparationCountdown = StartCoroutine(WaitForPreparationExpiry(countdownGeneration));
            _log.Info(
                $"Preparation clock started after all joined Players had operational input. " +
                $"players={joinedPlayerCount}; readinessOccurrence={readinessOccurrence}; " +
                $"activityOccurrence={sessionActivityOccurrence}; start={_preparationStart:R}; deadline={_preparationDeadline:R}");
            PublishObservation("preparation-start");
        }

        private IEnumerator WaitForPreparationExpiry(int countdownGeneration)
        {
            while (countdownGeneration == _preparationCountdownGeneration &&
                   _bound &&
                   _current == preparation &&
                   Time.timeAsDouble < _preparationDeadline)
            {
                yield return null;
            }

            if (countdownGeneration != _preparationCountdownGeneration)
                yield break;

            _preparationCountdown = null;
            if (!_bound || _current != preparation || double.IsNaN(_preparationDeadline))
                yield break;

            _preparationElapsed = true;
            PublishObservation("preparation-expired");
            RequestAssault();
        }

        private void StartAssaultClock(
            int joinedPlayerCount,
            int readinessOccurrence,
            int sessionActivityOccurrence)
        {
            if (!_bound || _current != assault || !double.IsNaN(_assaultStart))
                return;

            _assaultStart = Time.timeAsDouble;
            _assaultDeadline = _assaultStart + assaultSeconds;
            _log.Info(
                $"Assault clock started once after the Activity was ready and all joined Players had operational input. " +
                $"players={joinedPlayerCount}; readinessOccurrence={readinessOccurrence}; " +
                $"activityOccurrence={sessionActivityOccurrence}; start={_assaultStart:R}; deadline={_assaultDeadline:R}");
            PublishObservation("assault-start");
        }

        public bool TryRead(out PegaMissionSnapshot state)
        {
            state = default;
            if (!_bound || _current == null ||
                (_current != preparation && _current != assault && _current != dispute))
                return false;

            PegaMainMission? mission = _current == assault
                ? PegaMainMission.FindRelic
                : _current == dispute
                    ? PegaMainMission.EscapeWithGroupRelic
                    : null;
            state = new PegaMissionSnapshot(
                _current,
                mission,
                _readiness,
                _preparationStart,
                _preparationDeadline,
                _assaultStart,
                _assaultDeadline,
                _discovery);
            return true;
        }

        public bool TryReadGlobalDiscovery(out PegaRelicDiscovery discovery)
        {
            discovery = default;
            return _bound &&
                generalKnowledge.IsOwnedByRoute(RouteContentContribution) &&
                generalKnowledge.ReadFact(PegaGlobalKnowledgeFacts.RelicDiscovery.For(0), Time.timeAsDouble)
                    .TryGetValue(out discovery);
        }

        public bool ConfirmDiscovery(PegaRelicDiscovery discovery)
        {
            if (!IsAssaultOperational || _discovery.HasValue || !discovery.IsValid ||
                discovery.TimeSeconds < _assaultStart ||
                discovery.TimeSeconds > Time.timeAsDouble ||
                discovery.TimeSeconds >= _assaultDeadline ||
                !generalKnowledge.IsOwnedByRoute(RouteContentContribution))
                return false;

            var evidence = PegaKnowledgeEvidence.From(discovery.DiscovererOccurrenceId, discovery.TimeSeconds, this);
            var key = PegaGlobalKnowledgeFacts.RelicDiscovery.For(0);
            if (generalKnowledge.ObserveFact(key, discovery, evidence) != PegaKnowledgeWriteResult.Recorded)
            {
                _log.Error("The Route could not publish the controlled relic discovery to Global Knowledge.");
                return false;
            }
            _discovery = discovery;
            PublishObservation("discovery");
            RequestDispute();
            return true;
        }

        [ContextMenu("Retry Assault Request")]
        private void RequestAssault()
        {
            if (!_bound || !_preparationElapsed || _current != preparation ||
                requestAssault.IsRequestInFlight)
                return;

            requestAssault.RequestActivity();
        }

        [ContextMenu("Retry Dispute Request")]
        private void RequestDispute()
        {
            if (!_bound || !_discovery.HasValue || _current != assault ||
                requestDispute.IsRequestInFlight)
                return;

            requestDispute.RequestActivity();
        }

        private void OnRequestEvent(ActivityRequestTriggerEvent change)
        {
            if (!change.ClearsActivity &&
                (change.TargetActivity == assault || change.TargetActivity == dispute))
            {
                _log.Info(
                    $"{change.TargetActivity.ActivityName} request {change.Phase}, " +
                    $"outcome={change.Outcome}, reason={change.Reason}, message={change.Message}");
            }
        }

        private void PublishObservation(string milestone)
        {
            if (TryRead(out PegaMissionSnapshot snapshot))
                _log.Info($"Mission {milestone}: {snapshot}");
            Observed?.Invoke(milestone);
        }

        private static RuntimeContentOwner CreateActivityOwner(ActivityAsset activity) =>
            RuntimeContentOwner.Activity(
                activity.ActivityId.StableText,
                activity.ActivityName,
                RuntimeDefinitionToken.FromUnityObject(activity));

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private void StopPreparationCountdown()
        {
            _preparationCountdownGeneration++;
            if (_preparationCountdown != null)
            {
                StopCoroutine(_preparationCountdown);
                _preparationCountdown = null;
            }
        }
    }
}
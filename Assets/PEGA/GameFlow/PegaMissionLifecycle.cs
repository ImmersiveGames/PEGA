using System;
using Immersive.Foundation.Events;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.Authoring;
using Immersive.Framework.GameFlow;
using Immersive.Framework.RouteLifecycle;
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
        public PegaMainMission Mission { get; }
        public bool IsReady { get; }
        public int ReadinessOccurrence { get; }
        public double AssaultStartedAt { get; }
        public double AssaultDeadline { get; }
        public double RemainingSeconds { get; }
        public PegaRelicDiscovery? Discovery { get; }

        internal PegaMissionSnapshot(
            ActivityAsset activity,
            PegaMainMission mission,
            ActivityReadinessSnapshot readiness,
            double assaultStart,
            double assaultDeadline,
            PegaRelicDiscovery? discovery)
        {
            Activity = activity;
            Mission = mission;
            IsReady = readiness.IsReady;
            ReadinessOccurrence = readiness.Occurrence;
            AssaultStartedAt = assaultStart;
            AssaultDeadline = assaultDeadline;
            RemainingSeconds = double.IsNaN(assaultDeadline)
                ? double.NaN
                : Math.Max(0d, assaultDeadline - Time.timeAsDouble);
            Discovery = discovery;
        }

        public override string ToString()
        {
            string history = Discovery.HasValue
                ? $"{Discovery.Value.RelicId} discovered by {Discovery.Value.DiscovererActorId}/{Discovery.Value.DiscovererOccurrenceId} at {Discovery.Value.Position} t={Discovery.Value.TimeSeconds:R}"
                : "none";
            return $"activity={Activity.ActivityName} mission={Mission} ready={IsReady} occurrence={ReadinessOccurrence} " +
                   $"assaultStart={AssaultStartedAt:R} assaultDeadline={AssaultDeadline:R} remaining={RemainingSeconds:R} discovery={history}";
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RouteContentContribution))]
    public sealed class PegaMissionLifecycle : RouteContentBehaviour, IRouteActivityTransitionObserver
    {
        [SerializeField] private ActivityAsset assault;
        [SerializeField] private ActivityAsset dispute;
        [SerializeField] private ActivityRequestTrigger requestDispute;
        [SerializeField] private ActivityReadinessEvents readiness;
        [SerializeField] private PegaGlobalKnowledgeState generalKnowledge;
        [SerializeField, Min(0.01f), Tooltip("Duração do Assalto em segundos de jogo, compartilhada com a Disputa.")]
        private float assaultSeconds = 180f;

        private ActivityAsset _current;
        private ActivityReadinessSnapshot _readiness;
        private double _assaultStart = double.NaN;
        private double _assaultDeadline = double.NaN;
        private PegaRelicDiscovery? _discovery;
        private IEventBinding _disputeRequestBinding;
        private ScopedLogger _log;
        private bool _bound;

        public event Action<string> Observed;

        public bool IsAssaultReady =>
            _bound && _current == assault && _readiness.IsReady;

        protected override void OnRouteContentEntered(RouteContentLifecycleContext context)
        {
            if (context.Binding == null || context.Binding.gameObject != gameObject || _bound)
                return;

            _log = PegaLogging.CreateScopedLogger<PegaMissionLifecycle>(
                "PEGA.Gameplay.Mission",
                gameObject.name);

            if (assault == null || dispute == null || assault == dispute ||
                !assault.HasValidActivityId || !dispute.HasValidActivityId ||
                readiness == null || generalKnowledge == null ||
                requestDispute == null || requestDispute.TargetActivity != dispute ||
                !IsPositiveFinite(assaultSeconds))
            {
                throw new InvalidOperationException(
                    "Mission requires distinct Assault and Dispute Activities, a matching Route request, readiness, Global Knowledge and a positive finite Assault duration.");
            }

            _bound = true;
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
            _assaultStart = double.NaN;
            _assaultDeadline = double.NaN;
        }

        public void OnActivityTransitionCommitted(RouteActivityTransitionContext context)
        {
            if (!_bound) return;

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
            if (update.IsReady && _current == assault && double.IsNaN(_assaultStart))
                StartAssaultClock(update.Occurrence);

            PublishObservation(update.IsReady ? "ready" : update.IsPreparing ? "preparing" : "not-ready");
        }

        private void StartAssaultClock(int readinessOccurrence)
        {
            if (!_bound || _current != assault || !double.IsNaN(_assaultStart))
                return;

            _assaultStart = Time.timeAsDouble;
            _assaultDeadline = _assaultStart + assaultSeconds;
            _log.Info(
                $"Assault clock started once at Activity readiness. " +
                $"readinessOccurrence={readinessOccurrence}; start={_assaultStart:R}; deadline={_assaultDeadline:R}");
            PublishObservation("assault-start");
        }

        public bool TryRead(out PegaMissionSnapshot state)
        {
            state = default;
            if (!_bound || _current == null || (_current != assault && _current != dispute))
                return false;

            PegaMainMission mission = _current == assault
                ? PegaMainMission.FindRelic
                : PegaMainMission.EscapeWithGroupRelic;
            state = new PegaMissionSnapshot(
                _current,
                mission,
                _readiness,
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
            if (!IsAssaultReady || _discovery.HasValue || !discovery.IsValid ||
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
            if (!change.ClearsActivity && change.TargetActivity == dispute)
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

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
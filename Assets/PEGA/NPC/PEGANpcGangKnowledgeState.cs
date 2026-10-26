using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Immersive.Foundation.Events;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    public enum PEGANpcGangKnowledgeEvidenceOrigin
    {
        SpawnPointPhysicalIngress = 0
    }

    /// <summary>Uma evidência de uso de Spawn Point, compartilhada pelos membros de uma Gangue.</summary>
    public sealed class PEGANpcGangKnowledgeFactChangedEvent : IEvent
    {
        internal PEGANpcGangKnowledgeFactChangedEvent(
            string spawnPointId,
            PEGANpcPlanningTarget target,
            PEGANpcGangKnowledgeEvidenceOrigin evidenceOrigin,
            double evidenceTimeSeconds)
        {
            SpawnPointId = spawnPointId;
            Target = target;
            EvidenceOrigin = evidenceOrigin;
            EvidenceTimeSeconds = evidenceTimeSeconds;
        }

        public string SpawnPointId { get; }

        public PEGANpcPlanningTarget Target { get; }

        public PEGANpcGangKnowledgeEvidenceOrigin EvidenceOrigin { get; }

        public double EvidenceTimeSeconds { get; }
    }

    /// <summary>Armazena conhecimento de Spawn Points no escopo de uma única Gangue.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Gang Knowledge State")]
    public sealed class PEGANpcGangKnowledgeState : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.GangKnowledge";
        private const string EscapeTargetIdPrefix = "pega.spawn-point.escape:";

        [NonSerialized]
        private Dictionary<string, KnownSpawnPointFact> _knownEscapePoints;

        [NonSerialized]
        private EventBus<PEGANpcGangKnowledgeFactChangedEvent> _changeEvents;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        private void Awake()
        {
            EnsureStateInitialized();
            EnsureLoggerInitialized();
        }

        private void OnDestroy()
        {
            _changeEvents?.Clear();
            _changeEvents = null;
        }

        /// <summary>Associa um NPC a esta Gangue sem copiar seu conhecimento individual.</summary>
        public bool TryJoinMember(
            GameObject actorRoot,
            out PEGANpcGangMembership membership,
            out string diagnostic)
        {
            membership = null;
            diagnostic = string.Empty;
            if (actorRoot == null)
            {
                diagnostic = "A gang member Actor root is required.";
                return false;
            }

            if (actorRoot.GetComponent<PEGANpcKnowledgeState>() == null)
            {
                diagnostic = "A gang member must have its own PEGANpcKnowledgeState on the Actor root.";
                return false;
            }

            membership = actorRoot.GetComponent<PEGANpcGangMembership>();
            if (membership == null)
            {
                diagnostic = "A gang member Actor must compose PEGANpcGangMembership before joining a Gang knowledge state.";
                return false;
            }

            if (!membership.TryBind(this, out diagnostic))
            {
                membership = null;
                return false;
            }

            EnsureLoggerInitialized();
            _log.Debug(
                "NPC joined a Gang knowledge state.",
                LogFields.Field("gangKnowledgeState", gameObject.name),
                LogFields.Field("actorRoot", actorRoot.name),
                LogFields.Field("membershipComposed", true),
                LogFields.Field("knownEscapePointCount", GetKnownEscapePointCount()));
            return true;
        }

        /// <summary>Retorna uma cópia ordenada dos Targets conhecidos neste estado da Gangue.</summary>
        public IReadOnlyList<PEGANpcPlanningTarget> GetKnownEscapeTargets()
        {
            EnsureStateInitialized();
            var spawnPointIds = new List<string>(_knownEscapePoints.Keys);
            spawnPointIds.Sort(StringComparer.Ordinal);

            var targets = new List<PEGANpcPlanningTarget>(spawnPointIds.Count);
            for (int i = 0; i < spawnPointIds.Count; i++)
            {
                targets.Add(_knownEscapePoints[spawnPointIds[i]].Target);
            }

            return new ReadOnlyCollection<PEGANpcPlanningTarget>(targets);
        }

        /// <summary>Consulta o Target imutável de um Spawn Point conhecido pela Gangue.</summary>
        public bool TryGetKnownEscapeTarget(
            string spawnPointId,
            out PEGANpcPlanningTarget target)
        {
            target = default;
            if (string.IsNullOrWhiteSpace(spawnPointId))
            {
                return false;
            }

            EnsureStateInitialized();
            if (!_knownEscapePoints.TryGetValue(spawnPointId.Trim(), out KnownSpawnPointFact fact))
            {
                return false;
            }

            target = fact.Target;
            return true;
        }

        /// <summary>Assina novas evidências aceitas por esta Gangue.</summary>
        public IEventBinding SubscribeToChanges(Action<PEGANpcGangKnowledgeFactChangedEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureStateInitialized();
            _changeEvents ??= new EventBus<PEGANpcGangKnowledgeFactChangedEvent>();
            return _changeEvents.Subscribe(change =>
            {
                try
                {
                    handler(change);
                }
                catch (Exception exception)
                {
                    LogSubscriberFailure(handler, change, exception);
                }
            });
        }

        public bool UnsubscribeFromChanges(IEventBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            return _changeEvents != null && _changeEvents.Unsubscribe(binding);
        }

        internal bool TryRecordSpawnPointUse(
            PEGANpcSpawnPoint spawnPoint,
            string spawnPointId,
            Vector3 knownPosition,
            double evidenceTimeSeconds,
            GameObject memberRoot,
            out bool wasAdded,
            out string diagnostic)
        {
            wasAdded = false;
            diagnostic = string.Empty;
            EnsureStateInitialized();
            if (spawnPoint == null || memberRoot == null ||
                string.IsNullOrWhiteSpace(spawnPointId) ||
                !IsFinite(knownPosition) || !IsNonNegativeFinite(evidenceTimeSeconds))
            {
                diagnostic = "Spawn Point evidence requires a source, member, stable id, finite position and valid time.";
                return false;
            }

            PEGANpcGangMembership membership = memberRoot.GetComponent<PEGANpcGangMembership>();
            if (membership == null || membership.GangKnowledgeState != this ||
                spawnPoint.GangKnowledgeState != this)
            {
                diagnostic = "The entering NPC and Spawn Point must reference this same Gang knowledge state.";
                return false;
            }

            string normalizedId = spawnPointId.Trim();
            if (_knownEscapePoints.TryGetValue(normalizedId, out KnownSpawnPointFact existing))
            {
                if (!ReferenceEquals(existing.Source, spawnPoint))
                {
                    diagnostic = "Another Spawn Point already uses this stable id in the Gang.";
                    return false;
                }

                return true;
            }

            var targetId = PEGANpcPlanningTargetId.ForStableDataId(
                string.Concat(EscapeTargetIdPrefix, normalizedId));
            PEGANpcPlanningTarget target = PEGANpcPlanningTarget.FromKnownEscapeDestination(
                targetId,
                knownPosition,
                evidenceTimeSeconds);
            if (!target.IsValid)
            {
                diagnostic = "The Spawn Point did not produce a valid known escape Target.";
                return false;
            }

            _knownEscapePoints.Add(
                normalizedId,
                new KnownSpawnPointFact(spawnPoint, target));
            wasAdded = true;

            EnsureLoggerInitialized();
            _log.Debug(
                "Gang learned a potential escape from physical Spawn Point use.",
                LogFields.Field("gangKnowledgeState", gameObject.name),
                LogFields.Field("spawnPointId", normalizedId),
                LogFields.Field("targetKind", target.Kind),
                LogFields.Field("targetProvenance", target.Provenance),
                LogFields.Field("targetIdentity", target.TargetId.IdentityKind),
                LogFields.Field("knownPositionX", knownPosition.x),
                LogFields.Field("knownPositionY", knownPosition.y),
                LogFields.Field("knownPositionZ", knownPosition.z),
                LogFields.Field("evidenceTimeSeconds", evidenceTimeSeconds),
                LogFields.Field("memberRoot", memberRoot.name));
            _log.Info(
                "Gang learned a new escape route.",
                LogFields.Field("actorRoot", memberRoot.name));

            PublishChange(new PEGANpcGangKnowledgeFactChangedEvent(
                normalizedId,
                target,
                PEGANpcGangKnowledgeEvidenceOrigin.SpawnPointPhysicalIngress,
                evidenceTimeSeconds));
            return true;
        }

        internal void LogRejectedSpawnPointUse(
            string spawnPointId,
            GameObject memberRoot,
            string reason)
        {
            EnsureLoggerInitialized();
            _log.Warning(
                "Gang Spawn Point use was not accepted as knowledge.",
                LogFields.Field("gangKnowledgeState", gameObject.name),
                LogFields.Field("spawnPointId", spawnPointId ?? string.Empty),
                LogFields.Field("memberRoot", memberRoot != null ? memberRoot.name : "unavailable"),
                LogFields.Field("reason", reason ?? string.Empty));
        }

        private void PublishChange(PEGANpcGangKnowledgeFactChangedEvent change)
        {
            if (_changeEvents == null)
            {
                return;
            }

            _changeEvents.Publish(change);
        }

        private void LogSubscriberFailure(
            Action<PEGANpcGangKnowledgeFactChangedEvent> handler,
            PEGANpcGangKnowledgeFactChangedEvent change,
            Exception exception)
        {
            EnsureLoggerInitialized();
            _log.Error(
                "Gang knowledge subscriber failed; the accepted fact and remaining subscribers are preserved.",
                LogFields.Field("gangKnowledgeState", gameObject.name),
                LogFields.Field("spawnPointId", change.SpawnPointId),
                LogFields.Field("subscriberMethod", handler.Method.Name),
                LogFields.Field("exceptionType", exception.GetType().FullName),
                LogFields.Field("exceptionMessage", exception.Message));
        }

        private int GetKnownEscapePointCount()
        {
            EnsureStateInitialized();
            return _knownEscapePoints.Count;
        }

        private void EnsureStateInitialized()
        {
            _knownEscapePoints ??= new Dictionary<string, KnownSpawnPointFact>(StringComparer.Ordinal);
        }

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PEGALogging.CreateScopedLogger<PEGANpcGangKnowledgeState>(LogCategory, gameObject.name);
            _loggerInitialized = true;
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(double value) =>
            value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);

        private readonly struct KnownSpawnPointFact
        {
            public KnownSpawnPointFact(
                PEGANpcSpawnPoint source,
                PEGANpcPlanningTarget target)
            {
                Source = source;
                Target = target;
            }

            public PEGANpcSpawnPoint Source { get; }

            public PEGANpcPlanningTarget Target { get; }
        }
    }
}

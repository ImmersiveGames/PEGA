using System;
using System.Collections.Generic;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.GOAP;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    public readonly struct PEGANpcDesireSatisfactionSnapshot
    {
        internal PEGANpcDesireSatisfactionSnapshot(
            string desireId,
            float satisfaction,
            int completedExecutions)
        {
            DesireId = desireId ?? string.Empty;
            Satisfaction = satisfaction;
            CompletedExecutions = completedExecutions;
        }

        public string DesireId { get; }

        public float Satisfaction { get; }

        public int CompletedExecutions { get; }
    }

    public sealed class PEGANpcDesireStateSnapshot
    {
        private static readonly IReadOnlyList<PEGANpcDesireSatisfactionSnapshot> EmptyValues =
            Array.AsReadOnly(Array.Empty<PEGANpcDesireSatisfactionSnapshot>());

        private readonly Dictionary<string, PEGANpcDesireSatisfactionSnapshot> _byDesireId;
        private readonly IReadOnlyList<PEGANpcDesireSatisfactionSnapshot> _values;

        internal PEGANpcDesireStateSnapshot(
            IReadOnlyList<PEGANpcDesireSatisfactionSnapshot> values)
        {
            PEGANpcDesireSatisfactionSnapshot[] copy = new PEGANpcDesireSatisfactionSnapshot[values.Count];
            _byDesireId = new Dictionary<string, PEGANpcDesireSatisfactionSnapshot>(
                values.Count,
                StringComparer.Ordinal);
            for (int index = 0; index < values.Count; index++)
            {
                PEGANpcDesireSatisfactionSnapshot value = values[index];
                copy[index] = value;
                _byDesireId[value.DesireId] = value;
            }

            _values = Array.AsReadOnly(copy);
        }

        public IReadOnlyList<PEGANpcDesireSatisfactionSnapshot> Values => _values ?? EmptyValues;

        public bool TryGet(string desireId, out PEGANpcDesireSatisfactionSnapshot value)
        {
            if (_byDesireId != null && !string.IsNullOrWhiteSpace(desireId) &&
                _byDesireId.TryGetValue(desireId, out value))
            {
                return true;
            }

            value = new PEGANpcDesireSatisfactionSnapshot(desireId, 0f, 0);
            return false;
        }
    }

    public sealed class PEGANpcDesireSatisfactionUpdate
    {
        private static readonly IReadOnlyList<PEGANpcDesireSatisfactionSnapshot> EmptyValues =
            Array.AsReadOnly(Array.Empty<PEGANpcDesireSatisfactionSnapshot>());

        private readonly IReadOnlyList<PEGANpcDesireSatisfactionSnapshot> _updatedValues;

        internal PEGANpcDesireSatisfactionUpdate(
            IReadOnlyList<PEGANpcDesireSatisfactionSnapshot> updatedValues)
        {
            PEGANpcDesireSatisfactionSnapshot[] copy = new PEGANpcDesireSatisfactionSnapshot[updatedValues.Count];
            for (int index = 0; index < updatedValues.Count; index++)
            {
                copy[index] = updatedValues[index];
            }

            _updatedValues = Array.AsReadOnly(copy);
        }

        public IReadOnlyList<PEGANpcDesireSatisfactionSnapshot> UpdatedValues =>
            _updatedValues ?? EmptyValues;

        public int UpdatedCount => UpdatedValues.Count;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Desire State")]
    public sealed class PEGANpcDesireState : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.Desires";

        [NonSerialized]
        private Dictionary<string, Entry> _entries;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        [NonSerialized]
        private long _highestRecordedRequestId;

        private void Awake()
        {
            EnsureEntriesInitialized();
            EnsureLoggerInitialized();
        }

        public PEGANpcDesireStateSnapshot GetSnapshot()
        {
            EnsureEntriesInitialized();
            List<string> ids = new List<string>(_entries.Keys);
            ids.Sort(StringComparer.Ordinal);

            List<PEGANpcDesireSatisfactionSnapshot> values =
                new List<PEGANpcDesireSatisfactionSnapshot>(ids.Count);
            for (int index = 0; index < ids.Count; index++)
            {
                Entry entry = _entries[ids[index]];
                values.Add(new PEGANpcDesireSatisfactionSnapshot(
                    ids[index],
                    entry.Satisfaction,
                    entry.CompletedExecutions));
            }

            return new PEGANpcDesireStateSnapshot(values);
        }

        public bool TryGetSatisfaction(
            string desireId,
            out PEGANpcDesireSatisfactionSnapshot satisfaction)
        {
            EnsureEntriesInitialized();
            if (!string.IsNullOrWhiteSpace(desireId) && _entries.TryGetValue(desireId, out Entry entry))
            {
                satisfaction = new PEGANpcDesireSatisfactionSnapshot(
                    desireId,
                    entry.Satisfaction,
                    entry.CompletedExecutions);
                return true;
            }

            satisfaction = new PEGANpcDesireSatisfactionSnapshot(desireId, 0f, 0);
            return false;
        }

        public PEGANpcDesireSatisfactionUpdate RecordCompletedAction(
            PEGANpcObservedActionEffect observedEffect,
            IPEGAGoapObservedStateView observedState,
            PEGANpcDesireContextDefinition activeContext)
        {
            if (observedState == null)
            {
                throw new ArgumentNullException(nameof(observedState));
            }

            if (activeContext == null)
            {
                throw new ArgumentNullException(nameof(activeContext));
            }

            if (observedState.Source != PEGAGoapStateSource.WorldState ||
                observedState.KnowledgeState == null ||
                observedState.KnowledgeState.Source != PEGAGoapStateSource.KnowledgeState)
            {
                throw new ArgumentException(
                    "Desire satisfaction requires an observed World State outcome and its separate Knowledge State.",
                    nameof(observedState));
            }

            if (!activeContext.TryValidate(
                    new HashSet<string>(StringComparer.Ordinal),
                    out string contextDiagnostic))
            {
                throw new ArgumentException(contextDiagnostic, nameof(activeContext));
            }

            EnsureEntriesInitialized();
            EnsureLoggerInitialized();
            List<PEGANpcDesireSatisfactionSnapshot> updated =
                new List<PEGANpcDesireSatisfactionSnapshot>();
            bool staleOrDuplicateObservation = false;

            if (observedEffect.IsValid && observedState.ObservedEffects.Count > 0)
            {
                if (observedEffect.RequestId <= _highestRecordedRequestId)
                {
                    staleOrDuplicateObservation = true;
                }
                else
                {
                    _highestRecordedRequestId = observedEffect.RequestId;
                    IReadOnlyList<PEGANpcDesireConfiguration> configurations = activeContext.Desires;
                    for (int index = 0; index < configurations.Count; index++)
                    {
                        PEGANpcDesireConfiguration configuration = configurations[index];
                        PEGANpcDesireDefinition definition = configuration.Definition;
                        PEGANpcDesireSatisfactionSnapshot current = GetSatisfaction(definition.DesireId);
                        if (definition.CreateGoal().Evaluate(observedState) != PEGAGoapTruthValue.True ||
                            !configuration.CanRecordSatisfaction(current))
                        {
                            continue;
                        }

                        if (current.CompletedExecutions == int.MaxValue)
                        {
                            continue;
                        }

                        float nextSatisfaction = Mathf.Min(
                            configuration.MaximumSatisfaction,
                            current.Satisfaction + configuration.SatisfactionGain);
                        Entry entry = new Entry(nextSatisfaction, current.CompletedExecutions + 1);
                        _entries[definition.DesireId] = entry;
                        updated.Add(new PEGANpcDesireSatisfactionSnapshot(
                            definition.DesireId,
                            entry.Satisfaction,
                            entry.CompletedExecutions));
                    }
                }
            }

            updated.Sort((left, right) => string.CompareOrdinal(left.DesireId, right.DesireId));
            PEGANpcDesireSatisfactionUpdate result = new PEGANpcDesireSatisfactionUpdate(updated);
            _log.Debug(
                "Completed NPC Action evaluated against active Goal conditions.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("context", activeContext.Context),
                LogFields.Field("effect", observedEffect.Kind),
                LogFields.Field("effectTargetId", observedEffect.TargetId),
                LogFields.Field("effectTargetKind", observedEffect.TargetKind),
                LogFields.Field("effectValid", observedEffect.IsValid),
                LogFields.Field("requestId", observedEffect.RequestId),
                LogFields.Field("staleOrDuplicateObservation", staleOrDuplicateObservation),
                LogFields.Field("observedCausalEffectCount", observedState.ObservedEffects.Count),
                LogFields.Field("updatedDesireCount", result.UpdatedCount),
                LogFields.Field("updatedDesireIds", JoinDesireIds(result.UpdatedValues)));
            return result;
        }

        private PEGANpcDesireSatisfactionSnapshot GetSatisfaction(string desireId)
        {
            if (_entries.TryGetValue(desireId, out Entry entry))
            {
                return new PEGANpcDesireSatisfactionSnapshot(
                    desireId,
                    entry.Satisfaction,
                    entry.CompletedExecutions);
            }

            return new PEGANpcDesireSatisfactionSnapshot(desireId, 0f, 0);
        }

        private void EnsureEntriesInitialized()
        {
            _entries ??= new Dictionary<string, Entry>(StringComparer.Ordinal);
        }

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PEGALogging.CreateScopedLogger<PEGANpcDesireState>(
                LogCategory,
                gameObject.name);
            _loggerInitialized = true;
        }

        private static string JoinDesireIds(
            IReadOnlyList<PEGANpcDesireSatisfactionSnapshot> values)
        {
            if (values.Count == 0)
            {
                return string.Empty;
            }

            string[] ids = new string[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                ids[index] = values[index].DesireId;
            }

            return string.Join(",", ids);
        }

        private sealed class Entry
        {
            public Entry(float satisfaction, int completedExecutions)
            {
                Satisfaction = satisfaction;
                CompletedExecutions = completedExecutions;
            }

            public float Satisfaction { get; }

            public int CompletedExecutions { get; }
        }
    }
}

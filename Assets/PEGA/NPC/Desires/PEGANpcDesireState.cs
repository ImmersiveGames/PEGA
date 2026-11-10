using System;
using System.Collections.Generic;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.GOAP;
using PEGA.NPC.Actions.Runtime;
using UnityEngine;
namespace PEGA.NPC.Desires
{
    public readonly struct PegaNpcDesireSatisfactionSnapshot
    {
        internal PegaNpcDesireSatisfactionSnapshot(
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

    public sealed class PegaNpcDesireStateSnapshot
    {
        private static readonly IReadOnlyList<PegaNpcDesireSatisfactionSnapshot> EmptyValues =
            Array.AsReadOnly(Array.Empty<PegaNpcDesireSatisfactionSnapshot>());

        private readonly Dictionary<string, PegaNpcDesireSatisfactionSnapshot> _byDesireId;
        private readonly IReadOnlyList<PegaNpcDesireSatisfactionSnapshot> _values;

        internal PegaNpcDesireStateSnapshot(
            IReadOnlyList<PegaNpcDesireSatisfactionSnapshot> values)
        {
            PegaNpcDesireSatisfactionSnapshot[] copy = new PegaNpcDesireSatisfactionSnapshot[values.Count];
            _byDesireId = new Dictionary<string, PegaNpcDesireSatisfactionSnapshot>(
                values.Count,
                StringComparer.Ordinal);
            for (int index = 0; index < values.Count; index++)
            {
                PegaNpcDesireSatisfactionSnapshot value = values[index];
                copy[index] = value;
                _byDesireId[value.DesireId] = value;
            }

            _values = Array.AsReadOnly(copy);
        }

        public IReadOnlyList<PegaNpcDesireSatisfactionSnapshot> Values => _values ?? EmptyValues;

        public bool TryGet(string desireId, out PegaNpcDesireSatisfactionSnapshot value)
        {
            if (_byDesireId != null && !string.IsNullOrWhiteSpace(desireId) &&
                _byDesireId.TryGetValue(desireId, out value))
            {
                return true;
            }

            value = new PegaNpcDesireSatisfactionSnapshot(desireId, 0f, 0);
            return false;
        }
    }

    public sealed class PegaNpcDesireSatisfactionUpdate
    {
        private static readonly IReadOnlyList<PegaNpcDesireSatisfactionSnapshot> EmptyValues =
            Array.AsReadOnly(Array.Empty<PegaNpcDesireSatisfactionSnapshot>());

        private readonly IReadOnlyList<PegaNpcDesireSatisfactionSnapshot> _updatedValues;

        internal PegaNpcDesireSatisfactionUpdate(
            IReadOnlyList<PegaNpcDesireSatisfactionSnapshot> updatedValues)
        {
            PegaNpcDesireSatisfactionSnapshot[] copy = new PegaNpcDesireSatisfactionSnapshot[updatedValues.Count];
            for (int index = 0; index < updatedValues.Count; index++)
            {
                copy[index] = updatedValues[index];
            }

            _updatedValues = Array.AsReadOnly(copy);
        }

        public IReadOnlyList<PegaNpcDesireSatisfactionSnapshot> UpdatedValues =>
            _updatedValues ?? EmptyValues;

        public int UpdatedCount => UpdatedValues.Count;
    }

    public sealed class PegaNpcDesireState
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.Desires";
        private readonly string _actorRootName;

        [NonSerialized]
        private Dictionary<string, Entry> _entries;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        [NonSerialized]
        private long _highestRecordedRequestId;

        internal PegaNpcDesireState(string actorRootName)
        {
            _actorRootName = string.IsNullOrWhiteSpace(actorRootName) ? "NPC" : actorRootName;
            EnsureEntriesInitialized();
            EnsureLoggerInitialized();
        }

        public PegaNpcDesireStateSnapshot GetSnapshot()
        {
            EnsureEntriesInitialized();
            List<string> ids = new List<string>(_entries.Keys);
            ids.Sort(StringComparer.Ordinal);

            List<PegaNpcDesireSatisfactionSnapshot> values =
                new List<PegaNpcDesireSatisfactionSnapshot>(ids.Count);
            for (int index = 0; index < ids.Count; index++)
            {
                Entry entry = _entries[ids[index]];
                values.Add(new PegaNpcDesireSatisfactionSnapshot(
                    ids[index],
                    entry.Satisfaction,
                    entry.CompletedExecutions));
            }

            return new PegaNpcDesireStateSnapshot(values);
        }

        public bool TryGetSatisfaction(
            string desireId,
            out PegaNpcDesireSatisfactionSnapshot satisfaction)
        {
            EnsureEntriesInitialized();
            if (!string.IsNullOrWhiteSpace(desireId) && _entries.TryGetValue(desireId, out Entry entry))
            {
                satisfaction = new PegaNpcDesireSatisfactionSnapshot(
                    desireId,
                    entry.Satisfaction,
                    entry.CompletedExecutions);
                return true;
            }

            satisfaction = new PegaNpcDesireSatisfactionSnapshot(desireId, 0f, 0);
            return false;
        }

        public PegaNpcDesireSatisfactionUpdate RecordCompletedAction(
            PegaNpcObservedActionEffect observedEffect,
            IGoapObservedStateView observedState,
            PegaNpcDesireContextDefinition activeContext)
        {
            if (observedState == null)
            {
                throw new ArgumentNullException(nameof(observedState));
            }

            if (activeContext == null)
            {
                throw new ArgumentNullException(nameof(activeContext));
            }

            if (observedState.Source != GoapStateSource.WorldState ||
                observedState.KnowledgeState == null ||
                observedState.KnowledgeState.Source != GoapStateSource.KnowledgeState)
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
            List<PegaNpcDesireSatisfactionSnapshot> updated =
                new List<PegaNpcDesireSatisfactionSnapshot>();
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
                    IReadOnlyList<PegaNpcDesireConfiguration> configurations = activeContext.Desires;
                    for (int index = 0; index < configurations.Count; index++)
                    {
                        PegaNpcDesireConfiguration configuration = configurations[index];
                        PegaNpcDesireDefinition definition = configuration.Definition;
                        PegaNpcDesireSatisfactionSnapshot current = GetSatisfaction(definition.DesireId);
                        if (definition.CreateGoal().Evaluate(observedState) != GoapTruthValue.True ||
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
                        updated.Add(new PegaNpcDesireSatisfactionSnapshot(
                            definition.DesireId,
                            entry.Satisfaction,
                            entry.CompletedExecutions));
                    }
                }
            }

            updated.Sort((left, right) => string.CompareOrdinal(left.DesireId, right.DesireId));
            PegaNpcDesireSatisfactionUpdate result = new PegaNpcDesireSatisfactionUpdate(updated);
            _log.Debug(
                "Completed NPC Action evaluated against active Goal conditions.",
                LogFields.Field("actorRoot", _actorRootName),
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

        private PegaNpcDesireSatisfactionSnapshot GetSatisfaction(string desireId)
        {
            if (_entries.TryGetValue(desireId, out Entry entry))
            {
                return new PegaNpcDesireSatisfactionSnapshot(
                    desireId,
                    entry.Satisfaction,
                    entry.CompletedExecutions);
            }

            return new PegaNpcDesireSatisfactionSnapshot(desireId, 0f, 0);
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

            _log = PegaLogging.CreateScopedLogger<PegaNpcDesireState>(
                LogCategory,
                _actorRootName);
            _loggerInitialized = true;
        }

        private static string JoinDesireIds(
            IReadOnlyList<PegaNpcDesireSatisfactionSnapshot> values)
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

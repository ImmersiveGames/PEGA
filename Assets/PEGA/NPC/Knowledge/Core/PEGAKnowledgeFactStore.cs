using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Immersive.Foundation.Events;
using PEGA.NPC.Knowledge.Definitions;
namespace PEGA.NPC.Knowledge.Core
{
    public enum PegaKnowledgeStatus
    {
        Unknown,
        Known,
        Invalidated
    }

    public enum PegaKnowledgeScope
    {
        Individual = 0,
        Group = 10,
        Global = 20
    }

    [Flags]
    public enum PegaKnowledgeInvalidationCause
    {
        None = 0,
        Explicit = 1 << 0,
        ConflictingEvidence = 1 << 1,
        PolicyValidity = 1 << 2
    }

    public enum PegaKnowledgeWriteResult
    {
        Recorded,
        Replaced,
        Merged,
        IgnoredStale,
        RejectedInvalid,
        InvalidatedConflict,
        InvalidatedExplicitly,
        NoChange
    }

    /// <summary>Opaque, typed evidence origin shared by facts from every knowledge scope.</summary>
    public readonly struct PegaKnowledgeEvidenceOrigin : IEquatable<PegaKnowledgeEvidenceOrigin>
    {
        private readonly Type _originType;
        private readonly object _origin;

        private PegaKnowledgeEvidenceOrigin(Type originType, object origin)
        {
            _originType = originType;
            _origin = origin;
        }

        public bool IsValid => _originType != null;

        public static PegaKnowledgeEvidenceOrigin From<TOrigin>(TOrigin origin)
        {
            if (origin is null)
            {
                throw new ArgumentNullException(nameof(origin));
            }

            return new PegaKnowledgeEvidenceOrigin(typeof(TOrigin), origin);
        }

        public bool Is<TOrigin>(TOrigin origin) =>
            IsValid && _originType == typeof(TOrigin) && Equals(_origin, origin);

        public bool Equals(PegaKnowledgeEvidenceOrigin other) =>
            _originType == other._originType && Equals(_origin, other._origin);

        public override bool Equals(object obj) =>
            obj is PegaKnowledgeEvidenceOrigin other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_originType != null ? _originType.GetHashCode() : 0) * 397) ^
                    (_origin != null ? _origin.GetHashCode() : 0);
            }
        }

        public override string ToString() => _origin != null ? _origin.ToString() : "Unknown";
    }

    /// <summary>One evidence item; source and payload retain typed domain evidence without a closed source enum.</summary>
    public readonly struct PegaKnowledgeEvidence
    {
        public PegaKnowledgeEvidence(
            PegaKnowledgeEvidenceOrigin origin,
            double observationTimeSeconds,
            object source = null,
            object payload = null)
        {
            Origin = origin;
            ObservationTimeSeconds = observationTimeSeconds;
            Source = source;
            Payload = payload;
        }

        public PegaKnowledgeEvidenceOrigin Origin { get; }

        public double ObservationTimeSeconds { get; }

        public object Source { get; }

        public object Payload { get; }

        public bool IsValid => Origin.IsValid &&
            !double.IsNaN(ObservationTimeSeconds) &&
            !double.IsInfinity(ObservationTimeSeconds) &&
            ObservationTimeSeconds >= 0d;

        public static PegaKnowledgeEvidence From<TOrigin>(
            TOrigin origin,
            double observationTimeSeconds,
            object source = null,
            object payload = null) =>
            new PegaKnowledgeEvidence(
                PegaKnowledgeEvidenceOrigin.From(origin),
                observationTimeSeconds,
                source,
                payload);
    }

    /// <summary>A value and its shared metadata, independent of the authority that owns it.</summary>
    public readonly struct PegaKnowledgeFact<TValue>
    {
        private readonly TValue _value;
        private readonly TValue _candidate;
        private readonly bool _hasCandidate;
        private readonly IReadOnlyList<PegaKnowledgeEvidence> _evidence;
        private readonly IReadOnlyList<PegaKnowledgeEvidenceOrigin> _origins;

        internal PegaKnowledgeFact(
            PegaKnowledgeStatus status,
            bool hasObservationTime,
            double observationTimeSeconds,
            IReadOnlyList<PegaKnowledgeEvidence> evidence,
            PegaKnowledgeInvalidationCause invalidationCauses,
            TValue value,
            TValue candidate,
            bool hasCandidate,
            PegaKnowledgeScope scope)
        {
            Status = status;
            HasObservationTime = hasObservationTime;
            ObservationTimeSeconds = observationTimeSeconds;
            _evidence = evidence ?? Array.Empty<PegaKnowledgeEvidence>();
            InvalidationCauses = invalidationCauses;
            _value = value;
            _candidate = candidate;
            _hasCandidate = hasCandidate;
            Scope = scope;

            var origins = new List<PegaKnowledgeEvidenceOrigin>();
            for (int index = 0; index < _evidence.Count; index++)
            {
                PegaKnowledgeEvidenceOrigin origin = _evidence[index].Origin;
                if (!origins.Contains(origin))
                {
                    origins.Add(origin);
                }
            }

            _origins = new ReadOnlyCollection<PegaKnowledgeEvidenceOrigin>(origins);
        }

        public PegaKnowledgeStatus Status { get; }

        public bool HasValue => Status == PegaKnowledgeStatus.Known;

        public bool HasObservationTime { get; }

        public double ObservationTimeSeconds { get; }

        public IReadOnlyList<PegaKnowledgeEvidence> Evidence =>
            _evidence ?? Array.Empty<PegaKnowledgeEvidence>();

        public IReadOnlyList<PegaKnowledgeEvidenceOrigin> Origins =>
            _origins ?? Array.Empty<PegaKnowledgeEvidenceOrigin>();

        public PegaKnowledgeInvalidationCause InvalidationCauses { get; }

        public PegaKnowledgeScope Scope { get; }

        public bool HasOrigin<TOrigin>(TOrigin origin)
        {
            for (int index = 0; index < Origins.Count; index++)
            {
                if (Origins[index].Is(origin))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetValue(out TValue value)
        {
            if (Status == PegaKnowledgeStatus.Known)
            {
                value = _value;
                return true;
            }

            value = default;
            return false;
        }

        internal bool HasCandidate => _hasCandidate;

        internal TValue Candidate => _candidate;

        internal static PegaKnowledgeFact<TValue> Unknown(PegaKnowledgeScope scope) =>
            new PegaKnowledgeFact<TValue>(
                PegaKnowledgeStatus.Unknown,
                false,
                0d,
                Array.Empty<PegaKnowledgeEvidence>(),
                PegaKnowledgeInvalidationCause.None,
                default,
                default,
                false,
                scope);

        internal static PegaKnowledgeFact<TValue> Create(
            PegaKnowledgeStatus status,
            bool hasObservationTime,
            double observationTimeSeconds,
            IReadOnlyList<PegaKnowledgeEvidence> evidence,
            PegaKnowledgeInvalidationCause invalidationCauses,
            TValue value,
            TValue candidate,
            bool hasCandidate,
            PegaKnowledgeScope scope) =>
            new PegaKnowledgeFact<TValue>(
                status,
                hasObservationTime,
                observationTimeSeconds,
                evidence,
                invalidationCauses,
                value,
                candidate,
                hasCandidate,
                scope);
    }

    public readonly struct PegaKnowledgeFactUpdate<TValue>
    {
        public PegaKnowledgeFactUpdate(
            PegaKnowledgeWriteResult result,
            PegaKnowledgeFact<TValue> fact,
            bool hasValueOrStatusChanged)
        {
            Result = result;
            Fact = fact;
            HasValueOrStatusChanged = hasValueOrStatusChanged;
        }

        public PegaKnowledgeWriteResult Result { get; }

        public PegaKnowledgeFact<TValue> Fact { get; }

        public bool HasValueOrStatusChanged { get; }
    }

    /// <summary>Per-data policy for validation, recency, resolution, invalidation and query validity.</summary>
    public interface IPegaKnowledgeFactPolicy<TValue>
    {
        PegaKnowledgeFactUpdate<TValue> Observe(
            PegaKnowledgeFact<TValue> current,
            TValue value,
            PegaKnowledgeEvidence evidence,
            PegaKnowledgeScope scope);

        PegaKnowledgeFactUpdate<TValue> Invalidate(
            PegaKnowledgeFact<TValue> current,
            PegaKnowledgeEvidence evidence,
            PegaKnowledgeScope scope);

        PegaKnowledgeFact<TValue> EvaluateValidity(
            PegaKnowledgeFact<TValue> current,
            double queryTimeSeconds,
            PegaKnowledgeScope scope);
    }

    /// <summary>Reusable latest-evidence policy matching the current Individual recency and conflict rules.</summary>
    public sealed class PegaLatestEvidenceKnowledgePolicy<TValue> : IPegaKnowledgeFactPolicy<TValue>
    {
        private readonly Func<TValue, TValue, bool> _areEqual;
        private readonly Func<TValue, string> _validateValue;
        private readonly Func<PegaKnowledgeFact<TValue>, double, bool> _isValidAt;

        public PegaLatestEvidenceKnowledgePolicy(
            Func<TValue, TValue, bool> areEqual = null,
            Func<TValue, string> validateValue = null,
            Func<PegaKnowledgeFact<TValue>, double, bool> isValidAt = null)
        {
            _areEqual = areEqual ?? EqualityComparer<TValue>.Default.Equals;
            _validateValue = validateValue;
            _isValidAt = isValidAt;
        }

        public PegaKnowledgeFactUpdate<TValue> Observe(
            PegaKnowledgeFact<TValue> current,
            TValue value,
            PegaKnowledgeEvidence evidence,
            PegaKnowledgeScope scope)
        {
            if (_validateValue != null && _validateValue(value) is string diagnostic &&
                !string.IsNullOrWhiteSpace(diagnostic))
            {
                return new PegaKnowledgeFactUpdate<TValue>(
                    PegaKnowledgeWriteResult.RejectedInvalid,
                    current,
                    false);
            }

            if (current.HasObservationTime && evidence.ObservationTimeSeconds < current.ObservationTimeSeconds)
            {
                return new PegaKnowledgeFactUpdate<TValue>(PegaKnowledgeWriteResult.IgnoredStale, current, false);
            }

            if (!current.HasObservationTime || evidence.ObservationTimeSeconds > current.ObservationTimeSeconds)
            {
                PegaKnowledgeFact<TValue> next = CreateKnown(value, evidence, scope);
                return new PegaKnowledgeFactUpdate<TValue>(
                    current.HasObservationTime ? PegaKnowledgeWriteResult.Replaced : PegaKnowledgeWriteResult.Recorded,
                    next,
                    !AreValuesEqual(current, next));
            }

            List<PegaKnowledgeEvidence> mergedEvidence = new List<PegaKnowledgeEvidence>(current.Evidence);
            bool evidenceChanged = AddEvidence(mergedEvidence, evidence);
            bool hasCandidate = current.HasCandidate;
            TValue candidate = current.Candidate;
            PegaKnowledgeStatus status = current.Status;
            PegaKnowledgeInvalidationCause causes = current.InvalidationCauses;
            TValue knownValue = current.HasValue && current.TryGetValue(out TValue existingValue)
                ? existingValue
                : default;

            if (!hasCandidate)
            {
                candidate = value;
                hasCandidate = true;
                evidenceChanged = true;
            }
            else if (!_areEqual(candidate, value))
            {
                bool causeChanged = (causes & PegaKnowledgeInvalidationCause.ConflictingEvidence) == 0;
                causes |= PegaKnowledgeInvalidationCause.ConflictingEvidence;
                status = PegaKnowledgeStatus.Invalidated;
                knownValue = default;
                var conflicting = Create(
                    status,
                    current,
                    mergedEvidence,
                    causes,
                    knownValue,
                    candidate,
                    hasCandidate,
                    scope);
                return new PegaKnowledgeFactUpdate<TValue>(
                    causeChanged
                        ? PegaKnowledgeWriteResult.InvalidatedConflict
                        : evidenceChanged
                            ? PegaKnowledgeWriteResult.Merged
                            : PegaKnowledgeWriteResult.NoChange,
                    conflicting,
                    current.Status != conflicting.Status || AreValuesEqual(current, conflicting) == false);
            }

            PegaKnowledgeFact<TValue> resultFact = Create(
                status,
                current,
                mergedEvidence,
                causes,
                knownValue,
                candidate,
                hasCandidate,
                scope);
            return new PegaKnowledgeFactUpdate<TValue>(
                evidenceChanged ? PegaKnowledgeWriteResult.Merged : PegaKnowledgeWriteResult.NoChange,
                resultFact,
                !AreValuesEqual(current, resultFact));
        }

        public PegaKnowledgeFactUpdate<TValue> Invalidate(
            PegaKnowledgeFact<TValue> current,
            PegaKnowledgeEvidence evidence,
            PegaKnowledgeScope scope)
        {
            if (current.HasObservationTime && evidence.ObservationTimeSeconds < current.ObservationTimeSeconds)
            {
                return new PegaKnowledgeFactUpdate<TValue>(PegaKnowledgeWriteResult.IgnoredStale, current, false);
            }

            if (!current.HasObservationTime || evidence.ObservationTimeSeconds > current.ObservationTimeSeconds)
            {
                PegaKnowledgeFact<TValue> invalidated = PegaKnowledgeFact<TValue>.Create(
                    PegaKnowledgeStatus.Invalidated,
                    true,
                    evidence.ObservationTimeSeconds,
                    new[] { evidence },
                    PegaKnowledgeInvalidationCause.Explicit,
                    default,
                    default,
                    false,
                    scope);
                return new PegaKnowledgeFactUpdate<TValue>(
                    PegaKnowledgeWriteResult.InvalidatedExplicitly,
                    invalidated,
                    current.Status != invalidated.Status || current.HasValue);
            }

            List<PegaKnowledgeEvidence> mergedEvidence = new List<PegaKnowledgeEvidence>(current.Evidence);
            bool evidenceChanged = AddEvidence(mergedEvidence, evidence);
            bool statusChanged = current.Status != PegaKnowledgeStatus.Invalidated;
            bool causeChanged = (current.InvalidationCauses & PegaKnowledgeInvalidationCause.Explicit) == 0;
            PegaKnowledgeFact<TValue> next = Create(
                PegaKnowledgeStatus.Invalidated,
                current,
                mergedEvidence,
                current.InvalidationCauses | PegaKnowledgeInvalidationCause.Explicit,
                default,
                current.Candidate,
                current.HasCandidate,
                scope);
            return new PegaKnowledgeFactUpdate<TValue>(
                statusChanged || causeChanged
                    ? PegaKnowledgeWriteResult.InvalidatedExplicitly
                    : evidenceChanged
                        ? PegaKnowledgeWriteResult.Merged
                        : PegaKnowledgeWriteResult.NoChange,
                next,
                statusChanged || current.HasValue);
        }

        public PegaKnowledgeFact<TValue> EvaluateValidity(
            PegaKnowledgeFact<TValue> current,
            double queryTimeSeconds,
            PegaKnowledgeScope scope)
        {
            if (!current.HasValue || _isValidAt == null || _isValidAt(current, queryTimeSeconds))
            {
                return current;
            }

            return PegaKnowledgeFact<TValue>.Create(
                PegaKnowledgeStatus.Invalidated,
                current.HasObservationTime,
                current.ObservationTimeSeconds,
                current.Evidence,
                current.InvalidationCauses | PegaKnowledgeInvalidationCause.PolicyValidity,
                default,
                current.Candidate,
                current.HasCandidate,
                scope);
        }

        private PegaKnowledgeFact<TValue> CreateKnown(
            TValue value,
            PegaKnowledgeEvidence evidence,
            PegaKnowledgeScope scope) =>
            PegaKnowledgeFact<TValue>.Create(
                PegaKnowledgeStatus.Known,
                true,
                evidence.ObservationTimeSeconds,
                new[] { evidence },
                PegaKnowledgeInvalidationCause.None,
                value,
                value,
                true,
                scope);

        private static PegaKnowledgeFact<TValue> Create(
            PegaKnowledgeStatus status,
            PegaKnowledgeFact<TValue> current,
            IReadOnlyList<PegaKnowledgeEvidence> evidence,
            PegaKnowledgeInvalidationCause causes,
            TValue value,
            TValue candidate,
            bool hasCandidate,
            PegaKnowledgeScope scope) =>
            PegaKnowledgeFact<TValue>.Create(
                status,
                current.HasObservationTime,
                current.ObservationTimeSeconds,
                evidence,
                causes,
                value,
                candidate,
                hasCandidate,
                scope);

        private static bool AddEvidence(List<PegaKnowledgeEvidence> evidence, PegaKnowledgeEvidence candidate)
        {
            for (int index = 0; index < evidence.Count; index++)
            {
                PegaKnowledgeEvidence current = evidence[index];
                if (current.Origin.Equals(candidate.Origin) &&
                    ReferenceEquals(current.Source, candidate.Source) &&
                    Equals(current.Payload, candidate.Payload))
                {
                    return false;
                }
            }

            evidence.Add(candidate);
            return true;
        }

        private bool AreValuesEqual(
            PegaKnowledgeFact<TValue> left,
            PegaKnowledgeFact<TValue> right)
        {
            if (left.Status != right.Status)
            {
                return false;
            }

            return !left.HasValue ||
                left.TryGetValue(out TValue leftValue) &&
                right.TryGetValue(out TValue rightValue) &&
                _areEqual(leftValue, rightValue);
        }
    }

    /// <summary>First-accepted policy for structural or authored facts that do not change during their owner lifetime.</summary>
    public sealed class PegaStructuralKnowledgePolicy<TValue> : IPegaKnowledgeFactPolicy<TValue>
    {
        private readonly Func<TValue, string> _validateValue;
        private readonly Func<TValue, TValue, bool> _areEqual;

        public PegaStructuralKnowledgePolicy(
            Func<TValue, string> validateValue = null,
            Func<TValue, TValue, bool> areEqual = null)
        {
            _validateValue = validateValue;
            _areEqual = areEqual ?? EqualityComparer<TValue>.Default.Equals;
        }

        public PegaKnowledgeFactUpdate<TValue> Observe(
            PegaKnowledgeFact<TValue> current,
            TValue value,
            PegaKnowledgeEvidence evidence,
            PegaKnowledgeScope scope)
        {
            if (_validateValue != null && !string.IsNullOrWhiteSpace(_validateValue(value)))
            {
                return new PegaKnowledgeFactUpdate<TValue>(PegaKnowledgeWriteResult.RejectedInvalid, current, false);
            }

            if (current.HasObservationTime && evidence.ObservationTimeSeconds < current.ObservationTimeSeconds)
            {
                return new PegaKnowledgeFactUpdate<TValue>(PegaKnowledgeWriteResult.IgnoredStale, current, false);
            }

            if (!current.HasValue && current.HasObservationTime &&
                evidence.ObservationTimeSeconds == current.ObservationTimeSeconds)
            {
                List<PegaKnowledgeEvidence> invalidatedEvidence = new List<PegaKnowledgeEvidence>(current.Evidence);
                bool isNewEvidence = true;
                for (int index = 0; index < invalidatedEvidence.Count; index++)
                {
                    if (invalidatedEvidence[index].Origin.Equals(evidence.Origin) &&
                        ReferenceEquals(invalidatedEvidence[index].Source, evidence.Source) &&
                        Equals(invalidatedEvidence[index].Payload, evidence.Payload) &&
                        invalidatedEvidence[index].ObservationTimeSeconds == evidence.ObservationTimeSeconds)
                    {
                        isNewEvidence = false;
                        break;
                    }
                }

                if (isNewEvidence)
                {
                    invalidatedEvidence.Add(evidence);
                }

                PegaKnowledgeFact<TValue> stillInvalidated = PegaKnowledgeFact<TValue>.Create(
                    PegaKnowledgeStatus.Invalidated,
                    true,
                    current.ObservationTimeSeconds,
                    invalidatedEvidence,
                    current.InvalidationCauses,
                    default,
                    current.Candidate,
                    current.HasCandidate,
                    scope);
                return new PegaKnowledgeFactUpdate<TValue>(
                    isNewEvidence ? PegaKnowledgeWriteResult.Merged : PegaKnowledgeWriteResult.NoChange,
                    stillInvalidated,
                    false);
            }

            if (!current.HasValue)
            {
                PegaKnowledgeFact<TValue> recorded = PegaKnowledgeFact<TValue>.Create(
                    PegaKnowledgeStatus.Known,
                    true,
                    evidence.ObservationTimeSeconds,
                    new[] { evidence },
                    PegaKnowledgeInvalidationCause.None,
                    value,
                    value,
                    true,
                    scope);
                return new PegaKnowledgeFactUpdate<TValue>(
                    current.HasObservationTime ? PegaKnowledgeWriteResult.Replaced : PegaKnowledgeWriteResult.Recorded,
                    recorded,
                    true);
            }

            List<PegaKnowledgeEvidence> merged = new List<PegaKnowledgeEvidence>(current.Evidence);
            for (int index = 0; index < merged.Count; index++)
            {
                if (merged[index].Origin.Equals(evidence.Origin) &&
                    ReferenceEquals(merged[index].Source, evidence.Source) &&
                    Equals(merged[index].Payload, evidence.Payload) &&
                    merged[index].ObservationTimeSeconds == evidence.ObservationTimeSeconds)
                {
                    return new PegaKnowledgeFactUpdate<TValue>(PegaKnowledgeWriteResult.NoChange, current, false);
                }
            }

            merged.Add(evidence);
            if (!current.TryGetValue(out TValue existingValue) || !_areEqual(existingValue, value))
            {
                PegaKnowledgeFact<TValue> conflict = PegaKnowledgeFact<TValue>.Create(
                    PegaKnowledgeStatus.Invalidated,
                    true,
                    evidence.ObservationTimeSeconds,
                    merged,
                    current.InvalidationCauses | PegaKnowledgeInvalidationCause.ConflictingEvidence,
                    default,
                    current.HasCandidate ? current.Candidate : existingValue,
                    current.HasCandidate || current.HasValue,
                    scope);
                return new PegaKnowledgeFactUpdate<TValue>(
                    PegaKnowledgeWriteResult.InvalidatedConflict,
                    conflict,
                    true);
            }

            PegaKnowledgeFact<TValue> updated = PegaKnowledgeFact<TValue>.Create(
                current.Status,
                true,
                evidence.ObservationTimeSeconds,
                merged,
                current.InvalidationCauses,
                current.TryGetValue(out TValue knownValue) ? knownValue : default,
                current.Candidate,
                current.HasCandidate,
                scope);
            return new PegaKnowledgeFactUpdate<TValue>(
                PegaKnowledgeWriteResult.Merged,
                updated,
                false);
        }

        public PegaKnowledgeFactUpdate<TValue> Invalidate(
            PegaKnowledgeFact<TValue> current,
            PegaKnowledgeEvidence evidence,
            PegaKnowledgeScope scope)
        {
            if (current.HasObservationTime && evidence.ObservationTimeSeconds < current.ObservationTimeSeconds)
            {
                return new PegaKnowledgeFactUpdate<TValue>(PegaKnowledgeWriteResult.IgnoredStale, current, false);
            }

            List<PegaKnowledgeEvidence> mergedEvidence = new List<PegaKnowledgeEvidence>(current.Evidence);
            bool evidenceChanged = true;
            for (int index = 0; index < mergedEvidence.Count; index++)
            {
                if (mergedEvidence[index].Origin.Equals(evidence.Origin) &&
                    ReferenceEquals(mergedEvidence[index].Source, evidence.Source) &&
                    Equals(mergedEvidence[index].Payload, evidence.Payload) &&
                    mergedEvidence[index].ObservationTimeSeconds == evidence.ObservationTimeSeconds)
                {
                    evidenceChanged = false;
                    break;
                }
            }

            if (evidenceChanged)
            {
                mergedEvidence.Add(evidence);
            }

            bool statusChanged = current.Status != PegaKnowledgeStatus.Invalidated;
            bool causeChanged = (current.InvalidationCauses & PegaKnowledgeInvalidationCause.Explicit) == 0;
            PegaKnowledgeFact<TValue> invalidated = PegaKnowledgeFact<TValue>.Create(
                PegaKnowledgeStatus.Invalidated,
                true,
                evidence.ObservationTimeSeconds,
                mergedEvidence,
                current.InvalidationCauses | PegaKnowledgeInvalidationCause.Explicit,
                default,
                current.Candidate,
                current.HasCandidate,
                scope);
            return new PegaKnowledgeFactUpdate<TValue>(
                statusChanged || causeChanged
                    ? PegaKnowledgeWriteResult.InvalidatedExplicitly
                    : evidenceChanged
                        ? PegaKnowledgeWriteResult.Merged
                        : PegaKnowledgeWriteResult.NoChange,
                invalidated,
                statusChanged || current.HasValue);
        }

        public PegaKnowledgeFact<TValue> EvaluateValidity(
            PegaKnowledgeFact<TValue> current,
            double queryTimeSeconds,
            PegaKnowledgeScope scope) => current;
    }

    public sealed class PegaKnowledgeFactChangedEvent : IEvent
    {
        internal PegaKnowledgeFactChangedEvent(
            IPegaKnowledgeFactKey key,
            PegaKnowledgeScope scope,
            PegaKnowledgeWriteResult result,
            PegaKnowledgeStatus previousStatus,
            PegaKnowledgeStatus currentStatus,
            bool hasValueOrStatusChanged,
            PegaKnowledgeEvidence evidence)
        {
            FactKey = key;
            Scope = scope;
            Result = result;
            PreviousStatus = previousStatus;
            CurrentStatus = currentStatus;
            HasValueOrStatusChanged = hasValueOrStatusChanged;
            Evidence = evidence;
        }

        public IPegaKnowledgeFactKey FactKey { get; }

        public PegaKnowledgeScope Scope { get; }

        public PegaKnowledgeWriteResult Result { get; }

        public PegaKnowledgeStatus PreviousStatus { get; }

        public PegaKnowledgeStatus CurrentStatus { get; }

        public bool HasValueOrStatusChanged { get; }

        public PegaKnowledgeEvidence Evidence { get; }

        public bool IsFor<TAddress, TValue>(PegaKnowledgeFactKey<TAddress, TValue> key) =>
            FactKey != null && FactKey.Equals(key);
    }

    /// <summary>Scope-local generic storage. Scope authorities own instances and enforce their own access/lifecycle.</summary>
    public sealed class PegaKnowledgeFactStore
    {
        private readonly Dictionary<IPegaKnowledgeFactKey, IPegaKnowledgeFactEntry> _facts =
            new Dictionary<IPegaKnowledgeFactKey, IPegaKnowledgeFactEntry>();

        public PegaKnowledgeFactStore(PegaKnowledgeScope scope)
        {
            if (scope != PegaKnowledgeScope.Individual &&
                scope != PegaKnowledgeScope.Group &&
                scope != PegaKnowledgeScope.Global)
            {
                throw new ArgumentOutOfRangeException(nameof(scope));
            }

            Scope = scope;
        }

        public PegaKnowledgeScope Scope { get; }

        internal event Action<PegaKnowledgeFactChangedEvent> Changed;

        public PegaKnowledgeFact<TValue> Get<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds)
        {
            ValidateQueryTime(queryTimeSeconds);
            if (!key.IsValid)
            {
                throw new ArgumentException("A valid fact definition and address are required.", nameof(key));
            }

            if (!_facts.TryGetValue(key, out IPegaKnowledgeFactEntry entry))
            {
                return PegaKnowledgeFact<TValue>.Unknown(Scope);
            }

            if (!(entry is PegaKnowledgeFactEntry<TAddress, TValue> typedEntry))
            {
                throw new InvalidOperationException("A knowledge fact key resolved to an incompatible stored value type.");
            }

            return key.Definition.Policy.EvaluateValidity(typedEntry.Fact, queryTimeSeconds, Scope);
        }

        public PegaKnowledgeWriteResult Observe<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PegaKnowledgeEvidence evidence)
        {
            if (!key.IsValid || !evidence.IsValid)
            {
                return PegaKnowledgeWriteResult.RejectedInvalid;
            }

            IPegaKnowledgeFactEntry entry = _facts.TryGetValue(key, out IPegaKnowledgeFactEntry found)
                ? found
                : null;
            if (entry != null && !(entry is PegaKnowledgeFactEntry<TAddress, TValue>))
            {
                return PegaKnowledgeWriteResult.RejectedInvalid;
            }

            PegaKnowledgeFact<TValue> previous = entry != null
                ? ((PegaKnowledgeFactEntry<TAddress, TValue>)entry).Fact
                : PegaKnowledgeFact<TValue>.Unknown(Scope);
            PegaKnowledgeFactUpdate<TValue> update = key.Definition.Policy.Observe(
                previous,
                value,
                evidence,
                Scope);
            Commit(key, entry, update, evidence);
            return update.Result;
        }

        public PegaKnowledgeWriteResult Invalidate<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            PegaKnowledgeEvidence evidence)
        {
            if (!key.IsValid || !evidence.IsValid)
            {
                return PegaKnowledgeWriteResult.RejectedInvalid;
            }

            IPegaKnowledgeFactEntry entry = _facts.TryGetValue(key, out IPegaKnowledgeFactEntry found)
                ? found
                : null;
            if (entry != null && !(entry is PegaKnowledgeFactEntry<TAddress, TValue>))
            {
                return PegaKnowledgeWriteResult.RejectedInvalid;
            }

            PegaKnowledgeFact<TValue> previous = entry != null
                ? ((PegaKnowledgeFactEntry<TAddress, TValue>)entry).Fact
                : PegaKnowledgeFact<TValue>.Unknown(Scope);
            PegaKnowledgeFactUpdate<TValue> update = key.Definition.Policy.Invalidate(
                previous,
                evidence,
                Scope);
            Commit(key, entry, update, evidence);
            return update.Result;
        }

        public IReadOnlyList<PegaKnowledgeFactSnapshot<TAddress, TValue>> GetFacts<TAddress, TValue>(
            PegaKnowledgeFactDefinition<TAddress, TValue> definition,
            double queryTimeSeconds)
        {
            ValidateQueryTime(queryTimeSeconds);
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var result = new List<PegaKnowledgeFactSnapshot<TAddress, TValue>>();
            foreach (KeyValuePair<IPegaKnowledgeFactKey, IPegaKnowledgeFactEntry> entry in _facts)
            {
                if (!(entry.Key is PegaKnowledgeFactKey<TAddress, TValue> key) ||
                    !ReferenceEquals(key.Definition, definition) ||
                    !(entry.Value is PegaKnowledgeFactEntry<TAddress, TValue> typedEntry))
                {
                    continue;
                }

                result.Add(new PegaKnowledgeFactSnapshot<TAddress, TValue>(
                    key,
                    definition.Policy.EvaluateValidity(typedEntry.Fact, queryTimeSeconds, Scope)));
            }

            return new ReadOnlyCollection<PegaKnowledgeFactSnapshot<TAddress, TValue>>(result);
        }

        public IReadOnlyList<IPegaKnowledgeFactKey> GetFactKeys()
        {
            var keys = new List<IPegaKnowledgeFactKey>(_facts.Keys);
            return new ReadOnlyCollection<IPegaKnowledgeFactKey>(keys);
        }

        public void Clear() => _facts.Clear();

        private static void ValidateQueryTime(double queryTimeSeconds)
        {
            if (double.IsNaN(queryTimeSeconds) || double.IsInfinity(queryTimeSeconds) || queryTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(queryTimeSeconds));
            }
        }

        private void Commit<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            IPegaKnowledgeFactEntry existing,
            PegaKnowledgeFactUpdate<TValue> update,
            PegaKnowledgeEvidence evidence)
        {
            if (!IsCommitted(update.Result))
            {
                return;
            }

            PegaKnowledgeStatus previousStatus = existing != null
                ? ((PegaKnowledgeFactEntry<TAddress, TValue>)existing).Fact.Status
                : PegaKnowledgeStatus.Unknown;

            PegaKnowledgeFactEntry<TAddress, TValue> entry =
                existing as PegaKnowledgeFactEntry<TAddress, TValue>;
            if (entry == null)
            {
                entry = new PegaKnowledgeFactEntry<TAddress, TValue>(key, update.Fact);
                _facts.Add(key, entry);
            }
            else
            {
                entry.Set(update.Fact);
            }

            Changed?.Invoke(new PegaKnowledgeFactChangedEvent(
                key,
                Scope,
                update.Result,
                previousStatus,
                update.Fact.Status,
                update.HasValueOrStatusChanged,
                evidence));
        }

        private static bool IsCommitted(PegaKnowledgeWriteResult result) =>
            result == PegaKnowledgeWriteResult.Recorded ||
            result == PegaKnowledgeWriteResult.Replaced ||
            result == PegaKnowledgeWriteResult.Merged ||
            result == PegaKnowledgeWriteResult.InvalidatedConflict ||
            result == PegaKnowledgeWriteResult.InvalidatedExplicitly;

        private interface IPegaKnowledgeFactEntry
        {
        }

        private sealed class PegaKnowledgeFactEntry<TAddress, TValue> : IPegaKnowledgeFactEntry
        {
            public PegaKnowledgeFactEntry(
                PegaKnowledgeFactKey<TAddress, TValue> key,
                PegaKnowledgeFact<TValue> fact)
            {
                Key = key;
                Fact = fact;
            }

            public PegaKnowledgeFactKey<TAddress, TValue> Key { get; }

            public PegaKnowledgeFact<TValue> Fact { get; private set; }

            public void Set(PegaKnowledgeFact<TValue> fact) => Fact = fact;
        }
    }

    public readonly struct PegaKnowledgeFactSnapshot<TAddress, TValue>
    {
        public PegaKnowledgeFactSnapshot(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            PegaKnowledgeFact<TValue> fact)
        {
            Key = key;
            Fact = fact;
        }

        public PegaKnowledgeFactKey<TAddress, TValue> Key { get; }

        public PegaKnowledgeFact<TValue> Fact { get; }
    }
}

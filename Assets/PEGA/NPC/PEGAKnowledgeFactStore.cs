using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Immersive.Foundation.Events;

namespace PEGA.NPC
{
    public enum PEGAKnowledgeStatus
    {
        Unknown,
        Known,
        Invalidated
    }

    public enum PEGAKnowledgeScope
    {
        Individual = 0,
        Group = 10,
        Global = 20
    }

    [Flags]
    public enum PEGAKnowledgeInvalidationCause
    {
        None = 0,
        Explicit = 1 << 0,
        ConflictingEvidence = 1 << 1,
        PolicyValidity = 1 << 2
    }

    public enum PEGAKnowledgeWriteResult
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
    public readonly struct PEGAKnowledgeEvidenceOrigin : IEquatable<PEGAKnowledgeEvidenceOrigin>
    {
        private readonly Type _originType;
        private readonly object _origin;

        private PEGAKnowledgeEvidenceOrigin(Type originType, object origin)
        {
            _originType = originType;
            _origin = origin;
        }

        public bool IsValid => _originType != null;

        public static PEGAKnowledgeEvidenceOrigin From<TOrigin>(TOrigin origin)
        {
            if (origin is null)
            {
                throw new ArgumentNullException(nameof(origin));
            }

            return new PEGAKnowledgeEvidenceOrigin(typeof(TOrigin), origin);
        }

        public bool Is<TOrigin>(TOrigin origin) =>
            IsValid && _originType == typeof(TOrigin) && Equals(_origin, origin);

        public bool Equals(PEGAKnowledgeEvidenceOrigin other) =>
            _originType == other._originType && Equals(_origin, other._origin);

        public override bool Equals(object obj) =>
            obj is PEGAKnowledgeEvidenceOrigin other && Equals(other);

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
    public readonly struct PEGAKnowledgeEvidence
    {
        public PEGAKnowledgeEvidence(
            PEGAKnowledgeEvidenceOrigin origin,
            double observationTimeSeconds,
            object source = null,
            object payload = null)
        {
            Origin = origin;
            ObservationTimeSeconds = observationTimeSeconds;
            Source = source;
            Payload = payload;
        }

        public PEGAKnowledgeEvidenceOrigin Origin { get; }

        public double ObservationTimeSeconds { get; }

        public object Source { get; }

        public object Payload { get; }

        public bool IsValid => Origin.IsValid &&
            !double.IsNaN(ObservationTimeSeconds) &&
            !double.IsInfinity(ObservationTimeSeconds) &&
            ObservationTimeSeconds >= 0d;

        public static PEGAKnowledgeEvidence From<TOrigin>(
            TOrigin origin,
            double observationTimeSeconds,
            object source = null,
            object payload = null) =>
            new PEGAKnowledgeEvidence(
                PEGAKnowledgeEvidenceOrigin.From(origin),
                observationTimeSeconds,
                source,
                payload);
    }

    /// <summary>A value and its shared metadata, independent of the authority that owns it.</summary>
    public readonly struct PEGAKnowledgeFact<TValue>
    {
        private readonly TValue _value;
        private readonly TValue _candidate;
        private readonly bool _hasCandidate;
        private readonly IReadOnlyList<PEGAKnowledgeEvidence> _evidence;
        private readonly IReadOnlyList<PEGAKnowledgeEvidenceOrigin> _origins;

        internal PEGAKnowledgeFact(
            PEGAKnowledgeStatus status,
            bool hasObservationTime,
            double observationTimeSeconds,
            IReadOnlyList<PEGAKnowledgeEvidence> evidence,
            PEGAKnowledgeInvalidationCause invalidationCauses,
            TValue value,
            TValue candidate,
            bool hasCandidate,
            PEGAKnowledgeScope scope)
        {
            Status = status;
            HasObservationTime = hasObservationTime;
            ObservationTimeSeconds = observationTimeSeconds;
            _evidence = evidence ?? Array.Empty<PEGAKnowledgeEvidence>();
            InvalidationCauses = invalidationCauses;
            _value = value;
            _candidate = candidate;
            _hasCandidate = hasCandidate;
            Scope = scope;

            var origins = new List<PEGAKnowledgeEvidenceOrigin>();
            for (int index = 0; index < _evidence.Count; index++)
            {
                PEGAKnowledgeEvidenceOrigin origin = _evidence[index].Origin;
                if (!origins.Contains(origin))
                {
                    origins.Add(origin);
                }
            }

            _origins = new ReadOnlyCollection<PEGAKnowledgeEvidenceOrigin>(origins);
        }

        public PEGAKnowledgeStatus Status { get; }

        public bool HasValue => Status == PEGAKnowledgeStatus.Known;

        public bool HasObservationTime { get; }

        public double ObservationTimeSeconds { get; }

        public IReadOnlyList<PEGAKnowledgeEvidence> Evidence =>
            _evidence ?? Array.Empty<PEGAKnowledgeEvidence>();

        public IReadOnlyList<PEGAKnowledgeEvidenceOrigin> Origins =>
            _origins ?? Array.Empty<PEGAKnowledgeEvidenceOrigin>();

        public PEGAKnowledgeInvalidationCause InvalidationCauses { get; }

        public PEGAKnowledgeScope Scope { get; }

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
            if (Status == PEGAKnowledgeStatus.Known)
            {
                value = _value;
                return true;
            }

            value = default;
            return false;
        }

        internal bool HasCandidate => _hasCandidate;

        internal TValue Candidate => _candidate;

        internal static PEGAKnowledgeFact<TValue> Unknown(PEGAKnowledgeScope scope) =>
            new PEGAKnowledgeFact<TValue>(
                PEGAKnowledgeStatus.Unknown,
                false,
                0d,
                Array.Empty<PEGAKnowledgeEvidence>(),
                PEGAKnowledgeInvalidationCause.None,
                default,
                default,
                false,
                scope);

        internal static PEGAKnowledgeFact<TValue> Create(
            PEGAKnowledgeStatus status,
            bool hasObservationTime,
            double observationTimeSeconds,
            IReadOnlyList<PEGAKnowledgeEvidence> evidence,
            PEGAKnowledgeInvalidationCause invalidationCauses,
            TValue value,
            TValue candidate,
            bool hasCandidate,
            PEGAKnowledgeScope scope) =>
            new PEGAKnowledgeFact<TValue>(
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

    public interface IPEGAKnowledgeFactKey : IEquatable<IPEGAKnowledgeFactKey>
    {
        object DefinitionIdentity { get; }

        object Address { get; }

        string Name { get; }

        Type AddressType { get; }

        Type ValueType { get; }

        bool IsValid { get; }
    }

    /// <summary>
    /// Stable definition of one fact data and its policy. Reuse the same definition instance for every producer,
    /// reader and writer of that fact; its address is supplied as a typed value by the domain owner.
    /// </summary>
    public sealed class PEGAKnowledgeFactDefinition<TAddress, TValue>
    {
        private readonly Func<TAddress, bool> _isValidAddress;

        public PEGAKnowledgeFactDefinition(
            string name,
            IPEGAKnowledgeFactPolicy<TValue> policy,
            Func<TAddress, bool> isValidAddress)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A fact definition name is required.", nameof(name));
            }

            Name = name.Trim();
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _isValidAddress = isValidAddress ?? throw new ArgumentNullException(nameof(isValidAddress));
        }

        public string Name { get; }

        public IPEGAKnowledgeFactPolicy<TValue> Policy { get; }

        public PEGAKnowledgeFactKey<TAddress, TValue> For(TAddress address) =>
            new PEGAKnowledgeFactKey<TAddress, TValue>(this, address);

        internal bool IsValidAddress(TAddress address) => _isValidAddress(address);
    }

    /// <summary>Typed address for one value; equality combines its stable data definition and domain identity.</summary>
    public readonly struct PEGAKnowledgeFactKey<TAddress, TValue> :
        IPEGAKnowledgeFactKey,
        IEquatable<PEGAKnowledgeFactKey<TAddress, TValue>>
    {
        internal PEGAKnowledgeFactKey(PEGAKnowledgeFactDefinition<TAddress, TValue> definition, TAddress address)
        {
            Definition = definition;
            AddressValue = address;
        }

        public PEGAKnowledgeFactDefinition<TAddress, TValue> Definition { get; }

        public TAddress AddressValue { get; }

        object IPEGAKnowledgeFactKey.DefinitionIdentity => Definition;

        object IPEGAKnowledgeFactKey.Address => AddressValue;

        public string Name => Definition != null ? Definition.Name : string.Empty;

        public Type AddressType => typeof(TAddress);

        public Type ValueType => typeof(TValue);

        public bool IsValid => Definition != null && Definition.IsValidAddress(AddressValue);

        public bool Equals(PEGAKnowledgeFactKey<TAddress, TValue> other) =>
            ReferenceEquals(Definition, other.Definition) &&
            EqualityComparer<TAddress>.Default.Equals(AddressValue, other.AddressValue);

        public bool Equals(IPEGAKnowledgeFactKey other) =>
            other is PEGAKnowledgeFactKey<TAddress, TValue> typed && Equals(typed);

        public override bool Equals(object obj) =>
            obj is PEGAKnowledgeFactKey<TAddress, TValue> typed && Equals(typed);

        public override int GetHashCode()
        {
            unchecked
            {
                int definitionHash = Definition != null
                    ? RuntimeHelpers.GetHashCode(Definition)
                    : 0;
                return (definitionHash * 397) ^
                    (AddressValue is null ? 0 : EqualityComparer<TAddress>.Default.GetHashCode(AddressValue));
            }
        }

        public override string ToString() => Definition == null
            ? "InvalidKnowledgeFactKey"
            : string.Concat(Definition.Name, "[", AddressValue, "]");
    }

    public readonly struct PEGAKnowledgeFactUpdate<TValue>
    {
        public PEGAKnowledgeFactUpdate(
            PEGAKnowledgeWriteResult result,
            PEGAKnowledgeFact<TValue> fact,
            bool hasValueOrStatusChanged)
        {
            Result = result;
            Fact = fact;
            HasValueOrStatusChanged = hasValueOrStatusChanged;
        }

        public PEGAKnowledgeWriteResult Result { get; }

        public PEGAKnowledgeFact<TValue> Fact { get; }

        public bool HasValueOrStatusChanged { get; }
    }

    /// <summary>Per-data policy for validation, recency, resolution, invalidation and query validity.</summary>
    public interface IPEGAKnowledgeFactPolicy<TValue>
    {
        PEGAKnowledgeFactUpdate<TValue> Observe(
            PEGAKnowledgeFact<TValue> current,
            TValue value,
            PEGAKnowledgeEvidence evidence,
            PEGAKnowledgeScope scope);

        PEGAKnowledgeFactUpdate<TValue> Invalidate(
            PEGAKnowledgeFact<TValue> current,
            PEGAKnowledgeEvidence evidence,
            PEGAKnowledgeScope scope);

        PEGAKnowledgeFact<TValue> EvaluateValidity(
            PEGAKnowledgeFact<TValue> current,
            double queryTimeSeconds,
            PEGAKnowledgeScope scope);
    }

    /// <summary>Reusable latest-evidence policy matching the current Individual recency and conflict rules.</summary>
    public sealed class PEGALatestEvidenceKnowledgePolicy<TValue> : IPEGAKnowledgeFactPolicy<TValue>
    {
        private readonly Func<TValue, TValue, bool> _areEqual;
        private readonly Func<TValue, string> _validateValue;
        private readonly Func<PEGAKnowledgeFact<TValue>, double, bool> _isValidAt;

        public PEGALatestEvidenceKnowledgePolicy(
            Func<TValue, TValue, bool> areEqual = null,
            Func<TValue, string> validateValue = null,
            Func<PEGAKnowledgeFact<TValue>, double, bool> isValidAt = null)
        {
            _areEqual = areEqual ?? EqualityComparer<TValue>.Default.Equals;
            _validateValue = validateValue;
            _isValidAt = isValidAt;
        }

        public PEGAKnowledgeFactUpdate<TValue> Observe(
            PEGAKnowledgeFact<TValue> current,
            TValue value,
            PEGAKnowledgeEvidence evidence,
            PEGAKnowledgeScope scope)
        {
            if (_validateValue != null && _validateValue(value) is string diagnostic &&
                !string.IsNullOrWhiteSpace(diagnostic))
            {
                return new PEGAKnowledgeFactUpdate<TValue>(
                    PEGAKnowledgeWriteResult.RejectedInvalid,
                    current,
                    false);
            }

            if (current.HasObservationTime && evidence.ObservationTimeSeconds < current.ObservationTimeSeconds)
            {
                return new PEGAKnowledgeFactUpdate<TValue>(PEGAKnowledgeWriteResult.IgnoredStale, current, false);
            }

            if (!current.HasObservationTime || evidence.ObservationTimeSeconds > current.ObservationTimeSeconds)
            {
                PEGAKnowledgeFact<TValue> next = CreateKnown(value, evidence, scope);
                return new PEGAKnowledgeFactUpdate<TValue>(
                    current.HasObservationTime ? PEGAKnowledgeWriteResult.Replaced : PEGAKnowledgeWriteResult.Recorded,
                    next,
                    !AreValuesEqual(current, next));
            }

            List<PEGAKnowledgeEvidence> mergedEvidence = new List<PEGAKnowledgeEvidence>(current.Evidence);
            bool evidenceChanged = AddEvidence(mergedEvidence, evidence);
            bool hasCandidate = current.HasCandidate;
            TValue candidate = current.Candidate;
            PEGAKnowledgeStatus status = current.Status;
            PEGAKnowledgeInvalidationCause causes = current.InvalidationCauses;
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
                bool causeChanged = (causes & PEGAKnowledgeInvalidationCause.ConflictingEvidence) == 0;
                causes |= PEGAKnowledgeInvalidationCause.ConflictingEvidence;
                status = PEGAKnowledgeStatus.Invalidated;
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
                return new PEGAKnowledgeFactUpdate<TValue>(
                    causeChanged
                        ? PEGAKnowledgeWriteResult.InvalidatedConflict
                        : evidenceChanged
                            ? PEGAKnowledgeWriteResult.Merged
                            : PEGAKnowledgeWriteResult.NoChange,
                    conflicting,
                    current.Status != conflicting.Status || AreValuesEqual(current, conflicting) == false);
            }

            PEGAKnowledgeFact<TValue> resultFact = Create(
                status,
                current,
                mergedEvidence,
                causes,
                knownValue,
                candidate,
                hasCandidate,
                scope);
            return new PEGAKnowledgeFactUpdate<TValue>(
                evidenceChanged ? PEGAKnowledgeWriteResult.Merged : PEGAKnowledgeWriteResult.NoChange,
                resultFact,
                !AreValuesEqual(current, resultFact));
        }

        public PEGAKnowledgeFactUpdate<TValue> Invalidate(
            PEGAKnowledgeFact<TValue> current,
            PEGAKnowledgeEvidence evidence,
            PEGAKnowledgeScope scope)
        {
            if (current.HasObservationTime && evidence.ObservationTimeSeconds < current.ObservationTimeSeconds)
            {
                return new PEGAKnowledgeFactUpdate<TValue>(PEGAKnowledgeWriteResult.IgnoredStale, current, false);
            }

            if (!current.HasObservationTime || evidence.ObservationTimeSeconds > current.ObservationTimeSeconds)
            {
                PEGAKnowledgeFact<TValue> invalidated = PEGAKnowledgeFact<TValue>.Create(
                    PEGAKnowledgeStatus.Invalidated,
                    true,
                    evidence.ObservationTimeSeconds,
                    new[] { evidence },
                    PEGAKnowledgeInvalidationCause.Explicit,
                    default,
                    default,
                    false,
                    scope);
                return new PEGAKnowledgeFactUpdate<TValue>(
                    PEGAKnowledgeWriteResult.InvalidatedExplicitly,
                    invalidated,
                    current.Status != invalidated.Status || current.HasValue);
            }

            List<PEGAKnowledgeEvidence> mergedEvidence = new List<PEGAKnowledgeEvidence>(current.Evidence);
            bool evidenceChanged = AddEvidence(mergedEvidence, evidence);
            bool statusChanged = current.Status != PEGAKnowledgeStatus.Invalidated;
            bool causeChanged = (current.InvalidationCauses & PEGAKnowledgeInvalidationCause.Explicit) == 0;
            PEGAKnowledgeFact<TValue> next = Create(
                PEGAKnowledgeStatus.Invalidated,
                current,
                mergedEvidence,
                current.InvalidationCauses | PEGAKnowledgeInvalidationCause.Explicit,
                default,
                current.Candidate,
                current.HasCandidate,
                scope);
            return new PEGAKnowledgeFactUpdate<TValue>(
                statusChanged || causeChanged
                    ? PEGAKnowledgeWriteResult.InvalidatedExplicitly
                    : evidenceChanged
                        ? PEGAKnowledgeWriteResult.Merged
                        : PEGAKnowledgeWriteResult.NoChange,
                next,
                statusChanged || current.HasValue);
        }

        public PEGAKnowledgeFact<TValue> EvaluateValidity(
            PEGAKnowledgeFact<TValue> current,
            double queryTimeSeconds,
            PEGAKnowledgeScope scope)
        {
            if (!current.HasValue || _isValidAt == null || _isValidAt(current, queryTimeSeconds))
            {
                return current;
            }

            return PEGAKnowledgeFact<TValue>.Create(
                PEGAKnowledgeStatus.Invalidated,
                current.HasObservationTime,
                current.ObservationTimeSeconds,
                current.Evidence,
                current.InvalidationCauses | PEGAKnowledgeInvalidationCause.PolicyValidity,
                default,
                current.Candidate,
                current.HasCandidate,
                scope);
        }

        private PEGAKnowledgeFact<TValue> CreateKnown(
            TValue value,
            PEGAKnowledgeEvidence evidence,
            PEGAKnowledgeScope scope) =>
            PEGAKnowledgeFact<TValue>.Create(
                PEGAKnowledgeStatus.Known,
                true,
                evidence.ObservationTimeSeconds,
                new[] { evidence },
                PEGAKnowledgeInvalidationCause.None,
                value,
                value,
                true,
                scope);

        private static PEGAKnowledgeFact<TValue> Create(
            PEGAKnowledgeStatus status,
            PEGAKnowledgeFact<TValue> current,
            IReadOnlyList<PEGAKnowledgeEvidence> evidence,
            PEGAKnowledgeInvalidationCause causes,
            TValue value,
            TValue candidate,
            bool hasCandidate,
            PEGAKnowledgeScope scope) =>
            PEGAKnowledgeFact<TValue>.Create(
                status,
                current.HasObservationTime,
                current.ObservationTimeSeconds,
                evidence,
                causes,
                value,
                candidate,
                hasCandidate,
                scope);

        private static bool AddEvidence(List<PEGAKnowledgeEvidence> evidence, PEGAKnowledgeEvidence candidate)
        {
            for (int index = 0; index < evidence.Count; index++)
            {
                PEGAKnowledgeEvidence current = evidence[index];
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
            PEGAKnowledgeFact<TValue> left,
            PEGAKnowledgeFact<TValue> right)
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
    public sealed class PEGAStructuralKnowledgePolicy<TValue> : IPEGAKnowledgeFactPolicy<TValue>
    {
        private readonly Func<TValue, string> _validateValue;
        private readonly Func<TValue, TValue, bool> _areEqual;

        public PEGAStructuralKnowledgePolicy(
            Func<TValue, string> validateValue = null,
            Func<TValue, TValue, bool> areEqual = null)
        {
            _validateValue = validateValue;
            _areEqual = areEqual ?? EqualityComparer<TValue>.Default.Equals;
        }

        public PEGAKnowledgeFactUpdate<TValue> Observe(
            PEGAKnowledgeFact<TValue> current,
            TValue value,
            PEGAKnowledgeEvidence evidence,
            PEGAKnowledgeScope scope)
        {
            if (_validateValue != null && !string.IsNullOrWhiteSpace(_validateValue(value)))
            {
                return new PEGAKnowledgeFactUpdate<TValue>(PEGAKnowledgeWriteResult.RejectedInvalid, current, false);
            }

            if (current.HasObservationTime && evidence.ObservationTimeSeconds < current.ObservationTimeSeconds)
            {
                return new PEGAKnowledgeFactUpdate<TValue>(PEGAKnowledgeWriteResult.IgnoredStale, current, false);
            }

            if (!current.HasValue && current.HasObservationTime &&
                evidence.ObservationTimeSeconds == current.ObservationTimeSeconds)
            {
                List<PEGAKnowledgeEvidence> invalidatedEvidence = new List<PEGAKnowledgeEvidence>(current.Evidence);
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

                PEGAKnowledgeFact<TValue> stillInvalidated = PEGAKnowledgeFact<TValue>.Create(
                    PEGAKnowledgeStatus.Invalidated,
                    true,
                    current.ObservationTimeSeconds,
                    invalidatedEvidence,
                    current.InvalidationCauses,
                    default,
                    current.Candidate,
                    current.HasCandidate,
                    scope);
                return new PEGAKnowledgeFactUpdate<TValue>(
                    isNewEvidence ? PEGAKnowledgeWriteResult.Merged : PEGAKnowledgeWriteResult.NoChange,
                    stillInvalidated,
                    false);
            }

            if (!current.HasValue)
            {
                PEGAKnowledgeFact<TValue> recorded = PEGAKnowledgeFact<TValue>.Create(
                    PEGAKnowledgeStatus.Known,
                    true,
                    evidence.ObservationTimeSeconds,
                    new[] { evidence },
                    PEGAKnowledgeInvalidationCause.None,
                    value,
                    value,
                    true,
                    scope);
                return new PEGAKnowledgeFactUpdate<TValue>(
                    current.HasObservationTime ? PEGAKnowledgeWriteResult.Replaced : PEGAKnowledgeWriteResult.Recorded,
                    recorded,
                    true);
            }

            List<PEGAKnowledgeEvidence> merged = new List<PEGAKnowledgeEvidence>(current.Evidence);
            for (int index = 0; index < merged.Count; index++)
            {
                if (merged[index].Origin.Equals(evidence.Origin) &&
                    ReferenceEquals(merged[index].Source, evidence.Source) &&
                    Equals(merged[index].Payload, evidence.Payload) &&
                    merged[index].ObservationTimeSeconds == evidence.ObservationTimeSeconds)
                {
                    return new PEGAKnowledgeFactUpdate<TValue>(PEGAKnowledgeWriteResult.NoChange, current, false);
                }
            }

            merged.Add(evidence);
            if (!current.TryGetValue(out TValue existingValue) || !_areEqual(existingValue, value))
            {
                PEGAKnowledgeFact<TValue> conflict = PEGAKnowledgeFact<TValue>.Create(
                    PEGAKnowledgeStatus.Invalidated,
                    true,
                    evidence.ObservationTimeSeconds,
                    merged,
                    current.InvalidationCauses | PEGAKnowledgeInvalidationCause.ConflictingEvidence,
                    default,
                    current.HasCandidate ? current.Candidate : existingValue,
                    current.HasCandidate || current.HasValue,
                    scope);
                return new PEGAKnowledgeFactUpdate<TValue>(
                    PEGAKnowledgeWriteResult.InvalidatedConflict,
                    conflict,
                    true);
            }

            PEGAKnowledgeFact<TValue> updated = PEGAKnowledgeFact<TValue>.Create(
                current.Status,
                true,
                evidence.ObservationTimeSeconds,
                merged,
                current.InvalidationCauses,
                current.TryGetValue(out TValue knownValue) ? knownValue : default,
                current.Candidate,
                current.HasCandidate,
                scope);
            return new PEGAKnowledgeFactUpdate<TValue>(
                PEGAKnowledgeWriteResult.Merged,
                updated,
                false);
        }

        public PEGAKnowledgeFactUpdate<TValue> Invalidate(
            PEGAKnowledgeFact<TValue> current,
            PEGAKnowledgeEvidence evidence,
            PEGAKnowledgeScope scope)
        {
            if (current.HasObservationTime && evidence.ObservationTimeSeconds < current.ObservationTimeSeconds)
            {
                return new PEGAKnowledgeFactUpdate<TValue>(PEGAKnowledgeWriteResult.IgnoredStale, current, false);
            }

            List<PEGAKnowledgeEvidence> mergedEvidence = new List<PEGAKnowledgeEvidence>(current.Evidence);
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

            bool statusChanged = current.Status != PEGAKnowledgeStatus.Invalidated;
            bool causeChanged = (current.InvalidationCauses & PEGAKnowledgeInvalidationCause.Explicit) == 0;
            PEGAKnowledgeFact<TValue> invalidated = PEGAKnowledgeFact<TValue>.Create(
                PEGAKnowledgeStatus.Invalidated,
                true,
                evidence.ObservationTimeSeconds,
                mergedEvidence,
                current.InvalidationCauses | PEGAKnowledgeInvalidationCause.Explicit,
                default,
                current.Candidate,
                current.HasCandidate,
                scope);
            return new PEGAKnowledgeFactUpdate<TValue>(
                statusChanged || causeChanged
                    ? PEGAKnowledgeWriteResult.InvalidatedExplicitly
                    : evidenceChanged
                        ? PEGAKnowledgeWriteResult.Merged
                        : PEGAKnowledgeWriteResult.NoChange,
                invalidated,
                statusChanged || current.HasValue);
        }

        public PEGAKnowledgeFact<TValue> EvaluateValidity(
            PEGAKnowledgeFact<TValue> current,
            double queryTimeSeconds,
            PEGAKnowledgeScope scope) => current;
    }

    public sealed class PEGAKnowledgeFactChangedEvent : IEvent
    {
        internal PEGAKnowledgeFactChangedEvent(
            IPEGAKnowledgeFactKey key,
            PEGAKnowledgeScope scope,
            PEGAKnowledgeWriteResult result,
            PEGAKnowledgeStatus previousStatus,
            PEGAKnowledgeStatus currentStatus,
            bool hasValueOrStatusChanged,
            PEGAKnowledgeEvidence evidence)
        {
            FactKey = key;
            Scope = scope;
            Result = result;
            PreviousStatus = previousStatus;
            CurrentStatus = currentStatus;
            HasValueOrStatusChanged = hasValueOrStatusChanged;
            Evidence = evidence;
        }

        public IPEGAKnowledgeFactKey FactKey { get; }

        public PEGAKnowledgeScope Scope { get; }

        public PEGAKnowledgeWriteResult Result { get; }

        public PEGAKnowledgeStatus PreviousStatus { get; }

        public PEGAKnowledgeStatus CurrentStatus { get; }

        public bool HasValueOrStatusChanged { get; }

        public PEGAKnowledgeEvidence Evidence { get; }

        public bool IsFor<TAddress, TValue>(PEGAKnowledgeFactKey<TAddress, TValue> key) =>
            FactKey != null && FactKey.Equals(key);
    }

    /// <summary>Scope-local generic storage. Scope authorities own instances and enforce their own access/lifecycle.</summary>
    public sealed class PEGAKnowledgeFactStore
    {
        private readonly Dictionary<IPEGAKnowledgeFactKey, IPEGAKnowledgeFactEntry> _facts =
            new Dictionary<IPEGAKnowledgeFactKey, IPEGAKnowledgeFactEntry>();

        public PEGAKnowledgeFactStore(PEGAKnowledgeScope scope)
        {
            if (scope != PEGAKnowledgeScope.Individual &&
                scope != PEGAKnowledgeScope.Group &&
                scope != PEGAKnowledgeScope.Global)
            {
                throw new ArgumentOutOfRangeException(nameof(scope));
            }

            Scope = scope;
        }

        public PEGAKnowledgeScope Scope { get; }

        internal event Action<PEGAKnowledgeFactChangedEvent> Changed;

        public PEGAKnowledgeFact<TValue> Get<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds)
        {
            ValidateQueryTime(queryTimeSeconds);
            if (!key.IsValid)
            {
                throw new ArgumentException("A valid fact definition and address are required.", nameof(key));
            }

            if (!_facts.TryGetValue(key, out IPEGAKnowledgeFactEntry entry))
            {
                return PEGAKnowledgeFact<TValue>.Unknown(Scope);
            }

            if (!(entry is PEGAKnowledgeFactEntry<TAddress, TValue> typedEntry))
            {
                throw new InvalidOperationException("A knowledge fact key resolved to an incompatible stored value type.");
            }

            return key.Definition.Policy.EvaluateValidity(typedEntry.Fact, queryTimeSeconds, Scope);
        }

        public PEGAKnowledgeWriteResult Observe<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PEGAKnowledgeEvidence evidence)
        {
            if (!key.IsValid || !evidence.IsValid)
            {
                return PEGAKnowledgeWriteResult.RejectedInvalid;
            }

            IPEGAKnowledgeFactEntry entry = _facts.TryGetValue(key, out IPEGAKnowledgeFactEntry found)
                ? found
                : null;
            if (entry != null && !(entry is PEGAKnowledgeFactEntry<TAddress, TValue>))
            {
                return PEGAKnowledgeWriteResult.RejectedInvalid;
            }

            PEGAKnowledgeFact<TValue> previous = entry != null
                ? ((PEGAKnowledgeFactEntry<TAddress, TValue>)entry).Fact
                : PEGAKnowledgeFact<TValue>.Unknown(Scope);
            PEGAKnowledgeFactUpdate<TValue> update = key.Definition.Policy.Observe(
                previous,
                value,
                evidence,
                Scope);
            Commit(key, entry, update, evidence);
            return update.Result;
        }

        public PEGAKnowledgeWriteResult Invalidate<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGAKnowledgeEvidence evidence)
        {
            if (!key.IsValid || !evidence.IsValid)
            {
                return PEGAKnowledgeWriteResult.RejectedInvalid;
            }

            IPEGAKnowledgeFactEntry entry = _facts.TryGetValue(key, out IPEGAKnowledgeFactEntry found)
                ? found
                : null;
            if (entry != null && !(entry is PEGAKnowledgeFactEntry<TAddress, TValue>))
            {
                return PEGAKnowledgeWriteResult.RejectedInvalid;
            }

            PEGAKnowledgeFact<TValue> previous = entry != null
                ? ((PEGAKnowledgeFactEntry<TAddress, TValue>)entry).Fact
                : PEGAKnowledgeFact<TValue>.Unknown(Scope);
            PEGAKnowledgeFactUpdate<TValue> update = key.Definition.Policy.Invalidate(
                previous,
                evidence,
                Scope);
            Commit(key, entry, update, evidence);
            return update.Result;
        }

        public IReadOnlyList<PEGAKnowledgeFactSnapshot<TAddress, TValue>> GetFacts<TAddress, TValue>(
            PEGAKnowledgeFactDefinition<TAddress, TValue> definition,
            double queryTimeSeconds)
        {
            ValidateQueryTime(queryTimeSeconds);
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var result = new List<PEGAKnowledgeFactSnapshot<TAddress, TValue>>();
            foreach (KeyValuePair<IPEGAKnowledgeFactKey, IPEGAKnowledgeFactEntry> entry in _facts)
            {
                if (!(entry.Key is PEGAKnowledgeFactKey<TAddress, TValue> key) ||
                    !ReferenceEquals(key.Definition, definition) ||
                    !(entry.Value is PEGAKnowledgeFactEntry<TAddress, TValue> typedEntry))
                {
                    continue;
                }

                result.Add(new PEGAKnowledgeFactSnapshot<TAddress, TValue>(
                    key,
                    definition.Policy.EvaluateValidity(typedEntry.Fact, queryTimeSeconds, Scope)));
            }

            return new ReadOnlyCollection<PEGAKnowledgeFactSnapshot<TAddress, TValue>>(result);
        }

        public IReadOnlyList<IPEGAKnowledgeFactKey> GetFactKeys()
        {
            var keys = new List<IPEGAKnowledgeFactKey>(_facts.Keys);
            return new ReadOnlyCollection<IPEGAKnowledgeFactKey>(keys);
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
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            IPEGAKnowledgeFactEntry existing,
            PEGAKnowledgeFactUpdate<TValue> update,
            PEGAKnowledgeEvidence evidence)
        {
            if (!IsCommitted(update.Result))
            {
                return;
            }

            PEGAKnowledgeStatus previousStatus = existing != null
                ? ((PEGAKnowledgeFactEntry<TAddress, TValue>)existing).Fact.Status
                : PEGAKnowledgeStatus.Unknown;

            PEGAKnowledgeFactEntry<TAddress, TValue> entry =
                existing as PEGAKnowledgeFactEntry<TAddress, TValue>;
            if (entry == null)
            {
                entry = new PEGAKnowledgeFactEntry<TAddress, TValue>(key, update.Fact);
                _facts.Add(key, entry);
            }
            else
            {
                entry.Set(update.Fact);
            }

            Changed?.Invoke(new PEGAKnowledgeFactChangedEvent(
                key,
                Scope,
                update.Result,
                previousStatus,
                update.Fact.Status,
                update.HasValueOrStatusChanged,
                evidence));
        }

        private static bool IsCommitted(PEGAKnowledgeWriteResult result) =>
            result == PEGAKnowledgeWriteResult.Recorded ||
            result == PEGAKnowledgeWriteResult.Replaced ||
            result == PEGAKnowledgeWriteResult.Merged ||
            result == PEGAKnowledgeWriteResult.InvalidatedConflict ||
            result == PEGAKnowledgeWriteResult.InvalidatedExplicitly;

        private interface IPEGAKnowledgeFactEntry
        {
        }

        private sealed class PEGAKnowledgeFactEntry<TAddress, TValue> : IPEGAKnowledgeFactEntry
        {
            public PEGAKnowledgeFactEntry(
                PEGAKnowledgeFactKey<TAddress, TValue> key,
                PEGAKnowledgeFact<TValue> fact)
            {
                Key = key;
                Fact = fact;
            }

            public PEGAKnowledgeFactKey<TAddress, TValue> Key { get; }

            public PEGAKnowledgeFact<TValue> Fact { get; private set; }

            public void Set(PEGAKnowledgeFact<TValue> fact) => Fact = fact;
        }
    }

    public readonly struct PEGAKnowledgeFactSnapshot<TAddress, TValue>
    {
        public PEGAKnowledgeFactSnapshot(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGAKnowledgeFact<TValue> fact)
        {
            Key = key;
            Fact = fact;
        }

        public PEGAKnowledgeFactKey<TAddress, TValue> Key { get; }

        public PEGAKnowledgeFact<TValue> Fact { get; }
    }
}

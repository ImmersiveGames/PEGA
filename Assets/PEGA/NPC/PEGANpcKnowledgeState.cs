using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    public enum PEGANpcKnowledgeStatus
    {
        Unknown,
        Known,
        Invalidated
    }

    [Flags]
    public enum PEGANpcEvidenceOrigin
    {
        None = 0,
        Perception = 1 << 0,
        ActionResult = 1 << 1
    }

    [Flags]
    public enum PEGANpcKnowledgeInvalidationCause
    {
        None = 0,
        Explicit = 1 << 0,
        ConflictingEvidence = 1 << 1
    }

    public enum PEGANpcKnowledgeWriteResult
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

    public readonly struct PEGANpcKnowledgeFact<T>
    {
        private readonly T _value;

        internal PEGANpcKnowledgeFact(
            PEGANpcKnowledgeStatus status,
            bool hasObservationTime,
            double observationTimeSeconds,
            PEGANpcEvidenceOrigin origins,
            PEGANpcKnowledgeInvalidationCause invalidationCauses,
            T value)
        {
            Status = status;
            HasObservationTime = hasObservationTime;
            ObservationTimeSeconds = observationTimeSeconds;
            Origins = origins;
            InvalidationCauses = invalidationCauses;
            _value = value;
        }

        public PEGANpcKnowledgeStatus Status { get; }

        public bool HasValue => Status == PEGANpcKnowledgeStatus.Known;

        public bool HasObservationTime { get; }

        public double ObservationTimeSeconds { get; }

        public PEGANpcEvidenceOrigin Origins { get; }

        public PEGANpcKnowledgeInvalidationCause InvalidationCauses { get; }

        public bool TryGetValue(out T value)
        {
            if (Status == PEGANpcKnowledgeStatus.Known)
            {
                value = _value;
                return true;
            }

            value = default;
            return false;
        }

        internal static PEGANpcKnowledgeFact<T> Unknown => new PEGANpcKnowledgeFact<T>(
            PEGANpcKnowledgeStatus.Unknown,
            false,
            0d,
            PEGANpcEvidenceOrigin.None,
            PEGANpcKnowledgeInvalidationCause.None,
            default);
    }

    [DisallowMultipleComponent]
    public sealed class PEGANpcKnowledgeState : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.Knowledge";

        [NonSerialized]
        private KnowledgeFactCell<bool> _perceivedAreaPresence;

        [NonSerialized]
        private Dictionary<ActorId, SubjectKnowledge> _knowledgeBySubject;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        private void Awake()
        {
            EnsureStateInitialized();
            EnsureLoggerInitialized();
        }

        public PEGANpcKnowledgeFact<bool> GetPerceivedAreaPresence()
        {
            EnsureStateInitialized();
            return _perceivedAreaPresence.Snapshot;
        }

        public PEGANpcKnowledgeFact<ActorProfileId> GetObservedCharacterIdentity(ActorId subjectActorId)
        {
            ValidateSubjectForQuery(subjectActorId);
            EnsureStateInitialized();
            return _knowledgeBySubject.TryGetValue(subjectActorId, out SubjectKnowledge subject)
                ? subject.CharacterIdentity.Snapshot
                : PEGANpcKnowledgeFact<ActorProfileId>.Unknown;
        }

        public PEGANpcKnowledgeFact<bool> GetRickVisualContact(ActorId rickSubjectActorId)
        {
            ValidateSubjectForQuery(rickSubjectActorId);
            EnsureStateInitialized();
            return _knowledgeBySubject.TryGetValue(rickSubjectActorId, out SubjectKnowledge subject)
                ? subject.RickVisualContact.Snapshot
                : PEGANpcKnowledgeFact<bool>.Unknown;
        }

        public PEGANpcKnowledgeFact<Vector3> GetLastObservedRickPosition(ActorId rickSubjectActorId)
        {
            ValidateSubjectForQuery(rickSubjectActorId);
            EnsureStateInitialized();
            return _knowledgeBySubject.TryGetValue(rickSubjectActorId, out SubjectKnowledge subject)
                ? subject.LastObservedRickPosition.Snapshot
                : PEGANpcKnowledgeFact<Vector3>.Unknown;
        }

        public PEGANpcKnowledgeWriteResult ObservePerceivedAreaPresence(
            bool isPresent,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            EnsureStateInitialized();
            return Observe(
                "perceivedAreaPresence",
                string.Empty,
                _perceivedAreaPresence,
                isPresent,
                AreEqual,
                origin,
                observationTimeSeconds);
        }

        public PEGANpcKnowledgeWriteResult ObserveCharacterIdentity(
            ActorId subjectActorId,
            ActorProfileId observedProfileId,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                return RejectEvidence(
                    "characterIdentity",
                    "invalid",
                    origin,
                    observationTimeSeconds,
                    "subject ActorId is invalid",
                    PEGANpcKnowledgeFact<ActorProfileId>.Unknown);
            }

            if (!observedProfileId.IsValid)
            {
                SubjectKnowledge existingSubject = FindSubject(subjectActorId);
                return RejectEvidence(
                    "characterIdentity",
                    subjectActorId.StableText,
                    origin,
                    observationTimeSeconds,
                    "observed ActorProfileId is invalid",
                    existingSubject != null
                        ? existingSubject.CharacterIdentity.Snapshot
                        : PEGANpcKnowledgeFact<ActorProfileId>.Unknown);
            }

            EnsureStateInitialized();
            SubjectKnowledge subject = GetOrCreateSubject(subjectActorId);
            return Observe(
                "characterIdentity",
                subjectActorId.StableText,
                subject.CharacterIdentity,
                observedProfileId,
                AreEqual,
                origin,
                observationTimeSeconds);
        }

        public PEGANpcKnowledgeWriteResult ObserveRickVisualContact(
            ActorId rickSubjectActorId,
            bool hasVisualContact,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!rickSubjectActorId.IsValid)
            {
                return RejectEvidence(
                    "rickVisualContact",
                    "invalid",
                    origin,
                    observationTimeSeconds,
                    "subject ActorId is invalid",
                    PEGANpcKnowledgeFact<bool>.Unknown);
            }

            EnsureStateInitialized();
            SubjectKnowledge subject = GetOrCreateSubject(rickSubjectActorId);
            return Observe(
                "rickVisualContact",
                rickSubjectActorId.StableText,
                subject.RickVisualContact,
                hasVisualContact,
                AreEqual,
                origin,
                observationTimeSeconds);
        }

        public PEGANpcKnowledgeWriteResult ObserveRickPosition(
            ActorId rickSubjectActorId,
            Vector3 observedPosition,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!rickSubjectActorId.IsValid)
            {
                return RejectEvidence(
                    "lastObservedRickPosition",
                    "invalid",
                    origin,
                    observationTimeSeconds,
                    "subject ActorId is invalid",
                    PEGANpcKnowledgeFact<Vector3>.Unknown);
            }

            if (!IsFinite(observedPosition))
            {
                SubjectKnowledge existingSubject = FindSubject(rickSubjectActorId);
                return RejectEvidence(
                    "lastObservedRickPosition",
                    rickSubjectActorId.StableText,
                    origin,
                    observationTimeSeconds,
                    "position contains a non-finite coordinate",
                    existingSubject != null
                        ? existingSubject.LastObservedRickPosition.Snapshot
                        : PEGANpcKnowledgeFact<Vector3>.Unknown);
            }

            EnsureStateInitialized();
            SubjectKnowledge subject = GetOrCreateSubject(rickSubjectActorId);
            return Observe(
                "lastObservedRickPosition",
                rickSubjectActorId.StableText,
                subject.LastObservedRickPosition,
                observedPosition,
                AreExactlyEqual,
                origin,
                observationTimeSeconds);
        }

        public PEGANpcKnowledgeWriteResult InvalidatePerceivedAreaPresence(
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            EnsureStateInitialized();
            return Invalidate("perceivedAreaPresence", string.Empty, _perceivedAreaPresence, origin, observationTimeSeconds);
        }

        public PEGANpcKnowledgeWriteResult InvalidateObservedCharacterIdentity(
            ActorId subjectActorId,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                return RejectEvidence(
                    "characterIdentity",
                    "invalid",
                    origin,
                    observationTimeSeconds,
                    "subject ActorId is invalid",
                    PEGANpcKnowledgeFact<ActorProfileId>.Unknown);
            }

            EnsureStateInitialized();
            SubjectKnowledge subject = GetOrCreateSubject(subjectActorId);
            return Invalidate(
                "characterIdentity",
                subjectActorId.StableText,
                subject.CharacterIdentity,
                origin,
                observationTimeSeconds);
        }

        public PEGANpcKnowledgeWriteResult InvalidateRickVisualContact(
            ActorId rickSubjectActorId,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!rickSubjectActorId.IsValid)
            {
                return RejectEvidence(
                    "rickVisualContact",
                    "invalid",
                    origin,
                    observationTimeSeconds,
                    "subject ActorId is invalid",
                    PEGANpcKnowledgeFact<bool>.Unknown);
            }

            EnsureStateInitialized();
            SubjectKnowledge subject = GetOrCreateSubject(rickSubjectActorId);
            return Invalidate(
                "rickVisualContact",
                rickSubjectActorId.StableText,
                subject.RickVisualContact,
                origin,
                observationTimeSeconds);
        }

        public PEGANpcKnowledgeWriteResult InvalidateLastObservedRickPosition(
            ActorId rickSubjectActorId,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!rickSubjectActorId.IsValid)
            {
                return RejectEvidence(
                    "lastObservedRickPosition",
                    "invalid",
                    origin,
                    observationTimeSeconds,
                    "subject ActorId is invalid",
                    PEGANpcKnowledgeFact<Vector3>.Unknown);
            }

            EnsureStateInitialized();
            SubjectKnowledge subject = GetOrCreateSubject(rickSubjectActorId);
            return Invalidate(
                "lastObservedRickPosition",
                rickSubjectActorId.StableText,
                subject.LastObservedRickPosition,
                origin,
                observationTimeSeconds);
        }

        private PEGANpcKnowledgeWriteResult Observe<T>(
            string factName,
            string subjectActorId,
            KnowledgeFactCell<T> fact,
            T value,
            Func<T, T, bool> areEqual,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                return RejectEvidence(factName, subjectActorId, origin, observationTimeSeconds, reason, fact.Snapshot);
            }

            PEGANpcKnowledgeFact<T> previous = fact.Snapshot;
            PEGANpcKnowledgeWriteResult result = fact.Observe(
                value,
                origin,
                observationTimeSeconds,
                areEqual,
                out PEGANpcKnowledgeFact<T> current);
            LogWriteResult(factName, subjectActorId, result, origin, observationTimeSeconds, previous, current);
            return result;
        }

        private PEGANpcKnowledgeWriteResult Invalidate<T>(
            string factName,
            string subjectActorId,
            KnowledgeFactCell<T> fact,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                return RejectEvidence(factName, subjectActorId, origin, observationTimeSeconds, reason, fact.Snapshot);
            }

            PEGANpcKnowledgeFact<T> previous = fact.Snapshot;
            PEGANpcKnowledgeWriteResult result = fact.Invalidate(
                origin,
                observationTimeSeconds,
                out PEGANpcKnowledgeFact<T> current);
            LogWriteResult(factName, subjectActorId, result, origin, observationTimeSeconds, previous, current);
            return result;
        }

        private PEGANpcKnowledgeWriteResult RejectEvidence<T>(
            string factName,
            string subjectActorId,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds,
            string reason,
            PEGANpcKnowledgeFact<T> current)
        {
            EnsureLoggerInitialized();
            _log.Warning(
                "NPC knowledge evidence was rejected.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("fact", factName),
                LogFields.Field("subjectActorId", subjectActorId),
                LogFields.Field("previousStatus", current.Status),
                LogFields.Field("newStatus", current.Status),
                LogFields.Field("evidenceOrigin", origin),
                LogFields.Field("storedOrigins", current.Origins),
                LogFields.Field("observationTimeSeconds", observationTimeSeconds),
                LogFields.Field("storedObservationTimeSeconds", current.HasObservationTime
                    ? (object)current.ObservationTimeSeconds
                    : "unavailable"),
                LogFields.Field("invalidationCauses", current.InvalidationCauses),
                LogFields.Field("reason", reason));
            return PEGANpcKnowledgeWriteResult.RejectedInvalid;
        }

        private void LogWriteResult<T>(
            string factName,
            string subjectActorId,
            PEGANpcKnowledgeWriteResult result,
            PEGANpcEvidenceOrigin evidenceOrigin,
            double evidenceTimeSeconds,
            PEGANpcKnowledgeFact<T> previous,
            PEGANpcKnowledgeFact<T> current)
        {
            if (result == PEGANpcKnowledgeWriteResult.NoChange)
            {
                return;
            }

            EnsureLoggerInitialized();
            object previousTime = previous.HasObservationTime
                ? (object)previous.ObservationTimeSeconds
                : "unavailable";

            if (result == PEGANpcKnowledgeWriteResult.IgnoredStale)
            {
                _log.Warning(
                    "NPC knowledge evidence is older than the stored fact and was ignored.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("fact", factName),
                    LogFields.Field("subjectActorId", subjectActorId),
                    LogFields.Field("previousStatus", previous.Status),
                    LogFields.Field("newStatus", current.Status),
                    LogFields.Field("evidenceOrigin", evidenceOrigin),
                    LogFields.Field("storedOrigins", current.Origins),
                    LogFields.Field("observationTimeSeconds", evidenceTimeSeconds),
                    LogFields.Field("storedObservationTimeSeconds", current.ObservationTimeSeconds),
                    LogFields.Field("previousObservationTimeSeconds", previousTime),
                    LogFields.Field("invalidationCauses", current.InvalidationCauses));
                return;
            }

            if (result == PEGANpcKnowledgeWriteResult.InvalidatedConflict ||
                result == PEGANpcKnowledgeWriteResult.InvalidatedExplicitly)
            {
                _log.Warning(
                    "NPC knowledge fact was invalidated.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("fact", factName),
                    LogFields.Field("subjectActorId", subjectActorId),
                    LogFields.Field("previousStatus", previous.Status),
                    LogFields.Field("newStatus", current.Status),
                    LogFields.Field("origins", current.Origins),
                    LogFields.Field("observationTimeSeconds", evidenceTimeSeconds),
                    LogFields.Field("invalidationCauses", current.InvalidationCauses));
                return;
            }

            _log.Info(
                "NPC knowledge fact was updated.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("fact", factName),
                LogFields.Field("subjectActorId", subjectActorId),
                LogFields.Field("result", result),
                LogFields.Field("previousStatus", previous.Status),
                LogFields.Field("newStatus", current.Status),
                LogFields.Field("origins", current.Origins),
                LogFields.Field("observationTimeSeconds", evidenceTimeSeconds),
                LogFields.Field("storedObservationTimeSeconds", current.ObservationTimeSeconds),
                LogFields.Field("invalidationCauses", current.InvalidationCauses));
        }

        private SubjectKnowledge GetOrCreateSubject(ActorId subjectActorId)
        {
            if (_knowledgeBySubject.TryGetValue(subjectActorId, out SubjectKnowledge subject))
            {
                return subject;
            }

            subject = new SubjectKnowledge();
            _knowledgeBySubject.Add(subjectActorId, subject);
            return subject;
        }

        private SubjectKnowledge FindSubject(ActorId subjectActorId)
        {
            return _knowledgeBySubject != null && _knowledgeBySubject.TryGetValue(subjectActorId, out SubjectKnowledge subject)
                ? subject
                : null;
        }

        private void EnsureStateInitialized()
        {
            if (_perceivedAreaPresence == null)
            {
                _perceivedAreaPresence = new KnowledgeFactCell<bool>();
            }

            if (_knowledgeBySubject == null)
            {
                _knowledgeBySubject = new Dictionary<ActorId, SubjectKnowledge>();
            }
        }

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PEGALogging.CreateScopedLogger<PEGANpcKnowledgeState>(LogCategory, gameObject.name);
            _loggerInitialized = true;
        }

        private static void ValidateSubjectForQuery(ActorId subjectActorId)
        {
            if (!subjectActorId.IsValid)
            {
                throw new ArgumentException("A valid subject ActorId is required.", nameof(subjectActorId));
            }
        }

        private static bool TryValidateEvidence(
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds,
            out string reason)
        {
            if (origin != PEGANpcEvidenceOrigin.Perception && origin != PEGANpcEvidenceOrigin.ActionResult)
            {
                reason = "origin must identify exactly one supported evidence source";
                return false;
            }

            if (double.IsNaN(observationTimeSeconds) ||
                double.IsInfinity(observationTimeSeconds) ||
                observationTimeSeconds < 0d)
            {
                reason = "observation time must be finite and non-negative";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool AreEqual(bool left, bool right) => left == right;

        private static bool AreEqual(ActorProfileId left, ActorProfileId right) => left.Equals(right);

        private static bool AreExactlyEqual(Vector3 left, Vector3 right) =>
            left.x == right.x && left.y == right.y && left.z == right.z;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class SubjectKnowledge
        {
            public readonly KnowledgeFactCell<ActorProfileId> CharacterIdentity =
                new KnowledgeFactCell<ActorProfileId>();

            public readonly KnowledgeFactCell<bool> RickVisualContact =
                new KnowledgeFactCell<bool>();

            public readonly KnowledgeFactCell<Vector3> LastObservedRickPosition =
                new KnowledgeFactCell<Vector3>();
        }

        private sealed class KnowledgeFactCell<T>
        {
            private PEGANpcKnowledgeStatus _status;
            private bool _hasObservationTime;
            private double _observationTimeSeconds;
            private PEGANpcEvidenceOrigin _origins;
            private PEGANpcKnowledgeInvalidationCause _invalidationCauses;
            private bool _hasCandidate;
            private T _candidate;
            private T _knownValue;

            public PEGANpcKnowledgeFact<T> Snapshot => new PEGANpcKnowledgeFact<T>(
                _status,
                _hasObservationTime,
                _observationTimeSeconds,
                _origins,
                _invalidationCauses,
                _knownValue);

            public PEGANpcKnowledgeWriteResult Observe(
                T value,
                PEGANpcEvidenceOrigin origin,
                double observationTimeSeconds,
                Func<T, T, bool> areEqual,
                out PEGANpcKnowledgeFact<T> current)
            {
                if (_hasObservationTime && observationTimeSeconds < _observationTimeSeconds)
                {
                    current = Snapshot;
                    return PEGANpcKnowledgeWriteResult.IgnoredStale;
                }

                if (!_hasObservationTime || observationTimeSeconds > _observationTimeSeconds)
                {
                    bool hadObservation = _hasObservationTime;
                    SetKnown(value, origin, observationTimeSeconds);
                    current = Snapshot;
                    return hadObservation
                        ? PEGANpcKnowledgeWriteResult.Replaced
                        : PEGANpcKnowledgeWriteResult.Recorded;
                }

                bool metadataChanged = AddOrigin(origin);
                if (!_hasCandidate)
                {
                    _candidate = value;
                    _hasCandidate = true;
                    metadataChanged = true;
                }
                else if (!areEqual(_candidate, value))
                {
                    bool causeChanged = (_invalidationCauses & PEGANpcKnowledgeInvalidationCause.ConflictingEvidence) == 0;
                    _invalidationCauses |= PEGANpcKnowledgeInvalidationCause.ConflictingEvidence;
                    _status = PEGANpcKnowledgeStatus.Invalidated;
                    _knownValue = default;
                    current = Snapshot;
                    return causeChanged
                        ? PEGANpcKnowledgeWriteResult.InvalidatedConflict
                        : metadataChanged
                            ? PEGANpcKnowledgeWriteResult.Merged
                            : PEGANpcKnowledgeWriteResult.NoChange;
                }

                if (_status == PEGANpcKnowledgeStatus.Invalidated)
                {
                    current = Snapshot;
                    return metadataChanged
                        ? PEGANpcKnowledgeWriteResult.Merged
                        : PEGANpcKnowledgeWriteResult.NoChange;
                }

                current = Snapshot;
                return metadataChanged
                    ? PEGANpcKnowledgeWriteResult.Merged
                    : PEGANpcKnowledgeWriteResult.NoChange;
            }

            public PEGANpcKnowledgeWriteResult Invalidate(
                PEGANpcEvidenceOrigin origin,
                double observationTimeSeconds,
                out PEGANpcKnowledgeFact<T> current)
            {
                if (_hasObservationTime && observationTimeSeconds < _observationTimeSeconds)
                {
                    current = Snapshot;
                    return PEGANpcKnowledgeWriteResult.IgnoredStale;
                }

                if (!_hasObservationTime || observationTimeSeconds > _observationTimeSeconds)
                {
                    _status = PEGANpcKnowledgeStatus.Invalidated;
                    _hasObservationTime = true;
                    _observationTimeSeconds = observationTimeSeconds;
                    _origins = origin;
                    _invalidationCauses = PEGANpcKnowledgeInvalidationCause.Explicit;
                    _hasCandidate = false;
                    _candidate = default;
                    _knownValue = default;
                    current = Snapshot;
                    return PEGANpcKnowledgeWriteResult.InvalidatedExplicitly;
                }

                bool metadataChanged = AddOrigin(origin);
                bool statusChanged = _status != PEGANpcKnowledgeStatus.Invalidated;
                bool causeChanged = (_invalidationCauses & PEGANpcKnowledgeInvalidationCause.Explicit) == 0;
                _status = PEGANpcKnowledgeStatus.Invalidated;
                _invalidationCauses |= PEGANpcKnowledgeInvalidationCause.Explicit;
                _knownValue = default;
                current = Snapshot;
                return statusChanged || causeChanged
                    ? PEGANpcKnowledgeWriteResult.InvalidatedExplicitly
                    : metadataChanged
                        ? PEGANpcKnowledgeWriteResult.Merged
                        : PEGANpcKnowledgeWriteResult.NoChange;
            }

            private bool AddOrigin(PEGANpcEvidenceOrigin origin)
            {
                PEGANpcEvidenceOrigin mergedOrigins = _origins | origin;
                if (mergedOrigins == _origins)
                {
                    return false;
                }

                _origins = mergedOrigins;
                return true;
            }

            private void SetKnown(T value, PEGANpcEvidenceOrigin origin, double observationTimeSeconds)
            {
                _status = PEGANpcKnowledgeStatus.Known;
                _hasObservationTime = true;
                _observationTimeSeconds = observationTimeSeconds;
                _origins = origin;
                _invalidationCauses = PEGANpcKnowledgeInvalidationCause.None;
                _candidate = value;
                _hasCandidate = true;
                _knownValue = value;
            }
        }
    }
}

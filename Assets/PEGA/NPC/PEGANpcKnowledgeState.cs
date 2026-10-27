using System;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using ImmersiveGames.Goap;
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

    public enum PEGANpcKnowledgeFactKind
    {
        PerceivedAreaPresence,
        ObservedCharacterIdentity,
        CharacterVisualContact,
        LastObservedCharacterPosition
    }

    /// <summary>Evidência radial ancorada à posição conhecida do observador, nunca à posição oculta do alvo.</summary>
    public readonly struct PEGANpcPerceivedAreaPresence
    {
        internal PEGANpcPerceivedAreaPresence(
            bool isPresent,
            Vector3 observerPosition,
            float perceivedAreaRadiusMeters)
        {
            IsPresent = isPresent;
            ObserverPosition = observerPosition;
            PerceivedAreaRadiusMeters = perceivedAreaRadiusMeters;
        }

        public bool IsPresent { get; }

        public Vector3 ObserverPosition { get; }

        public float PerceivedAreaRadiusMeters { get; }

        internal bool IsValid =>
            IsFinite(ObserverPosition) &&
            !float.IsNaN(PerceivedAreaRadiusMeters) &&
            !float.IsInfinity(PerceivedAreaRadiusMeters) &&
            PerceivedAreaRadiusMeters > 0f;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Descreve uma mudança de fato já aceita por um Knowledge State.</summary>
    public sealed class PEGANpcKnowledgeFactChangedEvent : IEvent
    {
        internal PEGANpcKnowledgeFactChangedEvent(
            PEGANpcKnowledgeFactKind factKind,
            ActorId? subjectActorId,
            PEGANpcKnowledgeWriteResult result,
            PEGANpcKnowledgeStatus previousStatus,
            PEGANpcKnowledgeStatus currentStatus,
            bool hasValueOrStatusChanged,
            PEGANpcEvidenceOrigin evidenceOrigin,
            double observationTimeSeconds)
        {
            FactKind = factKind;
            SubjectActorId = subjectActorId;
            Result = result;
            PreviousStatus = previousStatus;
            CurrentStatus = currentStatus;
            HasValueOrStatusChanged = hasValueOrStatusChanged;
            EvidenceOrigin = evidenceOrigin;
            ObservationTimeSeconds = observationTimeSeconds;
        }

        public PEGANpcKnowledgeFactKind FactKind { get; }

        public ActorId? SubjectActorId { get; }

        public PEGANpcKnowledgeWriteResult Result { get; }

        public PEGANpcKnowledgeStatus PreviousStatus { get; }

        public PEGANpcKnowledgeStatus CurrentStatus { get; }

        /// <summary>Indica mudança do valor conhecido ou do estado do fato, ignorando apenas metadados.</summary>
        public bool HasValueOrStatusChanged { get; }

        public PEGANpcEvidenceOrigin EvidenceOrigin { get; }

        public double ObservationTimeSeconds { get; }
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
    public sealed class PEGANpcKnowledgeState : MonoBehaviour, IGoapKnowledgeCapability
    {
        private const string LogCategory = "PEGA.Gameplay.Knowledge";

        [NonSerialized]
        private KnowledgeFactCell<PEGANpcPerceivedAreaPresence> _perceivedAreaPresence;

        [NonSerialized]
        private Dictionary<ActorId, SubjectKnowledge> _knowledgeBySubject;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        [NonSerialized]
        private EventBus<PEGANpcKnowledgeFactChangedEvent> _knowledgeChangeEvents;

        private void Awake()
        {
            EnsureStateInitialized();
            EnsureLoggerInitialized();
        }

        /// <summary>Assina mudanças confirmadas deste NPC.</summary>
        /// <remarks>
        /// A entrega é síncrona. Uma observação pode confirmar mais de um fato; consumidores devem agrupar
        /// os deltas antes de reavaliar um plano, sem decidir dentro da gravação da observação. Exceções de
        /// assinantes são registradas e isoladas para preservar a gravação e a entrega aos demais assinantes.
        /// </remarks>
        public IEventBinding SubscribeToKnowledgeChanges(Action<PEGANpcKnowledgeFactChangedEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureKnowledgeChangeEventsInitialized();
            return _knowledgeChangeEvents.Subscribe(change =>
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

        public bool UnsubscribeFromKnowledgeChanges(IEventBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            return _knowledgeChangeEvents != null && _knowledgeChangeEvents.Unsubscribe(binding);
        }

        private void OnDestroy()
        {
            _knowledgeChangeEvents?.Clear();
            _knowledgeChangeEvents = null;
        }

        public PEGANpcKnowledgeFact<PEGANpcPerceivedAreaPresence> GetPerceivedAreaPresence()
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

        public PEGANpcKnowledgeFact<bool> GetCharacterVisualContact(ActorId subjectActorId)
        {
            ValidateSubjectForQuery(subjectActorId);
            EnsureStateInitialized();
            return _knowledgeBySubject.TryGetValue(subjectActorId, out SubjectKnowledge subject)
                ? subject.CharacterVisualContact.Snapshot
                : PEGANpcKnowledgeFact<bool>.Unknown;
        }

        public PEGANpcKnowledgeFact<Vector3> GetLastObservedCharacterPosition(ActorId subjectActorId)
        {
            ValidateSubjectForQuery(subjectActorId);
            EnsureStateInitialized();
            return _knowledgeBySubject.TryGetValue(subjectActorId, out SubjectKnowledge subject)
                ? subject.LastObservedCharacterPosition.Snapshot
                : PEGANpcKnowledgeFact<Vector3>.Unknown;
        }

        public PEGANpcKnowledgeWriteResult ObservePerceivedAreaPresence(
            PEGANpcPerceivedAreaPresence perceivedPresence,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            EnsureStateInitialized();
            if (!perceivedPresence.IsValid)
            {
                return RejectEvidence(
                    "perceivedAreaPresence",
                    string.Empty,
                    origin,
                    observationTimeSeconds,
                    "radial presence evidence requires a finite observer position and positive finite area radius",
                    _perceivedAreaPresence.Snapshot);
            }

            return Observe(
                PEGANpcKnowledgeFactKind.PerceivedAreaPresence,
                "perceivedAreaPresence",
                null,
                _perceivedAreaPresence,
                perceivedPresence,
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
                PEGANpcKnowledgeFactKind.ObservedCharacterIdentity,
                "characterIdentity",
                subjectActorId,
                subject.CharacterIdentity,
                observedProfileId,
                AreEqual,
                origin,
                observationTimeSeconds);
        }

        public PEGANpcKnowledgeWriteResult ObserveCharacterVisualContact(
            ActorId subjectActorId,
            bool hasVisualContact,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                return RejectEvidence(
                    "targetVisualContact",
                    "invalid",
                    origin,
                    observationTimeSeconds,
                    "subject ActorId is invalid",
                    PEGANpcKnowledgeFact<bool>.Unknown);
            }

            EnsureStateInitialized();
            SubjectKnowledge subject = GetOrCreateSubject(subjectActorId);
            return Observe(
                PEGANpcKnowledgeFactKind.CharacterVisualContact,
                "targetVisualContact",
                subjectActorId,
                subject.CharacterVisualContact,
                hasVisualContact,
                AreEqual,
                origin,
                observationTimeSeconds);
        }

        public PEGANpcKnowledgeWriteResult ObserveCharacterPosition(
            ActorId subjectActorId,
            Vector3 observedPosition,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                return RejectEvidence(
                    "lastObservedTargetPosition",
                    "invalid",
                    origin,
                    observationTimeSeconds,
                    "subject ActorId is invalid",
                    PEGANpcKnowledgeFact<Vector3>.Unknown);
            }

            if (!IsFinite(observedPosition))
            {
                SubjectKnowledge existingSubject = FindSubject(subjectActorId);
                return RejectEvidence(
                    "lastObservedTargetPosition",
                    subjectActorId.StableText,
                    origin,
                    observationTimeSeconds,
                    "position contains a non-finite coordinate",
                    existingSubject != null
                        ? existingSubject.LastObservedCharacterPosition.Snapshot
                        : PEGANpcKnowledgeFact<Vector3>.Unknown);
            }

            EnsureStateInitialized();
            SubjectKnowledge subject = GetOrCreateSubject(subjectActorId);
            return Observe(
                PEGANpcKnowledgeFactKind.LastObservedCharacterPosition,
                "lastObservedTargetPosition",
                subjectActorId,
                subject.LastObservedCharacterPosition,
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
            return Invalidate(
                PEGANpcKnowledgeFactKind.PerceivedAreaPresence,
                "perceivedAreaPresence",
                null,
                _perceivedAreaPresence,
                origin,
                observationTimeSeconds);
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
                PEGANpcKnowledgeFactKind.ObservedCharacterIdentity,
                "characterIdentity",
                subjectActorId,
                subject.CharacterIdentity,
                origin,
                observationTimeSeconds);
        }

        public PEGANpcKnowledgeWriteResult InvalidateCharacterVisualContact(
            ActorId subjectActorId,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                return RejectEvidence(
                    "targetVisualContact",
                    "invalid",
                    origin,
                    observationTimeSeconds,
                    "subject ActorId is invalid",
                    PEGANpcKnowledgeFact<bool>.Unknown);
            }

            EnsureStateInitialized();
            SubjectKnowledge subject = GetOrCreateSubject(subjectActorId);
            return Invalidate(
                PEGANpcKnowledgeFactKind.CharacterVisualContact,
                "targetVisualContact",
                subjectActorId,
                subject.CharacterVisualContact,
                origin,
                observationTimeSeconds);
        }

        public PEGANpcKnowledgeWriteResult InvalidateLastObservedCharacterPosition(
            ActorId subjectActorId,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                return RejectEvidence(
                    "lastObservedTargetPosition",
                    "invalid",
                    origin,
                    observationTimeSeconds,
                    "subject ActorId is invalid",
                    PEGANpcKnowledgeFact<Vector3>.Unknown);
            }

            EnsureStateInitialized();
            SubjectKnowledge subject = GetOrCreateSubject(subjectActorId);
            return Invalidate(
                PEGANpcKnowledgeFactKind.LastObservedCharacterPosition,
                "lastObservedTargetPosition",
                subjectActorId,
                subject.LastObservedCharacterPosition,
                origin,
                observationTimeSeconds);
        }

        private PEGANpcKnowledgeWriteResult Observe<T>(
            PEGANpcKnowledgeFactKind factKind,
            string factName,
            ActorId? subjectActorId,
            KnowledgeFactCell<T> fact,
            T value,
            Func<T, T, bool> areEqual,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                return RejectEvidence(
                    factName,
                    GetSubjectActorText(subjectActorId),
                    origin,
                    observationTimeSeconds,
                    reason,
                    fact.Snapshot);
            }

            PEGANpcKnowledgeFact<T> previous = fact.Snapshot;
            PEGANpcKnowledgeWriteResult result = fact.Observe(
                value,
                origin,
                observationTimeSeconds,
                areEqual,
                out PEGANpcKnowledgeFact<T> current);
            LogWriteResult(
                factKind,
                factName,
                GetSubjectActorText(subjectActorId),
                result,
                origin,
                observationTimeSeconds,
                previous,
                current);
            PublishKnowledgeChange(
                factKind,
                subjectActorId,
                result,
                previous.Status,
                current.Status,
                HasValueOrStatusChanged(previous, current, areEqual),
                origin,
                observationTimeSeconds);
            return result;
        }

        private PEGANpcKnowledgeWriteResult Invalidate<T>(
            PEGANpcKnowledgeFactKind factKind,
            string factName,
            ActorId? subjectActorId,
            KnowledgeFactCell<T> fact,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                return RejectEvidence(
                    factName,
                    GetSubjectActorText(subjectActorId),
                    origin,
                    observationTimeSeconds,
                    reason,
                    fact.Snapshot);
            }

            PEGANpcKnowledgeFact<T> previous = fact.Snapshot;
            PEGANpcKnowledgeWriteResult result = fact.Invalidate(
                origin,
                observationTimeSeconds,
                out PEGANpcKnowledgeFact<T> current);
            LogWriteResult(
                factKind,
                factName,
                GetSubjectActorText(subjectActorId),
                result,
                origin,
                observationTimeSeconds,
                previous,
                current);
            PublishKnowledgeChange(
                factKind,
                subjectActorId,
                result,
                previous.Status,
                current.Status,
                previous.Status != current.Status,
                origin,
                observationTimeSeconds);
            return result;
        }

        private void PublishKnowledgeChange(
            PEGANpcKnowledgeFactKind factKind,
            ActorId? subjectActorId,
            PEGANpcKnowledgeWriteResult result,
            PEGANpcKnowledgeStatus previousStatus,
            PEGANpcKnowledgeStatus currentStatus,
            bool hasValueOrStatusChanged,
            PEGANpcEvidenceOrigin evidenceOrigin,
            double observationTimeSeconds)
        {
            if (_knowledgeChangeEvents == null || !IsCommittedKnowledgeChange(result))
            {
                return;
            }

            _knowledgeChangeEvents.Publish(new PEGANpcKnowledgeFactChangedEvent(
                factKind,
                subjectActorId,
                result,
                previousStatus,
                currentStatus,
                hasValueOrStatusChanged,
                evidenceOrigin,
                observationTimeSeconds));
        }

        private static bool HasValueOrStatusChanged<T>(
            PEGANpcKnowledgeFact<T> previous,
            PEGANpcKnowledgeFact<T> current,
            Func<T, T, bool> areEqual)
        {
            if (previous.Status != current.Status)
            {
                return true;
            }

            if (previous.Status != PEGANpcKnowledgeStatus.Known ||
                !previous.TryGetValue(out T previousValue) ||
                !current.TryGetValue(out T currentValue))
            {
                return false;
            }

            return !areEqual(previousValue, currentValue);
        }

        private void LogSubscriberFailure(
            Action<PEGANpcKnowledgeFactChangedEvent> handler,
            PEGANpcKnowledgeFactChangedEvent change,
            Exception exception)
        {
            EnsureLoggerInitialized();
            _log.Error(
                "NPC knowledge change subscriber failed; remaining subscribers will still be notified.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("fact", change.FactKind),
                LogFields.Field("subjectActorId", GetSubjectActorText(change.SubjectActorId)),
                LogFields.Field("writeResult", change.Result),
                LogFields.Field("subscriberMethod", handler.Method.Name),
                LogFields.Field("exceptionType", exception.GetType().FullName),
                LogFields.Field("exceptionMessage", exception.Message));
        }

        private void EnsureKnowledgeChangeEventsInitialized()
        {
            _knowledgeChangeEvents ??= new EventBus<PEGANpcKnowledgeFactChangedEvent>();
        }

        private static bool IsCommittedKnowledgeChange(PEGANpcKnowledgeWriteResult result) =>
            result == PEGANpcKnowledgeWriteResult.Recorded ||
            result == PEGANpcKnowledgeWriteResult.Replaced ||
            result == PEGANpcKnowledgeWriteResult.Merged ||
            result == PEGANpcKnowledgeWriteResult.InvalidatedConflict ||
            result == PEGANpcKnowledgeWriteResult.InvalidatedExplicitly;

        private static string GetSubjectActorText(ActorId? subjectActorId) =>
            subjectActorId.HasValue ? subjectActorId.Value.StableText : string.Empty;

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
            PEGANpcKnowledgeFactKind factKind,
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

            LogField[] fields =
            {
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("fact", factName),
                LogFields.Field("subjectActorId", subjectActorId),
                LogFields.Field("result", result),
                LogFields.Field("previousStatus", previous.Status),
                LogFields.Field("newStatus", current.Status),
                LogFields.Field("origins", current.Origins),
                LogFields.Field("observationTimeSeconds", evidenceTimeSeconds),
                LogFields.Field("storedObservationTimeSeconds", current.ObservationTimeSeconds),
                LogFields.Field("invalidationCauses", current.InvalidationCauses)
            };

            const string message = "NPC knowledge fact was updated.";
            bool isRepeatedPositionEvidence =
                factKind == PEGANpcKnowledgeFactKind.LastObservedCharacterPosition &&
                result == PEGANpcKnowledgeWriteResult.Replaced &&
                previous.Status == PEGANpcKnowledgeStatus.Known &&
                current.Status == PEGANpcKnowledgeStatus.Known;
            if (isRepeatedPositionEvidence)
            {
                _log.Debug(message, fields);
            }
            else
            {
                _log.Debug(message, fields);
            }
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
                _perceivedAreaPresence = new KnowledgeFactCell<PEGANpcPerceivedAreaPresence>();
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

        private static bool AreEqual(
            PEGANpcPerceivedAreaPresence left,
            PEGANpcPerceivedAreaPresence right) =>
            left.IsPresent == right.IsPresent &&
            AreExactlyEqual(left.ObserverPosition, right.ObserverPosition) &&
            left.PerceivedAreaRadiusMeters == right.PerceivedAreaRadiusMeters;

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

            public readonly KnowledgeFactCell<bool> CharacterVisualContact =
                new KnowledgeFactCell<bool>();

            public readonly KnowledgeFactCell<Vector3> LastObservedCharacterPosition =
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

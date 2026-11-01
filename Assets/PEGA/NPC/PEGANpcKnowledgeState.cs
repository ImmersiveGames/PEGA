using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using ImmersiveGames.Goap;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    public enum PEGANpcEvidenceOrigin
    {
        None = 0,
        Perception = 1,
        ActionResult = 2
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

    /// <summary>Individual knowledge authority for one NPC Actor; all fact kinds use the same generic store.</summary>
    [DisallowMultipleComponent]
    public sealed class PEGANpcKnowledgeState : MonoBehaviour, IGoapKnowledgeCapability
    {
        private const string LogCategory = "PEGA.Gameplay.Knowledge";

        [NonSerialized]
        private PEGAKnowledgeFactStore _factStore;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        [NonSerialized]
        private EventBus<PEGAKnowledgeFactChangedEvent> _knowledgeChangeEvents;

        private void Awake()
        {
            EnsureStateInitialized();
            EnsureLoggerInitialized();
        }

        /// <summary>Assina mudanças confirmadas deste NPC para qualquer definição de fato.</summary>
        /// <remarks>
        /// A entrega é síncrona. Uma observação pode confirmar mais de um fato; consumidores devem agrupar
        /// os deltas antes de reavaliar um plano. A falha de um assinante é registrada e isolada.
        /// </remarks>
        public IEventBinding SubscribeToKnowledgeChanges(Action<PEGAKnowledgeFactChangedEvent> handler)
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
            if (_factStore != null)
            {
                _factStore.Changed -= OnFactChanged;
                _factStore.Clear();
                _factStore = null;
            }

            _knowledgeChangeEvents?.Clear();
            _knowledgeChangeEvents = null;
        }

        /// <summary>Consulta qualquer fato Individual por sua definição e endereço tipados.</summary>
        public PEGAKnowledgeFact<TValue> GetFact<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key)
        {
            EnsureStateInitialized();
            return _factStore.Get(key, Time.timeAsDouble);
        }

        /// <summary>Returns character subjects represented by this NPC's current character-fact adapters.</summary>
        public IReadOnlyList<ActorId> GetKnownCharacterSubjectIds()
        {
            EnsureStateInitialized();
            var subjectIds = new HashSet<ActorId>();
            AddSubjects(
                subjectIds,
                _factStore.GetFacts(PEGANpcKnowledgeFacts.ObservedCharacterIdentity, Time.timeAsDouble));
            AddSubjects(
                subjectIds,
                _factStore.GetFacts(PEGANpcKnowledgeFacts.CharacterVisualContact, Time.timeAsDouble));
            AddSubjects(
                subjectIds,
                _factStore.GetFacts(PEGANpcKnowledgeFacts.LastObservedCharacterPosition, Time.timeAsDouble));

            var sorted = new List<ActorId>(subjectIds);
            sorted.Sort((left, right) => string.CompareOrdinal(left.StableText, right.StableText));
            return new ReadOnlyCollection<ActorId>(sorted);
        }

        private static void AddSubjects<TValue>(
            HashSet<ActorId> target,
            IReadOnlyList<PEGAKnowledgeFactSnapshot<ActorId, TValue>> facts)
        {
            for (int index = 0; index < facts.Count; index++)
            {
                if (facts[index].Key.AddressValue.IsValid)
                {
                    target.Add(facts[index].Key.AddressValue);
                }
            }
        }

        /// <summary>Registra ou atualiza qualquer fato Individual pela política da definição.</summary>
        public PEGAKnowledgeWriteResult ObserveFact<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PEGAKnowledgeEvidence evidence)
        {
            EnsureStateInitialized();
            if (!key.IsValid || !evidence.IsValid)
            {
                return PEGAKnowledgeWriteResult.RejectedInvalid;
            }

            PEGAKnowledgeFact<TValue> previous = _factStore.Get(key, evidence.ObservationTimeSeconds);
            PEGAKnowledgeWriteResult result = _factStore.Observe(key, value, evidence);
            PEGAKnowledgeFact<TValue> current = _factStore.Get(key, evidence.ObservationTimeSeconds);
            LogWriteResult(key, result, evidence, previous, current);
            return result;
        }

        /// <summary>Invalida qualquer fato Individual pela política da definição.</summary>
        public PEGAKnowledgeWriteResult InvalidateFact<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGAKnowledgeEvidence evidence)
        {
            EnsureStateInitialized();
            if (!key.IsValid || !evidence.IsValid)
            {
                return PEGAKnowledgeWriteResult.RejectedInvalid;
            }

            PEGAKnowledgeFact<TValue> previous = _factStore.Get(key, evidence.ObservationTimeSeconds);
            PEGAKnowledgeWriteResult result = _factStore.Invalidate(key, evidence);
            PEGAKnowledgeFact<TValue> current = _factStore.Get(key, evidence.ObservationTimeSeconds);
            LogWriteResult(key, result, evidence, previous, current);
            return result;
        }

        public PEGAKnowledgeFact<PEGANpcPerceivedAreaPresence> GetPerceivedAreaPresence() =>
            GetFact(PEGANpcKnowledgeFacts.PerceivedAreaPresence.For(0));

        public PEGAKnowledgeFact<ActorProfileId> GetObservedCharacterIdentity(ActorId subjectActorId)
        {
            ValidateSubjectForQuery(subjectActorId);
            return GetFact(PEGANpcKnowledgeFacts.ObservedCharacterIdentity.For(subjectActorId));
        }

        public PEGAKnowledgeFact<bool> GetCharacterVisualContact(ActorId subjectActorId)
        {
            ValidateSubjectForQuery(subjectActorId);
            return GetFact(PEGANpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId));
        }

        public PEGAKnowledgeFact<Vector3> GetLastObservedCharacterPosition(ActorId subjectActorId)
        {
            ValidateSubjectForQuery(subjectActorId);
            return GetFact(PEGANpcKnowledgeFacts.LastObservedCharacterPosition.For(subjectActorId));
        }

        public PEGAKnowledgeWriteResult ObservePerceivedAreaPresence(
            PEGANpcPerceivedAreaPresence perceivedPresence,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                return RejectEvidence(
                    PEGANpcKnowledgeFacts.PerceivedAreaPresence.For(0),
                    PEGAKnowledgeEvidence.From(origin, observationTimeSeconds),
                    reason,
                    GetPerceivedAreaPresence());
            }

            return ObserveFact(
                PEGANpcKnowledgeFacts.PerceivedAreaPresence.For(0),
                perceivedPresence,
                PEGAKnowledgeEvidence.From(origin, observationTimeSeconds));
        }

        public PEGAKnowledgeWriteResult ObserveCharacterIdentity(
            ActorId subjectActorId,
            ActorProfileId observedProfileId,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                PEGAKnowledgeFactKey<ActorId, ActorProfileId> key =
                    PEGANpcKnowledgeFacts.ObservedCharacterIdentity.For(subjectActorId);
                return RejectEvidence(
                    key,
                    PEGAKnowledgeEvidence.From(origin, observationTimeSeconds),
                    "subject ActorId is invalid",
                    PEGAKnowledgeFact<ActorProfileId>.Unknown(PEGAKnowledgeScope.Individual));
            }

            if (!observedProfileId.IsValid)
            {
                var key = PEGANpcKnowledgeFacts.ObservedCharacterIdentity.For(subjectActorId);
                return RejectEvidence(
                    key,
                    PEGAKnowledgeEvidence.From(origin, observationTimeSeconds),
                    "observed ActorProfileId is invalid",
                    GetFact(key));
            }

            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                var key = PEGANpcKnowledgeFacts.ObservedCharacterIdentity.For(subjectActorId);
                return RejectEvidence(key, PEGAKnowledgeEvidence.From(origin, observationTimeSeconds), reason, GetFact(key));
            }

            return ObserveFact(
                PEGANpcKnowledgeFacts.ObservedCharacterIdentity.For(subjectActorId),
                observedProfileId,
                PEGAKnowledgeEvidence.From(origin, observationTimeSeconds));
        }

        public PEGAKnowledgeWriteResult ObserveCharacterVisualContact(
            ActorId subjectActorId,
            bool hasVisualContact,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                var key = PEGANpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId);
                return RejectEvidence(
                    key,
                    PEGAKnowledgeEvidence.From(origin, observationTimeSeconds),
                    "subject ActorId is invalid",
                    PEGAKnowledgeFact<bool>.Unknown(PEGAKnowledgeScope.Individual));
            }

            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                var key = PEGANpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId);
                return RejectEvidence(key, PEGAKnowledgeEvidence.From(origin, observationTimeSeconds), reason, GetFact(key));
            }

            return ObserveFact(
                PEGANpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId),
                hasVisualContact,
                PEGAKnowledgeEvidence.From(origin, observationTimeSeconds));
        }

        public PEGAKnowledgeWriteResult ObserveCharacterPosition(
            ActorId subjectActorId,
            Vector3 observedPosition,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                var key = PEGANpcKnowledgeFacts.LastObservedCharacterPosition.For(subjectActorId);
                return RejectEvidence(
                    key,
                    PEGAKnowledgeEvidence.From(origin, observationTimeSeconds),
                    "subject ActorId is invalid",
                    PEGAKnowledgeFact<Vector3>.Unknown(PEGAKnowledgeScope.Individual));
            }

            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                var key = PEGANpcKnowledgeFacts.LastObservedCharacterPosition.For(subjectActorId);
                return RejectEvidence(key, PEGAKnowledgeEvidence.From(origin, observationTimeSeconds), reason, GetFact(key));
            }

            return ObserveFact(
                PEGANpcKnowledgeFacts.LastObservedCharacterPosition.For(subjectActorId),
                observedPosition,
                PEGAKnowledgeEvidence.From(origin, observationTimeSeconds));
        }

        public PEGAKnowledgeWriteResult InvalidatePerceivedAreaPresence(
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds) =>
            InvalidateLegacyFact(
                PEGANpcKnowledgeFacts.PerceivedAreaPresence.For(0),
                origin,
                observationTimeSeconds,
                GetPerceivedAreaPresence());

        public PEGAKnowledgeWriteResult InvalidateObservedCharacterIdentity(
            ActorId subjectActorId,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                var key = PEGANpcKnowledgeFacts.ObservedCharacterIdentity.For(subjectActorId);
                return RejectEvidence(key, PEGAKnowledgeEvidence.From(origin, observationTimeSeconds),
                    "subject ActorId is invalid", PEGAKnowledgeFact<ActorProfileId>.Unknown(PEGAKnowledgeScope.Individual));
            }

            return InvalidateLegacyFact(
                PEGANpcKnowledgeFacts.ObservedCharacterIdentity.For(subjectActorId),
                origin,
                observationTimeSeconds,
                GetObservedCharacterIdentity(subjectActorId));
        }

        public PEGAKnowledgeWriteResult InvalidateCharacterVisualContact(
            ActorId subjectActorId,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                var key = PEGANpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId);
                return RejectEvidence(key, PEGAKnowledgeEvidence.From(origin, observationTimeSeconds),
                    "subject ActorId is invalid", PEGAKnowledgeFact<bool>.Unknown(PEGAKnowledgeScope.Individual));
            }

            return InvalidateLegacyFact(
                PEGANpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId),
                origin,
                observationTimeSeconds,
                GetCharacterVisualContact(subjectActorId));
        }

        public PEGAKnowledgeWriteResult InvalidateLastObservedCharacterPosition(
            ActorId subjectActorId,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                var key = PEGANpcKnowledgeFacts.LastObservedCharacterPosition.For(subjectActorId);
                return RejectEvidence(key, PEGAKnowledgeEvidence.From(origin, observationTimeSeconds),
                    "subject ActorId is invalid", PEGAKnowledgeFact<Vector3>.Unknown(PEGAKnowledgeScope.Individual));
            }

            return InvalidateLegacyFact(
                PEGANpcKnowledgeFacts.LastObservedCharacterPosition.For(subjectActorId),
                origin,
                observationTimeSeconds,
                GetLastObservedCharacterPosition(subjectActorId));
        }

        private PEGAKnowledgeWriteResult InvalidateLegacyFact<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGANpcEvidenceOrigin origin,
            double observationTimeSeconds,
            PEGAKnowledgeFact<TValue> current)
        {
            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                return RejectEvidence(key, PEGAKnowledgeEvidence.From(origin, observationTimeSeconds), reason, current);
            }

            return InvalidateFact(key, PEGAKnowledgeEvidence.From(origin, observationTimeSeconds));
        }

        private PEGAKnowledgeWriteResult RejectEvidence<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGAKnowledgeEvidence evidence,
            string reason,
            PEGAKnowledgeFact<TValue> current)
        {
            EnsureLoggerInitialized();
            _log.Warning(
                "NPC knowledge evidence was rejected.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("fact", key.Name),
                LogFields.Field("factAddress", key.AddressValue),
                LogFields.Field("previousStatus", current.Status),
                LogFields.Field("evidenceOrigin", evidence.Origin),
                LogFields.Field("storedOrigins", current.Origins),
                LogFields.Field("observationTimeSeconds", evidence.ObservationTimeSeconds),
                LogFields.Field("storedObservationTimeSeconds", current.HasObservationTime
                    ? (object)current.ObservationTimeSeconds
                    : "unavailable"),
                LogFields.Field("invalidationCauses", current.InvalidationCauses),
                LogFields.Field("reason", reason));
            return PEGAKnowledgeWriteResult.RejectedInvalid;
        }

        private void LogWriteResult<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGAKnowledgeWriteResult result,
            PEGAKnowledgeEvidence evidence,
            PEGAKnowledgeFact<TValue> previous,
            PEGAKnowledgeFact<TValue> current)
        {
            if (result == PEGAKnowledgeWriteResult.NoChange)
            {
                return;
            }

            EnsureLoggerInitialized();
            _log.Debug(
                result == PEGAKnowledgeWriteResult.IgnoredStale
                    ? "NPC knowledge evidence is older than the stored fact and was ignored."
                    : result == PEGAKnowledgeWriteResult.InvalidatedConflict ||
                      result == PEGAKnowledgeWriteResult.InvalidatedExplicitly
                        ? "NPC knowledge fact was invalidated."
                        : "NPC knowledge fact was updated.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("fact", key.Name),
                LogFields.Field("factAddress", key.AddressValue),
                LogFields.Field("result", result),
                LogFields.Field("previousStatus", previous.Status),
                LogFields.Field("currentStatus", current.Status),
                LogFields.Field("origins", current.Origins),
                LogFields.Field("observationTimeSeconds", evidence.ObservationTimeSeconds),
                LogFields.Field("storedObservationTimeSeconds", current.HasObservationTime
                    ? (object)current.ObservationTimeSeconds
                    : "unavailable"),
                LogFields.Field("invalidationCauses", current.InvalidationCauses));
        }

        private void OnFactChanged(PEGAKnowledgeFactChangedEvent change)
        {
            _knowledgeChangeEvents?.Publish(change);
        }

        private void LogSubscriberFailure(
            Action<PEGAKnowledgeFactChangedEvent> handler,
            PEGAKnowledgeFactChangedEvent change,
            Exception exception)
        {
            EnsureLoggerInitialized();
            _log.Error(
                "NPC knowledge change subscriber failed; remaining subscribers will still be notified.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("fact", change.FactKey),
                LogFields.Field("writeResult", change.Result),
                LogFields.Field("subscriberMethod", handler.Method.Name),
                LogFields.Field("exceptionType", exception.GetType().FullName),
                LogFields.Field("exceptionMessage", exception.Message));
        }

        private void EnsureKnowledgeChangeEventsInitialized()
        {
            _knowledgeChangeEvents ??= new EventBus<PEGAKnowledgeFactChangedEvent>();
        }

        private void EnsureStateInitialized()
        {
            if (_factStore != null)
            {
                return;
            }

            _factStore = new PEGAKnowledgeFactStore(PEGAKnowledgeScope.Individual);
            _factStore.Changed += OnFactChanged;
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
    }
}

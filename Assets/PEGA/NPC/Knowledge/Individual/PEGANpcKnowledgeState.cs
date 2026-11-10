using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.NPC.Knowledge.Authoring;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using UnityEngine;
namespace PEGA.NPC.Knowledge.Individual
{
    public enum PegaNpcEvidenceOrigin
    {
        None = 0,
        Perception = 1,
        ActionResult = 2,
        DesignerAuthoredInitial = 3
    }

    /// <summary>Evidência radial ancorada à posição conhecida do observador, nunca à posição oculta do alvo.</summary>
    [Serializable]
    public struct PegaNpcPerceivedAreaPresence
    {
        [SerializeField]
        [Tooltip("Se a informação declara presença na área.")]
        private bool isPresent;

        [SerializeField]
        [Tooltip("Posição conhecida do observador que ancora a área, não a posição oculta do alvo.")]
        private Vector3 observerPosition;

        [SerializeField, Min(0.01f)]
        [Tooltip("Raio da área em metros.")]
        private float perceivedAreaRadiusMeters;

        internal PegaNpcPerceivedAreaPresence(
            bool isPresent,
            Vector3 observerPosition,
            float perceivedAreaRadiusMeters)
        {
            this.isPresent = isPresent;
            this.observerPosition = observerPosition;
            this.perceivedAreaRadiusMeters = perceivedAreaRadiusMeters;
        }

        public bool IsPresent => isPresent;

        public Vector3 ObserverPosition => observerPosition;

        public float PerceivedAreaRadiusMeters => perceivedAreaRadiusMeters;

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
    public sealed class PegaNpcKnowledgeState : MonoBehaviour, IPegaKnowledgeFactReader, IPegaKnowledgeFactWriter
    {
        private const string LogCategory = "PEGA.Gameplay.Knowledge";
        private const double InitialKnowledgeRetryIntervalSeconds = 0.1d;

        [SerializeField, Tooltip("Conhecimentos iniciais deste NPC. Cada linha usa uma definição canônica e entra no Knowledge State Individual.")]
        private List<PegaKnowledgeFactAuthoringEntry> initialKnowledge = new List<PegaKnowledgeFactAuthoringEntry>();

        [NonSerialized]
        private PegaKnowledgeFactStore _factStore;

        [NonSerialized]
        private HashSet<int> _processedInitialKnowledgeEntries;

        [NonSerialized]
        private PegaKnowledgeFactAuthoringEntry[] _initialKnowledgeSnapshot;

        [NonSerialized]
        private ActorOccurrenceReferenceBinding _actorBinding;

        [NonSerialized]
        private bool _applyingInitialKnowledge;

        [NonSerialized]
        private double _initialKnowledgeObservationTimeSeconds;

        [NonSerialized]
        private double _nextInitialKnowledgeRetryTimeSeconds;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        public PegaKnowledgeScope Scope => PegaKnowledgeScope.Individual;

        public bool IsAvailable => _factStore != null;

        [NonSerialized]
        private EventBus<PegaKnowledgeFactChangedEvent> _knowledgeChangeEvents;

        private void Awake()
        {
            _initialKnowledgeObservationTimeSeconds = Time.timeAsDouble;
            _initialKnowledgeSnapshot = initialKnowledge != null
                ? initialKnowledge.ToArray()
                : Array.Empty<PegaKnowledgeFactAuthoringEntry>();
            _actorBinding = GetComponent<ActorOccurrenceReferenceBinding>();
            if (_actorBinding != null)
            {
                _actorBinding.Changed += OnActorBindingChanged;
            }

            EnsureFactStoreInitialized();
            EnsureLoggerInitialized();
            ApplyPendingInitialKnowledge();
        }

        private void LateUpdate()
        {
            if (!HasPendingInitialKnowledgeEntries())
            {
                return;
            }

            double now = Time.timeAsDouble;
            if (now < _nextInitialKnowledgeRetryTimeSeconds)
            {
                return;
            }

            _nextInitialKnowledgeRetryTimeSeconds = now + InitialKnowledgeRetryIntervalSeconds;
            ApplyPendingInitialKnowledge();
        }

        /// <summary>Assina mudanças confirmadas deste NPC para qualquer definição de fato.</summary>
        /// <remarks>
        /// A entrega é síncrona. Uma observação pode confirmar mais de um fato; consumidores devem agrupar
        /// os deltas antes de reavaliar um plano. A falha de um assinante é registrada e isolada.
        /// </remarks>
        public IEventBinding SubscribeToKnowledgeChanges(Action<PegaKnowledgeFactChangedEvent> handler)
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
            if (_actorBinding != null)
            {
                _actorBinding.Changed -= OnActorBindingChanged;
                _actorBinding = null;
            }

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
        public PegaKnowledgeFact<TValue> GetFact<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key)
        {
            EnsureStateInitialized();
            return _factStore.Get(key, Time.timeAsDouble);
        }

        public PegaKnowledgeFact<TValue> ReadFact<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds)
        {
            EnsureStateInitialized();
            return _factStore.Get(key, queryTimeSeconds);
        }

        /// <summary>Returns character subjects represented by this NPC's current character-fact adapters.</summary>
        public IReadOnlyList<ActorId> GetKnownCharacterSubjectIds()
        {
            EnsureStateInitialized();
            var subjectIds = new HashSet<ActorId>();
            AddSubjects(
                subjectIds,
                _factStore.GetFacts(PegaNpcKnowledgeFacts.IsPlayer, Time.timeAsDouble));
            AddSubjects(
                subjectIds,
                _factStore.GetFacts(PegaNpcKnowledgeFacts.CharacterVisualContact, Time.timeAsDouble));
            AddSubjects(
                subjectIds,
                _factStore.GetFacts(PegaNpcKnowledgeFacts.LastObservedCharacterPosition, Time.timeAsDouble));

            var sorted = new List<ActorId>(subjectIds);
            sorted.Sort((left, right) => string.CompareOrdinal(left.StableText, right.StableText));
            return new ReadOnlyCollection<ActorId>(sorted);
        }

        private static void AddSubjects<TValue>(
            HashSet<ActorId> target,
            IReadOnlyList<PegaKnowledgeFactSnapshot<ActorId, TValue>> facts)
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
        public PegaKnowledgeWriteResult ObserveFact<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PegaKnowledgeEvidence evidence)
        {
            EnsureStateInitialized();
            if (!key.IsValid || !evidence.IsValid)
            {
                return PegaKnowledgeWriteResult.RejectedInvalid;
            }

            PegaKnowledgeFact<TValue> previous = _factStore.Get(key, evidence.ObservationTimeSeconds);
            PegaKnowledgeWriteResult result = _factStore.Observe(key, value, evidence);
            PegaKnowledgeFact<TValue> current = _factStore.Get(key, evidence.ObservationTimeSeconds);
            LogWriteResult(key, result, evidence, previous, current);
            return result;
        }

        /// <summary>Invalida qualquer fato Individual pela política da definição.</summary>
        public PegaKnowledgeWriteResult InvalidateFact<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            PegaKnowledgeEvidence evidence)
        {
            EnsureStateInitialized();
            if (!key.IsValid || !evidence.IsValid)
            {
                return PegaKnowledgeWriteResult.RejectedInvalid;
            }

            PegaKnowledgeFact<TValue> previous = _factStore.Get(key, evidence.ObservationTimeSeconds);
            PegaKnowledgeWriteResult result = _factStore.Invalidate(key, evidence);
            PegaKnowledgeFact<TValue> current = _factStore.Get(key, evidence.ObservationTimeSeconds);
            LogWriteResult(key, result, evidence, previous, current);
            return result;
        }

        public PegaKnowledgeFact<PegaNpcPerceivedAreaPresence> GetPerceivedAreaPresence() =>
            GetFact(PegaNpcKnowledgeFacts.PerceivedAreaPresence.For(0));

        public PegaKnowledgeFact<bool> GetCharacterVisualContact(ActorId subjectActorId)
        {
            ValidateSubjectForQuery(subjectActorId);
            return GetFact(PegaNpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId));
        }

        public PegaKnowledgeFact<Vector3> GetLastObservedCharacterPosition(ActorId subjectActorId)
        {
            ValidateSubjectForQuery(subjectActorId);
            return GetFact(PegaNpcKnowledgeFacts.LastObservedCharacterPosition.For(subjectActorId));
        }

        public PegaKnowledgeWriteResult ObservePerceivedAreaPresence(
            PegaNpcPerceivedAreaPresence perceivedPresence,
            PegaNpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                return RejectEvidence(
                    PegaNpcKnowledgeFacts.PerceivedAreaPresence.For(0),
                    PegaKnowledgeEvidence.From(origin, observationTimeSeconds),
                    reason,
                    GetPerceivedAreaPresence());
            }

            return ObserveFact(
                PegaNpcKnowledgeFacts.PerceivedAreaPresence.For(0),
                perceivedPresence,
                PegaKnowledgeEvidence.From(origin, observationTimeSeconds));
        }

        public PegaKnowledgeWriteResult ObserveCharacterVisualContact(
            ActorId subjectActorId,
            bool hasVisualContact,
            PegaNpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                var key = PegaNpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId);
                return RejectEvidence(
                    key,
                    PegaKnowledgeEvidence.From(origin, observationTimeSeconds),
                    "subject ActorId is invalid",
                    PegaKnowledgeFact<bool>.Unknown(PegaKnowledgeScope.Individual));
            }

            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                var key = PegaNpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId);
                return RejectEvidence(key, PegaKnowledgeEvidence.From(origin, observationTimeSeconds), reason, GetFact(key));
            }

            return ObserveFact(
                PegaNpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId),
                hasVisualContact,
                PegaKnowledgeEvidence.From(origin, observationTimeSeconds));
        }

        public PegaKnowledgeWriteResult ObserveCharacterPosition(
            ActorId subjectActorId,
            Vector3 observedPosition,
            PegaNpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                var key = PegaNpcKnowledgeFacts.LastObservedCharacterPosition.For(subjectActorId);
                return RejectEvidence(
                    key,
                    PegaKnowledgeEvidence.From(origin, observationTimeSeconds),
                    "subject ActorId is invalid",
                    PegaKnowledgeFact<Vector3>.Unknown(PegaKnowledgeScope.Individual));
            }

            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                var key = PegaNpcKnowledgeFacts.LastObservedCharacterPosition.For(subjectActorId);
                return RejectEvidence(key, PegaKnowledgeEvidence.From(origin, observationTimeSeconds), reason, GetFact(key));
            }

            return ObserveFact(
                PegaNpcKnowledgeFacts.LastObservedCharacterPosition.For(subjectActorId),
                observedPosition,
                PegaKnowledgeEvidence.From(origin, observationTimeSeconds));
        }

        public PegaKnowledgeWriteResult InvalidatePerceivedAreaPresence(
            PegaNpcEvidenceOrigin origin,
            double observationTimeSeconds) =>
            InvalidateLegacyFact(
                PegaNpcKnowledgeFacts.PerceivedAreaPresence.For(0),
                origin,
                observationTimeSeconds,
                GetPerceivedAreaPresence());

        public PegaKnowledgeWriteResult InvalidateCharacterVisualContact(
            ActorId subjectActorId,
            PegaNpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                var key = PegaNpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId);
                return RejectEvidence(key, PegaKnowledgeEvidence.From(origin, observationTimeSeconds),
                    "subject ActorId is invalid", PegaKnowledgeFact<bool>.Unknown(PegaKnowledgeScope.Individual));
            }

            return InvalidateLegacyFact(
                PegaNpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId),
                origin,
                observationTimeSeconds,
                GetCharacterVisualContact(subjectActorId));
        }

        public PegaKnowledgeWriteResult InvalidateLastObservedCharacterPosition(
            ActorId subjectActorId,
            PegaNpcEvidenceOrigin origin,
            double observationTimeSeconds)
        {
            if (!subjectActorId.IsValid)
            {
                var key = PegaNpcKnowledgeFacts.LastObservedCharacterPosition.For(subjectActorId);
                return RejectEvidence(key, PegaKnowledgeEvidence.From(origin, observationTimeSeconds),
                    "subject ActorId is invalid", PegaKnowledgeFact<Vector3>.Unknown(PegaKnowledgeScope.Individual));
            }

            return InvalidateLegacyFact(
                PegaNpcKnowledgeFacts.LastObservedCharacterPosition.For(subjectActorId),
                origin,
                observationTimeSeconds,
                GetLastObservedCharacterPosition(subjectActorId));
        }

        private PegaKnowledgeWriteResult InvalidateLegacyFact<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            PegaNpcEvidenceOrigin origin,
            double observationTimeSeconds,
            PegaKnowledgeFact<TValue> current)
        {
            if (!TryValidateEvidence(origin, observationTimeSeconds, out string reason))
            {
                return RejectEvidence(key, PegaKnowledgeEvidence.From(origin, observationTimeSeconds), reason, current);
            }

            return InvalidateFact(key, PegaKnowledgeEvidence.From(origin, observationTimeSeconds));
        }

        private PegaKnowledgeWriteResult RejectEvidence<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            PegaKnowledgeEvidence evidence,
            string reason,
            PegaKnowledgeFact<TValue> current)
        {
            EnsureLoggerInitialized();
            _log.Warning(
                "NPC knowledge evidence was rejected.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("fact", key.Name),
                LogFields.Field("factDefinitionId", key.Definition.Id),
                LogFields.Field("factAddress", key.AddressValue),
                LogFields.Field("scope", Scope),
                LogFields.Field("previousStatus", current.Status),
                LogFields.Field("evidenceOrigin", evidence.Origin),
                LogFields.Field("storedOrigins", current.Origins),
                LogFields.Field("observationTimeSeconds", evidence.ObservationTimeSeconds),
                LogFields.Field("storedObservationTimeSeconds", current.HasObservationTime
                    ? (object)current.ObservationTimeSeconds
                    : "unavailable"),
                LogFields.Field("invalidationCauses", current.InvalidationCauses),
                LogFields.Field("reason", reason));
            return PegaKnowledgeWriteResult.RejectedInvalid;
        }

        private void LogWriteResult<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            PegaKnowledgeWriteResult result,
            PegaKnowledgeEvidence evidence,
            PegaKnowledgeFact<TValue> previous,
            PegaKnowledgeFact<TValue> current)
        {
            if (result == PegaKnowledgeWriteResult.NoChange ||
                IsSameKnownValueReplacement(result, previous, current))
            {
                return;
            }

            EnsureLoggerInitialized();
            _log.Debug(
                result == PegaKnowledgeWriteResult.IgnoredStale
                    ? "NPC knowledge evidence is older than the stored fact and was ignored."
                    : result == PegaKnowledgeWriteResult.InvalidatedConflict ||
                      result == PegaKnowledgeWriteResult.InvalidatedExplicitly
                        ? "NPC knowledge fact was invalidated."
                        : "NPC knowledge fact was updated.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("fact", key.Name),
                LogFields.Field("factDefinitionId", key.Definition.Id),
                LogFields.Field("factAddress", key.AddressValue),
                LogFields.Field("scope", Scope),
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

        private static bool IsSameKnownValueReplacement<TValue>(
            PegaKnowledgeWriteResult result,
            PegaKnowledgeFact<TValue> previous,
            PegaKnowledgeFact<TValue> current)
        {
            return result == PegaKnowledgeWriteResult.Replaced &&
                   previous.Status == PegaKnowledgeStatus.Known &&
                   current.Status == PegaKnowledgeStatus.Known &&
                   previous.TryGetValue(out TValue previousValue) &&
                   current.TryGetValue(out TValue currentValue) &&
                   EqualityComparer<TValue>.Default.Equals(previousValue, currentValue);
        }

        private void OnFactChanged(PegaKnowledgeFactChangedEvent change)
        {
            _knowledgeChangeEvents?.Publish(change);
        }

        private void LogSubscriberFailure(
            Action<PegaKnowledgeFactChangedEvent> handler,
            PegaKnowledgeFactChangedEvent change,
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
            _knowledgeChangeEvents ??= new EventBus<PegaKnowledgeFactChangedEvent>();
        }

        private void EnsureStateInitialized()
        {
            EnsureFactStoreInitialized();
        }

        private void EnsureFactStoreInitialized()
        {
            if (_factStore != null)
            {
                return;
            }

            _factStore = new PegaKnowledgeFactStore(PegaKnowledgeScope.Individual);
            _factStore.Changed += OnFactChanged;
        }

        private void ApplyPendingInitialKnowledge()
        {
            if (_initialKnowledgeSnapshot == null || _initialKnowledgeSnapshot.Length == 0)
            {
                return;
            }

            _processedInitialKnowledgeEntries ??= new HashSet<int>();
            _applyingInitialKnowledge = true;
            try
            {
                for (int index = 0; index < _initialKnowledgeSnapshot.Length; index++)
                {
                    if (_processedInitialKnowledgeEntries.Contains(index))
                    {
                        continue;
                    }

                    PegaKnowledgeFactAuthoringEntry entry = _initialKnowledgeSnapshot[index];
                    PegaKnowledgeEvidence evidence = PegaKnowledgeEvidence.From(
                        PegaNpcEvidenceOrigin.DesignerAuthoredInitial,
                        _initialKnowledgeObservationTimeSeconds,
                        this,
                        entry.DefinitionId);
                    PegaKnowledgeFactInitialAuthoringStatus status = entry.TryApply(
                        _actorBinding,
                        this,
                        evidence,
                        out PegaKnowledgeWriteResult result,
                        out string diagnostic);
                    if (status == PegaKnowledgeFactInitialAuthoringStatus.Pending)
                    {
                        continue;
                    }

                    _processedInitialKnowledgeEntries.Add(index);
                    if (status == PegaKnowledgeFactInitialAuthoringStatus.Rejected ||
                        result == PegaKnowledgeWriteResult.RejectedInvalid)
                    {
                        EnsureLoggerInitialized();
                        _log.Warning(
                            "Designer-authored initial knowledge was rejected.",
                            LogFields.Field("actorRoot", gameObject.name),
                            LogFields.Field("entryIndex", index),
                            LogFields.Field("factDefinitionId", entry.DefinitionId),
                            LogFields.Field("writeResult", result),
                            LogFields.Field("reason", diagnostic));
                    }
                }
            }
            finally
            {
                _applyingInitialKnowledge = false;
            }
        }

        private void OnActorBindingChanged(ActorOccurrenceReferenceChange change)
        {
            if (!_applyingInitialKnowledge)
            {
                ApplyPendingInitialKnowledge();
            }
        }

        private bool HasPendingInitialKnowledgeEntries() =>
            _initialKnowledgeSnapshot != null &&
            _processedInitialKnowledgeEntries != null &&
            _processedInitialKnowledgeEntries.Count < _initialKnowledgeSnapshot.Length;

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PegaLogging.CreateScopedLogger<PegaNpcKnowledgeState>(LogCategory, gameObject.name);
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
            PegaNpcEvidenceOrigin origin,
            double observationTimeSeconds,
            out string reason)
        {
            if (origin != PegaNpcEvidenceOrigin.Perception && origin != PegaNpcEvidenceOrigin.ActionResult)
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

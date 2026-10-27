using System;
using System.Collections;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Framework.PlayerParticipation;
using Immersive.Logging.Records;
using ImmersiveGames.Perception;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// PEGA adapter that resolves the observed Player Actor occurrence and translates composed sensor observations
    /// into Knowledge State facts and Autonomy re-evaluation requests.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorOccurrenceReferenceBinding))]
    [RequireComponent(typeof(PEGANpcKnowledgeState))]
    [AddComponentMenu("PEGA/NPC/Perception Source")]
    public sealed class PEGANpcPerceptionSource : MonoBehaviour, IPEGANpcTargetObservationSource
    {
        private const string LogCategory = "PEGA.Gameplay.Perception";

        [Header("Observed Player")]
        [SerializeField]
        [Tooltip("Observador da Player Session no escopo da Activity que contém este NPC.")]
        private PlayerSessionObserver playerSessionObserver;

        [Header("Sensor Assets")]
        [SerializeField]
        [Tooltip("Asset do sensor radial que informa presença sem identidade.")]
        private RadialPresenceSensor perceivedAreaSensor;

        [SerializeField]
        [Tooltip("Asset do sensor de visão que confirma identidade e contato visual.")]
        private VisionConeSensor visionConeSensor;

        [Header("Sampling")]
        [SerializeField, Min(0.05f)]
        [Tooltip("Intervalo entre amostras físicas da percepção (s).")]
        private float observationIntervalSeconds = 0.25f;

        [Header("Lost Target")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Tempo de jogo sem readquirir Target após perder contato visual antes de pedir a seleção normal de Desejos (s). O valor inicial de 5 s é provisório e deve ser calibrado por missão.")]
        private float lostContactEvadedIntervalSeconds = 5f;

        [Header("Scene View")]
        [SerializeField]
        [Tooltip("Desenha Área Percebida, Cone de Visão e o resultado recente na Scene View.")]
        private bool drawPerceptionGizmos = true;

        [SerializeField]
        [Tooltip("Desenha os Gizmos mesmo quando este NPC não está selecionado.")]
        private bool drawGizmosWhenUnselected;

        [System.NonSerialized]
        private ActorOccurrenceReferenceBinding _actorReference;

        [System.NonSerialized]
        private PEGANpcKnowledgeState _knowledgeState;

        [System.NonSerialized]
        private PerceptionSensorSet _perceptionSensorSet;

        [System.NonSerialized]
        private RadialPresenceSensor _areaPresenceSensor;

        [System.NonSerialized]
        private VisionConeSensor _visionConeSensor;

        [System.NonSerialized]
        private Immersive.Logging.Loggers.ScopedLogger _log;

        [System.NonSerialized]
        private bool _isConfigured;

        [System.NonSerialized]
        private double _nextObservationTimeSeconds;

        [System.NonSerialized]
        private string _lastTargetDiagnostic;

        [System.NonSerialized]
        private EventBus<PEGANpcPlanReevaluationRequestedEvent> _planReevaluationEvents;

        [System.NonSerialized]
        private Coroutine _lostContactReevaluationRoutine;

        [System.NonSerialized]
        private ActorId _lostContactSubjectActorId;

        [System.NonSerialized]
        private double _lostContactObservationTimeSeconds;

        [System.NonSerialized]
        private bool _hasLostContactEpisode;

        [System.NonSerialized]
        private bool _evadedRequestPublishedForLostContact;

#if UNITY_EDITOR
        internal float LostContactEvadedIntervalSeconds => lostContactEvadedIntervalSeconds;
#endif

#if UNITY_EDITOR
        private PEGANpcPerceptionGizmoState _gizmoState;

        public readonly struct PEGANpcPerceptionGizmoState
        {
            internal PEGANpcPerceptionGizmoState(
                bool hasSample,
                bool hasTarget,
                Vector3 origin,
                Vector3 targetAimPoint,
                bool isAreaOccluded,
                bool isVisionOccluded,
                bool isWithinPerceivedArea,
                bool isInsideVisionCone,
                bool isAreaPresencePerceived,
                bool hasVisualContact)
            {
                HasSample = hasSample;
                HasTarget = hasTarget;
                Origin = origin;
                TargetAimPoint = targetAimPoint;
                IsAreaOccluded = isAreaOccluded;
                IsVisionOccluded = isVisionOccluded;
                IsWithinPerceivedArea = isWithinPerceivedArea;
                IsInsideVisionCone = isInsideVisionCone;
                IsAreaPresencePerceived = isAreaPresencePerceived;
                HasVisualContact = hasVisualContact;
            }

            public bool HasSample { get; }
            public bool HasTarget { get; }
            public Vector3 Origin { get; }
            public Vector3 TargetAimPoint { get; }
            public bool IsAreaOccluded { get; }
            public bool IsVisionOccluded { get; }
            public bool IsWithinPerceivedArea { get; }
            public bool IsInsideVisionCone { get; }
            public bool IsAreaPresencePerceived { get; }
            public bool HasVisualContact { get; }
        }

        public float PerceivedAreaRadiusMeters
        {
            get
            {
                ResolveSensorsForGizmos();
                return _areaPresenceSensor != null ? _areaPresenceSensor.RadiusMeters : 0f;
            }
        }

        public float VisionRangeMeters
        {
            get
            {
                ResolveSensorsForGizmos();
                return _visionConeSensor != null ? _visionConeSensor.RangeMeters : 0f;
            }
        }

        public float VisionFieldOfViewDegrees
        {
            get
            {
                ResolveSensorsForGizmos();
                return _visionConeSensor != null ? _visionConeSensor.FieldOfViewDegrees : 0f;
            }
        }

        public Vector3 VisionForward
        {
            get
            {
                ResolveSensorsForGizmos();
                return _visionConeSensor != null
                    ? _visionConeSensor.GetForward(transform)
                    : transform.forward;
            }
        }

        public Vector3 PerceivedAreaOrigin
        {
            get
            {
                ResolveSensorsForGizmos();
                return _areaPresenceSensor != null
                    ? _areaPresenceSensor.GetObserverOrigin(transform)
                    : transform.position;
            }
        }

        public Vector3 VisionOrigin
        {
            get
            {
                ResolveSensorsForGizmos();
                return _visionConeSensor != null
                    ? _visionConeSensor.GetObserverOrigin(transform)
                    : transform.position;
            }
        }

        public bool DrawPerceptionGizmos => drawPerceptionGizmos;
        public bool DrawGizmosWhenUnselected => drawGizmosWhenUnselected;
        public PEGANpcPerceptionGizmoState LatestGizmoState => _gizmoState;
#endif

        private void Awake()
        {
            _log = PEGALogging.CreateScopedLogger<PEGANpcPerceptionSource>(
                LogCategory,
                gameObject.name);

            _actorReference = GetComponent<ActorOccurrenceReferenceBinding>();
            _knowledgeState = GetComponent<PEGANpcKnowledgeState>();

            if (!TryValidateConfiguration(out string issue))
            {
                _log.Error(
                    $"{nameof(PEGANpcPerceptionSource)} requires the Activity observer, target Actor binding, Knowledge State and radial and vision sensor assets.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("diagnostic", issue));
                enabled = false;
                return;
            }

            _isConfigured = true;
            _nextObservationTimeSeconds = Time.timeAsDouble;
        }

        private void LateUpdate()
        {
            if (!_isConfigured)
            {
                return;
            }

            double observationTimeSeconds = Time.timeAsDouble;
            if (observationTimeSeconds < _nextObservationTimeSeconds)
            {
                return;
            }

            _nextObservationTimeSeconds = observationTimeSeconds + observationIntervalSeconds;
            ObserveCurrentPlayer(observationTimeSeconds);
        }

        private void OnEnable()
        {
            if (!_isConfigured)
            {
                return;
            }

            double observationTimeSeconds = Time.timeAsDouble;
            _nextObservationTimeSeconds = observationTimeSeconds + observationIntervalSeconds;
            ObserveCurrentPlayer(observationTimeSeconds);
            ResumeLostContactReevaluationIfNeeded();
        }

        private void OnDisable()
        {
            StopLostContactReevaluationRoutine();
        }

        /// <summary>Assina pedidos de reavaliação relacionados ao alvo observado por este NPC.</summary>
        /// <remarks>
        /// A entrega é síncrona, após consolidar uma readquisição ou após expirar a janela configurada
        /// de perda de contato. O consumidor avalia os planos; este evento não escolhe nem executa Actions.
        /// </remarks>
        public IEventBinding SubscribeToPlanReevaluationRequests(
            Action<PEGANpcPlanReevaluationRequestedEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsurePlanReevaluationEventsInitialized();
            return _planReevaluationEvents.Subscribe(request =>
            {
                try
                {
                    handler(request);
                }
                catch (Exception exception)
                {
                    LogPlanReevaluationSubscriberFailure(handler, request, exception);
                }
            });
        }

        public bool UnsubscribeFromPlanReevaluationRequests(IEventBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            return _planReevaluationEvents != null && _planReevaluationEvents.Unsubscribe(binding);
        }

        private void OnDestroy()
        {
            StopLostContactReevaluationRoutine();
            _planReevaluationEvents?.Clear();
            _planReevaluationEvents = null;
        }

        public bool TryGetCurrentTargetActorId(out ActorId actorId, out string diagnostic)
        {
            actorId = default;
            if (_actorReference == null ||
                !_actorReference.TryGetCurrent(out ActorOccurrenceResolution resolution) ||
                !resolution.Succeeded || !resolution.Occurrence.HasPlayerSlot)
            {
                diagnostic = "The authored Player Slot does not currently resolve to a Player Actor occurrence.";
                return false;
            }

            actorId = resolution.Occurrence.ActorId;
            diagnostic = actorId.IsValid ? string.Empty : "The current Player Actor occurrence has no valid ActorId.";
            return actorId.IsValid;
        }

        public bool TryBindActivityObserverBeforeActivation(
            PlayerSessionObserver observer,
            out string diagnostic)
        {
            if (observer == null)
            {
                diagnostic = "The current Activity Player Session observer is required before Perception activation.";
                return false;
            }

            if (_isConfigured || isActiveAndEnabled || gameObject.activeInHierarchy)
            {
                diagnostic = "The Activity observer can only be bound before this Perception Source is activated.";
                return false;
            }

            playerSessionObserver = observer;
            diagnostic = string.Empty;
            return true;
        }

        private bool TryValidateConfiguration(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_actorReference == null)
            {
                diagnostic = $"{nameof(ActorOccurrenceReferenceBinding)} is missing from the NPC root.";
                return false;
            }

            if (_knowledgeState == null)
            {
                diagnostic = $"{nameof(PEGANpcKnowledgeState)} is missing from the NPC root.";
                return false;
            }

            if (playerSessionObserver == null)
            {
                diagnostic = $"{nameof(PlayerSessionObserver)} must reference the current Activity observer.";
                return false;
            }

            if (!TryResolveSensors(out diagnostic))
            {
                return false;
            }

            if (!_perceptionSensorSet.TryValidateComposition(out diagnostic))
            {
                return false;
            }

            if (!_areaPresenceSensor.TryValidateConfiguration(out diagnostic))
            {
                diagnostic = $"Radial presence sensor configuration is invalid: {diagnostic}";
                return false;
            }

            if (!_visionConeSensor.TryValidateConfiguration(out diagnostic))
            {
                diagnostic = $"Vision cone sensor configuration is invalid: {diagnostic}";
                return false;
            }

            if (!IsPositiveFinite(observationIntervalSeconds) ||
                !IsPositiveFinite(lostContactEvadedIntervalSeconds))
            {
                diagnostic = "Sampling interval and lost-contact interval must be finite and positive.";
                return false;
            }

            if (!_actorReference.TryGetReference(out ActorOccurrenceReference reference, out diagnostic) ||
                !reference.IsValid)
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "NPC actor binding must reference a valid Player Slot."
                    : diagnostic;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryResolveSensors(out string diagnostic)
        {
            _areaPresenceSensor = perceivedAreaSensor;
            _visionConeSensor = visionConeSensor;

            if (_areaPresenceSensor == null)
            {
                diagnostic = "Radial presence sensor asset is not assigned.";
                return false;
            }

            if (_visionConeSensor == null)
            {
                diagnostic = "Vision cone sensor asset is not assigned.";
                return false;
            }

            if (_perceptionSensorSet == null)
            {
                _perceptionSensorSet = new PerceptionSensorSet(
                    transform,
                    new PerceptionSensorDefinition[]
                    {
                        _areaPresenceSensor,
                        _visionConeSensor
                    });
            }

            diagnostic = string.Empty;
            return true;
        }

        private void ResolveSensorsForGizmos()
        {
            if (_perceptionSensorSet != null &&
                _areaPresenceSensor != null &&
                _visionConeSensor != null)
            {
                return;
            }

            TryResolveSensors(out _);
        }

        private void ObserveCurrentPlayer(double observationTimeSeconds)
        {
            if (!_actorReference.TryGetCurrent(out ActorOccurrenceResolution resolution) ||
                !resolution.Succeeded)
            {
                SetTargetDiagnostic("Player Slot occurrence is not currently resolved.");
                SetGizmoUnavailable();
                return;
            }

            ActorOccurrence occurrence = resolution.Occurrence;
            if (!occurrence.HasPlayerSlot)
            {
                SetTargetDiagnostic("Resolved Actor occurrence does not belong to a Player Slot.");
                SetGizmoUnavailable();
                return;
            }

            if (!TryResolveCurrentProfile(occurrence, out ActorProfileId currentProfileId, out string diagnostic))
            {
                SetTargetDiagnostic(diagnostic);
                SetGizmoUnavailable();
                return;
            }

            CharacterController targetController = occurrence.ActorRoot.GetComponent<CharacterController>();
            if (targetController == null || !targetController.enabled || !occurrence.ActorRoot.activeInHierarchy)
            {
                SetTargetDiagnostic("Resolved Player Actor root requires an active CharacterController.");
                SetGizmoUnavailable();
                return;
            }

            SetTargetDiagnostic(string.Empty);
            EvaluateObservation(
                occurrence,
                targetController.bounds.center,
                currentProfileId,
                observationTimeSeconds);
        }

        private bool TryResolveCurrentProfile(
            ActorOccurrence occurrence,
            out ActorProfileId profileId,
            out string diagnostic)
        {
            profileId = default;
            if (playerSessionObserver == null ||
                !playerSessionObserver.TryGetObservation(out PlayerSessionScopedObservationSnapshot observation))
            {
                diagnostic = "Player Session observation is currently unavailable.";
                return false;
            }

            for (int index = 0; index < observation.Slots.Count; index++)
            {
                PlayerSessionScopedSlotObservation slot = observation.Slots[index];
                if (!slot.Slot.PlayerSlotId.Equals(occurrence.PlayerSlotId))
                {
                    continue;
                }

                if (!slot.IsJoined ||
                    !slot.HasCurrentActorEvidence ||
                    !slot.CurrentActor.HasCurrentActor)
                {
                    diagnostic = "Player Slot does not have current Actor correlation evidence.";
                    return false;
                }

                PlayerActorCorrelationEvidence evidence = slot.CurrentActor.ActorEvidence;
                if (!evidence.IsValid ||
                    !evidence.PlayerSlotId.Equals(occurrence.PlayerSlotId) ||
                    !evidence.ActorId.Equals(occurrence.ActorId) ||
                    !evidence.Owner.Equals(occurrence.Owner) ||
                    !slot.Slot.SelectedActorProfileId.Equals(evidence.ActorProfileId))
                {
                    diagnostic = "Resolved Actor occurrence does not match current Player Slot identity and owner evidence.";
                    return false;
                }

                profileId = evidence.ActorProfileId;
                diagnostic = string.Empty;
                return profileId.IsValid;
            }

            diagnostic = "Player Session observation has no matching Player Slot.";
            return false;
        }

        private void EvaluateObservation(
            ActorOccurrence occurrence,
            Vector3 targetAimPoint,
            ActorProfileId observedProfileId,
            double observationTimeSeconds)
        {
            if (_perceptionSensorSet == null)
            {
                SetTargetDiagnostic("Perception Sensor Set is unavailable.");
                SetGizmoUnavailable();
                return;
            }

            if (!_perceptionSensorSet.TryObserveAll(
                    targetAimPoint,
                    observationTimeSeconds,
                    out IReadOnlyList<PerceptionSensorResult> sensorResults,
                    out string diagnostic))
            {
                SetTargetDiagnostic(string.IsNullOrWhiteSpace(diagnostic)
                    ? "Perception Sensor Set could not evaluate the configured sensors."
                    : diagnostic);
                SetGizmoUnavailable();
                return;
            }

            if (!TryGetSensorObservation(
                    sensorResults,
                    _areaPresenceSensor,
                    out PerceptionSensorObservation areaObservation,
                    out diagnostic))
            {
                SetTargetDiagnostic(diagnostic);
                SetGizmoUnavailable();
                return;
            }

            if (!TryGetSensorObservation(
                    sensorResults,
                    _visionConeSensor,
                    out PerceptionSensorObservation visionObservation,
                    out diagnostic))
            {
                SetTargetDiagnostic(diagnostic);
                SetGizmoUnavailable();
                return;
            }

            Vector3 origin = visionObservation.ObserverOrigin;
            float planarDistance = areaObservation.PlanarDistanceMeters;
            bool withinPerceivedArea = areaObservation.IsWithinSensorBounds;
            bool insideVisionCone = visionObservation.IsWithinSensorBounds;
            bool isAreaPresencePerceived = areaObservation.IsDetected;
            bool hasVisualContact = visionObservation.IsDetected;

#if UNITY_EDITOR
            _gizmoState = new PEGANpcPerceptionGizmoState(
                true,
                true,
                origin,
                targetAimPoint,
                areaObservation.IsOccluded,
                visionObservation.IsOccluded,
                withinPerceivedArea,
                insideVisionCone,
                isAreaPresencePerceived,
                hasVisualContact);
#endif

            PEGANpcKnowledgeFact<PEGANpcPerceivedAreaPresence> previousArea =
                _knowledgeState.GetPerceivedAreaPresence();
            PEGANpcKnowledgeFact<bool> previousContact =
                _knowledgeState.GetCharacterVisualContact(occurrence.ActorId);
            bool hadPreviousContact = previousContact.TryGetValue(out bool previousVisualContact);
            bool isTargetReacquisition = hasVisualContact &&
                                       hadPreviousContact &&
                                       !previousVisualContact;
            bool isTargetContactLoss = !hasVisualContact &&
                                     hadPreviousContact &&
                                     previousVisualContact;
            PublishAreaPresenceIfChanged(isAreaPresencePerceived, observationTimeSeconds);

            if (hasVisualContact)
            {
                PublishIdentityIfChanged(
                    occurrence.ActorId,
                    observedProfileId,
                    observationTimeSeconds,
                    isTargetReacquisition);

                PEGANpcKnowledgeFact<Vector3> lastPosition =
                    _knowledgeState.GetLastObservedCharacterPosition(occurrence.ActorId);
                Vector3 observedRootPosition = occurrence.ActorRoot.transform.position;
                if (isTargetReacquisition ||
                    !lastPosition.TryGetValue(out Vector3 previousPosition) ||
                    !previousPosition.Equals(observedRootPosition))
                {
                    _knowledgeState.ObserveCharacterPosition(
                        occurrence.ActorId,
                        observedRootPosition,
                        PEGANpcEvidenceOrigin.Perception,
                        observationTimeSeconds);
                }

                PublishTargetContactIfChanged(occurrence.ActorId, true, observationTimeSeconds);
            }
            else
            {
                PublishTargetContactIfChanged(occurrence.ActorId, false, observationTimeSeconds);
            }

            LogPerceptionTransition(
                occurrence,
                previousArea,
                previousContact,
                isAreaPresencePerceived,
                hasVisualContact,
                areaObservation.IsOccluded,
                visionObservation.IsOccluded,
                planarDistance,
                observationTimeSeconds);

            if (hasVisualContact)
            {
                CancelLostContactReevaluationAfterRealReacquisition(
                    occurrence.ActorId,
                    observationTimeSeconds);
            }
            else if (isTargetContactLoss)
            {
                BeginLostContactReevaluation(
                    occurrence.ActorId,
                    observationTimeSeconds);
            }

            if (isTargetReacquisition)
            {
                PublishPlanReevaluationRequestAfterReacquisition(
                    occurrence.ActorId,
                    observationTimeSeconds);
            }
        }

        private static bool TryGetSensorObservation(
            IReadOnlyList<PerceptionSensorResult> results,
            PerceptionSensorDefinition sensorDefinition,
            out PerceptionSensorObservation observation,
            out string diagnostic)
        {
            observation = default;
            if (sensorDefinition == null)
            {
                diagnostic = "A required sensor asset is no longer available.";
                return false;
            }

            for (int index = 0; index < results.Count; index++)
            {
                PerceptionSensorResult result = results[index];
                if (!ReferenceEquals(result.SensorDefinition, sensorDefinition))
                {
                    continue;
                }

                if (!result.HasObservation)
                {
                    diagnostic = $"Sensor '{sensorDefinition.SensorId}' did not produce an observation: {result.Diagnostic}";
                    return false;
                }

                observation = result.Observation;
                diagnostic = string.Empty;
                return true;
            }

            diagnostic = $"Perception Sensor Set returned no result for sensor '{sensorDefinition.SensorId}'.";
            return false;
        }

        private void PublishAreaPresenceIfChanged(bool isPresent, double observationTimeSeconds)
        {
            PEGANpcKnowledgeFact<PEGANpcPerceivedAreaPresence> presence =
                _knowledgeState.GetPerceivedAreaPresence();
            if (presence.TryGetValue(out PEGANpcPerceivedAreaPresence previousPresence) &&
                previousPresence.IsPresent == isPresent)
            {
                return;
            }

            if (presence.Status == PEGANpcKnowledgeStatus.Unknown && !isPresent)
            {
                return;
            }

            _knowledgeState.ObservePerceivedAreaPresence(
                new PEGANpcPerceivedAreaPresence(
                    isPresent,
                    isPresent ? transform.position : Vector3.zero,
                    _areaPresenceSensor.RadiusMeters),
                PEGANpcEvidenceOrigin.Perception,
                observationTimeSeconds);
        }

        private void PublishIdentityIfChanged(
            ActorId subjectActorId,
            ActorProfileId observedProfileId,
            double observationTimeSeconds,
            bool refreshEvidence)
        {
            PEGANpcKnowledgeFact<ActorProfileId> identity =
                _knowledgeState.GetObservedCharacterIdentity(subjectActorId);
            if (refreshEvidence ||
                !identity.TryGetValue(out ActorProfileId previousProfileId) ||
                !previousProfileId.Equals(observedProfileId))
            {
                _knowledgeState.ObserveCharacterIdentity(
                    subjectActorId,
                    observedProfileId,
                    PEGANpcEvidenceOrigin.Perception,
                    observationTimeSeconds);
            }
        }

        private void PublishTargetContactIfChanged(
            ActorId subjectActorId,
            bool hasVisualContact,
            double observationTimeSeconds)
        {
            PEGANpcKnowledgeFact<bool> contact = _knowledgeState.GetCharacterVisualContact(subjectActorId);
            if (contact.TryGetValue(out bool previousContact) && previousContact == hasVisualContact)
            {
                return;
            }

            if (contact.Status == PEGANpcKnowledgeStatus.Unknown && !hasVisualContact)
            {
                return;
            }

            _knowledgeState.ObserveCharacterVisualContact(
                subjectActorId,
                hasVisualContact,
                PEGANpcEvidenceOrigin.Perception,
                observationTimeSeconds);
        }

        private void PublishPlanReevaluationRequestAfterReacquisition(
            ActorId subjectActorId,
            double observationTimeSeconds)
        {
            PEGANpcKnowledgeFact<ActorProfileId> identity =
                _knowledgeState.GetObservedCharacterIdentity(subjectActorId);
            PEGANpcKnowledgeFact<bool> contact =
                _knowledgeState.GetCharacterVisualContact(subjectActorId);
            PEGANpcKnowledgeFact<Vector3> position =
                _knowledgeState.GetLastObservedCharacterPosition(subjectActorId);

            bool hasConsolidatedEvidence =
                identity.TryGetValue(out _) &&
                HasCurrentPerceptionEvidence(identity.Origins, identity.HasObservationTime,
                    identity.ObservationTimeSeconds, observationTimeSeconds) &&
                contact.TryGetValue(out bool hasVisualContact) &&
                hasVisualContact &&
                HasCurrentPerceptionEvidence(contact.Origins, contact.HasObservationTime,
                    contact.ObservationTimeSeconds, observationTimeSeconds) &&
                position.TryGetValue(out _) &&
                HasCurrentPerceptionEvidence(position.Origins, position.HasObservationTime,
                    position.ObservationTimeSeconds, observationTimeSeconds);

            if (!hasConsolidatedEvidence)
            {
                _log.Warning(
                    "NPC plan re-evaluation request was withheld because Target reacquisition evidence was not fully committed.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", subjectActorId.StableText),
                    LogFields.Field("observationTimeSeconds", observationTimeSeconds),
                    LogFields.Field("identityStatus", identity.Status),
                    LogFields.Field("contactStatus", contact.Status),
                    LogFields.Field("positionStatus", position.Status));
                return;
            }

            EnsurePlanReevaluationEventsInitialized();
            var request = new PEGANpcPlanReevaluationRequestedEvent(
                PEGANpcPlanReevaluationTrigger.TargetReacquired,
                subjectActorId,
                observationTimeSeconds,
                observationTimeSeconds);

            try
            {
                int deliveredCount = _planReevaluationEvents.Publish(request);
                _log.Debug(
                    "NPC plan re-evaluation requested after consolidated real Target reacquisition.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", subjectActorId.StableText),
                    LogFields.Field("trigger", request.Trigger),
                    LogFields.Field("observationTimeSeconds", observationTimeSeconds),
                    LogFields.Field("deliveredCount", deliveredCount));
            }
            catch (Exception exception)
            {
                _log.Error(
                    "NPC plan re-evaluation event publication failed after reacquisition was committed.",
                    exception,
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", subjectActorId.StableText),
                    LogFields.Field("observationTimeSeconds", observationTimeSeconds));
            }
        }

        private static bool HasCurrentPerceptionEvidence(
            PEGANpcEvidenceOrigin origins,
            bool hasObservationTime,
            double evidenceTimeSeconds,
            double observationTimeSeconds) =>
            (origins & PEGANpcEvidenceOrigin.Perception) != 0 &&
            hasObservationTime &&
            Math.Abs(evidenceTimeSeconds - observationTimeSeconds) <= 0.000001d;

        private void BeginLostContactReevaluation(
            ActorId subjectActorId,
            double lossObservationTimeSeconds)
        {
            if (_hasLostContactEpisode)
            {
                return;
            }

            if (!HasCurrentPerceptionContact(subjectActorId, false, lossObservationTimeSeconds))
            {
                _log.Warning(
                    "Lost-contact reevaluation was not scheduled because the false contact fact was not committed by this perception sample.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", subjectActorId.StableText),
                    LogFields.Field("observationTimeSeconds", lossObservationTimeSeconds));
                return;
            }

            _hasLostContactEpisode = true;
            _evadedRequestPublishedForLostContact = false;
            _lostContactSubjectActorId = subjectActorId;
            _lostContactObservationTimeSeconds = lossObservationTimeSeconds;
            StartLostContactReevaluationRoutine(lostContactEvadedIntervalSeconds);

            _log.Debug(
                "Configured Target reacquisition window started after real visual contact loss.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("targetActorId", subjectActorId.StableText),
                LogFields.Field("contactLossObservationTimeSeconds", lossObservationTimeSeconds),
                LogFields.Field("reevaluationDelaySeconds", lostContactEvadedIntervalSeconds),
                LogFields.Field("knowledgePreserved", true));
        }

        private void CancelLostContactReevaluationAfterRealReacquisition(
            ActorId subjectActorId,
            double observationTimeSeconds)
        {
            if (!_hasLostContactEpisode)
            {
                return;
            }

            bool cancelledPendingRequest = !_evadedRequestPublishedForLostContact;
            StopLostContactReevaluationRoutine();
            _hasLostContactEpisode = false;
            _evadedRequestPublishedForLostContact = false;
            _lostContactSubjectActorId = default;
            _lostContactObservationTimeSeconds = 0d;

            if (cancelledPendingRequest)
            {
                _log.Debug(
                    "Pending Target evasion reevaluation was cancelled by real visual reacquisition.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", subjectActorId.StableText),
                    LogFields.Field("reacquisitionObservationTimeSeconds", observationTimeSeconds));
            }
        }

        private void ResumeLostContactReevaluationIfNeeded()
        {
            if (!_hasLostContactEpisode ||
                _evadedRequestPublishedForLostContact ||
                _lostContactReevaluationRoutine != null ||
                !isActiveAndEnabled)
            {
                return;
            }

            if (!HasCurrentPerceptionContact(
                    _lostContactSubjectActorId,
                    false,
                    _lostContactObservationTimeSeconds))
            {
                _log.Warning(
                    "Pending Target evasion reevaluation was discarded because its real lost-contact evidence is no longer current.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", _lostContactSubjectActorId.StableText),
                    LogFields.Field("contactLossObservationTimeSeconds", _lostContactObservationTimeSeconds));
                ClearLostContactEpisode();
                return;
            }

            double elapsedSeconds = Math.Max(0d, Time.timeAsDouble - _lostContactObservationTimeSeconds);
            float remainingSeconds = (float)Math.Max(0d, lostContactEvadedIntervalSeconds - elapsedSeconds);
            StartLostContactReevaluationRoutine(remainingSeconds);
        }

        private void StartLostContactReevaluationRoutine(float delaySeconds)
        {
            StopLostContactReevaluationRoutine();
            _lostContactReevaluationRoutine = StartCoroutine(
                WaitForLostContactReevaluation(
                    _lostContactSubjectActorId,
                    _lostContactObservationTimeSeconds,
                    delaySeconds));
        }

        private IEnumerator WaitForLostContactReevaluation(
            ActorId subjectActorId,
            double lossObservationTimeSeconds,
            float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            _lostContactReevaluationRoutine = null;

            if (!_isConfigured ||
                !_hasLostContactEpisode ||
                _evadedRequestPublishedForLostContact ||
                !_lostContactSubjectActorId.Equals(subjectActorId) ||
                Math.Abs(_lostContactObservationTimeSeconds - lossObservationTimeSeconds) > 0.000001d)
            {
                yield break;
            }

            double requestedAtTimeSeconds = Time.timeAsDouble;

            // Fecha a janela com uma amostra física do mesmo sensor, sem injetar fatos nem consultar o planner.
            ObserveCurrentPlayer(requestedAtTimeSeconds);

            if (!_hasLostContactEpisode ||
                !_lostContactSubjectActorId.Equals(subjectActorId) ||
                Math.Abs(_lostContactObservationTimeSeconds - lossObservationTimeSeconds) > 0.000001d)
            {
                yield break;
            }

            if (!HasCurrentPerceptionContact(subjectActorId, false, lossObservationTimeSeconds))
            {
                _log.Warning(
                    "Target evasion reevaluation was withheld because the original real loss-of-contact evidence is no longer current.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", subjectActorId.StableText),
                    LogFields.Field("contactLossObservationTimeSeconds", lossObservationTimeSeconds),
                    LogFields.Field("requestedAtTimeSeconds", requestedAtTimeSeconds));
                _evadedRequestPublishedForLostContact = true;
                yield break;
            }

            _evadedRequestPublishedForLostContact = true;
            PublishPlanReevaluationRequestAfterLostContact(
                subjectActorId,
                lossObservationTimeSeconds,
                requestedAtTimeSeconds);
        }

        private bool HasCurrentPerceptionContact(
            ActorId subjectActorId,
            bool expectedContact,
            double expectedObservationTimeSeconds)
        {
            PEGANpcKnowledgeFact<bool> contact = _knowledgeState.GetCharacterVisualContact(subjectActorId);
            return contact.TryGetValue(out bool actualContact) &&
                   actualContact == expectedContact &&
                   (contact.Origins & PEGANpcEvidenceOrigin.Perception) != 0 &&
                   contact.HasObservationTime &&
                   Math.Abs(contact.ObservationTimeSeconds - expectedObservationTimeSeconds) <= 0.000001d;
        }

        private void PublishPlanReevaluationRequestAfterLostContact(
            ActorId subjectActorId,
            double lossObservationTimeSeconds,
            double requestedAtTimeSeconds)
        {
            if (!HasCurrentPerceptionContact(subjectActorId, false, lossObservationTimeSeconds))
            {
                _log.Warning(
                    "Target evasion reevaluation request was withheld because the real loss-of-contact evidence was not available at publication.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", subjectActorId.StableText),
                    LogFields.Field("contactLossObservationTimeSeconds", lossObservationTimeSeconds),
                    LogFields.Field("requestedAtTimeSeconds", requestedAtTimeSeconds));
                return;
            }

            EnsurePlanReevaluationEventsInitialized();
            var request = new PEGANpcPlanReevaluationRequestedEvent(
                PEGANpcPlanReevaluationTrigger.TargetContactLostBeyondWindow,
                subjectActorId,
                lossObservationTimeSeconds,
                requestedAtTimeSeconds);

            try
            {
                int deliveredCount = _planReevaluationEvents.Publish(request);
                _log.Debug(
                    "NPC normal Desire set reevaluation requested after Target remained outside real visual contact for the configured interval.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", subjectActorId.StableText),
                    LogFields.Field("trigger", request.Trigger),
                    LogFields.Field("evaluationScope", "NormalDesires"),
                    LogFields.Field("contactLossObservationTimeSeconds", lossObservationTimeSeconds),
                    LogFields.Field("requestedAtTimeSeconds", requestedAtTimeSeconds),
                    LogFields.Field("elapsedSeconds", requestedAtTimeSeconds - lossObservationTimeSeconds),
                    LogFields.Field("configuredDelaySeconds", lostContactEvadedIntervalSeconds),
                    LogFields.Field("knowledgePreserved", true),
                    LogFields.Field("deliveredCount", deliveredCount));
            }
            catch (Exception exception)
            {
                _log.Error(
                    "NPC normal Desire set reevaluation event publication failed after Target loss was confirmed.",
                    exception,
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", subjectActorId.StableText),
                    LogFields.Field("contactLossObservationTimeSeconds", lossObservationTimeSeconds),
                    LogFields.Field("requestedAtTimeSeconds", requestedAtTimeSeconds));
            }
        }

        private void ClearLostContactEpisode()
        {
            StopLostContactReevaluationRoutine();
            _hasLostContactEpisode = false;
            _evadedRequestPublishedForLostContact = false;
            _lostContactSubjectActorId = default;
            _lostContactObservationTimeSeconds = 0d;
        }

        private void StopLostContactReevaluationRoutine()
        {
            if (_lostContactReevaluationRoutine == null)
            {
                return;
            }

            StopCoroutine(_lostContactReevaluationRoutine);
            _lostContactReevaluationRoutine = null;
        }

        private void EnsurePlanReevaluationEventsInitialized()
        {
            _planReevaluationEvents ??= new EventBus<PEGANpcPlanReevaluationRequestedEvent>();
        }

        private void LogPlanReevaluationSubscriberFailure(
            Action<PEGANpcPlanReevaluationRequestedEvent> handler,
            PEGANpcPlanReevaluationRequestedEvent request,
            Exception exception)
        {
            if (_log == null)
            {
                _log = PEGALogging.CreateScopedLogger<PEGANpcPerceptionSource>(
                    LogCategory,
                    gameObject.name);
            }

            _log.Error(
                "NPC plan re-evaluation subscriber failed; the committed perception and other subscribers remain isolated.",
                exception,
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("targetActorId", request.TargetActorId.StableText),
                LogFields.Field("trigger", request.Trigger),
                LogFields.Field("subscriberMethod", handler.Method.Name));
        }

        private void LogPerceptionTransition(
            ActorOccurrence occurrence,
            PEGANpcKnowledgeFact<PEGANpcPerceivedAreaPresence> previousArea,
            PEGANpcKnowledgeFact<bool> previousContact,
            bool isAreaPresencePerceived,
            bool hasVisualContact,
            bool isAreaOccluded,
            bool isVisionOccluded,
            float planarDistance,
            double observationTimeSeconds)
        {
            if (_log == null)
            {
                return;
            }

            bool hadAreaPresence = previousArea.TryGetValue(
                out PEGANpcPerceivedAreaPresence previousAreaPresence);
            if (isAreaPresencePerceived && (!hadAreaPresence || !previousAreaPresence.IsPresent) ||
                !isAreaPresencePerceived && hadAreaPresence && previousAreaPresence.IsPresent)
            {
                _log.Debug(
                    "NPC radial area perception state changed.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", occurrence.ActorId.StableText),
                    LogFields.Field("perceivedAreaPresence", isAreaPresencePerceived),
                    LogFields.Field("occluded", isAreaOccluded),
                    LogFields.Field("planarDistanceMeters", planarDistance),
                    LogFields.Field("observationTimeSeconds", observationTimeSeconds));
            }

            bool hadContact = previousContact.TryGetValue(out bool previousVisualContact);
            if (hasVisualContact && (!hadContact || !previousVisualContact) ||
                !hasVisualContact && hadContact && previousVisualContact)
            {
                _log.Debug(
                    "NPC visual contact state changed.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", occurrence.ActorId.StableText),
                    LogFields.Field("visualContact", hasVisualContact),
                    LogFields.Field("occluded", isVisionOccluded),
                    LogFields.Field("planarDistanceMeters", planarDistance),
                    LogFields.Field("observationTimeSeconds", observationTimeSeconds));
                _log.Info(
                    hasVisualContact
                        ? "NPC acquired visual contact with Target."
                        : "NPC lost visual contact with Target.",
                    LogFields.Field("actorRoot", gameObject.name));
            }
        }

        private void SetTargetDiagnostic(string diagnostic)
        {
            if (string.IsNullOrEmpty(diagnostic))
            {
                _lastTargetDiagnostic = string.Empty;
                return;
            }

            if (string.Equals(_lastTargetDiagnostic, diagnostic, System.StringComparison.Ordinal))
            {
                return;
            }

            _lastTargetDiagnostic = diagnostic;
            _log.Warning(
                "NPC perception sample was skipped because current target evidence is unavailable.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("diagnostic", diagnostic));
        }

        private void SetGizmoUnavailable()
        {
#if UNITY_EDITOR
            _gizmoState = new PEGANpcPerceptionGizmoState(
                false,
                false,
                GetObserverOrigin(),
                Vector3.zero,
                false,
                false,
                false,
                false,
                false,
                false);
#endif
        }

        private Vector3 GetObserverOrigin() =>
            _areaPresenceSensor != null
                ? _areaPresenceSensor.GetObserverOrigin(transform)
                : transform.position;

        private static bool IsPositiveFinite(float value) =>
            value > 0f && IsFinite(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

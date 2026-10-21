using Immersive.Framework.Actors;
using Immersive.Framework.PlayerParticipation;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// Production runtime perception for the first Rick pursuit slice.
    /// It writes only observations authorized by radial presence or visual confirmation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorOccurrenceReferenceBinding))]
    [RequireComponent(typeof(PEGANpcKnowledgeState))]
    [AddComponentMenu("PEGA/NPC/Perception Source")]
    public sealed class PEGANpcPerceptionSource : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.Perception";
        private const float MinimumPlanarMagnitude = 0.0001f;

        [Header("Rick target")]
        [SerializeField]
        [Tooltip("Observador da Player Session no escopo da Activity que contém este NPC.")]
        private PlayerSessionObserver playerSessionObserver;

        [SerializeField]
        [Tooltip("Perfil canônico usado para limitar esta etapa da percepção à perseguição de Rick.")]
        private ActorProfile rickActorProfile;

        [Header("Perceived Area")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Raio horizontal da Área Percebida (m). Ela informa presença, sem identidade ou posição.")]
        private float perceivedAreaRadiusMeters = 6f;

        [Header("Vision Cone")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Alcance horizontal máximo do Cone de Visão (m).")]
        private float visionRangeMeters = 10f;

        [SerializeField, Range(1f, 180f)]
        [Tooltip("Ângulo total do Cone de Visão à frente do NPC (°).")]
        private float visionFieldOfViewDegrees = 100f;

        [SerializeField, Min(0f)]
        [Tooltip("Altura da origem de observação acima da raiz do NPC (m).")]
        private float observerOriginHeightMeters = 1.55f;

        [Header("Occlusion and sampling")]
        [SerializeField]
        [Tooltip("Somente colliders nesta máscara bloqueiam percepção. Inclua a layer PEGA_Obstacle.")]
        private LayerMask occlusionLayers = 1 << 6;

        [SerializeField, Min(0.05f)]
        [Tooltip("Intervalo entre amostras físicas da percepção (s).")]
        private float observationIntervalSeconds = 0.25f;

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
        private Immersive.Logging.Loggers.ScopedLogger _log;

        [System.NonSerialized]
        private ActorProfileId _rickActorProfileId;

        [System.NonSerialized]
        private bool _isConfigured;

        [System.NonSerialized]
        private double _nextObservationTimeSeconds;

        [System.NonSerialized]
        private string _lastTargetDiagnostic;

#if UNITY_EDITOR
        private PEGANpcPerceptionGizmoState _gizmoState;

        public readonly struct PEGANpcPerceptionGizmoState
        {
            internal PEGANpcPerceptionGizmoState(
                bool hasSample,
                bool hasRickTarget,
                Vector3 origin,
                Vector3 targetAimPoint,
                bool isOccluded,
                bool isWithinPerceivedArea,
                bool isInsideVisionCone,
                bool isAreaPresencePerceived,
                bool hasVisualContact)
            {
                HasSample = hasSample;
                HasRickTarget = hasRickTarget;
                Origin = origin;
                TargetAimPoint = targetAimPoint;
                IsOccluded = isOccluded;
                IsWithinPerceivedArea = isWithinPerceivedArea;
                IsInsideVisionCone = isInsideVisionCone;
                IsAreaPresencePerceived = isAreaPresencePerceived;
                HasVisualContact = hasVisualContact;
            }

            public bool HasSample { get; }
            public bool HasRickTarget { get; }
            public Vector3 Origin { get; }
            public Vector3 TargetAimPoint { get; }
            public bool IsOccluded { get; }
            public bool IsWithinPerceivedArea { get; }
            public bool IsInsideVisionCone { get; }
            public bool IsAreaPresencePerceived { get; }
            public bool HasVisualContact { get; }
        }

        public float PerceivedAreaRadiusMeters => perceivedAreaRadiusMeters;
        public float VisionRangeMeters => visionRangeMeters;
        public float VisionFieldOfViewDegrees => visionFieldOfViewDegrees;
        public float ObserverOriginHeightMeters => observerOriginHeightMeters;
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

            if (!TryValidateConfiguration(out string issue) ||
                !rickActorProfile.TryGetActorProfileId(out _rickActorProfileId, out issue))
            {
                _log.Error(
                    $"{nameof(PEGANpcPerceptionSource)} requires valid Rick profile, Player Session observer, actor binding, Knowledge State and perception settings.",
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

        internal bool TryGetRickProfileId(out ActorProfileId profileId, out string diagnostic)
        {
            profileId = default;
            if (rickActorProfile == null)
            {
                diagnostic = "Rick Actor Profile reference is not assigned.";
                return false;
            }

            return rickActorProfile.TryGetActorProfileId(out profileId, out diagnostic);
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

            if (rickActorProfile == null ||
                !rickActorProfile.TryGetActorProfileId(out ActorProfileId profileId, out diagnostic) ||
                !profileId.IsValid)
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "Rick Actor Profile must provide a valid ActorProfileId."
                    : diagnostic;
                return false;
            }

            if (!IsPositiveFinite(perceivedAreaRadiusMeters) ||
                !IsPositiveFinite(visionRangeMeters) ||
                !IsFinite(visionFieldOfViewDegrees) ||
                visionFieldOfViewDegrees <= 0f || visionFieldOfViewDegrees > 180f ||
                !IsNonNegativeFinite(observerOriginHeightMeters) ||
                !IsPositiveFinite(observationIntervalSeconds) ||
                occlusionLayers.value == 0)
            {
                diagnostic = "Area radius, vision range, field of view, origin height, sample interval and occlusion mask must be valid and finite.";
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

            if (!currentProfileId.Equals(_rickActorProfileId))
            {
                SetTargetDiagnostic(string.Empty);
                SetGizmoUnavailable();
                return;
            }

            CharacterController targetController = occurrence.ActorRoot.GetComponent<CharacterController>();
            if (targetController == null || !targetController.enabled || !occurrence.ActorRoot.activeInHierarchy)
            {
                SetTargetDiagnostic("Resolved Rick Actor root requires an active CharacterController.");
                SetGizmoUnavailable();
                return;
            }

            SetTargetDiagnostic(string.Empty);
            EvaluateObservation(
                occurrence,
                targetController.bounds.center,
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
            double observationTimeSeconds)
        {
            Vector3 origin = transform.position + Vector3.up * observerOriginHeightMeters;
            Vector3 planarToTarget = targetAimPoint - origin;
            planarToTarget.y = 0f;
            float planarDistance = planarToTarget.magnitude;
            bool withinPerceivedArea = planarDistance <= perceivedAreaRadiusMeters;

            Vector3 planarForward = transform.forward;
            planarForward.y = 0f;
            bool hasForward = planarForward.sqrMagnitude > MinimumPlanarMagnitude * MinimumPlanarMagnitude;
            planarForward = hasForward ? planarForward.normalized : Vector3.forward;
            bool insideVisionCone = hasForward &&
                planarDistance <= visionRangeMeters &&
                planarDistance > MinimumPlanarMagnitude &&
                Vector3.Angle(planarForward, planarToTarget) <= visionFieldOfViewDegrees * 0.5f;

            bool occluded = false;
            if (withinPerceivedArea || insideVisionCone)
            {
                Vector3 toTarget = targetAimPoint - origin;
                float distance = toTarget.magnitude;
                if (distance > MinimumPlanarMagnitude)
                {
                    occluded = Physics.Raycast(
                        origin,
                        toTarget / distance,
                        distance,
                        occlusionLayers,
                        QueryTriggerInteraction.Collide);
                }
            }

            bool isAreaPresencePerceived = withinPerceivedArea && !occluded;
            bool hasVisualContact = insideVisionCone && !occluded;

#if UNITY_EDITOR
            _gizmoState = new PEGANpcPerceptionGizmoState(
                true,
                true,
                origin,
                targetAimPoint,
                occluded,
                withinPerceivedArea,
                insideVisionCone,
                isAreaPresencePerceived,
                hasVisualContact);
#endif

            PEGANpcKnowledgeFact<bool> previousArea = _knowledgeState.GetPerceivedAreaPresence();
            PEGANpcKnowledgeFact<bool> previousContact =
                _knowledgeState.GetRickVisualContact(occurrence.ActorId);
            PublishAreaPresenceIfChanged(isAreaPresencePerceived, observationTimeSeconds);

            if (hasVisualContact)
            {
                PublishIdentityIfChanged(occurrence.ActorId, observationTimeSeconds);
                PublishRickContactIfChanged(occurrence.ActorId, true, observationTimeSeconds);

                PEGANpcKnowledgeFact<Vector3> lastPosition =
                    _knowledgeState.GetLastObservedRickPosition(occurrence.ActorId);
                Vector3 observedRootPosition = occurrence.ActorRoot.transform.position;
                if (!lastPosition.TryGetValue(out Vector3 previousPosition) ||
                    !previousPosition.Equals(observedRootPosition))
                {
                    _knowledgeState.ObserveRickPosition(
                        occurrence.ActorId,
                        observedRootPosition,
                        PEGANpcEvidenceOrigin.Perception,
                        observationTimeSeconds);
                }
            }
            else
            {
                PublishRickContactIfChanged(occurrence.ActorId, false, observationTimeSeconds);
            }

            LogPerceptionTransition(
                occurrence,
                previousArea,
                previousContact,
                isAreaPresencePerceived,
                hasVisualContact,
                occluded,
                planarDistance,
                observationTimeSeconds);
        }

        private void PublishAreaPresenceIfChanged(bool isPresent, double observationTimeSeconds)
        {
            PEGANpcKnowledgeFact<bool> presence = _knowledgeState.GetPerceivedAreaPresence();
            if (presence.TryGetValue(out bool previousPresence) && previousPresence == isPresent)
            {
                return;
            }

            if (presence.Status == PEGANpcKnowledgeStatus.Unknown && !isPresent)
            {
                return;
            }

            _knowledgeState.ObservePerceivedAreaPresence(
                isPresent,
                PEGANpcEvidenceOrigin.Perception,
                observationTimeSeconds);
        }

        private void PublishIdentityIfChanged(ActorId subjectActorId, double observationTimeSeconds)
        {
            PEGANpcKnowledgeFact<ActorProfileId> identity =
                _knowledgeState.GetObservedCharacterIdentity(subjectActorId);
            if (!identity.TryGetValue(out ActorProfileId observedProfileId) ||
                !observedProfileId.Equals(_rickActorProfileId))
            {
                _knowledgeState.ObserveCharacterIdentity(
                    subjectActorId,
                    _rickActorProfileId,
                    PEGANpcEvidenceOrigin.Perception,
                    observationTimeSeconds);
            }
        }

        private void PublishRickContactIfChanged(
            ActorId subjectActorId,
            bool hasVisualContact,
            double observationTimeSeconds)
        {
            PEGANpcKnowledgeFact<bool> contact = _knowledgeState.GetRickVisualContact(subjectActorId);
            if (contact.TryGetValue(out bool previousContact) && previousContact == hasVisualContact)
            {
                return;
            }

            if (contact.Status == PEGANpcKnowledgeStatus.Unknown && !hasVisualContact)
            {
                return;
            }

            _knowledgeState.ObserveRickVisualContact(
                subjectActorId,
                hasVisualContact,
                PEGANpcEvidenceOrigin.Perception,
                observationTimeSeconds);
        }

        private void LogPerceptionTransition(
            ActorOccurrence occurrence,
            PEGANpcKnowledgeFact<bool> previousArea,
            PEGANpcKnowledgeFact<bool> previousContact,
            bool isAreaPresencePerceived,
            bool hasVisualContact,
            bool occluded,
            float planarDistance,
            double observationTimeSeconds)
        {
            if (_log == null)
            {
                return;
            }

            bool hadAreaPresence = previousArea.TryGetValue(out bool previousAreaPresence);
            if (isAreaPresencePerceived && (!hadAreaPresence || !previousAreaPresence) ||
                !isAreaPresencePerceived && hadAreaPresence && previousAreaPresence)
            {
                _log.Info(
                    "NPC radial area perception state changed.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", occurrence.ActorId.StableText),
                    LogFields.Field("perceivedAreaPresence", isAreaPresencePerceived),
                    LogFields.Field("occluded", occluded),
                    LogFields.Field("planarDistanceMeters", planarDistance),
                    LogFields.Field("observationTimeSeconds", observationTimeSeconds));
            }

            bool hadContact = previousContact.TryGetValue(out bool previousVisualContact);
            if (hasVisualContact && (!hadContact || !previousVisualContact) ||
                !hasVisualContact && hadContact && previousVisualContact)
            {
                _log.Info(
                    "NPC visual contact state changed.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", occurrence.ActorId.StableText),
                    LogFields.Field("visualContact", hasVisualContact),
                    LogFields.Field("occluded", occluded),
                    LogFields.Field("planarDistanceMeters", planarDistance),
                    LogFields.Field("observationTimeSeconds", observationTimeSeconds));
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
                false);
#endif
        }

        private Vector3 GetObserverOrigin() =>
            transform.position + Vector3.up * observerOriginHeightMeters;

        private static bool IsPositiveFinite(float value) =>
            value > 0f && IsFinite(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && IsFinite(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using Immersive.Framework.RuntimeContent;
using Immersive.Logging.Records;
using ImmersiveGames.Perception;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// PEGA adapter that acquires declared physical candidates, resolves Framework identities after sensor
    /// evaluation, and translates observations into Knowledge State facts and targeted Autonomy requests.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PEGANpcKnowledgeState))]
    [RequireComponent(typeof(PerceptionSensorSet))]
    [RequireComponent(typeof(ActorOccurrenceQueryBinding))]
    [RequireComponent(typeof(ObjectEntryOccurrenceQueryBinding))]
    [AddComponentMenu("PEGA/NPC/Perception Source")]
    public sealed class PEGANpcPerceptionSource : MonoBehaviour, IPEGANpcTargetObservationSource
    {
        private const string LogCategory = "PEGA.Gameplay.Perception";

        private readonly struct NearestFrameworkDeclarations
        {
            public NearestFrameworkDeclarations(
                Transform root,
                bool hasValidActorDeclaration,
                ObjectEntryDeclaration objectEntryDeclaration,
                ObjectEntryId objectEntryId,
                bool hasValidObjectEntryDeclaration)
            {
                Root = root;
                HasValidActorDeclaration = hasValidActorDeclaration;
                ObjectEntryDeclaration = objectEntryDeclaration;
                ObjectEntryId = objectEntryId;
                HasValidObjectEntryDeclaration = hasValidObjectEntryDeclaration;
            }

            public Transform Root { get; }
            public bool HasValidActorDeclaration { get; }
            public ObjectEntryDeclaration ObjectEntryDeclaration { get; }
            public ObjectEntryId ObjectEntryId { get; }
            public bool HasValidObjectEntryDeclaration { get; }
            public bool HasAnyValidDeclaration => HasValidActorDeclaration || HasValidObjectEntryDeclaration;
        }

        private readonly struct StaleActorContact
        {
            public StaleActorContact(ActorId actorId, ActorOccurrenceId occurrenceId)
            {
                ActorId = actorId;
                OccurrenceId = occurrenceId;
            }

            public ActorId ActorId { get; }
            public ActorOccurrenceId OccurrenceId { get; }
        }

        [Header("Gameplay Sensor Roles")]
        [SerializeField]
        [Tooltip("Instância radial que atualiza somente presença anônima no Knowledge State.")]
        private PerceptionSensorInstance perceivedAreaSensorInstance;

        [SerializeField]
        [Tooltip("Instância visual que atualiza contato e, quando positiva, identidade e posição.")]
        private PerceptionSensorInstance visionConeSensorInstance;

        [Header("Lost Target")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Tempo de jogo sem readquirir Target após perder contato visual antes de pedir a seleção normal de Desejos (s). O valor inicial de 5 s é provisório e deve ser calibrado por missão.")]
        private float lostContactEvadedIntervalSeconds = 5f;

        [Header("Scene View")]
        [SerializeField]
        [Tooltip("Off oculta a percepção; Resumido mostra limites reais, R (radial), V (visual), Aq (aquisição) e candidatos; Detalhado preserva estados, identidades e diagnósticos completos.")]
        private PEGADiagnosticVisualizationLevel perceptionVisualizationLevel =
            PEGADiagnosticVisualizationLevel.Summary;

        [SerializeField]
        [Tooltip("Desenha os Gizmos mesmo quando este NPC não está selecionado.")]
        private bool drawGizmosWhenUnselected;

        [System.NonSerialized]
        private ActorOccurrenceReferenceBinding _actorReference;

        [System.NonSerialized]
        private ActorDeclaration _actorDeclaration;

        [System.NonSerialized]
        private ActorOccurrenceQueryBinding _actorOccurrenceQuery;

        [System.NonSerialized]
        private IActorOccurrenceRecognitionQuery _actorOccurrenceRecognitionQuery;

        [System.NonSerialized]
        private ObjectEntryOccurrenceQueryBinding _objectEntryRecognitionQuery;

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

        [System.NonSerialized]
        private readonly Dictionary<ActorId, ActorOccurrenceReference> _recognizedActorReferences =
            new Dictionary<ActorId, ActorOccurrenceReference>();

        [System.NonSerialized]
        private readonly Dictionary<Collider, string> _candidateRecognitionLogStates =
            new Dictionary<Collider, string>();

        [System.NonSerialized]
        private readonly HashSet<Collider> _candidateCollidersThisSample = new HashSet<Collider>();

        [System.NonSerialized]
        private string _lastObserverOccurrenceDiagnostic;

        [System.NonSerialized]
        private ActorOccurrenceId _lastObserverOccurrenceId;

        [System.NonSerialized]
        private string _lastExternalTargetDiagnostic;

        [System.NonSerialized]
        private bool _hasLoggedAcquisitionState;

        [System.NonSerialized]
        private PerceptionAcquisitionStatus _lastAcquisitionStatus;

        [System.NonSerialized]
        private int _lastPhysicalCandidateCount = -1;

        [System.NonSerialized]
        private int _lastExternalCandidateCount = -1;

        [System.NonSerialized]
        private string _lastAcquisitionDiagnostic;

#if UNITY_EDITOR
        internal float LostContactEvadedIntervalSeconds => lostContactEvadedIntervalSeconds;
#endif

#if UNITY_EDITOR
        private PEGANpcPerceptionGizmoState _gizmoState;

        public readonly struct PEGANpcPerceptionSensorGizmoResult
        {
            internal PEGANpcPerceptionSensorGizmoResult(
                bool wasSampled,
                bool hasResult,
                bool hasOutcome,
                PerceptionObservationStatus status,
                bool hasObservation,
                PerceptionSensorObservation observation,
                string diagnostic)
            {
                WasSampled = wasSampled;
                HasResult = hasResult;
                HasOutcome = hasOutcome;
                Status = status;
                HasObservation = hasObservation;
                Observation = observation;
                Diagnostic = diagnostic ?? string.Empty;
            }

            public bool WasSampled { get; }
            public bool HasResult { get; }
            public bool HasOutcome { get; }
            public PerceptionObservationStatus Status { get; }
            public bool HasObservation { get; }
            public PerceptionSensorObservation Observation { get; }
            public string Diagnostic { get; }
        }

        public readonly struct PEGANpcPerceptionCandidateGizmoState
        {
            internal PEGANpcPerceptionCandidateGizmoState(
                int candidateIndex,
                PerceptionCandidateOrigin origins,
                bool hasPosition,
                Vector3 position,
                PEGANpcPerceptionSensorGizmoResult radial,
                PEGANpcPerceptionSensorGizmoResult visual,
                bool identityRecognized,
                string identitySummary,
                string identityDiagnostic)
            {
                CandidateIndex = candidateIndex;
                Origins = origins;
                HasPosition = hasPosition;
                Position = position;
                Radial = radial;
                Visual = visual;
                IdentityRecognized = identityRecognized;
                IdentitySummary = identitySummary ?? string.Empty;
                IdentityDiagnostic = identityDiagnostic ?? string.Empty;
            }

            public int CandidateIndex { get; }
            public PerceptionCandidateOrigin Origins { get; }
            public bool HasPosition { get; }
            public Vector3 Position { get; }
            public PEGANpcPerceptionSensorGizmoResult Radial { get; }
            public PEGANpcPerceptionSensorGizmoResult Visual { get; }
            public bool IdentityRecognized { get; }
            public string IdentitySummary { get; }
            public string IdentityDiagnostic { get; }
        }

        public readonly struct PEGANpcPerceptionAcquisitionGizmoState
        {
            internal PEGANpcPerceptionAcquisitionGizmoState(
                PerceptionSpatialDiscoveryVolume volume,
                PerceptionAcquisitionStatus status,
                string sensorLabels)
            {
                Volume = volume;
                Status = status;
                SensorLabels = sensorLabels ?? string.Empty;
            }

            public PerceptionSpatialDiscoveryVolume Volume { get; }
            public PerceptionAcquisitionStatus Status { get; }
            public string SensorLabels { get; }
        }

        public readonly struct PEGANpcPerceptionGizmoState
        {
            internal PEGANpcPerceptionGizmoState(
                bool hasSample,
                bool hasTarget,
                bool hasAreaObservation,
                bool hasVisionObservation,
                bool radialSensorSampled,
                bool visualSensorSampled,
                Vector3 origin,
                Vector3 targetAimPoint,
                bool isAreaOccluded,
                bool isVisionOccluded,
                bool isWithinPerceivedArea,
                bool isInsideVisionCone,
                bool isAreaPresencePerceived,
                bool hasVisualContact,
                PerceptionAcquisitionStatus acquisitionStatus,
                string acquisitionDiagnostic,
                IReadOnlyList<PEGANpcPerceptionCandidateGizmoState> candidates,
                IReadOnlyList<PEGANpcPerceptionAcquisitionGizmoState> acquisitionVolumes)
            {
                HasSample = hasSample;
                HasTarget = hasTarget;
                HasAreaObservation = hasAreaObservation;
                HasVisionObservation = hasVisionObservation;
                RadialSensorSampled = radialSensorSampled;
                VisualSensorSampled = visualSensorSampled;
                Origin = origin;
                TargetAimPoint = targetAimPoint;
                IsAreaOccluded = isAreaOccluded;
                IsVisionOccluded = isVisionOccluded;
                IsWithinPerceivedArea = isWithinPerceivedArea;
                IsInsideVisionCone = isInsideVisionCone;
                IsAreaPresencePerceived = isAreaPresencePerceived;
                HasVisualContact = hasVisualContact;
                AcquisitionStatus = acquisitionStatus;
                AcquisitionDiagnostic = acquisitionDiagnostic ?? string.Empty;
                Candidates = CopyReadOnly(candidates);
                AcquisitionVolumes = CopyReadOnly(acquisitionVolumes);
            }

            public bool HasSample { get; }
            public bool HasTarget { get; }
            public bool HasAreaObservation { get; }
            public bool HasVisionObservation { get; }
            public bool RadialSensorSampled { get; }
            public bool VisualSensorSampled { get; }
            public Vector3 Origin { get; }
            public Vector3 TargetAimPoint { get; }
            public bool IsAreaOccluded { get; }
            public bool IsVisionOccluded { get; }
            public bool IsWithinPerceivedArea { get; }
            public bool IsInsideVisionCone { get; }
            public bool IsAreaPresencePerceived { get; }
            public bool HasVisualContact { get; }
            public PerceptionAcquisitionStatus AcquisitionStatus { get; }
            public string AcquisitionDiagnostic { get; }
            public IReadOnlyList<PEGANpcPerceptionCandidateGizmoState> Candidates { get; }
            public IReadOnlyList<PEGANpcPerceptionAcquisitionGizmoState> AcquisitionVolumes { get; }

            private static IReadOnlyList<T> CopyReadOnly<T>(IReadOnlyList<T> source)
            {
                if (source == null || source.Count == 0)
                {
                    return Array.Empty<T>();
                }

                var copy = new T[source.Count];
                for (int index = 0; index < source.Count; index++)
                {
                    copy[index] = source[index];
                }

                return Array.AsReadOnly(copy);
            }
        }

        public float PerceivedAreaRadiusMeters
        {
            get
            {
                RadialPresenceSensor sensor = GetGizmoAreaSensor();
                return sensor != null ? sensor.RadiusMeters : 0f;
            }
        }

        public float VisionRangeMeters
        {
            get
            {
                VisionConeSensor sensor = GetGizmoVisionSensor();
                return sensor != null ? sensor.RangeMeters : 0f;
            }
        }

        public float VisionFieldOfViewDegrees
        {
            get
            {
                VisionConeSensor sensor = GetGizmoVisionSensor();
                return sensor != null ? sensor.FieldOfViewDegrees : 0f;
            }
        }

        public Vector3 VisionForward
        {
            get
            {
                VisionConeSensor sensor = GetGizmoVisionSensor();
                return sensor != null
                    ? sensor.GetForward(transform)
                    : transform.forward;
            }
        }

        public Vector3 PerceivedAreaOrigin
        {
            get
            {
                RadialPresenceSensor sensor = GetGizmoAreaSensor();
                return sensor != null
                    ? sensor.GetObserverOrigin(transform)
                    : transform.position;
            }
        }

        public Vector3 VisionOrigin
        {
            get
            {
                VisionConeSensor sensor = GetGizmoVisionSensor();
                return sensor != null
                    ? sensor.GetObserverOrigin(transform)
                    : transform.position;
            }
        }

        public bool TryGetPerceivedAreaDiscoveryVolume(out PerceptionSpatialDiscoveryVolume volume)
        {
            RadialPresenceSensor sensor = GetGizmoAreaSensor();
            if (sensor != null && sensor.TryGetSpatialDiscoveryVolume(transform, out volume, out _))
            {
                return true;
            }

            volume = default;
            return false;
        }

        public bool TryGetVisionDiscoveryVolume(out PerceptionSpatialDiscoveryVolume volume)
        {
            VisionConeSensor sensor = GetGizmoVisionSensor();
            if (sensor != null && sensor.TryGetSpatialDiscoveryVolume(transform, out volume, out _))
            {
                return true;
            }

            volume = default;
            return false;
        }

        public PEGADiagnosticVisualizationLevel PerceptionVisualizationLevel =>
            perceptionVisualizationLevel;

        public bool DrawGizmosWhenUnselected => drawGizmosWhenUnselected;
        public bool HasPerceivedAreaSensor
        {
            get
            {
                return GetGizmoAreaSensor() != null;
            }
        }

        public bool HasVisionSensor
        {
            get
            {
                return GetGizmoVisionSensor() != null;
            }
        }

        private RadialPresenceSensor GetGizmoAreaSensor() =>
            Application.isPlaying
                ? _areaPresenceSensor
                : perceivedAreaSensorInstance != null
                    ? perceivedAreaSensorInstance.Definition as RadialPresenceSensor
                    : null;

        private VisionConeSensor GetGizmoVisionSensor() =>
            Application.isPlaying
                ? _visionConeSensor
                : visionConeSensorInstance != null
                    ? visionConeSensorInstance.Definition as VisionConeSensor
                    : null;

        public PEGANpcPerceptionGizmoState LatestGizmoState => _gizmoState;
#endif

        private void Awake()
        {
            _log = PEGALogging.CreateScopedLogger<PEGANpcPerceptionSource>(
                LogCategory,
                gameObject.name);

            _actorReference = GetComponent<ActorOccurrenceReferenceBinding>();
            _actorDeclaration = GetComponent<ActorDeclaration>();
            _actorOccurrenceQuery = GetComponent<ActorOccurrenceQueryBinding>();
            _actorOccurrenceRecognitionQuery = _actorOccurrenceQuery as IActorOccurrenceRecognitionQuery;
            _objectEntryRecognitionQuery = GetComponent<ObjectEntryOccurrenceQueryBinding>();
            _knowledgeState = GetComponent<PEGANpcKnowledgeState>();
            _perceptionSensorSet = GetComponent<PerceptionSensorSet>();

            if (!TryValidateConfiguration(out string issue))
            {
                _log.Error(
                    $"{nameof(PEGANpcPerceptionSource)} requires Framework query authorities, Knowledge State and configured Perception sensor assets.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("diagnostic", issue));
                enabled = false;
                return;
            }

            _isConfigured = true;
        }

        private void LateUpdate()
        {
            if (!_isConfigured)
            {
                return;
            }

            double observationTimeSeconds = Time.timeAsDouble;
            if (!_perceptionSensorSet.TryValidateComposition(out string authorityDiagnostic))
            {
                SetTargetDiagnostic(authorityDiagnostic);
                SetGizmoUnavailable();
                return;
            }

            if (_perceptionSensorSet.HasDueSensorInstances(this, observationTimeSeconds))
            {
                ObserveCurrentPlayer(observationTimeSeconds, false);
            }
        }

        private void OnEnable()
        {
            if (!_isConfigured)
            {
                return;
            }

            if (!_perceptionSensorSet.TryRegisterSamplingDriver(this, out string driverDiagnostic))
            {
                _log.Error(
                    $"{nameof(PEGANpcPerceptionSource)} could not become the Actor's Perception sampling driver.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("diagnostic", driverDiagnostic));
                enabled = false;
                return;
            }

            double observationTimeSeconds = Time.timeAsDouble;
            _perceptionSensorSet.RequestImmediateSample(this, perceivedAreaSensorInstance);
            _perceptionSensorSet.RequestImmediateSample(this, visionConeSensorInstance);
            ObserveCurrentPlayer(observationTimeSeconds, false);
            ResumeLostContactReevaluationIfNeeded();
        }

        private void OnDisable()
        {
            StopLostContactReevaluationRoutine();
            _perceptionSensorSet?.UnregisterSamplingDriver(this);
            SetGizmoUnavailable();
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
            _perceptionSensorSet?.UnregisterSamplingDriver(this);
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

        private bool TryValidateConfiguration(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_actorDeclaration == null)
            {
                diagnostic = $"{nameof(ActorDeclaration)} is missing from the NPC root.";
                return false;
            }

            if (_actorOccurrenceQuery == null)
            {
                diagnostic = $"{nameof(ActorOccurrenceQueryBinding)} is missing from the NPC root.";
                return false;
            }

            if (_actorOccurrenceRecognitionQuery == null)
            {
                diagnostic = $"{nameof(ActorOccurrenceQueryBinding)} does not provide {nameof(IActorOccurrenceRecognitionQuery)}.";
                return false;
            }

            if (_objectEntryRecognitionQuery == null)
            {
                diagnostic = $"{nameof(ObjectEntryOccurrenceQueryBinding)} is missing from the NPC root.";
                return false;
            }

            if (_knowledgeState == null)
            {
                diagnostic = $"{nameof(PEGANpcKnowledgeState)} is missing from the NPC root.";
                return false;
            }

            if (_perceptionSensorSet == null)
            {
                diagnostic = $"{nameof(PerceptionSensorSet)} is missing from the NPC root.";
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

            if (perceivedAreaSensorInstance != null &&
                (_areaPresenceSensor == null || !_perceptionSensorSet.ContainsSensorInstance(perceivedAreaSensorInstance)))
            {
                diagnostic = "The radial presence gameplay role must reference a composed instance using a radial sensor asset.";
                return false;
            }

            if (visionConeSensorInstance != null &&
                (_visionConeSensor == null || !_perceptionSensorSet.ContainsSensorInstance(visionConeSensorInstance)))
            {
                diagnostic = "The visual contact gameplay role must reference a composed instance using a vision sensor asset.";
                return false;
            }

            if (_areaPresenceSensor != null &&
                !_areaPresenceSensor.TryValidateConfiguration(out diagnostic))
            {
                diagnostic = $"Radial presence gameplay sensor configuration is invalid: {diagnostic}";
                return false;
            }

            if (_visionConeSensor != null &&
                !_visionConeSensor.TryValidateConfiguration(out diagnostic))
            {
                diagnostic = $"Visual contact gameplay sensor configuration is invalid: {diagnostic}";
                return false;
            }

            if (!IsPositiveFinite(lostContactEvadedIntervalSeconds))
            {
                diagnostic = "Lost-contact interval must be finite and positive.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryResolveSensors(out string diagnostic)
        {
            _perceptionSensorSet = GetComponent<PerceptionSensorSet>();
            if (_perceptionSensorSet == null)
            {
                diagnostic = $"{nameof(PerceptionSensorSet)} is missing from the NPC Actor.";
                return false;
            }

            _areaPresenceSensor = perceivedAreaSensorInstance != null
                ? perceivedAreaSensorInstance.Definition as RadialPresenceSensor
                : null;
            _visionConeSensor = visionConeSensorInstance != null
                ? visionConeSensorInstance.Definition as VisionConeSensor
                : null;

            if (perceivedAreaSensorInstance != null && _areaPresenceSensor == null)
            {
                diagnostic = "The radial presence role requires a Radial Presence sensor instance.";
                return false;
            }

            if (visionConeSensorInstance != null && _visionConeSensor == null)
            {
                diagnostic = "The visual contact role requires a Vision Cone sensor instance.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private void ObserveCurrentPlayer(double observationTimeSeconds, bool force)
        {
            ObserveCandidates(observationTimeSeconds, force);
        }

        private void ObserveCandidates(double observationTimeSeconds, bool force)
        {
            if (!TryValidateConfiguration(out string configurationDiagnostic))
            {
                SetTargetDiagnostic(configurationDiagnostic);
                SetGizmoUnavailable();
                return;
            }

            if (double.IsNaN(observationTimeSeconds) || double.IsInfinity(observationTimeSeconds))
            {
                SetTargetDiagnostic("NPC Perception observation time must be finite.");
                SetGizmoUnavailable();
                return;
            }

            ActorOccurrenceResolution? observerOccurrence = ResolveObserverOccurrence();
            PerceptionCandidateAcquisition externalCandidates = BuildExternallyKnownCandidates(
                observationTimeSeconds,
                out ActorOccurrence? playerOccurrence,
                out List<StaleActorContact> staleActorContacts);

            if (!_perceptionSensorSet.TrySample(
                    this,
                    observerOccurrence,
                    externalCandidates,
                    observationTimeSeconds,
                    null,
                    null,
                    force,
                    IsEligiblePhysicalCandidate,
                    out bool sampled,
                    out IReadOnlyList<PerceptionSensorInstance> sampledSensorInstances,
                    out PerceptionCandidateAcquisition acquisition,
                    out IReadOnlyList<PerceptionCandidateSensorResult> sensorResults,
                    out string diagnostic))
            {
                SetTargetDiagnostic(string.IsNullOrWhiteSpace(diagnostic)
                    ? "Perception Sensor Set could not evaluate the current candidate batch."
                    : diagnostic);
                SetGizmoUnavailable();
                return;
            }

            if (!sampled)
            {
                return;
            }

            SetTargetDiagnostic(string.Empty);
            LogAcquisitionTransition(acquisition);

            bool radialWasSampled = ContainsSensorInstance(sampledSensorInstances, perceivedAreaSensorInstance);
            bool visualWasSampled = ContainsSensorInstance(sampledSensorInstances, visionConeSensorInstance);
            bool hasRadialPositive = false;
            bool allRadialCandidatesUsable = true;
            bool hasFocus = false;
            bool focusIsPlayer = false;
            float focusDistance = float.PositiveInfinity;
            PerceptionSensorObservation focusRadialObservation = default;
            PerceptionSensorObservation focusVisionObservation = default;
            bool focusHasRadialObservation = false;
            bool focusHasVisionObservation = false;
            var positiveActors = new Dictionary<ActorId, ActorOccurrence>();
            var negativeActors = new Dictionary<ActorId, ActorOccurrence>();
            var positiveActorPositions = new Dictionary<ActorId, Vector3>();
            var objectEntryRoots = new Dictionary<ObjectEntryId, GameObject>();
            var ambiguousObjectEntryIds = new HashSet<ObjectEntryId>();
#if UNITY_EDITOR
            var candidateIdentityRecognized = new bool[acquisition.Candidates.Count];
            var candidateIdentitySummaries = new string[acquisition.Candidates.Count];
            var candidateIdentityDiagnostics = new string[acquisition.Candidates.Count];
#endif
            _candidateCollidersThisSample.Clear();
            for (int candidateIndex = 0; candidateIndex < acquisition.Candidates.Count; candidateIndex++)
            {
                PerceptionCandidate candidate = acquisition.Candidates[candidateIndex];
                Collider physicalCollider = GetPhysicalCollider(candidate);
                if (physicalCollider != null)
                {
                    _candidateCollidersThisSample.Add(physicalCollider);
                }

                bool radialCandidate = IsCandidateInSensorAcquisition(candidate, perceivedAreaSensorInstance);
                bool hasRadialObservation = false;
                PerceptionSensorObservation radialObservation = default;
                if (radialWasSampled && radialCandidate)
                {
                    hasRadialObservation = TryGetSensorObservation(
                        sensorResults,
                        perceivedAreaSensorInstance,
                        candidateIndex,
                        candidate,
                        observationTimeSeconds,
                        out radialObservation,
                        out _);
                    if (hasRadialObservation)
                    {
                        if (radialObservation.IsDetected)
                        {
                            hasRadialPositive = true;
                        }
                    }
                    else
                    {
                        allRadialCandidatesUsable = false;
                    }
                }

                bool hasVisionObservation = false;
                PerceptionSensorObservation visionObservation = default;
                if (visualWasSampled)
                {
                    hasVisionObservation = TryGetSensorObservation(
                        sensorResults,
                        visionConeSensorInstance,
                        candidateIndex,
                        candidate,
                        observationTimeSeconds,
                        out visionObservation,
                        out _);
                }

                ActorOccurrenceReference? externalActorReference = GetExternalActorReference(candidate);
                bool isPlayerCandidate = externalActorReference.HasValue &&
                                         externalActorReference.Value.PlayerSlotId.IsValid &&
                                         playerOccurrence.HasValue &&
                                         SameOccurrence(externalActorReference.Value, playerOccurrence.Value);
#if UNITY_EDITOR
                if (externalActorReference.HasValue)
                {
                    candidateIdentityRecognized[candidateIndex] = true;
                    candidateIdentitySummaries[candidateIndex] =
                        "Actor " + externalActorReference.Value.ActorId.StableText;
                    candidateIdentityDiagnostics[candidateIndex] =
                        "Candidate carries a previously resolved exact Framework Actor occurrence reference.";
                }
#endif

                if ((hasRadialObservation || hasVisionObservation) &&
                    (!hasFocus || isPlayerCandidate && !focusIsPlayer ||
                     isPlayerCandidate == focusIsPlayer &&
                     Math.Min(
                         hasRadialObservation ? radialObservation.PlanarDistanceMeters : float.PositiveInfinity,
                         hasVisionObservation ? visionObservation.PlanarDistanceMeters : float.PositiveInfinity) < focusDistance))
                {
                    hasFocus = true;
                    focusIsPlayer = isPlayerCandidate;
                    focusDistance = Math.Min(
                        hasRadialObservation ? radialObservation.PlanarDistanceMeters : float.PositiveInfinity,
                        hasVisionObservation ? visionObservation.PlanarDistanceMeters : float.PositiveInfinity);
                    focusRadialObservation = radialObservation;
                    focusVisionObservation = visionObservation;
                    focusHasRadialObservation = hasRadialObservation;
                    focusHasVisionObservation = hasVisionObservation;
                }

                if (physicalCollider != null &&
                    hasRadialObservation &&
                    radialObservation.IsDetected &&
                    (!hasVisionObservation || !visionObservation.IsDetected))
                {
                    LogCandidateRecognition(
                        physicalCollider,
                        "anonymous:radial-presence",
                        string.Empty,
                        "Radial detection establishes presence only; no positive visual recognition was produced.");
                }

                if (!visualWasSampled || !hasVisionObservation)
                {
                    continue;
                }

                if (visionObservation.IsDetected)
                {
                    ActorOccurrence actorOccurrence = default;
                    string actorRecognitionState = "actor:none";
                    string actorRecognitionDiagnostic = string.Empty;
                    bool actorRecognized = TryRecognizeActorCandidate(
                        candidate,
                        physicalCollider,
                        out actorOccurrence,
                        out actorRecognitionState,
                        out actorRecognitionDiagnostic);
                    bool recognizedAnyDeclaration = actorRecognized;
                    if (actorRecognized)
                    {
                        bool duplicateOfConfiguredPlayerCandidate =
                            !externalActorReference.HasValue &&
                            playerOccurrence.HasValue &&
                            SameOccurrence(actorOccurrence, playerOccurrence.Value);
                        if (!duplicateOfConfiguredPlayerCandidate)
                        {
                            positiveActors[actorOccurrence.ActorId] = actorOccurrence;
                            positiveActorPositions[actorOccurrence.ActorId] = actorOccurrence.ActorRoot.transform.position;
                        }
                    }

                    ObjectEntryId objectEntryId = default;
                    GameObject objectEntryRoot = null;
                    string objectRecognitionState = "object-entry:none";
                    string objectRecognitionDiagnostic = string.Empty;
                    bool objectEntryRecognized = TryRecognizeObjectEntry(
                        physicalCollider,
                        out objectEntryId,
                        out objectEntryRoot,
                        out objectRecognitionState,
                        out objectRecognitionDiagnostic);
                    if (objectEntryRecognized)
                    {
                        if (objectEntryRoots.TryGetValue(objectEntryId, out GameObject previousRoot) &&
                            previousRoot != objectEntryRoot)
                        {
                            ambiguousObjectEntryIds.Add(objectEntryId);
                        }
                        else
                        {
                            objectEntryRoots[objectEntryId] = objectEntryRoot;
                        }
                        recognizedAnyDeclaration = true;
                    }

                    if (physicalCollider != null)
                    {
                        string identitySummary = string.Empty;
                        if (actorRecognized)
                        {
                            identitySummary = "Actor " + actorOccurrence.ActorId.StableText;
                        }
                        if (objectEntryRecognized)
                        {
                            identitySummary = string.IsNullOrEmpty(identitySummary)
                                ? "Object Entry " + objectEntryId.StableText
                                : identitySummary + ", Object Entry " + objectEntryId.StableText;
                        }
                        string combinedDiagnostic = JoinDiagnostics(actorRecognitionDiagnostic, objectRecognitionDiagnostic);
                        if (!recognizedAnyDeclaration && string.IsNullOrWhiteSpace(combinedDiagnostic))
                        {
                            combinedDiagnostic = "Visible declaration did not resolve to a committed official occurrence.";
                        }
                        LogCandidateRecognition(
                            physicalCollider,
                            actorRecognitionState + ";" + objectRecognitionState,
                            identitySummary,
                            combinedDiagnostic);
#if UNITY_EDITOR
                        if (recognizedAnyDeclaration)
                        {
                            candidateIdentityRecognized[candidateIndex] = true;
                            candidateIdentitySummaries[candidateIndex] = identitySummary;
                            candidateIdentityDiagnostics[candidateIndex] = string.Empty;
                        }
                        else
                        {
                            candidateIdentityDiagnostics[candidateIndex] =
                                string.IsNullOrWhiteSpace(combinedDiagnostic)
                                    ? actorRecognitionState + ";" + objectRecognitionState
                                    : combinedDiagnostic;
                        }
#endif
                    }
                }
                else if (externalActorReference.HasValue)
                {
                    ActorOccurrence currentExternalOccurrence = default;
                    ActorOccurrenceResolutionStatus resolutionStatus = ActorOccurrenceResolutionStatus.Unavailable;
                    string resolutionDiagnostic = string.Empty;
                    bool hasCurrentOccurrence = isPlayerCandidate && playerOccurrence.HasValue;
                    if (hasCurrentOccurrence)
                    {
                        currentExternalOccurrence = playerOccurrence.Value;
                    }
                    else
                    {
                        hasCurrentOccurrence = TryResolveExactActorOccurrence(
                            externalActorReference.Value,
                            out currentExternalOccurrence,
                            out resolutionStatus,
                            out resolutionDiagnostic);
                    }

                    if (hasCurrentOccurrence)
                    {
                        negativeActors[currentExternalOccurrence.ActorId] = currentExternalOccurrence;
                    }
                    else if (resolutionStatus == ActorOccurrenceResolutionStatus.Stale)
                    {
                        AddStaleActorContact(
                            staleActorContacts,
                            externalActorReference.Value.ActorId,
                            externalActorReference.Value.ExpectedOccurrence);
                    }
                    else if (resolutionStatus != ActorOccurrenceResolutionStatus.Unavailable)
                    {
                        LogStaleOrAmbiguousExternalActor(
                            externalActorReference.Value,
                            resolutionStatus,
                            resolutionDiagnostic);
                    }
                }
            }

            PruneCandidateRecognitionLogStates();

            bool publishRadialPresence = false;
            bool radialPresence = hasRadialPositive;
            if (radialWasSampled && !hasRadialPositive &&
                HasCompletePhysicalCoverage(acquisition, perceivedAreaSensorInstance) &&
                allRadialCandidatesUsable)
            {
                publishRadialPresence = true;
                radialPresence = false;
            }
            else if (radialWasSampled && hasRadialPositive)
            {
                publishRadialPresence = true;
            }

            PEGAKnowledgeFact<PEGANpcPerceivedAreaPresence> previousArea =
                _knowledgeState.GetPerceivedAreaPresence();
            if (publishRadialPresence)
            {
                PublishAreaPresenceIfChanged(radialPresence, observationTimeSeconds);
                LogAreaPresenceTransition(previousArea, radialPresence, observationTimeSeconds);
            }

            foreach (KeyValuePair<ActorId, ActorOccurrence> pair in positiveActors)
            {
                PublishRecognizedActorObservation(
                    pair.Value,
                    positiveActorPositions[pair.Key],
                    true,
                    observationTimeSeconds,
                    isTargetPlayer: playerOccurrence.HasValue && SameOccurrence(pair.Value, playerOccurrence.Value),
                    forceCurrentEvidence: HasDifferentStaleActorOccurrence(
                        staleActorContacts,
                        pair.Key,
                        pair.Value.Id));
            }

            foreach (KeyValuePair<ActorId, ActorOccurrence> pair in negativeActors)
            {
                if (positiveActors.ContainsKey(pair.Key) ||
                    IsSameStaleActorOccurrence(staleActorContacts, pair.Key, pair.Value.Id))
                {
                    continue;
                }

                PublishRecognizedActorObservation(
                    pair.Value,
                    default,
                    false,
                    observationTimeSeconds,
                    isTargetPlayer: playerOccurrence.HasValue && SameOccurrence(pair.Value, playerOccurrence.Value),
                    forceCurrentEvidence: false);
            }

            for (int index = 0; index < staleActorContacts.Count; index++)
            {
                StaleActorContact stale = staleActorContacts[index];
                bool hasCurrentOccurrenceEvidence = positiveActors.ContainsKey(stale.ActorId) ||
                    negativeActors.TryGetValue(stale.ActorId, out ActorOccurrence negativeOccurrence) &&
                    negativeOccurrence.Id != stale.OccurrenceId;
                if (!hasCurrentOccurrenceEvidence)
                {
                    InvalidateStaleActorContact(
                        stale.ActorId,
                        stale.OccurrenceId,
                        observationTimeSeconds);
                }

                RemoveStaleActorReference(stale);
            }

            foreach (KeyValuePair<ObjectEntryId, GameObject> pair in objectEntryRoots)
            {
                if (ambiguousObjectEntryIds.Contains(pair.Key) || pair.Value == null)
                {
                    continue;
                }

                Vector3 observedPosition = pair.Value.transform.position;
                if (!IsFinite(observedPosition))
                {
                    continue;
                }

                _knowledgeState.ObserveFact(
                    PEGAObjectEntryKnowledgeFacts.Position.For(pair.Key),
                    observedPosition,
                    PEGAKnowledgeEvidence.From(
                        PEGANpcEvidenceOrigin.Perception,
                        observationTimeSeconds,
                        this,
                            pair.Key));
            }

#if UNITY_EDITOR
            PerceptionSensorObservation focusObservation = focusHasVisionObservation
                ? focusVisionObservation
                : focusRadialObservation;
            PEGANpcPerceptionCandidateGizmoState[] candidateGizmoStates = BuildCandidateGizmoStates(
                acquisition,
                sensorResults,
                observationTimeSeconds,
                radialWasSampled,
                visualWasSampled,
                candidateIdentityRecognized,
                candidateIdentitySummaries,
                candidateIdentityDiagnostics);
            PEGANpcPerceptionAcquisitionGizmoState[] acquisitionGizmoStates =
                BuildAcquisitionGizmoStates(acquisition);
            _gizmoState = new PEGANpcPerceptionGizmoState(
                true,
                hasFocus,
                focusHasRadialObservation,
                focusHasVisionObservation,
                radialWasSampled,
                visualWasSampled,
                hasFocus ? focusObservation.ObserverOrigin : GetObserverOrigin(),
                hasFocus ? focusObservation.TargetAimPoint : Vector3.zero,
                hasFocus && focusHasRadialObservation && focusRadialObservation.IsOccluded,
                hasFocus && focusHasVisionObservation && focusVisionObservation.IsOccluded,
                hasFocus && focusHasRadialObservation && focusRadialObservation.IsWithinSensorBounds,
                hasFocus && focusHasVisionObservation && focusVisionObservation.IsWithinSensorBounds,
                hasFocus && focusHasRadialObservation && focusRadialObservation.IsDetected,
                hasFocus && focusHasVisionObservation && focusVisionObservation.IsDetected,
                acquisition.Status,
                GetAcquisitionDiagnostic(acquisition),
                candidateGizmoStates,
                acquisitionGizmoStates);
#endif
        }

        private static bool ContainsSensorInstance(
            IReadOnlyList<PerceptionSensorInstance> sensorInstances,
            PerceptionSensorInstance requestedInstance)
        {
            if (sensorInstances == null || requestedInstance == null)
            {
                return false;
            }

            for (int index = 0; index < sensorInstances.Count; index++)
            {
                if (ReferenceEquals(sensorInstances[index], requestedInstance))
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        private PEGANpcPerceptionCandidateGizmoState[] BuildCandidateGizmoStates(
            PerceptionCandidateAcquisition acquisition,
            IReadOnlyList<PerceptionCandidateSensorResult> results,
            double observationTimeSeconds,
            bool radialWasSampled,
            bool visualWasSampled,
            IReadOnlyList<bool> identityRecognized,
            IReadOnlyList<string> identitySummaries,
            IReadOnlyList<string> identityDiagnostics)
        {
            var snapshots = new PEGANpcPerceptionCandidateGizmoState[acquisition.Candidates.Count];
            for (int candidateIndex = 0; candidateIndex < acquisition.Candidates.Count; candidateIndex++)
            {
                PerceptionCandidate candidate = acquisition.Candidates[candidateIndex];
                PEGANpcPerceptionSensorGizmoResult radial = CreateSensorGizmoResult(
                    results,
                    perceivedAreaSensorInstance,
                    candidateIndex,
                    candidate,
                    observationTimeSeconds,
                    radialWasSampled);
                PEGANpcPerceptionSensorGizmoResult visual = CreateSensorGizmoResult(
                    results,
                    visionConeSensorInstance,
                    candidateIndex,
                    candidate,
                    observationTimeSeconds,
                    visualWasSampled);

                bool hasPosition = candidate.TryGetUniquePosition(out Vector3 position, out _);
                if (!hasPosition && visual.HasObservation)
                {
                    position = visual.Observation.TargetAimPoint;
                    hasPosition = true;
                }
                else if (!hasPosition && radial.HasObservation)
                {
                    position = radial.Observation.TargetAimPoint;
                    hasPosition = true;
                }

                snapshots[candidateIndex] = new PEGANpcPerceptionCandidateGizmoState(
                    candidateIndex,
                    candidate.Origins,
                    hasPosition,
                    position,
                    radial,
                    visual,
                    identityRecognized[candidateIndex],
                    identitySummaries[candidateIndex],
                    identityDiagnostics[candidateIndex]);
            }

            return snapshots;
        }

        private static PEGANpcPerceptionSensorGizmoResult CreateSensorGizmoResult(
            IReadOnlyList<PerceptionCandidateSensorResult> results,
            PerceptionSensorInstance sensorInstance,
            int candidateIndex,
            PerceptionCandidate candidate,
            double observationTimeSeconds,
            bool wasSampled)
        {
            if (results != null && sensorInstance != null && candidate != null)
            {
                for (int index = 0; index < results.Count; index++)
                {
                    PerceptionCandidateSensorResult result = results[index];
                    if (result.CandidateIndex != candidateIndex ||
                        !ReferenceEquals(result.SensorInstance, sensorInstance))
                    {
                        continue;
                    }

                    bool belongsToCandidate =
                        ReferenceEquals(result.CorrelationToken, candidate.CorrelationToken) &&
                        result.CandidateOrigins == candidate.Origins &&
                        ReferenceEquals(result.SensorDefinition, sensorInstance.Definition) &&
                        Math.Abs(result.ObservationTimeSeconds - observationTimeSeconds) <= 0.000001d;
                    if (!belongsToCandidate)
                    {
                        return new PEGANpcPerceptionSensorGizmoResult(
                            wasSampled,
                            true,
                            false,
                            PerceptionObservationStatus.Unspecified,
                            false,
                            default,
                            "The sensor result does not match this candidate sample.");
                    }

                    bool hasObservation = result.HasOutcome &&
                                         (result.Status == PerceptionObservationStatus.Positive ||
                                          result.Status == PerceptionObservationStatus.Negative) &&
                                         result.Evidence is PerceptionSensorObservation;
                    PerceptionSensorObservation observation = default;
                    if (hasObservation)
                    {
                        observation = (PerceptionSensorObservation)result.Evidence;
                    }
                    return new PEGANpcPerceptionSensorGizmoResult(
                        wasSampled,
                        true,
                        result.HasOutcome,
                        result.Status,
                        hasObservation,
                        observation,
                        result.Diagnostic);
                }
            }

            return new PEGANpcPerceptionSensorGizmoResult(
                wasSampled,
                false,
                false,
                PerceptionObservationStatus.Unspecified,
                false,
                default,
                wasSampled ? "The sampled sensor returned no result for this candidate." : string.Empty);
        }

        private PEGANpcPerceptionAcquisitionGizmoState[] BuildAcquisitionGizmoStates(
            PerceptionCandidateAcquisition acquisition)
        {
            var snapshots = new List<PEGANpcPerceptionAcquisitionGizmoState>();
            for (int index = 0; index < acquisition.Sources.Count; index++)
            {
                PerceptionCandidateSourceResult source = acquisition.Sources[index];
                if (source.Origin != PerceptionCandidateOrigin.Physical ||
                    !(source.CoverageReference is PerceptionSpatialDiscoveryVolume volume))
                {
                    continue;
                }

                PerceptionSensorInstance sensorInstance = source.SourceReference as PerceptionSensorInstance;
                string sensorLabel = ReferenceEquals(sensorInstance, perceivedAreaSensorInstance)
                    ? "Radial"
                    : ReferenceEquals(sensorInstance, visionConeSensorInstance)
                        ? "Visual"
                        : sensorInstance != null && sensorInstance.Definition != null
                            ? sensorInstance.Definition.SensorId
                            : "Sensor";
                int existingIndex = FindSameGeometry(snapshots, volume);
                if (existingIndex < 0)
                {
                    snapshots.Add(new PEGANpcPerceptionAcquisitionGizmoState(
                        volume,
                        source.Status,
                        sensorLabel));
                    continue;
                }

                PEGANpcPerceptionAcquisitionGizmoState previous = snapshots[existingIndex];
                string labels = previous.SensorLabels.IndexOf(sensorLabel, StringComparison.Ordinal) >= 0
                    ? previous.SensorLabels
                    : previous.SensorLabels + " + " + sensorLabel;
                snapshots[existingIndex] = new PEGANpcPerceptionAcquisitionGizmoState(
                    previous.Volume,
                    WorseAcquisitionStatus(previous.Status, source.Status),
                    labels);
            }

            return snapshots.ToArray();
        }

        private static int FindSameGeometry(
            IReadOnlyList<PEGANpcPerceptionAcquisitionGizmoState> snapshots,
            PerceptionSpatialDiscoveryVolume candidate)
        {
            for (int index = 0; index < snapshots.Count; index++)
            {
                PerceptionSpatialDiscoveryVolume current = snapshots[index].Volume;
                if (HasSameVolumeGeometry(current, candidate))
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool HasSameVolumeGeometry(
            PerceptionSpatialDiscoveryVolume left,
            PerceptionSpatialDiscoveryVolume right)
        {
            if (left.Shape != right.Shape || !left.Center.Equals(right.Center))
            {
                return false;
            }

            switch (left.Shape)
            {
                case PerceptionSpatialVolumeShape.Sphere:
                    return left.RadiusMeters.Equals(right.RadiusMeters);
                case PerceptionSpatialVolumeShape.Box:
                    return left.Rotation.Equals(right.Rotation) &&
                           left.BoxSizeMeters.Equals(right.BoxSizeMeters);
                case PerceptionSpatialVolumeShape.Capsule:
                    return left.Rotation.Equals(right.Rotation) &&
                           left.RadiusMeters.Equals(right.RadiusMeters) &&
                           left.CapsuleHeightMeters.Equals(right.CapsuleHeightMeters);
                default:
                    return false;
            }
        }

        private static PerceptionAcquisitionStatus WorseAcquisitionStatus(
            PerceptionAcquisitionStatus left,
            PerceptionAcquisitionStatus right)
        {
            return AcquisitionStatusRank(right) > AcquisitionStatusRank(left) ? right : left;
        }

        private static int AcquisitionStatusRank(PerceptionAcquisitionStatus status)
        {
            switch (status)
            {
                case PerceptionAcquisitionStatus.Unavailable: return 3;
                case PerceptionAcquisitionStatus.Partial: return 2;
                case PerceptionAcquisitionStatus.Complete: return 1;
                default: return 0;
            }
        }

        private static string GetAcquisitionDiagnostic(PerceptionCandidateAcquisition acquisition)
        {
            string diagnostic = string.Empty;
            for (int index = 0; index < acquisition.Sources.Count; index++)
            {
                string sourceDiagnostic = acquisition.Sources[index].Diagnostic;
                if (!string.IsNullOrWhiteSpace(sourceDiagnostic))
                {
                    diagnostic = JoinDiagnostics(diagnostic, sourceDiagnostic);
                }
            }

            return diagnostic;
        }
#endif

        private ActorOccurrenceResolution? ResolveObserverOccurrence()
        {
            if (_actorDeclaration == null || _actorOccurrenceQuery == null)
            {
                LogObserverOccurrenceUnavailable(
                    _actorDeclaration == null
                        ? $"{nameof(ActorDeclaration)} is unavailable."
                        : $"{nameof(ActorOccurrenceQueryBinding)} is unavailable.");
                return null;
            }

            if (!_actorDeclaration.TryCreateDescriptor(
                    nameof(PEGANpcPerceptionSource),
                    out ActorDescriptor descriptor,
                    out ActorSetIssue issue))
            {
                LogObserverOccurrenceUnavailable(issue.ToString());
                return null;
            }

            ActorOccurrenceResolution resolution = _actorOccurrenceQuery.Resolve(
                ActorOccurrenceReference.ForActorId(descriptor.ActorId));
            if (!resolution.Succeeded || resolution.Occurrence.ActorRoot != gameObject)
            {
                string diagnostic = resolution.Succeeded
                    ? "The official Actor query resolved this ActorId to a different physical root."
                    : resolution.Diagnostic;
                LogObserverOccurrenceUnavailable(diagnostic);
                return null;
            }

            _lastObserverOccurrenceDiagnostic = string.Empty;
            if (_lastObserverOccurrenceId != resolution.Occurrence.Id)
            {
                _lastObserverOccurrenceId = resolution.Occurrence.Id;
                _log.Info(
                    "NPC Perception resolved its own committed Framework Actor occurrence; physical acquisition can exclude the observer occurrence.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("actorId", resolution.Occurrence.ActorId.StableText),
                    LogFields.Field("actorOccurrenceId", resolution.Occurrence.Id.ToString()));
            }

            return resolution;
        }

        private void LogObserverOccurrenceUnavailable(string diagnostic)
        {
            diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                ? "The Framework Actor occurrence query is not currently available."
                : diagnostic;
            if (string.Equals(_lastObserverOccurrenceDiagnostic, diagnostic, StringComparison.Ordinal))
            {
                return;
            }

            _lastObserverOccurrenceDiagnostic = diagnostic;
            _lastObserverOccurrenceId = default;
            _log.Warning(
                "NPC Perception cannot exclude its own physical occurrence until the official Actor occurrence query resolves it; external candidates remain evaluable and acquisition will retry.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("diagnostic", diagnostic));
        }

        private PerceptionCandidateAcquisition BuildExternallyKnownCandidates(
            double observationTimeSeconds,
            out ActorOccurrence? playerOccurrence,
            out List<StaleActorContact> staleActorContacts)
        {
            var sources = new List<PerceptionCandidateSourceResult>();
            var candidates = new List<PerceptionCandidate>();
            var includedActorOccurrences = new Dictionary<ActorId, ActorOccurrenceId>();
            playerOccurrence = null;
            staleActorContacts = new List<StaleActorContact>();
            string targetDiagnostic = string.Empty;

            if (_actorReference != null)
            {
                if (_actorReference.TryGetCurrent(out ActorOccurrenceResolution playerResolution) &&
                    playerResolution.Succeeded &&
                    playerResolution.Occurrence.HasPlayerSlot)
                {
                    ActorOccurrence occurrence = playerResolution.Occurrence;
                    CharacterController controller = occurrence.ActorRoot != null
                        ? occurrence.ActorRoot.GetComponent<CharacterController>()
                        : null;
                    if (occurrence.ActorRoot != null && occurrence.ActorRoot.activeInHierarchy &&
                        controller != null && controller.enabled)
                    {
                        Vector3 candidatePosition = controller.bounds.center;
                        if (IsFinite(candidatePosition) && AddExternalActorCandidate(
                                occurrence,
                                candidatePosition,
                                _actorReference,
                                observationTimeSeconds,
                                sources,
                                candidates))
                        {
                            playerOccurrence = occurrence;
                            includedActorOccurrences[occurrence.ActorId] = occurrence.Id;
                        }
                        else
                        {
                            targetDiagnostic = "Resolved Player Actor candidate has no finite current position.";
                            AddUnavailableExternalSource(sources, _actorReference, occurrence.Id, observationTimeSeconds, targetDiagnostic);
                        }
                    }
                    else
                    {
                        targetDiagnostic = "Resolved Player Actor root requires an active CharacterController.";
                        AddUnavailableExternalSource(sources, _actorReference, occurrence.Id, observationTimeSeconds, targetDiagnostic);
                    }
                }
                else
                {
                    targetDiagnostic = playerResolution.Succeeded
                        ? "Resolved Actor occurrence does not belong to a Player Slot."
                        : string.IsNullOrWhiteSpace(playerResolution.Diagnostic)
                            ? "Player Slot occurrence is not currently resolved."
                            : playerResolution.Diagnostic;
                    AddUnavailableExternalSource(sources, _actorReference, null, observationTimeSeconds, targetDiagnostic);
                }
            }
            else
            {
                targetDiagnostic = "No Player Slot reference is configured; autonomous Actor and Object Entry sensing remains active.";
            }

            LogExternalTargetAvailability(targetDiagnostic, playerOccurrence.HasValue);

            foreach (KeyValuePair<ActorId, ActorOccurrenceReference> pair in _recognizedActorReferences)
            {
                if (includedActorOccurrences.TryGetValue(pair.Key, out ActorOccurrenceId includedOccurrenceId) &&
                    includedOccurrenceId == pair.Value.ExpectedOccurrence)
                {
                    continue;
                }

                if (!TryResolveExactActorOccurrence(
                        pair.Value,
                        out ActorOccurrence currentOccurrence,
                        out ActorOccurrenceResolutionStatus resolutionStatus,
                        out string resolutionDiagnostic))
                {
                    if (resolutionStatus == ActorOccurrenceResolutionStatus.Stale)
                    {
                        AddStaleActorContact(
                            staleActorContacts,
                            pair.Key,
                            pair.Value.ExpectedOccurrence);
                    }
                    else
                    {
                        AddUnavailableExternalSource(
                            sources,
                            _actorOccurrenceQuery,
                            pair.Value.ExpectedOccurrence,
                            observationTimeSeconds,
                            string.IsNullOrWhiteSpace(resolutionDiagnostic)
                                ? $"Official Actor occurrence query returned {resolutionStatus}."
                                : resolutionDiagnostic);
                    }
                    continue;
                }

                Vector3 currentPosition = GetActorCandidatePosition(currentOccurrence);
                if (!IsFinite(currentPosition) || !AddExternalActorCandidate(
                        currentOccurrence,
                        currentPosition,
                        this,
                        observationTimeSeconds,
                        sources,
                        candidates))
                {
                    continue;
                }

                includedActorOccurrences[currentOccurrence.ActorId] = currentOccurrence.Id;
            }

            return new PerceptionCandidateAcquisition(sources, candidates);
        }

        private void LogExternalTargetAvailability(string diagnostic, bool isAvailable)
        {
            if (string.Equals(_lastExternalTargetDiagnostic, diagnostic, StringComparison.Ordinal))
            {
                return;
            }

            bool wasUnavailable = !string.IsNullOrEmpty(_lastExternalTargetDiagnostic);
            _lastExternalTargetDiagnostic = diagnostic ?? string.Empty;
            if (isAvailable)
            {
                _log.Info(
                    wasUnavailable
                        ? "NPC Player reference recovered; the official Player occurrence is again supplied as an external Perception candidate."
                        : "NPC Player reference is available as an external Perception candidate.",
                    LogFields.Field("actorRoot", gameObject.name));
            }
            else if (!string.IsNullOrWhiteSpace(diagnostic))
            {
                _log.Warning(
                    "NPC Perception has no current external Player candidate; autonomous physical discovery remains active and no negative Player contact is inferred.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("diagnostic", diagnostic));
            }
        }

        private static void AddUnavailableExternalSource(
            ICollection<PerceptionCandidateSourceResult> sources,
            object sourceReference,
            object coverageReference,
            double observationTimeSeconds,
            string diagnostic)
        {
            sources.Add(new PerceptionCandidateSourceResult(
                PerceptionCandidateOrigin.External,
                PerceptionAcquisitionStatus.Unavailable,
                observationTimeSeconds,
                sourceReference,
                coverageReference,
                diagnostic));
        }

        private static bool AddExternalActorCandidate(
            ActorOccurrence occurrence,
            Vector3 position,
            object sourceReference,
            double observationTimeSeconds,
            ICollection<PerceptionCandidateSourceResult> sources,
            ICollection<PerceptionCandidate> candidates)
        {
            if (!occurrence.IsValid || !IsFinite(position))
            {
                return false;
            }

            object correlation = new object();
            PerceptionCandidateEvidence[] evidence =
            {
                PerceptionCandidateEvidence.ForExternalReference(
                    ActorOccurrenceReference.ForOccurrence(occurrence),
                    observationTimeSeconds,
                    sourceReference),
                PerceptionCandidateEvidence.ForPosition(
                    position,
                    PerceptionCandidateOrigin.External,
                    observationTimeSeconds,
                    sourceReference)
            };
            candidates.Add(new PerceptionCandidate(evidence, correlation));
            sources.Add(new PerceptionCandidateSourceResult(
                PerceptionCandidateOrigin.External,
                PerceptionAcquisitionStatus.Complete,
                observationTimeSeconds,
                sourceReference,
                occurrence.Id));
            return true;
        }

        private static Vector3 GetActorCandidatePosition(ActorOccurrence occurrence)
        {
            if (!occurrence.IsValid || occurrence.ActorRoot == null || !occurrence.ActorRoot.activeInHierarchy)
            {
                return new Vector3(float.NaN, float.NaN, float.NaN);
            }

            CharacterController controller = occurrence.ActorRoot.GetComponent<CharacterController>();
            if (controller != null && controller.enabled)
            {
                return controller.bounds.center;
            }

            return occurrence.ActorRoot.transform.position;
        }

        private bool TryResolveExactActorOccurrence(
            ActorOccurrenceReference expectedOccurrence,
            out ActorOccurrence currentOccurrence,
            out ActorOccurrenceResolutionStatus status,
            out string diagnostic)
        {
            currentOccurrence = default;
            status = ActorOccurrenceResolutionStatus.Unavailable;
            if (_actorOccurrenceQuery == null || !expectedOccurrence.IsValid || !expectedOccurrence.IsExactOccurrence)
            {
                diagnostic = "An exact official Actor occurrence and a bound Actor occurrence query are required.";
                return false;
            }

            ActorOccurrenceResolution resolution = _actorOccurrenceQuery.Resolve(expectedOccurrence);
            status = resolution.Status;
            if (!resolution.Succeeded ||
                resolution.Occurrence.Id != expectedOccurrence.ExpectedOccurrence ||
                !resolution.Occurrence.ActorId.Equals(expectedOccurrence.ActorId))
            {
                diagnostic = resolution.Succeeded
                    ? "Official Actor query did not preserve the exact expected occurrence."
                    : resolution.Diagnostic;
                return false;
            }

            currentOccurrence = resolution.Occurrence;
            diagnostic = string.Empty;
            return true;
        }

        private void InvalidateStaleActorContact(
            ActorId actorId,
            ActorOccurrenceId staleOccurrenceId,
            double observationTimeSeconds)
        {
            PEGAKnowledgeFact<bool> contact = _knowledgeState.GetCharacterVisualContact(actorId);
            if (contact.Status == PEGAKnowledgeStatus.Unknown ||
                contact.Status == PEGAKnowledgeStatus.Invalidated)
            {
                return;
            }

            PEGAKnowledgeWriteResult result = _knowledgeState.InvalidateFact(
                PEGANpcKnowledgeFacts.CharacterVisualContact.For(actorId),
                PEGAKnowledgeEvidence.From(
                    PEGANpcEvidenceOrigin.Perception,
                    observationTimeSeconds,
                    this,
                    staleOccurrenceId));
            if (result == PEGAKnowledgeWriteResult.InvalidatedExplicitly)
            {
                _log.Debug(
                    "NPC invalidated visual contact for an Actor whose exact Framework occurrence became stale; last known position is preserved.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", actorId.StableText),
                    LogFields.Field("staleActorOccurrenceId", staleOccurrenceId.ToString()),
                    LogFields.Field("observationTimeSeconds", observationTimeSeconds));
            }
        }

        private void LogStaleOrAmbiguousExternalActor(
            ActorOccurrenceReference expectedOccurrence,
            ActorOccurrenceResolutionStatus status,
            string diagnostic)
        {
            _log.Warning(
                "NPC retained a previously recognized Actor candidate because the official occurrence query is not uniquely resolved; no negative contact was inferred.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("subjectActorId", expectedOccurrence.ActorId.StableText),
                LogFields.Field("actorOccurrenceId", expectedOccurrence.ExpectedOccurrence.ToString()),
                LogFields.Field("resolutionStatus", status),
                LogFields.Field("diagnostic", diagnostic));
        }

        private bool IsEligiblePhysicalCandidate(Collider candidate)
        {
            return TryGetNearestFrameworkDeclarations(candidate, out NearestFrameworkDeclarations declarations) &&
                   declarations.HasAnyValidDeclaration;
        }

        private static bool TryGetNearestFrameworkDeclarations(
            Collider candidate,
            out NearestFrameworkDeclarations declarations)
        {
            declarations = default;
            if (candidate == null)
            {
                return false;
            }

            Transform current = candidate.transform;
            while (current != null)
            {
                ActorDeclaration actorDeclaration = current.GetComponent<ActorDeclaration>();
                ObjectEntryDeclaration objectEntryDeclaration = current.GetComponent<ObjectEntryDeclaration>();
                if (actorDeclaration != null || objectEntryDeclaration != null)
                {
                    bool hasValidActorDeclaration = actorDeclaration != null &&
                        actorDeclaration.TryCreateDescriptor(
                            nameof(PEGANpcPerceptionSource),
                            out _,
                            out _);
                    ObjectEntryId objectEntryId = default;
                    bool hasValidObjectEntryDeclaration = objectEntryDeclaration != null &&
                        objectEntryDeclaration.TryGetObjectEntryId(out objectEntryId);
                    declarations = new NearestFrameworkDeclarations(
                        current,
                        hasValidActorDeclaration,
                        objectEntryDeclaration,
                        objectEntryId,
                        hasValidObjectEntryDeclaration);
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private bool TryRecognizeActorCandidate(
            PerceptionCandidate candidate,
            Collider physicalCollider,
            out ActorOccurrence occurrence,
            out string state,
            out string diagnostic)
        {
            occurrence = default;
            ActorOccurrenceReference? externalReference = GetExternalActorReference(candidate);
            if (externalReference.HasValue)
            {
                if (externalReference.Value.PlayerSlotId.IsValid &&
                    _actorReference != null &&
                    _actorReference.TryGetCurrent(out ActorOccurrenceResolution playerResolution) &&
                    playerResolution.Succeeded &&
                    SameOccurrence(externalReference.Value, playerResolution.Occurrence))
                {
                    occurrence = playerResolution.Occurrence;
                    state = "actor:resolved";
                    diagnostic = string.Empty;
                    return true;
                }

                if (TryResolveExactActorOccurrence(
                        externalReference.Value,
                        out occurrence,
                        out ActorOccurrenceResolutionStatus resolutionStatus,
                        out diagnostic))
                {
                    state = "actor:resolved";
                    return true;
                }

                state = "actor:" + resolutionStatus;
                return false;
            }

            if (_actorOccurrenceRecognitionQuery == null)
            {
                state = "actor:unavailable";
                diagnostic = $"{nameof(IActorOccurrenceRecognitionQuery)} is unavailable.";
                return false;
            }

            ActorOccurrenceRecognitionResult recognition =
                _actorOccurrenceRecognitionQuery.Recognize(physicalCollider);
            if (!recognition.Succeeded)
            {
                state = "actor:" + recognition.Status;
                diagnostic = recognition.Diagnostic;
                return false;
            }

            occurrence = recognition.Occurrence;
            state = "actor:resolved";
            diagnostic = recognition.Diagnostic;
            return true;
        }

        private bool TryRecognizeObjectEntry(
            Collider physicalCollider,
            out ObjectEntryId objectEntryId,
            out GameObject occurrenceRoot,
            out string state,
            out string diagnostic)
        {
            objectEntryId = default;
            occurrenceRoot = null;
            if (!TryGetNearestFrameworkDeclarations(physicalCollider, out NearestFrameworkDeclarations declarations) ||
                declarations.ObjectEntryDeclaration == null)
            {
                state = "object-entry:none";
                diagnostic = string.Empty;
                return false;
            }

            if (!declarations.HasValidObjectEntryDeclaration)
            {
                state = "object-entry:invalid-declaration";
                diagnostic = "Nearest ObjectEntryDeclaration has an invalid ObjectEntryId.";
                return false;
            }

            if (_objectEntryRecognitionQuery == null)
            {
                state = "object-entry:unavailable";
                diagnostic = $"{nameof(ObjectEntryOccurrenceQueryBinding)} is unavailable.";
                return false;
            }

            ObjectEntryRecognitionResult recognition = _objectEntryRecognitionQuery.Recognize(physicalCollider);
            if (!recognition.Succeeded)
            {
                state = "object-entry:" + recognition.Status;
                diagnostic = recognition.Diagnostic;
                return false;
            }

            if (!recognition.ObjectEntryId.Equals(declarations.ObjectEntryId) ||
                recognition.Occurrence != declarations.Root.gameObject)
            {
                state = "object-entry:root-mismatch";
                diagnostic = "Official Object Entry recognition does not match the nearest declaration boundary.";
                return false;
            }

            objectEntryId = recognition.ObjectEntryId;
            ObjectEntryOccurrenceResolution logicalResolution = _objectEntryRecognitionQuery.Resolve(
                StableObjectReference.ForEntry(recognition.ObjectEntryId));
            if (!logicalResolution.Succeeded)
            {
                state = "object-entry:" + logicalResolution.Status;
                diagnostic = $"Framework physically recognized ObjectEntryId '{recognition.ObjectEntryId.StableText}', but unscoped logical resolution returned {logicalResolution.Status}: {logicalResolution.Diagnostic}";
                return false;
            }

            if (logicalResolution.Occurrence != recognition.Occurrence)
            {
                state = "object-entry:logical-root-mismatch";
                diagnostic = "Unscoped official Object Entry resolution did not select the same physical root recognized from the candidate Collider.";
                return false;
            }

            occurrenceRoot = logicalResolution.Occurrence;
            state = "object-entry:resolved";
            diagnostic = string.Empty;
            return true;
        }

        private static Collider GetPhysicalCollider(PerceptionCandidate candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            for (int index = 0; index < candidate.Evidence.Count; index++)
            {
                if (candidate.Evidence[index].Kind == PerceptionCandidateEvidenceKind.PhysicalCollider &&
                    candidate.Evidence[index].PhysicalCollider != null)
                {
                    return candidate.Evidence[index].PhysicalCollider;
                }
            }

            return null;
        }

        private static ActorOccurrenceReference? GetExternalActorReference(PerceptionCandidate candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            for (int index = 0; index < candidate.Evidence.Count; index++)
            {
                PerceptionCandidateEvidence evidence = candidate.Evidence[index];
                if (evidence.Kind == PerceptionCandidateEvidenceKind.ExternalReference &&
                    evidence.ExternalReference is ActorOccurrenceReference reference &&
                    reference.IsValid && reference.IsExactOccurrence)
                {
                    return reference;
                }
            }

            return null;
        }

        private static bool IsCandidateInSensorAcquisition(
            PerceptionCandidate candidate,
            PerceptionSensorInstance sensorInstance)
        {
            if (candidate == null || sensorInstance == null)
            {
                return false;
            }

            if ((candidate.Origins & PerceptionCandidateOrigin.External) != 0)
            {
                return true;
            }

            for (int index = 0; index < candidate.Evidence.Count; index++)
            {
                PerceptionCandidateEvidence evidence = candidate.Evidence[index];
                if (evidence.Origin == PerceptionCandidateOrigin.Physical &&
                    evidence.Kind == PerceptionCandidateEvidenceKind.PhysicalCollider &&
                    ReferenceEquals(evidence.SourceReference, sensorInstance))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCompletePhysicalCoverage(
            PerceptionCandidateAcquisition acquisition,
            PerceptionSensorInstance sensorInstance)
        {
            if (acquisition == null || sensorInstance == null)
            {
                return false;
            }

            bool found = false;
            for (int index = 0; index < acquisition.Sources.Count; index++)
            {
                PerceptionCandidateSourceResult source = acquisition.Sources[index];
                if (source.Origin != PerceptionCandidateOrigin.Physical ||
                    !ReferenceEquals(source.SourceReference, sensorInstance))
                {
                    continue;
                }

                found = true;
                if (source.Status != PerceptionAcquisitionStatus.Complete)
                {
                    return false;
                }
            }

            return found;
        }

        private bool TryGetSensorObservation(
            IReadOnlyList<PerceptionCandidateSensorResult> results,
            PerceptionSensorInstance sensorInstance,
            int candidateIndex,
            PerceptionCandidate candidate,
            double observationTimeSeconds,
            out PerceptionSensorObservation observation,
            out string diagnostic)
        {
            observation = default;
            PerceptionSensorDefinition sensorDefinition = sensorInstance != null
                ? sensorInstance.Definition
                : null;
            if (results == null || sensorDefinition == null || candidate == null)
            {
                diagnostic = "Sensor result collection, role instance and candidate must be available.";
                return false;
            }

            for (int index = 0; index < results.Count; index++)
            {
                PerceptionCandidateSensorResult result = results[index];
                if (result.CandidateIndex != candidateIndex ||
                    !ReferenceEquals(result.SensorInstance, sensorInstance))
                {
                    continue;
                }

                if (!ReferenceEquals(result.CorrelationToken, candidate.CorrelationToken) ||
                    result.CandidateOrigins != candidate.Origins)
                {
                    diagnostic = $"Sensor '{sensorDefinition.SensorId}' returned evidence for another candidate batch.";
                    return false;
                }

                if (!ReferenceEquals(result.SensorDefinition, sensorDefinition))
                {
                    diagnostic = $"Sensor '{sensorDefinition.SensorId}' definition changed during observation resolution.";
                    return false;
                }

                if (!result.HasOutcome ||
                    (result.Status != PerceptionObservationStatus.Positive &&
                     result.Status != PerceptionObservationStatus.Negative))
                {
                    diagnostic = result.HasOutcome
                        ? $"Sensor '{sensorDefinition.SensorId}' returned {result.Status}; no gameplay fact is inferred."
                        : $"Sensor '{sensorDefinition.SensorId}' did not produce an observation: {result.Diagnostic}";
                    return false;
                }

                if (!(result.Evidence is PerceptionSensorObservation sensorObservation))
                {
                    diagnostic = $"Sensor '{sensorDefinition.SensorId}' did not provide positional observation evidence.";
                    return false;
                }

                if (result.ObservationTimeSeconds != observationTimeSeconds ||
                    sensorObservation.ObservationTimeSeconds != observationTimeSeconds ||
                    (result.ValidUntilSeconds.HasValue && observationTimeSeconds > result.ValidUntilSeconds.Value))
                {
                    diagnostic = $"Sensor '{sensorDefinition.SensorId}' returned evidence outside the current observation time.";
                    return false;
                }

                bool expectedDetection = result.Status == PerceptionObservationStatus.Positive;
                if (sensorObservation.IsDetected != expectedDetection)
                {
                    diagnostic = $"Sensor '{sensorDefinition.SensorId}' status does not match its positional evidence.";
                    return false;
                }

                observation = sensorObservation;
                diagnostic = string.Empty;
                return true;
            }

            diagnostic = $"Perception Sensor Set returned no result for candidate {candidateIndex} and sensor instance '{sensorInstance.name}' ({sensorDefinition.SensorId}).";
            return false;
        }

        private void LogAcquisitionTransition(PerceptionCandidateAcquisition acquisition)
        {
            if (acquisition == null)
            {
                return;
            }

            int physicalCount = 0;
            int externalCount = 0;
            for (int index = 0; index < acquisition.Candidates.Count; index++)
            {
                PerceptionCandidate candidate = acquisition.Candidates[index];
                if ((candidate.Origins & PerceptionCandidateOrigin.Physical) != 0) physicalCount++;
                if ((candidate.Origins & PerceptionCandidateOrigin.External) != 0) externalCount++;
            }

            string diagnostic = string.Empty;
            for (int index = 0; index < acquisition.Sources.Count; index++)
            {
                string sourceDiagnostic = acquisition.Sources[index].Diagnostic;
                if (!string.IsNullOrWhiteSpace(sourceDiagnostic))
                {
                    diagnostic = JoinDiagnostics(diagnostic, sourceDiagnostic);
                }
            }

            bool changed = !_hasLoggedAcquisitionState ||
                           _lastAcquisitionStatus != acquisition.Status ||
                           _lastPhysicalCandidateCount != physicalCount ||
                           _lastExternalCandidateCount != externalCount ||
                           !string.Equals(_lastAcquisitionDiagnostic, diagnostic, StringComparison.Ordinal);
            if (!changed)
            {
                return;
            }

            bool recovered = _hasLoggedAcquisitionState &&
                             _lastAcquisitionStatus == PerceptionAcquisitionStatus.Unavailable &&
                             acquisition.Status != PerceptionAcquisitionStatus.Unavailable;
            _hasLoggedAcquisitionState = true;
            _lastAcquisitionStatus = acquisition.Status;
            _lastPhysicalCandidateCount = physicalCount;
            _lastExternalCandidateCount = externalCount;
            _lastAcquisitionDiagnostic = diagnostic;

            if (acquisition.Status == PerceptionAcquisitionStatus.Unavailable ||
                acquisition.Status == PerceptionAcquisitionStatus.Partial)
            {
                _log.Warning(
                    recovered
                        ? "NPC Perception acquisition recovered with partial candidate coverage."
                        : "NPC Perception candidate acquisition is partial or unavailable.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("acquisitionStatus", acquisition.Status),
                    LogFields.Field("physicalCandidateCount", physicalCount),
                    LogFields.Field("externalCandidateCount", externalCount),
                    LogFields.Field("sourceCount", acquisition.Sources.Count),
                    LogFields.Field("diagnostic", diagnostic));
            }
            else
            {
                _log.Info(
                    recovered
                        ? "NPC Perception candidate acquisition recovered."
                        : "NPC Perception acquired a complete candidate batch.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("acquisitionStatus", acquisition.Status),
                    LogFields.Field("physicalCandidateCount", physicalCount),
                    LogFields.Field("externalCandidateCount", externalCount),
                    LogFields.Field("sourceCount", acquisition.Sources.Count));
            }
        }

        private void LogCandidateRecognition(
            Collider candidate,
            string state,
            string identitySummary,
            string diagnostic)
        {
            if (candidate == null)
            {
                return;
            }

            if (_candidateRecognitionLogStates.TryGetValue(candidate, out string previousState) &&
                string.Equals(previousState, state, StringComparison.Ordinal))
            {
                return;
            }

            _candidateRecognitionLogStates[candidate] = state;
            bool recognized = state != null &&
                              (state.IndexOf("actor:resolved", StringComparison.Ordinal) >= 0 ||
                               state.IndexOf("object-entry:resolved", StringComparison.Ordinal) >= 0);
            if (recognized)
            {
                _log.Info(
                    "NPC Perception evidence recognized a declared entity through an official Framework query.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("candidateColliderEntityId", candidate.GetEntityId().ToString()),
                    LogFields.Field("recognitionState", state),
                    LogFields.Field("officialIdentity", identitySummary),
                    LogFields.Field("diagnostic", diagnostic));
            }
            else
            {
                _log.Warning(
                    "NPC Perception evidence has no unique official identity and occurrence suitable for Knowledge publication.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("candidateColliderEntityId", candidate.GetEntityId().ToString()),
                    LogFields.Field("recognitionState", state),
                    LogFields.Field("diagnostic", diagnostic));
            }
        }

        private void PruneCandidateRecognitionLogStates()
        {
            if (_candidateRecognitionLogStates.Count == 0)
            {
                return;
            }

            var removed = new List<Collider>();
            foreach (KeyValuePair<Collider, string> pair in _candidateRecognitionLogStates)
            {
                if (pair.Key == null || !_candidateCollidersThisSample.Contains(pair.Key))
                {
                    removed.Add(pair.Key);
                }
            }

            for (int index = 0; index < removed.Count; index++)
            {
                _candidateRecognitionLogStates.Remove(removed[index]);
            }
        }

        private void PublishRecognizedActorObservation(
            ActorOccurrence occurrence,
            Vector3 observedRootPosition,
            bool hasVisualContact,
            double observationTimeSeconds,
            bool isTargetPlayer,
            bool forceCurrentEvidence)
        {
            if (!occurrence.IsValid || !occurrence.ActorId.IsValid)
            {
                return;
            }

            PEGAKnowledgeFact<bool> previousContact = _knowledgeState.GetCharacterVisualContact(occurrence.ActorId);
            bool hadPreviousContact = previousContact.TryGetValue(out bool previousVisualContact);
            bool isReacquisition = hasVisualContact &&
                                   (hadPreviousContact && !previousVisualContact || forceCurrentEvidence);
            bool isContactLoss = !hasVisualContact && hadPreviousContact && previousVisualContact;

            if (hasVisualContact)
            {
                PEGAKnowledgeFact<Vector3> previousPosition =
                    _knowledgeState.GetLastObservedCharacterPosition(occurrence.ActorId);
                if (isReacquisition ||
                    !previousPosition.TryGetValue(out Vector3 storedPosition) ||
                    !storedPosition.Equals(observedRootPosition))
                {
                    _knowledgeState.ObserveCharacterPosition(
                        occurrence.ActorId,
                        observedRootPosition,
                        PEGANpcEvidenceOrigin.Perception,
                        observationTimeSeconds);
                }

                if (forceCurrentEvidence)
                {
                    _knowledgeState.ObserveCharacterVisualContact(
                        occurrence.ActorId,
                        true,
                        PEGANpcEvidenceOrigin.Perception,
                        observationTimeSeconds);
                }
                else
                {
                    PublishTargetContactIfChanged(occurrence.ActorId, true, observationTimeSeconds);
                }
                if (occurrence.HasPlayerSlot)
                {
                    PublishPlayerFactIfUnknown(occurrence, observationTimeSeconds);
                }
                _recognizedActorReferences[occurrence.ActorId] = ActorOccurrenceReference.ForOccurrence(occurrence);
            }
            else
            {
                PublishTargetContactIfChanged(occurrence.ActorId, false, observationTimeSeconds);
            }

            if (hasVisualContact && (!hadPreviousContact || !previousVisualContact) ||
                !hasVisualContact && hadPreviousContact && previousVisualContact)
            {
                _log.Debug(
                    hasVisualContact
                        ? "NPC visual contact with a Framework Actor changed to positive."
                        : "NPC visual contact with a known Framework Actor changed to negative.",
                    LogFields.Field("observerActorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", occurrence.ActorId.StableText),
                    LogFields.Field("subjectActorOccurrenceId", occurrence.Id.ToString()),
                    LogFields.Field("isPlayerSlotActor", occurrence.HasPlayerSlot),
                    LogFields.Field("isConfiguredTarget", isTargetPlayer),
                    LogFields.Field("visualContact", hasVisualContact),
                    LogFields.Field("observationTimeSeconds", observationTimeSeconds));
            }

            if (!isTargetPlayer)
            {
                return;
            }

            if (hasVisualContact)
            {
                CancelLostContactReevaluationAfterRealReacquisition(
                    occurrence.ActorId,
                    observationTimeSeconds);
            }
            else if (isContactLoss)
            {
                BeginLostContactReevaluation(
                    occurrence.ActorId,
                    observationTimeSeconds);
            }

            if (isReacquisition)
            {
                PublishPlanReevaluationRequestAfterReacquisition(
                    occurrence.ActorId,
                    observationTimeSeconds);
            }
        }

        private void LogAreaPresenceTransition(
            PEGAKnowledgeFact<PEGANpcPerceivedAreaPresence> previousArea,
            bool isPresent,
            double observationTimeSeconds)
        {
            bool hadPrevious = previousArea.TryGetValue(out PEGANpcPerceivedAreaPresence previous);
            if ((hadPrevious && previous.IsPresent == isPresent) ||
                (!hadPrevious && !isPresent))
            {
                return;
            }

            _log.Debug(
                "NPC radial presence aggregate changed from the shared candidate batch.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("perceivedAreaPresence", isPresent),
                LogFields.Field("sensorInstance", perceivedAreaSensorInstance != null
                    ? perceivedAreaSensorInstance.name
                    : string.Empty),
                LogFields.Field("observationTimeSeconds", observationTimeSeconds));
        }

        private static bool SameOccurrence(ActorOccurrence left, ActorOccurrence right) =>
            left.Id == right.Id && left.ActorRoot == right.ActorRoot && left.ActorId.Equals(right.ActorId);

        private static bool SameOccurrence(ActorOccurrenceReference expected, ActorOccurrence actual) =>
            expected.IsExactOccurrence &&
            expected.ExpectedOccurrence == actual.Id &&
            expected.ActorId.Equals(actual.ActorId) &&
            (!expected.HasOwner || expected.Owner.Equals(actual.Owner)) &&
            (!expected.PlayerSlotId.IsValid || expected.PlayerSlotId.Equals(actual.PlayerSlotId));

        private static void AddStaleActorContact(
            ICollection<StaleActorContact> staleContacts,
            ActorId actorId,
            ActorOccurrenceId occurrenceId)
        {
            foreach (StaleActorContact current in staleContacts)
            {
                if (current.ActorId.Equals(actorId) && current.OccurrenceId == occurrenceId)
                {
                    return;
                }
            }

            staleContacts.Add(new StaleActorContact(actorId, occurrenceId));
        }

        private void RemoveStaleActorReference(StaleActorContact staleContact)
        {
            if (_recognizedActorReferences.TryGetValue(staleContact.ActorId, out ActorOccurrenceReference currentReference) &&
                currentReference.IsExactOccurrence &&
                currentReference.ExpectedOccurrence == staleContact.OccurrenceId)
            {
                _recognizedActorReferences.Remove(staleContact.ActorId);
            }
        }

        private static bool IsSameStaleActorOccurrence(
            IReadOnlyList<StaleActorContact> staleContacts,
            ActorId actorId,
            ActorOccurrenceId occurrenceId)
        {
            for (int index = 0; index < staleContacts.Count; index++)
            {
                StaleActorContact stale = staleContacts[index];
                if (stale.ActorId.Equals(actorId) && stale.OccurrenceId == occurrenceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasDifferentStaleActorOccurrence(
            IReadOnlyList<StaleActorContact> staleContacts,
            ActorId actorId,
            ActorOccurrenceId currentOccurrenceId)
        {
            for (int index = 0; index < staleContacts.Count; index++)
            {
                StaleActorContact stale = staleContacts[index];
                if (stale.ActorId.Equals(actorId) && stale.OccurrenceId != currentOccurrenceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static string JoinDiagnostics(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first)) return second ?? string.Empty;
            if (string.IsNullOrWhiteSpace(second)) return first;
            return first + " " + second;
        }

        private void PublishAreaPresenceIfChanged(bool isPresent, double observationTimeSeconds)
        {
            PEGAKnowledgeFact<PEGANpcPerceivedAreaPresence> presence =
                _knowledgeState.GetPerceivedAreaPresence();
            if (presence.TryGetValue(out PEGANpcPerceivedAreaPresence previousPresence) &&
                previousPresence.IsPresent == isPresent)
            {
                return;
            }

            if (presence.Status == PEGAKnowledgeStatus.Unknown && !isPresent)
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

        private void PublishTargetContactIfChanged(
            ActorId subjectActorId,
            bool hasVisualContact,
            double observationTimeSeconds)
        {
            PEGAKnowledgeFact<bool> contact = _knowledgeState.GetCharacterVisualContact(subjectActorId);
            if (contact.TryGetValue(out bool previousContact) && previousContact == hasVisualContact)
            {
                return;
            }

            if (contact.Status == PEGAKnowledgeStatus.Unknown && !hasVisualContact)
            {
                return;
            }

            _knowledgeState.ObserveCharacterVisualContact(
                subjectActorId,
                hasVisualContact,
                PEGANpcEvidenceOrigin.Perception,
                observationTimeSeconds);
        }

        private void PublishPlayerFactIfUnknown(
            ActorOccurrence occurrence,
            double observationTimeSeconds)
        {
            if (!occurrence.HasPlayerSlot || !occurrence.ActorId.IsValid)
            {
                return;
            }

            PEGAKnowledgeFactKey<ActorId, bool> key =
                PEGANpcKnowledgeFacts.IsPlayer.For(occurrence.ActorId);
            PEGAKnowledgeFact<bool> playerFact = _knowledgeState.GetFact(key);
            if (PEGANpcKnowledgeFacts.HasPerceptionBackedPlayerFact(playerFact))
            {
                return;
            }

            _knowledgeState.ObserveFact(
                key,
                true,
                PEGAKnowledgeEvidence.From(
                    PEGANpcEvidenceOrigin.Perception,
                    observationTimeSeconds,
                    this,
                    occurrence.PlayerSlotId));
        }

        private void PublishPlanReevaluationRequestAfterReacquisition(
            ActorId subjectActorId,
            double observationTimeSeconds)
        {
            PEGAKnowledgeFact<bool> contact =
                _knowledgeState.GetCharacterVisualContact(subjectActorId);
            PEGAKnowledgeFact<Vector3> position =
                _knowledgeState.GetLastObservedCharacterPosition(subjectActorId);

            bool hasConsolidatedEvidence =
                contact.TryGetValue(out bool hasVisualContact) &&
                hasVisualContact &&
                HasCurrentPerceptionEvidence(contact.HasOrigin(PEGANpcEvidenceOrigin.Perception), contact.HasObservationTime,
                    contact.ObservationTimeSeconds, observationTimeSeconds) &&
                position.TryGetValue(out _) &&
                HasCurrentPerceptionEvidence(position.HasOrigin(PEGANpcEvidenceOrigin.Perception), position.HasObservationTime,
                    position.ObservationTimeSeconds, observationTimeSeconds);

            if (!hasConsolidatedEvidence)
            {
                _log.Warning(
                    "NPC plan re-evaluation request was withheld because Target reacquisition evidence was not fully committed.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", subjectActorId.StableText),
                    LogFields.Field("observationTimeSeconds", observationTimeSeconds),
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
            bool hasPerceptionOrigin,
            bool hasObservationTime,
            double evidenceTimeSeconds,
            double observationTimeSeconds) =>
            hasPerceptionOrigin &&
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
            ObserveCurrentPlayer(requestedAtTimeSeconds, true);

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
            PEGAKnowledgeFact<bool> contact = _knowledgeState.GetCharacterVisualContact(subjectActorId);
            return contact.TryGetValue(out bool actualContact) &&
                   actualContact == expectedContact &&
                   contact.HasOrigin(PEGANpcEvidenceOrigin.Perception) &&
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
                false,
                false,
                false,
                false,
                GetObserverOrigin(),
                Vector3.zero,
                false,
                false,
                false,
                false,
                false,
                false,
                PerceptionAcquisitionStatus.Unavailable,
                string.Empty,
                Array.Empty<PEGANpcPerceptionCandidateGizmoState>(),
                Array.Empty<PEGANpcPerceptionAcquisitionGizmoState>());
#endif
        }

        private Vector3 GetObserverOrigin() =>
            _areaPresenceSensor != null
                ? _areaPresenceSensor.GetObserverOrigin(transform)
                : _visionConeSensor != null
                    ? _visionConeSensor.GetObserverOrigin(transform)
                    : transform.position;

        private static bool IsPositiveFinite(float value) =>
            value > 0f && IsFinite(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

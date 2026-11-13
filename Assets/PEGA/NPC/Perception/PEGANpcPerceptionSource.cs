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
using PEGA.Interactions.Runtime;
using PEGA.Interactions.Runtime.ObjectEntry;
using PEGA.NPC.Actions.Runtime;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
namespace PEGA.NPC.Perception
{
    /// <summary>
    /// PEGA adapter that acquires declared physical candidates, resolves Framework identities after sensor
    /// evaluation, and translates observations into Knowledge State facts and targeted Autonomy requests.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PegaNpcKnowledgeState))]
    [RequireComponent(typeof(PerceptionSensorSet))]
    [RequireComponent(typeof(ActorOccurrenceQueryBinding))]
    [RequireComponent(typeof(ObjectEntryOccurrenceQueryBinding))]
    [AddComponentMenu("PEGA/Perception/NPC/Perception Source")]
    public sealed class PegaNpcPerceptionSource : MonoBehaviour, IPegaNpcTargetObservationSource
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

        private readonly struct ResolvedCandidateIdentity
        {
            public ResolvedCandidateIdentity(
                ActorId actorId,
                ActorOccurrenceId actorOccurrenceId,
                ObjectEntryId objectEntryId)
            {
                ActorId = actorId;
                ActorOccurrenceId = actorOccurrenceId;
                ObjectEntryId = objectEntryId;
            }

            public ActorId ActorId { get; }
            public ActorOccurrenceId ActorOccurrenceId { get; }
            public ObjectEntryId ObjectEntryId { get; }
        }

        [Header("Gameplay Sensor Roles")]
        [SerializeField]
        [Tooltip("Instância radial que descobre Object Entries e mantém presença anônima apenas para candidatos ainda sem identidade reconhecida.")]
        private PerceptionSensorInstance perceivedAreaSensorInstance;

        [SerializeField]
        [Tooltip("Instância visual que atualiza contato e identidade de personagens; também pode observar Object Entries.")]
        private PerceptionSensorInstance visionConeSensorInstance;

        [Header("Lost Target")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Tempo de jogo sem readquirir Target após perder contato visual antes de pedir a seleção normal de Desejos (s). O valor inicial de 5 s é provisório e deve ser calibrado por missão.")]
        private float lostContactEvadedIntervalSeconds = 5f;

        [Header("Scene View")]
        [SerializeField]
        [Tooltip("Off oculta a percepção; Resumido mostra limites reais, R (radial), V (visual), Aq (aquisição) e candidatos; Detalhado preserva estados, identidades e diagnósticos completos.")]
        private PegaDiagnosticVisualizationLevel perceptionVisualizationLevel =
            PegaDiagnosticVisualizationLevel.Summary;

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
        private PegaNpcKnowledgeState _knowledgeState;

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
        private EventBus<PegaNpcPlanReevaluationRequestedEvent> _planReevaluationEvents;

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
        private readonly Dictionary<Transform, ResolvedCandidateIdentity> _resolvedCandidateRoots =
            new Dictionary<Transform, ResolvedCandidateIdentity>();

        [System.NonSerialized]
        private readonly Dictionary<Collider, string> _objectEntryOpportunityLogStates =
            new Dictionary<Collider, string>();

        [System.NonSerialized]
        private string _lastMissingPhysicalColliderDetectionState;

        [System.NonSerialized]
        private readonly Dictionary<Collider, long> _radialOpportunitySequencesByCollider =
            new Dictionary<Collider, long>();

        [System.NonSerialized]
        private readonly Dictionary<ActorOccurrenceId, long> _radialOpportunitySequencesByOccurrence =
            new Dictionary<ActorOccurrenceId, long>();

        [System.NonSerialized]
        private readonly Dictionary<long, PegaNpcPerceivedAreaOpportunity> _radialOpportunityEpisodesBySequence =
            new Dictionary<long, PegaNpcPerceivedAreaOpportunity>();

        [System.NonSerialized]
        private readonly HashSet<Collider> _unresolvedCollidersThisSample = new HashSet<Collider>();

        [System.NonSerialized]
        private readonly HashSet<ActorOccurrenceId> _unresolvedOccurrencesThisSample =
            new HashSet<ActorOccurrenceId>();

        [System.NonSerialized]
        private long _nextRadialOpportunitySequence;

        [System.NonSerialized]
        private long _unboundRadialOpportunitySequence;

        [System.NonSerialized]
        private bool _hasUnboundUnresolvedCandidateThisSample;

        [System.NonSerialized]
        private readonly HashSet<Collider> _candidateCollidersThisSample = new HashSet<Collider>();

        [System.NonSerialized]
        private string _lastObserverOccurrenceDiagnostic;

        [System.NonSerialized]
        private ActorOccurrenceId _lastObserverOccurrenceId;

        [System.NonSerialized]
        private bool _hasResolvedObserverOccurrence;

        [System.NonSerialized]
        private ActorOccurrenceResolutionStatus? _lastObserverOccurrenceResolutionStatus;

        [System.NonSerialized]
        private bool _lastObserverOccurrenceWasWarning;

        [System.NonSerialized]
        private string _lastExternalTargetDiagnostic;

        [System.NonSerialized]
        private ActorOccurrenceResolutionStatus? _lastExternalTargetResolutionStatus;

        [System.NonSerialized]
        private bool _lastExternalTargetHadReference;

        [System.NonSerialized]
        private bool _lastExternalTargetWasAvailable;

        [System.NonSerialized]
        private bool _lastExternalTargetWasWarning;

        [System.NonSerialized]
        private bool _hasLoggedExternalTargetState;

        [System.NonSerialized]
        private bool _hasResolvedExternalPlayerOccurrence;

        [System.NonSerialized]
        private bool _hasLoggedAcquisitionState;

        [System.NonSerialized]
        private PerceptionAcquisitionStatus _lastAcquisitionStatus;

        [System.NonSerialized]
        private bool _lastAcquisitionHadUnexpectedFailure;

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
        private PegaNpcPerceptionGizmoState _gizmoState;

        public readonly struct PegaNpcPerceptionSensorGizmoResult
        {
            internal PegaNpcPerceptionSensorGizmoResult(
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

        public readonly struct PegaNpcPerceptionCandidateGizmoState
        {
            internal PegaNpcPerceptionCandidateGizmoState(
                int candidateIndex,
                PerceptionCandidateOrigin origins,
                bool hasPosition,
                Vector3 position,
                PegaNpcPerceptionSensorGizmoResult radial,
                PegaNpcPerceptionSensorGizmoResult visual,
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
            public PegaNpcPerceptionSensorGizmoResult Radial { get; }
            public PegaNpcPerceptionSensorGizmoResult Visual { get; }
            public bool IdentityRecognized { get; }
            public string IdentitySummary { get; }
            public string IdentityDiagnostic { get; }
        }

        public readonly struct PegaNpcPerceptionAcquisitionGizmoState
        {
            internal PegaNpcPerceptionAcquisitionGizmoState(
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

        public readonly struct PegaNpcPerceptionGizmoState
        {
            internal PegaNpcPerceptionGizmoState(
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
                IReadOnlyList<PegaNpcPerceptionCandidateGizmoState> candidates,
                IReadOnlyList<PegaNpcPerceptionAcquisitionGizmoState> acquisitionVolumes)
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
            public IReadOnlyList<PegaNpcPerceptionCandidateGizmoState> Candidates { get; }
            public IReadOnlyList<PegaNpcPerceptionAcquisitionGizmoState> AcquisitionVolumes { get; }

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

        public PegaDiagnosticVisualizationLevel PerceptionVisualizationLevel =>
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

        public PegaNpcPerceptionGizmoState LatestGizmoState => _gizmoState;
#endif

        private void Awake()
        {
            _log = PegaLogging.CreateScopedLogger<PegaNpcPerceptionSource>(
                LogCategory,
                gameObject.name);

            _actorReference = GetComponent<ActorOccurrenceReferenceBinding>();
            _actorDeclaration = GetComponent<ActorDeclaration>();
            _actorOccurrenceQuery = GetComponent<ActorOccurrenceQueryBinding>();
            _actorOccurrenceRecognitionQuery = _actorOccurrenceQuery as IActorOccurrenceRecognitionQuery;
            _objectEntryRecognitionQuery = GetComponent<ObjectEntryOccurrenceQueryBinding>();
            _knowledgeState = GetComponent<PegaNpcKnowledgeState>();
            _perceptionSensorSet = GetComponent<PerceptionSensorSet>();

            if (!TryValidateConfiguration(out string issue))
            {
                _log.Error(
                    $"{nameof(PegaNpcPerceptionSource)} requires Framework query authorities, Knowledge State and configured Perception sensor assets.",
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
                    $"{nameof(PegaNpcPerceptionSource)} could not become the Actor's Perception sampling driver.",
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
            Action<PegaNpcPlanReevaluationRequestedEvent> handler)
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
                diagnostic = $"{nameof(PegaNpcKnowledgeState)} is missing from the NPC root.";
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
                out List<StaleActorContact> staleActorContacts,
                out ActorOccurrenceResolutionStatus? playerReferenceStatus);

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
            LogAcquisitionTransition(
                acquisition,
                observerOccurrence.HasValue,
                _actorReference != null,
                playerReferenceStatus);

            bool radialWasSampled = ContainsSensorInstance(sampledSensorInstances, perceivedAreaSensorInstance);
            bool visualWasSampled = ContainsSensorInstance(sampledSensorInstances, visionConeSensorInstance);
            bool hasRadialPositive = false;
            bool allRadialCandidatesUsable = true;
            int unresolvedRadialCandidateCount = 0;
            int resolvedRadialCandidateCount = 0;
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
            var radiallyConfirmedObjectEntryIds = new HashSet<ObjectEntryId>();
            var visuallyConfirmedObjectEntryIds = new HashSet<ObjectEntryId>();
            var objectEntryEvidenceColliders = new Dictionary<ObjectEntryId, Collider>();
            _unresolvedCollidersThisSample.Clear();
            _unresolvedOccurrencesThisSample.Clear();
            _hasUnboundUnresolvedCandidateThisSample = false;
            long unresolvedRadialOpportunitySequence = 0;
            bool hasExplicitRadialResolution = false;
#if UNITY_EDITOR
            var candidateIdentityRecognized = new bool[acquisition.Candidates.Count];
            var candidateIdentitySummaries = new string[acquisition.Candidates.Count];
            var candidateIdentityDiagnostics = new string[acquisition.Candidates.Count];
#endif
            _candidateCollidersThisSample.Clear();
            int radialOnlyDetectionsWithoutCollider = 0;
            int visualOnlyDetectionsWithoutCollider = 0;
            int combinedDetectionsWithoutCollider = 0;
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
                NearestFrameworkDeclarations candidateDeclarations = default;
                bool hasCandidateDeclarations = physicalCollider != null &&
                    TryGetNearestFrameworkDeclarations(physicalCollider, out candidateDeclarations);
                Transform candidateRoot = hasCandidateDeclarations
                    ? candidateDeclarations.Root
                    : null;
                GetPreviouslyRecognizedCandidateIdentity(
                    candidateRoot,
                    candidateDeclarations,
                    externalActorReference,
                    out bool actorIdentityResolved,
                    out bool objectEntryIdentityResolved);
                bool candidateHasActorIdentity = externalActorReference.HasValue ||
                    candidateDeclarations.HasValidActorDeclaration;
                bool radialDetected = hasRadialObservation && radialObservation.IsDetected;
                bool visualDetected = hasVisionObservation && visionObservation.IsDetected;
                if (physicalCollider == null && (radialDetected || visualDetected))
                {
                    if (radialDetected && visualDetected)
                    {
                        combinedDetectionsWithoutCollider++;
                    }
                    else if (radialDetected)
                    {
                        radialOnlyDetectionsWithoutCollider++;
                    }
                    else
                    {
                        visualOnlyDetectionsWithoutCollider++;
                    }
                }

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

                ObjectEntryId objectEntryId = default;
                GameObject objectEntryRoot = null;
                string objectRecognitionState = "object-entry:not-detected";
                string objectRecognitionDiagnostic = string.Empty;
                bool objectEntryRecognized = false;
                if (physicalCollider != null && (radialDetected || visualDetected))
                {
                    objectEntryRecognized = TryRecognizeObjectEntry(
                        physicalCollider,
                        out objectEntryId,
                        out objectEntryRoot,
                        out objectRecognitionState,
                        out objectRecognitionDiagnostic);
                    objectEntryIdentityResolved |= objectEntryRecognized;

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
                            objectEntryEvidenceColliders[objectEntryId] = physicalCollider;
                        }

                        if (radialDetected)
                        {
                            radiallyConfirmedObjectEntryIds.Add(objectEntryId);
                        }
                        if (visualDetected)
                        {
                            visuallyConfirmedObjectEntryIds.Add(objectEntryId);
                        }

                        RememberResolvedCandidateRoot(objectEntryRoot.transform, default, objectEntryId);
                    }

                    bool unresolvedActorOrEntity = candidateHasActorIdentity
                        ? !actorIdentityResolved
                        : !objectEntryIdentityResolved;
                    if (!unresolvedActorOrEntity)
                    {
                        hasExplicitRadialResolution |=
                            CloseUnresolvedRadialOpportunity(physicalCollider, externalActorReference);
                    }

#if UNITY_EDITOR
                    if (objectEntryRecognized)
                    {
                        candidateIdentityRecognized[candidateIndex] = true;
                        candidateIdentitySummaries[candidateIndex] =
                            "Object Entry " + objectEntryId.StableText;
                        candidateIdentityDiagnostics[candidateIndex] = string.Empty;
                    }
                    else if (!string.IsNullOrWhiteSpace(objectRecognitionDiagnostic))
                    {
                        candidateIdentityDiagnostics[candidateIndex] = objectRecognitionDiagnostic;
                    }
#endif

                    if (!visualDetected)
                    {
                        string identitySummary = objectEntryRecognized
                            ? "Object Entry " + objectEntryId.StableText
                            : actorIdentityResolved && externalActorReference.HasValue
                                ? "Previously resolved Actor " + externalActorReference.Value.ActorId.StableText
                                : string.Empty;
                        string state = (actorIdentityResolved ? "actor:resolved;" : "actor:unresolved-by-radial;") +
                                       objectRecognitionState;
                        string candidateDiagnostic = JoinDiagnostics(
                            actorIdentityResolved ? string.Empty : "Radial evidence does not resolve Actor identity.",
                            objectRecognitionDiagnostic);
                        LogCandidateRecognition(
                            physicalCollider,
                            state,
                            identitySummary,
                            candidateDiagnostic,
                            radialDetected,
                            visualDetected,
                            expectedAnonymousRadialPresence: radialDetected && unresolvedActorOrEntity);
                    }
                }

                if (!visualWasSampled || !hasVisionObservation)
                {
                    if (radialDetected && (candidateHasActorIdentity
                            ? !actorIdentityResolved
                            : !objectEntryIdentityResolved))
                    {
                        unresolvedRadialCandidateCount++;
                        unresolvedRadialOpportunitySequence = Math.Max(
                            unresolvedRadialOpportunitySequence,
                            RegisterUnresolvedRadialOpportunity(physicalCollider, externalActorReference));
                    }

                    continue;
                }

                if (visualDetected)
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

                    actorIdentityResolved |= actorRecognized;
                    bool recognizedAnyDeclaration = actorRecognized || objectEntryRecognized;
                    if (recognizedAnyDeclaration)
                    {
                        if (!candidateHasActorIdentity || actorIdentityResolved)
                        {
                            hasExplicitRadialResolution |=
                                CloseUnresolvedRadialOpportunity(physicalCollider, externalActorReference);
                        }
                        RememberResolvedCandidateRoot(
                            candidateRoot != null
                                ? candidateRoot
                                : actorRecognized && actorOccurrence.ActorRoot != null
                                    ? actorOccurrence.ActorRoot.transform
                                    : objectEntryRecognized && objectEntryRoot != null
                                        ? objectEntryRoot.transform
                                        : null,
                            actorRecognized ? actorOccurrence : default,
                            objectEntryRecognized ? objectEntryId : default);
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
                        else if (objectEntryIdentityResolved && externalActorReference.HasValue)
                        {
                            identitySummary = string.IsNullOrEmpty(identitySummary)
                                ? "Previously resolved Object Entry identity"
                                : identitySummary + ", previously resolved Object Entry identity";
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
                            combinedDiagnostic,
                            radialDetected,
                            visualDetected);
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

                if (hasRadialObservation && radialObservation.IsDetected)
                {
                    bool unresolvedActorOrEntity = candidateHasActorIdentity
                        ? !actorIdentityResolved
                        : !objectEntryIdentityResolved;
                    if (!unresolvedActorOrEntity)
                    {
                        resolvedRadialCandidateCount++;
                    }
                    else
                    {
                        unresolvedRadialCandidateCount++;
                        unresolvedRadialOpportunitySequence = Math.Max(
                            unresolvedRadialOpportunitySequence,
                            RegisterUnresolvedRadialOpportunity(physicalCollider, externalActorReference));
                    }
                }
            }

            LogMissingPhysicalColliderDetections(
                radialOnlyDetectionsWithoutCollider,
                visualOnlyDetectionsWithoutCollider,
                combinedDetectionsWithoutCollider);
            PruneCandidateRecognitionLogStates();
            PruneResolvedCandidateRoots();
            PruneObjectEntryOpportunityLogStates();

            bool completeRadialCoverage = radialWasSampled &&
                HasCompleteRadialCoverage(acquisition, perceivedAreaSensorInstance) &&
                allRadialCandidatesUsable &&
                staleActorContacts.Count == 0;
            if (completeRadialCoverage)
            {
                PruneUnresolvedRadialOpportunities();
            }

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

            PegaKnowledgeFact<PegaNpcPerceivedAreaPresence> previousArea =
                _knowledgeState.GetPerceivedAreaPresence();
            if (publishRadialPresence)
            {
                PublishAreaPresenceIfChanged(radialPresence, observationTimeSeconds);
                LogAreaPresenceTransition(previousArea, radialPresence, observationTimeSeconds);
            }

            if (_radialOpportunityEpisodesBySequence.Count > 0 &&
                (radialWasSampled || hasExplicitRadialResolution))
            {
                PegaKnowledgeFact<PegaNpcPerceivedAreaPresence> previousUnresolvedPresence =
                    _knowledgeState.GetUnresolvedRadialPresence();
                PegaNpcPerceivedAreaPresence unresolvedPresence =
                    CreateUnresolvedRadialPresenceValue();
                PublishUnresolvedRadialPresenceIfChanged(unresolvedPresence, observationTimeSeconds);
                LogUnresolvedRadialPresenceTransition(
                    previousUnresolvedPresence,
                    unresolvedPresence,
                    observationTimeSeconds,
                    unresolvedRadialCandidateCount,
                    resolvedRadialCandidateCount);
            }
            else if (radialWasSampled && completeRadialCoverage)
            {
                PegaKnowledgeFact<PegaNpcPerceivedAreaPresence> previousUnresolvedPresence =
                    _knowledgeState.GetUnresolvedRadialPresence();
                PegaNpcPerceivedAreaPresence noUnresolvedPresence = new PegaNpcPerceivedAreaPresence(
                    false,
                    Vector3.zero,
                    _areaPresenceSensor.RadiusMeters);
                PublishUnresolvedRadialPresenceIfChanged(noUnresolvedPresence, observationTimeSeconds);
                LogUnresolvedRadialPresenceTransition(
                    previousUnresolvedPresence,
                    noUnresolvedPresence,
                    observationTimeSeconds,
                    unresolvedRadialCandidateCount,
                    resolvedRadialCandidateCount);
            }
            else if (hasExplicitRadialResolution)
            {
                PegaKnowledgeWriteResult result = _knowledgeState.InvalidateUnresolvedRadialPresence(
                    PegaNpcEvidenceOrigin.Perception,
                    observationTimeSeconds);
                if (result == PegaKnowledgeWriteResult.InvalidatedExplicitly)
                {
                    _log.Debug(
                        "NPC perception resolved the active anonymous radial opportunity; unresolved-presence Knowledge became unknown because global radial absence is not established.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("unresolvedRadialPresence", false),
                        LogFields.Field("unresolvedRadialCandidateCount", unresolvedRadialCandidateCount),
                        LogFields.Field("resolvedRadialCandidateCount", resolvedRadialCandidateCount),
                        LogFields.Field("observationTimeSeconds", observationTimeSeconds));
                }
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
                bool radialDetection = radiallyConfirmedObjectEntryIds.Contains(pair.Key);
                bool visualDetection = visuallyConfirmedObjectEntryIds.Contains(pair.Key);
                objectEntryEvidenceColliders.TryGetValue(pair.Key, out Collider evidenceCollider);
                if (ambiguousObjectEntryIds.Contains(pair.Key))
                {
                    LogObjectEntryPickUpOpportunity(
                        evidenceCollider,
                        pair.Key,
                        radialDetection,
                        visualDetection,
                        "object-entry-occurrence-ambiguous",
                        false,
                        false,
                        default,
                        "not-published",
                        "Multiple physical roots resolved to the same ObjectEntryId in this candidate batch.");
                    continue;
                }

                if (pair.Value == null)
                {
                    LogObjectEntryPickUpOpportunity(
                        evidenceCollider,
                        pair.Key,
                        radialDetection,
                        visualDetection,
                        "object-entry-root-unavailable",
                        false,
                        false,
                        default,
                        "not-published",
                        "The recognized Object Entry root was unavailable before Knowledge publication.");
                    continue;
                }

                Vector3 observedPosition = pair.Value.transform.position;
                if (!IsFinite(observedPosition))
                {
                    LogObjectEntryPickUpOpportunity(
                        evidenceCollider,
                        pair.Key,
                        radialDetection,
                        visualDetection,
                        "object-entry-position-invalid",
                        false,
                        false,
                        default,
                        "not-published",
                        "The recognized Object Entry root position is not finite.");
                    continue;
                }

                PegaKnowledgeEvidence objectEntryEvidence = PegaKnowledgeEvidence.From(
                    PegaNpcEvidenceOrigin.Perception,
                    observationTimeSeconds,
                    this,
                    pair.Key);
                PegaKnowledgeWriteResult positionWriteResult = _knowledgeState.ObserveFact(
                    PegaObjectEntryKnowledgeFacts.Position.For(pair.Key),
                    observedPosition,
                    objectEntryEvidence);

                PegaKnowledgeFact<bool> previousAvailability = _knowledgeState.GetFact(
                    PegaObjectEntryKnowledgeFacts.PickUpAvailable.For(pair.Key));
                bool hasPreviouslyPerceptionBackedOffer =
                    previousAvailability.TryGetValue(out bool wasAvailable) &&
                    wasAvailable &&
                    previousAvailability.HasOrigin(PegaNpcEvidenceOrigin.Perception);
                if (!radialDetection && !(visualDetection && hasPreviouslyPerceptionBackedOffer))
                {
                    continue;
                }

                ObserveObjectEntryPickUpOffer(
                    pair.Key,
                    pair.Value,
                    evidenceCollider,
                    radialDetection,
                    visualDetection,
                    objectEntryEvidence,
                    positionWriteResult);
            }

#if UNITY_EDITOR
            PerceptionSensorObservation focusObservation = focusHasVisionObservation
                ? focusVisionObservation
                : focusRadialObservation;
            PegaNpcPerceptionCandidateGizmoState[] candidateGizmoStates = BuildCandidateGizmoStates(
                acquisition,
                sensorResults,
                observationTimeSeconds,
                radialWasSampled,
                visualWasSampled,
                candidateIdentityRecognized,
                candidateIdentitySummaries,
                candidateIdentityDiagnostics);
            PegaNpcPerceptionAcquisitionGizmoState[] acquisitionGizmoStates =
                BuildAcquisitionGizmoStates(acquisition);
            _gizmoState = new PegaNpcPerceptionGizmoState(
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
        private PegaNpcPerceptionCandidateGizmoState[] BuildCandidateGizmoStates(
            PerceptionCandidateAcquisition acquisition,
            IReadOnlyList<PerceptionCandidateSensorResult> results,
            double observationTimeSeconds,
            bool radialWasSampled,
            bool visualWasSampled,
            IReadOnlyList<bool> identityRecognized,
            IReadOnlyList<string> identitySummaries,
            IReadOnlyList<string> identityDiagnostics)
        {
            var snapshots = new PegaNpcPerceptionCandidateGizmoState[acquisition.Candidates.Count];
            for (int candidateIndex = 0; candidateIndex < acquisition.Candidates.Count; candidateIndex++)
            {
                PerceptionCandidate candidate = acquisition.Candidates[candidateIndex];
                PegaNpcPerceptionSensorGizmoResult radial = CreateSensorGizmoResult(
                    results,
                    perceivedAreaSensorInstance,
                    candidateIndex,
                    candidate,
                    observationTimeSeconds,
                    radialWasSampled);
                PegaNpcPerceptionSensorGizmoResult visual = CreateSensorGizmoResult(
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

                snapshots[candidateIndex] = new PegaNpcPerceptionCandidateGizmoState(
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

        private static PegaNpcPerceptionSensorGizmoResult CreateSensorGizmoResult(
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
                        return new PegaNpcPerceptionSensorGizmoResult(
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
                    return new PegaNpcPerceptionSensorGizmoResult(
                        wasSampled,
                        true,
                        result.HasOutcome,
                        result.Status,
                        hasObservation,
                        observation,
                        result.Diagnostic);
                }
            }

            return new PegaNpcPerceptionSensorGizmoResult(
                wasSampled,
                false,
                false,
                PerceptionObservationStatus.Unspecified,
                false,
                default,
                wasSampled ? "The sampled sensor returned no result for this candidate." : string.Empty);
        }

        private PegaNpcPerceptionAcquisitionGizmoState[] BuildAcquisitionGizmoStates(
            PerceptionCandidateAcquisition acquisition)
        {
            var snapshots = new List<PegaNpcPerceptionAcquisitionGizmoState>();
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
                    snapshots.Add(new PegaNpcPerceptionAcquisitionGizmoState(
                        volume,
                        source.Status,
                        sensorLabel));
                    continue;
                }

                PegaNpcPerceptionAcquisitionGizmoState previous = snapshots[existingIndex];
                string labels = previous.SensorLabels.IndexOf(sensorLabel, StringComparison.Ordinal) >= 0
                    ? previous.SensorLabels
                    : previous.SensorLabels + " + " + sensorLabel;
                snapshots[existingIndex] = new PegaNpcPerceptionAcquisitionGizmoState(
                    previous.Volume,
                    WorseAcquisitionStatus(previous.Status, source.Status),
                    labels);
            }

            return snapshots.ToArray();
        }

        private static int FindSameGeometry(
            IReadOnlyList<PegaNpcPerceptionAcquisitionGizmoState> snapshots,
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
                        : $"{nameof(ActorOccurrenceQueryBinding)} is unavailable.",
                    null,
                    configurationFailure: true);
                return null;
            }

            if (!_actorDeclaration.TryCreateDescriptor(
                    nameof(PegaNpcPerceptionSource),
                    out ActorDescriptor descriptor,
                    out ActorSetIssue issue))
            {
                LogObserverOccurrenceUnavailable(
                    issue.ToString(),
                    null,
                    configurationFailure: true);
                return null;
            }

            ActorOccurrenceResolution resolution = _actorOccurrenceQuery.Resolve(
                ActorOccurrenceReference.ForActorId(descriptor.ActorId));
            if (!resolution.Succeeded || resolution.Occurrence.ActorRoot != gameObject)
            {
                string diagnostic = resolution.Succeeded
                    ? "The official Actor query resolved this ActorId to a different physical root."
                    : resolution.Diagnostic;
                LogObserverOccurrenceUnavailable(
                    diagnostic,
                    resolution.Status,
                    configurationFailure: resolution.Status != ActorOccurrenceResolutionStatus.Unavailable ||
                                          resolution.Succeeded);
                return null;
            }

            _lastObserverOccurrenceDiagnostic = string.Empty;
            _lastObserverOccurrenceResolutionStatus = null;
            _lastObserverOccurrenceWasWarning = false;
            _hasResolvedObserverOccurrence = true;
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

        private void LogObserverOccurrenceUnavailable(
            string diagnostic,
            ActorOccurrenceResolutionStatus? resolutionStatus,
            bool configurationFailure)
        {
            diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                ? "The Framework Actor occurrence query is not currently available."
                : diagnostic;
            bool expectedCompositionUnavailable =
                !configurationFailure &&
                resolutionStatus == ActorOccurrenceResolutionStatus.Unavailable &&
                !_hasResolvedObserverOccurrence;
            bool shouldWarn = !expectedCompositionUnavailable;
            if (string.Equals(_lastObserverOccurrenceDiagnostic, diagnostic, StringComparison.Ordinal) &&
                _lastObserverOccurrenceResolutionStatus == resolutionStatus &&
                _lastObserverOccurrenceWasWarning == shouldWarn)
            {
                return;
            }

            _lastObserverOccurrenceDiagnostic = diagnostic;
            _lastObserverOccurrenceResolutionStatus = resolutionStatus;
            _lastObserverOccurrenceWasWarning = shouldWarn;
            _lastObserverOccurrenceId = default;
            if (shouldWarn)
            {
                _log.Warning(
                    "NPC Perception could not resolve its own Framework Actor occurrence outside the expected unbound-composition state.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("resolutionStatus", resolutionStatus?.ToString() ?? "InvalidConfiguration"),
                    LogFields.Field("diagnostic", diagnostic));
            }
            else
            {
                _log.Debug(
                    "NPC Perception is waiting for its own committed Framework Actor occurrence; candidate acquisition will retry.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("resolutionStatus", resolutionStatus?.ToString() ?? "Unavailable"),
                    LogFields.Field("diagnostic", diagnostic));
            }
        }

        private PerceptionCandidateAcquisition BuildExternallyKnownCandidates(
            double observationTimeSeconds,
            out ActorOccurrence? playerOccurrence,
            out List<StaleActorContact> staleActorContacts,
            out ActorOccurrenceResolutionStatus? playerReferenceStatus)
        {
            var sources = new List<PerceptionCandidateSourceResult>();
            var candidates = new List<PerceptionCandidate>();
            var includedActorOccurrences = new Dictionary<ActorId, ActorOccurrenceId>();
            playerOccurrence = null;
            staleActorContacts = new List<StaleActorContact>();
            playerReferenceStatus = null;
            string targetDiagnostic = string.Empty;

            if (_actorReference != null)
            {
                bool hasCurrentReference = _actorReference.TryGetCurrent(
                    out ActorOccurrenceResolution playerResolution);
                playerReferenceStatus = playerResolution.Status;
                if (playerResolution.Succeeded)
                {
                    _hasResolvedExternalPlayerOccurrence = true;
                }

                if (hasCurrentReference &&
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

            LogExternalTargetAvailability(
                targetDiagnostic,
                playerOccurrence.HasValue,
                _actorReference != null,
                playerReferenceStatus);

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

        private void LogExternalTargetAvailability(
            string diagnostic,
            bool isAvailable,
            bool hasPlayerReference,
            ActorOccurrenceResolutionStatus? resolutionStatus)
        {
            bool unavailableAfterResolution =
                hasPlayerReference &&
                resolutionStatus == ActorOccurrenceResolutionStatus.Unavailable &&
                _hasResolvedExternalPlayerOccurrence;
            bool hasResolutionFailure =
                hasPlayerReference &&
                resolutionStatus.HasValue &&
                resolutionStatus.Value != ActorOccurrenceResolutionStatus.Unavailable &&
                !isAvailable;
            bool shouldWarn = !isAvailable && (unavailableAfterResolution || hasResolutionFailure);

            if (_hasLoggedExternalTargetState &&
                string.Equals(_lastExternalTargetDiagnostic, diagnostic, StringComparison.Ordinal) &&
                _lastExternalTargetResolutionStatus == resolutionStatus &&
                _lastExternalTargetHadReference == hasPlayerReference &&
                _lastExternalTargetWasAvailable == isAvailable &&
                _lastExternalTargetWasWarning == shouldWarn)
            {
                return;
            }

            bool wasUnavailable = _hasLoggedExternalTargetState && !_lastExternalTargetWasAvailable;
            _lastExternalTargetDiagnostic = diagnostic ?? string.Empty;
            _lastExternalTargetResolutionStatus = resolutionStatus;
            _lastExternalTargetHadReference = hasPlayerReference;
            _lastExternalTargetWasAvailable = isAvailable;
            _lastExternalTargetWasWarning = shouldWarn;
            _hasLoggedExternalTargetState = true;
            if (isAvailable)
            {
                _log.Info(
                    wasUnavailable
                        ? "NPC Player reference recovered; the official Player occurrence is again supplied as an external Perception candidate."
                        : "NPC Player reference is available as an external Perception candidate.",
                    LogFields.Field("actorRoot", gameObject.name));
            }
            else if (shouldWarn)
            {
                _log.Warning(
                    "NPC Perception has an invalid external Player reference or lost a previously resolved Player occurrence.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("resolutionStatus", resolutionStatus?.ToString() ?? "Unavailable"),
                    LogFields.Field("diagnostic", diagnostic));
            }
            else
            {
                _log.Debug(
                    hasPlayerReference
                        ? "NPC Perception has no current external Player candidate; the selected Slot is currently unavailable and no negative Player contact is inferred."
                        : "NPC Perception has no configured Player Slot reference; autonomous Actor and Object Entry sensing remains active.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("resolutionStatus", resolutionStatus?.ToString() ?? "NotConfigured"),
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
            PegaKnowledgeFact<bool> contact = _knowledgeState.GetCharacterVisualContact(actorId);
            if (contact.Status == PegaKnowledgeStatus.Unknown ||
                contact.Status == PegaKnowledgeStatus.Invalidated)
            {
                return;
            }

            PegaKnowledgeWriteResult result = _knowledgeState.InvalidateFact(
                PegaNpcKnowledgeFacts.CharacterVisualContact.For(actorId),
                PegaKnowledgeEvidence.From(
                    PegaNpcEvidenceOrigin.Perception,
                    observationTimeSeconds,
                    this,
                    staleOccurrenceId));
            if (result == PegaKnowledgeWriteResult.InvalidatedExplicitly)
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
                            nameof(PegaNpcPerceptionSource),
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
                diagnostic = "The radial or visual candidate has no nearest ObjectEntryDeclaration to recognize.";
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

        private void ObserveObjectEntryPickUpOffer(
            ObjectEntryId objectEntryId,
            GameObject occurrenceRoot,
            Collider evidenceCollider,
            bool radialDetected,
            bool visualDetected,
            PegaKnowledgeEvidence evidence,
            PegaKnowledgeWriteResult positionWriteResult)
        {
            PegaObjectEntryInteractionAggregator aggregator = occurrenceRoot != null
                ? occurrenceRoot.GetComponent<PegaObjectEntryInteractionAggregator>()
                : null;
            PegaObjectEntryPickUpAction pickUpAction = occurrenceRoot != null
                ? occurrenceRoot.GetComponent<PegaObjectEntryPickUpAction>()
                : null;
            PegaObjectEntryPhysicalItem physicalItem = occurrenceRoot != null
                ? occurrenceRoot.GetComponent<PegaObjectEntryPhysicalItem>()
                : null;
            PegaPickUpObjectEntryConfigurationSnapshot parameters = default;
            string configurationDiagnostic = string.Empty;
            bool hasConfiguration = pickUpAction != null && pickUpAction.TryGetConfiguration(
                out parameters,
                out configurationDiagnostic);
            if (!hasConfiguration)
            {
                configurationDiagnostic = pickUpAction == null
                    ? "No PEGA Pick Up action is composed on this Object Entry root."
                    : configurationDiagnostic;
            }

            bool aggregatorActive = aggregator != null && aggregator.isActiveAndEnabled &&
                                    occurrenceRoot != null && occurrenceRoot.activeInHierarchy;
            bool actionActive = pickUpAction != null && pickUpAction.isActiveAndEnabled &&
                                occurrenceRoot != null && occurrenceRoot.activeInHierarchy;
            bool offeredToNpc = aggregatorActive && actionActive &&
                                aggregator.IsOfferedTo(
                                    pickUpAction,
                                    PegaObjectInteractionParticipant.Npc);
            bool offeredToPlayer = aggregatorActive && actionActive &&
                                   aggregator.IsOfferedTo(
                                       pickUpAction,
                                       PegaObjectInteractionParticipant.Player);
            bool reservationOwnedByThisActor = physicalItem != null && physicalItem.IsReserved &&
                                                IsReservedByThisActor(physicalItem);

            string offerState;
            string diagnostic;
            if (aggregator == null)
            {
                offerState = "interaction-aggregator-missing";
                diagnostic = "The recognized Object Entry has no PEGA interaction aggregator.";
            }
            else if (!aggregatorActive)
            {
                offerState = "interaction-aggregator-inactive";
                diagnostic = "The recognized Object Entry interaction aggregator is inactive.";
            }
            else if (pickUpAction == null)
            {
                offerState = "pick-up-action-missing";
                diagnostic = "No PEGA Pick Up action is composed on this Object Entry root.";
            }
            else if (!actionActive)
            {
                offerState = "pick-up-action-inactive";
                diagnostic = "The PEGA Pick Up action component is inactive.";
            }
            else if (!offeredToNpc)
            {
                offerState = offeredToPlayer
                    ? "pick-up-offered-to-player-only"
                    : aggregator.OfferCount == 0
                        ? "no-authored-interaction-offers"
                        : "pick-up-not-offered-to-npc";
                diagnostic = offeredToPlayer
                    ? "The Object Entry offers Pick Up to Player, not to NPC."
                    : "The Object Entry does not offer Pick Up to NPC, or its authored offer composition is invalid.";
            }
            else if (!hasConfiguration)
            {
                offerState = "pick-up-configuration-invalid";
                diagnostic = configurationDiagnostic;
            }
            else if (physicalItem == null)
            {
                offerState = "physical-item-component-missing";
                diagnostic = "The recognized Object Entry has a Pick Up action but no physical-item component.";
            }
            else if (reservationOwnedByThisActor)
            {
                offerState = "pick-up-reserved-by-this-npc";
                diagnostic = "This NPC owns the active Pick Up reservation; its current offer facts are retained for Action continuity.";
            }
            else if (physicalItem.IsReserved)
            {
                offerState = "pick-up-temporarily-reserved";
                diagnostic = "The physical Object Entry is currently reserved by an interaction.";
            }
            else if (!physicalItem.IsAvailableOnGround)
            {
                offerState = "pick-up-no-longer-on-ground";
                diagnostic = "The physical Object Entry is no longer available on the ground.";
            }
            else
            {
                offerState = "pick-up-offered-to-npc";
                diagnostic = string.Empty;
            }

            bool canPublishOffer = offerState == "pick-up-offered-to-npc" ||
                                   offerState == "pick-up-reserved-by-this-npc";
            PegaKnowledgeFactKey<ObjectEntryId, bool> availableKey =
                PegaObjectEntryKnowledgeFacts.PickUpAvailable.For(objectEntryId);
            PegaKnowledgeFactKey<ObjectEntryId, PegaPickUpObjectEntryConfigurationSnapshot> parametersKey =
                PegaObjectEntryKnowledgeFacts.PickUpPlanningParameters.For(objectEntryId);
            PegaKnowledgeFact<bool> previousAvailability = _knowledgeState.GetFact(availableKey);
            PegaKnowledgeFact<PegaPickUpObjectEntryConfigurationSnapshot> previousParameters =
                _knowledgeState.GetFact(parametersKey);
            bool wasAlreadyAvailable = previousAvailability.TryGetValue(out bool wasAvailable) && wasAvailable;
            bool hasActionResultPositiveAvailability =
                previousAvailability.HasOrigin(PegaNpcEvidenceOrigin.ActionResult) &&
                previousAvailability.TryGetValue(out bool actionResultAvailability) &&
                actionResultAvailability;
            bool hadMatchingParameters = canPublishOffer &&
                previousParameters.TryGetValue(out PegaPickUpObjectEntryConfigurationSnapshot previousSnapshot) &&
                previousSnapshot.Equals(parameters);
            bool hasNonPerceptionAvailabilityEvidence =
                previousAvailability.HasOrigin(PegaNpcEvidenceOrigin.DesignerAuthoredInitial) ||
                hasActionResultPositiveAvailability;
            bool hasNonPerceptionParameterEvidence =
                previousParameters.HasOrigin(PegaNpcEvidenceOrigin.ActionResult) ||
                previousParameters.HasOrigin(PegaNpcEvidenceOrigin.DesignerAuthoredInitial);
            bool canRevokePerceptionOffer = wasAlreadyAvailable &&
                previousAvailability.HasOrigin(PegaNpcEvidenceOrigin.Perception) &&
                !hasNonPerceptionAvailabilityEvidence &&
                !hasNonPerceptionParameterEvidence;
            bool canWritePerceptionUnavailable = !hasNonPerceptionAvailabilityEvidence &&
                (!wasAlreadyAvailable || previousAvailability.HasOrigin(PegaNpcEvidenceOrigin.Perception) ||
                 previousAvailability.HasOrigin(PegaNpcEvidenceOrigin.ActionResult));
            bool shouldInvalidatePerceptionParameters =
                previousParameters.HasOrigin(PegaNpcEvidenceOrigin.Perception) &&
                !hasNonPerceptionParameterEvidence;

            string knowledgeUpdate;
            string knowledgeCoherenceDiagnostic = string.Empty;
            PegaKnowledgeWriteResult? availabilityWriteResult = null;
            PegaKnowledgeWriteResult? parametersWriteResult = null;
            PegaKnowledgeWriteResult? parametersInvalidationResult = null;
            bool coherentPerceptionOffer = false;
            bool knowledgeWriteStateVerified = false;
            if (canPublishOffer)
            {
                availabilityWriteResult = _knowledgeState.ObserveFact(availableKey, true, evidence);
                parametersWriteResult = _knowledgeState.ObserveFact(parametersKey, parameters, evidence);
                coherentPerceptionOffer = TryReadCoherentPerceptionPickUpOffer(
                    objectEntryId,
                    occurrenceRoot.transform.position,
                    parameters,
                    evidence.ObservationTimeSeconds,
                    out knowledgeCoherenceDiagnostic);
                knowledgeWriteStateVerified = coherentPerceptionOffer;
                knowledgeUpdate = coherentPerceptionOffer
                    ? wasAlreadyAvailable && hadMatchingParameters
                        ? "offer-refreshed"
                        : "offer-published"
                    : "offer-publication-not-committed";
            }
            else if (canWritePerceptionUnavailable)
            {
                availabilityWriteResult = _knowledgeState.ObserveFact(availableKey, false, evidence);
                if (shouldInvalidatePerceptionParameters)
                {
                    parametersInvalidationResult = _knowledgeState.InvalidateFact(parametersKey, evidence);
                }

                knowledgeWriteStateVerified = TryReadPerceptionUnavailablePickUpFact(
                    objectEntryId,
                    evidence.ObservationTimeSeconds,
                    shouldInvalidatePerceptionParameters,
                    out knowledgeCoherenceDiagnostic);
                knowledgeUpdate = knowledgeWriteStateVerified
                    ? canRevokePerceptionOffer
                        ? "offer-revoked"
                        : wasAlreadyAvailable
                            ? "availability-revoked-parameters-retained"
                            : "offer-not-published"
                    : "offer-negative-observation-not-committed";
            }
            else
            {
                knowledgeUpdate = wasAlreadyAvailable
                    ? "offer-not-published-existing-knowledge-retained"
                    : "offer-not-published";
                if (hasNonPerceptionAvailabilityEvidence || hasNonPerceptionParameterEvidence)
                {
                    knowledgeCoherenceDiagnostic =
                        "The current radial observation does not overwrite ActionResult or DesignerAuthoredInitial Pick Up facts.";
                }
            }

            LogObjectEntryPickUpOpportunity(
                evidenceCollider,
                objectEntryId,
                radialDetected,
                visualDetected,
                offerState,
                canPublishOffer,
                hasConfiguration,
                canPublishOffer ? parameters : default,
                knowledgeUpdate,
                diagnostic,
                positionWriteResult,
                availabilityWriteResult,
                parametersWriteResult,
                parametersInvalidationResult,
                coherentPerceptionOffer,
                knowledgeWriteStateVerified,
                knowledgeCoherenceDiagnostic);
        }

        private bool IsReservedByThisActor(PegaObjectEntryPhysicalItem physicalItem) =>
            physicalItem != null && physicalItem.IsReservedBy(gameObject);

        private bool TryReadCoherentPerceptionPickUpOffer(
            ObjectEntryId objectEntryId,
            Vector3 expectedPosition,
            PegaPickUpObjectEntryConfigurationSnapshot expectedParameters,
            double expectedObservationTimeSeconds,
            out string diagnostic)
        {
            PegaKnowledgeFact<Vector3> position = _knowledgeState.ReadFact(
                PegaObjectEntryKnowledgeFacts.Position.For(objectEntryId),
                Time.timeAsDouble);
            PegaKnowledgeFact<bool> availability = _knowledgeState.ReadFact(
                PegaObjectEntryKnowledgeFacts.PickUpAvailable.For(objectEntryId),
                Time.timeAsDouble);
            PegaKnowledgeFact<PegaPickUpObjectEntryConfigurationSnapshot> parameters = _knowledgeState.ReadFact(
                PegaObjectEntryKnowledgeFacts.PickUpPlanningParameters.For(objectEntryId),
                Time.timeAsDouble);

            bool isCoherent = position.Scope == PegaKnowledgeScope.Individual &&
                position.TryGetValue(out Vector3 knownPosition) &&
                IsFinite(knownPosition) && knownPosition.Equals(expectedPosition) &&
                position.HasOrigin(PegaNpcEvidenceOrigin.Perception) &&
                availability.Scope == PegaKnowledgeScope.Individual &&
                availability.TryGetValue(out bool isAvailable) && isAvailable &&
                availability.HasOrigin(PegaNpcEvidenceOrigin.Perception) &&
                parameters.Scope == PegaKnowledgeScope.Individual &&
                parameters.TryGetValue(out PegaPickUpObjectEntryConfigurationSnapshot knownParameters) &&
                knownParameters.IsValid && knownParameters.Equals(expectedParameters) &&
                parameters.HasOrigin(PegaNpcEvidenceOrigin.Perception) &&
                HasSameObservationTime(position, availability) &&
                HasSameObservationTime(position, parameters) &&
                IsObservationTime(position, expectedObservationTimeSeconds);
            diagnostic = isCoherent
                ? string.Empty
                : "Knowledge did not retain a current Individual Perception Position, true offer and matching planning parameters with one observation timestamp.";
            return isCoherent;
        }

        private bool TryReadPerceptionUnavailablePickUpFact(
            ObjectEntryId objectEntryId,
            double expectedObservationTimeSeconds,
            bool expectParameterInvalidation,
            out string diagnostic)
        {
            PegaKnowledgeFact<bool> availability = _knowledgeState.ReadFact(
                PegaObjectEntryKnowledgeFacts.PickUpAvailable.For(objectEntryId),
                Time.timeAsDouble);
            PegaKnowledgeFact<PegaPickUpObjectEntryConfigurationSnapshot> parameters = _knowledgeState.ReadFact(
                PegaObjectEntryKnowledgeFacts.PickUpPlanningParameters.For(objectEntryId),
                Time.timeAsDouble);
            bool isCoherentNegativeObservation = availability.Scope == PegaKnowledgeScope.Individual &&
                availability.TryGetValue(out bool isAvailable) && !isAvailable &&
                availability.HasOrigin(PegaNpcEvidenceOrigin.Perception) &&
                IsObservationTime(availability, expectedObservationTimeSeconds);
            if (expectParameterInvalidation)
            {
                isCoherentNegativeObservation &= parameters.Scope == PegaKnowledgeScope.Individual &&
                    parameters.Status == PegaKnowledgeStatus.Invalidated &&
                    (parameters.InvalidationCauses & PegaKnowledgeInvalidationCause.Explicit) != 0 &&
                    parameters.HasOrigin(PegaNpcEvidenceOrigin.Perception) &&
                    IsObservationTime(parameters, expectedObservationTimeSeconds);
            }

            diagnostic = isCoherentNegativeObservation
                ? string.Empty
                : "Knowledge did not retain the current Perception false offer and expected planning-parameter state.";
            return isCoherentNegativeObservation;
        }

        private static bool HasSameObservationTime<TValue>(
            PegaKnowledgeFact<Vector3> left,
            PegaKnowledgeFact<TValue> right) =>
            left.HasObservationTime && right.HasObservationTime &&
            Math.Abs(left.ObservationTimeSeconds - right.ObservationTimeSeconds) <= 0.000001d;

        private static bool IsObservationTime<TValue>(
            PegaKnowledgeFact<TValue> fact,
            double expectedObservationTimeSeconds) =>
            fact.HasObservationTime &&
            Math.Abs(fact.ObservationTimeSeconds - expectedObservationTimeSeconds) <= 0.000001d;

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

        private static bool HasCompleteRadialCoverage(
            PerceptionCandidateAcquisition acquisition,
            PerceptionSensorInstance sensorInstance)
        {
            if (!HasCompletePhysicalCoverage(acquisition, sensorInstance))
            {
                return false;
            }

            for (int index = 0; index < acquisition.Sources.Count; index++)
            {
                PerceptionCandidateSourceResult source = acquisition.Sources[index];
                if (source.Origin == PerceptionCandidateOrigin.External &&
                    source.Status != PerceptionAcquisitionStatus.Complete)
                {
                    return false;
                }
            }

            return true;
        }

        private void GetPreviouslyRecognizedCandidateIdentity(
            Transform candidateRoot,
            NearestFrameworkDeclarations declarations,
            ActorOccurrenceReference? externalActorReference,
            out bool actorIdentityResolved,
            out bool objectEntryIdentityResolved)
        {
            actorIdentityResolved = externalActorReference.HasValue &&
                                    IsPreviouslyRecognizedExternalActor(externalActorReference.Value);
            objectEntryIdentityResolved = false;

            if (candidateRoot == null ||
                !_resolvedCandidateRoots.TryGetValue(
                    candidateRoot,
                    out ResolvedCandidateIdentity identity))
            {
                return;
            }

            objectEntryIdentityResolved = identity.ObjectEntryId.IsValid &&
                declarations.HasValidObjectEntryDeclaration &&
                declarations.ObjectEntryId == identity.ObjectEntryId;
            if (identity.ActorId.IsValid &&
                declarations.HasValidActorDeclaration &&
                candidateRoot.GetComponent<ActorDeclaration>() is ActorDeclaration actorDeclaration &&
                actorDeclaration.TryCreateDescriptor(
                    nameof(PegaNpcPerceptionSource),
                    out ActorDescriptor descriptor,
                    out _) &&
                descriptor.ActorId == identity.ActorId &&
                _recognizedActorReferences.TryGetValue(
                    identity.ActorId,
                    out ActorOccurrenceReference recognizedReference) &&
                recognizedReference.IsExactOccurrence &&
                recognizedReference.ExpectedOccurrence == identity.ActorOccurrenceId &&
                TryResolveExactActorOccurrence(
                    recognizedReference,
                    out ActorOccurrence currentOccurrence,
                    out _,
                    out _) &&
                currentOccurrence.Id == identity.ActorOccurrenceId &&
                currentOccurrence.ActorRoot.transform == candidateRoot)
            {
                actorIdentityResolved |= IsPerceptionBackedCharacterPositionKnown(identity.ActorId);
            }

            if (!actorIdentityResolved && !objectEntryIdentityResolved)
            {
                _resolvedCandidateRoots.Remove(candidateRoot);
            }
        }

        private bool IsPreviouslyRecognizedExternalActor(ActorOccurrenceReference externalReference) =>
            _knowledgeState != null &&
            _recognizedActorReferences.TryGetValue(
                externalReference.ActorId,
                out ActorOccurrenceReference recognizedReference) &&
            recognizedReference.IsExactOccurrence &&
            recognizedReference.ExpectedOccurrence == externalReference.ExpectedOccurrence &&
            IsPerceptionBackedCharacterPositionKnown(externalReference.ActorId);

        private bool IsPerceptionBackedCharacterPositionKnown(ActorId actorId)
        {
            if (_knowledgeState == null || !actorId.IsValid)
            {
                return false;
            }

            PegaKnowledgeFact<Vector3> lastObservedPosition =
                _knowledgeState.GetLastObservedCharacterPosition(actorId);
            return lastObservedPosition.TryGetValue(out _) &&
                   lastObservedPosition.HasOrigin(PegaNpcEvidenceOrigin.Perception);
        }

        private void RememberResolvedCandidateRoot(
            Transform candidateRoot,
            ActorOccurrence actorOccurrence,
            ObjectEntryId objectEntryId)
        {
            if (candidateRoot == null)
            {
                return;
            }

            ActorId actorId = actorOccurrence.IsValid ? actorOccurrence.ActorId : default;
            ActorOccurrenceId actorOccurrenceId = actorOccurrence.IsValid ? actorOccurrence.Id : default;
            if (_resolvedCandidateRoots.TryGetValue(
                    candidateRoot,
                    out ResolvedCandidateIdentity previous))
            {
                if (!actorOccurrence.IsValid)
                {
                    actorId = previous.ActorId;
                    actorOccurrenceId = previous.ActorOccurrenceId;
                }

                if (!objectEntryId.IsValid)
                {
                    objectEntryId = previous.ObjectEntryId;
                }
            }

            _resolvedCandidateRoots[candidateRoot] = new ResolvedCandidateIdentity(
                actorId,
                actorOccurrenceId,
                objectEntryId);
        }

        private long RegisterUnresolvedRadialOpportunity(
            Collider physicalCollider,
            ActorOccurrenceReference? externalActorReference)
        {
            if (externalActorReference.HasValue)
            {
                ActorOccurrenceId occurrenceId = externalActorReference.Value.ExpectedOccurrence;
                _unresolvedOccurrencesThisSample.Add(occurrenceId);
                if (!_radialOpportunitySequencesByOccurrence.TryGetValue(
                        occurrenceId,
                        out long sequence))
                {
                    sequence = AllocateRadialOpportunitySequence();
                    _radialOpportunitySequencesByOccurrence.Add(occurrenceId, sequence);
                    _radialOpportunityEpisodesBySequence.Add(
                        sequence,
                        CreateRadialOpportunityEpisode(sequence));
                }

                return sequence;
            }

            if (physicalCollider != null)
            {
                _unresolvedCollidersThisSample.Add(physicalCollider);
                if (!_radialOpportunitySequencesByCollider.TryGetValue(
                        physicalCollider,
                        out long sequence))
                {
                    sequence = AllocateRadialOpportunitySequence();
                    _radialOpportunitySequencesByCollider.Add(physicalCollider, sequence);
                    _radialOpportunityEpisodesBySequence.Add(
                        sequence,
                        CreateRadialOpportunityEpisode(sequence));
                }

                return sequence;
            }

            _hasUnboundUnresolvedCandidateThisSample = true;
            if (_unboundRadialOpportunitySequence <= 0)
            {
                _unboundRadialOpportunitySequence = AllocateRadialOpportunitySequence();
                _radialOpportunityEpisodesBySequence.Add(
                    _unboundRadialOpportunitySequence,
                    CreateRadialOpportunityEpisode(_unboundRadialOpportunitySequence));
            }

            return _unboundRadialOpportunitySequence;
        }

        private bool CloseUnresolvedRadialOpportunity(
            Collider physicalCollider,
            ActorOccurrenceReference? externalActorReference)
        {
            bool removed = false;
            if (externalActorReference.HasValue)
            {
                if (_radialOpportunitySequencesByOccurrence.TryGetValue(
                        externalActorReference.Value.ExpectedOccurrence,
                        out long sequence))
                {
                    _radialOpportunitySequencesByOccurrence.Remove(
                        externalActorReference.Value.ExpectedOccurrence);
                    _radialOpportunityEpisodesBySequence.Remove(sequence);
                    removed = true;
                }
            }

            if (physicalCollider != null)
            {
                if (_radialOpportunitySequencesByCollider.TryGetValue(physicalCollider, out long sequence))
                {
                    _radialOpportunitySequencesByCollider.Remove(physicalCollider);
                    _radialOpportunityEpisodesBySequence.Remove(sequence);
                    removed = true;
                }
            }

            return removed;
        }

        private PegaNpcPerceivedAreaOpportunity CreateRadialOpportunityEpisode(long sequence) =>
            new PegaNpcPerceivedAreaOpportunity(
                sequence,
                transform.position,
                _areaPresenceSensor != null ? _areaPresenceSensor.RadiusMeters : 0f);

        private PegaNpcPerceivedAreaPresence CreateUnresolvedRadialPresenceValue()
        {
            List<PegaNpcPerceivedAreaOpportunity> opportunities =
                new List<PegaNpcPerceivedAreaOpportunity>(_radialOpportunityEpisodesBySequence.Values);
            opportunities.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            PegaNpcPerceivedAreaOpportunity primary = opportunities[opportunities.Count - 1];
            return new PegaNpcPerceivedAreaPresence(
                true,
                primary.ObserverPosition,
                primary.PerceivedAreaRadiusMeters,
                primary.Sequence,
                opportunities.ToArray());
        }

        private long AllocateRadialOpportunitySequence()
        {
            if (_nextRadialOpportunitySequence == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The Perception source exhausted its radial opportunity sequence range.");
            }

            return ++_nextRadialOpportunitySequence;
        }

        private void PruneUnresolvedRadialOpportunities()
        {
            List<Collider> staleColliders = null;
            foreach (KeyValuePair<Collider, long> pair in _radialOpportunitySequencesByCollider)
            {
                if (pair.Key == null || !_unresolvedCollidersThisSample.Contains(pair.Key))
                {
                    staleColliders ??= new List<Collider>();
                    staleColliders.Add(pair.Key);
                }
            }

            if (staleColliders != null)
            {
                for (int index = 0; index < staleColliders.Count; index++)
                {
                    if (_radialOpportunitySequencesByCollider.TryGetValue(
                            staleColliders[index],
                            out long sequence))
                    {
                        _radialOpportunitySequencesByCollider.Remove(staleColliders[index]);
                        _radialOpportunityEpisodesBySequence.Remove(sequence);
                    }
                }
            }

            List<ActorOccurrenceId> staleOccurrences = null;
            foreach (KeyValuePair<ActorOccurrenceId, long> pair in _radialOpportunitySequencesByOccurrence)
            {
                if (!_unresolvedOccurrencesThisSample.Contains(pair.Key))
                {
                    staleOccurrences ??= new List<ActorOccurrenceId>();
                    staleOccurrences.Add(pair.Key);
                }
            }

            if (staleOccurrences != null)
            {
                for (int index = 0; index < staleOccurrences.Count; index++)
                {
                    if (_radialOpportunitySequencesByOccurrence.TryGetValue(
                            staleOccurrences[index],
                            out long sequence))
                    {
                        _radialOpportunitySequencesByOccurrence.Remove(staleOccurrences[index]);
                        _radialOpportunityEpisodesBySequence.Remove(sequence);
                    }
                }
            }

            if (!_hasUnboundUnresolvedCandidateThisSample)
            {
                if (_unboundRadialOpportunitySequence > 0)
                {
                    _radialOpportunityEpisodesBySequence.Remove(_unboundRadialOpportunitySequence);
                }
                _unboundRadialOpportunitySequence = 0;
            }
        }

        private void PruneResolvedCandidateRoots()
        {
            if (_resolvedCandidateRoots.Count == 0)
            {
                return;
            }

            List<Transform> staleRoots = null;
            foreach (KeyValuePair<Transform, ResolvedCandidateIdentity> pair in _resolvedCandidateRoots)
            {
                if (pair.Key == null)
                {
                    staleRoots ??= new List<Transform>();
                    staleRoots.Add(pair.Key);
                }
            }

            if (staleRoots == null)
            {
                return;
            }

            for (int index = 0; index < staleRoots.Count; index++)
            {
                _resolvedCandidateRoots.Remove(staleRoots[index]);
            }
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

        private void LogAcquisitionTransition(
            PerceptionCandidateAcquisition acquisition,
            bool observerOccurrenceAvailable,
            bool hasPlayerReference,
            ActorOccurrenceResolutionStatus? playerReferenceStatus)
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

            bool hasUnexpectedFailure = HasUnexpectedAcquisitionFailure(
                acquisition,
                observerOccurrenceAvailable,
                hasPlayerReference,
                playerReferenceStatus);
            bool diagnosticChanged = !string.Equals(
                _lastAcquisitionDiagnostic,
                diagnostic,
                StringComparison.Ordinal);
            bool stateChanged = !_hasLoggedAcquisitionState ||
                                _lastAcquisitionStatus != acquisition.Status ||
                                _lastAcquisitionHadUnexpectedFailure != hasUnexpectedFailure ||
                                diagnosticChanged &&
                                (acquisition.Status != PerceptionAcquisitionStatus.Complete ||
                                 hasUnexpectedFailure);
            bool candidateCountChanged = _lastPhysicalCandidateCount != physicalCount ||
                                         _lastExternalCandidateCount != externalCount;
            if (!stateChanged && !candidateCountChanged && !diagnosticChanged)
            {
                return;
            }

            bool recovered = _hasLoggedAcquisitionState &&
                             ((_lastAcquisitionStatus == PerceptionAcquisitionStatus.Unavailable &&
                               acquisition.Status != PerceptionAcquisitionStatus.Unavailable) ||
                              (_lastAcquisitionStatus == PerceptionAcquisitionStatus.Partial &&
                               acquisition.Status == PerceptionAcquisitionStatus.Complete) ||
                              (_lastAcquisitionHadUnexpectedFailure && !hasUnexpectedFailure));
            _hasLoggedAcquisitionState = true;
            _lastAcquisitionStatus = acquisition.Status;
            _lastAcquisitionHadUnexpectedFailure = hasUnexpectedFailure;
            _lastPhysicalCandidateCount = physicalCount;
            _lastExternalCandidateCount = externalCount;
            _lastAcquisitionDiagnostic = diagnostic;

            if (!stateChanged)
            {
                _log.Trace(
                    "NPC Perception candidate acquisition details changed within the same state.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("acquisitionStatus", acquisition.Status),
                    LogFields.Field("physicalCandidateCount", physicalCount),
                    LogFields.Field("externalCandidateCount", externalCount),
                    LogFields.Field("sourceCount", acquisition.Sources.Count),
                    LogFields.Field("diagnostic", diagnostic));
                return;
            }

            if (hasUnexpectedFailure)
            {
                _log.Warning(
                    "NPC Perception candidate acquisition has incomplete coverage from an anomalous required or configured source.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("acquisitionStatus", acquisition.Status),
                    LogFields.Field("physicalCandidateCount", physicalCount),
                    LogFields.Field("externalCandidateCount", externalCount),
                    LogFields.Field("sourceCount", acquisition.Sources.Count),
                    LogFields.Field("diagnostic", diagnostic));
            }
            else if (recovered)
            {
                _log.Info(
                    $"NPC Perception candidate acquisition recovered to {acquisition.Status} coverage.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("acquisitionStatus", acquisition.Status),
                    LogFields.Field("physicalCandidateCount", physicalCount),
                    LogFields.Field("externalCandidateCount", externalCount),
                    LogFields.Field("sourceCount", acquisition.Sources.Count),
                    LogFields.Field("diagnostic", diagnostic));
            }
            else if (acquisition.Status == PerceptionAcquisitionStatus.Unavailable ||
                     acquisition.Status == PerceptionAcquisitionStatus.Partial)
            {
                _log.Debug(
                    "NPC Perception candidate coverage is incomplete while Framework occurrences are being composed or optional Player coverage is absent.",
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

        private bool HasUnexpectedAcquisitionFailure(
            PerceptionCandidateAcquisition acquisition,
            bool observerOccurrenceAvailable,
            bool hasPlayerReference,
            ActorOccurrenceResolutionStatus? playerReferenceStatus)
        {
            for (int index = 0; index < acquisition.Sources.Count; index++)
            {
                PerceptionCandidateSourceResult source = acquisition.Sources[index];
                if (source.Status == PerceptionAcquisitionStatus.Complete)
                {
                    continue;
                }

                if (source.Status == PerceptionAcquisitionStatus.Partial)
                {
                    return true;
                }

                if (source.Status != PerceptionAcquisitionStatus.Unavailable)
                {
                    return true;
                }

                if ((source.Origin & PerceptionCandidateOrigin.Physical) != 0)
                {
                    if (!observerOccurrenceAvailable && !_hasResolvedObserverOccurrence)
                    {
                        continue;
                    }

                    return true;
                }

                if ((source.Origin & PerceptionCandidateOrigin.External) != 0 &&
                    hasPlayerReference &&
                    ReferenceEquals(source.SourceReference, _actorReference))
                {
                    if (playerReferenceStatus == ActorOccurrenceResolutionStatus.Unavailable &&
                        !_hasResolvedExternalPlayerOccurrence)
                    {
                        continue;
                    }

                    return true;
                }

                if (!observerOccurrenceAvailable && !_hasResolvedObserverOccurrence)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void LogMissingPhysicalColliderDetections(
            int radialOnlyCount,
            int visualOnlyCount,
            int combinedCount)
        {
            if (radialOnlyCount == 0 && visualOnlyCount == 0 && combinedCount == 0)
            {
                _lastMissingPhysicalColliderDetectionState = string.Empty;
                return;
            }

            string state = string.Concat(radialOnlyCount, "|", visualOnlyCount, "|", combinedCount);
            if (string.Equals(_lastMissingPhysicalColliderDetectionState, state, StringComparison.Ordinal))
            {
                return;
            }

            _lastMissingPhysicalColliderDetectionState = state;
            _log.Warning(
                "NPC Perception detected candidates without physical Collider evidence; Object Entry recognition and offer publication were unavailable.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("radialOnlyCandidateCount", radialOnlyCount),
                LogFields.Field("visualOnlyCandidateCount", visualOnlyCount),
                LogFields.Field("radialAndVisualCandidateCount", combinedCount),
                LogFields.Field("objectEntryRecognitionAvailable", false),
                LogFields.Field("diagnostic", "Official Object Entry recognition requires physical Collider evidence."));
        }

        private void LogCandidateRecognition(
            Collider candidate,
            string state,
            string identitySummary,
            string diagnostic,
            bool radialDetected,
            bool visualDetected,
            bool expectedAnonymousRadialPresence = false)
        {
            if (candidate == null)
            {
                return;
            }

            string stateKey = string.Concat(
                state,
                "|radial=", radialDetected,
                "|visual=", visualDetected);
            if (_candidateRecognitionLogStates.TryGetValue(candidate, out string previousState) &&
                string.Equals(previousState, stateKey, StringComparison.Ordinal))
            {
                return;
            }

            _candidateRecognitionLogStates[candidate] = stateKey;
            string detectionChannels = DescribeDetectionChannels(radialDetected, visualDetected);
            bool recognized = state != null &&
                              (state.IndexOf("actor:resolved", StringComparison.Ordinal) >= 0 ||
                               state.IndexOf("object-entry:resolved", StringComparison.Ordinal) >= 0);
            if (recognized)
            {
                _log.Info(
                    "NPC Perception evidence recognized a declared entity through an official Framework query.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("candidateColliderEntityId", candidate.GetEntityId().ToString()),
                    LogFields.Field("detectionChannels", detectionChannels),
                    LogFields.Field("recognitionState", state),
                    LogFields.Field("officialIdentity", identitySummary),
                    LogFields.Field("diagnostic", diagnostic));
            }
            else if (expectedAnonymousRadialPresence)
            {
                _log.Debug(
                    "NPC Perception observed anonymous radial presence without publishing an entity identity.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("candidateColliderEntityId", candidate.GetEntityId().ToString()),
                    LogFields.Field("detectionChannels", detectionChannels),
                    LogFields.Field("recognitionState", state),
                    LogFields.Field("diagnostic", diagnostic));
            }
            else
            {
                _log.Warning(
                    "NPC Perception evidence has no unique official identity and occurrence suitable for Knowledge publication.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("candidateColliderEntityId", candidate.GetEntityId().ToString()),
                    LogFields.Field("detectionChannels", detectionChannels),
                    LogFields.Field("recognitionState", state),
                    LogFields.Field("diagnostic", diagnostic));
            }
        }

        private void LogObjectEntryPickUpOpportunity(
            Collider evidenceCollider,
            ObjectEntryId objectEntryId,
            bool radialDetected,
            bool visualDetected,
            string offerState,
            bool isAuthorizedOffer,
            bool configurationValid,
            PegaPickUpObjectEntryConfigurationSnapshot parameters,
            string knowledgeUpdate,
            string diagnostic,
            PegaKnowledgeWriteResult? positionWriteResult = null,
            PegaKnowledgeWriteResult? availabilityWriteResult = null,
            PegaKnowledgeWriteResult? parametersWriteResult = null,
            PegaKnowledgeWriteResult? parametersInvalidationResult = null,
            bool coherentPerceptionOffer = false,
            bool knowledgeWriteStateVerified = false,
            string knowledgeCoherenceDiagnostic = null)
        {
            string detectionChannels = DescribeDetectionChannels(radialDetected, visualDetected);
            string stateKey = string.Concat(
                objectEntryId.StableText,
                "|", detectionChannels,
                "|", offerState,
                "|", isAuthorizedOffer,
                "|", configurationValid,
                "|", configurationValid ? parameters.InteractionRangeMeters : -1f,
                "|", configurationValid ? parameters.ExecutionDurationSeconds : -1f,
                "|", knowledgeUpdate,
                "|", coherentPerceptionOffer,
                "|", knowledgeWriteStateVerified,
                "|", knowledgeCoherenceDiagnostic,
                "|", diagnostic);
            if (evidenceCollider != null &&
                _objectEntryOpportunityLogStates.TryGetValue(evidenceCollider, out string previousState) &&
                string.Equals(previousState, stateKey, StringComparison.Ordinal))
            {
                return;
            }

            if (evidenceCollider != null)
            {
                _objectEntryOpportunityLogStates[evidenceCollider] = stateKey;
            }

            _log.Debug(
                "NPC Perception assessed whether an Object Entry Pick Up opportunity could be published to Individual Knowledge.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("candidateColliderEntityId", evidenceCollider != null
                    ? evidenceCollider.GetEntityId().ToString()
                    : string.Empty),
                LogFields.Field("objectEntryId", objectEntryId.StableText),
                LogFields.Field("detectionChannels", detectionChannels),
                LogFields.Field("interactionOfferState", offerState),
                LogFields.Field("pickUpOfferAuthorizedForNpc", isAuthorizedOffer),
                LogFields.Field("configurationValid", configurationValid),
                LogFields.Field("runtimeEligibilityEvaluated", false),
                LogFields.Field("knowledgeUpdate", knowledgeUpdate),
                LogFields.Field("knowledgeOfferCoherent", coherentPerceptionOffer),
                LogFields.Field("knowledgeWriteStateVerified", knowledgeWriteStateVerified),
                LogFields.Field("positionWriteResult", DescribeKnowledgeWriteResult(positionWriteResult)),
                LogFields.Field("pickUpAvailabilityWriteResult", DescribeKnowledgeWriteResult(availabilityWriteResult)),
                LogFields.Field("planningParametersWriteResult", DescribeKnowledgeWriteResult(parametersWriteResult)),
                LogFields.Field("planningParametersInvalidationResult", DescribeKnowledgeWriteResult(parametersInvalidationResult)),
                LogFields.Field("interactionRangeMeters", configurationValid
                    ? parameters.InteractionRangeMeters
                    : -1f),
                LogFields.Field("executionDurationSeconds", configurationValid
                    ? parameters.ExecutionDurationSeconds
                    : -1f),
                LogFields.Field("knowledgeCoherenceDiagnostic", knowledgeCoherenceDiagnostic ?? string.Empty),
                LogFields.Field("diagnostic", diagnostic ?? string.Empty));
        }

        private static string DescribeKnowledgeWriteResult(PegaKnowledgeWriteResult? result) =>
            result.HasValue ? result.Value.ToString() : "not-attempted";

        private static string DescribeDetectionChannels(bool radialDetected, bool visualDetected)
        {
            if (radialDetected && visualDetected)
            {
                return "radial+visual";
            }

            if (radialDetected)
            {
                return "radial";
            }

            return visualDetected ? "visual" : "none";
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

        private void PruneObjectEntryOpportunityLogStates()
        {
            if (_objectEntryOpportunityLogStates.Count == 0)
            {
                return;
            }

            List<Collider> staleCandidates = null;
            foreach (KeyValuePair<Collider, string> pair in _objectEntryOpportunityLogStates)
            {
                if (pair.Key == null || !_candidateCollidersThisSample.Contains(pair.Key))
                {
                    staleCandidates ??= new List<Collider>();
                    staleCandidates.Add(pair.Key);
                }
            }

            if (staleCandidates == null)
            {
                return;
            }

            for (int index = 0; index < staleCandidates.Count; index++)
            {
                _objectEntryOpportunityLogStates.Remove(staleCandidates[index]);
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

            PegaKnowledgeFact<bool> previousContact = _knowledgeState.GetCharacterVisualContact(occurrence.ActorId);
            bool hadPreviousContact = previousContact.TryGetValue(out bool previousVisualContact);
            bool isReacquisition = hasVisualContact &&
                                   (hadPreviousContact && !previousVisualContact || forceCurrentEvidence);
            bool isContactLoss = !hasVisualContact && hadPreviousContact && previousVisualContact;

            if (hasVisualContact)
            {
                PegaKnowledgeFact<Vector3> previousPosition =
                    _knowledgeState.GetLastObservedCharacterPosition(occurrence.ActorId);
                if (isReacquisition ||
                    !previousPosition.TryGetValue(out Vector3 storedPosition) ||
                    !storedPosition.Equals(observedRootPosition))
                {
                    _knowledgeState.ObserveCharacterPosition(
                        occurrence.ActorId,
                        observedRootPosition,
                        PegaNpcEvidenceOrigin.Perception,
                        observationTimeSeconds);
                }

                if (forceCurrentEvidence)
                {
                    _knowledgeState.ObserveCharacterVisualContact(
                        occurrence.ActorId,
                        true,
                        PegaNpcEvidenceOrigin.Perception,
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
            PegaKnowledgeFact<PegaNpcPerceivedAreaPresence> previousArea,
            bool isPresent,
            double observationTimeSeconds)
        {
            bool hadPrevious = previousArea.TryGetValue(out PegaNpcPerceivedAreaPresence previous);
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

        private void LogUnresolvedRadialPresenceTransition(
            PegaKnowledgeFact<PegaNpcPerceivedAreaPresence> previousPresence,
            PegaNpcPerceivedAreaPresence currentPresence,
            double observationTimeSeconds,
            int unresolvedCandidateCount,
            int resolvedCandidateCount)
        {
            bool hadPrevious = previousPresence.TryGetValue(out PegaNpcPerceivedAreaPresence previous);
            if ((hadPrevious && previous.IsEquivalentTo(currentPresence)) ||
                (!hadPrevious && !currentPresence.IsPresent))
            {
                return;
            }

            _log.Debug(
                "NPC radial perception updated unresolved-presence Knowledge after checking identity evidence.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("unresolvedRadialPresence", currentPresence.IsPresent),
                LogFields.Field("opportunitySequence", currentPresence.OpportunitySequence),
                LogFields.Field("previousOpportunitySequence", hadPrevious ? previous.OpportunitySequence : 0),
                LogFields.Field("activeOpportunityCount", currentPresence.ActiveOpportunityCount),
                LogFields.Field("activeOpportunitySequenceIds", DescribeRadialOpportunitySequences(currentPresence)),
                LogFields.Field("unresolvedRadialCandidateCount", unresolvedCandidateCount),
                LogFields.Field("resolvedRadialCandidateCount", resolvedCandidateCount),
                LogFields.Field("observerPosition", currentPresence.ObserverPosition),
                LogFields.Field("perceivedAreaRadiusMeters", currentPresence.PerceivedAreaRadiusMeters),
                LogFields.Field("sensorInstance", perceivedAreaSensorInstance != null
                    ? perceivedAreaSensorInstance.name
                    : string.Empty),
                LogFields.Field("observationTimeSeconds", observationTimeSeconds));
        }

        private static string DescribeRadialOpportunitySequences(PegaNpcPerceivedAreaPresence presence)
        {
            if (presence.ActiveOpportunityCount == 0)
            {
                return string.Empty;
            }

            string[] sequences = new string[presence.ActiveOpportunityCount];
            for (int index = 0; index < sequences.Length; index++)
            {
                sequences[index] = presence.GetActiveOpportunityAt(index).Sequence.ToString();
            }

            return string.Join(",", sequences);
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
            PegaKnowledgeFact<PegaNpcPerceivedAreaPresence> presence =
                _knowledgeState.GetPerceivedAreaPresence();
            if (presence.TryGetValue(out PegaNpcPerceivedAreaPresence previousPresence) &&
                previousPresence.IsPresent == isPresent)
            {
                return;
            }

            if (presence.Status == PegaKnowledgeStatus.Unknown && !isPresent)
            {
                return;
            }

            _knowledgeState.ObservePerceivedAreaPresence(
                new PegaNpcPerceivedAreaPresence(
                    isPresent,
                    isPresent ? transform.position : Vector3.zero,
                    _areaPresenceSensor.RadiusMeters),
                PegaNpcEvidenceOrigin.Perception,
                observationTimeSeconds);
        }

        private void PublishUnresolvedRadialPresenceIfChanged(
            PegaNpcPerceivedAreaPresence unresolvedPresence,
            double observationTimeSeconds)
        {
            PegaKnowledgeFact<PegaNpcPerceivedAreaPresence> presence =
                _knowledgeState.GetUnresolvedRadialPresence();
            if (presence.TryGetValue(out PegaNpcPerceivedAreaPresence previousPresence) &&
                previousPresence.IsEquivalentTo(unresolvedPresence))
            {
                return;
            }

            if (presence.Status == PegaKnowledgeStatus.Unknown && !unresolvedPresence.IsPresent)
            {
                return;
            }

            _knowledgeState.ObserveUnresolvedRadialPresence(
                unresolvedPresence,
                PegaNpcEvidenceOrigin.Perception,
                observationTimeSeconds);
        }

        private void PublishTargetContactIfChanged(
            ActorId subjectActorId,
            bool hasVisualContact,
            double observationTimeSeconds)
        {
            PegaKnowledgeFact<bool> contact = _knowledgeState.GetCharacterVisualContact(subjectActorId);
            if (contact.TryGetValue(out bool previousContact) && previousContact == hasVisualContact)
            {
                return;
            }

            if (contact.Status == PegaKnowledgeStatus.Unknown && !hasVisualContact)
            {
                return;
            }

            _knowledgeState.ObserveCharacterVisualContact(
                subjectActorId,
                hasVisualContact,
                PegaNpcEvidenceOrigin.Perception,
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

            PegaKnowledgeFactKey<ActorId, bool> key =
                PegaNpcKnowledgeFacts.IsPlayer.For(occurrence.ActorId);
            PegaKnowledgeFact<bool> playerFact = _knowledgeState.GetFact(key);
            if (PegaNpcKnowledgeFacts.HasPerceptionBackedPlayerFact(playerFact))
            {
                return;
            }

            _knowledgeState.ObserveFact(
                key,
                true,
                PegaKnowledgeEvidence.From(
                    PegaNpcEvidenceOrigin.Perception,
                    observationTimeSeconds,
                    this,
                    occurrence.PlayerSlotId));
        }

        private void PublishPlanReevaluationRequestAfterReacquisition(
            ActorId subjectActorId,
            double observationTimeSeconds)
        {
            PegaKnowledgeFact<bool> contact =
                _knowledgeState.GetCharacterVisualContact(subjectActorId);
            PegaKnowledgeFact<Vector3> position =
                _knowledgeState.GetLastObservedCharacterPosition(subjectActorId);

            bool hasConsolidatedEvidence =
                contact.TryGetValue(out bool hasVisualContact) &&
                hasVisualContact &&
                HasCurrentPerceptionEvidence(contact.HasOrigin(PegaNpcEvidenceOrigin.Perception), contact.HasObservationTime,
                    contact.ObservationTimeSeconds, observationTimeSeconds) &&
                position.TryGetValue(out _) &&
                HasCurrentPerceptionEvidence(position.HasOrigin(PegaNpcEvidenceOrigin.Perception), position.HasObservationTime,
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
            var request = new PegaNpcPlanReevaluationRequestedEvent(
                PegaNpcPlanReevaluationTrigger.TargetReacquired,
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
            PegaKnowledgeFact<bool> contact = _knowledgeState.GetCharacterVisualContact(subjectActorId);
            return contact.TryGetValue(out bool actualContact) &&
                   actualContact == expectedContact &&
                   contact.HasOrigin(PegaNpcEvidenceOrigin.Perception) &&
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
            var request = new PegaNpcPlanReevaluationRequestedEvent(
                PegaNpcPlanReevaluationTrigger.TargetContactLostBeyondWindow,
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
            _planReevaluationEvents ??= new EventBus<PegaNpcPlanReevaluationRequestedEvent>();
        }

        private void LogPlanReevaluationSubscriberFailure(
            Action<PegaNpcPlanReevaluationRequestedEvent> handler,
            PegaNpcPlanReevaluationRequestedEvent request,
            Exception exception)
        {
            if (_log == null)
            {
                _log = PegaLogging.CreateScopedLogger<PegaNpcPerceptionSource>(
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
            _gizmoState = new PegaNpcPerceptionGizmoState(
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
                Array.Empty<PegaNpcPerceptionCandidateGizmoState>(),
                Array.Empty<PegaNpcPerceptionAcquisitionGizmoState>());
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

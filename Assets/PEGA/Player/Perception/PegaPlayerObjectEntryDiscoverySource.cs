using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using ImmersiveGames.Perception;
using PEGA.Diagnostics;
using PEGA.Interactions.Runtime;
using UnityEngine;
namespace PEGA.Player.Perception
{
    /// <summary>Descobre ocorrências físicas de Object Entry próximas ao Player.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PerceptionSensorSet))]
    [AddComponentMenu("PEGA/Perception/Player/Object Entry Discovery Source")]
    public sealed class PegaPlayerObjectEntryDiscoverySource : MonoBehaviour, IPegaObjectInteractionOccurrenceAuthority
    {
        private const string LogCategory = "PEGA.Gameplay.PlayerObjectDiscovery";

        [Header("Discovery Sensor Instance")]
        [SerializeField]
        [Tooltip("Instância composta no Actor cujo asset configura a descoberta espacial de Object Entries.")]
        private PerceptionSensorInstance candidateDiscoverySensorInstance;

        [Header("Scene View")]
        [SerializeField]
        [Tooltip("Off oculta a descoberta; Resumido mostra o volume, contagem e marcadores; Detalhado mostra estados, identidades e diagnósticos dos candidatos.")]
        private PegaDiagnosticVisualizationLevel discoveryVisualizationLevel =
            PegaDiagnosticVisualizationLevel.Summary;

        [SerializeField]
        [Tooltip("Desenha os Gizmos mesmo quando este Player não está selecionado.")]
        private bool drawGizmosWhenUnselected;

        [NonSerialized]
        private CharacterController _characterController;

        [NonSerialized]
        private PerceptionSensorSet _perceptionSensorSet;

        [NonSerialized]
        private PerceptionSensorInstance _configuredDiscoverySensorInstance;

        [NonSerialized]
        private PerceptionSensorDefinition _configuredDiscoverySensorDefinition;

        [NonSerialized]
        private readonly HashSet<OccurrenceKey> _seenOccurrences = new HashSet<OccurrenceKey>();

        [NonSerialized]
        private readonly HashSet<Collider> _seenPhysicalColliders = new HashSet<Collider>();

        [NonSerialized]
        private readonly List<PegaPlayerObjectEntryCandidate> _scanCandidates =
            new List<PegaPlayerObjectEntryCandidate>();

        [NonSerialized]
        private readonly Dictionary<RecognitionIssueKey, RecognitionIssueSummary> _scanRecognitionIssues =
            new Dictionary<RecognitionIssueKey, RecognitionIssueSummary>();

        [NonSerialized]
        private readonly HashSet<RecognitionIssueKey> _activeRecognitionIssues =
            new HashSet<RecognitionIssueKey>();

        [NonSerialized]
        private IReadOnlyList<PegaPlayerObjectEntryCandidate> _candidateSnapshot =
            Array.Empty<PegaPlayerObjectEntryCandidate>();

        [NonSerialized]
        private ObjectEntryOccurrenceQueryBinding _recognitionQueryBinding;

        [NonSerialized]
        private ActorOccurrenceReferenceBinding _playerActorReference;

        [NonSerialized]
        private ActorOccurrenceId _boundPlayerOccurrenceId;

        [NonSerialized]
        private ActorOccurrence _playerOccurrence;

        [NonSerialized]
        private bool _playerOccurrenceColliderExclusionReported;

        [NonSerialized]
        private string _diagnostic = string.Empty;

        [NonSerialized]
        private string _lastLoggedConfigurationDiagnostic = string.Empty;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private PerceptionAcquisitionStatus _acquisitionStatus;

        [NonSerialized]
        private PerceptionAcquisitionStatus _lastLoggedAcquisitionStatus;

        [NonSerialized]
        private string _lastAcquisitionDiagnostic = string.Empty;

        internal readonly struct CandidateCurrentnessDiagnostic
        {
            private CandidateCurrentnessDiagnostic(
                bool isCurrent,
                string failureReason,
                bool recognitionQueryExecuted,
                bool failureRecognitionQueryExecuted,
                ObjectEntryRecognitionStatus? recognitionStatus,
                string frameworkDiagnostic,
                string failureColliderName,
                string recognitionQueryColliderName,
                int rejectedRecognitionColliderCount)
            {
                IsCurrent = isCurrent;
                FailureReason = failureReason ?? string.Empty;
                RecognitionQueryExecuted = recognitionQueryExecuted;
                FailureRecognitionQueryExecuted = failureRecognitionQueryExecuted;
                RecognitionStatus = recognitionStatus;
                FrameworkDiagnostic = frameworkDiagnostic ?? string.Empty;
                FailureColliderName = failureColliderName ?? string.Empty;
                RecognitionQueryColliderName = recognitionQueryColliderName ?? string.Empty;
                RejectedRecognitionColliderCount = rejectedRecognitionColliderCount;
            }

            internal bool IsCurrent { get; }

            internal string FailureReason { get; }

            internal bool RecognitionQueryExecuted { get; }

            internal bool FailureRecognitionQueryExecuted { get; }

            internal ObjectEntryRecognitionStatus? RecognitionStatus { get; }

            internal string FrameworkDiagnostic { get; }

            internal string FailureColliderName { get; }

            internal string RecognitionQueryColliderName { get; }

            internal int RejectedRecognitionColliderCount { get; }

            internal static CandidateCurrentnessDiagnostic Rejected(
                string reason,
                string colliderName = "") =>
                new CandidateCurrentnessDiagnostic(
                    false,
                    reason,
                    false,
                    false,
                    null,
                    string.Empty,
                    colliderName,
                    string.Empty,
                    0);

            internal static CandidateCurrentnessDiagnostic RejectedAfterRecognition(
                string reason,
                ObjectEntryRecognitionResult recognition,
                string colliderName) =>
                new CandidateCurrentnessDiagnostic(
                    false,
                    reason,
                    true,
                    true,
                    recognition.Status,
                    recognition.Diagnostic,
                    colliderName,
                    colliderName,
                    0);

            internal static CandidateCurrentnessDiagnostic Recognized(
                string colliderName,
                int rejectedRecognitionColliderCount,
                ObjectEntryRecognitionResult? recognition = null) =>
                new CandidateCurrentnessDiagnostic(
                    true,
                    string.Empty,
                    recognition.HasValue,
                    recognition.HasValue,
                    recognition.HasValue ? recognition.Value.Status : null,
                    recognition.HasValue ? recognition.Value.Diagnostic : string.Empty,
                    colliderName,
                    colliderName,
                    rejectedRecognitionColliderCount);

            internal CandidateCurrentnessDiagnostic WithRejectedColliderCount(int count) =>
                new CandidateCurrentnessDiagnostic(
                    IsCurrent,
                    FailureReason,
                    RecognitionQueryExecuted,
                    FailureRecognitionQueryExecuted,
                    RecognitionStatus,
                    FrameworkDiagnostic,
                    FailureColliderName,
                    RecognitionQueryColliderName,
                    count);

            internal CandidateCurrentnessDiagnostic WithRecognitionObservation(
                CandidateCurrentnessDiagnostic recognitionObservation) =>
                new CandidateCurrentnessDiagnostic(
                    IsCurrent,
                    FailureReason,
                    recognitionObservation.RecognitionQueryExecuted,
                    FailureRecognitionQueryExecuted,
                    recognitionObservation.RecognitionStatus,
                    recognitionObservation.FrameworkDiagnostic,
                    FailureColliderName,
                    recognitionObservation.RecognitionQueryColliderName,
                    RejectedRecognitionColliderCount);
        }

        public IReadOnlyList<PegaPlayerObjectEntryCandidate> Candidates => _candidateSnapshot;

        public string Diagnostic => _diagnostic;

        public PerceptionAcquisitionStatus AcquisitionStatus => _acquisitionStatus;

        public PerceptionSensorDefinition CandidateDiscoverySensor => candidateDiscoverySensorInstance != null
            ? candidateDiscoverySensorInstance.Definition
            : null;

        public Vector3 DiscoveryCenter
        {
            get
            {
                CharacterController controller = _characterController != null
                    ? _characterController
                    : GetComponent<CharacterController>();
                return controller != null ? controller.bounds.center : transform.position;
            }
        }

        public bool TryGetDiscoveryVolume(
            out PerceptionSpatialDiscoveryVolume volume,
            out string diagnostic)
        {
            PerceptionSensorDefinition discoverySensor = CandidateDiscoverySensor;
            if (discoverySensor == null)
            {
                volume = default;
                diagnostic = "Player spatial discovery sensor instance or definition is not assigned.";
                return false;
            }

            if (!discoverySensor.TryValidateConfiguration(out diagnostic))
            {
                volume = default;
                return false;
            }

            return discoverySensor.TryGetSpatialDiscoveryVolume(
                transform,
                DiscoveryCenter,
                out volume,
                out diagnostic);
        }

#if UNITY_EDITOR
        public PegaDiagnosticVisualizationLevel DiscoveryVisualizationLevel =>
            discoveryVisualizationLevel;

        public bool DrawGizmosWhenUnselected => drawGizmosWhenUnselected;
#endif

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _perceptionSensorSet = GetComponent<PerceptionSensorSet>();
            _log = PegaLogging.CreateScopedLogger<PegaPlayerObjectEntryDiscoverySource>(
                LogCategory,
                gameObject.name);

            ResolvePerceptionAuthority();
        }

        private void OnEnable()
        {
            _lastLoggedAcquisitionStatus = PerceptionAcquisitionStatus.Unspecified;
            _lastAcquisitionDiagnostic = string.Empty;
            if (_perceptionSensorSet == null)
            {
                SetDiagnostic("A autoridade de Perception não está disponível no Actor do Player.");
                _log.Error(
                    "Player Object Entry discovery could not become the Actor's Perception sampling driver.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("diagnostic", _diagnostic));
                enabled = false;
                return;
            }

            if (!_perceptionSensorSet.TryRegisterSamplingDriver(this, out string driverDiagnostic))
            {
                SetDiagnostic(driverDiagnostic);
                _log.Error(
                    "Player Object Entry discovery could not become the Actor's Perception sampling driver.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("diagnostic", _diagnostic));
                enabled = false;
                return;
            }

            _perceptionSensorSet?.RequestImmediateSample(this, candidateDiscoverySensorInstance);
        }

        private void OnDisable()
        {
            _perceptionSensorSet?.UnregisterSamplingDriver(this);
            _acquisitionStatus = PerceptionAcquisitionStatus.Unavailable;
            ClearCandidates("player-discovery-source-disabled");
        }

        private void OnDestroy()
        {
            _perceptionSensorSet?.UnregisterSamplingDriver(this);
        }

        private void Update()
        {
            if (!object.ReferenceEquals(_configuredDiscoverySensorInstance, candidateDiscoverySensorInstance) ||
                !object.ReferenceEquals(
                    _configuredDiscoverySensorDefinition,
                    CandidateDiscoverySensor))
            {
                ResolvePerceptionAuthority();
                ClearCandidates("perception-sensor-asset-replaced");
                _log.Info(
                    "Player spatial discovery sensor instance or definition was replaced.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("sensorId", CandidateDiscoverySensor != null
                        ? CandidateDiscoverySensor.SensorId
                        : string.Empty),
                    LogFields.Field("reason", "perception-sensor-asset-replaced"));
            }

            if (!TryValidateConfiguration(out string configurationDiagnostic))
            {
                SetDiagnostic(configurationDiagnostic);
                _acquisitionStatus = PerceptionAcquisitionStatus.Unavailable;
                ReportConfigurationDiagnostic(configurationDiagnostic);
                ClearCandidates("invalid-discovery-configuration");
                return;
            }

            if (!string.IsNullOrEmpty(_lastLoggedConfigurationDiagnostic))
            {
                _log.Info(
                    "Player Object Entry discovery configuration became valid again.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("previousDiagnostic", _lastLoggedConfigurationDiagnostic),
                    LogFields.Field("reason", "discovery-configuration-restored"));
                _lastLoggedConfigurationDiagnostic = string.Empty;
            }

            if (_recognitionQueryBinding == null || _playerActorReference == null)
            {
                SetDiagnostic("A consulta de reconhecimento não está disponível para este Player.");
                _acquisitionStatus = PerceptionAcquisitionStatus.Unavailable;
                ClearCandidates("recognition-query-access-unavailable");
                return;
            }

            if (!_perceptionSensorSet.HasDueSensorInstances(this, Time.timeAsDouble))
            {
                return;
            }

            RefreshCandidates();
        }

        internal bool TryBindRecognitionQuery(
            ObjectEntryOccurrenceQueryBinding queryBinding,
            ActorOccurrenceReferenceBinding playerActorReference,
            out string diagnostic)
        {
            if (queryBinding == null)
            {
                diagnostic = "A âncora de reconhecimento de Object Entry é obrigatória.";
                return false;
            }

            ActorOccurrenceResolution resolution = default;
            if (playerActorReference == null ||
                !playerActorReference.TryGetCurrent(out resolution) ||
                !resolution.Succeeded ||
                !object.ReferenceEquals(resolution.Occurrence.ActorRoot, gameObject))
            {
                diagnostic = resolution.Diagnostic;
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "A referência oficial deve resolver uma ocorrência do Player enraizada neste Discovery Source.";
                }

                return false;
            }

            ActorOccurrence playerOccurrence = resolution.Occurrence;
            if (!playerActorReference.TryGetCurrent(out ActorOccurrenceResolution currentResolution) ||
                !currentResolution.Succeeded ||
                !currentResolution.Occurrence.Id.Equals(playerOccurrence.Id) ||
                !object.ReferenceEquals(currentResolution.Occurrence.ActorRoot, gameObject))
            {
                diagnostic = string.IsNullOrWhiteSpace(currentResolution.Diagnostic)
                    ? "A referência oficial do Player mudou durante a conexão da consulta."
                    : currentResolution.Diagnostic;
                return false;
            }

            playerOccurrence = currentResolution.Occurrence;

            bool samePlayerOccurrence = object.ReferenceEquals(_playerActorReference, playerActorReference) &&
                _boundPlayerOccurrenceId.Equals(playerOccurrence.Id);

            if (object.ReferenceEquals(_recognitionQueryBinding, queryBinding) && samePlayerOccurrence)
            {
                _playerOccurrence = playerOccurrence;
                diagnostic = string.Empty;
                return true;
            }

            string clearReason = _recognitionQueryBinding == null
                ? "recognition-query-access-granted"
                : !samePlayerOccurrence
                    ? "player-occurrence-replaced"
                    : "recognition-query-replaced";
            ClearCandidates(clearReason);
            _recognitionQueryBinding = queryBinding;
            _playerActorReference = playerActorReference;
            _boundPlayerOccurrenceId = playerOccurrence.Id;
            _playerOccurrence = playerOccurrence;
            if (!samePlayerOccurrence)
            {
                _playerOccurrenceColliderExclusionReported = false;
            }

            _perceptionSensorSet?.RequestImmediateSample(this, candidateDiscoverySensorInstance);
            SetDiagnostic(string.Empty);
            diagnostic = string.Empty;
            return true;
        }

        internal void UnbindRecognitionQuery(ObjectEntryOccurrenceQueryBinding queryBinding)
        {
            if (!object.ReferenceEquals(_recognitionQueryBinding, queryBinding))
            {
                return;
            }

            _recognitionQueryBinding = null;
            _playerActorReference = null;
            _boundPlayerOccurrenceId = default;
            _playerOccurrence = default;
            _playerOccurrenceColliderExclusionReported = false;
            _acquisitionStatus = PerceptionAcquisitionStatus.Unavailable;
            SetDiagnostic("A consulta de reconhecimento foi liberada pelo lifecycle do conteúdo.");
            ClearCandidates("recognition-query-access-revoked");
        }

        private void RefreshCandidates()
        {
            ObjectEntryOccurrenceQueryBinding queryBinding = _recognitionQueryBinding;
            ActorOccurrenceReferenceBinding actorReference = _playerActorReference;
            if (queryBinding == null || actorReference == null || _perceptionSensorSet == null)
            {
                SetDiagnostic("A consulta de reconhecimento não está disponível para este Player.");
                _acquisitionStatus = PerceptionAcquisitionStatus.Unavailable;
                ClearCandidates("recognition-query-access-unavailable");
                return;
            }

            if (!TryResolveBoundPlayerOccurrence(
                    queryBinding,
                    actorReference,
                    out ActorOccurrenceResolution playerOccurrence,
                    out string occurrenceDiagnostic))
            {
                if (!IsSameBoundAccess(queryBinding, actorReference))
                {
                    return;
                }

                SetDiagnostic(occurrenceDiagnostic);
                _acquisitionStatus = PerceptionAcquisitionStatus.Unavailable;
                ClearCandidates("player-actor-occurrence-unavailable");
                return;
            }

            Vector3 center = DiscoveryCenter;
            if (!_perceptionSensorSet.TrySample(
                    this,
                    playerOccurrence,
                    null,
                    Time.timeAsDouble,
                    candidateDiscoverySensorInstance,
                    center,
                    false,
                    out bool sampled,
                    out IReadOnlyList<PerceptionSensorInstance> sampledSensorInstances,
                    out PerceptionCandidateAcquisition acquisition,
                    out _,
                    out string acquisitionDiagnostic))
            {
                _acquisitionStatus = PerceptionAcquisitionStatus.Unavailable;
                SetDiagnostic(acquisitionDiagnostic);
                LogAcquisitionStatusTransition(
                    _acquisitionStatus,
                    acquisitionDiagnostic,
                    0,
                    playerOccurrence.Occurrence);
                ClearCandidates("spatial-acquisition-unavailable");
                return;
            }

            if (!sampled || acquisition == null ||
                !ContainsSensorInstance(sampledSensorInstances, candidateDiscoverySensorInstance))
            {
                return;
            }

            if (!TryGetSensorAcquisitionSource(
                    acquisition,
                    candidateDiscoverySensorInstance,
                    out PerceptionCandidateSourceResult discoverySource))
            {
                const string missingSourceDiagnostic = "The Player discovery sensor produced no physical acquisition source for its sample.";
                _acquisitionStatus = PerceptionAcquisitionStatus.Unavailable;
                SetDiagnostic(missingSourceDiagnostic);
                LogAcquisitionStatusTransition(
                    _acquisitionStatus,
                    missingSourceDiagnostic,
                    0,
                    playerOccurrence.Occurrence);
                ClearCandidates("discovery-source-unavailable");
                return;
            }

            _seenOccurrences.Clear();
            _seenPhysicalColliders.Clear();
            _scanCandidates.Clear();
            _scanRecognitionIssues.Clear();

            if (!IsSameBoundAccess(queryBinding, actorReference) ||
                !playerOccurrence.Occurrence.Id.Equals(_boundPlayerOccurrenceId))
            {
                return;
            }

            _playerOccurrence = playerOccurrence.Occurrence;

            _acquisitionStatus = discoverySource.Status;
            string statusDiagnostic = GetAcquisitionDiagnostic(discoverySource);
            SetDiagnostic(statusDiagnostic);
            LogAcquisitionStatusTransition(
                discoverySource.Status,
                statusDiagnostic,
                discoverySource.ExcludedObserverColliderCount,
                playerOccurrence.Occurrence);

            if (discoverySource.Status == PerceptionAcquisitionStatus.Unavailable)
            {
                ClearCandidates("spatial-acquisition-unavailable");
                return;
            }

            for (int candidateIndex = 0; candidateIndex < acquisition.Candidates.Count; candidateIndex++)
            {
                PerceptionCandidate candidate = acquisition.Candidates[candidateIndex];
                for (int evidenceIndex = 0; evidenceIndex < candidate.Evidence.Count; evidenceIndex++)
                {
                    PerceptionCandidateEvidence evidence = candidate.Evidence[evidenceIndex];
                    if (evidence.Kind != PerceptionCandidateEvidenceKind.PhysicalCollider ||
                        (evidence.Origin & PerceptionCandidateOrigin.Physical) == 0 ||
                        !ReferenceEquals(evidence.SourceReference, candidateDiscoverySensorInstance))
                    {
                        continue;
                    }

                    Collider candidateCollider = evidence.PhysicalCollider;
                    if (candidateCollider == null || !_seenPhysicalColliders.Add(candidateCollider))
                    {
                        continue;
                    }

                    if (BelongsToPlayerOccurrence(
                            playerOccurrence.Occurrence,
                            candidateCollider.transform))
                    {
                        continue;
                    }

                    ObjectEntryRecognitionResult recognition = queryBinding.Recognize(candidateCollider);
                    if (recognition.Status != ObjectEntryRecognitionStatus.Resolved)
                    {
                        RecordRecognitionIssue(recognition.Status, recognition.Diagnostic, candidateCollider);
                        continue;
                    }

                    if (!recognition.Succeeded)
                    {
                        RecordRecognitionIssue(
                            ObjectEntryRecognitionStatus.Unavailable,
                            string.IsNullOrWhiteSpace(recognition.Diagnostic)
                                ? "The resolved occurrence is no longer physically available."
                                : recognition.Diagnostic,
                            candidateCollider);
                        continue;
                    }

                    if (BelongsToPlayerOccurrence(
                            playerOccurrence.Occurrence,
                            recognition.Occurrence.transform))
                    {
                        continue;
                    }

                    EntityId occurrenceId = recognition.Occurrence.GetEntityId();
                    OccurrenceKey key = new OccurrenceKey(
                        recognition.ObjectEntryId,
                        occurrenceId);
                    if (!_seenOccurrences.Add(key))
                    {
                        AddRecognitionColliderToCandidate(
                            recognition.ObjectEntryId,
                            occurrenceId,
                            candidateCollider);
                        continue;
                    }

                    _scanCandidates.Add(new PegaPlayerObjectEntryCandidate(
                        recognition.ObjectEntryId,
                        recognition.Occurrence,
                        candidateCollider,
                        occurrenceId,
                        recognition.Occurrence.name));
                }
            }

            if (!IsSameBoundAccess(queryBinding, actorReference) ||
                !playerOccurrence.Occurrence.Id.Equals(_boundPlayerOccurrenceId))
            {
                return;
            }

            bool isComplete = discoverySource.Status == PerceptionAcquisitionStatus.Complete;
            if (!isComplete)
            {
                RetainPriorCandidatesForPartialAcquisition();
            }

            LogCandidateTransitions("no-longer-resolved-in-current-discovery-scan", isComplete);
            LogRecognitionIssueTransitions(isComplete);

            _candidateSnapshot = _scanCandidates.Count == 0
                ? Array.Empty<PegaPlayerObjectEntryCandidate>()
                : Array.AsReadOnly(_scanCandidates.ToArray());
        }

        private bool TryResolveBoundPlayerOccurrence(
            ObjectEntryOccurrenceQueryBinding queryBinding,
            ActorOccurrenceReferenceBinding actorReference,
            out ActorOccurrenceResolution resolution,
            out string diagnostic)
        {
            resolution = default;
            if (!IsSameBoundAccess(queryBinding, actorReference))
            {
                diagnostic = "Player recognition access changed while resolving the current Actor occurrence.";
                return false;
            }

            bool resolved = actorReference.TryGetCurrent(out resolution);
            if (!IsSameBoundAccess(queryBinding, actorReference))
            {
                diagnostic = "Player recognition access changed during current Actor occurrence resolution.";
                return false;
            }

            if (!resolved || !resolution.Succeeded)
            {
                diagnostic = string.IsNullOrWhiteSpace(resolution.Diagnostic)
                    ? "The current Framework Actor occurrence is unavailable."
                    : resolution.Diagnostic;
                return false;
            }

            ActorOccurrence occurrence = resolution.Occurrence;
            if (!occurrence.IsValid ||
                !object.ReferenceEquals(occurrence.ActorRoot, gameObject) ||
                !occurrence.Id.Equals(_boundPlayerOccurrenceId))
            {
                diagnostic = "The current Framework Actor occurrence no longer matches this Player discovery source.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool IsSameBoundAccess(
            ObjectEntryOccurrenceQueryBinding queryBinding,
            ActorOccurrenceReferenceBinding actorReference) =>
            isActiveAndEnabled &&
            object.ReferenceEquals(_recognitionQueryBinding, queryBinding) &&
            object.ReferenceEquals(_playerActorReference, actorReference);

        private static bool BelongsToPlayerOccurrence(
            ActorOccurrence playerOccurrence,
            Transform physicalTransform)
        {
            GameObject actorRoot = playerOccurrence.ActorRoot;
            if (!playerOccurrence.IsValid || actorRoot == null || physicalTransform == null)
            {
                return false;
            }

            Transform actorRootTransform = actorRoot.transform;
            return physicalTransform == actorRootTransform || physicalTransform.IsChildOf(actorRootTransform);
        }

        internal bool IsCurrentCandidate(PegaPlayerObjectEntryCandidate candidate) =>
            IsCurrentCandidate(candidate, out _);

        internal bool IsCurrentCandidate(
            PegaPlayerObjectEntryCandidate candidate,
            out CandidateCurrentnessDiagnostic diagnostic)
        {
            ObjectEntryOccurrenceQueryBinding queryBinding = _recognitionQueryBinding;
            ActorOccurrenceReferenceBinding actorReference = _playerActorReference;
            ActorOccurrence playerOccurrence = _playerOccurrence;
            GameObject occurrence = candidate.Occurrence;

            if (queryBinding == null)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("recognition-query-unavailable");
                return false;
            }

            if (actorReference == null)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("player-actor-reference-unavailable");
                return false;
            }

            if (_perceptionSensorSet == null || !_perceptionSensorSet.isActiveAndEnabled)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("player-perception-sensor-set-unavailable");
                return false;
            }

            if (candidateDiscoverySensorInstance == null || !candidateDiscoverySensorInstance.isActiveAndEnabled)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("discovery-sensor-instance-unavailable");
                return false;
            }

            if (occurrence == null)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("occurrence-destroyed-or-unavailable");
                return false;
            }

            if (!occurrence.activeInHierarchy)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("occurrence-inactive");
                return false;
            }

            if (!IsSameBoundAccess(queryBinding, actorReference))
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("player-recognition-access-changed");
                return false;
            }

            if (!playerOccurrence.IsValid)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("player-occurrence-unavailable");
                return false;
            }

            if (!object.ReferenceEquals(playerOccurrence.ActorRoot, gameObject))
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("player-occurrence-root-changed");
                return false;
            }

            if (!playerOccurrence.Id.Equals(_boundPlayerOccurrenceId))
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("player-occurrence-identity-changed");
                return false;
            }

            if (BelongsToPlayerOccurrence(playerOccurrence, occurrence.transform))
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("candidate-belongs-to-player-occurrence");
                return false;
            }

            IReadOnlyList<Collider> recognitionColliders = candidate.RecognitionColliders;
            if (recognitionColliders == null || recognitionColliders.Count == 0)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("candidate-has-no-recognition-colliders");
                return false;
            }

            CandidateCurrentnessDiagnostic lastColliderDiagnostic = default;
            CandidateCurrentnessDiagnostic lastRecognitionQueryDiagnostic = default;
            bool hasRecognitionQueryDiagnostic = false;
            int rejectedColliderCount = 0;
            for (int index = 0; index < recognitionColliders.Count; index++)
            {
                Collider recognitionCollider = recognitionColliders[index];
                if (IsCurrentRecognitionCollider(candidate, recognitionCollider, out CandidateCurrentnessDiagnostic colliderDiagnostic))
                {
                    diagnostic = colliderDiagnostic.WithRejectedColliderCount(rejectedColliderCount);
                    return true;
                }

                rejectedColliderCount++;
                lastColliderDiagnostic = colliderDiagnostic;
                if (colliderDiagnostic.RecognitionQueryExecuted)
                {
                    lastRecognitionQueryDiagnostic = colliderDiagnostic;
                    hasRecognitionQueryDiagnostic = true;
                }
            }

            diagnostic = lastColliderDiagnostic.WithRejectedColliderCount(rejectedColliderCount);
            if (hasRecognitionQueryDiagnostic && !diagnostic.RecognitionQueryExecuted)
            {
                diagnostic = diagnostic.WithRecognitionObservation(lastRecognitionQueryDiagnostic);
            }

            return false;
        }

        bool IPegaObjectInteractionOccurrenceAuthority.TryValidateCurrentOccurrence(
            GameObject occurrenceRoot,
            ObjectEntryId objectEntryId,
            out string diagnostic)
        {
            IReadOnlyList<PegaPlayerObjectEntryCandidate> candidates = _candidateSnapshot;
            if (occurrenceRoot == null || !objectEntryId.IsValid ||
                !isActiveAndEnabled || _recognitionQueryBinding == null)
            {
                diagnostic = "The Player's physical-recognition authority is unavailable.";
                return false;
            }

            for (int index = 0; index < candidates.Count; index++)
            {
                PegaPlayerObjectEntryCandidate candidate = candidates[index];
                if (candidate.ObjectEntryId.Equals(objectEntryId) &&
                    object.ReferenceEquals(candidate.Occurrence, occurrenceRoot) &&
                    IsCurrentCandidate(candidate))
                {
                    diagnostic = string.Empty;
                    return true;
                }
            }

            diagnostic = "The selected physical Object Entry is no longer recognized under the Player's current Activity access.";
            return false;
        }

        internal bool IsCurrentRecognitionCollider(
            PegaPlayerObjectEntryCandidate candidate,
            Collider recognitionCollider) =>
            IsCurrentRecognitionCollider(candidate, recognitionCollider, out _);

        internal bool IsCurrentRecognitionCollider(
            PegaPlayerObjectEntryCandidate candidate,
            Collider recognitionCollider,
            out CandidateCurrentnessDiagnostic diagnostic)
        {
            ObjectEntryOccurrenceQueryBinding queryBinding = _recognitionQueryBinding;
            ActorOccurrenceReferenceBinding actorReference = _playerActorReference;
            ActorOccurrence playerOccurrence = _playerOccurrence;
            GameObject occurrence = candidate.Occurrence;

            if (queryBinding == null || actorReference == null)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected(
                    "recognition-query-or-player-reference-unavailable",
                    recognitionCollider == null ? string.Empty : recognitionCollider.name);
                return false;
            }

            if (_perceptionSensorSet == null || !_perceptionSensorSet.isActiveAndEnabled)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected(
                    "player-perception-sensor-set-unavailable",
                    recognitionCollider == null ? string.Empty : recognitionCollider.name);
                return false;
            }

            if (candidateDiscoverySensorInstance == null || !candidateDiscoverySensorInstance.isActiveAndEnabled)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected(
                    "discovery-sensor-instance-unavailable",
                    recognitionCollider == null ? string.Empty : recognitionCollider.name);
                return false;
            }

            if (recognitionCollider == null)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected("recognition-collider-destroyed-or-unavailable");
                return false;
            }

            if (!recognitionCollider.enabled || !recognitionCollider.gameObject.activeInHierarchy)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected(
                    "recognition-collider-inactive",
                    recognitionCollider.name);
                return false;
            }

            if (occurrence == null || !occurrence.activeInHierarchy)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected(
                    "occurrence-destroyed-inactive-or-unavailable",
                    recognitionCollider.name);
                return false;
            }

            if (!IsSameBoundAccess(queryBinding, actorReference))
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected(
                    "player-recognition-access-changed",
                    recognitionCollider.name);
                return false;
            }

            if (!playerOccurrence.IsValid ||
                !object.ReferenceEquals(playerOccurrence.ActorRoot, gameObject) ||
                !playerOccurrence.Id.Equals(_boundPlayerOccurrenceId))
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected(
                    "player-occurrence-changed-or-unavailable",
                    recognitionCollider.name);
                return false;
            }

            if (BelongsToPlayerOccurrence(playerOccurrence, recognitionCollider.transform))
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected(
                    "candidate-belongs-to-player-occurrence",
                    recognitionCollider.name);
                return false;
            }

            IReadOnlyList<Collider> candidateColliders = candidate.RecognitionColliders;
            bool belongsToCandidateSnapshot = false;
            for (int index = 0; candidateColliders != null && index < candidateColliders.Count; index++)
            {
                if (object.ReferenceEquals(candidateColliders[index], recognitionCollider))
                {
                    belongsToCandidateSnapshot = true;
                    break;
                }
            }

            if (!belongsToCandidateSnapshot)
            {
                diagnostic = CandidateCurrentnessDiagnostic.Rejected(
                    "recognition-collider-not-in-candidate-snapshot",
                    recognitionCollider.name);
                return false;
            }

            ObjectEntryRecognitionResult recognition = queryBinding.Recognize(recognitionCollider);
            if (!IsSameBoundAccess(queryBinding, actorReference))
            {
                diagnostic = CandidateCurrentnessDiagnostic.RejectedAfterRecognition(
                    "player-recognition-access-changed-during-query",
                    recognition,
                    recognitionCollider.name);
                return false;
            }

            if (!playerOccurrence.Id.Equals(_boundPlayerOccurrenceId))
            {
                diagnostic = CandidateCurrentnessDiagnostic.RejectedAfterRecognition(
                    "player-occurrence-identity-changed-during-query",
                    recognition,
                    recognitionCollider.name);
                return false;
            }

            if (!recognition.Succeeded)
            {
                diagnostic = CandidateCurrentnessDiagnostic.RejectedAfterRecognition(
                    "framework-recognition-rejected-collider",
                    recognition,
                    recognitionCollider.name);
                return false;
            }

            if (!recognition.ObjectEntryId.Equals(candidate.ObjectEntryId))
            {
                diagnostic = CandidateCurrentnessDiagnostic.RejectedAfterRecognition(
                    "recognized-object-entry-identity-changed",
                    recognition,
                    recognitionCollider.name);
                return false;
            }

            if (!object.ReferenceEquals(recognition.Occurrence, occurrence))
            {
                diagnostic = CandidateCurrentnessDiagnostic.RejectedAfterRecognition(
                    "recognized-occurrence-reference-changed",
                    recognition,
                    recognitionCollider.name);
                return false;
            }

            diagnostic = CandidateCurrentnessDiagnostic.Recognized(
                recognitionCollider.name,
                rejectedRecognitionColliderCount: 0,
                recognition: recognition);
            return true;
        }

        private void AddRecognitionColliderToCandidate(
            ObjectEntryId objectEntryId,
            EntityId occurrenceId,
            Collider recognitionCollider)
        {
            for (int index = 0; index < _scanCandidates.Count; index++)
            {
                PegaPlayerObjectEntryCandidate candidate = _scanCandidates[index];
                if (candidate.ObjectEntryId.Equals(objectEntryId) &&
                    candidate.OccurrenceId.Equals(occurrenceId))
                {
                    candidate.AddRecognitionCollider(recognitionCollider);
                    return;
                }
            }
        }

        private void ResolvePerceptionAuthority()
        {
            _configuredDiscoverySensorInstance = candidateDiscoverySensorInstance;
            _configuredDiscoverySensorDefinition = CandidateDiscoverySensor;
            _perceptionSensorSet = GetComponent<PerceptionSensorSet>();
        }

        private bool TryValidateConfiguration(out string diagnostic)
        {
            if (_characterController == null || !_characterController.enabled)
            {
                diagnostic = "O root do Player requer um CharacterController ativo.";
                return false;
            }

            if (_perceptionSensorSet == null)
            {
                diagnostic = $"O componente {nameof(PerceptionSensorSet)} é obrigatório no Player.";
                return false;
            }

            PerceptionSensorDefinition discoverySensor = CandidateDiscoverySensor;
            if (candidateDiscoverySensorInstance == null || discoverySensor == null)
            {
                diagnostic = "A instância de sensor para aquisição espacial do Player é obrigatória.";
                return false;
            }

            if (!_perceptionSensorSet.ContainsSensorInstance(candidateDiscoverySensorInstance))
            {
                diagnostic = "A instância de descoberta precisa estar composta no mesmo Actor que a autoridade de Perception.";
                return false;
            }

            if (!candidateDiscoverySensorInstance.isActiveAndEnabled)
            {
                diagnostic = "A instância de descoberta do Player está desabilitada.";
                return false;
            }

            if (!discoverySensor.TryValidateConfiguration(out diagnostic))
            {
                return false;
            }

            if (!discoverySensor.TryGetSpatialDiscoveryVolume(
                    transform,
                    DiscoveryCenter,
                    out PerceptionSpatialDiscoveryVolume volume,
                    out diagnostic))
            {
                return false;
            }

            if (volume.Shape != PerceptionSpatialVolumeShape.Sphere)
            {
                diagnostic = "O asset do Player deve fornecer um volume esférico 3D.";
                return false;
            }

            if ((volume.Center - DiscoveryCenter).sqrMagnitude > 0.000001f)
            {
                diagnostic = "O centro do volume do Player deve permanecer no centro físico atual do CharacterController.";
                return false;
            }

            if (volume.CandidateLayerMask == ~0)
            {
                diagnostic = "Configure layers de candidatos específicas no asset, sem consultar todas as layers.";
                return false;
            }

            if (volume.TriggerInteraction != QueryTriggerInteraction.Collide)
            {
                diagnostic = "O asset do Player deve manter QueryTriggerInteraction.Collide.";
                return false;
            }

            if (_perceptionSensorSet == null ||
                !_perceptionSensorSet.TryValidateComposition(out diagnostic))
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "A composição de sensores de Perception do Player está indisponível.";
                }

                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool ContainsSensorInstance(
            IReadOnlyList<PerceptionSensorInstance> sensorInstances,
            PerceptionSensorInstance target)
        {
            if (sensorInstances == null || target == null)
            {
                return false;
            }

            for (int index = 0; index < sensorInstances.Count; index++)
            {
                if (ReferenceEquals(sensorInstances[index], target))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearCandidates(string reason)
        {
            for (int index = 0; index < _candidateSnapshot.Count; index++)
            {
                LogCandidateRemoved(_candidateSnapshot[index], reason);
            }

            _candidateSnapshot = Array.Empty<PegaPlayerObjectEntryCandidate>();
            _scanCandidates.Clear();
            _seenOccurrences.Clear();
            _seenPhysicalColliders.Clear();
            _scanRecognitionIssues.Clear();
            _activeRecognitionIssues.Clear();
        }

        private void RetainPriorCandidatesForPartialAcquisition()
        {
            for (int index = 0; index < _candidateSnapshot.Count; index++)
            {
                PegaPlayerObjectEntryCandidate candidate = _candidateSnapshot[index];
                OccurrenceKey key = new OccurrenceKey(candidate.ObjectEntryId, candidate.OccurrenceId);
                if (_seenOccurrences.Add(key))
                {
                    _scanCandidates.Add(candidate);
                    continue;
                }

                MergePriorRecognitionColliders(candidate, key);
            }
        }

        private void MergePriorRecognitionColliders(
            PegaPlayerObjectEntryCandidate previousCandidate,
            OccurrenceKey key)
        {
            for (int candidateIndex = 0; candidateIndex < _scanCandidates.Count; candidateIndex++)
            {
                PegaPlayerObjectEntryCandidate currentCandidate = _scanCandidates[candidateIndex];
                if (!key.Equals(new OccurrenceKey(
                        currentCandidate.ObjectEntryId,
                        currentCandidate.OccurrenceId)))
                {
                    continue;
                }

                IReadOnlyList<Collider> previousColliders = previousCandidate.RecognitionColliders;
                for (int colliderIndex = 0; previousColliders != null && colliderIndex < previousColliders.Count; colliderIndex++)
                {
                    currentCandidate.AddRecognitionCollider(previousColliders[colliderIndex]);
                }

                return;
            }
        }

        private void LogCandidateTransitions(string removalReason, bool mayRemoveAbsentCandidates)
        {
            for (int index = 0; index < _scanCandidates.Count; index++)
            {
                PegaPlayerObjectEntryCandidate candidate = _scanCandidates[index];
                OccurrenceKey key = new OccurrenceKey(candidate.ObjectEntryId, candidate.OccurrenceId);
                if (!ContainsCandidate(_candidateSnapshot, key))
                {
                    LogCandidateAdded(candidate);
                }
            }

            if (!mayRemoveAbsentCandidates)
            {
                return;
            }

            for (int index = 0; index < _candidateSnapshot.Count; index++)
            {
                PegaPlayerObjectEntryCandidate candidate = _candidateSnapshot[index];
                OccurrenceKey key = new OccurrenceKey(candidate.ObjectEntryId, candidate.OccurrenceId);
                if (!_seenOccurrences.Contains(key))
                {
                    LogCandidateRemoved(candidate, removalReason);
                }
            }
        }

        private static bool ContainsCandidate(
            IReadOnlyList<PegaPlayerObjectEntryCandidate> candidates,
            OccurrenceKey key)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                PegaPlayerObjectEntryCandidate candidate = candidates[index];
                if (key.Equals(new OccurrenceKey(candidate.ObjectEntryId, candidate.OccurrenceId)))
                {
                    return true;
                }
            }

            return false;
        }

        private void LogCandidateAdded(PegaPlayerObjectEntryCandidate candidate)
        {
            _log.Info(
                "Player discovered a committed Object Entry occurrence.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("objectEntryId", candidate.ObjectEntryId.StableText),
                LogFields.Field("occurrenceId", candidate.OccurrenceId.ToString()),
                LogFields.Field("physicalRoot", candidate.PhysicalRootName),
                LogFields.Field("reason", "recognized-within-discovery-volume"));
        }

        private static bool TryGetSensorAcquisitionSource(
            PerceptionCandidateAcquisition acquisition,
            PerceptionSensorInstance sensorInstance,
            out PerceptionCandidateSourceResult source)
        {
            for (int index = 0; index < acquisition.Sources.Count; index++)
            {
                PerceptionCandidateSourceResult candidateSource = acquisition.Sources[index];
                if (ReferenceEquals(candidateSource.SourceReference, sensorInstance) &&
                    (candidateSource.Origin & PerceptionCandidateOrigin.Physical) != 0)
                {
                    source = candidateSource;
                    return true;
                }
            }

            source = default;
            return false;
        }

        private static string GetAcquisitionDiagnostic(PerceptionCandidateSourceResult source)
        {
            return !string.IsNullOrWhiteSpace(source.Diagnostic)
                ? source.Diagnostic
                : source.Status == PerceptionAcquisitionStatus.Partial
                    ? "Player discovery sensor acquisition coverage is partial."
                    : source.Status == PerceptionAcquisitionStatus.Unavailable
                        ? "Player discovery sensor acquisition is unavailable."
                        : string.Empty;
        }

        private void LogAcquisitionStatusTransition(
            PerceptionAcquisitionStatus status,
            string diagnostic,
            int excludedObserverColliderCount,
            ActorOccurrence playerOccurrence)
        {
            if (excludedObserverColliderCount > 0 && playerOccurrence.IsValid)
            {
                LogPlayerOccurrenceCollidersExcluded(
                    excludedObserverColliderCount,
                    playerOccurrence);
            }

            if (status == _lastLoggedAcquisitionStatus &&
                string.Equals(diagnostic, _lastAcquisitionDiagnostic, StringComparison.Ordinal))
            {
                return;
            }

            if (status == PerceptionAcquisitionStatus.Partial)
            {
                _log.Warning(
                    "Player spatial candidate acquisition is partial; prior candidates remain until a complete scan.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("diagnostic", diagnostic),
                    LogFields.Field("reason", "partial-perception-acquisition"));
            }
            else if (status == PerceptionAcquisitionStatus.Unavailable)
            {
                _log.Warning(
                    "Player spatial candidate acquisition is unavailable.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("diagnostic", diagnostic),
                    LogFields.Field("reason", "perception-acquisition-unavailable"));
            }
            else if (_lastLoggedAcquisitionStatus != PerceptionAcquisitionStatus.Unspecified &&
                     _lastLoggedAcquisitionStatus != PerceptionAcquisitionStatus.Complete)
            {
                _log.Info(
                    "Player spatial candidate acquisition returned to complete coverage.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("reason", "complete-perception-acquisition-restored"));
            }
            else if (_lastLoggedAcquisitionStatus == PerceptionAcquisitionStatus.Unspecified)
            {
                _log.Info(
                    "Player spatial candidate acquisition started with complete coverage.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("reason", "initial-complete-perception-acquisition"));
            }
            else
            {
                _log.Debug(
                    "Player spatial candidate acquisition is complete.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("reason", "complete-perception-acquisition"));
            }

            _lastLoggedAcquisitionStatus = status;
            _lastAcquisitionDiagnostic = diagnostic ?? string.Empty;
        }

        private void LogPlayerOccurrenceCollidersExcluded(
            int colliderCount,
            ActorOccurrence playerOccurrence)
        {
            if (colliderCount <= 0 || _playerOccurrenceColliderExclusionReported)
            {
                return;
            }

            GameObject actorRoot = playerOccurrence.ActorRoot;
            if (actorRoot == null)
            {
                return;
            }

            _playerOccurrenceColliderExclusionReported = true;
            _log.Debug(
                "Colliders belonging to the current Player Actor occurrence were excluded from Object Entry discovery.",
                LogFields.Field("actorOccurrenceId", playerOccurrence.Id.ToString()),
                LogFields.Field("playerRootEntityId", actorRoot.GetEntityId().ToString()),
                LogFields.Field("excludedColliderCount", colliderCount),
                LogFields.Field("reason", "belongs-to-current-player-occurrence"));
        }

        private void LogCandidateRemoved(PegaPlayerObjectEntryCandidate candidate, string reason)
        {
            _log.Info(
                "Player removed an Object Entry occurrence from the discovery snapshot.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("objectEntryId", candidate.ObjectEntryId.StableText),
                LogFields.Field("occurrenceId", candidate.OccurrenceId.ToString()),
                LogFields.Field("physicalRoot", candidate.PhysicalRootName),
                LogFields.Field("reason", reason));
        }

        private void RecordRecognitionIssue(
            ObjectEntryRecognitionStatus status,
            string diagnostic,
            Collider candidateCollider)
        {
            string issueDiagnostic = string.IsNullOrWhiteSpace(diagnostic)
                ? $"Framework returned {status} without a diagnostic."
                : diagnostic;
            RecognitionIssueKey key = new RecognitionIssueKey(status, issueDiagnostic);

            if (_scanRecognitionIssues.TryGetValue(key, out RecognitionIssueSummary summary))
            {
                summary.count++;
                _scanRecognitionIssues[key] = summary;
                return;
            }

            _scanRecognitionIssues.Add(
                key,
                new RecognitionIssueSummary(
                    candidateCollider.name,
                    candidateCollider.GetEntityId().ToString()));
        }

        private void LogRecognitionIssueTransitions(bool acquisitionComplete)
        {
            foreach (KeyValuePair<RecognitionIssueKey, RecognitionIssueSummary> issue in _scanRecognitionIssues)
            {
                if (!_activeRecognitionIssues.Contains(issue.Key))
                {
                    LogRecognitionIssue(issue.Key, issue.Value);
                }
            }

            if (acquisitionComplete)
            {
                foreach (RecognitionIssueKey previousIssue in _activeRecognitionIssues)
                {
                    if (_scanRecognitionIssues.ContainsKey(previousIssue) ||
                        previousIssue.Status == ObjectEntryRecognitionStatus.Unrecognized)
                    {
                        continue;
                    }

                    _log.Debug(
                        "Object Entry recognition diagnostic is no longer observed in the discovery volume.",
                        LogFields.Field("recognitionStatus", previousIssue.Status),
                        LogFields.Field("diagnostic", previousIssue.Diagnostic),
                        LogFields.Field("reason", "no-collider-returned-this-recognition-status"));
                }

                _activeRecognitionIssues.Clear();
            }

            foreach (RecognitionIssueKey currentIssue in _scanRecognitionIssues.Keys)
            {
                _activeRecognitionIssues.Add(currentIssue);
            }
        }

        private void LogRecognitionIssue(RecognitionIssueKey issue, RecognitionIssueSummary summary)
        {
            LogField[] fields =
            {
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("recognitionStatus", issue.Status),
                LogFields.Field("colliderCount", summary.count),
                LogFields.Field("sampleCollider", summary.SampleColliderName),
                LogFields.Field("sampleColliderEntityId", summary.SampleColliderEntityId),
                LogFields.Field("diagnostic", issue.Diagnostic),
                LogFields.Field("reason", RecognitionIssueReason(issue.Status))
            };

            switch (issue.Status)
            {
                case ObjectEntryRecognitionStatus.Unrecognized:
                    _log.Debug(
                        "Expected unrelated colliders were ignored because they have no Object Entry declaration.",
                        fields);
                    break;

                case ObjectEntryRecognitionStatus.Unavailable:
                    _log.Info(
                        "Framework could not resolve these colliders to committed Object Entry occurrences; they were excluded.",
                        fields);
                    break;

                case ObjectEntryRecognitionStatus.Invalid:
                    _log.Error(
                        "Framework rejected invalid physical Object Entry recognition evidence.",
                        fields);
                    break;

                case ObjectEntryRecognitionStatus.Ambiguous:
                    _log.Error(
                        "Framework reported ambiguous physical Object Entry recognition evidence.",
                        fields);
                    break;

                default:
                    _log.Warning(
                        "Framework returned an unhandled Object Entry recognition status.",
                        fields);
                    break;
            }
        }

        private static string RecognitionIssueReason(ObjectEntryRecognitionStatus status)
        {
            switch (status)
            {
                case ObjectEntryRecognitionStatus.Unrecognized:
                    return "no-object-entry-declaration";
                case ObjectEntryRecognitionStatus.Unavailable:
                    return "recognition-unavailable-or-occurrence-not-admitted";
                case ObjectEntryRecognitionStatus.Invalid:
                    return "invalid-recognition-evidence";
                case ObjectEntryRecognitionStatus.Ambiguous:
                    return "ambiguous-committed-binding-evidence";
                default:
                    return "unhandled-recognition-status";
            }
        }

        private void SetDiagnostic(string diagnostic)
        {
            _diagnostic = diagnostic ?? string.Empty;
        }

        private void ReportConfigurationDiagnostic(string diagnostic)
        {
            if (string.Equals(
                    _lastLoggedConfigurationDiagnostic,
                    diagnostic,
                    StringComparison.Ordinal))
            {
                return;
            }

            _lastLoggedConfigurationDiagnostic = diagnostic;
            _log.Error(
                "Player Object Entry discovery configuration is invalid.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("diagnostic", diagnostic),
                LogFields.Field("reason", "invalid-discovery-configuration"));
        }

        private readonly struct OccurrenceKey : IEquatable<OccurrenceKey>
        {
            private readonly ObjectEntryId _objectEntryId;
            private readonly EntityId _occurrenceId;

            internal OccurrenceKey(ObjectEntryId objectEntryId, EntityId occurrenceId)
            {
                _objectEntryId = objectEntryId;
                _occurrenceId = occurrenceId;
            }

            public bool Equals(OccurrenceKey other) =>
                _objectEntryId.Equals(other._objectEntryId) &&
                _occurrenceId.Equals(other._occurrenceId);

            public override bool Equals(object obj) =>
                obj is OccurrenceKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_objectEntryId.GetHashCode() * 397) ^
                        _occurrenceId.GetHashCode();
                }
            }
        }

        private readonly struct RecognitionIssueKey : IEquatable<RecognitionIssueKey>
        {
            internal RecognitionIssueKey(ObjectEntryRecognitionStatus status, string diagnostic)
            {
                Status = status;
                Diagnostic = diagnostic ?? string.Empty;
            }

            internal ObjectEntryRecognitionStatus Status { get; }

            internal string Diagnostic { get; }

            public bool Equals(RecognitionIssueKey other) =>
                Status == other.Status &&
                string.Equals(Diagnostic, other.Diagnostic, StringComparison.Ordinal);

            public override bool Equals(object obj) =>
                obj is RecognitionIssueKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Status * 397) ^ StringComparer.Ordinal.GetHashCode(Diagnostic);
                }
            }
        }

        private struct RecognitionIssueSummary
        {
            internal RecognitionIssueSummary(string sampleColliderName, string sampleColliderEntityId)
            {
                count = 1;
                SampleColliderName = sampleColliderName;
                SampleColliderEntityId = sampleColliderEntityId;
            }

            internal int count;

            internal string SampleColliderName { get; }

            internal string SampleColliderEntityId { get; }
        }
    }

    /// <summary>Identidade e raiz física reconhecidas pelo Framework para um candidato do Player.</summary>
    public readonly struct PegaPlayerObjectEntryCandidate
    {
        private readonly List<Collider> _recognitionColliders;

        internal PegaPlayerObjectEntryCandidate(
            ObjectEntryId objectEntryId,
            GameObject occurrence,
            Collider recognitionCollider,
            EntityId occurrenceId,
            string physicalRootName)
        {
            ObjectEntryId = objectEntryId;
            Occurrence = occurrence;
            _recognitionColliders = new List<Collider> { recognitionCollider };
            OccurrenceId = occurrenceId;
            PhysicalRootName = physicalRootName;
        }

        public ObjectEntryId ObjectEntryId { get; }

        public GameObject Occurrence { get; }

        internal IReadOnlyList<Collider> RecognitionColliders => _recognitionColliders;

        internal void AddRecognitionCollider(Collider recognitionCollider)
        {
            for (int index = 0; index < _recognitionColliders.Count; index++)
            {
                if (_recognitionColliders[index] == recognitionCollider)
                {
                    return;
                }
            }

            _recognitionColliders.Add(recognitionCollider);
        }

        internal EntityId OccurrenceId { get; }

        internal string PhysicalRootName { get; }
    }
}

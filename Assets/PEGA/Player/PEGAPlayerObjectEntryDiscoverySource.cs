using System;
using System.Collections.Generic;
using Immersive.Framework.ObjectEntry;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.Player
{
    /// <summary>Descobre ocorrências físicas de Object Entry próximas ao Player.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("PEGA/Player/Object Entry Discovery Source")]
    public sealed class PEGAPlayerObjectEntryDiscoverySource : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.PlayerObjectDiscovery";
        private const float MinimumScanIntervalSeconds = 0.05f;
        private const int InitialColliderBufferCapacity = 32;

        [Header("Physical Discovery")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Raio do volume esférico 3D consultado ao redor do centro físico do Player (m).")]
        private float discoveryRadiusMeters = 12f;

        [SerializeField, Min(MinimumScanIntervalSeconds)]
        [Tooltip("Intervalo entre consultas de colliders no volume do Player (s).")]
        private float scanIntervalSeconds = 0.2f;

        [SerializeField]
        [Tooltip("Layers que podem conter colliders de Object Entries candidatos.")]
        private LayerMask candidateLayers = ~0;

        [Header("Scene View")]
        [SerializeField]
        [Tooltip("Desenha o volume e as ocorrências descobertas na Scene View.")]
        private bool drawDiscoveryGizmos = true;

        [SerializeField]
        [Tooltip("Desenha os Gizmos mesmo quando este Player não está selecionado.")]
        private bool drawGizmosWhenUnselected;

        [NonSerialized]
        private CharacterController _characterController;

        [NonSerialized]
        private Collider[] _overlapBuffer = new Collider[InitialColliderBufferCapacity];

        [NonSerialized]
        private readonly HashSet<OccurrenceKey> _seenOccurrences = new HashSet<OccurrenceKey>();

        [NonSerialized]
        private readonly List<PEGAPlayerObjectEntryCandidate> _scanCandidates =
            new List<PEGAPlayerObjectEntryCandidate>();

        [NonSerialized]
        private readonly Dictionary<RecognitionIssueKey, RecognitionIssueSummary> _scanRecognitionIssues =
            new Dictionary<RecognitionIssueKey, RecognitionIssueSummary>();

        [NonSerialized]
        private readonly HashSet<RecognitionIssueKey> _activeRecognitionIssues =
            new HashSet<RecognitionIssueKey>();

        [NonSerialized]
        private IReadOnlyList<PEGAPlayerObjectEntryCandidate> _candidateSnapshot =
            Array.Empty<PEGAPlayerObjectEntryCandidate>();

        [NonSerialized]
        private ObjectEntryOccurrenceQueryBinding _recognitionQueryBinding;

        [NonSerialized]
        private float _timeUntilNextScan;

        [NonSerialized]
        private string _diagnostic = string.Empty;

        [NonSerialized]
        private string _lastLoggedConfigurationDiagnostic = string.Empty;

        [NonSerialized]
        private ScopedLogger _log;

        public IReadOnlyList<PEGAPlayerObjectEntryCandidate> Candidates => _candidateSnapshot;

        public string Diagnostic => _diagnostic;

        public float DiscoveryRadiusMeters => discoveryRadiusMeters;

        public Vector3 DiscoveryCenter => _characterController != null
            ? _characterController.bounds.center
            : transform.position;

#if UNITY_EDITOR
        public bool DrawDiscoveryGizmos => drawDiscoveryGizmos;

        public bool DrawGizmosWhenUnselected => drawGizmosWhenUnselected;
#endif

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _log = PEGALogging.CreateScopedLogger<PEGAPlayerObjectEntryDiscoverySource>(
                LogCategory,
                gameObject.name);
        }

        private void OnEnable()
        {
            _timeUntilNextScan = 0f;
        }

        private void OnDisable()
        {
            ClearCandidates("player-discovery-source-disabled");
        }

        private void Update()
        {
            if (!TryValidateConfiguration(out string configurationDiagnostic))
            {
                SetDiagnostic(configurationDiagnostic);
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

            if (_recognitionQueryBinding == null)
            {
                SetDiagnostic("A consulta de reconhecimento não está disponível para este Player.");
                ClearCandidates("recognition-query-access-unavailable");
                return;
            }

            _timeUntilNextScan -= Time.deltaTime;
            if (_timeUntilNextScan > 0f)
            {
                return;
            }

            _timeUntilNextScan = scanIntervalSeconds;
            RefreshCandidates();
        }

        internal bool TryBindRecognitionQuery(
            ObjectEntryOccurrenceQueryBinding queryBinding,
            out string diagnostic)
        {
            if (queryBinding == null)
            {
                diagnostic = "A âncora de reconhecimento de Object Entry é obrigatória.";
                return false;
            }

            if (object.ReferenceEquals(_recognitionQueryBinding, queryBinding))
            {
                diagnostic = string.Empty;
                return true;
            }

            ClearCandidates(_recognitionQueryBinding == null
                ? "recognition-query-access-granted"
                : "recognition-query-replaced");
            _recognitionQueryBinding = queryBinding;
            _timeUntilNextScan = 0f;
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
            _timeUntilNextScan = 0f;
            SetDiagnostic("A consulta de reconhecimento foi liberada pelo lifecycle do conteúdo.");
            ClearCandidates("recognition-query-access-revoked");
        }

        private void RefreshCandidates()
        {
            ObjectEntryOccurrenceQueryBinding queryBinding = _recognitionQueryBinding;
            if (queryBinding == null)
            {
                SetDiagnostic("A consulta de reconhecimento não está disponível para este Player.");
                ClearCandidates("recognition-query-access-unavailable");
                return;
            }

            SetDiagnostic(string.Empty);
            _seenOccurrences.Clear();
            _scanCandidates.Clear();
            _scanRecognitionIssues.Clear();

            Vector3 center = DiscoveryCenter;
            int colliderCount = QueryOverlappingColliders(center);
            for (int index = 0; index < colliderCount; index++)
            {
                Collider candidateCollider = _overlapBuffer[index];
                if (candidateCollider == null || BelongsToThisPlayer(candidateCollider))
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

                EntityId occurrenceId = recognition.Occurrence.GetEntityId();
                OccurrenceKey key = new OccurrenceKey(
                    recognition.ObjectEntryId,
                    occurrenceId);
                if (!_seenOccurrences.Add(key))
                {
                    continue;
                }

                _scanCandidates.Add(new PEGAPlayerObjectEntryCandidate(
                    recognition.ObjectEntryId,
                    recognition.Occurrence,
                    occurrenceId,
                    recognition.Occurrence.name));
            }

            LogCandidateTransitions("no-longer-resolved-in-current-discovery-scan");
            LogRecognitionIssueTransitions();

            _candidateSnapshot = _scanCandidates.Count == 0
                ? Array.Empty<PEGAPlayerObjectEntryCandidate>()
                : Array.AsReadOnly(_scanCandidates.ToArray());
        }

        private int QueryOverlappingColliders(Vector3 center)
        {
            if (_overlapBuffer == null || _overlapBuffer.Length == 0)
            {
                _overlapBuffer = new Collider[InitialColliderBufferCapacity];
            }

            int colliderCount;
            do
            {
                colliderCount = Physics.OverlapSphereNonAlloc(
                    center,
                    discoveryRadiusMeters,
                    _overlapBuffer,
                    candidateLayers,
                    QueryTriggerInteraction.Collide);

                if (colliderCount < _overlapBuffer.Length)
                {
                    return colliderCount;
                }

                Array.Resize(ref _overlapBuffer, _overlapBuffer.Length * 2);
            }
            while (true);
        }

        private bool BelongsToThisPlayer(Collider candidateCollider)
        {
            Transform candidateTransform = candidateCollider.transform;
            return candidateTransform == transform || candidateTransform.IsChildOf(transform);
        }

        private bool TryValidateConfiguration(out string diagnostic)
        {
            if (_characterController == null)
            {
                diagnostic = "O root do Player requer um CharacterController ativo.";
                return false;
            }

            if (!IsPositiveFinite(discoveryRadiusMeters))
            {
                diagnostic = "O raio de descoberta 3D deve ser finito e maior que zero.";
                return false;
            }

            if (!IsPositiveFinite(scanIntervalSeconds) ||
                scanIntervalSeconds < MinimumScanIntervalSeconds)
            {
                diagnostic = $"O intervalo de consulta deve ser de pelo menos {MinimumScanIntervalSeconds:0.00} s.";
                return false;
            }

            if (candidateLayers.value == 0)
            {
                diagnostic = "Selecione ao menos uma Layer para descobrir colliders.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private void ClearCandidates(string reason)
        {
            for (int index = 0; index < _candidateSnapshot.Count; index++)
            {
                LogCandidateRemoved(_candidateSnapshot[index], reason);
            }

            _candidateSnapshot = Array.Empty<PEGAPlayerObjectEntryCandidate>();
            _scanCandidates.Clear();
            _seenOccurrences.Clear();
            _scanRecognitionIssues.Clear();
            _activeRecognitionIssues.Clear();
        }

        private void LogCandidateTransitions(string removalReason)
        {
            for (int index = 0; index < _scanCandidates.Count; index++)
            {
                PEGAPlayerObjectEntryCandidate candidate = _scanCandidates[index];
                OccurrenceKey key = new OccurrenceKey(candidate.ObjectEntryId, candidate.OccurrenceId);
                if (!ContainsCandidate(_candidateSnapshot, key))
                {
                    LogCandidateAdded(candidate);
                }
            }

            for (int index = 0; index < _candidateSnapshot.Count; index++)
            {
                PEGAPlayerObjectEntryCandidate candidate = _candidateSnapshot[index];
                OccurrenceKey key = new OccurrenceKey(candidate.ObjectEntryId, candidate.OccurrenceId);
                if (!_seenOccurrences.Contains(key))
                {
                    LogCandidateRemoved(candidate, removalReason);
                }
            }
        }

        private static bool ContainsCandidate(
            IReadOnlyList<PEGAPlayerObjectEntryCandidate> candidates,
            OccurrenceKey key)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                PEGAPlayerObjectEntryCandidate candidate = candidates[index];
                if (key.Equals(new OccurrenceKey(candidate.ObjectEntryId, candidate.OccurrenceId)))
                {
                    return true;
                }
            }

            return false;
        }

        private void LogCandidateAdded(PEGAPlayerObjectEntryCandidate candidate)
        {
            _log.Info(
                "Player discovered a committed Object Entry occurrence.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("objectEntryId", candidate.ObjectEntryId.StableText),
                LogFields.Field("occurrenceId", candidate.OccurrenceId.ToString()),
                LogFields.Field("physicalRoot", candidate.PhysicalRootName),
                LogFields.Field("reason", "recognized-within-discovery-volume"));
        }

        private void LogCandidateRemoved(PEGAPlayerObjectEntryCandidate candidate, string reason)
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
                summary.Count++;
                _scanRecognitionIssues[key] = summary;
                return;
            }

            _scanRecognitionIssues.Add(
                key,
                new RecognitionIssueSummary(
                    candidateCollider.name,
                    candidateCollider.GetEntityId().ToString()));
        }

        private void LogRecognitionIssueTransitions()
        {
            foreach (KeyValuePair<RecognitionIssueKey, RecognitionIssueSummary> issue in _scanRecognitionIssues)
            {
                if (!_activeRecognitionIssues.Contains(issue.Key))
                {
                    LogRecognitionIssue(issue.Key, issue.Value);
                }
            }

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
                LogFields.Field("colliderCount", summary.Count),
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

        private void OnValidate()
        {
            if (!IsPositiveFinite(discoveryRadiusMeters))
            {
                discoveryRadiusMeters = 0.1f;
            }

            if (!IsPositiveFinite(scanIntervalSeconds) ||
                scanIntervalSeconds < MinimumScanIntervalSeconds)
            {
                scanIntervalSeconds = MinimumScanIntervalSeconds;
            }
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

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
                Count = 1;
                SampleColliderName = sampleColliderName;
                SampleColliderEntityId = sampleColliderEntityId;
            }

            internal int Count;

            internal string SampleColliderName { get; }

            internal string SampleColliderEntityId { get; }
        }
    }

    /// <summary>Identidade e raiz física reconhecidas pelo Framework para um candidato do Player.</summary>
    public readonly struct PEGAPlayerObjectEntryCandidate
    {
        internal PEGAPlayerObjectEntryCandidate(
            ObjectEntryId objectEntryId,
            GameObject occurrence,
            EntityId occurrenceId,
            string physicalRootName)
        {
            ObjectEntryId = objectEntryId;
            Occurrence = occurrence;
            OccurrenceId = occurrenceId;
            PhysicalRootName = physicalRootName;
        }

        public ObjectEntryId ObjectEntryId { get; }

        public GameObject Occurrence { get; }

        internal EntityId OccurrenceId { get; }

        internal string PhysicalRootName { get; }
    }
}

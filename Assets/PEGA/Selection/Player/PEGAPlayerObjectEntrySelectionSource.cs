using System;
using System.Collections.Generic;
using System.Text;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.Player.Perception;
using UnityEngine;
namespace PEGA.Selection.Player
{
    /// <summary>Seleciona a ocorrência física elegível mais relevante à frente deste Player.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PegaPlayerObjectEntryDiscoverySource))]
    [AddComponentMenu("PEGA/Selection/Player/Object Entry Selection Source")]
    public sealed class PegaPlayerObjectEntrySelectionSource : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.PlayerObjectSelection";
        private const int PlayerVolumeGridSize = 3;
        private const int InitialVisibilityHitBufferSize = 32;
        private const int MaximumVisibilityHitBufferSize = 1024;

        [Header("Seleção Contextual 3D")]
        [SerializeField, Min(0f), InspectorName("Alcance de seleção (m)")]
        [Tooltip("Distância máxima do centro físico do Player até um ponto visível do collider reconhecido. Use metros.")]
        private float selectionRangeMeters = 5f;

        [SerializeField, Range(0f, 90f), InspectorName("Ângulo horizontal (±°)")]
        [Tooltip("Semiângulo para cada lado da direção horizontal do Player. Define a abertura lateral do volume frontal, em graus.")]
        private float selectionHorizontalHalfAngleDegrees = 35f;

        [SerializeField, Range(0f, 90f), InspectorName("Ângulo vertical (±°)")]
        [Tooltip("Elevação permitida acima e abaixo do plano horizontal do Player, em graus. Permite selecionar objetos no chão e em superfícies elevadas.")]
        private float selectionVerticalHalfAngleDegrees = 70f;

        [SerializeField, Range(0f, 90f), InspectorName("Margem de retenção (°)")]
        [Tooltip("Margem angular adicionada aos dois ângulos somente para manter a ocorrência já selecionada. Não altera a elegibilidade de novos alvos.")]
        private float stickyVolumeMarginDegrees = 5f;

        [Header("Priorização e Estabilidade")]
        [SerializeField, Range(0f, 1f), InspectorName("Peso da distância")]
        [Tooltip("Participação normalizada da proximidade física no ranqueamento relativo dos alvos. O peso total de distância, volume frontal e direção não pode exceder 1.")]
        private float proximityWeight = 0.3f;

        [SerializeField, Range(0f, 1f), InspectorName("Peso da direção do Player")]
        [Tooltip("Participação do alinhamento entre a direção horizontal do Player e o alvo no ranqueamento relativo.")]
        private float playerFacingWeight = 0.2f;

        [SerializeField, Range(0f, 1f), InspectorName("Alinhamento mínimo para intenção")]
        [Tooltip("Alinhamento normalizado mínimo entre a frente horizontal do Player e outro alvo para reconhecer intenção clara de troca.")]
        private float facingIntentAlignmentThreshold = 0.9f;

        [SerializeField, Range(0f, 1f), InspectorName("Vantagem mínima de alinhamento")]
        [Tooltip("Vantagem mínima no alinhamento frontal de outro alvo para superar a retenção do foco atual.")]
        private float facingIntentAdvantage = 0.2f;

        [SerializeField, Range(0f, 1f), InspectorName("Margem de troca de foco")]
        [Tooltip("Vantagem mínima no score relativo de outro candidato para iniciar a troca de foco.")]
        private float switchScoreMargin = 0.12f;

        [SerializeField, Min(0f), InspectorName("Confirmação da troca (s)")]
        [Tooltip("Tempo pelo qual outro candidato deve manter vantagem antes de se tornar o foco. Use segundos.")]
        private float switchConfirmationSeconds = 0.15f;

        [Header("Visualização na Scene View")]
        [SerializeField, InspectorName("Nível de visualização")]
        [Tooltip("Off oculta a seleção; Resumido mostra alcance e foco; Detalhado também mostra identidade e limites angulares do volume frontal.")]
        private PegaDiagnosticVisualizationLevel selectionVisualizationLevel =
            PegaDiagnosticVisualizationLevel.Summary;

        private readonly RaycastHit[] _initialVisibilityHits =
            new RaycastHit[InitialVisibilityHitBufferSize];

        private RaycastHit[] _visibilityHits;
        private PegaPlayerObjectEntryDiscoverySource _discoverySource;
        private CharacterController _characterController;
        private ScopedLogger _log;
        private PegaPlayerObjectEntryCandidate? _currentSelection;
        private PegaPlayerObjectEntryCandidate? _pendingSwitch;
        private float _pendingSwitchElapsedSeconds;
        private string _lastConfigurationDiagnostic = string.Empty;
        private bool _visibilityBufferSaturationReported;
        private bool _visibilityBufferSaturatedThisUpdate;
        private bool _visibilityQueryPerformedThisUpdate;
        private int _lastPendingSwitchAccumulationFrame = -1;

        /// <summary>A seleção física atual; nula quando não há candidato elegível.</summary>
        public PegaPlayerObjectEntryCandidate? CurrentSelection => _currentSelection;

        /// <summary>O alcance físico máximo da seleção, em metros.</summary>
        public float SelectionRangeMeters => selectionRangeMeters;

        /// <summary>O semiângulo horizontal do volume frontal de seleção, em graus.</summary>
        public float SelectionHorizontalHalfAngleDegrees => selectionHorizontalHalfAngleDegrees;

        /// <summary>O semiângulo vertical do volume frontal de seleção, em graus.</summary>
        public float SelectionVerticalHalfAngleDegrees => selectionVerticalHalfAngleDegrees;

        /// <summary>A margem angular aplicada somente à ocorrência já selecionada, em graus.</summary>
        public float StickyVolumeMarginDegrees => stickyVolumeMarginDegrees;

#if UNITY_EDITOR
        /// <summary>Obtém centro e origem física da avaliação quando o corpo do Player está ativo.</summary>
        public bool TryGetPhysicalSelectionFrame(
            out Vector3 selectionCenter,
            out Vector3 visibilityOrigin)
        {
            PegaPlayerObjectEntryDiscoverySource discoverySource = _discoverySource != null
                ? _discoverySource
                : GetComponent<PegaPlayerObjectEntryDiscoverySource>();
            return TryGetPhysicalSelectionFrame(
                discoverySource,
                out selectionCenter,
                out visibilityOrigin,
                out _);
        }

        public PegaDiagnosticVisualizationLevel SelectionVisualizationLevel =>
            selectionVisualizationLevel;
#endif

        /// <summary>Notifica consumidores quando a ocorrência selecionada muda ou é removida.</summary>
        public event Action<PegaPlayerObjectEntryCandidate?> SelectionChanged;

        /// <summary>Notifica consumidores após avaliar a seleção física atual do Player.</summary>
        internal event Action ContextualEvaluationCompleted;

        private void Awake()
        {
            _discoverySource = GetComponent<PegaPlayerObjectEntryDiscoverySource>();
            _characterController = GetComponent<CharacterController>();
            _visibilityHits = _initialVisibilityHits;
            _log = PegaLogging.CreateScopedLogger<PegaPlayerObjectEntrySelectionSource>(
                LogCategory,
                gameObject.name);

            if (_discoverySource == null)
            {
                _log.Error(
                    $"{nameof(PegaPlayerObjectEntrySelectionSource)} requires {nameof(PegaPlayerObjectEntryDiscoverySource)} on the Player Actor root.",
                    LogFields.Field("actorRoot", gameObject.name));
                enabled = false;
                return;
            }

            if (_characterController == null)
            {
                _log.Error(
                    $"{nameof(PegaPlayerObjectEntrySelectionSource)} requires {nameof(CharacterController)} on the Player Actor root for physical selection.",
                    LogFields.Field("actorRoot", gameObject.name));
                enabled = false;
            }
        }

        private void OnEnable()
        {
            ResetPendingSwitch();
        }

        private void LateUpdate()
        {
            EvaluateSelection();
            ContextualEvaluationCompleted?.Invoke();
        }

        private void EvaluateSelection()
        {
            _visibilityBufferSaturatedThisUpdate = false;
            _visibilityQueryPerformedThisUpdate = false;
            if (!TryValidateConfiguration(out string configurationDiagnostic))
            {
                ReportConfigurationDiagnostic(configurationDiagnostic);
                LogField[] selectionLossFields = CreateCurrentSelectionEvaluationUnavailableFields(
                    "invalid-selection-configuration",
                    configurationDiagnostic);
                ResetPendingSwitch();
                PublishSelection(null, "invalid-selection-configuration", 0f, 0f, selectionLossFields);
                return;
            }

            if (!TryGetPlayerForward(out Vector3 playerForward))
            {
                const string forwardDiagnostic = "Player forward direction is not finite or has no horizontal component.";
                ReportConfigurationDiagnostic(forwardDiagnostic);
                LogField[] selectionLossFields = CreateCurrentSelectionEvaluationUnavailableFields(
                    "player-forward-unavailable",
                    forwardDiagnostic);
                ResetPendingSwitch();
                PublishSelection(null, "player-forward-unavailable", 0f, 0f, selectionLossFields);
                return;
            }

            ClearConfigurationDiagnostic();

            PegaPlayerObjectEntryDiscoverySource discoverySource = _discoverySource;
            if (discoverySource == null || !discoverySource.isActiveAndEnabled)
            {
                LogField[] selectionLossFields = CreateCurrentSelectionEvaluationUnavailableFields(
                    "discovery-source-unavailable",
                    "The discovery source is missing or inactive.");
                ResetPendingSwitch();
                PublishSelection(null, "discovery-source-unavailable", 0f, 0f, selectionLossFields);
                return;
            }

            if (!TryGetPhysicalSelectionFrame(
                    discoverySource,
                    out Vector3 discoveryCenter,
                    out Vector3 visibilityOrigin,
                    out string bodyDiagnostic))
            {
                LogField[] selectionLossFields = CreateCurrentSelectionEvaluationUnavailableFields(
                    "player-physical-body-unavailable",
                    bodyDiagnostic);
                ResetPendingSwitch();
                PublishSelection(null, "player-physical-body-unavailable", 0f, 0f, selectionLossFields);
                return;
            }

            float selectionRangeSquared = selectionRangeMeters * selectionRangeMeters;
            bool foundBestCandidate = false;
            bool foundBestFacingCandidate = false;
            bool foundBestFacingBandCandidate = false;
            bool foundCurrentCandidate = false;
            bool currentCandidateFoundInSnapshot = false;
            bool currentCandidateRevalidationAttempted = false;
            bool currentCandidateEvaluationAttempted = false;
            string currentCandidateFailureReason = string.Empty;
            PegaPlayerObjectEntryDiscoverySource.CandidateCurrentnessDiagnostic currentCandidateRevalidation = default;
            CandidateEvaluationDiagnostics currentCandidateEvaluationDiagnostics = default;
            CandidateEvaluation bestCandidate = default;
            CandidateEvaluation bestFacingCandidate = default;
            CandidateEvaluation bestFacingBandCandidate = default;
            CandidateEvaluation currentCandidate = default;
            IReadOnlyList<PegaPlayerObjectEntryCandidate> candidates = discoverySource.Candidates;

            for (int index = 0; index < candidates.Count; index++)
            {
                PegaPlayerObjectEntryCandidate candidate = candidates[index];
                GameObject occurrence = candidate.Occurrence;
                bool isCurrent = _currentSelection.HasValue &&
                    IsSameOccurrence(candidate, _currentSelection.Value);
                if (isCurrent)
                {
                    currentCandidateFoundInSnapshot = true;
                }

                if (occurrence == null)
                {
                    if (isCurrent)
                    {
                        currentCandidateFailureReason = "selected-occurrence-reference-unavailable";
                    }

                    continue;
                }

                bool candidateIsCurrent;
                if (isCurrent)
                {
                    currentCandidateRevalidationAttempted = true;
                    candidateIsCurrent = discoverySource.IsCurrentCandidate(
                        candidate,
                        out currentCandidateRevalidation);
                    if (!candidateIsCurrent)
                    {
                        currentCandidateFailureReason = currentCandidateRevalidation.FailureReason;
                    }
                }
                else
                {
                    candidateIsCurrent = discoverySource.IsCurrentCandidate(candidate);
                }

                if (!candidateIsCurrent)
                {
                    continue;
                }

                CandidateEvaluationDiagnostics candidateEvaluationDiagnostics = default;
                if (isCurrent)
                {
                    currentCandidateEvaluationAttempted = true;
                }

                if (!TryEvaluateCandidate(
                        candidate,
                        discoveryCenter,
                        visibilityOrigin,
                        playerForward,
                        selectionRangeSquared,
                        isCurrent,
                        out CandidateEvaluation evaluation,
                        out candidateEvaluationDiagnostics))
                {
                    if (isCurrent)
                    {
                        currentCandidateEvaluationDiagnostics = candidateEvaluationDiagnostics;
                        currentCandidateFailureReason = "no-player-volume-sample-passed-all-selection-criteria";
                    }

                    continue;
                }

                if (isCurrent)
                {
                    currentCandidateEvaluationDiagnostics = candidateEvaluationDiagnostics;
                }

                if (isCurrent)
                {
                    foundCurrentCandidate = true;
                    currentCandidate = evaluation;
                }

                if (evaluation.PlayerFacingAlignment >= facingIntentAlignmentThreshold &&
                    (!foundBestFacingCandidate ||
                     IsMoreFacingIntent(evaluation, bestFacingCandidate)))
                {
                    foundBestFacingCandidate = true;
                    bestFacingCandidate = evaluation;
                }

                if (evaluation.PlayerFacingAlignment >= facingIntentAlignmentThreshold &&
                    (!foundBestFacingBandCandidate ||
                     IsMoreRelevant(evaluation, bestFacingBandCandidate)))
                {
                    foundBestFacingBandCandidate = true;
                    bestFacingBandCandidate = evaluation;
                }

                if (!foundBestCandidate || IsMoreRelevant(evaluation, bestCandidate))
                {
                    foundBestCandidate = true;
                    bestCandidate = evaluation;
                }
            }

            FlushVisibilityBufferDiagnostic();
            LogField[] currentSelectionLossFields = _currentSelection.HasValue && !foundCurrentCandidate
                ? CreateCurrentSelectionEligibilityLossFields(
                    _currentSelection.Value,
                    currentCandidateFoundInSnapshot,
                    currentCandidateRevalidationAttempted,
                    currentCandidateRevalidation,
                    currentCandidateEvaluationAttempted,
                    currentCandidateEvaluationDiagnostics,
                    currentCandidateFailureReason,
                    discoveryCenter,
                    visibilityOrigin,
                    playerForward)
                : null;

            if (!foundBestCandidate)
            {
                ResetPendingSwitch();
                PublishSelection(
                    null,
                    _currentSelection.HasValue
                        ? "previous-selection-no-longer-visible-or-eligible"
                        : "no-contextual-candidate",
                    0f,
                    0f,
                    currentSelectionLossFields);
                return;
            }

            if (!_currentSelection.HasValue || !foundCurrentCandidate)
            {
                ResetPendingSwitch();
                PublishSelection(
                    bestCandidate.Candidate,
                    _currentSelection.HasValue
                        ? "previous-selection-no-longer-visible-or-eligible"
                        : "best-contextual-candidate",
                    bestCandidate.DistanceMeters,
                    bestCandidate.Score,
                    currentSelectionLossFields);
                return;
            }

            CandidateEvaluation switchCandidate = bestCandidate;
            bool isPlayerFacingIntentSwitch = false;
            bool isFacingBandScoreSwitch = false;
            bool hasDirectPlayerForwardSwitch = bestCandidate.IsDirectlyAimed &&
                !currentCandidate.IsDirectlyAimed &&
                !IsSameOccurrence(bestCandidate.Candidate, currentCandidate.Candidate);

            if (!hasDirectPlayerForwardSwitch && currentCandidate.IsDirectlyAimed)
            {
                ResetPendingSwitch();
                PublishSelection(
                    currentCandidate.Candidate,
                    "current-selection-on-player-forward-axis",
                    currentCandidate.DistanceMeters,
                    currentCandidate.Score);
                return;
            }

            if (!hasDirectPlayerForwardSwitch &&
                currentCandidate.PlayerFacingAlignment >= facingIntentAlignmentThreshold)
            {
                if (!foundBestFacingBandCandidate ||
                    IsSameOccurrence(bestFacingBandCandidate.Candidate, currentCandidate.Candidate) ||
                    bestFacingBandCandidate.Score + switchScoreMargin >= currentCandidate.Score)
                {
                    ResetPendingSwitch();
                    PublishSelection(
                        currentCandidate.Candidate,
                        "current-selection-matches-player-facing-intent",
                        currentCandidate.DistanceMeters,
                        currentCandidate.Score);
                    return;
                }

                switchCandidate = bestFacingBandCandidate;
                isFacingBandScoreSwitch = true;
            }

            if (!hasDirectPlayerForwardSwitch && !isFacingBandScoreSwitch &&
                foundBestFacingCandidate &&
                !IsSameOccurrence(bestFacingCandidate.Candidate, currentCandidate.Candidate) &&
                bestFacingCandidate.PlayerFacingAlignment - currentCandidate.PlayerFacingAlignment >= facingIntentAdvantage)
            {
                switchCandidate = bestFacingCandidate;
                isPlayerFacingIntentSwitch = true;
            }

            if (IsSameOccurrence(switchCandidate.Candidate, currentCandidate.Candidate))
            {
                ResetPendingSwitch();
                PublishSelection(
                    currentCandidate.Candidate,
                    "current-selection-remains-most-relevant",
                    currentCandidate.DistanceMeters,
                    currentCandidate.Score);
                return;
            }

            if (!hasDirectPlayerForwardSwitch && !isPlayerFacingIntentSwitch &&
                !isFacingBandScoreSwitch &&
                switchCandidate.Score + switchScoreMargin >= currentCandidate.Score)
            {
                ResetPendingSwitch();
                PublishSelection(
                    currentCandidate.Candidate,
                    "current-selection-sticky",
                    currentCandidate.DistanceMeters,
                    currentCandidate.Score);
                return;
            }

            AccumulatePendingSwitch(switchCandidate.Candidate);
            if (_pendingSwitchElapsedSeconds >= switchConfirmationSeconds)
            {
                ResetPendingSwitch();
                PublishSelection(
                    switchCandidate.Candidate,
                    isPlayerFacingIntentSwitch
                        ? "player-facing-intent-confirmed"
                        : hasDirectPlayerForwardSwitch
                            ? "direct-player-forward-focus-confirmed"
                            : isFacingBandScoreSwitch
                                ? "clearer-focus-within-player-facing-band-confirmed"
                                : "clearer-contextual-focus-confirmed",
                    switchCandidate.DistanceMeters,
                    switchCandidate.Score);
                return;
            }

            PublishSelection(
                currentCandidate.Candidate,
                "waiting-for-clearer-focus",
                currentCandidate.DistanceMeters,
                currentCandidate.Score);
        }

        private void OnDisable()
        {
            ResetPendingSwitch();
            LogField[] selectionLossFields = CreateCurrentSelectionEvaluationUnavailableFields(
                "selection-source-disabled",
                "The contextual selection source was disabled before another evaluation could run.");
            PublishSelection(null, "selection-source-disabled", 0f, 0f, selectionLossFields);
        }

        private LogField[] CreateCurrentSelectionEvaluationUnavailableFields(string reason, string diagnostic)
        {
            if (!_currentSelection.HasValue)
            {
                return null;
            }

            PegaPlayerObjectEntryCandidate candidate = _currentSelection.Value;
            bool snapshotAvailable = TryGetDiscoverySnapshotMembership(candidate, out bool snapshotContainsCandidate);
            List<LogField> fields = CreateSelectionLossFields(
                candidate,
                "selection-evaluation-context-unavailable",
                reason,
                snapshotAvailable,
                snapshotContainsCandidate);
            fields.Add(LogFields.Field("recognitionRevalidationResult", "not-performed"));
            fields.Add(LogFields.Field("recognitionRevalidationSkipReason", reason));
            if (!string.IsNullOrWhiteSpace(diagnostic))
            {
                fields.Add(LogFields.Field("evaluationContextDiagnostic", diagnostic));
            }

            return fields.ToArray();
        }

        private LogField[] CreateCurrentSelectionEligibilityLossFields(
            PegaPlayerObjectEntryCandidate candidate,
            bool candidateFoundInSnapshot,
            bool currentnessRevalidationAttempted,
            PegaPlayerObjectEntryDiscoverySource.CandidateCurrentnessDiagnostic currentness,
            bool candidateEvaluationAttempted,
            CandidateEvaluationDiagnostics evaluation,
            string failureReason,
            Vector3 discoveryCenter,
            Vector3 visibilityOrigin,
            Vector3 playerForward)
        {
            string lossClassification;
            if (!candidateFoundInSnapshot)
            {
                lossClassification = "discovery-snapshot-absent";
                failureReason = "selected-occurrence-not-present-in-discovery-snapshot";
            }
            else if (!currentnessRevalidationAttempted)
            {
                lossClassification = "candidate-reference-unavailable-before-revalidation";
            }
            else if (!currentness.IsCurrent)
            {
                lossClassification = ClassifyCurrentnessFailure(currentness);
                failureReason = currentness.FailureReason;
            }
            else if (candidateEvaluationAttempted && evaluation.RecognizedColliderCount == 0)
            {
                lossClassification = evaluation.RecognitionColliderRejectionCount > 0
                    ? "recognition-revalidation-rejected-during-evaluation"
                    : "no-recognized-collider-reached-contextual-evaluation";
            }
            else if (evaluation.CandidateColliderRevalidationBlockCount > 0 ||
                     evaluation.RecognitionColliderRejectionCount > 0)
            {
                lossClassification = "contextual-evaluation-with-recognition-rejections";
            }
            else
            {
                lossClassification = "player-volume-geometry-or-visibility-rejected";
            }

            List<LogField> fields = CreateSelectionLossFields(
                candidate,
                lossClassification,
                failureReason,
                snapshotAvailable: true,
                snapshotContainsCandidate: candidateFoundInSnapshot);
            fields.Add(LogFields.Field(
                "recognitionRevalidationResult",
                !currentnessRevalidationAttempted
                    ? "not-performed"
                    : currentness.IsCurrent
                        ? "passed"
                        : "rejected"));
            fields.Add(LogFields.Field(
                "recognitionQueryExecuted",
                currentnessRevalidationAttempted && currentness.RecognitionQueryExecuted));
            fields.Add(LogFields.Field(
                "recognitionQueryStatus",
                currentness.RecognitionStatus.HasValue
                    ? currentness.RecognitionStatus.Value.ToString()
                    : "not-run"));
            fields.Add(LogFields.Field("recognitionFailureReason", currentness.FailureReason));
            fields.Add(LogFields.Field("recognitionDiagnostic", currentness.FrameworkDiagnostic));
            fields.Add(LogFields.Field("recognitionFailureQueryExecuted", currentness.FailureRecognitionQueryExecuted));
            fields.Add(LogFields.Field("recognitionFailureCollider", currentness.FailureColliderName));
            fields.Add(LogFields.Field("recognitionQueryCollider", currentness.RecognitionQueryColliderName));
            fields.Add(LogFields.Field(
                "recognitionResultScope",
                currentness.IsCurrent
                    ? "matching-recognition-collider"
                    : currentness.RecognitionQueryExecuted && !currentness.FailureRecognitionQueryExecuted
                        ? "latest-query-from-another-rejected-collider"
                        : currentness.RejectedRecognitionColliderCount > 1
                        ? "last-rejected-recognition-collider"
                        : currentness.RejectedRecognitionColliderCount == 1
                            ? "rejected-recognition-collider"
                            : "candidate-level-check"));
            fields.Add(LogFields.Field(
                "recognitionRejectedColliderCount",
                currentness.RejectedRecognitionColliderCount));
            fields.Add(LogFields.Field("candidateEvaluationAttempted", candidateEvaluationAttempted));
            fields.Add(LogFields.Field("selectionCenterX", discoveryCenter.x));
            fields.Add(LogFields.Field("selectionCenterY", discoveryCenter.y));
            fields.Add(LogFields.Field("selectionCenterZ", discoveryCenter.z));
            fields.Add(LogFields.Field("visibilityOriginX", visibilityOrigin.x));
            fields.Add(LogFields.Field("visibilityOriginY", visibilityOrigin.y));
            fields.Add(LogFields.Field("visibilityOriginZ", visibilityOrigin.z));
            fields.Add(LogFields.Field("playerForwardX", playerForward.x));
            fields.Add(LogFields.Field("playerForwardY", playerForward.y));
            fields.Add(LogFields.Field("playerForwardZ", playerForward.z));
            AddEvaluationDiagnosticFields(fields, evaluation);

            return fields.ToArray();
        }

        private List<LogField> CreateSelectionLossFields(
            PegaPlayerObjectEntryCandidate candidate,
            string lossClassification,
            string failureReason,
            bool snapshotAvailable,
            bool snapshotContainsCandidate)
        {
            return new List<LogField>(28)
            {
                LogFields.Field("lostObjectEntryId", candidate.ObjectEntryId.StableText),
                LogFields.Field("lostOccurrenceId", candidate.OccurrenceId.ToString()),
                LogFields.Field("lostPhysicalRoot", candidate.PhysicalRootName),
                LogFields.Field("lossClassification", lossClassification),
                LogFields.Field("failureReason", failureReason ?? string.Empty),
                LogFields.Field("discoverySnapshotAvailable", snapshotAvailable),
                LogFields.Field("discoverySnapshotContainsOccurrence", snapshotContainsCandidate),
                LogFields.Field(
                    "discoverySourceActiveAndEnabled",
                    _discoverySource != null && _discoverySource.isActiveAndEnabled),
                LogFields.Field("selectionRangeMeters", selectionRangeMeters),
                LogFields.Field("selectionHorizontalHalfAngleDegrees", selectionHorizontalHalfAngleDegrees),
                LogFields.Field("selectionVerticalHalfAngleDegrees", selectionVerticalHalfAngleDegrees),
                LogFields.Field("stickyVolumeMarginDegrees", stickyVolumeMarginDegrees),
                LogFields.Field("physicsVisibilityLayerMask", Physics.DefaultRaycastLayers),
                LogFields.Field("physicsVisibilityTriggers", QueryTriggerInteraction.Ignore.ToString()),
                LogFields.Field("visibilityHitBufferCapacity", _visibilityHits == null ? 0 : _visibilityHits.Length)
            };
        }

        private bool TryGetDiscoverySnapshotMembership(
            PegaPlayerObjectEntryCandidate candidate,
            out bool containsCandidate)
        {
            containsCandidate = false;
            PegaPlayerObjectEntryDiscoverySource discoverySource = _discoverySource;
            if (discoverySource == null)
            {
                return false;
            }

            IReadOnlyList<PegaPlayerObjectEntryCandidate> candidates = discoverySource.Candidates;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (IsSameOccurrence(candidates[index], candidate))
                {
                    containsCandidate = true;
                    break;
                }
            }

            return true;
        }

        private static string ClassifyCurrentnessFailure(
            PegaPlayerObjectEntryDiscoverySource.CandidateCurrentnessDiagnostic currentness)
        {
            if (currentness.FailureRecognitionQueryExecuted &&
                currentness.FailureReason == "framework-recognition-rejected-collider")
            {
                return "framework-recognition-rejected";
            }

            string failureReason = currentness.FailureReason;
            if (failureReason == "occurrence-destroyed-or-unavailable" ||
                failureReason == "occurrence-inactive" ||
                failureReason == "candidate-has-no-recognition-colliders" ||
                failureReason == "recognition-collider-destroyed-or-unavailable" ||
                failureReason == "recognition-collider-inactive" ||
                failureReason == "occurrence-destroyed-inactive-or-unavailable" ||
                failureReason == "recognition-collider-not-in-candidate-snapshot" ||
                failureReason == "recognized-object-entry-identity-changed" ||
                failureReason == "recognized-occurrence-reference-changed")
            {
                return "physical-occurrence-or-collider-currentness-rejected";
            }

            return "discovery-or-player-access-currentness-rejected";
        }

        private static void AddEvaluationDiagnosticFields(
            List<LogField> fields,
            CandidateEvaluationDiagnostics diagnostics)
        {
            fields.Add(LogFields.Field("rejectionCriteria", diagnostics.BuildRejectionCriteria()));
            fields.Add(LogFields.Field("playerVolumeRaySampleCount", diagnostics.PlayerVolumeRaySampleCount));
            fields.Add(LogFields.Field("recognizedColliderCountDuringEvaluation", diagnostics.RecognizedColliderCount));
            fields.Add(LogFields.Field("recognitionColliderRejectionCountDuringEvaluation", diagnostics.RecognitionColliderRejectionCount));
            fields.Add(LogFields.Field("candidateColliderRayMissCount", diagnostics.CandidateColliderRayMissCount));
            fields.Add(LogFields.Field("invalidTargetPointCount", diagnostics.InvalidTargetPointCount));
            fields.Add(LogFields.Field("invalidTargetDistanceCount", diagnostics.InvalidTargetDistanceCount));
            fields.Add(LogFields.Field("outOfRangeSampleCount", diagnostics.OutOfRangeCount));
            fields.Add(LogFields.Field("behindPlayerSampleCount", diagnostics.BehindPlayerCount));
            fields.Add(LogFields.Field("outsideHorizontalSelectionVolumeCount", diagnostics.OutsideHorizontalVolumeCount));
            fields.Add(LogFields.Field("outsideVerticalSelectionVolumeCount", diagnostics.OutsideVerticalVolumeCount));
            fields.Add(LogFields.Field("invalidVisibilityRayCount", diagnostics.InvalidVisibilityRayCount));
            fields.Add(LogFields.Field("visibilityBlockedByColliderSampleCount", diagnostics.OccludedRayCount));
            fields.Add(LogFields.Field("candidateColliderRevalidationBlockCount", diagnostics.CandidateColliderRevalidationBlockCount));
            fields.Add(LogFields.Field("visibilityHitBufferSaturatedSampleCount", diagnostics.VisibilityBufferSaturatedCount));

            if (diagnostics.HasFailedTargetPoint)
            {
                fields.Add(LogFields.Field("lastRejectedTargetPointX", diagnostics.LastFailedTargetPoint.x));
                fields.Add(LogFields.Field("lastRejectedTargetPointY", diagnostics.LastFailedTargetPoint.y));
                fields.Add(LogFields.Field("lastRejectedTargetPointZ", diagnostics.LastFailedTargetPoint.z));
            }

            if (diagnostics.HasTargetDistance)
            {
                fields.Add(LogFields.Field(
                    "minimumEvaluatedTargetDistanceMeters",
                    Mathf.Sqrt(diagnostics.MinimumTargetDistanceSquared)));
            }

            if (diagnostics.HasSelectionAngles)
            {
                fields.Add(LogFields.Field("minimumHorizontalSelectionAngleDegrees", diagnostics.MinimumHorizontalAngleDegrees));
                fields.Add(LogFields.Field("maximumHorizontalSelectionAngleDegrees", diagnostics.MaximumHorizontalAngleDegrees));
                fields.Add(LogFields.Field("minimumVerticalSelectionAngleDegrees", diagnostics.MinimumVerticalAngleDegrees));
                fields.Add(LogFields.Field("maximumVerticalSelectionAngleDegrees", diagnostics.MaximumVerticalAngleDegrees));
            }

            if (diagnostics.HasPlayerFacingDot)
            {
                fields.Add(LogFields.Field("maximumPlayerFacingDot", diagnostics.MaximumPlayerFacingDot));
            }

            if (!string.IsNullOrEmpty(diagnostics.NearestOccludingColliderName))
            {
                fields.Add(LogFields.Field("nearestVisibilityBlocker", diagnostics.NearestOccludingColliderName));
                fields.Add(LogFields.Field("nearestVisibilityBlockerEntityId", diagnostics.NearestOccludingColliderEntityId));
                fields.Add(LogFields.Field("nearestVisibilityBlockerDistanceMeters", diagnostics.NearestOccludingDistanceMeters));
            }

            if (!string.IsNullOrEmpty(diagnostics.LastRecognitionFailureReason))
            {
                fields.Add(LogFields.Field("evaluationRecognitionFailureReason", diagnostics.LastRecognitionFailureReason));
                fields.Add(LogFields.Field("evaluationRecognitionFailureQueryExecuted", diagnostics.LastRecognitionFailureQueryExecuted));
                fields.Add(LogFields.Field("evaluationRecognitionFailureStatus", diagnostics.LastRecognitionStatus));
                fields.Add(LogFields.Field("evaluationRecognitionDiagnostic", diagnostics.LastRecognitionDiagnostic));
                fields.Add(LogFields.Field("evaluationRecognitionCollider", diagnostics.LastRecognitionColliderName));
                fields.Add(LogFields.Field("evaluationRecognitionQueryCount", diagnostics.RecognitionQueryCount));
                fields.Add(LogFields.Field("evaluationLatestRecognitionQueryStatus", diagnostics.LastRecognitionQueryStatus));
                fields.Add(LogFields.Field("evaluationLatestRecognitionQueryDiagnostic", diagnostics.LastRecognitionQueryDiagnostic));
                fields.Add(LogFields.Field("evaluationLatestRecognitionQueryCollider", diagnostics.LastRecognitionQueryColliderName));
            }

            if (!string.IsNullOrEmpty(diagnostics.CandidateColliderRevalidationFailureReason))
            {
                fields.Add(LogFields.Field(
                    "candidateColliderRevalidationFailureReason",
                    diagnostics.CandidateColliderRevalidationFailureReason));
                fields.Add(LogFields.Field(
                    "candidateColliderRevalidationFailureQueryExecuted",
                    diagnostics.CandidateColliderRevalidationFailureQueryExecuted));
                fields.Add(LogFields.Field(
                    "candidateColliderRevalidationFailureStatus",
                    diagnostics.CandidateColliderRevalidationStatus));
                fields.Add(LogFields.Field(
                    "candidateColliderRevalidationDiagnostic",
                    diagnostics.CandidateColliderRevalidationDiagnostic));
                fields.Add(LogFields.Field(
                    "candidateColliderRevalidationQueryCount",
                    diagnostics.CandidateColliderRevalidationQueryCount));
                fields.Add(LogFields.Field(
                    "candidateColliderLatestQueryStatus",
                    diagnostics.CandidateColliderLatestQueryStatus));
                fields.Add(LogFields.Field(
                    "candidateColliderLatestQueryDiagnostic",
                    diagnostics.CandidateColliderLatestQueryDiagnostic));
                fields.Add(LogFields.Field(
                    "candidateColliderLatestQueryCollider",
                    diagnostics.CandidateColliderLatestQueryColliderName));
            }
        }

        private bool TryValidateConfiguration(out string diagnostic)
        {
            if (!IsNonNegativeFinite(selectionRangeMeters))
            {
                diagnostic = "Selection range must be finite and non-negative.";
                return false;
            }

            if (!IsNonNegativeFinite(selectionHorizontalHalfAngleDegrees) ||
                selectionHorizontalHalfAngleDegrees > 90f)
            {
                diagnostic = "Horizontal selection half-angle must be finite and within [0, 90] degrees.";
                return false;
            }

            if (!IsNonNegativeFinite(selectionVerticalHalfAngleDegrees) ||
                selectionVerticalHalfAngleDegrees > 90f)
            {
                diagnostic = "Vertical selection half-angle must be finite and within [0, 90] degrees.";
                return false;
            }

            if (!IsNonNegativeFinite(stickyVolumeMarginDegrees) ||
                stickyVolumeMarginDegrees > 90f ||
                selectionHorizontalHalfAngleDegrees + stickyVolumeMarginDegrees > 90f ||
                selectionVerticalHalfAngleDegrees + stickyVolumeMarginDegrees > 90f)
            {
                diagnostic = "Sticky volume margin must be finite and keep both selection half-angles within 90 degrees.";
                return false;
            }

            if (!IsFinite(proximityWeight) || proximityWeight < 0f || proximityWeight > 1f ||
                !IsFinite(playerFacingWeight) || playerFacingWeight < 0f || playerFacingWeight > 1f ||
                proximityWeight + playerFacingWeight > 1f)
            {
                diagnostic = "Proximity and Player-facing weights must be finite within [0, 1] and their sum must not exceed 1.";
                return false;
            }

            if (!IsFinite(facingIntentAlignmentThreshold) ||
                facingIntentAlignmentThreshold < 0f || facingIntentAlignmentThreshold > 1f)
            {
                diagnostic = "Facing intent alignment threshold must be finite and within [0, 1].";
                return false;
            }

            if (!IsFinite(facingIntentAdvantage) ||
                facingIntentAdvantage < 0f || facingIntentAdvantage > 1f)
            {
                diagnostic = "Facing intent advantage must be finite and within [0, 1].";
                return false;
            }

            if (!IsFinite(switchScoreMargin) || switchScoreMargin < 0f || switchScoreMargin > 1f)
            {
                diagnostic = "Switch score margin must be finite and within [0, 1].";
                return false;
            }

            if (!IsNonNegativeFinite(switchConfirmationSeconds))
            {
                diagnostic = "Switch confirmation duration must be finite and non-negative.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryGetPlayerForward(out Vector3 playerForward)
        {
            playerForward = transform.forward;
            playerForward.y = 0f;
            if (!IsFinite(playerForward) || playerForward.sqrMagnitude <= 0f)
            {
                playerForward = default;
                return false;
            }

            playerForward.Normalize();
            return true;
        }

        private bool TryGetPhysicalSelectionFrame(
            PegaPlayerObjectEntryDiscoverySource discoverySource,
            out Vector3 selectionCenter,
            out Vector3 visibilityOrigin,
            out string diagnostic)
        {
            selectionCenter = default;
            visibilityOrigin = default;
            CharacterController controller = _characterController != null
                ? _characterController
                : GetComponent<CharacterController>();
            if (controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
            {
                diagnostic = "The Player CharacterController is missing, disabled or inactive.";
                return false;
            }

            Bounds bodyBounds = controller.bounds;
            if (!IsFinite(bodyBounds.center) ||
                !IsFinite(bodyBounds.extents) ||
                bodyBounds.extents.sqrMagnitude <= 0f ||
                !IsPositiveFinite(controller.height) ||
                !IsPositiveFinite(controller.radius))
            {
                diagnostic = "The Player CharacterController has no usable physical bounds.";
                return false;
            }

            selectionCenter = discoverySource != null
                ? discoverySource.DiscoveryCenter
                : bodyBounds.center;
            visibilityOrigin = bodyBounds.center + Vector3.up * (bodyBounds.extents.y * 0.75f);
            if (!IsFinite(selectionCenter) || !IsFinite(visibilityOrigin))
            {
                selectionCenter = default;
                visibilityOrigin = default;
                diagnostic = "The current Player physical selection frame is not finite.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryEvaluateCandidate(
            PegaPlayerObjectEntryCandidate candidate,
            Vector3 playerCenter,
            Vector3 visibilityOrigin,
            Vector3 playerForward,
            float selectionRangeSquared,
            bool isCurrent,
            out CandidateEvaluation bestEvaluation,
            out CandidateEvaluationDiagnostics diagnostics)
        {
            bestEvaluation = default;
            diagnostics = default;

            bool foundEvaluation = false;
            IReadOnlyList<Collider> recognitionColliders = candidate.RecognitionColliders;
            for (int colliderIndex = 0;
                 recognitionColliders != null && colliderIndex < recognitionColliders.Count;
                 colliderIndex++)
            {
                Collider candidateCollider = recognitionColliders[colliderIndex];
                PegaPlayerObjectEntryDiscoverySource.CandidateCurrentnessDiagnostic colliderCurrentness = default;
                bool isCurrentRecognitionCollider;
                if (isCurrent)
                {
                    isCurrentRecognitionCollider = _discoverySource.IsCurrentRecognitionCollider(
                        candidate,
                        candidateCollider,
                        out colliderCurrentness);
                }
                else
                {
                    isCurrentRecognitionCollider = _discoverySource.IsCurrentRecognitionCollider(
                        candidate,
                        candidateCollider);
                }

                if (!isCurrentRecognitionCollider)
                {
                    if (isCurrent)
                    {
                        diagnostics.RecordRecognitionColliderRejection(colliderCurrentness);
                    }

                    continue;
                }

                if (isCurrent)
                {
                    diagnostics.RecognizedColliderCount++;
                }

                float maximumDirectRayDistance = selectionRangeMeters +
                    Vector3.Distance(visibilityOrigin, playerCenter);
                Vector3 directRayEnd = visibilityOrigin + playerForward * maximumDirectRayDistance;
                if (TryEvaluatePlayerVolumeRay(
                        candidate,
                        candidateCollider,
                        directRayEnd,
                        visibilityOrigin,
                        playerCenter,
                        playerForward,
                        selectionRangeSquared,
                        isCurrent,
                        isDirectlyAimed: true,
                        out CandidateEvaluation directEvaluation,
                        ref diagnostics) &&
                    (!foundEvaluation || IsMoreRelevant(directEvaluation, bestEvaluation)))
                {
                    foundEvaluation = true;
                    bestEvaluation = directEvaluation;
                }

                if (!TryGetPlayerLocalBounds(candidateCollider.bounds, out Vector3 localBoundsMinimum, out Vector3 localBoundsMaximum))
                {
                    if (isCurrent)
                    {
                        diagnostics.InvalidTargetPointCount++;
                    }

                    continue;
                }

                for (int zIndex = 0; zIndex < PlayerVolumeGridSize; zIndex++)
                {
                    float localZ = GridCoordinate(
                        zIndex,
                        PlayerVolumeGridSize,
                        localBoundsMinimum.z,
                        localBoundsMaximum.z);
                    for (int yIndex = 0; yIndex < PlayerVolumeGridSize; yIndex++)
                    {
                        float localY = GridCoordinate(
                            yIndex,
                            PlayerVolumeGridSize,
                            localBoundsMinimum.y,
                            localBoundsMaximum.y);
                        for (int xIndex = 0; xIndex < PlayerVolumeGridSize; xIndex++)
                        {
                            float localX = GridCoordinate(
                                xIndex,
                                PlayerVolumeGridSize,
                                localBoundsMinimum.x,
                                localBoundsMaximum.x);
                            Vector3 samplePoint = transform.TransformPoint(new Vector3(localX, localY, localZ));
                            if (!TryEvaluatePlayerVolumeRay(
                                    candidate,
                                    candidateCollider,
                                    samplePoint,
                                    visibilityOrigin,
                                    playerCenter,
                                    playerForward,
                                    selectionRangeSquared,
                                    isCurrent,
                                    isDirectlyAimed: false,
                                    out CandidateEvaluation evaluation,
                                    ref diagnostics))
                            {
                                continue;
                            }

                            if (!foundEvaluation || IsMoreRelevant(evaluation, bestEvaluation))
                            {
                                foundEvaluation = true;
                                bestEvaluation = evaluation;
                            }
                        }
                    }
                }
            }

            return foundEvaluation;
        }

        private bool TryGetPlayerLocalBounds(
            Bounds worldBounds,
            out Vector3 localMinimum,
            out Vector3 localMaximum)
        {
            Vector3 boundsMinimum = worldBounds.min;
            Vector3 boundsMaximum = worldBounds.max;
            localMinimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            localMaximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldPoint = new Vector3(
                    (corner & 1) == 0 ? boundsMinimum.x : boundsMaximum.x,
                    (corner & 2) == 0 ? boundsMinimum.y : boundsMaximum.y,
                    (corner & 4) == 0 ? boundsMinimum.z : boundsMaximum.z);
                Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
                if (!IsFinite(localPoint))
                {
                    localMinimum = default;
                    localMaximum = default;
                    return false;
                }

                localMinimum = Vector3.Min(localMinimum, localPoint);
                localMaximum = Vector3.Max(localMaximum, localPoint);
            }

            if (!IsFinite(localMinimum) || !IsFinite(localMaximum))
            {
                localMinimum = default;
                localMaximum = default;
                return false;
            }

            return localMinimum.x <= localMaximum.x &&
                   localMinimum.y <= localMaximum.y &&
                   localMinimum.z <= localMaximum.z;
        }

        private bool TryEvaluatePlayerVolumeRay(
            PegaPlayerObjectEntryCandidate candidate,
            Collider candidateCollider,
            Vector3 samplePoint,
            Vector3 visibilityOrigin,
            Vector3 playerCenter,
            Vector3 playerForward,
            float selectionRangeSquared,
            bool isCurrent,
            bool isDirectlyAimed,
            out CandidateEvaluation evaluation,
            ref CandidateEvaluationDiagnostics diagnostics)
        {
            evaluation = default;
            if (isCurrent)
            {
                diagnostics.PlayerVolumeRaySampleCount++;
            }

            Vector3 toSample = samplePoint - visibilityOrigin;
            float sampleDistance = toSample.magnitude;
            if (!IsFinite(samplePoint) || !IsFinite(sampleDistance) || sampleDistance <= 0f)
            {
                if (isCurrent)
                {
                    diagnostics.InvalidTargetPointCount++;
                    diagnostics.RecordFailedTargetPoint(samplePoint);
                }

                return false;
            }

            Ray playerVolumeRay = new Ray(visibilityOrigin, toSample / sampleDistance);
            if (!candidateCollider.Raycast(playerVolumeRay, out RaycastHit targetHit, sampleDistance))
            {
                if (isCurrent)
                {
                    diagnostics.CandidateColliderRayMissCount++;
                    diagnostics.RecordFailedTargetPoint(samplePoint);
                }

                return false;
            }

            if (!IsFinite(targetHit.point))
            {
                if (isCurrent)
                {
                    diagnostics.InvalidTargetPointCount++;
                    diagnostics.RecordFailedTargetPoint(targetHit.point);
                }

                return false;
            }

            Vector3 toTarget = targetHit.point - playerCenter;
            float distanceSquared = toTarget.sqrMagnitude;
            if (!IsFinite(distanceSquared))
            {
                if (isCurrent)
                {
                    diagnostics.InvalidTargetDistanceCount++;
                    diagnostics.RecordFailedTargetPoint(targetHit.point);
                }

                return false;
            }

            if (isCurrent)
            {
                diagnostics.RecordTargetDistance(distanceSquared);
            }

            if (distanceSquared > selectionRangeSquared)
            {
                if (isCurrent)
                {
                    diagnostics.OutOfRangeCount++;
                    diagnostics.RecordFailedTargetPoint(targetHit.point);
                }

                return false;
            }

            Vector3 planarToTarget = toTarget;
            planarToTarget.y = 0f;
            float planarDistanceSquared = planarToTarget.sqrMagnitude;
            float playerFacingDot = planarDistanceSquared > 0f
                ? Vector3.Dot(playerForward, planarToTarget / Mathf.Sqrt(planarDistanceSquared))
                : 1f;
            if (isCurrent)
            {
                diagnostics.RecordPlayerFacingDot(playerFacingDot);
            }

            if (planarDistanceSquared > 0f && playerFacingDot < 0f)
            {
                if (isCurrent)
                {
                    diagnostics.BehindPlayerCount++;
                    diagnostics.RecordFailedTargetPoint(targetHit.point);
                }

                return false;
            }

            float playerFacingAlignment = planarDistanceSquared > 0f
                ? Mathf.Clamp01(Vector3.Dot(
                    playerForward,
                    planarToTarget / Mathf.Sqrt(planarDistanceSquared)))
                : 1f;

            float horizontalAngleDegrees = planarDistanceSquared > 0f
                ? Vector3.Angle(playerForward, planarToTarget)
                : 0f;
            float planarDistance = Mathf.Sqrt(planarDistanceSquared);
            float verticalAngleDegrees = Mathf.Atan2(
                Mathf.Abs(toTarget.y),
                planarDistance) * Mathf.Rad2Deg;
            if (!IsFinite(horizontalAngleDegrees) || !IsFinite(verticalAngleDegrees))
            {
                if (isCurrent)
                {
                    diagnostics.InvalidTargetDistanceCount++;
                    diagnostics.RecordFailedTargetPoint(targetHit.point);
                }

                return false;
            }

            if (isCurrent)
            {
                diagnostics.RecordSelectionAngles(horizontalAngleDegrees, verticalAngleDegrees);
            }

            float maximumHorizontalAngle = selectionHorizontalHalfAngleDegrees +
                (isCurrent ? stickyVolumeMarginDegrees : 0f);
            float maximumVerticalAngle = selectionVerticalHalfAngleDegrees +
                (isCurrent ? stickyVolumeMarginDegrees : 0f);
            bool outsideHorizontalVolume = horizontalAngleDegrees > maximumHorizontalAngle;
            bool outsideVerticalVolume = verticalAngleDegrees > maximumVerticalAngle;
            if (outsideHorizontalVolume || outsideVerticalVolume)
            {
                if (isCurrent)
                {
                    if (outsideHorizontalVolume)
                    {
                        diagnostics.OutsideHorizontalVolumeCount++;
                    }

                    if (outsideVerticalVolume)
                    {
                        diagnostics.OutsideVerticalVolumeCount++;
                    }

                    diagnostics.RecordFailedTargetPoint(targetHit.point);
                }

                return false;
            }

            float distanceMeters = Mathf.Sqrt(distanceSquared);
            if (!HasClearLineOfSight(
                    candidate,
                    playerVolumeRay,
                    targetHit.distance,
                    out VisibilityRejectionDiagnostic visibilityRejection))
            {
                if (isCurrent)
                {
                    diagnostics.RecordVisibilityRejection(visibilityRejection);
                    diagnostics.RecordFailedTargetPoint(targetHit.point);
                }

                return false;
            }

            float normalizedHorizontalAngle = NormalizeSelectionAngle(
                horizontalAngleDegrees,
                selectionHorizontalHalfAngleDegrees);
            float normalizedVerticalAngle = NormalizeSelectionAngle(
                verticalAngleDegrees,
                selectionVerticalHalfAngleDegrees);
            float normalizedVolumeDistance = Mathf.Max(
                normalizedHorizontalAngle,
                normalizedVerticalAngle);
            float normalizedRangeDistance = selectionRangeMeters > 0f
                ? Mathf.Clamp01(distanceMeters / selectionRangeMeters)
                : 0f;
            float volumeWeight = 1f - proximityWeight - playerFacingWeight;
            float score =
                volumeWeight * normalizedVolumeDistance +
                proximityWeight * normalizedRangeDistance +
                playerFacingWeight * (1f - playerFacingAlignment);
            evaluation = new CandidateEvaluation(
                candidate,
                score,
                distanceMeters,
                playerFacingAlignment,
                isDirectlyAimed);
            return true;
        }

        private static float NormalizeSelectionAngle(float angleDegrees, float halfAngleDegrees) =>
            halfAngleDegrees > 0f
                ? Mathf.Clamp01(angleDegrees / halfAngleDegrees)
                : angleDegrees <= 0f ? 0f : 1f;

        private static bool IsMoreRelevant(
            CandidateEvaluation candidate,
            CandidateEvaluation currentBest)
        {
            if (candidate.IsDirectlyAimed != currentBest.IsDirectlyAimed)
            {
                return candidate.IsDirectlyAimed;
            }

            return candidate.Score < currentBest.Score;
        }

        private static bool IsMoreFacingIntent(
            CandidateEvaluation candidate,
            CandidateEvaluation currentBest)
        {
            if (!Mathf.Approximately(
                    candidate.PlayerFacingAlignment,
                    currentBest.PlayerFacingAlignment))
            {
                return candidate.PlayerFacingAlignment > currentBest.PlayerFacingAlignment;
            }

            return IsMoreRelevant(candidate, currentBest);
        }

        private bool HasClearLineOfSight(
            PegaPlayerObjectEntryCandidate candidate,
            Ray viewRay,
            float targetDistanceMeters,
            out VisibilityRejectionDiagnostic rejection)
        {
            rejection = default;
            if (!IsFinite(viewRay.origin) || !IsFinite(viewRay.direction) ||
                !IsPositiveFinite(targetDistanceMeters))
            {
                rejection = new VisibilityRejectionDiagnostic("invalid-visibility-ray", null, 0f, false, default);
                return false;
            }

            _visibilityQueryPerformedThisUpdate = true;
            int hitCount = Physics.RaycastNonAlloc(
                viewRay,
                _visibilityHits,
                targetDistanceMeters,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            while (hitCount >= _visibilityHits.Length &&
                   _visibilityHits.Length < MaximumVisibilityHitBufferSize)
            {
                Array.Resize(
                    ref _visibilityHits,
                    Mathf.Min(_visibilityHits.Length * 2, MaximumVisibilityHitBufferSize));
                hitCount = Physics.RaycastNonAlloc(
                    viewRay,
                    _visibilityHits,
                    targetDistanceMeters,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            }

            if (hitCount >= _visibilityHits.Length)
            {
                _visibilityBufferSaturatedThisUpdate = true;
                rejection = new VisibilityRejectionDiagnostic(
                    "visibility-hit-buffer-saturated",
                    null,
                    0f,
                    false,
                    default);
                return false;
            }

            bool foundBlockingHit = false;
            float nearestBlockingDistance = float.PositiveInfinity;
            VisibilityRejectionDiagnostic nearestBlockingRejection = default;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit hit = _visibilityHits[hitIndex];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hit.distance >= targetDistanceMeters)
                {
                    continue;
                }

                if (IsIgnoredVisibilityCollider(
                        candidate,
                        hitCollider,
                        out bool isCandidateRecognitionCollider,
                        out PegaPlayerObjectEntryDiscoverySource.CandidateCurrentnessDiagnostic colliderCurrentness) ||
                    hit.distance >= nearestBlockingDistance)
                {
                    continue;
                }

                foundBlockingHit = true;
                nearestBlockingDistance = hit.distance;
                nearestBlockingRejection = new VisibilityRejectionDiagnostic(
                    isCandidateRecognitionCollider
                        ? "candidate-collider-recognition-revalidation-failed"
                        : "occluding-collider",
                    hitCollider,
                    hit.distance,
                    isCandidateRecognitionCollider,
                    colliderCurrentness);
            }

            if (foundBlockingHit)
            {
                rejection = nearestBlockingRejection;
                return false;
            }

            return true;
        }

        private bool IsIgnoredVisibilityCollider(
            PegaPlayerObjectEntryCandidate candidate,
            Collider collider,
            out bool isCandidateRecognitionCollider,
            out PegaPlayerObjectEntryDiscoverySource.CandidateCurrentnessDiagnostic currentness)
        {
            isCandidateRecognitionCollider = false;
            currentness = default;
            Transform colliderTransform = collider.transform;
            if (colliderTransform == transform || colliderTransform.IsChildOf(transform))
            {
                return true;
            }

            IReadOnlyList<Collider> recognitionColliders = candidate.RecognitionColliders;
            for (int index = 0; recognitionColliders != null && index < recognitionColliders.Count; index++)
            {
                if (object.ReferenceEquals(recognitionColliders[index], collider))
                {
                    isCandidateRecognitionCollider = true;
                    if (_discoverySource.IsCurrentRecognitionCollider(
                            candidate,
                            collider,
                            out PegaPlayerObjectEntryDiscoverySource.CandidateCurrentnessDiagnostic colliderCurrentness))
                    {
                        currentness = colliderCurrentness;
                        return true;
                    }

                    currentness = colliderCurrentness;
                }
            }

            return false;
        }

        private void AccumulatePendingSwitch(PegaPlayerObjectEntryCandidate candidate)
        {
            if (!_pendingSwitch.HasValue ||
                !IsSameOccurrence(candidate, _pendingSwitch.Value))
            {
                _pendingSwitch = candidate;
                _pendingSwitchElapsedSeconds = 0f;
            }

            if (_lastPendingSwitchAccumulationFrame == Time.frameCount)
            {
                return;
            }

            _lastPendingSwitchAccumulationFrame = Time.frameCount;
            _pendingSwitchElapsedSeconds += Time.deltaTime;
        }

        private void ResetPendingSwitch()
        {
            _pendingSwitch = null;
            _pendingSwitchElapsedSeconds = 0f;
        }

        private void PublishSelection(
            PegaPlayerObjectEntryCandidate? selection,
            string reason,
            float distanceMeters,
            float relevanceScore,
            LogField[] selectionLossFields = null)
        {
            PegaPlayerObjectEntryCandidate? previous = _currentSelection;
            if (SameSelection(previous, selection))
            {
                _currentSelection = selection;
                return;
            }

            _currentSelection = selection;
            if (selection.HasValue)
            {
                PegaPlayerObjectEntryCandidate current = selection.Value;
                LogField[] fields = AppendLogFields(
                    new[]
                    {
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("objectEntryId", current.ObjectEntryId.StableText),
                        LogFields.Field("occurrenceId", current.OccurrenceId.ToString()),
                        LogFields.Field("physicalRoot", current.PhysicalRootName),
                        LogFields.Field("distanceMeters", distanceMeters),
                        LogFields.Field("relevanceScore", relevanceScore),
                        LogFields.Field("reason", reason)
                    },
                    selectionLossFields);
                _log.Trace(
                    "Player selected a visible world Object Entry occurrence.",
                    fields);
            }
            else if (previous.HasValue)
            {
                PegaPlayerObjectEntryCandidate prior = previous.Value;
                LogField[] fields = AppendLogFields(
                    new[]
                    {
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("objectEntryId", prior.ObjectEntryId.StableText),
                        LogFields.Field("occurrenceId", prior.OccurrenceId.ToString()),
                        LogFields.Field("physicalRoot", prior.PhysicalRootName),
                        LogFields.Field("reason", reason)
                    },
                    selectionLossFields);
                _log.Trace(
                    "Player cleared the selected world Object Entry occurrence.",
                    fields);
            }

            SelectionChanged?.Invoke(selection);
        }

        private static LogField[] AppendLogFields(LogField[] baseFields, LogField[] additionalFields)
        {
            if (additionalFields == null || additionalFields.Length == 0)
            {
                return baseFields;
            }

            LogField[] combinedFields = new LogField[baseFields.Length + additionalFields.Length];
            Array.Copy(baseFields, combinedFields, baseFields.Length);
            Array.Copy(additionalFields, 0, combinedFields, baseFields.Length, additionalFields.Length);
            return combinedFields;
        }

        private void ReportConfigurationDiagnostic(string diagnostic)
        {
            if (string.Equals(_lastConfigurationDiagnostic, diagnostic, StringComparison.Ordinal))
            {
                return;
            }

            _lastConfigurationDiagnostic = diagnostic;
            _log.Error(
                "Player Object Entry selection configuration is invalid.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("diagnostic", diagnostic),
                LogFields.Field("reason", "invalid-selection-configuration"));
        }

        private void ClearConfigurationDiagnostic()
        {
            if (string.IsNullOrEmpty(_lastConfigurationDiagnostic))
            {
                return;
            }

            _log.Info(
                "Player Object Entry selection configuration became valid again.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("previousDiagnostic", _lastConfigurationDiagnostic),
                LogFields.Field("reason", "selection-configuration-restored"));
            _lastConfigurationDiagnostic = string.Empty;
        }

        private void FlushVisibilityBufferDiagnostic()
        {
            if (!_visibilityQueryPerformedThisUpdate)
            {
                return;
            }

            if (_visibilityBufferSaturatedThisUpdate)
            {
                if (_visibilityBufferSaturationReported)
                {
                    return;
                }

                _visibilityBufferSaturationReported = true;
                _log.Warning(
                    "Player Object Entry selection could not confirm visibility because the physics hit buffer is full.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("bufferCapacity", _visibilityHits.Length),
                    LogFields.Field("reason", "visibility-hit-buffer-saturated"));
                return;
            }

            if (!_visibilityBufferSaturationReported)
            {
                return;
            }

            _log.Info(
                "Player Object Entry selection visibility queries fit in their physics hit buffer again.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("bufferCapacity", _visibilityHits.Length),
                LogFields.Field("reason", "visibility-hit-buffer-restored"));
            _visibilityBufferSaturationReported = false;
        }

        private static bool SameSelection(
            PegaPlayerObjectEntryCandidate? first,
            PegaPlayerObjectEntryCandidate? second)
        {
            if (first.HasValue != second.HasValue)
            {
                return false;
            }

            return !first.HasValue || IsSameOccurrence(first.Value, second.Value);
        }

        private static bool IsSameOccurrence(
            PegaPlayerObjectEntryCandidate first,
            PegaPlayerObjectEntryCandidate second) =>
            first.ObjectEntryId.Equals(second.ObjectEntryId) &&
            object.ReferenceEquals(first.Occurrence, second.Occurrence);

        private static float GridCoordinate(int index, int sampleCount, float minimum, float maximum)
        {
            if (index == 0)
            {
                return minimum;
            }

            if (index == sampleCount - 1)
            {
                return maximum;
            }

            return (minimum + maximum) * 0.5f;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private readonly struct CandidateEvaluation
        {
            internal CandidateEvaluation(
                PegaPlayerObjectEntryCandidate candidate,
                float score,
                float distanceMeters,
                float playerFacingAlignment,
                bool isDirectlyAimed)
            {
                Candidate = candidate;
                Score = score;
                DistanceMeters = distanceMeters;
                PlayerFacingAlignment = playerFacingAlignment;
                IsDirectlyAimed = isDirectlyAimed;
            }

            internal PegaPlayerObjectEntryCandidate Candidate { get; }

            internal float Score { get; }

            internal float DistanceMeters { get; }

            internal float PlayerFacingAlignment { get; }

            internal bool IsDirectlyAimed { get; }
        }

        internal readonly struct VisibilityRejectionDiagnostic
        {
            internal VisibilityRejectionDiagnostic(
                string reason,
                Collider blockingCollider,
                float blockingDistanceMeters,
                bool isCandidateRecognitionCollider,
                PegaPlayerObjectEntryDiscoverySource.CandidateCurrentnessDiagnostic candidateColliderCurrentness)
            {
                Reason = reason ?? string.Empty;
                BlockingCollider = blockingCollider;
                BlockingDistanceMeters = blockingDistanceMeters;
                IsCandidateRecognitionCollider = isCandidateRecognitionCollider;
                CandidateColliderCurrentness = candidateColliderCurrentness;
            }

            internal string Reason { get; }

            internal Collider BlockingCollider { get; }

            internal float BlockingDistanceMeters { get; }

            internal bool IsCandidateRecognitionCollider { get; }

            internal PegaPlayerObjectEntryDiscoverySource.CandidateCurrentnessDiagnostic CandidateColliderCurrentness { get; }
        }

        private struct CandidateEvaluationDiagnostics
        {
            internal int PlayerVolumeRaySampleCount;
            internal int RecognizedColliderCount;
            internal int RecognitionColliderRejectionCount;
            internal int CandidateColliderRayMissCount;
            internal int InvalidTargetPointCount;
            internal int InvalidTargetDistanceCount;
            internal int OutOfRangeCount;
            internal int BehindPlayerCount;
            internal int OutsideHorizontalVolumeCount;
            internal int OutsideVerticalVolumeCount;
            internal int InvalidVisibilityRayCount;
            internal int OccludedRayCount;
            internal int CandidateColliderRevalidationBlockCount;
            internal int CandidateColliderRevalidationQueryCount;
            internal int VisibilityBufferSaturatedCount;
            internal bool HasFailedTargetPoint;
            internal Vector3 LastFailedTargetPoint;
            internal bool HasTargetDistance;
            internal float MinimumTargetDistanceSquared;
            internal bool HasSelectionAngles;
            internal float MinimumHorizontalAngleDegrees;
            internal float MaximumHorizontalAngleDegrees;
            internal float MinimumVerticalAngleDegrees;
            internal float MaximumVerticalAngleDegrees;
            internal bool HasPlayerFacingDot;
            internal float MaximumPlayerFacingDot;
            internal string NearestOccludingColliderName;
            internal string NearestOccludingColliderEntityId;
            internal float NearestOccludingDistanceMeters;
            internal string LastRecognitionFailureReason;
            internal bool LastRecognitionFailureQueryExecuted;
            internal string LastRecognitionStatus;
            internal string LastRecognitionDiagnostic;
            internal string LastRecognitionColliderName;
            internal int RecognitionQueryCount;
            internal string LastRecognitionQueryStatus;
            internal string LastRecognitionQueryDiagnostic;
            internal string LastRecognitionQueryColliderName;
            internal string CandidateColliderRevalidationFailureReason;
            internal bool CandidateColliderRevalidationFailureQueryExecuted;
            internal string CandidateColliderRevalidationStatus;
            internal string CandidateColliderRevalidationDiagnostic;
            internal string CandidateColliderLatestQueryStatus;
            internal string CandidateColliderLatestQueryDiagnostic;
            internal string CandidateColliderLatestQueryColliderName;

            internal void RecordRecognitionColliderRejection(
                PegaPlayerObjectEntryDiscoverySource.CandidateCurrentnessDiagnostic currentness)
            {
                RecognitionColliderRejectionCount++;
                LastRecognitionFailureReason = currentness.FailureReason;
                LastRecognitionFailureQueryExecuted = currentness.FailureRecognitionQueryExecuted;
                LastRecognitionStatus = currentness.RecognitionStatus.HasValue
                    ? currentness.RecognitionStatus.Value.ToString()
                    : "not-run";
                LastRecognitionDiagnostic = currentness.FrameworkDiagnostic;
                LastRecognitionColliderName = currentness.FailureColliderName;
                if (currentness.RecognitionQueryExecuted)
                {
                    RecognitionQueryCount++;
                    LastRecognitionQueryStatus = currentness.RecognitionStatus.HasValue
                        ? currentness.RecognitionStatus.Value.ToString()
                        : "not-run";
                    LastRecognitionQueryDiagnostic = currentness.FrameworkDiagnostic;
                    LastRecognitionQueryColliderName = currentness.RecognitionQueryColliderName;
                }
            }

            internal void RecordTargetDistance(float distanceSquared)
            {
                if (!HasTargetDistance || distanceSquared < MinimumTargetDistanceSquared)
                {
                    HasTargetDistance = true;
                    MinimumTargetDistanceSquared = distanceSquared;
                }
            }

            internal void RecordSelectionAngles(float horizontalAngleDegrees, float verticalAngleDegrees)
            {
                if (!HasSelectionAngles)
                {
                    HasSelectionAngles = true;
                    MinimumHorizontalAngleDegrees = horizontalAngleDegrees;
                    MaximumHorizontalAngleDegrees = horizontalAngleDegrees;
                    MinimumVerticalAngleDegrees = verticalAngleDegrees;
                    MaximumVerticalAngleDegrees = verticalAngleDegrees;
                    return;
                }

                MinimumHorizontalAngleDegrees = Mathf.Min(
                    MinimumHorizontalAngleDegrees,
                    horizontalAngleDegrees);
                MaximumHorizontalAngleDegrees = Mathf.Max(
                    MaximumHorizontalAngleDegrees,
                    horizontalAngleDegrees);
                MinimumVerticalAngleDegrees = Mathf.Min(
                    MinimumVerticalAngleDegrees,
                    verticalAngleDegrees);
                MaximumVerticalAngleDegrees = Mathf.Max(
                    MaximumVerticalAngleDegrees,
                    verticalAngleDegrees);
            }

            internal void RecordPlayerFacingDot(float playerFacingDot)
            {
                if (!HasPlayerFacingDot || playerFacingDot > MaximumPlayerFacingDot)
                {
                    HasPlayerFacingDot = true;
                    MaximumPlayerFacingDot = playerFacingDot;
                }
            }

            internal void RecordFailedTargetPoint(Vector3 targetPoint)
            {
                HasFailedTargetPoint = true;
                LastFailedTargetPoint = targetPoint;
            }

            internal void RecordVisibilityRejection(VisibilityRejectionDiagnostic rejection)
            {
                switch (rejection.Reason)
                {
                    case "invalid-visibility-ray":
                        InvalidVisibilityRayCount++;
                        break;
                    case "visibility-hit-buffer-saturated":
                        VisibilityBufferSaturatedCount++;
                        break;
                    case "candidate-collider-recognition-revalidation-failed":
                        CandidateColliderRevalidationBlockCount++;
                        CandidateColliderRevalidationFailureReason =
                            rejection.CandidateColliderCurrentness.FailureReason;
                        CandidateColliderRevalidationFailureQueryExecuted =
                            rejection.CandidateColliderCurrentness.FailureRecognitionQueryExecuted;
                        CandidateColliderRevalidationStatus = rejection.CandidateColliderCurrentness.RecognitionStatus.HasValue
                            ? rejection.CandidateColliderCurrentness.RecognitionStatus.Value.ToString()
                            : "not-run";
                        CandidateColliderRevalidationDiagnostic =
                            rejection.CandidateColliderCurrentness.FrameworkDiagnostic;
                        if (rejection.CandidateColliderCurrentness.RecognitionQueryExecuted)
                        {
                            CandidateColliderRevalidationQueryCount++;
                            CandidateColliderLatestQueryStatus =
                                rejection.CandidateColliderCurrentness.RecognitionStatus.HasValue
                                    ? rejection.CandidateColliderCurrentness.RecognitionStatus.Value.ToString()
                                    : "not-run";
                            CandidateColliderLatestQueryDiagnostic =
                                rejection.CandidateColliderCurrentness.FrameworkDiagnostic;
                            CandidateColliderLatestQueryColliderName =
                                rejection.CandidateColliderCurrentness.RecognitionQueryColliderName;
                        }

                        break;
                    case "occluding-collider":
                        OccludedRayCount++;
                        if (rejection.BlockingCollider != null &&
                            (string.IsNullOrEmpty(NearestOccludingColliderName) ||
                             rejection.BlockingDistanceMeters < NearestOccludingDistanceMeters))
                        {
                            NearestOccludingColliderName = rejection.BlockingCollider.name;
                            NearestOccludingColliderEntityId = rejection.BlockingCollider.GetEntityId().ToString();
                            NearestOccludingDistanceMeters = rejection.BlockingDistanceMeters;
                        }

                        break;
                    default:
                        InvalidVisibilityRayCount++;
                        break;
                }
            }

            internal string BuildRejectionCriteria()
            {
                StringBuilder criteria = new StringBuilder();
                AppendRejectionCount(criteria, "recognition-collider", RecognitionColliderRejectionCount);
                AppendRejectionCount(criteria, "candidate-collider-ray-miss", CandidateColliderRayMissCount);
                AppendRejectionCount(criteria, "invalid-target-point", InvalidTargetPointCount);
                AppendRejectionCount(criteria, "invalid-target-distance", InvalidTargetDistanceCount);
                AppendRejectionCount(criteria, "out-of-range", OutOfRangeCount);
                AppendRejectionCount(criteria, "behind-player", BehindPlayerCount);
                AppendRejectionCount(criteria, "outside-horizontal-selection-volume", OutsideHorizontalVolumeCount);
                AppendRejectionCount(criteria, "outside-vertical-selection-volume", OutsideVerticalVolumeCount);
                AppendRejectionCount(criteria, "invalid-visibility-ray", InvalidVisibilityRayCount);
                AppendRejectionCount(criteria, "occluding-collider", OccludedRayCount);
                AppendRejectionCount(
                    criteria,
                    "candidate-collider-recognition-failed-during-visibility",
                    CandidateColliderRevalidationBlockCount);
                AppendRejectionCount(criteria, "visibility-hit-buffer-saturated", VisibilityBufferSaturatedCount);
                return criteria.Length == 0 ? "no-sample-reason-recorded" : criteria.ToString();
            }

            private static void AppendRejectionCount(StringBuilder criteria, string reason, int count)
            {
                if (count <= 0)
                {
                    return;
                }

                if (criteria.Length > 0)
                {
                    criteria.Append(';');
                }

                criteria.Append(reason).Append('=').Append(count);
            }
        }
    }
}

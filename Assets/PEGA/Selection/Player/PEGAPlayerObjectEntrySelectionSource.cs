using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.Player.Perception;
using UnityEngine;
namespace PEGA.Selection.Player
{
    /// <summary>Seleciona a ocorrência física elegível mais relevante para a câmera deste Player.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PegaPlayerObjectEntryDiscoverySource))]
    [AddComponentMenu("PEGA/Selection/Player/Object Entry Selection Source")]
    public sealed class PegaPlayerObjectEntrySelectionSource : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.PlayerObjectSelection";
        private const int ProjectedBoundsGridSize = 3;
        private const int InitialVisibilityHitBufferSize = 32;
        private const int MaximumVisibilityHitBufferSize = 1024;
        private const float VisibilityDistanceToleranceMeters = 0.005f;
        private static readonly Vector2 ViewportCenter = new Vector2(0.5f, 0.5f);

        [Header("Contextual Selection")]
        [SerializeField, Min(0f)]
        [Tooltip("Distância máxima, em metros, do centro físico do Player até um ponto visível da geometria elegível.")]
        private float selectionRangeMeters = 5f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Raio máximo da mira contextual em coordenadas normalizadas da viewport da câmera.")]
        private float aimAssistViewportRadius = 0.45f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Margem adicional de viewport usada apenas para manter a ocorrência já selecionada.")]
        private float stickyViewportMargin = 0.1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Peso da distância física no score relativo; o peso restante é dividido entre mira e direção do Player.")]
        private float proximityWeight = 0.3f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Peso do alinhamento entre a frente horizontal do Player e a geometria avaliada.")]
        private float playerFacingWeight = 0.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Alinhamento mínimo da frente do Player para contar como intenção clara de troca.")]
        private float facingIntentAlignmentThreshold = 0.9f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Vantagem mínima de alinhamento frontal para superar a retenção do foco atual.")]
        private float facingIntentAdvantage = 0.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Vantagem mínima do score de outro candidato para iniciar a troca de foco.")]
        private float switchScoreMargin = 0.12f;

        [SerializeField, Min(0f)]
        [Tooltip("Tempo, em segundos, pelo qual outro candidato deve manter vantagem antes da troca.")]
        private float switchConfirmationSeconds = 0.15f;

        [Header("Scene View")]
        [SerializeField]
        [Tooltip("Off oculta a seleção; Resumido mostra alcance e foco atual; Detalhado inclui a identidade estável do objeto e a mira contextual.")]
        private PegaDiagnosticVisualizationLevel selectionVisualizationLevel =
            PegaDiagnosticVisualizationLevel.Summary;

        private readonly RaycastHit[] _initialVisibilityHits =
            new RaycastHit[InitialVisibilityHitBufferSize];

        private RaycastHit[] _visibilityHits;
        private PegaPlayerObjectEntryDiscoverySource _discoverySource;
        private PlayerActorDeclaration _playerActorDeclaration;
        private ScopedLogger _log;
        private PegaPlayerObjectEntryCandidate? _currentSelection;
        private PegaPlayerObjectEntryCandidate? _pendingSwitch;
        private float _pendingSwitchElapsedSeconds;
        private string _lastConfigurationDiagnostic = string.Empty;
        private string _lastCameraDiagnostic = string.Empty;
        private bool _visibilityBufferSaturationReported;
        private bool _visibilityBufferSaturatedThisUpdate;
        private bool _visibilityQueryPerformedThisUpdate;

        /// <summary>A seleção física atual; nula quando não há candidato elegível.</summary>
        public PegaPlayerObjectEntryCandidate? CurrentSelection => _currentSelection;

        /// <summary>O alcance físico máximo da seleção, em metros.</summary>
        public float SelectionRangeMeters => selectionRangeMeters;

        /// <summary>O raio de mira contextual medido em coordenadas normalizadas da viewport.</summary>
        public float AimAssistViewportRadius => aimAssistViewportRadius;

#if UNITY_EDITOR
        public PegaDiagnosticVisualizationLevel SelectionVisualizationLevel =>
            selectionVisualizationLevel;
#endif

        /// <summary>Notifica consumidores quando a ocorrência selecionada muda ou é removida.</summary>
        public event Action<PegaPlayerObjectEntryCandidate?> SelectionChanged;

        private void Awake()
        {
            _discoverySource = GetComponent<PegaPlayerObjectEntryDiscoverySource>();
            _playerActorDeclaration = GetComponent<PlayerActorDeclaration>();
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

            if (_playerActorDeclaration == null)
            {
                _log.Error(
                    $"{nameof(PegaPlayerObjectEntrySelectionSource)} requires {nameof(PlayerActorDeclaration)} on the Player Actor root to read its bound camera.",
                    LogFields.Field("actorRoot", gameObject.name));
                enabled = false;
            }
        }

        private void OnEnable()
        {
            ResetPendingSwitch();
        }

        private void Update()
        {
            _visibilityBufferSaturatedThisUpdate = false;
            _visibilityQueryPerformedThisUpdate = false;
            if (!TryGetPlayerCamera(out Camera playerCamera, out string cameraDiagnostic))
            {
                ReportCameraDiagnostic(cameraDiagnostic);
                ResetPendingSwitch();
                PublishSelection(null, "player-camera-unavailable", 0f, 0f);
                return;
            }

            ClearCameraDiagnostic();
            if (!TryValidateConfiguration(out string configurationDiagnostic))
            {
                ReportConfigurationDiagnostic(configurationDiagnostic);
                ResetPendingSwitch();
                PublishSelection(null, "invalid-selection-configuration", 0f, 0f);
                return;
            }

            if (!TryGetPlayerForward(out Vector3 playerForward))
            {
                ReportConfigurationDiagnostic("Player forward direction is not finite or has no horizontal component.");
                ResetPendingSwitch();
                PublishSelection(null, "player-forward-unavailable", 0f, 0f);
                return;
            }

            ClearConfigurationDiagnostic();

            PegaPlayerObjectEntryDiscoverySource discoverySource = _discoverySource;
            if (discoverySource == null || !discoverySource.isActiveAndEnabled)
            {
                ResetPendingSwitch();
                PublishSelection(null, "discovery-source-unavailable", 0f, 0f);
                return;
            }

            Vector3 discoveryCenter = discoverySource.DiscoveryCenter;
            if (!IsFinite(discoveryCenter))
            {
                ResetPendingSwitch();
                PublishSelection(null, "player-selection-origin-unavailable", 0f, 0f);
                return;
            }

            float selectionRangeSquared = selectionRangeMeters * selectionRangeMeters;
            bool foundBestCandidate = false;
            bool foundBestFacingCandidate = false;
            bool foundBestFacingBandCandidate = false;
            bool foundCurrentCandidate = false;
            CandidateEvaluation bestCandidate = default;
            CandidateEvaluation bestFacingCandidate = default;
            CandidateEvaluation bestFacingBandCandidate = default;
            CandidateEvaluation currentCandidate = default;
            IReadOnlyList<PegaPlayerObjectEntryCandidate> candidates = discoverySource.Candidates;

            for (int index = 0; index < candidates.Count; index++)
            {
                PegaPlayerObjectEntryCandidate candidate = candidates[index];
                GameObject occurrence = candidate.Occurrence;
                if (occurrence == null ||
                    !discoverySource.IsCurrentCandidate(candidate))
                {
                    continue;
                }

                bool isCurrent = _currentSelection.HasValue &&
                    IsSameOccurrence(candidate, _currentSelection.Value);
                if (!TryEvaluateCandidate(
                        candidate,
                        playerCamera,
                        discoveryCenter,
                        playerForward,
                        selectionRangeSquared,
                        isCurrent,
                        out CandidateEvaluation evaluation))
                {
                    continue;
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
            if (!foundBestCandidate)
            {
                ResetPendingSwitch();
                PublishSelection(
                    null,
                    _currentSelection.HasValue
                        ? "previous-selection-no-longer-visible-or-eligible"
                        : "no-contextual-candidate",
                    0f,
                    0f);
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
                    bestCandidate.Score);
                return;
            }

            CandidateEvaluation switchCandidate = bestCandidate;
            bool isPlayerFacingIntentSwitch = false;
            bool isFacingBandScoreSwitch = false;
            bool hasDirectCameraSwitch = bestCandidate.IsDirectlyAimed &&
                !currentCandidate.IsDirectlyAimed &&
                !IsSameOccurrence(bestCandidate.Candidate, currentCandidate.Candidate);

            if (!hasDirectCameraSwitch && currentCandidate.IsDirectlyAimed)
            {
                ResetPendingSwitch();
                PublishSelection(
                    currentCandidate.Candidate,
                    "current-selection-directly-aimed",
                    currentCandidate.DistanceMeters,
                    currentCandidate.Score);
                return;
            }

            if (!hasDirectCameraSwitch &&
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

            if (!hasDirectCameraSwitch && !isFacingBandScoreSwitch &&
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

            if (!hasDirectCameraSwitch && !isPlayerFacingIntentSwitch &&
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
                        : hasDirectCameraSwitch
                            ? "direct-camera-focus-confirmed"
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
            PublishSelection(null, "selection-source-disabled", 0f, 0f);
        }

        private bool TryGetPlayerCamera(out Camera camera, out string diagnostic)
        {
            camera = null;
            if (_playerActorDeclaration == null)
            {
                diagnostic = $"{nameof(PlayerActorDeclaration)} is unavailable on the Player Actor root.";
                return false;
            }

            if (_playerActorDeclaration.PlayerInput == null)
            {
                diagnostic = "The current Player occurrence has no bound PlayerInput evidence yet.";
                return false;
            }

            camera = _playerActorDeclaration.PlayerInput.camera;
            if (camera == null)
            {
                diagnostic = "The current PlayerInput has no camera assigned by its Player camera Output.";
                return false;
            }

            if (!camera.isActiveAndEnabled)
            {
                diagnostic = "The camera assigned to this Player is not active and enabled.";
                camera = null;
                return false;
            }

            if (!IsPositiveFinite(camera.farClipPlane) ||
                !IsPositiveFinite(camera.nearClipPlane))
            {
                diagnostic = "The camera clipping planes are invalid for contextual selection.";
                camera = null;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryValidateConfiguration(out string diagnostic)
        {
            if (!IsNonNegativeFinite(selectionRangeMeters))
            {
                diagnostic = "Selection range must be finite and non-negative.";
                return false;
            }

            if (!IsPositiveFinite(aimAssistViewportRadius) ||
                aimAssistViewportRadius > 1f)
            {
                diagnostic = "Aim assist viewport radius must be finite and within (0, 1].";
                return false;
            }

            if (!IsNonNegativeFinite(stickyViewportMargin) ||
                stickyViewportMargin > 1f)
            {
                diagnostic = "Sticky viewport margin must be finite and within [0, 1].";
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

        private bool TryEvaluateCandidate(
            PegaPlayerObjectEntryCandidate candidate,
            Camera camera,
            Vector3 playerCenter,
            Vector3 playerForward,
            float selectionRangeSquared,
            bool isCurrent,
            out CandidateEvaluation bestEvaluation)
        {
            bestEvaluation = default;
            bool foundEvaluation = false;
            IReadOnlyList<Collider> recognitionColliders = candidate.RecognitionColliders;
            for (int colliderIndex = 0;
                 recognitionColliders != null && colliderIndex < recognitionColliders.Count;
                 colliderIndex++)
            {
                Collider candidateCollider = recognitionColliders[colliderIndex];
                if (!_discoverySource.IsCurrentRecognitionCollider(candidate, candidateCollider))
                {
                    continue;
                }

                if (TryGetProjectedViewportBounds(
                        camera,
                        candidateCollider.bounds,
                        out Vector2 viewportMinimum,
                        out Vector2 viewportMaximum))
                {
                    for (int yIndex = 0; yIndex < ProjectedBoundsGridSize; yIndex++)
                    {
                        float viewportY = GridCoordinate(yIndex, viewportMinimum.y, viewportMaximum.y);
                        for (int xIndex = 0; xIndex < ProjectedBoundsGridSize; xIndex++)
                        {
                            float viewportX = GridCoordinate(xIndex, viewportMinimum.x, viewportMaximum.x);
                            if (!TryEvaluateViewportRay(
                                    candidate,
                                    candidateCollider,
                                    camera,
                                    playerCenter,
                                    playerForward,
                                    selectionRangeSquared,
                                    isCurrent,
                                    new Vector2(viewportX, viewportY),
                                    out CandidateEvaluation evaluation))
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

                if (TryEvaluateViewportRay(
                        candidate,
                        candidateCollider,
                        camera,
                        playerCenter,
                        playerForward,
                        selectionRangeSquared,
                        isCurrent,
                        ViewportCenter,
                        out CandidateEvaluation centeredEvaluation) &&
                    (!foundEvaluation || IsMoreRelevant(centeredEvaluation, bestEvaluation)))
                {
                    foundEvaluation = true;
                    bestEvaluation = centeredEvaluation;
                }
            }

            return foundEvaluation;
        }

        private static bool TryGetProjectedViewportBounds(
            Camera camera,
            Bounds worldBounds,
            out Vector2 viewportMinimum,
            out Vector2 viewportMaximum)
        {
            Vector3 boundsMinimum = worldBounds.min;
            Vector3 boundsMaximum = worldBounds.max;
            float minimumX = float.PositiveInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float maximumY = float.NegativeInfinity;
            bool foundFrontCorner = false;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldPoint = new Vector3(
                    (corner & 1) == 0 ? boundsMinimum.x : boundsMaximum.x,
                    (corner & 2) == 0 ? boundsMinimum.y : boundsMaximum.y,
                    (corner & 4) == 0 ? boundsMinimum.z : boundsMaximum.z);
                Vector3 viewportPoint = camera.WorldToViewportPoint(worldPoint);
                if (!IsFinite(viewportPoint) || viewportPoint.z <= camera.nearClipPlane)
                {
                    continue;
                }

                foundFrontCorner = true;
                minimumX = Mathf.Min(minimumX, viewportPoint.x);
                minimumY = Mathf.Min(minimumY, viewportPoint.y);
                maximumX = Mathf.Max(maximumX, viewportPoint.x);
                maximumY = Mathf.Max(maximumY, viewportPoint.y);
            }

            if (!foundFrontCorner ||
                maximumX < 0f || minimumX > 1f ||
                maximumY < 0f || minimumY > 1f)
            {
                viewportMinimum = default;
                viewportMaximum = default;
                return false;
            }

            viewportMinimum = new Vector2(
                Mathf.Clamp01(minimumX),
                Mathf.Clamp01(minimumY));
            viewportMaximum = new Vector2(
                Mathf.Clamp01(maximumX),
                Mathf.Clamp01(maximumY));
            return viewportMinimum.x <= viewportMaximum.x &&
                   viewportMinimum.y <= viewportMaximum.y;
        }

        private bool TryEvaluateViewportRay(
            PegaPlayerObjectEntryCandidate candidate,
            Collider candidateCollider,
            Camera camera,
            Vector3 playerCenter,
            Vector3 playerForward,
            float selectionRangeSquared,
            bool isCurrent,
            Vector2 viewportPoint,
            out CandidateEvaluation evaluation)
        {
            evaluation = default;
            Ray viewRay = camera.ViewportPointToRay(
                new Vector3(viewportPoint.x, viewportPoint.y, 0f));
            if (!candidateCollider.Raycast(viewRay, out RaycastHit targetHit, camera.farClipPlane) ||
                !IsFinite(targetHit.point))
            {
                return false;
            }

            Vector3 toTarget = targetHit.point - playerCenter;
            float distanceSquared = toTarget.sqrMagnitude;
            if (!IsFinite(distanceSquared) || distanceSquared > selectionRangeSquared)
            {
                return false;
            }

            Vector3 planarToTarget = toTarget;
            planarToTarget.y = 0f;
            float planarDistanceSquared = planarToTarget.sqrMagnitude;
            if (planarDistanceSquared > 0f &&
                Vector3.Dot(playerForward, planarToTarget / Mathf.Sqrt(planarDistanceSquared)) < 0f)
            {
                return false;
            }

            float playerFacingAlignment = planarDistanceSquared > 0f
                ? Mathf.Clamp01(Vector3.Dot(
                    playerForward,
                    planarToTarget / Mathf.Sqrt(planarDistanceSquared)))
                : 1f;

            Vector3 projectedTarget = camera.WorldToViewportPoint(targetHit.point);
            if (!IsFinite(projectedTarget) ||
                projectedTarget.z <= camera.nearClipPlane ||
                projectedTarget.z > camera.farClipPlane)
            {
                return false;
            }

            float viewportCenterDistance = Vector2.Distance(
                new Vector2(projectedTarget.x, projectedTarget.y),
                ViewportCenter);
            float maximumViewportDistance = aimAssistViewportRadius +
                (isCurrent ? stickyViewportMargin : 0f);
            if (!IsFinite(viewportCenterDistance) ||
                viewportCenterDistance > maximumViewportDistance)
            {
                return false;
            }

            float distanceMeters = Mathf.Sqrt(distanceSquared);
            if (!HasClearLineOfSight(
                    candidate,
                    viewRay,
                    targetHit.distance))
            {
                return false;
            }

            float normalizedViewDistance = Mathf.Clamp01(
                viewportCenterDistance / aimAssistViewportRadius);
            float normalizedRangeDistance = selectionRangeMeters > 0f
                ? Mathf.Clamp01(distanceMeters / selectionRangeMeters)
                : 0f;
            float cameraWeight = 1f - proximityWeight - playerFacingWeight;
            float score =
                cameraWeight * normalizedViewDistance +
                proximityWeight * normalizedRangeDistance +
                playerFacingWeight * (1f - playerFacingAlignment);
            evaluation = new CandidateEvaluation(
                candidate,
                score,
                distanceMeters,
                playerFacingAlignment,
                viewportCenterDistance <= 0.001f);
            return true;
        }

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
            float targetDistanceMeters)
        {
            if (!IsFinite(viewRay.origin) || !IsFinite(viewRay.direction) ||
                !IsPositiveFinite(targetDistanceMeters))
            {
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
                return false;
            }

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit hit = _visibilityHits[hitIndex];
                Collider hitCollider = hit.collider;
                if (hitCollider == null ||
                    hit.distance >= targetDistanceMeters - VisibilityDistanceToleranceMeters ||
                    IsIgnoredVisibilityCollider(candidate, hitCollider))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private bool IsIgnoredVisibilityCollider(
            PegaPlayerObjectEntryCandidate candidate,
            Collider collider)
        {
            Transform colliderTransform = collider.transform;
            if (colliderTransform == transform || colliderTransform.IsChildOf(transform))
            {
                return true;
            }

            IReadOnlyList<Collider> recognitionColliders = candidate.RecognitionColliders;
            for (int index = 0; recognitionColliders != null && index < recognitionColliders.Count; index++)
            {
                if (object.ReferenceEquals(recognitionColliders[index], collider) &&
                    _discoverySource.IsCurrentRecognitionCollider(candidate, collider))
                {
                    return true;
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
            float relevanceScore)
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
                _log.Trace(
                    "Player selected a visible world Object Entry occurrence.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("objectEntryId", current.ObjectEntryId.StableText),
                    LogFields.Field("occurrenceId", current.OccurrenceId.ToString()),
                    LogFields.Field("physicalRoot", current.PhysicalRootName),
                    LogFields.Field("distanceMeters", distanceMeters),
                    LogFields.Field("relevanceScore", relevanceScore),
                    LogFields.Field("reason", reason));
            }
            else if (previous.HasValue)
            {
                PegaPlayerObjectEntryCandidate prior = previous.Value;
                _log.Trace(
                    "Player cleared the selected world Object Entry occurrence.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("objectEntryId", prior.ObjectEntryId.StableText),
                    LogFields.Field("occurrenceId", prior.OccurrenceId.ToString()),
                    LogFields.Field("physicalRoot", prior.PhysicalRootName),
                    LogFields.Field("reason", reason));
            }

            SelectionChanged?.Invoke(selection);
        }

        private void ReportCameraDiagnostic(string diagnostic)
        {
            if (string.Equals(_lastCameraDiagnostic, diagnostic, StringComparison.Ordinal))
            {
                return;
            }

            _lastCameraDiagnostic = diagnostic;
            _log.Warning(
                "Player Object Entry selection is waiting for its bound camera.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("diagnostic", diagnostic),
                LogFields.Field("reason", "player-camera-unavailable"));
        }

        private void ClearCameraDiagnostic()
        {
            if (string.IsNullOrEmpty(_lastCameraDiagnostic))
            {
                return;
            }

            _log.Info(
                "Player Object Entry selection camera became available again.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("previousDiagnostic", _lastCameraDiagnostic),
                LogFields.Field("reason", "player-camera-restored"));
            _lastCameraDiagnostic = string.Empty;
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

        private static float GridCoordinate(int index, float minimum, float maximum)
        {
            if (index == 0)
            {
                return minimum;
            }

            if (index == ProjectedBoundsGridSize - 1)
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
    }
}

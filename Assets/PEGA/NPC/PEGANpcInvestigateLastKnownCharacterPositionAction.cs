using System;
using System.Collections.Generic;
using PEGA.Actors;
using UnityEngine;
using UnityEngine.AI;

namespace PEGA.NPC
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Actions/Investigate Last Known Target Position")]
    [RequireComponent(typeof(PEGANpcKnowledgeState))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PEGANpcInvestigateLastKnownCharacterPositionAction : PEGANpcNavigationAction,
        IPEGANpcKnowledgeChangeRelevanceProvider,
        IPEGANpcAttemptedTargetProvider
    {
        private const float PositionDeduplicationToleranceMeters = 0.1f;
        private const float PositionComparisonEpsilonMeters = 0.001f;

        [Header("Local Investigation")]
        [SerializeField, Min(0.25f)]
        [Tooltip("Raio planar máximo em torno da última posição conhecida (m). Limita esta Mission; não identifica fronteiras semânticas de sala.")]
        private float localRadiusMeters = 3f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Maior comprimento permitido para cada rota de investigação (m).")]
        private float maximumRouteLengthMeters = 6f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Deslocamento físico mínimo pretendido além do raio de chegada (m). Evita concluir a investigação com apenas alguns centímetros de movimento.")]
        private float minimumPhysicalRepositionMeters = 0.75f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Espaçamento radial entre amostras locais (m).")]
        private float sampleSpacingMeters = 1f;

        [SerializeField, Range(4, 16)]
        [Tooltip("Quantidade determinística de direções amostradas por anel.")]
        private int sampleDirectionsPerRing = 8;

        [SerializeField, Min(0.05f)]
        [Tooltip("Distância máxima para projetar cada amostra na NavMesh compatível (m).")]
        private float sampleProjectionDistanceMeters = 0.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Diferença vertical máxima entre a evidência e cada ponto/canto da rota (m).")]
        private float maximumVerticalDifferenceMeters = 0.35f;

        private PEGANpcKnowledgeState _knowledgeState;
        private IPEGANpcLastKnownTargetContinuationProvider _approachAction;
        private NavMeshAgent _agent;
        private PEGANpcPlanContinuation _executionContinuation;
        private PEGANpcPlanningTargetId _executionTargetId;
        private bool _hasExecutionContinuation;

        protected override PEGANpcActionEffectKind ActionEffectKind =>
            PEGANpcActionEffectKind.ReachLocalObservationPosition;

        public override PEGANpcActionContract Contract => new PEGANpcActionContract(
            PEGANpcPlanningActionId.InvestigateLastKnownCharacterPosition,
            ActionEffectKind,
            PEGANpcPlanningTargetKind.LocalObservationPosition,
            true,
            "NPC began searching Target's last known position.");

        internal bool IsLocalTargetUsable(
            PEGANpcPlanSnapshot plan,
            PEGANpcPlanContinuation continuation,
            out string diagnostic,
            bool requireFurtherPhysicalMovement = true)
        {
            diagnostic = string.Empty;
            if (plan.ActionId != PEGANpcPlanningActionId.InvestigateLastKnownCharacterPosition ||
                plan.Purpose != PEGANpcPlanPurpose.PlanContinuationStep ||
                plan.TargetKind != PEGANpcPlanningTargetKind.LocalObservationPosition ||
                plan.TargetProvenance != PEGANpcPlanningTargetProvenance.DerivedFromKnowledge ||
                plan.TargetId.IdentityKind != PEGANpcPlanningTargetIdentityKind.InvestigationSample ||
                plan.TargetId.InvestigationSubjectActorId != continuation.SubjectActorId ||
                Math.Abs(plan.TargetId.InvestigationLossTimeSeconds -
                         continuation.ContactLossObservationTimeSeconds) > 0.000001d ||
                Math.Abs(plan.EvidenceTimeSeconds - continuation.PositionEvidenceTimeSeconds) > 0.000001d)
            {
                diagnostic = "The selected Action Target does not belong to this live pursuit continuation.";
                return false;
            }

            if (!TryValidateContinuation(continuation, out diagnostic))
            {
                return false;
            }

            if (!ValidateConfiguration(out diagnostic))
            {
                return false;
            }

            if (!TryCalculateBoundedRoute(
                    continuation.LastKnownPosition,
                    plan.TargetPosition,
                    out PEGANpcNavigationRouteEstimate route,
                    out float maximumCornerDistanceMeters,
                    out diagnostic))
            {
                return false;
            }

            if (route.RouteLengthMeters > maximumRouteLengthMeters + PositionComparisonEpsilonMeters ||
                maximumCornerDistanceMeters > localRadiusMeters + PositionComparisonEpsilonMeters ||
                Math.Abs(plan.TargetPosition.y - continuation.LastKnownPosition.y) > maximumVerticalDifferenceMeters + PositionComparisonEpsilonMeters)
            {
                diagnostic = "The selected local Target or its complete route exceeds the configured investigation bounds.";
                return false;
            }

            if (requireFurtherPhysicalMovement &&
                !RequiresPhysicalReposition(plan.TargetPosition))
            {
                diagnostic = "A local investigation Target must require physical movement beyond its arrival radius and configured minimum reposition distance.";
                return false;
            }

            return true;
        }

        public bool TryCreatePlanningCandidates(
            PEGANpcPlanContinuation continuation,
            IReadOnlyCollection<PEGANpcPlanningTargetId> attemptedTargets,
            out IReadOnlyList<PEGANpcPlanningTarget> targets,
            out IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
            out string diagnostic)
        {
            List<PEGANpcPlanningTarget> targetList = new List<PEGANpcPlanningTarget>();
            List<PEGANpcPlanningActionCandidate> candidateList = new List<PEGANpcPlanningActionCandidate>();
            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());

            if (!TryValidateContinuation(continuation, out diagnostic))
            {
                return false;
            }

            if (!ValidateConfiguration(out diagnostic))
            {
                return false;
            }

            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            {
                diagnostic = "The NPC NavMeshAgent must be enabled and placed on its baked NavMesh to sample local positions.";
                return false;
            }

            NavMeshQueryFilter filter = CreateQueryFilter();
            List<Vector3> projectedPositions = new List<Vector3>();
            int ringCount = Mathf.CeilToInt(localRadiusMeters / sampleSpacingMeters);
            int sampleOrdinal = 0;
            int projectionRejected = 0;
            int boundsRejected = 0;
            int insufficientReposition = 0;
            int duplicatePosition = 0;
            int previouslyAttempted = 0;
            int routeRejected = 0;
            string firstRouteDiagnostic = string.Empty;
            for (int ring = 1; ring <= ringCount; ring++)
            {
                float requestedRadius = Mathf.Min(ring * sampleSpacingMeters, localRadiusMeters);
                for (int direction = 0; direction < sampleDirectionsPerRing; direction++)
                {
                    int ordinal = sampleOrdinal++;
                    PEGANpcPlanningTargetId targetId = PEGANpcPlanningTargetId.ForInvestigationSample(
                        continuation.SubjectActorId,
                        continuation.ContactLossObservationTimeSeconds,
                        ordinal);
                    float angleRadians = 2f * Mathf.PI * direction / sampleDirectionsPerRing;
                    Vector3 requestedPosition = continuation.LastKnownPosition + new Vector3(
                        Mathf.Cos(angleRadians) * requestedRadius,
                        0f,
                        Mathf.Sin(angleRadians) * requestedRadius);
                    if (!NavMesh.SamplePosition(
                            requestedPosition,
                            out NavMeshHit hit,
                            sampleProjectionDistanceMeters,
                            filter))
                    {
                        projectionRejected++;
                        continue;
                    }

                    if (!IsCandidatePositionWithinBounds(continuation.LastKnownPosition, hit.position))
                    {
                        boundsRejected++;
                        continue;
                    }

                    if (!RequiresPhysicalReposition(hit.position))
                    {
                        insufficientReposition++;
                        continue;
                    }

                    if (ContainsDuplicatePosition(projectedPositions, hit.position))
                    {
                        duplicatePosition++;
                        continue;
                    }

                    projectedPositions.Add(hit.position);
                    if (ContainsTarget(attemptedTargets, targetId))
                    {
                        previouslyAttempted++;
                        continue;
                    }

                    if (!TryCalculateBoundedRoute(
                            continuation.LastKnownPosition,
                            hit.position,
                            out PEGANpcNavigationRouteEstimate route,
                            out float maximumCornerDistanceMeters,
                            out string routeDiagnostic))
                    {
                        routeRejected++;
                        if (string.IsNullOrEmpty(firstRouteDiagnostic))
                        {
                            firstRouteDiagnostic = routeDiagnostic;
                        }

                        continue;
                    }

                    if (route.RouteLengthMeters > maximumRouteLengthMeters + PositionComparisonEpsilonMeters ||
                        maximumCornerDistanceMeters > localRadiusMeters + PositionComparisonEpsilonMeters)
                    {
                        routeRejected++;
                        if (string.IsNullOrEmpty(firstRouteDiagnostic))
                        {
                            firstRouteDiagnostic = "The complete route exceeds the configured local radius or route length.";
                        }

                        continue;
                    }

                    PEGANpcPlanningTarget target = PEGANpcPlanningTarget.FromLocalObservationPosition(
                        targetId,
                        hit.position,
                        continuation.PositionEvidenceTimeSeconds,
                        maximumCornerDistanceMeters);
                    var candidate = new PEGANpcPlanningActionCandidate(
                        Contract,
                        this,
                        targetId,
                        PEGAActorMovementSpeedRegime.Pursuit,
                        route,
                        IntrinsicDurationSeconds);
                    targetList.Add(target);
                    candidateList.Add(candidate);
                }
            }

            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());
            diagnostic = targetList.Count == 0
                ? $"No unattempted local position has a complete NavMesh route within the configured bounds. " +
                  $"samples={sampleOrdinal}, projectionRejected={projectionRejected}, boundsRejected={boundsRejected}, " +
                  $"insufficientReposition={insufficientReposition}, duplicatePosition={duplicatePosition}, " +
                  $"previouslyAttempted={previouslyAttempted}, routeRejected={routeRejected}, " +
                  $"firstRouteDiagnostic={firstRouteDiagnostic}"
                : string.Empty;
            return true;
        }

        public override bool TryCreatePlanningCandidates(
            PEGANpcActionPlanningContext context,
            out IReadOnlyList<PEGANpcPlanningTarget> targets,
            out IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
            out string diagnostic)
        {
            if (!context.PlanContinuation.HasValue)
            {
                targets = Array.AsReadOnly(Array.Empty<PEGANpcPlanningTarget>());
                candidates = Array.AsReadOnly(Array.Empty<PEGANpcPlanningActionCandidate>());
                diagnostic = string.Empty;
                return true;
            }

            bool created = TryCreatePlanningCandidates(
                context.PlanContinuation.Value,
                context.AttemptedInvestigationTargets,
                out targets,
                out candidates,
                out diagnostic);
            return created && (candidates.Count > 0 || string.IsNullOrWhiteSpace(diagnostic));
        }

        public override bool CanOfferCandidate(
            PEGANpcPlanningActionCandidate candidate,
            PEGANpcPlanContinuation? continuation) =>
            CandidateMatchesContract(candidate) && continuation.HasValue &&
            continuation.Value.LastKnownPositionReached &&
            candidate.TargetId.IdentityKind == PEGANpcPlanningTargetIdentityKind.InvestigationSample;

        public bool ShouldRecordAttemptedTarget(PEGANpcPlanningActionCandidate candidate) =>
            CandidateMatchesContract(candidate) &&
            candidate.TargetId.IdentityKind == PEGANpcPlanningTargetIdentityKind.InvestigationSample;

        public bool IsRelevantKnowledgeChange(PEGANpcKnowledgeFactChangedEvent change)
        {
            if (change == null || !change.HasValueOrStatusChanged ||
                !_hasExecutionContinuation ||
                !change.SubjectActorId.HasValue ||
                change.SubjectActorId.Value != _executionContinuation.SubjectActorId)
            {
                return false;
            }

            return change.FactKind == PEGANpcKnowledgeFactKind.ObservedCharacterIdentity ||
                change.FactKind == PEGANpcKnowledgeFactKind.CharacterVisualContact ||
                change.FactKind == PEGANpcKnowledgeFactKind.LastObservedCharacterPosition;
        }

        public override bool IsCurrentPlanValid(
            PEGANpcPlanSnapshot plan,
            PEGANpcPlanContinuation? continuation,
            out Vector3 currentKnownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            currentKnownPosition = default;
            evidenceTimeSeconds = 0d;
            diagnostic = string.Empty;
            if (!continuation.HasValue || !IsLocalTargetUsable(
                    plan,
                    continuation.Value,
                    out diagnostic,
                    requireFurtherPhysicalMovement: false))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "Investigation requires its active Knowledge-backed pursuit continuation."
                    : diagnostic;
                return false;
            }

            currentKnownPosition = continuation.Value.LastKnownPosition;
            evidenceTimeSeconds = continuation.Value.PositionEvidenceTimeSeconds;
            return true;
        }

        public override bool TryStart(
            PEGANpcPlanSnapshot plan,
            PEGANpcPlanContinuation? continuation,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!continuation.HasValue || !IsLocalTargetUsable(
                    plan,
                    continuation.Value,
                    out diagnostic,
                    requireFurtherPhysicalMovement: true))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "Investigation requires a current physically actionable local target in its active pursuit continuation."
                    : diagnostic;
                RejectStart(diagnostic);
                return false;
            }

            return TryStart(plan, continuation.Value, out diagnostic);
        }

        public bool TryStart(
            PEGANpcPlanSnapshot selectedPlan,
            PEGANpcPlanContinuation continuation,
            out string diagnostic)
        {
            if (!IsLocalTargetUsable(selectedPlan, continuation, out diagnostic))
            {
                RejectStart(diagnostic);
                return false;
            }

            _executionContinuation = continuation;
            _executionTargetId = selectedPlan.TargetId;
            _hasExecutionContinuation = true;
            bool started = TryStartNavigation(
                selectedPlan.TargetId,
                selectedPlan.TargetPosition,
                PEGAActorMovementSpeedRegime.Pursuit,
                out diagnostic);
            if (!started && Outcome != PEGANpcActionOutcome.Running)
            {
                _hasExecutionContinuation = false;
                _executionContinuation = default;
                _executionTargetId = default;
            }

            return started;
        }

        protected override void Awake()
        {
            base.Awake();
            _knowledgeState = GetComponent<PEGANpcKnowledgeState>();
            _approachAction = FindLostContactPursuitProvider();
            _agent = GetComponent<NavMeshAgent>();
        }

        private IPEGANpcLastKnownTargetContinuationProvider FindLostContactPursuitProvider()
        {
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is IPEGANpcLastKnownTargetContinuationProvider provider)
                {
                    return provider;
                }
            }

            return null;
        }

        protected override void OnDisable()
        {
            _hasExecutionContinuation = false;
            _executionContinuation = default;
            _executionTargetId = default;
            base.OnDisable();
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_hasExecutionContinuation ||
                !TryValidateContinuation(_executionContinuation, out diagnostic))
            {
                if (string.IsNullOrEmpty(diagnostic))
                {
                    diagnostic = "The local Action has no active lost-contact continuation.";
                }

                return false;
            }

            if (!IsCurrentExecutionTargetWithinOpportunity(_executionTargetId, _executionContinuation))
            {
                diagnostic = "The active local Target no longer belongs to the live lost-contact opportunity.";
                return false;
            }

            return true;
        }

        protected override bool IsExpectedPreconditionInvalidation() =>
            _hasExecutionContinuation &&
            _executionContinuation.IsValid &&
            _executionContinuation.LastKnownPositionReached &&
            _knowledgeState != null && _knowledgeState.gameObject == gameObject &&
            _approachAction != null &&
            IsCurrentExecutionTargetWithinOpportunity(_executionTargetId, _executionContinuation) &&
            _approachAction.IsContinuationSupersededByNewRecognition(
                _executionContinuation.ToIntent());

        private bool TryValidateContinuation(
            PEGANpcPlanContinuation continuation,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!continuation.IsValid ||
                !continuation.LastKnownPositionReached ||
                _knowledgeState == null || _knowledgeState.gameObject != gameObject ||
                _approachAction == null)
            {
                diagnostic = "A valid continuation that physically reached the last-known position and this NPC's Knowledge State/Approach Action are required.";
                return false;
            }

            if (!_approachAction.IsCurrentContinuationIntent(continuation.ToIntent(), out diagnostic))
            {
                return false;
            }

            return true;
        }

        private bool IsCurrentExecutionTargetWithinOpportunity(
            PEGANpcPlanningTargetId targetId,
            PEGANpcPlanContinuation continuation) =>
            targetId.IdentityKind == PEGANpcPlanningTargetIdentityKind.InvestigationSample &&
            targetId.InvestigationSubjectActorId == continuation.SubjectActorId &&
            Math.Abs(targetId.InvestigationLossTimeSeconds - continuation.ContactLossObservationTimeSeconds) <= 0.000001d;

        private bool ValidateConfiguration(out string diagnostic)
        {
            if (!IsPositiveFinite(localRadiusMeters) ||
                !IsPositiveFinite(maximumRouteLengthMeters) ||
                !IsPositiveFinite(minimumPhysicalRepositionMeters) ||
                !IsPositiveFinite(sampleSpacingMeters) ||
                !IsPositiveFinite(sampleProjectionDistanceMeters) ||
                !IsNonNegativeFinite(maximumVerticalDifferenceMeters) ||
                sampleDirectionsPerRing < 4 || sampleDirectionsPerRing > 16 ||
                Mathf.CeilToInt(localRadiusMeters / sampleSpacingMeters) > 32)
            {
                diagnostic = "Investigation bounds and minimum physical reposition must be finite and positive, with 4-16 directions and at most 32 radial rings.";
                return false;
            }

            if (_knowledgeState == null || _approachAction == null || _agent == null)
            {
                diagnostic = "Knowledge State, Approach Action and NavMeshAgent must exist on this NPC root.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private NavMeshQueryFilter CreateQueryFilter() => new NavMeshQueryFilter
        {
            agentTypeID = _agent.agentTypeID,
            areaMask = _agent.areaMask
        };

        private bool TryCalculateBoundedRoute(
            Vector3 anchor,
            Vector3 targetPosition,
            out PEGANpcNavigationRouteEstimate estimate,
            out float maximumCornerDistanceMeters,
            out string diagnostic)
        {
            estimate = default;
            maximumCornerDistanceMeters = 0f;
            diagnostic = string.Empty;
            if (_agent == null || !_agent.isOnNavMesh)
            {
                diagnostic = "The NPC NavMeshAgent is not on the baked NavMesh.";
                return false;
            }

            NavMeshPath path = new NavMeshPath();
            if (!_agent.CalculatePath(targetPosition, path) ||
                path.status != NavMeshPathStatus.PathComplete ||
                path.corners == null || path.corners.Length < 2)
            {
                diagnostic = $"The local sample route is not complete ({path.status}).";
                return false;
            }

            for (int index = 0; index < path.corners.Length; index++)
            {
                Vector3 corner = path.corners[index];
                float cornerDistanceMeters = PlanarDistance(anchor, corner);
                maximumCornerDistanceMeters = Mathf.Max(maximumCornerDistanceMeters, cornerDistanceMeters);
                if (cornerDistanceMeters > localRadiusMeters + PositionComparisonEpsilonMeters ||
                    Mathf.Abs(corner.y - anchor.y) > maximumVerticalDifferenceMeters + PositionComparisonEpsilonMeters)
                {
                    diagnostic = $"Route corner {index} leaves the configured local radius or vertical band.";
                    return false;
                }
            }

            if (maximumCornerDistanceMeters > localRadiusMeters + PositionComparisonEpsilonMeters)
            {
                diagnostic = "The complete route exceeds the local radius around the last-known position.";
                return false;
            }

            if (!TryEstimateNavigationRoute(
                    targetPosition,
                    PEGAActorMovementSpeedRegime.Pursuit,
                    out estimate,
                    out diagnostic))
            {
                return false;
            }

            if (estimate.Status != PEGANpcPlanningRouteStatus.Complete ||
                estimate.AgentTypeId != _agent.agentTypeID ||
                estimate.AreaMask != _agent.areaMask ||
                estimate.RouteLengthMeters > maximumRouteLengthMeters + PositionComparisonEpsilonMeters)
            {
                diagnostic = "The route estimate is incomplete or exceeds the configured route length.";
                return false;
            }

            return true;
        }

        private bool IsCandidatePositionWithinBounds(Vector3 anchor, Vector3 position) =>
            IsFinite(position) &&
            Mathf.Abs(position.y - anchor.y) <= maximumVerticalDifferenceMeters + PositionComparisonEpsilonMeters &&
            PlanarDistance(anchor, position) <= localRadiusMeters + PositionComparisonEpsilonMeters;

        private bool RequiresPhysicalReposition(Vector3 targetPosition) =>
            PlanarDistance(transform.position, targetPosition) >
            ArrivalRadiusMeters + minimumPhysicalRepositionMeters;

        private static bool ContainsTarget(
            IReadOnlyCollection<PEGANpcPlanningTargetId> targets,
            PEGANpcPlanningTargetId targetId)
        {
            if (targets == null)
            {
                return false;
            }

            foreach (PEGANpcPlanningTargetId attempted in targets)
            {
                if (attempted == targetId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDuplicatePosition(
            IReadOnlyList<Vector3> positions,
            Vector3 position)
        {
            for (int index = 0; index < positions.Count; index++)
            {
                Vector3 existing = positions[index];
                if (PlanarDistance(existing, position) < PositionDeduplicationToleranceMeters &&
                    Mathf.Abs(existing.y - position.y) < PositionDeduplicationToleranceMeters)
                {
                    return true;
                }
            }

            return false;
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            Vector3 delta = left - right;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsPositiveFinite(float value) => value > 0f && IsFinite(value);

        private static bool IsNonNegativeFinite(float value) => value >= 0f && IsFinite(value);
    }
}

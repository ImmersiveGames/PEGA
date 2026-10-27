using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using PEGA.Actors;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace PEGA.NPC
{
    [CreateAssetMenu(fileName = "InvestigateDetectedPresence", menuName = "PEGA/NPC/Investigation/Investigate Detected Presence")]
    public sealed class PEGANpcInvestigatePerceivedAreaPresenceAction : PEGANpcNavigationAction,
        IPEGANpcKnowledgeChangeRelevanceProvider,
        IPEGANpcAttemptedTargetProvider
    {
        private const float PositionComparisonEpsilonMeters = 0.001f;
        private static readonly Vector3[] SearchDirections =
        {
            Vector3.forward,
            Vector3.right,
            Vector3.back,
            Vector3.left
        };

        [Header("Search Area")]
        [SerializeField, Min(0.25f)]
        [FormerlySerializedAs("searchRadiusMeters")]
        [Tooltip("Radius around the observer's known position to search for detected presence (m).")]
        private float presenceSearchRadiusMeters = 3f;

        [SerializeField, Min(0.1f)]
        [FormerlySerializedAs("maximumRouteLengthMeters")]
        [Tooltip("Maximum travel distance to each search point (m).")]
        private float maximumTravelDistanceMeters = 6f;

        [SerializeField, Min(0.05f)]
        [FormerlySerializedAs("minimumPhysicalRepositionMeters")]
        [Tooltip("Minimum physical movement for arrival to count as an investigation (m).")]
        private float minimumSearchMovementMeters = 0.75f;

        [SerializeField, Min(0.05f), HideInInspector]
        [Tooltip("Distância máxima para projetar os pontos de busca na NavMesh compatível (m).")]
        private float sampleProjectionDistanceMeters = 0.5f;

        [SerializeField, Min(0f)]
        [FormerlySerializedAs("maximumVerticalDifferenceMeters")]
        [Tooltip("Maximum height difference between the observer's known position and a search point (m).")]
        private float maximumHeightDifferenceMeters = 0.35f;

        private PEGANpcKnowledgeState _knowledgeState;
        private NavMeshAgent _agent;
        private ActorId _candidateTargetActorId;
        private ActorId _executionTargetActorId;
        private PEGANpcPlanSnapshot _executionPlan;
        private Vector3 _executionStartPosition;
        private bool _hasExecutionPlan;

        protected override PEGANpcActionEffectKind ActionEffectKind =>
            PEGANpcActionEffectKind.ReachLocalObservationPosition;

        public float SearchRadiusMeters => presenceSearchRadiusMeters;
        public float MaximumVerticalDifferenceMeters => maximumHeightDifferenceMeters;
        public float MinimumPhysicalRepositionMeters => minimumSearchMovementMeters;

        public override PEGANpcActionContract Contract => new PEGANpcActionContract(
            PEGANpcPlanningActionId.InvestigatePerceivedAreaPresence,
            ActionEffectKind,
            PEGANpcPlanningTargetKind.LocalObservationPosition,
            true,
            "NPC began investigating an unidentified radial presence.");

        protected override PEGANpcCausalStepEvaluationStatus EvaluateCausalNavigationConstraints(
            PEGANpcGoapKnownTarget target,
            Vector3 sourcePosition,
            PEGANpcNavigationRouteEstimate routeEstimate,
            out string diagnostic)
        {
            if (!RequiresPhysicalReposition(target.Target.KnownPosition, sourcePosition))
            {
                diagnostic = "The projected position is already within the investigation arrival and reposition thresholds.";
                return PEGANpcCausalStepEvaluationStatus.Unavailable;
            }

            if (routeEstimate.RouteLengthMeters > maximumTravelDistanceMeters + PositionComparisonEpsilonMeters)
            {
                diagnostic = "The route from the projected position exceeds the configured investigation route bound.";
                return PEGANpcCausalStepEvaluationStatus.Unavailable;
            }

            diagnostic = string.Empty;
            return PEGANpcCausalStepEvaluationStatus.Available;
        }

        public override bool TryCreatePlanningCandidates(
            PEGANpcActionPlanningContext context,
            out IReadOnlyList<PEGANpcPlanningTarget> targets,
            out IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
            out string diagnostic)
        {
            List<PEGANpcPlanningTarget> targetList = new List<PEGANpcPlanningTarget>();
            List<PEGANpcPlanningActionCandidate> candidateList =
                new List<PEGANpcPlanningActionCandidate>();
            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());
            diagnostic = string.Empty;

            if (context.PlanContinuation.HasValue ||
                context.HasPerceptionBackedTargetContact ||
                !context.TargetActorId.IsValid)
            {
                return true;
            }

            ActorId targetActorId = context.TargetActorId;
            _candidateTargetActorId = targetActorId;
            if (!TryGetCurrentPresence(
                    out PEGANpcPerceivedAreaPresence presence,
                    out double evidenceTimeSeconds,
                    out diagnostic))
            {
                return string.IsNullOrWhiteSpace(diagnostic);
            }

            if (IsCharacterIdentityKnownByPerception(_candidateTargetActorId))
            {
                return true;
            }

            if (!ValidateConfiguration(presence, out diagnostic))
            {
                return false;
            }

            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            {
                diagnostic = "The NPC NavMeshAgent must be enabled and placed on its baked NavMesh to investigate radial presence.";
                return false;
            }

            if (!TryDeferNavigationRoute(
                    PEGAActorMovementSpeedRegime.Movement,
                    out PEGANpcNavigationRouteEstimate deferredRoute,
                    out diagnostic))
            {
                return false;
            }

            if (_hasExecutionPlan &&
                Outcome == PEGANpcActionOutcome.Running &&
                _executionPlan.TargetId.IdentityKind ==
                    PEGANpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample &&
                _executionPlan.TargetId.PerceivedAreaEvidenceTimeSeconds == evidenceTimeSeconds &&
                IsCandidatePositionWithinBounds(presence, _executionPlan.TargetPosition) &&
                TryEstimateNavigationRoute(
                    _executionPlan.TargetPosition,
                    PEGAActorMovementSpeedRegime.Movement,
                    out PEGANpcNavigationRouteEstimate activeRoute,
                    out _) &&
                activeRoute.Status == PEGANpcPlanningRouteStatus.Complete &&
                activeRoute.RouteLengthMeters <= maximumTravelDistanceMeters + PositionComparisonEpsilonMeters)
            {
                PEGANpcPlanningTarget activeTarget = PEGANpcPlanningTarget.FromLocalObservationPosition(
                    _executionPlan.TargetId,
                    _executionPlan.TargetPosition,
                    evidenceTimeSeconds,
                    presenceSearchRadiusMeters);
                targetList.Add(activeTarget);
                candidateList.Add(new PEGANpcPlanningActionCandidate(
                    Contract,
                    this,
                    _executionPlan.TargetId,
                    PEGAActorMovementSpeedRegime.Movement,
                    activeRoute,
                    IntrinsicDurationSeconds));
                targets = Array.AsReadOnly(targetList.ToArray());
                candidates = Array.AsReadOnly(candidateList.ToArray());
                return true;
            }

            HashSet<PEGANpcPlanningTargetId> attemptedTargets =
                new HashSet<PEGANpcPlanningTargetId>(context.AttemptedInvestigationTargets);
            List<Vector3> projectedPositions = new List<Vector3>(SearchDirections.Length);
            NavMeshQueryFilter filter = CreateQueryFilter();
            int projectionRejected = 0;
            int boundsRejected = 0;
            int duplicatePosition = 0;
            int previouslyAttempted = 0;

            for (int sampleOrdinal = 0; sampleOrdinal < SearchDirections.Length; sampleOrdinal++)
            {
                PEGANpcPlanningTargetId targetId =
                    PEGANpcPlanningTargetId.ForPerceivedAreaInvestigationSample(
                        evidenceTimeSeconds,
                        sampleOrdinal);
                if (attemptedTargets.Contains(targetId))
                {
                    previouslyAttempted++;
                    continue;
                }

                Vector3 requestedPosition = presence.ObserverPosition +
                    SearchDirections[sampleOrdinal] * presenceSearchRadiusMeters;
                if (!NavMesh.SamplePosition(
                        requestedPosition,
                        out NavMeshHit hit,
                        sampleProjectionDistanceMeters,
                        filter))
                {
                    projectionRejected++;
                    continue;
                }

                if (!IsCandidatePositionWithinBounds(presence, hit.position))
                {
                    boundsRejected++;
                    continue;
                }

                if (ContainsDuplicatePosition(projectedPositions, hit.position))
                {
                    duplicatePosition++;
                    continue;
                }

                projectedPositions.Add(hit.position);
                PEGANpcPlanningTarget target = PEGANpcPlanningTarget.FromLocalObservationPosition(
                    targetId,
                    hit.position,
                    evidenceTimeSeconds,
                    presenceSearchRadiusMeters);
                PEGANpcPlanningActionCandidate candidate = new PEGANpcPlanningActionCandidate(
                    Contract,
                    this,
                    targetId,
                    PEGAActorMovementSpeedRegime.Movement,
                    deferredRoute,
                    IntrinsicDurationSeconds);
                targetList.Add(target);
                candidateList.Add(candidate);
            }

            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());
            if (candidateList.Count == 0)
            {
                diagnostic = "No unattempted radial-area search position satisfies the configured sample bounds. " +
                    $"projectionRejected={projectionRejected}, boundsRejected={boundsRejected}, " +
                    $"duplicatePosition={duplicatePosition}, previouslyAttempted={previouslyAttempted}";
            }

            return candidateList.Count > 0 || string.IsNullOrWhiteSpace(diagnostic);
        }

        public override bool CanOfferCandidate(
            PEGANpcPlanningActionCandidate candidate,
            PEGANpcPlanContinuation? continuation) =>
            !continuation.HasValue &&
            CandidateMatchesContract(candidate) &&
            candidate.TargetId.IdentityKind ==
                PEGANpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample;

        public bool ShouldRecordAttemptedTarget(PEGANpcPlanningActionCandidate candidate) =>
            CandidateMatchesContract(candidate) &&
            candidate.TargetId.IdentityKind ==
                PEGANpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample;

        public bool IsRelevantKnowledgeChange(PEGANpcKnowledgeFactChangedEvent change)
        {
            if (change == null || !change.HasValueOrStatusChanged)
            {
                return false;
            }

            if (change.FactKind == PEGANpcKnowledgeFactKind.PerceivedAreaPresence)
            {
                return true;
            }

            return _executionTargetActorId.IsValid &&
                   change.SubjectActorId.HasValue &&
                   change.SubjectActorId.Value == _executionTargetActorId &&
                   (change.FactKind == PEGANpcKnowledgeFactKind.ObservedCharacterIdentity ||
                    change.FactKind == PEGANpcKnowledgeFactKind.CharacterVisualContact);
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
            ActorId subjectActorId = _executionTargetActorId.IsValid
                ? _executionTargetActorId
                : _candidateTargetActorId;
            if (continuation.HasValue ||
                !TryValidatePlan(plan, subjectActorId, false, out currentKnownPosition,
                    out evidenceTimeSeconds, out diagnostic))
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "Radial presence investigation requires current positive Perception evidence and no Perception-backed identity.";
                }

                return false;
            }

            return true;
        }

        public override bool TryStart(
            PEGANpcPlanSnapshot plan,
            PEGANpcPlanContinuation? continuation,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (continuation.HasValue ||
                !TryValidatePlan(plan, _candidateTargetActorId, true, out _, out _, out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "Radial presence investigation cannot start during a pursuit continuation or without current evidence."
                    : diagnostic;
                RejectStart(diagnostic);
                return false;
            }

            _executionPlan = plan;
            _executionTargetActorId = _candidateTargetActorId;
            _executionStartPosition = ActorTransform.position;
            _hasExecutionPlan = true;
            bool started = TryStartNavigation(
                plan.TargetId,
                plan.TargetPosition,
                PEGAActorMovementSpeedRegime.Movement,
                out diagnostic);
            if (!started && Outcome != PEGANpcActionOutcome.Running)
            {
                ClearExecutionEvidence();
            }

            return started;
        }

        protected override bool OnRuntimeInitialize(out string diagnostic)
        {
            if (!base.OnRuntimeInitialize(out diagnostic))
            {
                return false;
            }

            _knowledgeState = RuntimeContext.KnowledgeState;
            _agent = RuntimeContext.NavigationAgent;
            if (_knowledgeState == null || _knowledgeState.gameObject != ActorRoot || _agent == null)
            {
                diagnostic = "Radial investigation requires the Actor's Knowledge State and NavMesh Agent.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        protected override void OnRuntimeDisabled()
        {
            ClearExecutionEvidence();
            _candidateTargetActorId = default;
            base.OnRuntimeDisabled();
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic)
        {
            diagnostic = string.Empty;
            return _hasExecutionPlan &&
                   IsCurrentPresenceUsable(_executionPlan, _executionTargetActorId, out diagnostic);
        }

        protected override bool IsActionSpecificEffectObserved(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_hasExecutionPlan ||
                !IsCurrentPresenceUsable(_executionPlan, _executionTargetActorId, out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The radial evidence stopped being current before the investigation point was reached."
                    : diagnostic;
                return false;
            }

            float physicalRepositionMeters = PlanarDistance(ActorTransform.position, _executionStartPosition);
            if (physicalRepositionMeters + PositionComparisonEpsilonMeters < minimumSearchMovementMeters)
            {
                diagnostic = "The NPC did not physically reposition far enough for this investigation attempt to count as work.";
                return false;
            }

            return true;
        }

        private bool TryValidatePlan(
            PEGANpcPlanSnapshot plan,
            ActorId subjectActorId,
            bool requirePhysicalReposition,
            out Vector3 currentKnownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            currentKnownPosition = default;
            evidenceTimeSeconds = 0d;
            if (!TryGetCurrentPresence(
                    out PEGANpcPerceivedAreaPresence presence,
                    out double currentEvidenceTimeSeconds,
                    out diagnostic))
            {
                return false;
            }

            if (!ValidateConfiguration(presence, out diagnostic) ||
                !plan.TargetId.IsValid ||
                !plan.ActionId.IsValid ||
                !IsFinite(plan.TargetPosition) ||
                !IsNonNegativeFinite(plan.EvidenceTimeSeconds) ||
                plan.Purpose != PEGANpcPlanPurpose.DesireGoal ||
                plan.ActionId != Contract.ActionId ||
                plan.PredictedEffect != ActionEffectKind ||
                plan.TargetKind != Contract.TargetKind ||
                plan.TargetProvenance != PEGANpcPlanningTargetProvenance.DerivedFromKnowledge ||
                plan.TargetId.IdentityKind !=
                    PEGANpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample ||
                plan.TargetId.PerceivedAreaEvidenceTimeSeconds != currentEvidenceTimeSeconds ||
                Math.Abs(plan.EvidenceTimeSeconds - currentEvidenceTimeSeconds) > 0.000001d ||
                plan.TargetId.PerceivedAreaSampleOrdinal < 0 ||
                plan.TargetId.PerceivedAreaSampleOrdinal >= SearchDirections.Length ||
                Mathf.Abs(plan.MaximumRouteDistanceFromAnchorMeters - presenceSearchRadiusMeters) >
                    PositionComparisonEpsilonMeters ||
                !IsCandidatePositionWithinBounds(presence, plan.TargetPosition))
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The plan Target does not belong to the current Perception-backed radial presence evidence.";
                }

                return false;
            }

            if (!subjectActorId.IsValid || IsCharacterIdentityKnownByPerception(subjectActorId))
            {
                diagnostic = "The radial presence has already been identified by Perception.";
                return false;
            }

            if (requirePhysicalReposition &&
                !RequiresPhysicalReposition(plan.TargetPosition, ActorTransform.position))
            {
                diagnostic = "The selected search position must require meaningful physical movement beyond the arrival radius.";
                return false;
            }

            if (!TryEstimateNavigationRoute(
                    plan.TargetPosition,
                    PEGAActorMovementSpeedRegime.Movement,
                    out PEGANpcNavigationRouteEstimate route,
                    out diagnostic) ||
                route.Status != PEGANpcPlanningRouteStatus.Complete ||
                route.RouteLengthMeters > maximumTravelDistanceMeters + PositionComparisonEpsilonMeters)
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The selected investigation position no longer has a complete route within the configured bound.";
                }

                return false;
            }

            currentKnownPosition = presence.ObserverPosition;
            evidenceTimeSeconds = currentEvidenceTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        private bool IsCurrentPresenceUsable(
            PEGANpcPlanSnapshot plan,
            ActorId subjectActorId,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!TryGetCurrentPresence(
                    out PEGANpcPerceivedAreaPresence presence,
                    out double currentEvidenceTimeSeconds,
                    out diagnostic))
            {
                return false;
            }

            if (plan.TargetId.IdentityKind !=
                    PEGANpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample ||
                plan.TargetId.PerceivedAreaEvidenceTimeSeconds != currentEvidenceTimeSeconds ||
                Math.Abs(plan.EvidenceTimeSeconds - currentEvidenceTimeSeconds) > 0.000001d ||
                !IsCandidatePositionWithinBounds(presence, plan.TargetPosition) ||
                !subjectActorId.IsValid ||
                IsCharacterIdentityKnownByPerception(subjectActorId))
            {
                diagnostic = "Current radial evidence no longer supports this unidentified-presence investigation.";
                return false;
            }

            return true;
        }

        private bool TryGetCurrentPresence(
            out PEGANpcPerceivedAreaPresence presence,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            presence = default;
            evidenceTimeSeconds = 0d;
            if (_knowledgeState == null || _knowledgeState.gameObject != ActorRoot)
            {
                diagnostic = "Knowledge State must be present on this NPC root.";
                return false;
            }

            PEGANpcKnowledgeFact<PEGANpcPerceivedAreaPresence> fact =
                _knowledgeState.GetPerceivedAreaPresence();
            if (!fact.TryGetValue(out presence) ||
                !presence.IsPresent ||
                !fact.HasObservationTime ||
                (fact.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                diagnostic = string.Empty;
                return false;
            }

            evidenceTimeSeconds = fact.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        private bool IsCharacterIdentityKnownByPerception(ActorId subjectActorId)
        {
            if (_knowledgeState == null || !subjectActorId.IsValid)
            {
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                _knowledgeState.GetObservedCharacterIdentity(subjectActorId);
            return identity.TryGetValue(out _) &&
                   (identity.Origins & PEGANpcEvidenceOrigin.Perception) != 0;
        }

        private bool ValidateConfiguration(
            PEGANpcPerceivedAreaPresence presence,
            out string diagnostic)
        {
            if (!IsPositiveFinite(presenceSearchRadiusMeters) ||
                !IsNonNegativeFinite(ArrivalRadiusMeters) ||
                !IsPositiveFinite(maximumTravelDistanceMeters) ||
                !IsPositiveFinite(minimumSearchMovementMeters) ||
                !IsPositiveFinite(sampleProjectionDistanceMeters) ||
                !IsNonNegativeFinite(maximumHeightDifferenceMeters) ||
                presenceSearchRadiusMeters > presence.PerceivedAreaRadiusMeters + PositionComparisonEpsilonMeters ||
                presenceSearchRadiusMeters <= ArrivalRadiusMeters + minimumSearchMovementMeters)
            {
                diagnostic = "Search radius must fit inside the recorded perceived area and exceed arrival radius plus minimum physical reposition; all distances must be finite and positive.";
                return false;
            }

            if (_knowledgeState == null || _agent == null)
            {
                diagnostic = "Knowledge State and NavMeshAgent must exist on this NPC root.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool IsCandidatePositionWithinBounds(
            PEGANpcPerceivedAreaPresence presence,
            Vector3 targetPosition)
        {
            if (!IsFinite(targetPosition) ||
                Mathf.Abs(targetPosition.y - presence.ObserverPosition.y) >
                    maximumHeightDifferenceMeters + PositionComparisonEpsilonMeters)
            {
                return false;
            }

            float planarDistance = PlanarDistance(targetPosition, presence.ObserverPosition);
            return planarDistance <= presenceSearchRadiusMeters + PositionComparisonEpsilonMeters &&
                   planarDistance <= presence.PerceivedAreaRadiusMeters + PositionComparisonEpsilonMeters;
        }

        private bool RequiresPhysicalReposition(Vector3 targetPosition, Vector3 actorPosition) =>
            PlanarDistance(targetPosition, actorPosition) >
            ArrivalRadiusMeters + minimumSearchMovementMeters + PositionComparisonEpsilonMeters;

        private bool ContainsDuplicatePosition(IReadOnlyList<Vector3> positions, Vector3 candidate)
        {
            for (int index = 0; index < positions.Count; index++)
            {
                if (PlanarDistance(positions[index], candidate) <= PositionComparisonEpsilonMeters)
                {
                    return true;
                }
            }

            return false;
        }

        private NavMeshQueryFilter CreateQueryFilter() => new NavMeshQueryFilter
        {
            agentTypeID = _agent.agentTypeID,
            areaMask = _agent.areaMask
        };

        private void ClearExecutionEvidence()
        {
            _executionTargetActorId = default;
            _executionPlan = default;
            _executionStartPosition = default;
            _hasExecutionPlan = false;
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            Vector3 delta = left - right;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(double value) =>
            value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

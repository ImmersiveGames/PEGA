using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using PEGA.Movement.Actor;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
namespace PEGA.NPC.Actions.Runtime
{
    [CreateAssetMenu(fileName = "InvestigateDetectedPresence", menuName = "PEGA/NPC/Investigation/Investigate Detected Presence")]
    public sealed class PegaNpcInvestigatePerceivedAreaPresenceAction : PegaNpcActionProviderAsset,
        IPegaNpcKnowledgeChangeRelevanceProvider,
        IPegaNpcAttemptedTargetProvider
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

        private PegaNpcKnowledgeState _knowledgeState;
        private NavMeshAgent _agent;
        private ActorId _candidateTargetActorId;
        private ActorId _executionTargetActorId;
        private PegaNpcPlanSnapshot _executionPlan;
        private Vector3 _executionStartPosition;
        private bool _hasExecutionPlan;
        private bool _preconditionInvalidatedByPlayerRecognition;

        protected override PegaNpcActionEffectKind ActionEffectKind =>
            PegaNpcActionEffectKind.ReachLocalObservationPosition;

        public float SearchRadiusMeters => presenceSearchRadiusMeters;
        public float MaximumVerticalDifferenceMeters => maximumHeightDifferenceMeters;
        public float MinimumPhysicalRepositionMeters => minimumSearchMovementMeters;

        public override PegaNpcActionContract Contract => new PegaNpcActionContract(
            PegaNpcPlanningActionId.InvestigatePerceivedAreaPresence,
            ActionEffectKind,
            PegaNpcPlanningTargetKind.LocalObservationPosition,
            true,
            "NPC began investigating an unidentified radial presence.");

        protected override PegaNpcCausalStepEvaluationStatus EvaluateCausalNavigationConstraints(
            PegaNpcGoapKnownTarget target,
            Vector3 sourcePosition,
            PegaNpcNavigationRouteEstimate routeEstimate,
            out string diagnostic)
        {
            if (!RequiresPhysicalReposition(target.Target.KnownPosition, sourcePosition))
            {
                diagnostic = "The projected position is already within the investigation arrival and reposition thresholds.";
                return PegaNpcCausalStepEvaluationStatus.Unavailable;
            }

            if (routeEstimate.RouteLengthMeters > maximumTravelDistanceMeters + PositionComparisonEpsilonMeters)
            {
                diagnostic = "The route from the projected position exceeds the configured investigation route bound.";
                return PegaNpcCausalStepEvaluationStatus.Unavailable;
            }

            diagnostic = string.Empty;
            return PegaNpcCausalStepEvaluationStatus.Available;
        }

        public override bool TryCreatePlanningCandidates(
            PegaNpcActionPlanningContext context,
            out IReadOnlyList<PegaNpcPlanningTarget> targets,
            out IReadOnlyList<PegaNpcPlanningActionCandidate> candidates,
            out string diagnostic)
        {
            List<PegaNpcPlanningTarget> targetList = new List<PegaNpcPlanningTarget>();
            List<PegaNpcPlanningActionCandidate> candidateList =
                new List<PegaNpcPlanningActionCandidate>();
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
                    out PegaNpcPerceivedAreaPresence presence,
                    out double evidenceTimeSeconds,
                    out diagnostic))
            {
                return string.IsNullOrWhiteSpace(diagnostic);
            }

            if (HasPerceptionBackedPlayerFact(_candidateTargetActorId))
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
                    PegaActorMovementSpeedRegime.Movement,
                    out PegaNpcNavigationRouteEstimate deferredRoute,
                    out diagnostic))
            {
                return false;
            }

            if (_hasExecutionPlan &&
                Outcome == PegaNpcActionOutcome.Running &&
                _executionPlan.TargetId.IdentityKind ==
                    PegaNpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample &&
                _executionPlan.TargetId.PerceivedAreaEvidenceTimeSeconds == evidenceTimeSeconds &&
                IsCandidatePositionWithinBounds(presence, _executionPlan.TargetPosition) &&
                TryEstimateNavigationRoute(
                    _executionPlan.TargetPosition,
                    PegaActorMovementSpeedRegime.Movement,
                    out PegaNpcNavigationRouteEstimate activeRoute,
                    out _) &&
                activeRoute.Status == PegaNpcPlanningRouteStatus.Complete &&
                activeRoute.RouteLengthMeters <= maximumTravelDistanceMeters + PositionComparisonEpsilonMeters)
            {
                PegaNpcPlanningTarget activeTarget = PegaNpcPlanningTarget.FromLocalObservationPosition(
                    _executionPlan.TargetId,
                    _executionPlan.TargetPosition,
                    evidenceTimeSeconds,
                    presenceSearchRadiusMeters);
                targetList.Add(activeTarget);
                candidateList.Add(new PegaNpcPlanningActionCandidate(
                    Contract,
                    this,
                    _executionPlan.TargetId,
                    PegaActorMovementSpeedRegime.Movement,
                    activeRoute,
                    IntrinsicDurationSeconds));
                targets = Array.AsReadOnly(targetList.ToArray());
                candidates = Array.AsReadOnly(candidateList.ToArray());
                return true;
            }

            HashSet<PegaNpcPlanningTargetId> attemptedTargets =
                new HashSet<PegaNpcPlanningTargetId>(context.AttemptedInvestigationTargets);
            List<Vector3> projectedPositions = new List<Vector3>(SearchDirections.Length);
            NavMeshQueryFilter filter = CreateQueryFilter();
            int projectionRejected = 0;
            int boundsRejected = 0;
            int duplicatePosition = 0;
            int previouslyAttempted = 0;

            for (int sampleOrdinal = 0; sampleOrdinal < SearchDirections.Length; sampleOrdinal++)
            {
                PegaNpcPlanningTargetId targetId =
                    PegaNpcPlanningTargetId.ForPerceivedAreaInvestigationSample(
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
                PegaNpcPlanningTarget target = PegaNpcPlanningTarget.FromLocalObservationPosition(
                    targetId,
                    hit.position,
                    evidenceTimeSeconds,
                    presenceSearchRadiusMeters);
                PegaNpcPlanningActionCandidate candidate = new PegaNpcPlanningActionCandidate(
                    Contract,
                    this,
                    targetId,
                    PegaActorMovementSpeedRegime.Movement,
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
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcPlanContinuation? continuation) =>
            !continuation.HasValue &&
            CandidateMatchesContract(candidate) &&
            candidate.TargetId.IdentityKind ==
                PegaNpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample;

        public bool ShouldRecordAttemptedTarget(PegaNpcPlanningActionCandidate candidate) =>
            CandidateMatchesContract(candidate) &&
            candidate.TargetId.IdentityKind ==
                PegaNpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample;

        public bool IsRelevantKnowledgeChange(PegaKnowledgeFactChangedEvent change)
        {
            if (change == null || !change.HasValueOrStatusChanged)
            {
                return false;
            }

            if (change.IsFor(PegaNpcKnowledgeFacts.PerceivedAreaPresence.For(0)))
            {
                return true;
            }

            return PegaNpcKnowledgeFacts.IsCharacterFactChangeFor(
                change,
                _executionTargetActorId);
        }

        public override bool IsCurrentPlanValid(
            PegaNpcPlanSnapshot plan,
            PegaNpcPlanContinuation? continuation,
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
                    diagnostic = "Radial presence investigation requires current positive Perception evidence and no visually known Player fact.";
                }

                return false;
            }

            return true;
        }

        public override bool TryStart(
            PegaNpcPlanSnapshot plan,
            PegaNpcPlanContinuation? continuation,
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
                PegaActorMovementSpeedRegime.Movement,
                out diagnostic);
            if (!started && Outcome != PegaNpcActionOutcome.Running)
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
            _preconditionInvalidatedByPlayerRecognition = false;
            base.OnRuntimeDisabled();
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic)
        {
            _preconditionInvalidatedByPlayerRecognition = false;
            if (!_hasExecutionPlan)
            {
                diagnostic = "The radial investigation has no active execution plan.";
                return false;
            }

            bool isUsable = IsCurrentPresenceUsable(
                _executionPlan,
                _executionTargetActorId,
                out diagnostic,
                out _preconditionInvalidatedByPlayerRecognition);
            return isUsable;
        }

        protected override bool IsExpectedPreconditionInvalidation() =>
            _preconditionInvalidatedByPlayerRecognition;

        protected override bool IsActionSpecificEffectObserved(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_hasExecutionPlan ||
                !IsCurrentPresenceUsable(
                    _executionPlan,
                    _executionTargetActorId,
                    out diagnostic,
                    out _))
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
            PegaNpcPlanSnapshot plan,
            ActorId subjectActorId,
            bool requirePhysicalReposition,
            out Vector3 currentKnownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            currentKnownPosition = default;
            evidenceTimeSeconds = 0d;
            if (!TryGetCurrentPresence(
                    out PegaNpcPerceivedAreaPresence presence,
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
                plan.Purpose != PegaNpcPlanPurpose.DesireGoal ||
                plan.ActionId != Contract.ActionId ||
                plan.PredictedEffect != ActionEffectKind ||
                plan.TargetKind != Contract.TargetKind ||
                plan.TargetProvenance != PegaNpcPlanningTargetProvenance.DerivedFromKnowledge ||
                plan.TargetId.IdentityKind !=
                    PegaNpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample ||
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

            if (!subjectActorId.IsValid || HasPerceptionBackedPlayerFact(subjectActorId))
            {
                diagnostic = "The current target is already known to be a Player through visual Perception.";
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
                    PegaActorMovementSpeedRegime.Movement,
                    out PegaNpcNavigationRouteEstimate route,
                    out diagnostic) ||
                route.Status != PegaNpcPlanningRouteStatus.Complete ||
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
            PegaNpcPlanSnapshot plan,
            ActorId subjectActorId,
            out string diagnostic,
            out bool invalidatedByPlayerRecognition)
        {
            diagnostic = string.Empty;
            invalidatedByPlayerRecognition = false;
            if (!TryGetCurrentPresence(
                    out PegaNpcPerceivedAreaPresence presence,
                    out double currentEvidenceTimeSeconds,
                    out diagnostic))
            {
                return false;
            }

            if (plan.TargetId.IdentityKind !=
                    PegaNpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample ||
                plan.TargetId.PerceivedAreaEvidenceTimeSeconds != currentEvidenceTimeSeconds ||
                Math.Abs(plan.EvidenceTimeSeconds - currentEvidenceTimeSeconds) > 0.000001d ||
                !IsCandidatePositionWithinBounds(presence, plan.TargetPosition) ||
                !subjectActorId.IsValid)
            {
                diagnostic = "Current radial evidence no longer matches the active investigation target.";
                return false;
            }

            if (HasPerceptionBackedPlayerFact(subjectActorId))
            {
                diagnostic = "Current radial evidence no longer supports investigation of a target whose Player status is visually known.";
                invalidatedByPlayerRecognition = true;
                return false;
            }

            return true;
        }

        private bool TryGetCurrentPresence(
            out PegaNpcPerceivedAreaPresence presence,
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

            PegaKnowledgeFact<PegaNpcPerceivedAreaPresence> fact =
                _knowledgeState.GetPerceivedAreaPresence();
            if (!fact.TryGetValue(out presence) ||
                !presence.IsPresent ||
                !fact.HasObservationTime ||
                !fact.HasOrigin(PegaNpcEvidenceOrigin.Perception))
            {
                diagnostic = string.Empty;
                return false;
            }

            evidenceTimeSeconds = fact.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        private bool HasPerceptionBackedPlayerFact(ActorId subjectActorId)
        {
            if (_knowledgeState == null || !subjectActorId.IsValid)
            {
                return false;
            }

            PegaKnowledgeFact<bool> playerFact = _knowledgeState.GetFact(
                PegaNpcKnowledgeFacts.IsPlayer.For(subjectActorId));
            return PegaNpcKnowledgeFacts.HasPerceptionBackedPlayerFact(playerFact);
        }

        private bool ValidateConfiguration(
            PegaNpcPerceivedAreaPresence presence,
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
            PegaNpcPerceivedAreaPresence presence,
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

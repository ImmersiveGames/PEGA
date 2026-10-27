using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using PEGA.Actors;
using UnityEngine;
using UnityEngine.Serialization;

namespace PEGA.NPC
{
    [CreateAssetMenu(fileName = "MoveAwayFromCharacter", menuName = "PEGA/NPC/Movement/Move Away from Character")]
    public sealed class PEGANpcAvoidKnownCharacterAction : PEGANpcNavigationAction,
        IPEGANpcKnowledgeChangeRelevanceProvider
    {
        private const float MinimumSeparationMeters = 0.1f;
        private const float SeparationTargetPrecisionMarginMeters = 0.001f;

        [System.NonSerialized]
        private PEGANpcKnowledgeState knowledgeState;

        [System.NonSerialized]
        private ActorOccurrenceReferenceBinding characterReference;

        [Header("Movement")]
        [SerializeField, Min(MinimumSeparationMeters)]
        [FormerlySerializedAs("safeSeparationDistanceMeters")]
        [Tooltip("Minimum desired distance from the character's last known position (m).")]
        private float minimumSafeDistanceMeters = 5f;

        [SerializeField, Min(MinimumSeparationMeters)]
        [FormerlySerializedAs("retreatStepDistanceMeters")]
        [Tooltip("Additional distance the NPC attempts to gain on each move (m).")]
        private float additionalDistancePerMoveMeters = 1f;

        protected override PEGANpcActionEffectKind ActionEffectKind =>
            PEGANpcActionEffectKind.ReachSafeSeparationPosition;

        public override PEGANpcActionContract Contract => new PEGANpcActionContract(
            PEGANpcPlanningActionId.MaintainSafeSeparationFromKnownCharacter,
            ActionEffectKind,
            PEGANpcPlanningTargetKind.SafeSeparationPosition,
            true,
            "NPC began moving to maintain safe separation from a known character.");

        public float SafeSeparationDistanceMeters => minimumSafeDistanceMeters;

        protected override bool OnRuntimeInitialize(out string diagnostic)
        {
            if (!base.OnRuntimeInitialize(out diagnostic))
            {
                return false;
            }

            knowledgeState = RuntimeContext.KnowledgeState;
            characterReference = RuntimeContext.ActorReference;
            if (knowledgeState == null || knowledgeState.gameObject != ActorRoot ||
                characterReference == null || characterReference.gameObject != ActorRoot)
            {
                diagnostic = "Avoid Known Character requires the Actor's Knowledge State and occurrence reference.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        public bool IsRelevantKnowledgeChange(PEGANpcKnowledgeFactChangedEvent change)
        {
            if (change == null || !change.HasValueOrStatusChanged ||
                !change.SubjectActorId.HasValue ||
                (change.FactKind != PEGANpcKnowledgeFactKind.ObservedCharacterIdentity &&
                 change.FactKind != PEGANpcKnowledgeFactKind.LastObservedCharacterPosition) ||
                !TryResolveCharacterSubject(out ActorId subjectActorId, out _))
            {
                return false;
            }

            return change.SubjectActorId.Value == subjectActorId;
        }

        public override bool TryCreatePlanningCandidates(
            PEGANpcActionPlanningContext context,
            out IReadOnlyList<PEGANpcPlanningTarget> targets,
            out IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
            out string diagnostic)
        {
            List<PEGANpcPlanningTarget> targetList = new List<PEGANpcPlanningTarget>();
            List<PEGANpcPlanningActionCandidate> candidateList = new List<PEGANpcPlanningActionCandidate>();
            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());

            if (!TryValidateConfiguration(out diagnostic))
            {
                return false;
            }

            if (context.PlanContinuation.HasValue ||
                !context.TargetActorId.IsValid ||
                !TryGetKnownCharacterEvidence(
                    out ActorId subjectActorId,
                    out Vector3 knownPosition,
                    out double evidenceTimeSeconds,
                    out _) ||
                subjectActorId != context.TargetActorId)
            {
                diagnostic = string.Empty;
                return true;
            }

            string routeDiagnostic = string.Empty;
            if (!TryCreateSafeSeparationPosition(
                    ActorTransform.position,
                    knownPosition,
                    out Vector3 destination,
                    out diagnostic) ||
                !TryDeferNavigationRoute(
                    PEGAActorMovementSpeedRegime.Pursuit,
                    out PEGANpcNavigationRouteEstimate routeEstimate,
                    out routeDiagnostic))
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = string.IsNullOrWhiteSpace(routeDiagnostic)
                        ? "No complete route reaches a safe separation position from the known character."
                        : routeDiagnostic;
                }

                return true;
            }

            PEGANpcPlanningTarget target = PEGANpcPlanningTarget.FromCharacterSeparationPosition(
                subjectActorId,
                destination,
                evidenceTimeSeconds);
            PEGANpcPlanningActionCandidate candidate = new PEGANpcPlanningActionCandidate(
                Contract,
                this,
                target.TargetId,
                PEGAActorMovementSpeedRegime.Pursuit,
                routeEstimate,
                IntrinsicDurationSeconds);

            targets = Array.AsReadOnly(new[] { target });
            candidates = Array.AsReadOnly(new[] { candidate });
            diagnostic = string.Empty;
            return true;
        }

        public override bool CanOfferCandidate(
            PEGANpcPlanningActionCandidate candidate,
            PEGANpcPlanContinuation? continuation)
        {
            return !continuation.HasValue &&
                   CandidateMatchesContract(candidate) &&
                   candidate.TargetId.IdentityKind == PEGANpcPlanningTargetIdentityKind.CharacterSeparationDestination &&
                   TryResolveCharacterSubject(out ActorId subjectActorId, out _) &&
                   candidate.TargetId.CharacterSeparationSubjectActorId == subjectActorId;
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
            if (continuation.HasValue || !TryValidateConfiguration(out diagnostic) ||
                !TryGetKnownCharacterEvidence(
                    out ActorId subjectActorId,
                    out Vector3 knownCharacterPosition,
                    out evidenceTimeSeconds,
                    out diagnostic) ||
                plan.Purpose != PEGANpcPlanPurpose.DesireGoal ||
                plan.ActionId != Contract.ActionId ||
                plan.PredictedEffect != Contract.EffectKind ||
                plan.TargetKind != Contract.TargetKind ||
                plan.TargetProvenance != PEGANpcPlanningTargetProvenance.DerivedFromKnowledge ||
                plan.TargetId != PEGANpcPlanningTargetId.ForCharacterSeparationDestination(subjectActorId) ||
                PlanarDistance(plan.TargetPosition, knownCharacterPosition) <
                minimumSafeDistanceMeters + ArrivalRadiusMeters)
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "The separation plan no longer has a valid known target or safe destination."
                    : diagnostic;
                return false;
            }

            currentKnownPosition = plan.TargetPosition;
            diagnostic = string.Empty;
            return true;
        }

        public override bool TryStart(
            PEGANpcPlanSnapshot plan,
            PEGANpcPlanContinuation? continuation,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (continuation.HasValue || !TryValidateConfiguration(out diagnostic) ||
                !TryGetKnownCharacterEvidence(
                    out ActorId subjectActorId,
                    out Vector3 knownCharacterPosition,
                    out _,
                    out diagnostic) ||
                plan.Purpose != PEGANpcPlanPurpose.DesireGoal ||
                plan.ActionId != Contract.ActionId ||
                plan.PredictedEffect != Contract.EffectKind ||
                plan.TargetKind != Contract.TargetKind ||
                plan.TargetProvenance != PEGANpcPlanningTargetProvenance.DerivedFromKnowledge ||
                plan.TargetId != PEGANpcPlanningTargetId.ForCharacterSeparationDestination(subjectActorId) ||
                !TryCreateSafeSeparationPosition(
                    ActorTransform.position,
                    knownCharacterPosition,
                    out Vector3 expectedDestination,
                    out diagnostic) ||
                !IsTargetPositionWithinTolerance(expectedDestination, plan.TargetPosition))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The selected separation plan is stale or no longer moves away from the known target."
                    : diagnostic;
                RejectStart(diagnostic);
                return false;
            }

            return TryStartNavigation(
                plan.TargetId,
                plan.TargetPosition,
                PEGAActorMovementSpeedRegime.Pursuit,
                out diagnostic);
        }

        public override bool IsEffectAlreadySatisfiedAtPosition(PEGANpcPlanSnapshot plan, Vector3 actorPosition)
        {
            return base.IsEffectAlreadySatisfiedAtPosition(plan, actorPosition) &&
                   TryGetKnownCharacterEvidence(
                       out _,
                       out Vector3 knownCharacterPosition,
                       out _,
                       out _) &&
                   PlanarDistance(actorPosition, knownCharacterPosition) >= minimumSafeDistanceMeters;
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic) =>
            TryValidateConfiguration(out diagnostic) &&
            TryGetKnownCharacterEvidence(out _, out _, out _, out diagnostic);

        protected override bool IsActionSpecificEffectObserved(out string diagnostic)
        {
            if (!TryValidateConfiguration(out diagnostic) ||
                !TryGetKnownCharacterEvidence(
                    out _,
                    out Vector3 knownCharacterPosition,
                    out _,
                    out diagnostic))
            {
                return false;
            }

            float observedSeparation = PlanarDistance(ActorTransform.position, knownCharacterPosition);
            if (observedSeparation < minimumSafeDistanceMeters)
            {
                diagnostic = $"Observed separation {observedSeparation:0.##} m is below the configured safe distance of {minimumSafeDistanceMeters:0.##} m.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryValidateConfiguration(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (knowledgeState == null || knowledgeState.gameObject != ActorRoot)
            {
                diagnostic = "The Action requires this NPC's Knowledge State on the same root.";
                return false;
            }

            if (characterReference == null || characterReference.gameObject != ActorRoot)
            {
                diagnostic = "The Action requires an explicit target Actor occurrence reference on the same root.";
                return false;
            }

            if (!IsPositiveFinite(minimumSafeDistanceMeters) ||
                !IsPositiveFinite(additionalDistancePerMoveMeters))
            {
                diagnostic = "Safe separation and retreat step distances must be finite and greater than zero.";
                return false;
            }

            return true;
        }

        private bool TryResolveCharacterSubject(out ActorId subjectActorId, out string diagnostic)
        {
            subjectActorId = default;
            if (!TryValidateConfiguration(out diagnostic))
            {
                return false;
            }

            if (!characterReference.TryGetCurrent(out ActorOccurrenceResolution resolution))
            {
                diagnostic = $"The configured target Actor occurrence is not currently resolved: {resolution.Status}. {resolution.Diagnostic}";
                return false;
            }

            subjectActorId = resolution.Occurrence.ActorId;
            if (!subjectActorId.IsValid)
            {
                diagnostic = "The current target Actor occurrence has no valid ActorId.";
                subjectActorId = default;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryGetKnownCharacterEvidence(
            out ActorId subjectActorId,
            out Vector3 knownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            subjectActorId = default;
            knownPosition = default;
            evidenceTimeSeconds = 0d;
            if (!TryResolveCharacterSubject(out subjectActorId, out diagnostic))
            {
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                knowledgeState.GetObservedCharacterIdentity(subjectActorId);
            if (!identity.TryGetValue(out _) ||
                (identity.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The target identity is not legitimately known from Perception evidence."
                    : diagnostic;
                return false;
            }

            PEGANpcKnowledgeFact<Vector3> lastObservedPosition =
                knowledgeState.GetLastObservedCharacterPosition(subjectActorId);
            if (!lastObservedPosition.TryGetValue(out knownPosition) ||
                !lastObservedPosition.HasObservationTime ||
                (lastObservedPosition.Origins & PEGANpcEvidenceOrigin.Perception) == 0 ||
                !IsFinite(knownPosition))
            {
                diagnostic = "The NPC has no valid Perception-backed target position with evidence time.";
                return false;
            }

            evidenceTimeSeconds = lastObservedPosition.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        private bool TryCreateSafeSeparationPosition(
            Vector3 actorPosition,
            Vector3 knownCharacterPosition,
            out Vector3 destination,
            out string diagnostic)
        {
            destination = default;
            diagnostic = string.Empty;
            if (!IsFinite(actorPosition) || !IsFinite(knownCharacterPosition) ||
                !IsPositiveFinite(minimumSafeDistanceMeters) ||
                !IsNonNegativeFinite(ArrivalRadiusMeters))
            {
                diagnostic = "A finite Actor position, known target position and positive safe separation distance are required.";
                return false;
            }

            Vector3 awayDirection = actorPosition - knownCharacterPosition;
            awayDirection.y = 0f;
            if (awayDirection.sqrMagnitude <= 0.000001f)
            {
                awayDirection = ActorTransform.forward;
                awayDirection.y = 0f;
                if (awayDirection.sqrMagnitude <= 0.000001f)
                {
                    awayDirection = Vector3.forward;
                }
            }

            awayDirection.Normalize();
            float currentSeparation = PlanarDistance(actorPosition, knownCharacterPosition);
            if (!IsNonNegativeFinite(currentSeparation))
            {
                diagnostic = "The current separation from the known character must be finite.";
                return false;
            }

            float minimumTargetSeparation = minimumSafeDistanceMeters +
                ArrivalRadiusMeters + SeparationTargetPrecisionMarginMeters;
            float retreatTargetSeparation = currentSeparation +
                additionalDistancePerMoveMeters + ArrivalRadiusMeters;
            float targetSeparation = Mathf.Max(minimumTargetSeparation, retreatTargetSeparation);
            destination = knownCharacterPosition + awayDirection * targetSeparation;
            destination.y = actorPosition.y;
            return IsFinite(destination);
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

        private static bool IsPositiveFinite(float value) =>
            value > 0f && IsFinite(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && IsFinite(value);
    }
}

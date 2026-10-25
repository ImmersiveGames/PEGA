using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using PEGA.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Actions/Avoid Known Character")]
    public sealed class PEGANpcAvoidKnownCharacterAction : PEGANpcNavigationAction,
        IPEGANpcKnowledgeChangeRelevanceProvider
    {
        private const float MinimumSeparationMeters = 0.1f;

        [Header("Known Character")]
        [SerializeField]
        [Tooltip("Knowledge State mantido por este NPC.")]
        private PEGANpcKnowledgeState knowledgeState;

        [SerializeField]
        [Tooltip("Referência explícita ao Actor conhecido que esta Action deve evitar.")]
        private ActorOccurrenceReferenceBinding characterReference;

        [SerializeField]
        [Tooltip("Actor Profile exigido para aceitar a identidade conhecida do alvo.")]
        private ActorProfile characterActorProfile;

        [Header("Gameplay")]
        [SerializeField, Min(MinimumSeparationMeters)]
        [Tooltip("Distância planar mínima desejada entre o NPC e a última posição conhecida do alvo (m).")]
        private float safeSeparationDistanceMeters = 5f;

        protected override PEGANpcActionEffectKind ActionEffectKind =>
            PEGANpcActionEffectKind.ReachSafeSeparationPosition;

        protected override PEGANpcDesireGoalKind ActionGoalKind =>
            PEGANpcDesireGoalKind.MaintainSafeSeparationFromKnownCharacter;

        public override PEGANpcActionContract Contract => new PEGANpcActionContract(
            PEGANpcPlanningActionId.MaintainSafeSeparationFromKnownCharacter,
            ActionEffectKind,
            ActionGoalKind,
            PEGANpcPlanningTargetKind.SafeSeparationPosition,
            true,
            "NPC began moving to maintain safe separation from a known character.");

        public float SafeSeparationDistanceMeters => safeSeparationDistanceMeters;

        public bool IsRelevantKnowledgeChange(PEGANpcKnowledgeFactChangedEvent change)
        {
            if (change == null || !change.HasValueOrStatusChanged ||
                !change.SubjectActorId.HasValue ||
                (change.FactKind != PEGANpcKnowledgeFactKind.ObservedCharacterIdentity &&
                 change.FactKind != PEGANpcKnowledgeFactKind.LastObservedRickPosition) ||
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

            if (context.PursuitContinuation.HasValue ||
                !context.PlayerActorId.IsValid ||
                !TryGetKnownCharacterEvidence(
                    out ActorId subjectActorId,
                    out Vector3 knownPosition,
                    out double evidenceTimeSeconds,
                    out _) ||
                subjectActorId != context.PlayerActorId)
            {
                diagnostic = string.Empty;
                return true;
            }

            float currentSeparation = PlanarDistance(transform.position, knownPosition);
            bool preservingRunningExecution = Outcome == PEGANpcActionOutcome.Running;
            if (currentSeparation >= safeSeparationDistanceMeters && !preservingRunningExecution)
            {
                diagnostic = string.Empty;
                return true;
            }

            string routeDiagnostic = string.Empty;
            if (!TryCreateSafeSeparationPosition(
                    transform.position,
                    knownPosition,
                    out Vector3 destination,
                    out diagnostic) ||
                !TryEstimateNavigationRoute(
                    destination,
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
                true,
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
            PEGANpcPursuitContinuation? continuation)
        {
            return !continuation.HasValue &&
                   CandidateMatchesContract(candidate) &&
                   candidate.TargetId.IdentityKind == PEGANpcPlanningTargetIdentityKind.CharacterSeparationDestination &&
                   TryResolveCharacterSubject(out ActorId subjectActorId, out _) &&
                   candidate.TargetId.CharacterSeparationSubjectActorId == subjectActorId;
        }

        public override bool IsCurrentPlanValid(
            PEGANpcPlanSnapshot plan,
            PEGANpcPursuitContinuation? continuation,
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
                plan.PredictedGoal != Contract.GoalKind ||
                plan.PredictedEffect != Contract.EffectKind ||
                plan.TargetKind != Contract.TargetKind ||
                plan.TargetProvenance != PEGANpcPlanningTargetProvenance.DerivedFromKnowledge ||
                plan.TargetId != PEGANpcPlanningTargetId.ForCharacterSeparationDestination(subjectActorId) ||
                PlanarDistance(plan.TargetPosition, knownCharacterPosition) <
                safeSeparationDistanceMeters + ArrivalRadiusMeters)
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
            PEGANpcPursuitContinuation? continuation,
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
                plan.PredictedGoal != Contract.GoalKind ||
                plan.PredictedEffect != Contract.EffectKind ||
                plan.TargetKind != Contract.TargetKind ||
                plan.TargetProvenance != PEGANpcPlanningTargetProvenance.DerivedFromKnowledge ||
                plan.TargetId != PEGANpcPlanningTargetId.ForCharacterSeparationDestination(subjectActorId) ||
                PlanarDistance(transform.position, knownCharacterPosition) >= safeSeparationDistanceMeters ||
                !TryCreateSafeSeparationPosition(
                    transform.position,
                    knownCharacterPosition,
                    out Vector3 expectedDestination,
                    out diagnostic) ||
                !IsTargetPositionWithinTolerance(expectedDestination, plan.TargetPosition))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The selected separation plan is stale or its target is no longer too close."
                    : diagnostic;
                RejectStart(diagnostic);
                return false;
            }

            return TryStartNavigation(plan.TargetPosition, PEGAActorMovementSpeedRegime.Pursuit, out diagnostic);
        }

        public override bool IsEffectAlreadySatisfiedAtPosition(PEGANpcPlanSnapshot plan, Vector3 actorPosition)
        {
            return base.IsEffectAlreadySatisfiedAtPosition(plan, actorPosition) &&
                   TryGetKnownCharacterEvidence(
                       out _,
                       out Vector3 knownCharacterPosition,
                       out _,
                       out _) &&
                   PlanarDistance(actorPosition, knownCharacterPosition) >= safeSeparationDistanceMeters;
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

            float observedSeparation = PlanarDistance(transform.position, knownCharacterPosition);
            if (observedSeparation < safeSeparationDistanceMeters)
            {
                diagnostic = $"Observed separation {observedSeparation:0.##} m is below the configured safe distance of {safeSeparationDistanceMeters:0.##} m.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryValidateConfiguration(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (knowledgeState == null || knowledgeState.gameObject != gameObject)
            {
                diagnostic = "The Action requires this NPC's Knowledge State on the same root.";
                return false;
            }

            if (characterReference == null || characterReference.gameObject != gameObject)
            {
                diagnostic = "The Action requires an explicit target Actor occurrence reference on the same root.";
                return false;
            }

            if (characterActorProfile == null ||
                !characterActorProfile.TryGetActorProfileId(out ActorProfileId profileId, out diagnostic) ||
                !profileId.IsValid)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Action requires a valid target Actor Profile."
                    : diagnostic;
                return false;
            }

            if (!IsPositiveFinite(safeSeparationDistanceMeters))
            {
                diagnostic = "Safe separation distance must be finite and greater than zero.";
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
            if (!identity.TryGetValue(out ActorProfileId observedProfileId) ||
                !characterActorProfile.TryGetActorProfileId(out ActorProfileId configuredProfileId, out diagnostic) ||
                observedProfileId != configuredProfileId ||
                (identity.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The target is not legitimately known with the configured Actor Profile."
                    : diagnostic;
                return false;
            }

            PEGANpcKnowledgeFact<Vector3> lastObservedPosition =
                knowledgeState.GetLastObservedRickPosition(subjectActorId);
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
                !IsPositiveFinite(safeSeparationDistanceMeters) ||
                !IsNonNegativeFinite(ArrivalRadiusMeters))
            {
                diagnostic = "A finite Actor position, known target position and positive safe separation distance are required.";
                return false;
            }

            Vector3 awayDirection = actorPosition - knownCharacterPosition;
            awayDirection.y = 0f;
            if (awayDirection.sqrMagnitude <= 0.000001f)
            {
                awayDirection = transform.forward;
                awayDirection.y = 0f;
                if (awayDirection.sqrMagnitude <= 0.000001f)
                {
                    awayDirection = Vector3.forward;
                }
            }

            awayDirection.Normalize();
            destination = knownCharacterPosition + awayDirection *
                (safeSeparationDistanceMeters + ArrivalRadiusMeters);
            destination.y = actorPosition.y;
            return IsFinite(destination);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!TryGetKnownCharacterEvidence(out _, out Vector3 knownPosition, out _, out _))
            {
                return;
            }

            UnityEditor.Handles.color = new Color(1f, 0.65f, 0.1f, 0.9f);
            UnityEditor.Handles.DrawWireDisc(knownPosition, Vector3.up, safeSeparationDistanceMeters);
        }
#endif

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

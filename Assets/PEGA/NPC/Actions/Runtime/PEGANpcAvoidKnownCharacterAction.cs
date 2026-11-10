using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using PEGA.Movement.Actor;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
using UnityEngine.Serialization;
namespace PEGA.NPC.Actions.Runtime
{
    [CreateAssetMenu(fileName = "MoveAwayFromCharacter", menuName = "PEGA/NPC/Movement/Move Away from Character")]
    public sealed class PegaNpcAvoidKnownCharacterAction : PegaNpcActionProviderAsset,
        IPegaNpcKnowledgeChangeRelevanceProvider
    {
        private const float MinimumSeparationMeters = 0.1f;
        private const float SeparationTargetPrecisionMarginMeters = 0.001f;

        [System.NonSerialized]
        private PegaNpcKnowledgeState _knowledgeState;

        [System.NonSerialized]
        private PegaNpcKnowledgeReadView _knowledgeReadView;

        [System.NonSerialized]
        private ActorOccurrenceReferenceBinding _characterReference;

        [Header("Movement")]
        [SerializeField, Min(MinimumSeparationMeters)]
        [FormerlySerializedAs("safeSeparationDistanceMeters")]
        [Tooltip("Minimum desired distance from the character's last known position (m).")]
        private float minimumSafeDistanceMeters = 5f;

        [SerializeField, Min(MinimumSeparationMeters)]
        [FormerlySerializedAs("retreatStepDistanceMeters")]
        [Tooltip("Additional distance the NPC attempts to gain on each move (m).")]
        private float additionalDistancePerMoveMeters = 1f;

        protected override PegaNpcActionEffectKind ActionEffectKind =>
            PegaNpcActionEffectKind.ReachSafeSeparationPosition;

        public override PegaNpcActionContract Contract => new PegaNpcActionContract(
            PegaNpcPlanningActionId.MaintainSafeSeparationFromKnownCharacter,
            ActionEffectKind,
            PegaNpcPlanningTargetKind.SafeSeparationPosition,
            true,
            "NPC began moving to maintain safe separation from a known character.");

        public float SafeSeparationDistanceMeters => minimumSafeDistanceMeters;

        protected override bool OnRuntimeInitialize(out string diagnostic)
        {
            if (!base.OnRuntimeInitialize(out diagnostic))
            {
                return false;
            }

            _knowledgeState = RuntimeContext.KnowledgeState;
            _knowledgeReadView = RuntimeContext.KnowledgeReadView;
            _characterReference = RuntimeContext.ActorReference;
            if (_knowledgeState == null || _knowledgeState.gameObject != ActorRoot ||
                _knowledgeReadView == null ||
                _characterReference == null || _characterReference.gameObject != ActorRoot)
            {
                diagnostic = "Avoid Known Character requires the Actor's Knowledge State and occurrence reference.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        public bool IsRelevantKnowledgeChange(PegaKnowledgeFactChangedEvent change)
        {
            if (change == null || !change.HasValueOrStatusChanged ||
                !TryResolveCharacterSubject(out ActorId subjectActorId, out _))
            {
                return false;
            }

            return PegaNpcKnowledgeFacts.IsCharacterFactChangeFor(change, subjectActorId);
        }

        public override bool TryCreatePlanningCandidates(
            PegaNpcActionPlanningContext context,
            out IReadOnlyList<PegaNpcPlanningTarget> targets,
            out IReadOnlyList<PegaNpcPlanningActionCandidate> candidates,
            out string diagnostic)
        {
            List<PegaNpcPlanningTarget> targetList = new List<PegaNpcPlanningTarget>();
            List<PegaNpcPlanningActionCandidate> candidateList = new List<PegaNpcPlanningActionCandidate>();
            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());

            if (!TryValidateConfiguration(out diagnostic))
            {
                return false;
            }

            if (context.PlanContinuation.HasValue ||
                !context.TargetActorId.IsValid ||
                !TryGetKnownCharacterEvidence(
                    context.KnowledgeReadView,
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
                    PegaActorMovementSpeedRegime.Pursuit,
                    out PegaNpcNavigationRouteEstimate routeEstimate,
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

            PegaNpcPlanningTarget target = PegaNpcPlanningTarget.FromCharacterSeparationPosition(
                subjectActorId,
                destination,
                evidenceTimeSeconds);
            PegaNpcPlanningActionCandidate candidate = new PegaNpcPlanningActionCandidate(
                Contract,
                this,
                target.TargetId,
                PegaActorMovementSpeedRegime.Pursuit,
                routeEstimate,
                IntrinsicDurationSeconds);

            targets = Array.AsReadOnly(new[] { target });
            candidates = Array.AsReadOnly(new[] { candidate });
            diagnostic = string.Empty;
            return true;
        }

        public override bool CanOfferCandidate(
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcPlanContinuation? continuation)
        {
            return !continuation.HasValue &&
                   CandidateMatchesContract(candidate) &&
                   candidate.TargetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.CharacterSeparationDestination &&
                   TryResolveCharacterSubject(out ActorId subjectActorId, out _) &&
                   candidate.TargetId.CharacterSeparationSubjectActorId == subjectActorId;
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
            if (continuation.HasValue || !TryValidateConfiguration(out diagnostic) ||
                !TryGetKnownCharacterEvidence(
                    out ActorId subjectActorId,
                    out Vector3 knownCharacterPosition,
                    out evidenceTimeSeconds,
                    out diagnostic) ||
                plan.Purpose != PegaNpcPlanPurpose.DesireGoal ||
                plan.ActionId != Contract.ActionId ||
                plan.PredictedEffect != Contract.EffectKind ||
                plan.TargetKind != Contract.TargetKind ||
                plan.TargetProvenance != PegaNpcPlanningTargetProvenance.DerivedFromKnowledge ||
                plan.TargetId != PegaNpcPlanningTargetId.ForCharacterSeparationDestination(subjectActorId) ||
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
            PegaNpcPlanSnapshot plan,
            PegaNpcPlanContinuation? continuation,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (continuation.HasValue || !TryValidateConfiguration(out diagnostic) ||
                !TryGetKnownCharacterEvidence(
                    out ActorId subjectActorId,
                    out Vector3 knownCharacterPosition,
                    out _,
                    out diagnostic) ||
                plan.Purpose != PegaNpcPlanPurpose.DesireGoal ||
                plan.ActionId != Contract.ActionId ||
                plan.PredictedEffect != Contract.EffectKind ||
                plan.TargetKind != Contract.TargetKind ||
                plan.TargetProvenance != PegaNpcPlanningTargetProvenance.DerivedFromKnowledge ||
                plan.TargetId != PegaNpcPlanningTargetId.ForCharacterSeparationDestination(subjectActorId) ||
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
                PegaActorMovementSpeedRegime.Pursuit,
                out diagnostic);
        }

        public override bool IsEffectAlreadySatisfiedAtPosition(PegaNpcPlanSnapshot plan, Vector3 actorPosition)
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
            if (_knowledgeState == null || _knowledgeState.gameObject != ActorRoot)
            {
                diagnostic = "The Action requires this NPC's Knowledge State on the same root.";
                return false;
            }

            if (_characterReference == null || _characterReference.gameObject != ActorRoot)
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

            if (!_characterReference.TryGetCurrent(out ActorOccurrenceResolution resolution))
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
            return TryGetKnownCharacterEvidence(
                _knowledgeReadView,
                out subjectActorId,
                out knownPosition,
                out evidenceTimeSeconds,
                out diagnostic);
        }

        private bool TryGetKnownCharacterEvidence(
            PegaNpcKnowledgeReadView knowledgeView,
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

            PegaKnowledgeFact<Vector3> lastObservedPosition =
                knowledgeView.GetKnownCharacterPosition(subjectActorId);
            PegaKnowledgeFact<bool> playerFact = knowledgeView.GetIsPlayer(subjectActorId);
            if (!PegaNpcKnowledgeFacts.HasPerceptionBackedPlayerFact(playerFact) ||
                !lastObservedPosition.TryGetValue(out knownPosition) ||
                !lastObservedPosition.HasObservationTime ||
                !lastObservedPosition.HasOrigin(PegaNpcEvidenceOrigin.Perception) ||
                !IsFinite(knownPosition))
            {
                diagnostic = "The target's Player status or location is unknown in this NPC's Perception-backed Individual knowledge.";
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

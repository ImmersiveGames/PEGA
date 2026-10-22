using Immersive.Framework.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Actions/Approach Known Rick")]
    public sealed class PEGANpcApproachKnownRickAction : PEGANpcNavigationAction
    {
        protected override PEGANpcActionEffectKind ActionEffectKind =>
            PEGANpcActionEffectKind.ApproachKnownRickPosition;

        protected override PEGANpcDesireGoalKind ActionGoalKind =>
            PEGANpcDesireGoalKind.ReachKnownCharacterPosition;

        [Header("Known Target")]
        [SerializeField]
        [Tooltip("Conhecimento mantido por este NPC.")]
        private PEGANpcKnowledgeState knowledgeState;

        [SerializeField]
        [Tooltip("Referência explícita ao Player cuja identidade deve estar reconhecida como Rick.")]
        private ActorOccurrenceReferenceBinding rickReference;

        [SerializeField]
        [Tooltip("Actor Profile que identifica Rick no Knowledge State.")]
        private ActorProfile rickActorProfile;

        public bool TryGetKnownRickPosition(out Vector3 position, out string diagnostic)
        {
            return TryGetKnownRickEvidence(out _, out position, out _, out diagnostic);
        }

        public bool TryCreatePlanningCandidate(
            out PEGANpcPlanningTarget target,
            out PEGANpcPlanningActionCandidate candidate,
            out string diagnostic)
        {
            target = default;
            candidate = default;
            if (!TryGetKnownRickEvidence(
                    out ActorId subjectActorId,
                    out Vector3 knownPosition,
                    out double evidenceTimeSeconds,
                    out diagnostic))
            {
                return false;
            }

            if (!TryEstimateNavigationRoute(
                    knownPosition,
                    PEGA.Actors.PEGAActorMovementSpeedRegime.Pursuit,
                    out PEGANpcNavigationRouteEstimate routeEstimate,
                    out diagnostic))
            {
                return false;
            }

            target = PEGANpcPlanningTarget.FromKnownActorPosition(
                subjectActorId,
                knownPosition,
                evidenceTimeSeconds);
            candidate = new PEGANpcPlanningActionCandidate(
                PEGANpcPlanningActionId.ApproachKnownRick,
                ActionEffectKind,
                ActionGoalKind,
                target.TargetId,
                PEGANpcPlanningTargetKind.CharacterPosition,
                true,
                PEGA.Actors.PEGAActorMovementSpeedRegime.Pursuit,
                routeEstimate,
                IntrinsicDurationSeconds);
            diagnostic = string.Empty;
            return true;
        }

        private bool TryGetKnownRickEvidence(
            out ActorId subjectActorId,
            out Vector3 position,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            subjectActorId = default;
            position = default;
            evidenceTimeSeconds = 0d;
            if (!TryResolveRickSubject(out subjectActorId, out diagnostic))
            {
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                knowledgeState.GetObservedCharacterIdentity(subjectActorId);
            if (!identity.TryGetValue(out ActorProfileId observedProfileId) ||
                !rickActorProfile.TryGetActorProfileId(out ActorProfileId rickProfileId, out diagnostic) ||
                observedProfileId != rickProfileId)
            {
                if (string.IsNullOrEmpty(diagnostic))
                {
                    diagnostic = "The current Player subject is not known as the configured Rick Actor Profile.";
                }

                return false;
            }

            PEGANpcKnowledgeFact<Vector3> lastObservedPosition =
                knowledgeState.GetLastObservedRickPosition(subjectActorId);
            if (!lastObservedPosition.TryGetValue(out position) || !lastObservedPosition.HasObservationTime)
            {
                diagnostic = "This NPC has no valid last observed Rick position with evidence time.";
                return false;
            }

            evidenceTimeSeconds = lastObservedPosition.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        public bool TryStart(out string diagnostic)
        {
            if (!TryGetKnownRickPosition(out Vector3 lastKnownPosition, out diagnostic))
            {
                RejectStart(diagnostic);
                return false;
            }

            return TryStartNavigation(
                lastKnownPosition,
                PEGA.Actors.PEGAActorMovementSpeedRegime.Pursuit,
                out diagnostic);
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic) =>
            TryGetKnownRickPosition(out _, out diagnostic);

        private bool TryResolveRickSubject(out ActorId subjectActorId, out string diagnostic)
        {
            subjectActorId = default;
            diagnostic = string.Empty;
            if (knowledgeState == null || knowledgeState.gameObject != gameObject)
            {
                diagnostic = "The Action requires this NPC's PEGANpcKnowledgeState on the same root.";
                return false;
            }

            if (rickReference == null || rickReference.gameObject != gameObject)
            {
                diagnostic = "The Action requires an explicit Player Actor occurrence reference on the same root.";
                return false;
            }

            if (rickActorProfile == null ||
                !rickActorProfile.TryGetActorProfileId(out ActorProfileId profileId, out diagnostic) ||
                !profileId.IsValid)
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "The Action requires a valid Rick Actor Profile."
                    : diagnostic;
                return false;
            }

            if (!rickReference.TryGetCurrent(out ActorOccurrenceResolution resolution))
            {
                diagnostic = $"The configured Rick Player occurrence is not currently resolved: {resolution.Status}. {resolution.Diagnostic}";
                return false;
            }

            subjectActorId = resolution.Occurrence.ActorId;
            if (!subjectActorId.IsValid)
            {
                diagnostic = "The current Rick Player occurrence has no valid ActorId.";
                subjectActorId = default;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }
    }
}

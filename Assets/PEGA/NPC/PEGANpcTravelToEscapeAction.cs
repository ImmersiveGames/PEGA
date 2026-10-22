using PEGA.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Actions/Travel to Escape Target")]
    public sealed class PEGANpcTravelToEscapeAction : PEGANpcNavigationAction
    {
        protected override PEGANpcActionEffectKind ActionEffectKind =>
            PEGANpcActionEffectKind.ReachSelectedEscapeDestination;

        protected override PEGANpcDesireGoalKind ActionGoalKind =>
            PEGANpcDesireGoalKind.ReachKnownEscapeDestination;

        public bool TryCreatePlanningCandidate(
            PEGANpcPlanningTarget knownEscapeTarget,
            PEGAActorMovementSpeedRegime speedRegime,
            out PEGANpcPlanningActionCandidate candidate,
            out string diagnostic)
        {
            candidate = default;
            if (knownEscapeTarget.Provenance != PEGANpcPlanningTargetProvenance.KnowledgeState ||
                knownEscapeTarget.Kind != PEGANpcPlanningTargetKind.EscapeDestination ||
                !knownEscapeTarget.TargetId.IsValid)
            {
                diagnostic = "Escape planning requires a valid escape Target already resolved from this NPC's Knowledge State.";
                return false;
            }

            if (!TryEstimateNavigationRoute(
                    knownEscapeTarget.KnownPosition,
                    speedRegime,
                    out PEGANpcNavigationRouteEstimate routeEstimate,
                    out diagnostic))
            {
                return false;
            }

            candidate = new PEGANpcPlanningActionCandidate(
                PEGANpcPlanningActionId.TravelToEscapeTarget,
                ActionEffectKind,
                ActionGoalKind,
                knownEscapeTarget.TargetId,
                PEGANpcPlanningTargetKind.EscapeDestination,
                true,
                speedRegime,
                routeEstimate,
                IntrinsicDurationSeconds);
            diagnostic = string.Empty;
            return true;
        }

        public bool TryStart(
            PEGANpcEscapeDestination selectedEscapeDestination,
            PEGAActorMovementSpeedRegime speedRegime,
            out string diagnostic)
        {
            if (selectedEscapeDestination == null)
            {
                diagnostic = "A selected PEGA escape destination is required by this Action.";
                RejectStart(diagnostic);
                return false;
            }

            return TryStart(selectedEscapeDestination.WorldPosition, speedRegime, out diagnostic);
        }

        public bool TryStart(
            Vector3 selectedEscapeTarget,
            PEGAActorMovementSpeedRegime speedRegime,
            out string diagnostic)
        {
            return TryStartNavigation(selectedEscapeTarget, speedRegime, out diagnostic);
        }
    }
}

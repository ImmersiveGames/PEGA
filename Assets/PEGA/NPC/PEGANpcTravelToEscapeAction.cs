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

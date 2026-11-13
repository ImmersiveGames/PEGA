using PEGA.GOAP;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;

namespace PEGA.NPC.Goals.Conditions
{
    [CreateAssetMenu(
        fileName = "UnresolvedRadialPresence",
        menuName = "PEGA/NPC/GOAP/Unresolved Radial Presence Condition")]
    public sealed class PegaNpcUnresolvedRadialPresenceConditionAsset : PegaNpcGoapConditionAsset
    {
        public override GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (state == null || state.Source != GoapStateSource.KnowledgeState ||
                !(state is IGoapKnowledgeStateView knowledgeState))
            {
                return GoapTruthValue.Unknown;
            }

            PegaNpcKnowledgeState individualKnowledge = null;
            for (int index = 0; index < knowledgeState.Capabilities.Count; index++)
            {
                if (!(knowledgeState.Capabilities[index] is PegaNpcKnowledgeState candidate))
                {
                    continue;
                }

                if (individualKnowledge != null && individualKnowledge != candidate)
                {
                    return GoapTruthValue.Unknown;
                }

                individualKnowledge = candidate;
            }

            if (individualKnowledge == null || individualKnowledge.Scope != PegaKnowledgeScope.Individual)
            {
                return GoapTruthValue.Unknown;
            }

            PegaKnowledgeFact<PegaNpcPerceivedAreaPresence> fact =
                individualKnowledge.GetUnresolvedRadialPresence();
            if (fact.Scope != PegaKnowledgeScope.Individual ||
                !fact.HasObservationTime ||
                !fact.HasOrigin(PegaNpcEvidenceOrigin.Perception) ||
                !fact.TryGetValue(out PegaNpcPerceivedAreaPresence presence) ||
                !presence.IsValid)
            {
                return GoapTruthValue.Unknown;
            }

            return presence.IsPresent ? GoapTruthValue.True : GoapTruthValue.False;
        }

        public override bool TryValidate(out string diagnostic)
        {
            diagnostic = string.Empty;
            return true;
        }
    }
}

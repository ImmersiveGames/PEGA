using PEGA.GOAP;
using PEGA.NPC.Autonomy.Planning;
using UnityEngine;
namespace PEGA.NPC.Goals.Conditions
{
    [CreateAssetMenu(
        fileName = "ObjectEntryPossessionGoalCondition",
        menuName = "PEGA/NPC/GOAP/Object Entry Possession Goal Condition")]
    public sealed class PegaObjectEntryPossessionGoalConditionAsset : PegaNpcGoapConditionAsset
    {
        public override GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (!(state is IGoapEffectStateView effectState))
            {
                return GoapTruthValue.Unknown;
            }

            for (int index = 0; index < effectState.Effects.Count; index++)
            {
                if (effectState.Effects[index] is PegaObjectEntryPossessedEffect)
                {
                    return GoapTruthValue.True;
                }
            }

            return GoapTruthValue.Unknown;
        }

        public override bool TryValidate(out string diagnostic)
        {
            diagnostic = string.Empty;
            return true;
        }
    }
}

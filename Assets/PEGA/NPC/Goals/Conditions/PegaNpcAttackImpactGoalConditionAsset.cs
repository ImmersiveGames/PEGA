using PEGA.GOAP;
using PEGA.NPC.Actions.Runtime;
using UnityEngine;

namespace PEGA.NPC.Goals.Conditions
{
    [CreateAssetMenu(
        fileName = "AttackCurrentCharacterGoalCondition",
        menuName = "PEGA/NPC/GOAP/Attack Current Character Goal Condition")]
    public sealed class PegaNpcAttackImpactGoalConditionAsset : PegaNpcGoapConditionAsset
    {
        public override GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (state is IGoapHypotheticalStateView hypothetical)
            {
                return ContainsAttackImpact(hypothetical.ProjectedEffects) ||
                       ContainsAttackImpact(hypothetical.ObservedEffects)
                    ? GoapTruthValue.True
                    : GoapTruthValue.False;
            }

            if (state is IGoapObservedStateView observed)
            {
                return ContainsAttackImpact(observed.ObservedEffects)
                    ? GoapTruthValue.True
                    : GoapTruthValue.False;
            }

            if (state is IGoapEffectStateView effects)
            {
                return ContainsAttackImpact(effects.Effects)
                    ? GoapTruthValue.True
                    : GoapTruthValue.False;
            }

            return GoapTruthValue.Unknown;
        }

        public override bool TryValidate(out string diagnostic)
        {
            diagnostic = string.Empty;
            return true;
        }

        private static bool ContainsAttackImpact(System.Collections.Generic.IReadOnlyList<IGoapEffect> effects)
        {
            for (int index = 0; index < effects.Count; index++)
            {
                if (effects[index] is PegaNpcAttackImpactEffect)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

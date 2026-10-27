using ImmersiveGames.Goap;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// Unity authoring surface for a PEGA GOAP condition. Implementations remain ordinary GOAP conditions;
    /// the planner and Desire policy consume only the open <see cref="IGoapCondition"/> contract.
    /// </summary>
    public abstract class PEGANpcGoapConditionAsset : ScriptableObject, IGoapCondition
    {
        public abstract GoapTruthValue Evaluate(IGoapStateView state);

        public abstract bool TryValidate(out string diagnostic);
    }
}

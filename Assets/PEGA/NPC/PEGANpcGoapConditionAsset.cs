using PEGA.GOAP;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// Unity authoring surface for a PEGA GOAP condition. Implementations remain ordinary GOAP conditions;
    /// the planner and Desire policy consume only the open <see cref="IPEGAGoapCondition"/> contract.
    /// </summary>
    public abstract class PEGANpcGoapConditionAsset : ScriptableObject, IPEGAGoapCondition
    {
        public abstract PEGAGoapTruthValue Evaluate(IPEGAGoapStateView state);

        public abstract bool TryValidate(out string diagnostic);
    }
}

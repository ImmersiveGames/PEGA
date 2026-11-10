using UnityEngine;

namespace PEGA.NPC.Desires
{
    /// <summary>
    /// Extensible asset policy that maps an effective Desire weight and estimated plan duration to
    /// separate cost-preference and comparison-score values. It does not choose or execute a plan.
    /// </summary>
    public abstract class PegaNpcPlanScorePolicy : ScriptableObject
    {
        public abstract bool TryValidate(float costReferenceSeconds, out string diagnostic);

        public abstract bool TryEvaluate(
            float effectiveDesireWeight,
            float estimatedSeconds,
            float costReferenceSeconds,
            out float costPreference,
            out float score);
    }
}

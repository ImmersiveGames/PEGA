using UnityEngine;

namespace PEGA.NPC.Desires
{
    /// <summary>Preserves the current reciprocal cost preference and weighted-cost product.</summary>
    [CreateAssetMenu(
        fileName = "PegaNpcPlanScore_Multiplicative",
        menuName = "PEGA/NPC/Desires/Plan Score Policies/Multiplicative")]
    public sealed class PegaNpcMultiplicativePlanScorePolicy : PegaNpcPlanScorePolicy
    {
        public override bool TryValidate(float costReferenceSeconds, out string diagnostic)
        {
            bool valid = IsPositiveFinite(costReferenceSeconds);
            diagnostic = valid
                ? string.Empty
                : "Cost reference time must be finite and greater than zero.";
            return valid;
        }

        public override bool TryEvaluate(
            float effectiveDesireWeight,
            float estimatedSeconds,
            float costReferenceSeconds,
            out float costPreference,
            out float score)
        {
            costPreference = 0f;
            score = 0f;
            if (!IsPositiveFinite(effectiveDesireWeight) ||
                !IsPositiveFinite(costReferenceSeconds) ||
                !IsNonNegativeFinite(estimatedSeconds))
            {
                return false;
            }

            costPreference = 1f / (1f + estimatedSeconds / costReferenceSeconds);
            score = effectiveDesireWeight * costPreference;
            return IsPositiveFinite(costPreference) && IsPositiveFinite(score);
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

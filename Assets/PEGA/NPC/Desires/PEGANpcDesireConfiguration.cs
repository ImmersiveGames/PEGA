using UnityEngine;
using UnityEngine.Serialization;

namespace PEGA.NPC
{
    [CreateAssetMenu(
        fileName = "New Desire Setting",
        menuName = "PEGA/NPC/Desire Settings")]
    public sealed class PEGANpcDesireConfiguration : ScriptableObject
    {
        [SerializeField]
        [FormerlySerializedAs("definition")]
        [Tooltip("Shared Desire Definition that names the motivation and describes when its Goal is fulfilled. Editing the definition affects every Setting that references it.")]
        private PEGANpcDesireDefinition desireDefinition;

        [SerializeField]
        [Tooltip("Turn this off to exclude this Desire from the Profile context that uses this Setting.")]
        private bool enabledForContext = true;

        [SerializeField, Min(0.001f)]
        [Tooltip("Starting influence of this Desire when plans are compared. A higher weight favors it but does not guarantee selection; estimated plan time also matters.")]
        private float baseWeight = 1f;

        [SerializeField, Min(0.001f)]
        [Tooltip("Amount added to this NPC's runtime satisfaction history after a completed execution confirms this Desire was fulfilled.")]
        private float satisfactionGain = 1f;

        [SerializeField, Min(0.001f)]
        [Tooltip("Maximum satisfaction tracked for this Desire. This scales the satisfaction-based weight reduction; it does not limit how often the Desire can be pursued.")]
        private float maximumSatisfaction = 1f;

        [SerializeField, Range(0f, 1f)]
        [FormerlySerializedAs("satisfactionWeightModifier")]
        [Tooltip("Maximum fraction by which accumulated satisfaction can reduce this Desire's weight. At zero, satisfaction does not change its influence.")]
        private float weightReductionFromSatisfaction;

        [SerializeField, Min(-1)]
        [FormerlySerializedAs("maximumCompletedExecutions")]
        [Tooltip("Maximum number of confirmed completions for this Desire; -1 means unlimited. This limit is tracked separately for each NPC.")]
        private int completionLimit = -1;

        public PEGANpcDesireDefinition Definition => desireDefinition;

        public bool EnabledForContext => enabledForContext;

        public float BaseWeight => baseWeight;

        public float SatisfactionGain => satisfactionGain;

        public float MaximumSatisfaction => maximumSatisfaction;

        public float SatisfactionWeightModifier => weightReductionFromSatisfaction;

        public int MaximumCompletedExecutions => completionLimit;

        public bool IsApplicable(PEGANpcDesireSatisfactionSnapshot satisfaction)
        {
            if (!enabledForContext)
            {
                return false;
            }

            return completionLimit < 0 ||
                   satisfaction.CompletedExecutions < completionLimit;
        }

        public float EvaluateEffectiveWeight(PEGANpcDesireSatisfactionSnapshot satisfaction)
        {
            float normalizedSatisfaction = maximumSatisfaction > 0f
                ? Mathf.Clamp01(satisfaction.Satisfaction / maximumSatisfaction)
                : 1f;
            float satisfactionFactor = 1f - weightReductionFromSatisfaction * normalizedSatisfaction;
            return baseWeight * Mathf.Max(0f, satisfactionFactor);
        }

        internal bool CanRecordSatisfaction(PEGANpcDesireSatisfactionSnapshot satisfaction) =>
            enabledForContext &&
            (completionLimit < 0 ||
             satisfaction.CompletedExecutions < completionLimit);

        internal bool TryValidate(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (desireDefinition == null)
            {
                diagnostic = "A reusable Desire definition is required.";
                return false;
            }

            if (!desireDefinition.TryValidate(out diagnostic))
            {
                diagnostic = $"Definition: {diagnostic}";
                return false;
            }

            if (!IsPositiveFinite(baseWeight))
            {
                diagnostic = $"Desire '{desireDefinition.DesireId}' base weight must be finite and greater than zero.";
                return false;
            }

            if (!IsPositiveFinite(satisfactionGain) || !IsPositiveFinite(maximumSatisfaction) ||
                satisfactionGain > maximumSatisfaction)
            {
                diagnostic = $"Desire '{desireDefinition.DesireId}' satisfaction gain and maximum must be finite, positive, and gain cannot exceed the maximum.";
                return false;
            }

            if (!IsNonNegativeFinite(weightReductionFromSatisfaction) || weightReductionFromSatisfaction > 1f)
            {
                diagnostic = $"Desire '{desireDefinition.DesireId}' satisfaction weight reduction must be between zero and one.";
                return false;
            }

            if (completionLimit == 0 || completionLimit < -1)
            {
                diagnostic = $"Desire '{desireDefinition.DesireId}' completion limit must be -1 (unlimited) or a positive count.";
                return false;
            }

            return true;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

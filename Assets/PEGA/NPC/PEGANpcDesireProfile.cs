using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PEGA.NPC
{
    public enum PEGANpcDesireContext
    {
        Normal = 0
    }

    [CreateAssetMenu(
        fileName = "New Desire Profile",
        menuName = "PEGA/NPC/Desire Profiles")]
    public sealed class PEGANpcDesireProfile : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Desire Settings and plan comparison used in each gameplay context of this Profile.")]
        private PEGANpcDesireContextDefinition[] contexts = Array.Empty<PEGANpcDesireContextDefinition>();

        public bool TryGetContext(
            PEGANpcDesireContext context,
            out PEGANpcDesireContextDefinition definition,
            out string diagnostic)
        {
            definition = null;
            if (!TryValidate(out diagnostic))
            {
                return false;
            }

            for (int index = 0; index < contexts.Length; index++)
            {
                if (contexts[index].Context == context)
                {
                    definition = contexts[index];
                    diagnostic = string.Empty;
                    return true;
                }
            }

            diagnostic = $"No Desire Settings are declared for context {context}.";
            return false;
        }

        public bool TryValidate(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (contexts == null || contexts.Length == 0)
            {
                diagnostic = "At least one explicit Desire context must be configured.";
                return false;
            }

            HashSet<PEGANpcDesireContext> seenContexts = new HashSet<PEGANpcDesireContext>();
            HashSet<string> seenDesireIds = new HashSet<string>(StringComparer.Ordinal);
            for (int contextIndex = 0; contextIndex < contexts.Length; contextIndex++)
            {
                PEGANpcDesireContextDefinition context = contexts[contextIndex];
                if (context == null)
                {
                    diagnostic = $"Desire context at index {contextIndex} is null.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(PEGANpcDesireContext), context.Context))
                {
                    diagnostic = $"Desire context at index {contextIndex} has an unsupported value.";
                    return false;
                }

                if (!seenContexts.Add(context.Context))
                {
                    diagnostic = $"Desire context {context.Context} is declared more than once.";
                    return false;
                }

                if (!context.TryValidate(seenDesireIds, out diagnostic))
                {
                    diagnostic = $"Context {context.Context}: {diagnostic}";
                    return false;
                }
            }

            return true;
        }
    }

    [Serializable]
    public sealed class PEGANpcDesireContextDefinition
    {
        [SerializeField]
        [Tooltip("Contexto explícito em que estes Desejos podem ser avaliados.")]
        private PEGANpcDesireContext context;

        [SerializeField]
        [FormerlySerializedAs("planSelectionPolicy")]
        [Tooltip("How each Desire's effective weight and estimated plan time are compared. A higher weight influences selection but does not guarantee that a Desire wins.")]
        private PEGANpcPlanSelectionPolicy planComparison = new PEGANpcPlanSelectionPolicy();

        [SerializeField]
        [FormerlySerializedAs("desires")]
        [Tooltip("Desire Settings assigned to this Profile and context. Settings are reusable assets; editing a shared Setting affects every Profile that references it.")]
        private PEGANpcDesireConfiguration[] desireSettings = Array.Empty<PEGANpcDesireConfiguration>();

        public PEGANpcDesireContextDefinition()
        {
        }

        internal PEGANpcDesireContextDefinition(
            PEGANpcDesireContext context,
            float costReferenceSeconds,
            PEGANpcDesireConfiguration[] desireSettings)
        {
            this.context = context;
            planComparison = new PEGANpcPlanSelectionPolicy(costReferenceSeconds);
            this.desireSettings = desireSettings != null
                ? (PEGANpcDesireConfiguration[])desireSettings.Clone()
                : Array.Empty<PEGANpcDesireConfiguration>();
        }

        public PEGANpcDesireContext Context => context;

        public PEGANpcPlanSelectionPolicy PlanSelectionPolicy => planComparison;

        public IReadOnlyList<PEGANpcDesireConfiguration> Desires =>
            Array.AsReadOnly(desireSettings ?? Array.Empty<PEGANpcDesireConfiguration>());

        internal PEGANpcDesireConfiguration[] Configurations =>
            desireSettings ?? Array.Empty<PEGANpcDesireConfiguration>();

        internal bool TryValidate(HashSet<string> allDesireIds, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!Enum.IsDefined(typeof(PEGANpcDesireContext), context))
            {
                diagnostic = "Context has an unsupported value.";
                return false;
            }

            if (planComparison == null || !planComparison.TryValidate(out diagnostic))
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "A plan-selection policy is required."
                    : diagnostic;
                return false;
            }

            if (desireSettings == null)
            {
                diagnostic = "Desire Settings cannot be null.";
                return false;
            }

            for (int index = 0; index < desireSettings.Length; index++)
            {
                PEGANpcDesireConfiguration configuration = desireSettings[index];
                if (configuration == null)
                {
                    diagnostic = $"Desire Setting at index {index} is null.";
                    return false;
                }

                if (!configuration.TryValidate(out diagnostic))
                {
                    diagnostic = $"Desire Setting at index {index}: {diagnostic}";
                    return false;
                }

                PEGANpcDesireDefinition definition = configuration.Definition;
                if (!allDesireIds.Add(definition.DesireId))
                {
                    diagnostic = $"Desire ID '{definition.DesireId}' is duplicated in this profile.";
                    return false;
                }
            }

            return true;
        }

        public bool TryEvaluatePlanScore(
            float effectiveDesireWeight,
            float estimatedSeconds,
            out float costPreference,
            out float score)
        {
            costPreference = 0f;
            score = 0f;
            return planComparison != null &&
                   planComparison.TryEvaluateScore(
                       effectiveDesireWeight,
                       estimatedSeconds,
                       out costPreference,
                       out score);
        }

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Extensible policy that maps an effective Desire weight and estimated plan duration to separate
    /// cost-preference and comparison-score values. It does not choose or execute a plan.
    /// </summary>
    [Serializable]
    public abstract class PEGANpcPlanScorePolicy
    {
        public abstract bool TryValidate(float costReferenceSeconds, out string diagnostic);

        public abstract bool TryEvaluate(
            float effectiveDesireWeight,
            float estimatedSeconds,
            float costReferenceSeconds,
            out float costPreference,
            out float score);
    }

    /// <summary>Preserves the current reciprocal cost preference and weighted-cost product.</summary>
    [Serializable]
    public sealed class PEGANpcMultiplicativePlanScorePolicy : PEGANpcPlanScorePolicy
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

    [Serializable]
    public sealed class PEGANpcPlanSelectionPolicy
    {
        [SerializeField, Min(0.01f)]
        [FormerlySerializedAs("costReferenceSeconds")]
        [Tooltip("Reference time in seconds for comparing estimated plan durations. Shorter plans tend to score higher; this is a comparison scale, not a time limit.")]
        private float planTimeReferenceSeconds = 10f;

        [SerializeReference]
        [FormerlySerializedAs("scorePolicy")]
        [Tooltip("How a Desire's effective weight and estimated plan time combine when plans are compared.")]
        private PEGANpcPlanScorePolicy weightAndTimeBalance = new PEGANpcMultiplicativePlanScorePolicy();

        public PEGANpcPlanSelectionPolicy()
        {
        }

        internal PEGANpcPlanSelectionPolicy(float costReferenceSeconds)
        {
            planTimeReferenceSeconds = costReferenceSeconds;
        }

        public float CostReferenceSeconds => planTimeReferenceSeconds;

        public PEGANpcPlanScorePolicy ScorePolicy => weightAndTimeBalance;

        public bool TryEvaluateScore(
            float effectiveDesireWeight,
            float estimatedSeconds,
            out float costPreference,
            out float score)
        {
            costPreference = 0f;
            score = 0f;
            if (weightAndTimeBalance == null ||
                !IsPositiveFinite(effectiveDesireWeight) ||
                !IsNonNegativeFinite(estimatedSeconds) ||
                !weightAndTimeBalance.TryEvaluate(
                    effectiveDesireWeight,
                    estimatedSeconds,
                    planTimeReferenceSeconds,
                    out costPreference,
                    out score))
            {
                costPreference = 0f;
                score = 0f;
                return false;
            }

            if (!IsPositiveFinite(costPreference) || !IsPositiveFinite(score))
            {
                costPreference = 0f;
                score = 0f;
                return false;
            }

            return true;
        }

        internal bool TryValidate(out string diagnostic)
        {
            if (weightAndTimeBalance == null)
            {
                diagnostic = "A plan score policy is required.";
                return false;
            }

            if (!weightAndTimeBalance.TryValidate(planTimeReferenceSeconds, out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "Plan score policy configuration is invalid."
                    : $"Plan score policy: {diagnostic}";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

}

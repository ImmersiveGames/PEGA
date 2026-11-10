using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
namespace PEGA.NPC.Desires
{
    public enum PegaNpcDesireContext
    {
        Normal = 0
    }

    [CreateAssetMenu(
        fileName = "New Desire Profile",
        menuName = "PEGA/NPC/Desire Profiles")]
    public sealed class PegaNpcDesireProfile : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Desire Settings and plan comparison used in each gameplay context of this Profile.")]
        private PegaNpcDesireContextDefinition[] contexts = Array.Empty<PegaNpcDesireContextDefinition>();

        public bool TryGetContext(
            PegaNpcDesireContext context,
            out PegaNpcDesireContextDefinition definition,
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

            HashSet<PegaNpcDesireContext> seenContexts = new HashSet<PegaNpcDesireContext>();
            HashSet<string> seenDesireIds = new HashSet<string>(StringComparer.Ordinal);
            for (int contextIndex = 0; contextIndex < contexts.Length; contextIndex++)
            {
                PegaNpcDesireContextDefinition context = contexts[contextIndex];
                if (context == null)
                {
                    diagnostic = $"Desire context at index {contextIndex} is null.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(PegaNpcDesireContext), context.Context))
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
    public sealed class PegaNpcDesireContextDefinition
    {
        [SerializeField]
        [Tooltip("Contexto explícito em que estes Desejos podem ser avaliados.")]
        private PegaNpcDesireContext context;

        [SerializeField]
        [FormerlySerializedAs("planSelectionPolicy")]
        [Tooltip("How each Desire's effective weight and estimated plan time are compared. A higher weight influences selection but does not guarantee that a Desire wins.")]
        private PegaNpcPlanSelectionPolicy planComparison = new PegaNpcPlanSelectionPolicy();

        [SerializeField]
        [FormerlySerializedAs("desires")]
        [Tooltip("Desire Settings assigned to this Profile and context. Settings are reusable assets; editing a shared Setting affects every Profile that references it.")]
        private PegaNpcDesireConfiguration[] desireSettings = Array.Empty<PegaNpcDesireConfiguration>();

        public PegaNpcDesireContextDefinition()
        {
        }

        internal PegaNpcDesireContextDefinition(
            PegaNpcDesireContext context,
            float costReferenceSeconds,
            PegaNpcPlanScorePolicy scorePolicy,
            PegaNpcDesireConfiguration[] desireSettings)
        {
            this.context = context;
            planComparison = new PegaNpcPlanSelectionPolicy(costReferenceSeconds, scorePolicy);
            this.desireSettings = desireSettings != null
                ? (PegaNpcDesireConfiguration[])desireSettings.Clone()
                : Array.Empty<PegaNpcDesireConfiguration>();
        }

        public PegaNpcDesireContext Context => context;

        public PegaNpcPlanSelectionPolicy PlanSelectionPolicy => planComparison;

        public IReadOnlyList<PegaNpcDesireConfiguration> Desires =>
            Array.AsReadOnly(desireSettings ?? Array.Empty<PegaNpcDesireConfiguration>());

        internal PegaNpcDesireConfiguration[] Configurations =>
            desireSettings ?? Array.Empty<PegaNpcDesireConfiguration>();

        internal bool TryValidate(HashSet<string> allDesireIds, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!Enum.IsDefined(typeof(PegaNpcDesireContext), context))
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
                PegaNpcDesireConfiguration configuration = desireSettings[index];
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

                PegaNpcDesireDefinition definition = configuration.Definition;
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

    [Serializable]
    public sealed class PegaNpcPlanSelectionPolicy
    {
        [SerializeField, Min(0.01f)]
        [FormerlySerializedAs("costReferenceSeconds")]
        [Tooltip("Reference time in seconds for comparing estimated plan durations. Shorter plans tend to score higher; this is a comparison scale, not a time limit.")]
        private float planTimeReferenceSeconds = 10f;

        [SerializeField]
        [Tooltip("How a Desire's effective weight and estimated plan time combine when plans are compared.")]
        private PegaNpcPlanScorePolicy weightAndTimeBalance;

        public PegaNpcPlanSelectionPolicy()
        {
        }

        internal PegaNpcPlanSelectionPolicy(
            float costReferenceSeconds,
            PegaNpcPlanScorePolicy weightAndTimeBalance)
        {
            planTimeReferenceSeconds = costReferenceSeconds;
            this.weightAndTimeBalance = weightAndTimeBalance;
        }

        public float CostReferenceSeconds => planTimeReferenceSeconds;

        public PegaNpcPlanScorePolicy ScorePolicy => weightAndTimeBalance;

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

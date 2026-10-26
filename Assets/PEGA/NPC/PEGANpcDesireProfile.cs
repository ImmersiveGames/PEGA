using System;
using System.Collections.Generic;
using UnityEngine;

namespace PEGA.NPC
{
    public enum PEGANpcDesireContext
    {
        Normal = 0
    }

    [CreateAssetMenu(
        fileName = "PEGANpcDesireProfile",
        menuName = "PEGA/NPC/Desire Profile")]
    public sealed class PEGANpcDesireProfile : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Configuração explícita por Contexto de gameplay. Contextos ausentes não recebem configuração padrão.")]
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

            diagnostic = $"No Desire configuration is declared for context {context}.";
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
        [Tooltip("Política determinística que combina peso de Desejo e custo estimado do plano.")]
        private PEGANpcPlanSelectionPolicy planSelectionPolicy = new PEGANpcPlanSelectionPolicy();

        [SerializeField]
        [Tooltip("Configurações deste Perfil/Contexto que referenciam definições reutilizáveis de Desejo.")]
        private PEGANpcDesireConfiguration[] desires = Array.Empty<PEGANpcDesireConfiguration>();

        public PEGANpcDesireContextDefinition()
        {
        }

        internal PEGANpcDesireContextDefinition(
            PEGANpcDesireContext context,
            float costReferenceSeconds,
            PEGANpcDesireConfiguration[] desires)
        {
            this.context = context;
            planSelectionPolicy = new PEGANpcPlanSelectionPolicy(costReferenceSeconds);
            this.desires = desires != null
                ? (PEGANpcDesireConfiguration[])desires.Clone()
                : Array.Empty<PEGANpcDesireConfiguration>();
        }

        public PEGANpcDesireContext Context => context;

        public PEGANpcPlanSelectionPolicy PlanSelectionPolicy => planSelectionPolicy;

        public IReadOnlyList<PEGANpcDesireConfiguration> Desires =>
            Array.AsReadOnly(desires ?? Array.Empty<PEGANpcDesireConfiguration>());

        internal PEGANpcDesireConfiguration[] Configurations =>
            desires ?? Array.Empty<PEGANpcDesireConfiguration>();

        internal bool TryValidate(HashSet<string> allDesireIds, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!Enum.IsDefined(typeof(PEGANpcDesireContext), context))
            {
                diagnostic = "Context has an unsupported value.";
                return false;
            }

            if (planSelectionPolicy == null || !planSelectionPolicy.TryValidate(out diagnostic))
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "A plan-selection policy is required."
                    : diagnostic;
                return false;
            }

            if (desires == null)
            {
                diagnostic = "Desire definitions cannot be null.";
                return false;
            }

            for (int index = 0; index < desires.Length; index++)
            {
                PEGANpcDesireConfiguration configuration = desires[index];
                if (configuration == null)
                {
                    diagnostic = $"Desire configuration at index {index} is null.";
                    return false;
                }

                if (!configuration.TryValidate(out diagnostic))
                {
                    diagnostic = $"Desire configuration at index {index}: {diagnostic}";
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
            return planSelectionPolicy != null &&
                   planSelectionPolicy.TryEvaluateScore(
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
        [Tooltip("Tempo de referência (s) usado pela política padrão: 1 / (1 + tempo estimado / referência).")]
        private float costReferenceSeconds = 10f;

        [SerializeReference]
        [Tooltip("Política determinística que combina o peso efetivo do Desejo e a preferência pelo custo do plano.")]
        private PEGANpcPlanScorePolicy scorePolicy = new PEGANpcMultiplicativePlanScorePolicy();

        public PEGANpcPlanSelectionPolicy()
        {
        }

        internal PEGANpcPlanSelectionPolicy(float costReferenceSeconds)
        {
            this.costReferenceSeconds = costReferenceSeconds;
        }

        public float CostReferenceSeconds => costReferenceSeconds;

        public PEGANpcPlanScorePolicy ScorePolicy => scorePolicy;

        public bool TryEvaluateScore(
            float effectiveDesireWeight,
            float estimatedSeconds,
            out float costPreference,
            out float score)
        {
            costPreference = 0f;
            score = 0f;
            if (scorePolicy == null ||
                !IsPositiveFinite(effectiveDesireWeight) ||
                !IsNonNegativeFinite(estimatedSeconds) ||
                !scorePolicy.TryEvaluate(
                    effectiveDesireWeight,
                    estimatedSeconds,
                    costReferenceSeconds,
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
            if (scorePolicy == null)
            {
                diagnostic = "A plan score policy is required.";
                return false;
            }

            if (!scorePolicy.TryValidate(costReferenceSeconds, out diagnostic))
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

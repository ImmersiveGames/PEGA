using System;
using System.Collections.Generic;
using UnityEngine;

namespace PEGA.NPC
{
    public enum PEGANpcDesireContext
    {
        Normal = 0
    }

    public enum PEGANpcDesireGoalKind
    {
        ReachKnownCharacterPosition = 0,
        ReachKnownEscapeDestination = 10
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
        [Tooltip("Definições individuais de Desejo para este Contexto.")]
        private PEGANpcDesireDefinition[] desires = Array.Empty<PEGANpcDesireDefinition>();

        public PEGANpcDesireContextDefinition()
        {
        }

        internal PEGANpcDesireContextDefinition(
            PEGANpcDesireContext context,
            float costReferenceSeconds,
            PEGANpcDesireDefinition[] desires)
        {
            this.context = context;
            planSelectionPolicy = new PEGANpcPlanSelectionPolicy(costReferenceSeconds);
            this.desires = desires != null
                ? (PEGANpcDesireDefinition[])desires.Clone()
                : Array.Empty<PEGANpcDesireDefinition>();
        }

        public PEGANpcDesireContext Context => context;

        public PEGANpcPlanSelectionPolicy PlanSelectionPolicy => planSelectionPolicy;

        public IReadOnlyList<PEGANpcDesireDefinition> Desires =>
            Array.AsReadOnly(desires ?? Array.Empty<PEGANpcDesireDefinition>());

        internal PEGANpcDesireDefinition[] Definitions => desires ?? Array.Empty<PEGANpcDesireDefinition>();

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
                PEGANpcDesireDefinition desire = desires[index];
                if (desire == null)
                {
                    diagnostic = $"Desire definition at index {index} is null.";
                    return false;
                }

                if (!desire.TryValidate(out diagnostic))
                {
                    diagnostic = $"Desire at index {index}: {diagnostic}";
                    return false;
                }

                if (!allDesireIds.Add(desire.DesireId))
                {
                    diagnostic = $"Desire ID '{desire.DesireId}' is duplicated in this profile.";
                    return false;
                }
            }

            return true;
        }

        public float EvaluateCostPreference(float estimatedSeconds)
        {
            if (planSelectionPolicy == null || !IsNonNegativeFinite(estimatedSeconds))
            {
                return 0f;
            }

            return planSelectionPolicy.EvaluateCostPreference(estimatedSeconds);
        }

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class PEGANpcPlanSelectionPolicy
    {
        [SerializeField, Min(0.01f)]
        [Tooltip("Tempo de referência (s) da curva de preferência 1 / (1 + tempo estimado / referência).")]
        private float costReferenceSeconds = 10f;

        public PEGANpcPlanSelectionPolicy()
        {
        }

        internal PEGANpcPlanSelectionPolicy(float costReferenceSeconds)
        {
            this.costReferenceSeconds = costReferenceSeconds;
        }

        public float CostReferenceSeconds => costReferenceSeconds;

        public float EvaluateCostPreference(float estimatedSeconds)
        {
            if (!IsPositiveFinite(costReferenceSeconds) || !IsNonNegativeFinite(estimatedSeconds))
            {
                return 0f;
            }

            return 1f / (1f + estimatedSeconds / costReferenceSeconds);
        }

        internal bool TryValidate(out string diagnostic)
        {
            bool valid = IsPositiveFinite(costReferenceSeconds);
            diagnostic = valid
                ? string.Empty
                : "Cost reference time must be finite and greater than zero.";
            return valid;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class PEGANpcDesireDefinition
    {
        [SerializeField]
        [Tooltip("ID estável PEGA, único dentro do profile e usado pelo ledger individual.")]
        private string desireId;

        [SerializeField]
        [Tooltip("Nome legível para diagnóstico e configuração.")]
        private string displayName;

        [SerializeField]
        [Tooltip("Quando desativado, este Desejo não é aplicável no Contexto.")]
        private bool enabledForContext = true;

        [SerializeField]
        [Tooltip("Predicado de estado desejado, avaliado separadamente dos efeitos das Actions disponíveis.")]
        private PEGANpcDesireGoalKind desiredGoal;

        [SerializeField, Min(0.001f)]
        [Tooltip("Peso base do Desejo antes do modificador de satisfação.")]
        private float baseWeight = 1f;

        [SerializeField, Min(0.001f)]
        [Tooltip("Acréscimo de satisfação aplicado por execução concluída que cumpre o efeito desejado.")]
        private float satisfactionGain = 1f;

        [SerializeField, Min(0.001f)]
        [Tooltip("Teto da satisfação acumulada e referência para o modificador de peso; não limita a reaplicabilidade do Desejo.")]
        private float maximumSatisfaction = 1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Fração máxima de redução do peso base conforme a satisfação acumulada progride.")]
        private float satisfactionWeightModifier;

        [SerializeField, Min(-1)]
        [Tooltip("Máximo de execuções que podem satisfazer este Desejo; -1 significa ilimitado.")]
        private int maximumCompletedExecutions = -1;

        public PEGANpcDesireDefinition()
        {
        }

        internal PEGANpcDesireDefinition(
            string desireId,
            string displayName,
            bool enabledForContext,
            PEGANpcDesireGoalKind desiredGoal,
            float baseWeight,
            float satisfactionGain,
            float maximumSatisfaction,
            float satisfactionWeightModifier,
            int maximumCompletedExecutions)
        {
            this.desireId = desireId;
            this.displayName = displayName;
            this.enabledForContext = enabledForContext;
            this.desiredGoal = desiredGoal;
            this.baseWeight = baseWeight;
            this.satisfactionGain = satisfactionGain;
            this.maximumSatisfaction = maximumSatisfaction;
            this.satisfactionWeightModifier = satisfactionWeightModifier;
            this.maximumCompletedExecutions = maximumCompletedExecutions;
        }

        public string DesireId => desireId;

        public string DisplayName => displayName;

        public PEGANpcDesireGoalKind DesiredGoal => desiredGoal;

        public float BaseWeight => baseWeight;

        public float SatisfactionGain => satisfactionGain;

        public float MaximumSatisfaction => maximumSatisfaction;

        public float SatisfactionWeightModifier => satisfactionWeightModifier;

        public int MaximumCompletedExecutions => maximumCompletedExecutions;

        public bool IsApplicable(PEGANpcDesireSatisfactionSnapshot satisfaction)
        {
            if (!enabledForContext)
            {
                return false;
            }

            return maximumCompletedExecutions < 0 ||
                   satisfaction.CompletedExecutions < maximumCompletedExecutions;
        }

        public float EvaluateEffectiveWeight(PEGANpcDesireSatisfactionSnapshot satisfaction)
        {
            float normalizedSatisfaction = maximumSatisfaction > 0f
                ? Mathf.Clamp01(satisfaction.Satisfaction / maximumSatisfaction)
                : 1f;
            float satisfactionFactor = 1f - satisfactionWeightModifier * normalizedSatisfaction;
            return baseWeight * Mathf.Max(0f, satisfactionFactor);
        }

        internal bool Matches(PEGANpcObservedActionEffect observedEffect) =>
            observedEffect.IsValid && observedEffect.GoalKind == desiredGoal;

        internal bool CanRecordSatisfaction(PEGANpcDesireSatisfactionSnapshot satisfaction) =>
            enabledForContext &&
            (maximumCompletedExecutions < 0 ||
             satisfaction.CompletedExecutions < maximumCompletedExecutions);

        internal bool TryValidate(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (string.IsNullOrWhiteSpace(desireId))
            {
                diagnostic = "Desire ID must be a non-empty stable PEGA identifier.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                diagnostic = $"Desire '{desireId}' requires a readable display name.";
                return false;
            }

            if (!Enum.IsDefined(typeof(PEGANpcDesireGoalKind), desiredGoal))
            {
                diagnostic = $"Desire '{desireId}' uses an unsupported goal predicate.";
                return false;
            }

            if (!IsPositiveFinite(baseWeight))
            {
                diagnostic = $"Desire '{desireId}' base weight must be finite and greater than zero.";
                return false;
            }

            if (!IsPositiveFinite(satisfactionGain) || !IsPositiveFinite(maximumSatisfaction) ||
                satisfactionGain > maximumSatisfaction)
            {
                diagnostic = $"Desire '{desireId}' satisfaction gain and maximum must be finite, positive, and gain cannot exceed the maximum.";
                return false;
            }

            if (!IsNonNegativeFinite(satisfactionWeightModifier) || satisfactionWeightModifier > 1f)
            {
                diagnostic = $"Desire '{desireId}' satisfaction weight modifier must be between zero and one.";
                return false;
            }

            if (maximumCompletedExecutions == 0 || maximumCompletedExecutions < -1)
            {
                diagnostic = $"Desire '{desireId}' execution limit must be -1 (unlimited) or a positive count.";
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

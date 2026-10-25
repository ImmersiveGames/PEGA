using UnityEngine;

namespace PEGA.NPC
{
    [CreateAssetMenu(
        fileName = "PEGANpcDesireConfiguration",
        menuName = "PEGA/NPC/Desire Configuration")]
    public sealed class PEGANpcDesireConfiguration : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Definição semântica reutilizável deste Desejo.")]
        private PEGANpcDesireDefinition definition;

        [SerializeField]
        [Tooltip("Quando desativado, este Desejo não é aplicável na composição que usa esta configuração.")]
        private bool enabledForContext = true;

        [SerializeField, Min(0.001f)]
        [Tooltip("Peso base do Desejo antes do modificador de satisfação.")]
        private float baseWeight = 1f;

        [SerializeField, Min(0.001f)]
        [Tooltip("Acréscimo de satisfação aplicado por execução concluída que cumpre o predicado do Desejo.")]
        private float satisfactionGain = 1f;

        [SerializeField, Min(0.001f)]
        [Tooltip("Teto da satisfação acumulada e referência do modificador de peso; não limita a reaplicabilidade do Desejo.")]
        private float maximumSatisfaction = 1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Fração máxima de redução do peso base conforme a satisfação acumulada progride.")]
        private float satisfactionWeightModifier;

        [SerializeField, Min(-1)]
        [Tooltip("Máximo de execuções que podem satisfazer este Desejo; -1 significa ilimitado.")]
        private int maximumCompletedExecutions = -1;

        public PEGANpcDesireDefinition Definition => definition;

        public bool EnabledForContext => enabledForContext;

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

        internal bool CanRecordSatisfaction(PEGANpcDesireSatisfactionSnapshot satisfaction) =>
            enabledForContext &&
            (maximumCompletedExecutions < 0 ||
             satisfaction.CompletedExecutions < maximumCompletedExecutions);

        internal bool TryValidate(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (definition == null)
            {
                diagnostic = "A reusable Desire definition is required.";
                return false;
            }

            if (!definition.TryValidate(out diagnostic))
            {
                diagnostic = $"Definition: {diagnostic}";
                return false;
            }

            if (!IsPositiveFinite(baseWeight))
            {
                diagnostic = $"Desire '{definition.DesireId}' base weight must be finite and greater than zero.";
                return false;
            }

            if (!IsPositiveFinite(satisfactionGain) || !IsPositiveFinite(maximumSatisfaction) ||
                satisfactionGain > maximumSatisfaction)
            {
                diagnostic = $"Desire '{definition.DesireId}' satisfaction gain and maximum must be finite, positive, and gain cannot exceed the maximum.";
                return false;
            }

            if (!IsNonNegativeFinite(satisfactionWeightModifier) || satisfactionWeightModifier > 1f)
            {
                diagnostic = $"Desire '{definition.DesireId}' satisfaction weight modifier must be between zero and one.";
                return false;
            }

            if (maximumCompletedExecutions == 0 || maximumCompletedExecutions < -1)
            {
                diagnostic = $"Desire '{definition.DesireId}' execution limit must be -1 (unlimited) or a positive count.";
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

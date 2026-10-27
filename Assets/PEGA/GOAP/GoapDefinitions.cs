using System;
using System.Collections.Generic;

namespace ImmersiveGames.Goap
{
    /// <summary>
    /// Declara uma mudança de estado prevista para a conclusão bem-sucedida de uma Action.
    /// Implementações são declarações imutáveis, sem dependência de Goals, e fornecem a semântica específica do domínio;
    /// a declaração não executa nem aplica a mudança.
    /// </summary>
    public interface IGoapEffect
    {
    }

    /// <summary>Descreve o estado-alvo de um Goal sem prescrever Actions para alcançá-lo.</summary>
    public sealed class GoapGoal
    {
        public GoapGoal(IGoapCondition targetCondition)
        {
            TargetCondition = targetCondition ?? throw new ArgumentNullException(nameof(targetCondition));
        }

        public IGoapCondition TargetCondition { get; }

        public GoapTruthValue Evaluate(IGoapStateView state) =>
            GoapConditionEvaluation.Evaluate(TargetCondition, state);
    }

    /// <summary>
    /// Declara o contrato de sucesso de planejamento de uma Action. Todas as precondições precisam ser satisfeitas;
    /// Effects descrevem mudanças previstas e não possuem associação com Goals. Este tipo não executa Actions,
    /// não avalia precondições e não projeta Effects.
    /// </summary>
    public sealed class GoapActionDefinition
    {
        public GoapActionDefinition(
            IEnumerable<IGoapCondition> successPreconditions,
            IEnumerable<IGoapEffect> effects)
        {
            SuccessPreconditions = CopyConditions(successPreconditions);
            Effects = CopyEffects(effects);
        }

        /// <summary>
        /// Condições necessárias ao sucesso previsto. A coleção vazia representa uma Action sem precondições
        /// adicionais; uma condição Desconhecida não pode ser tratada como satisfeita pelo planejamento.
        /// </summary>
        public IReadOnlyList<IGoapCondition> SuccessPreconditions { get; }

        /// <summary>Effects declarativos previstos para a conclusão bem-sucedida, sem vínculo a qualquer Goal.</summary>
        public IReadOnlyList<IGoapEffect> Effects { get; }

        private static IReadOnlyList<IGoapCondition> CopyConditions(
            IEnumerable<IGoapCondition> conditions)
        {
            if (conditions == null)
            {
                throw new ArgumentNullException(nameof(conditions));
            }

            List<IGoapCondition> copy = new List<IGoapCondition>();
            foreach (IGoapCondition condition in conditions)
            {
                copy.Add(condition ?? throw new ArgumentException(
                    "Success preconditions cannot contain null conditions.",
                    nameof(conditions)));
            }

            return copy.AsReadOnly();
        }

        private static IReadOnlyList<IGoapEffect> CopyEffects(IEnumerable<IGoapEffect> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            List<IGoapEffect> copy = new List<IGoapEffect>();
            foreach (IGoapEffect effect in effects)
            {
                copy.Add(effect ?? throw new ArgumentException(
                    "Effects cannot contain null declarations.",
                    nameof(effects)));
            }

            return copy.AsReadOnly();
        }
    }
}

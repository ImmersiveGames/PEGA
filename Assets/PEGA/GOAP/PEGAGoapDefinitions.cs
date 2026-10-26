using System;
using System.Collections.Generic;

namespace PEGA.GOAP
{
    /// <summary>
    /// Declara uma mudança de estado prevista para a conclusão bem-sucedida de uma Action.
    /// Implementações são declarações imutáveis, sem dependência de Goals, e fornecem a semântica específica do domínio;
    /// a declaração não executa nem aplica a mudança.
    /// </summary>
    public interface IPEGAGoapEffect
    {
    }

    /// <summary>Descreve o estado-alvo de um Goal sem prescrever Actions para alcançá-lo.</summary>
    public sealed class PEGAGoapGoal
    {
        public PEGAGoapGoal(IPEGAGoapCondition targetCondition)
        {
            TargetCondition = targetCondition ?? throw new ArgumentNullException(nameof(targetCondition));
        }

        public IPEGAGoapCondition TargetCondition { get; }

        public PEGAGoapTruthValue Evaluate(IPEGAGoapStateView state) =>
            PEGAGoapConditionEvaluation.Evaluate(TargetCondition, state);
    }

    /// <summary>
    /// Declara o contrato de sucesso de planejamento de uma Action. Todas as precondições precisam ser satisfeitas;
    /// Effects descrevem mudanças previstas e não possuem associação com Goals. Este tipo não executa Actions,
    /// não avalia precondições e não projeta Effects.
    /// </summary>
    public sealed class PEGAGoapActionDefinition
    {
        public PEGAGoapActionDefinition(
            IEnumerable<IPEGAGoapCondition> successPreconditions,
            IEnumerable<IPEGAGoapEffect> effects)
        {
            SuccessPreconditions = CopyConditions(successPreconditions);
            Effects = CopyEffects(effects);
        }

        /// <summary>
        /// Condições necessárias ao sucesso previsto. A coleção vazia representa uma Action sem precondições
        /// adicionais; uma condição Desconhecida não pode ser tratada como satisfeita pelo planejamento.
        /// </summary>
        public IReadOnlyList<IPEGAGoapCondition> SuccessPreconditions { get; }

        /// <summary>Effects declarativos previstos para a conclusão bem-sucedida, sem vínculo a qualquer Goal.</summary>
        public IReadOnlyList<IPEGAGoapEffect> Effects { get; }

        private static IReadOnlyList<IPEGAGoapCondition> CopyConditions(
            IEnumerable<IPEGAGoapCondition> conditions)
        {
            if (conditions == null)
            {
                throw new ArgumentNullException(nameof(conditions));
            }

            List<IPEGAGoapCondition> copy = new List<IPEGAGoapCondition>();
            foreach (IPEGAGoapCondition condition in conditions)
            {
                copy.Add(condition ?? throw new ArgumentException(
                    "Success preconditions cannot contain null conditions.",
                    nameof(conditions)));
            }

            return copy.AsReadOnly();
        }

        private static IReadOnlyList<IPEGAGoapEffect> CopyEffects(IEnumerable<IPEGAGoapEffect> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            List<IPEGAGoapEffect> copy = new List<IPEGAGoapEffect>();
            foreach (IPEGAGoapEffect effect in effects)
            {
                copy.Add(effect ?? throw new ArgumentException(
                    "Effects cannot contain null declarations.",
                    nameof(effects)));
            }

            return copy.AsReadOnly();
        }
    }
}

using System;
using System.Collections.Generic;

namespace PEGA.GOAP
{
    /// <summary>
    /// Vista somente para leitura que separa o conhecimento factual da camada de Effects projetados.
    /// Effects projetados não se tornam fatos do Knowledge State.
    /// </summary>
    public interface IPEGAGoapHypotheticalStateView : IPEGAGoapEffectStateView
    {
        IPEGAGoapKnowledgeStateView KnowledgeState { get; }

        IReadOnlyList<IPEGAGoapEffect> ProjectedEffects { get; }
    }

    /// <summary>
    /// Estado hipotético imutável para avaliação de condições. Cada projeção cria uma nova vista que conserva
    /// o mesmo Knowledge State e acumula Effects declarativos sem alterar as fontes factuais.
    /// </summary>
    public sealed class PEGAGoapHypotheticalState : IPEGAGoapHypotheticalStateView
    {
        private readonly IPEGAGoapKnowledgeStateView _knowledgeState;
        private readonly IReadOnlyList<IPEGAGoapEffect> _projectedEffects;

        public PEGAGoapHypotheticalState(IPEGAGoapKnowledgeStateView knowledgeState)
            : this(knowledgeState, new List<IPEGAGoapEffect>())
        {
        }

        private PEGAGoapHypotheticalState(
            IPEGAGoapKnowledgeStateView knowledgeState,
            IEnumerable<IPEGAGoapEffect> projectedEffects)
        {
            PEGAGoapConditionEvaluation.ValidateState(knowledgeState);
            if (knowledgeState.Source != PEGAGoapStateSource.KnowledgeState)
            {
                throw new ArgumentException(
                    "A hypothetical planning state must start from a Knowledge State view.",
                    nameof(knowledgeState));
            }

            _knowledgeState = knowledgeState;
            _projectedEffects = CopyEffects(projectedEffects);
        }

        public PEGAGoapStateSource Source => PEGAGoapStateSource.HypotheticalState;

        public IPEGAGoapKnowledgeStateView KnowledgeState => _knowledgeState;

        public IReadOnlyList<IPEGAGoapEffect> ProjectedEffects => _projectedEffects;

        public IReadOnlyList<IPEGAGoapEffect> Effects => _projectedEffects;

        /// <summary>
        /// Retorna uma nova vista hipotética com os Effects declarados pela Action acrescentados em ordem.
        /// Não executa a Action nem avalia suas Preconditions.
        /// </summary>
        public PEGAGoapHypotheticalState Project(PEGAGoapActionDefinition action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (action.Effects.Count == 0)
            {
                return this;
            }

            List<IPEGAGoapEffect> projectedEffects = new List<IPEGAGoapEffect>(
                _projectedEffects.Count + action.Effects.Count);
            for (int index = 0; index < _projectedEffects.Count; index++)
            {
                projectedEffects.Add(_projectedEffects[index]);
            }

            for (int index = 0; index < action.Effects.Count; index++)
            {
                projectedEffects.Add(action.Effects[index]);
            }

            return new PEGAGoapHypotheticalState(_knowledgeState, projectedEffects);
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
                    "Projected effects cannot contain null declarations.",
                    nameof(effects)));
            }

            return copy.AsReadOnly();
        }
    }
}

using System;
using System.Collections.Generic;

namespace ImmersiveGames.Goap
{
    /// <summary>
    /// Vista somente para leitura que separa o conhecimento factual da camada de Effects projetados.
    /// Effects projetados não se tornam fatos do Knowledge State.
    /// </summary>
    public interface IGoapHypotheticalStateView : IGoapEffectStateView
    {
        IGoapKnowledgeStateView KnowledgeState { get; }

        IReadOnlyList<IGoapEffect> ProjectedEffects { get; }
    }

    /// <summary>
    /// Estado hipotético imutável para avaliação de condições. Cada projeção cria uma nova vista que conserva
    /// o mesmo Knowledge State e acumula Effects declarativos sem alterar as fontes factuais.
    /// </summary>
    public sealed class GoapHypotheticalState : IGoapHypotheticalStateView
    {
        private readonly IGoapKnowledgeStateView _knowledgeState;
        private readonly IReadOnlyList<IGoapEffect> _projectedEffects;

        public GoapHypotheticalState(IGoapKnowledgeStateView knowledgeState)
            : this(knowledgeState, new List<IGoapEffect>())
        {
        }

        private GoapHypotheticalState(
            IGoapKnowledgeStateView knowledgeState,
            IEnumerable<IGoapEffect> projectedEffects)
        {
            GoapConditionEvaluation.ValidateState(knowledgeState);
            if (knowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                throw new ArgumentException(
                    "A hypothetical planning state must start from a Knowledge State view.",
                    nameof(knowledgeState));
            }

            _knowledgeState = knowledgeState;
            _projectedEffects = CopyEffects(projectedEffects);
        }

        public GoapStateSource Source => GoapStateSource.HypotheticalState;

        public IGoapKnowledgeStateView KnowledgeState => _knowledgeState;

        public IReadOnlyList<IGoapEffect> ProjectedEffects => _projectedEffects;

        public IReadOnlyList<IGoapEffect> Effects => _projectedEffects;

        /// <summary>
        /// Retorna uma nova vista hipotética com os Effects declarados pela Action acrescentados em ordem.
        /// Não executa a Action nem avalia suas Preconditions.
        /// </summary>
        public GoapHypotheticalState Project(GoapActionDefinition action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (action.Effects.Count == 0)
            {
                return this;
            }

            List<IGoapEffect> projectedEffects = new List<IGoapEffect>(
                _projectedEffects.Count + action.Effects.Count);
            for (int index = 0; index < _projectedEffects.Count; index++)
            {
                projectedEffects.Add(_projectedEffects[index]);
            }

            for (int index = 0; index < action.Effects.Count; index++)
            {
                projectedEffects.Add(action.Effects[index]);
            }

            return new GoapHypotheticalState(_knowledgeState, projectedEffects);
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
                    "Projected effects cannot contain null declarations.",
                    nameof(effects)));
            }

            return copy.AsReadOnly();
        }
    }
}

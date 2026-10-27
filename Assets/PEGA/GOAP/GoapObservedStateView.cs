using System;
using System.Collections.Generic;

namespace ImmersiveGames.Goap
{
    /// <summary>
    /// Read-only view of Effects confirmed by an observed successful Action. It keeps the Actor's Knowledge
    /// State available to conditions while exposing only the outcome evidence supplied by the Action owner.
    /// </summary>
    public interface IGoapObservedStateView : IGoapEffectStateView
    {
        IGoapKnowledgeStateView KnowledgeState { get; }

        IReadOnlyList<IGoapEffect> ObservedEffects { get; }
    }

    public sealed class GoapObservedStateView : IGoapObservedStateView
    {
        private readonly IReadOnlyList<IGoapEffect> _observedEffects;

        public GoapObservedStateView(
            IGoapKnowledgeStateView knowledgeState,
            IEnumerable<IGoapEffect> observedEffects)
        {
            GoapConditionEvaluation.ValidateState(knowledgeState);
            if (knowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                throw new ArgumentException(
                    "An observed outcome must retain an explicit Knowledge State view.",
                    nameof(knowledgeState));
            }

            KnowledgeState = knowledgeState;
            _observedEffects = CopyEffects(observedEffects);
        }

        /// <summary>The observed Action outcome is real world evidence, never a hypothetical projection.</summary>
        public GoapStateSource Source => GoapStateSource.WorldState;

        public IGoapKnowledgeStateView KnowledgeState { get; }

        public IReadOnlyList<IGoapEffect> ObservedEffects => _observedEffects;

        public IReadOnlyList<IGoapEffect> Effects => _observedEffects;

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
                    "Observed Effects cannot contain null declarations.",
                    nameof(effects)));
            }

            return copy.AsReadOnly();
        }
    }
}

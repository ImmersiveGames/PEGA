using System;
using System.Collections.Generic;

namespace PEGA.GOAP
{
    /// <summary>
    /// Read-only view of Effects confirmed by an observed successful Action. It keeps the Actor's Knowledge
    /// State available to conditions while exposing only the outcome evidence supplied by the Action owner.
    /// </summary>
    public interface IPEGAGoapObservedStateView : IPEGAGoapEffectStateView
    {
        IPEGAGoapKnowledgeStateView KnowledgeState { get; }

        IReadOnlyList<IPEGAGoapEffect> ObservedEffects { get; }
    }

    public sealed class PEGAGoapObservedStateView : IPEGAGoapObservedStateView
    {
        private readonly IReadOnlyList<IPEGAGoapEffect> _observedEffects;

        public PEGAGoapObservedStateView(
            IPEGAGoapKnowledgeStateView knowledgeState,
            IEnumerable<IPEGAGoapEffect> observedEffects)
        {
            PEGAGoapConditionEvaluation.ValidateState(knowledgeState);
            if (knowledgeState.Source != PEGAGoapStateSource.KnowledgeState)
            {
                throw new ArgumentException(
                    "An observed outcome must retain an explicit Knowledge State view.",
                    nameof(knowledgeState));
            }

            KnowledgeState = knowledgeState;
            _observedEffects = CopyEffects(observedEffects);
        }

        /// <summary>The observed Action outcome is real world evidence, never a hypothetical projection.</summary>
        public PEGAGoapStateSource Source => PEGAGoapStateSource.WorldState;

        public IPEGAGoapKnowledgeStateView KnowledgeState { get; }

        public IReadOnlyList<IPEGAGoapEffect> ObservedEffects => _observedEffects;

        public IReadOnlyList<IPEGAGoapEffect> Effects => _observedEffects;

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
                    "Observed Effects cannot contain null declarations.",
                    nameof(effects)));
            }

            return copy.AsReadOnly();
        }
    }
}

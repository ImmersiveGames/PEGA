using System;

namespace PEGA.GOAP
{
    public enum PEGAGoapTruthValue
    {
        Unknown = 0,
        False = 1,
        True = 2
    }

    public enum PEGAGoapStateSource
    {
        Unspecified = 0,
        WorldState = 1,
        KnowledgeState = 2,
        HypotheticalState = 3
    }

    /// <summary>Vista de estado somente para leitura, identificada pela autoridade que a fornece.</summary>
    public interface IPEGAGoapStateView
    {
        PEGAGoapStateSource Source { get; }
    }

    /// <summary>
    /// Knowledge contributed by one explicitly composed gameplay capability. A condition may inspect the
    /// capabilities exposed by its Knowledge State without requiring the planner to recognize their types.
    /// </summary>
    public interface IPEGAGoapKnowledgeCapability
    {
    }

    /// <summary>A read-only Knowledge State view and its composed, independently owned knowledge capabilities.</summary>
    public interface IPEGAGoapKnowledgeStateView : IPEGAGoapStateView
    {
        System.Collections.Generic.IReadOnlyList<IPEGAGoapKnowledgeCapability> Capabilities { get; }
    }

    /// <summary>Read-only state views that expose Effects as facts for condition evaluation.</summary>
    public interface IPEGAGoapEffectStateView : IPEGAGoapStateView
    {
        System.Collections.Generic.IReadOnlyList<IPEGAGoapEffect> Effects { get; }
    }

    /// <summary>
    /// Condição de GOAP avaliada contra uma fonte explícita. Implementações devem observar a vista sem
    /// alterá-la; os operadores compostos encaminham a mesma vista a cada condição filha.
    /// </summary>
    public interface IPEGAGoapCondition
    {
        PEGAGoapTruthValue Evaluate(IPEGAGoapStateView state);
    }

    public sealed class PEGAGoapNotCondition : IPEGAGoapCondition
    {
        private readonly IPEGAGoapCondition _condition;

        public PEGAGoapNotCondition(IPEGAGoapCondition condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public PEGAGoapTruthValue Evaluate(IPEGAGoapStateView state)
        {
            PEGAGoapConditionEvaluation.ValidateState(state);
            switch (PEGAGoapConditionEvaluation.Evaluate(_condition, state))
            {
                case PEGAGoapTruthValue.True:
                    return PEGAGoapTruthValue.False;
                case PEGAGoapTruthValue.False:
                    return PEGAGoapTruthValue.True;
                case PEGAGoapTruthValue.Unknown:
                    return PEGAGoapTruthValue.Unknown;
                default:
                    throw new InvalidOperationException("Condition returned an unsupported truth value.");
            }
        }
    }

    public sealed class PEGAGoapAndCondition : IPEGAGoapCondition
    {
        private readonly IPEGAGoapCondition[] _conditions;

        public PEGAGoapAndCondition(params IPEGAGoapCondition[] conditions)
        {
            _conditions = PEGAGoapConditionEvaluation.CopyConditions(conditions);
        }

        public PEGAGoapTruthValue Evaluate(IPEGAGoapStateView state)
        {
            PEGAGoapConditionEvaluation.ValidateState(state);
            bool hasUnknown = false;

            for (int index = 0; index < _conditions.Length; index++)
            {
                PEGAGoapTruthValue value = PEGAGoapConditionEvaluation.Evaluate(_conditions[index], state);
                if (value == PEGAGoapTruthValue.False)
                {
                    return PEGAGoapTruthValue.False;
                }

                hasUnknown |= value == PEGAGoapTruthValue.Unknown;
            }

            return hasUnknown ? PEGAGoapTruthValue.Unknown : PEGAGoapTruthValue.True;
        }
    }

    public sealed class PEGAGoapOrCondition : IPEGAGoapCondition
    {
        private readonly IPEGAGoapCondition[] _conditions;

        public PEGAGoapOrCondition(params IPEGAGoapCondition[] conditions)
        {
            _conditions = PEGAGoapConditionEvaluation.CopyConditions(conditions);
        }

        public PEGAGoapTruthValue Evaluate(IPEGAGoapStateView state)
        {
            PEGAGoapConditionEvaluation.ValidateState(state);
            bool hasUnknown = false;

            for (int index = 0; index < _conditions.Length; index++)
            {
                PEGAGoapTruthValue value = PEGAGoapConditionEvaluation.Evaluate(_conditions[index], state);
                if (value == PEGAGoapTruthValue.True)
                {
                    return PEGAGoapTruthValue.True;
                }

                hasUnknown |= value == PEGAGoapTruthValue.Unknown;
            }

            return hasUnknown ? PEGAGoapTruthValue.Unknown : PEGAGoapTruthValue.False;
        }
    }

    internal static class PEGAGoapConditionEvaluation
    {
        public static void ValidateState(IPEGAGoapStateView state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.Source != PEGAGoapStateSource.WorldState &&
                state.Source != PEGAGoapStateSource.KnowledgeState &&
                state.Source != PEGAGoapStateSource.HypotheticalState)
            {
                throw new ArgumentException("Condition evaluation requires an explicit supported state source.", nameof(state));
            }
        }

        public static PEGAGoapTruthValue Evaluate(
            IPEGAGoapCondition condition,
            IPEGAGoapStateView state)
        {
            PEGAGoapTruthValue value = condition.Evaluate(state);
            if (value != PEGAGoapTruthValue.Unknown &&
                value != PEGAGoapTruthValue.False &&
                value != PEGAGoapTruthValue.True)
            {
                throw new InvalidOperationException("Condition returned an unsupported truth value.");
            }

            return value;
        }

        public static IPEGAGoapCondition[] CopyConditions(IPEGAGoapCondition[] conditions)
        {
            if (conditions == null)
            {
                throw new ArgumentNullException(nameof(conditions));
            }

            if (conditions.Length == 0)
            {
                throw new ArgumentException("A composed condition requires at least one child condition.", nameof(conditions));
            }

            IPEGAGoapCondition[] copy = new IPEGAGoapCondition[conditions.Length];
            for (int index = 0; index < conditions.Length; index++)
            {
                copy[index] = conditions[index] ?? throw new ArgumentException(
                    "A composed condition cannot contain a null child.",
                    nameof(conditions));
            }

            return copy;
        }
    }
}

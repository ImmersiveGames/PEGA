using System;

namespace ImmersiveGames.Goap
{
    public enum GoapTruthValue
    {
        Unknown = 0,
        False = 1,
        True = 2
    }

    public enum GoapStateSource
    {
        Unspecified = 0,
        WorldState = 1,
        KnowledgeState = 2,
        HypotheticalState = 3
    }

    /// <summary>Vista de estado somente para leitura, identificada pela autoridade que a fornece.</summary>
    public interface IGoapStateView
    {
        GoapStateSource Source { get; }
    }

    /// <summary>
    /// Knowledge contributed by one explicitly composed gameplay capability. A condition may inspect the
    /// capabilities exposed by its Knowledge State without requiring the planner to recognize their types.
    /// </summary>
    public interface IGoapKnowledgeCapability
    {
    }

    /// <summary>A read-only Knowledge State view and its composed, independently owned knowledge capabilities.</summary>
    public interface IGoapKnowledgeStateView : IGoapStateView
    {
        System.Collections.Generic.IReadOnlyList<IGoapKnowledgeCapability> Capabilities { get; }
    }

    /// <summary>Read-only state views that expose Effects as facts for condition evaluation.</summary>
    public interface IGoapEffectStateView : IGoapStateView
    {
        System.Collections.Generic.IReadOnlyList<IGoapEffect> Effects { get; }
    }

    /// <summary>
    /// Condição de GOAP avaliada contra uma fonte explícita. Implementações devem observar a vista sem
    /// alterá-la; os operadores compostos encaminham a mesma vista a cada condição filha.
    /// </summary>
    public interface IGoapCondition
    {
        GoapTruthValue Evaluate(IGoapStateView state);
    }

    public sealed class GoapNotCondition : IGoapCondition
    {
        private readonly IGoapCondition _condition;

        public GoapNotCondition(IGoapCondition condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public GoapTruthValue Evaluate(IGoapStateView state)
        {
            GoapConditionEvaluation.ValidateState(state);
            switch (GoapConditionEvaluation.Evaluate(_condition, state))
            {
                case GoapTruthValue.True:
                    return GoapTruthValue.False;
                case GoapTruthValue.False:
                    return GoapTruthValue.True;
                case GoapTruthValue.Unknown:
                    return GoapTruthValue.Unknown;
                default:
                    throw new InvalidOperationException("Condition returned an unsupported truth value.");
            }
        }
    }

    public sealed class GoapAndCondition : IGoapCondition
    {
        private readonly IGoapCondition[] _conditions;

        public GoapAndCondition(params IGoapCondition[] conditions)
        {
            _conditions = GoapConditionEvaluation.CopyConditions(conditions);
        }

        public GoapTruthValue Evaluate(IGoapStateView state)
        {
            GoapConditionEvaluation.ValidateState(state);
            bool hasUnknown = false;

            for (int index = 0; index < _conditions.Length; index++)
            {
                GoapTruthValue value = GoapConditionEvaluation.Evaluate(_conditions[index], state);
                if (value == GoapTruthValue.False)
                {
                    return GoapTruthValue.False;
                }

                hasUnknown |= value == GoapTruthValue.Unknown;
            }

            return hasUnknown ? GoapTruthValue.Unknown : GoapTruthValue.True;
        }
    }

    public sealed class GoapOrCondition : IGoapCondition
    {
        private readonly IGoapCondition[] _conditions;

        public GoapOrCondition(params IGoapCondition[] conditions)
        {
            _conditions = GoapConditionEvaluation.CopyConditions(conditions);
        }

        public GoapTruthValue Evaluate(IGoapStateView state)
        {
            GoapConditionEvaluation.ValidateState(state);
            bool hasUnknown = false;

            for (int index = 0; index < _conditions.Length; index++)
            {
                GoapTruthValue value = GoapConditionEvaluation.Evaluate(_conditions[index], state);
                if (value == GoapTruthValue.True)
                {
                    return GoapTruthValue.True;
                }

                hasUnknown |= value == GoapTruthValue.Unknown;
            }

            return hasUnknown ? GoapTruthValue.Unknown : GoapTruthValue.False;
        }
    }

    internal static class GoapConditionEvaluation
    {
        public static void ValidateState(IGoapStateView state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.Source != GoapStateSource.WorldState &&
                state.Source != GoapStateSource.KnowledgeState &&
                state.Source != GoapStateSource.HypotheticalState)
            {
                throw new ArgumentException("Condition evaluation requires an explicit supported state source.", nameof(state));
            }
        }

        public static GoapTruthValue Evaluate(
            IGoapCondition condition,
            IGoapStateView state)
        {
            GoapTruthValue value = condition.Evaluate(state);
            if (value != GoapTruthValue.Unknown &&
                value != GoapTruthValue.False &&
                value != GoapTruthValue.True)
            {
                throw new InvalidOperationException("Condition returned an unsupported truth value.");
            }

            return value;
        }

        public static IGoapCondition[] CopyConditions(IGoapCondition[] conditions)
        {
            if (conditions == null)
            {
                throw new ArgumentNullException(nameof(conditions));
            }

            if (conditions.Length == 0)
            {
                throw new ArgumentException("A composed condition requires at least one child condition.", nameof(conditions));
            }

            IGoapCondition[] copy = new IGoapCondition[conditions.Length];
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

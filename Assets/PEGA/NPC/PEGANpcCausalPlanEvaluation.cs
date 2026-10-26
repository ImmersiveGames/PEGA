using System;
using System.Collections.Generic;
using PEGA.GOAP;

namespace PEGA.NPC
{
    /// <summary>One scored causal plan alternative with the existing PEGA Action bindings retained.</summary>
    public sealed class PEGANpcCausalPlanAlternative
    {
        private readonly IReadOnlyList<PEGANpcPlanningActionCandidate> _actionSteps;

        internal PEGANpcCausalPlanAlternative(
            string desireId,
            string desireName,
            PEGANpcPlanningTarget target,
            PEGAGoapPlan plan,
            IReadOnlyList<PEGANpcPlanningActionCandidate> actionSteps,
            float estimatedSeconds,
            float effectiveDesireWeight,
            float costPreference)
        {
            DesireId = desireId ?? string.Empty;
            DesireName = desireName ?? string.Empty;
            Target = target;
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _actionSteps = Copy(actionSteps);
            EstimatedSeconds = estimatedSeconds;
            EffectiveDesireWeight = effectiveDesireWeight;
            CostPreference = costPreference;
            Score = effectiveDesireWeight * costPreference;
        }

        public string DesireId { get; }

        public string DesireName { get; }

        public PEGANpcPlanningTarget Target { get; }

        public PEGAGoapPlan Plan { get; }

        /// <summary>PEGA providers corresponding positionally to <see cref="Plan"/>. No Action is executed here.</summary>
        public IReadOnlyList<PEGANpcPlanningActionCandidate> ActionSteps => _actionSteps;

        public float EstimatedSeconds { get; }

        public float EffectiveDesireWeight { get; }

        public float CostPreference { get; }

        public float Score { get; }

        private static IReadOnlyList<PEGANpcPlanningActionCandidate> Copy(
            IReadOnlyList<PEGANpcPlanningActionCandidate> actionSteps)
        {
            if (actionSteps == null)
            {
                throw new ArgumentNullException(nameof(actionSteps));
            }

            PEGANpcPlanningActionCandidate[] copy = new PEGANpcPlanningActionCandidate[actionSteps.Count];
            for (int index = 0; index < actionSteps.Count; index++)
            {
                copy[index] = actionSteps[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>Alternatives and bounded-search status for one configured Desire Goal condition.</summary>
    public sealed class PEGANpcCausalGoalSearchEvaluation
    {
        private readonly IReadOnlyList<PEGANpcCausalPlanAlternative> _alternatives;

        internal PEGANpcCausalGoalSearchEvaluation(
            string desireId,
            PEGAGoapSearchAlternativesResult search,
            IReadOnlyList<PEGANpcCausalPlanAlternative> alternatives,
            string diagnostic)
        {
            DesireId = desireId ?? string.Empty;
            Search = search ?? throw new ArgumentNullException(nameof(search));
            _alternatives = Copy(alternatives);
            Diagnostic = diagnostic ?? string.Empty;
        }

        public string DesireId { get; }

        public PEGAGoapSearchAlternativesResult Search { get; }

        public IReadOnlyList<PEGANpcCausalPlanAlternative> Alternatives => _alternatives;

        public string Diagnostic { get; }

        private static IReadOnlyList<PEGANpcCausalPlanAlternative> Copy(
            IReadOnlyList<PEGANpcCausalPlanAlternative> alternatives)
        {
            if (alternatives == null)
            {
                throw new ArgumentNullException(nameof(alternatives));
            }

            PEGANpcCausalPlanAlternative[] copy = new PEGANpcCausalPlanAlternative[alternatives.Count];
            for (int index = 0; index < alternatives.Count; index++)
            {
                copy[index] = alternatives[index] ?? throw new ArgumentException(
                    "Causal plan alternatives cannot contain null entries.",
                    nameof(alternatives));
            }

            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    /// Deterministically ordered causal alternatives available to the PEGA policy. It intentionally has no
    /// selected-plan property; PEGA policy compares these alternatives with applicable Desire configuration.
    /// </summary>
    public sealed class PEGANpcCausalPlanEvaluation
    {
        private readonly IReadOnlyList<PEGANpcCausalGoalSearchEvaluation> _goalSearches;
        private readonly IReadOnlyList<PEGANpcCausalPlanAlternative> _alternatives;

        internal PEGANpcCausalPlanEvaluation(
            IReadOnlyList<PEGANpcCausalGoalSearchEvaluation> goalSearches,
            IReadOnlyList<PEGANpcCausalPlanAlternative> alternatives)
        {
            _goalSearches = Copy(goalSearches);
            _alternatives = Copy(alternatives);
        }

        public IReadOnlyList<PEGANpcCausalGoalSearchEvaluation> GoalSearches => _goalSearches;

        public IReadOnlyList<PEGANpcCausalPlanAlternative> Alternatives => _alternatives;

        public bool HasAlternatives => _alternatives.Count > 0;

        public bool HasIncompleteSearch
        {
            get
            {
                for (int index = 0; index < _goalSearches.Count; index++)
                {
                    if (_goalSearches[index].Search.Status == PEGAGoapSearchStatus.BudgetExhausted)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            T[] copy = new T[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                copy[index] = values[index] ?? throw new ArgumentException(
                    "Causal planning evaluations cannot contain null entries.",
                    nameof(values));
            }

            return Array.AsReadOnly(copy);
        }
    }

    internal static class PEGANpcCausalPlanEvaluator
    {
        public static PEGANpcCausalPlanEvaluation Evaluate(
            PEGANpcPlanningInput input,
            PEGANpcDesireContextDefinition activeContext,
            PEGANpcDesireStateSnapshot satisfaction,
            int candidateEvaluationBudget)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (activeContext == null)
            {
                throw new ArgumentNullException(nameof(activeContext));
            }

            if (satisfaction == null)
            {
                throw new ArgumentNullException(nameof(satisfaction));
            }

            if (input.CausalKnowledge == null)
            {
                return new PEGANpcCausalPlanEvaluation(
                    Array.Empty<PEGANpcCausalGoalSearchEvaluation>(),
                    Array.Empty<PEGANpcCausalPlanAlternative>());
            }

            List<PEGAGoapActionCandidate> causalCandidates = new List<PEGAGoapActionCandidate>();
            Dictionary<PEGAGoapActionCandidate, PEGANpcPlanningActionCandidate> runtimeCandidates =
                new Dictionary<PEGAGoapActionCandidate, PEGANpcPlanningActionCandidate>();
            Dictionary<PEGANpcPlanningTargetId, PEGANpcGoapKnownTarget> knownTargets =
                new Dictionary<PEGANpcPlanningTargetId, PEGANpcGoapKnownTarget>();
            for (int targetIndex = 0; targetIndex < input.CausalKnowledge.KnownTargets.Count; targetIndex++)
            {
                PEGANpcGoapKnownTarget target = input.CausalKnowledge.KnownTargets[targetIndex];
                knownTargets.Add(target.Target.TargetId, target);
            }

            string candidateDiagnostic = string.Empty;
            for (int candidateIndex = 0; candidateIndex < input.ActionCandidates.Count; candidateIndex++)
            {
                PEGANpcPlanningActionCandidate candidate = input.ActionCandidates[candidateIndex];
                if (!knownTargets.TryGetValue(candidate.TargetId, out PEGANpcGoapKnownTarget target) ||
                    candidate.RequiredTargetKind != target.Target.Kind ||
                    !PEGANpcPlanSelector.IsUsableCandidate(candidate) ||
                    candidate.Action == null ||
                    !candidate.Action.CanOfferCandidate(candidate, input.PlanContinuation))
                {
                    continue;
                }

                if (!candidate.Action.TryCreateCausalActionDefinition(
                        candidate,
                        target,
                        out PEGAGoapActionDefinition actionDefinition,
                        out string definitionDiagnostic))
                {
                    if (!string.IsNullOrWhiteSpace(definitionDiagnostic))
                    {
                        candidateDiagnostic = definitionDiagnostic;
                    }

                    continue;
                }

                PEGAGoapActionCandidate causalCandidate = new PEGAGoapActionCandidate(
                    actionDefinition,
                    target);
                causalCandidates.Add(causalCandidate);
                runtimeCandidates.Add(causalCandidate, candidate);
            }

            SortCausalCandidates(causalCandidates, runtimeCandidates);
            List<PEGANpcCausalGoalSearchEvaluation> goalSearches =
                new List<PEGANpcCausalGoalSearchEvaluation>();
            List<PEGANpcCausalPlanAlternative> alternatives =
                new List<PEGANpcCausalPlanAlternative>();
            PEGAGoapCausalPlanner planner = new PEGAGoapCausalPlanner();

            for (int desireIndex = 0; desireIndex < activeContext.Configurations.Length; desireIndex++)
            {
                PEGANpcDesireConfiguration configuration = activeContext.Configurations[desireIndex];
                PEGANpcDesireDefinition desire = configuration.Definition;
                satisfaction.TryGet(desire.DesireId, out PEGANpcDesireSatisfactionSnapshot currentSatisfaction);
                if (!configuration.IsApplicable(currentSatisfaction) ||
                    !IsContinuationOwner(input, desire))
                {
                    continue;
                }

                PEGAGoapGoal activeGoal = input.PlanContinuation.HasValue
                    ? new PEGAGoapGoal(input.PlanContinuation.Value.ProgressCondition)
                    : desire.CreateGoal();
                PEGAGoapSearchAlternativesResult search = planner.SearchAlternatives(
                    activeGoal,
                    input.CausalKnowledge.InitialState,
                    causalCandidates,
                    candidateEvaluationBudget);
                List<PEGANpcCausalPlanAlternative> desireAlternatives = CreatePlanAlternatives(
                    desire,
                    search,
                    runtimeCandidates,
                    configuration.EvaluateEffectiveWeight(currentSatisfaction),
                    activeContext,
                    out string scoringDiagnostic);
                alternatives.AddRange(desireAlternatives);
                string diagnostic = !string.IsNullOrWhiteSpace(candidateDiagnostic)
                    ? candidateDiagnostic
                    : scoringDiagnostic;
                goalSearches.Add(new PEGANpcCausalGoalSearchEvaluation(
                    desire.DesireId,
                    search,
                    desireAlternatives,
                    diagnostic));
            }

            goalSearches.Sort(CompareGoalSearches);
            alternatives.Sort(CompareAlternatives);
            return new PEGANpcCausalPlanEvaluation(goalSearches, alternatives);
        }

        private static List<PEGANpcCausalPlanAlternative> CreatePlanAlternatives(
            PEGANpcDesireDefinition desire,
            PEGAGoapSearchAlternativesResult search,
            IReadOnlyDictionary<PEGAGoapActionCandidate, PEGANpcPlanningActionCandidate> runtimeCandidates,
            float effectiveWeight,
            PEGANpcDesireContextDefinition activeContext,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            List<PEGANpcCausalPlanAlternative> alternatives =
                new List<PEGANpcCausalPlanAlternative>(search.Alternatives.Count);
            for (int planIndex = 0; planIndex < search.Alternatives.Count; planIndex++)
            {
                PEGAGoapPlan plan = search.Alternatives[planIndex];
                if (plan.Steps.Count == 0)
                {
                    continue;
                }

                List<PEGANpcPlanningActionCandidate> actionSteps =
                    new List<PEGANpcPlanningActionCandidate>(plan.Steps.Count);
                float estimatedSeconds = 0f;
                bool canScore = true;
                for (int stepIndex = 0; stepIndex < plan.Steps.Count; stepIndex++)
                {
                    PEGAGoapActionCandidate causalCandidate = plan.Steps[stepIndex];
                    if (!runtimeCandidates.TryGetValue(causalCandidate, out PEGANpcPlanningActionCandidate runtimeCandidate) ||
                        !TryGetEstimatedSeconds(runtimeCandidate, out float stepSeconds))
                    {
                        canScore = false;
                        diagnostic = "A causal plan step has no valid PEGA cost/provider binding.";
                        break;
                    }

                    actionSteps.Add(runtimeCandidate);
                    estimatedSeconds += stepSeconds;
                    if (!IsNonNegativeFinite(estimatedSeconds))
                    {
                        canScore = false;
                        diagnostic = "The total causal plan cost is not finite.";
                        break;
                    }
                }

                if (!canScore ||
                    !(plan.Steps[0].Target is PEGANpcGoapKnownTarget firstTarget))
                {
                    continue;
                }

                float costPreference = activeContext.EvaluateCostPreference(estimatedSeconds);
                float score = effectiveWeight * costPreference;
                if (!IsPositiveFinite(effectiveWeight) || !IsPositiveFinite(costPreference) ||
                    !IsPositiveFinite(score))
                {
                    diagnostic = "The causal plan has no positive finite configured Desire/cost score.";
                    continue;
                }

                alternatives.Add(new PEGANpcCausalPlanAlternative(
                    desire.DesireId,
                    desire.DisplayName,
                    firstTarget.Target,
                    plan,
                    actionSteps,
                    estimatedSeconds,
                    effectiveWeight,
                    costPreference));
            }

            return alternatives;
        }

        private static bool IsContinuationOwner(
            PEGANpcPlanningInput input,
            PEGANpcDesireDefinition desire) =>
            !input.PlanContinuation.HasValue ||
            string.Equals(
                input.PlanContinuation.Value.DesireId,
                desire.DesireId,
                StringComparison.Ordinal);
        private static void SortCausalCandidates(
            List<PEGAGoapActionCandidate> candidates,
            IReadOnlyDictionary<PEGAGoapActionCandidate, PEGANpcPlanningActionCandidate> runtimeCandidates)
        {
            candidates.Sort((left, right) =>
            {
                PEGANpcPlanningActionCandidate leftRuntime = runtimeCandidates[left];
                PEGANpcPlanningActionCandidate rightRuntime = runtimeCandidates[right];
                int actionOrder = leftRuntime.ActionId.CompareTo(rightRuntime.ActionId);
                return actionOrder != 0
                    ? actionOrder
                    : leftRuntime.TargetId.CompareTo(rightRuntime.TargetId);
            });
        }

        private static int CompareGoalSearches(
            PEGANpcCausalGoalSearchEvaluation left,
            PEGANpcCausalGoalSearchEvaluation right) =>
            string.CompareOrdinal(left.DesireId, right.DesireId);

        private static int CompareAlternatives(
            PEGANpcCausalPlanAlternative left,
            PEGANpcCausalPlanAlternative right)
        {
            int scoreOrder = right.Score.CompareTo(left.Score);
            if (scoreOrder != 0)
            {
                return scoreOrder;
            }

            int desireOrder = string.CompareOrdinal(left.DesireId, right.DesireId);
            if (desireOrder != 0)
            {
                return desireOrder;
            }

            int commonStepCount = Math.Min(left.ActionSteps.Count, right.ActionSteps.Count);
            for (int index = 0; index < commonStepCount; index++)
            {
                int actionOrder = left.ActionSteps[index].ActionId.CompareTo(right.ActionSteps[index].ActionId);
                if (actionOrder != 0)
                {
                    return actionOrder;
                }

                int targetOrder = left.ActionSteps[index].TargetId.CompareTo(right.ActionSteps[index].TargetId);
                if (targetOrder != 0)
                {
                    return targetOrder;
                }
            }

            return left.ActionSteps.Count.CompareTo(right.ActionSteps.Count);
        }

        private static bool TryGetEstimatedSeconds(
            PEGANpcPlanningActionCandidate candidate,
            out float estimatedSeconds)
        {
            estimatedSeconds = 0f;
            if (!PEGANpcPlanSelector.IsUsableCandidate(candidate))
            {
                return false;
            }

            PEGANpcNavigationRouteEstimate route = candidate.RouteEstimate;
            float travelSeconds = candidate.Contract.RequiresNavigation
                ? route.RouteLengthMeters / route.EffectiveSpeedMetersPerSecond
                : 0f;
            estimatedSeconds = travelSeconds + candidate.IntrinsicDurationSeconds;
            return IsNonNegativeFinite(estimatedSeconds);
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

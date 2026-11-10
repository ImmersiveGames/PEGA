using System;
using System.Collections.Generic;
using PEGA.GOAP;
using PEGA.NPC.Actions.Runtime;
using PEGA.NPC.Desires;
namespace PEGA.NPC.Autonomy.Planning
{
    /// <summary>One scored causal plan alternative with the existing PEGA Action bindings retained.</summary>
    public sealed class PegaNpcCausalPlanAlternative
    {
        private readonly IReadOnlyList<PegaNpcPlanningActionCandidate> _actionSteps;

        internal PegaNpcCausalPlanAlternative(
            string desireId,
            string desireName,
            PegaNpcPlanningTarget target,
            GoapPlan plan,
            IReadOnlyList<PegaNpcPlanningActionCandidate> actionSteps,
            float estimatedSeconds,
            float effectiveDesireWeight,
            float costPreference,
            float score)
        {
            DesireId = desireId ?? string.Empty;
            DesireName = desireName ?? string.Empty;
            Target = target;
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _actionSteps = Copy(actionSteps);
            EstimatedSeconds = estimatedSeconds;
            EffectiveDesireWeight = effectiveDesireWeight;
            CostPreference = costPreference;
            Score = score;
        }

        public string DesireId { get; }

        public string DesireName { get; }

        public PegaNpcPlanningTarget Target { get; }

        public GoapPlan Plan { get; }

        /// <summary>PEGA providers corresponding positionally to <see cref="Plan"/>. No Action is executed here.</summary>
        public IReadOnlyList<PegaNpcPlanningActionCandidate> ActionSteps => _actionSteps;

        public float EstimatedSeconds { get; }

        public float EffectiveDesireWeight { get; }

        public float CostPreference { get; }

        public float Score { get; }

        private static IReadOnlyList<PegaNpcPlanningActionCandidate> Copy(
            IReadOnlyList<PegaNpcPlanningActionCandidate> actionSteps)
        {
            if (actionSteps == null)
            {
                throw new ArgumentNullException(nameof(actionSteps));
            }

            PegaNpcPlanningActionCandidate[] copy = new PegaNpcPlanningActionCandidate[actionSteps.Count];
            for (int index = 0; index < actionSteps.Count; index++)
            {
                copy[index] = actionSteps[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>Alternatives and bounded-search status for one configured Desire Goal condition.</summary>
    public sealed class PegaNpcCausalGoalSearchEvaluation
    {
        private readonly IReadOnlyList<PegaNpcCausalPlanAlternative> _alternatives;

        internal PegaNpcCausalGoalSearchEvaluation(
            string desireId,
            GoapSearchAlternativesResult search,
            IReadOnlyList<PegaNpcCausalPlanAlternative> alternatives,
            string diagnostic,
            bool hasIncompleteStepEvaluation)
        {
            DesireId = desireId ?? string.Empty;
            Search = search ?? throw new ArgumentNullException(nameof(search));
            _alternatives = Copy(alternatives);
            Diagnostic = diagnostic ?? string.Empty;
            HasIncompleteStepEvaluation = hasIncompleteStepEvaluation;
        }

        public string DesireId { get; }

        public GoapSearchAlternativesResult Search { get; }

        public IReadOnlyList<PegaNpcCausalPlanAlternative> Alternatives => _alternatives;

        public string Diagnostic { get; }

        public bool HasIncompleteStepEvaluation { get; }

        private static IReadOnlyList<PegaNpcCausalPlanAlternative> Copy(
            IReadOnlyList<PegaNpcCausalPlanAlternative> alternatives)
        {
            if (alternatives == null)
            {
                throw new ArgumentNullException(nameof(alternatives));
            }

            PegaNpcCausalPlanAlternative[] copy = new PegaNpcCausalPlanAlternative[alternatives.Count];
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
    public sealed class PegaNpcCausalPlanEvaluation
    {
        private readonly IReadOnlyList<PegaNpcCausalGoalSearchEvaluation> _goalSearches;
        private readonly IReadOnlyList<PegaNpcCausalPlanAlternative> _alternatives;

        internal PegaNpcCausalPlanEvaluation(
            IReadOnlyList<PegaNpcCausalGoalSearchEvaluation> goalSearches,
            IReadOnlyList<PegaNpcCausalPlanAlternative> alternatives)
        {
            _goalSearches = Copy(goalSearches);
            _alternatives = Copy(alternatives);
        }

        public IReadOnlyList<PegaNpcCausalGoalSearchEvaluation> GoalSearches => _goalSearches;

        public IReadOnlyList<PegaNpcCausalPlanAlternative> Alternatives => _alternatives;

        public bool HasAlternatives => _alternatives.Count > 0;

        public bool HasIncompleteSearch
        {
            get
            {
                for (int index = 0; index < _goalSearches.Count; index++)
                {
                    if (_goalSearches[index].Search.Status == GoapSearchStatus.BudgetExhausted ||
                        _goalSearches[index].HasIncompleteStepEvaluation)
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

    internal static class PegaNpcCausalPlanEvaluator
    {
        public static PegaNpcCausalPlanEvaluation Evaluate(
            PegaNpcPlanningInput input,
            PegaNpcDesireContextDefinition activeContext,
            PegaNpcDesireStateSnapshot satisfaction,
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
                return new PegaNpcCausalPlanEvaluation(
                    Array.Empty<PegaNpcCausalGoalSearchEvaluation>(),
                    Array.Empty<PegaNpcCausalPlanAlternative>());
            }

            List<GoapActionCandidate> causalCandidates = new List<GoapActionCandidate>();
            Dictionary<GoapActionCandidate, PegaNpcPlanningActionCandidate> runtimeCandidates =
                new Dictionary<GoapActionCandidate, PegaNpcPlanningActionCandidate>();
            Dictionary<PegaNpcPlanningTargetId, PegaNpcGoapKnownTarget> knownTargets =
                new Dictionary<PegaNpcPlanningTargetId, PegaNpcGoapKnownTarget>();
            for (int targetIndex = 0; targetIndex < input.CausalKnowledge.KnownTargets.Count; targetIndex++)
            {
                PegaNpcGoapKnownTarget target = input.CausalKnowledge.KnownTargets[targetIndex];
                knownTargets.Add(target.Target.TargetId, target);
            }

            string candidateDiagnostic = string.Empty;
            for (int candidateIndex = 0; candidateIndex < input.ActionCandidates.Count; candidateIndex++)
            {
                PegaNpcPlanningActionCandidate candidate = input.ActionCandidates[candidateIndex];
                if (!knownTargets.TryGetValue(candidate.TargetId, out PegaNpcGoapKnownTarget target) ||
                    candidate.RequiredTargetKind != target.Target.Kind ||
                    !PegaNpcPlanSelector.IsUsableCandidate(candidate) ||
                    candidate.Action == null ||
                    !candidate.Action.CanOfferCandidate(candidate, input.PlanContinuation))
                {
                    continue;
                }

                if (!candidate.Action.TryCreateCausalActionDefinition(
                        candidate,
                        target,
                        out GoapActionDefinition actionDefinition,
                        out string definitionDiagnostic))
                {
                    if (!string.IsNullOrWhiteSpace(definitionDiagnostic))
                    {
                        candidateDiagnostic = definitionDiagnostic;
                    }

                    continue;
                }

                GoapActionCandidate causalCandidate = new GoapActionCandidate(
                    actionDefinition,
                    target);
                causalCandidates.Add(causalCandidate);
                runtimeCandidates.Add(causalCandidate, candidate);
            }

            SortCausalCandidates(causalCandidates, runtimeCandidates);
            List<PegaNpcCausalGoalSearchEvaluation> goalSearches =
                new List<PegaNpcCausalGoalSearchEvaluation>();
            List<PegaNpcCausalPlanAlternative> alternatives =
                new List<PegaNpcCausalPlanAlternative>();
            GoapCausalPlanner planner = new GoapCausalPlanner();

            for (int desireIndex = 0; desireIndex < activeContext.Configurations.Length; desireIndex++)
            {
                PegaNpcDesireConfiguration configuration = activeContext.Configurations[desireIndex];
                PegaNpcDesireDefinition desire = configuration.Definition;
                satisfaction.TryGet(desire.DesireId, out PegaNpcDesireSatisfactionSnapshot currentSatisfaction);
                if (!configuration.IsApplicable(currentSatisfaction, input.CausalKnowledge.KnowledgeState) ||
                    !IsContinuationOwner(input, desire))
                {
                    continue;
                }

                GoapGoal activeGoal = input.PlanContinuation.HasValue
                    ? new GoapGoal(input.PlanContinuation.Value.ProgressCondition)
                    : desire.CreateGoal();
                CausalSearchStepEvaluator stepEvaluator = new CausalSearchStepEvaluator(
                    runtimeCandidates,
                    input.CausalKnowledge);
                GoapSearchAlternativesResult search = planner.SearchAlternatives(
                    activeGoal,
                    input.CausalKnowledge.InitialState,
                    causalCandidates,
                    candidateEvaluationBudget,
                    stepEvaluator);
                List<PegaNpcCausalPlanAlternative> desireAlternatives = CreatePlanAlternatives(
                    desire,
                    search,
                    runtimeCandidates,
                    input.CausalKnowledge,
                    configuration.EvaluateEffectiveWeight(currentSatisfaction),
                    activeContext,
                    candidateEvaluationBudget,
                    out string scoringDiagnostic,
                    out bool hasIncompleteStepEvaluation);
                alternatives.AddRange(desireAlternatives);
                string diagnostic = !string.IsNullOrWhiteSpace(scoringDiagnostic)
                    ? scoringDiagnostic
                    : candidateDiagnostic;
                goalSearches.Add(new PegaNpcCausalGoalSearchEvaluation(
                    desire.DesireId,
                    search,
                    desireAlternatives,
                    diagnostic,
                    hasIncompleteStepEvaluation));
            }

            goalSearches.Sort(CompareGoalSearches);
            alternatives.Sort(CompareAlternatives);
            return new PegaNpcCausalPlanEvaluation(goalSearches, alternatives);
        }

        private static List<PegaNpcCausalPlanAlternative> CreatePlanAlternatives(
            PegaNpcDesireDefinition desire,
            GoapSearchAlternativesResult search,
            IReadOnlyDictionary<GoapActionCandidate, PegaNpcPlanningActionCandidate> runtimeCandidates,
            PegaNpcCausalPlanningKnowledge causalKnowledge,
            float effectiveWeight,
            PegaNpcDesireContextDefinition activeContext,
            int stepEvaluationBudget,
            out string diagnostic,
            out bool hasIncompleteStepEvaluation)
        {
            diagnostic = search.StepEvaluationDiagnostic;
            hasIncompleteStepEvaluation = search.HasIncompleteStepEvaluation;
            int stepEvaluations = 0;
            List<PegaNpcCausalPlanAlternative> alternatives =
                new List<PegaNpcCausalPlanAlternative>(search.Alternatives.Count);
            for (int planIndex = 0; planIndex < search.Alternatives.Count; planIndex++)
            {
                GoapPlan plan = search.Alternatives[planIndex];
                if (plan.Steps.Count == 0)
                {
                    continue;
                }

                List<PegaNpcPlanningActionCandidate> actionSteps =
                    new List<PegaNpcPlanningActionCandidate>(plan.Steps.Count);
                GoapHypotheticalState stepState = causalKnowledge.InitialState;
                float estimatedSeconds = 0f;
                bool canScore = true;
                for (int stepIndex = 0; stepIndex < plan.Steps.Count; stepIndex++)
                {
                    if (stepEvaluations >= stepEvaluationBudget)
                    {
                        canScore = false;
                        hasIncompleteStepEvaluation = true;
                        diagnostic = "The configured hypothetical Action-step evaluation budget was exhausted.";
                        break;
                    }

                    GoapActionCandidate causalCandidate = plan.Steps[stepIndex];
                    if (!runtimeCandidates.TryGetValue(causalCandidate, out PegaNpcPlanningActionCandidate runtimeCandidate))
                    {
                        canScore = false;
                        diagnostic = "A causal plan step has no PEGA Action-provider binding.";
                        break;
                    }

                    stepEvaluations++;
                    PegaNpcCausalStepEvaluationStatus stepStatus = EvaluateCausalStep(
                        runtimeCandidate,
                        stepState,
                        causalKnowledge,
                        out PegaNpcNavigationRouteEstimate routeEstimate,
                        out float stepSeconds,
                        out string stepDiagnostic);
                    if (stepStatus != PegaNpcCausalStepEvaluationStatus.Available ||
                        !IsNonNegativeFinite(stepSeconds) ||
                        (runtimeCandidate.Contract.RequiresNavigation &&
                         (routeEstimate.Status != PegaNpcPlanningRouteStatus.Complete ||
                          !IsPositiveFinite(routeEstimate.EffectiveSpeedMetersPerSecond))))
                    {
                        canScore = false;
                        if (stepStatus == PegaNpcCausalStepEvaluationStatus.Unknown)
                        {
                            hasIncompleteStepEvaluation = true;
                        }

                        diagnostic = !string.IsNullOrWhiteSpace(stepDiagnostic)
                            ? stepDiagnostic
                            : stepStatus == PegaNpcCausalStepEvaluationStatus.Unavailable
                                ? "A causal Action step is unavailable in its projected state."
                                : "A causal Action step could not be evaluated against its projected state.";
                        break;
                    }

                    PegaNpcPlanningActionCandidate evaluatedCandidate = runtimeCandidate.Contract.RequiresNavigation
                        ? runtimeCandidate.WithRouteEstimate(routeEstimate)
                        : runtimeCandidate;
                    actionSteps.Add(evaluatedCandidate);
                    estimatedSeconds += stepSeconds;
                    if (!IsNonNegativeFinite(estimatedSeconds))
                    {
                        canScore = false;
                        diagnostic = "The total causal plan cost is not finite.";
                        break;
                    }

                    stepState = stepState.Project(causalCandidate.ActionDefinition);
                }

                if (!canScore ||
                    !(plan.Steps[0].Target is PegaNpcGoapKnownTarget firstTarget))
                {
                    continue;
                }

                if (!IsPositiveFinite(effectiveWeight) ||
                    !activeContext.TryEvaluatePlanScore(
                        effectiveWeight,
                        estimatedSeconds,
                        out float costPreference,
                        out float score))
                {
                    diagnostic = "The causal plan has no positive finite configured Desire/cost score.";
                    continue;
                }

                alternatives.Add(new PegaNpcCausalPlanAlternative(
                    desire.DesireId,
                    desire.DisplayName,
                    firstTarget.Target,
                    plan,
                    actionSteps,
                    estimatedSeconds,
                    effectiveWeight,
                    costPreference,
                    score));
            }

            return alternatives;
        }

        private static bool IsContinuationOwner(
            PegaNpcPlanningInput input,
            PegaNpcDesireDefinition desire) =>
            !input.PlanContinuation.HasValue ||
            string.Equals(
                input.PlanContinuation.Value.DesireId,
                desire.DesireId,
                StringComparison.Ordinal);
        private static void SortCausalCandidates(
            List<GoapActionCandidate> candidates,
            IReadOnlyDictionary<GoapActionCandidate, PegaNpcPlanningActionCandidate> runtimeCandidates)
        {
            candidates.Sort((left, right) =>
            {
                PegaNpcPlanningActionCandidate leftRuntime = runtimeCandidates[left];
                PegaNpcPlanningActionCandidate rightRuntime = runtimeCandidates[right];
                int actionOrder = leftRuntime.ActionId.CompareTo(rightRuntime.ActionId);
                return actionOrder != 0
                    ? actionOrder
                    : leftRuntime.TargetId.CompareTo(rightRuntime.TargetId);
            });
        }

        private static int CompareGoalSearches(
            PegaNpcCausalGoalSearchEvaluation left,
            PegaNpcCausalGoalSearchEvaluation right) =>
            string.CompareOrdinal(left.DesireId, right.DesireId);

        private static int CompareAlternatives(
            PegaNpcCausalPlanAlternative left,
            PegaNpcCausalPlanAlternative right)
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

        private static PegaNpcCausalStepEvaluationStatus EvaluateCausalStep(
            PegaNpcPlanningActionCandidate candidate,
            IGoapHypotheticalStateView stateBeforeStep,
            PegaNpcCausalPlanningKnowledge causalKnowledge,
            out PegaNpcNavigationRouteEstimate routeEstimate,
            out float estimatedSeconds,
            out string diagnostic)
        {
            routeEstimate = candidate.RouteEstimate;
            estimatedSeconds = 0f;
            diagnostic = string.Empty;
            if (!PegaNpcPlanSelector.IsUsableCandidate(candidate))
            {
                diagnostic = "A causal plan step has an invalid PEGA Action candidate.";
                return PegaNpcCausalStepEvaluationStatus.Unavailable;
            }

            if (candidate.Action is IPegaNpcCausalActionStepEvaluator evaluator)
            {
                return evaluator.EvaluateCausalStep(
                    candidate,
                    stateBeforeStep,
                    causalKnowledge,
                    out routeEstimate,
                    out estimatedSeconds,
                    out diagnostic);
            }

            if (stateBeforeStep.ProjectedEffects.Count > 0)
            {
                diagnostic = "The Action provider has no evaluator for a projected pre-step state.";
                return PegaNpcCausalStepEvaluationStatus.Unknown;
            }

            PegaNpcNavigationRouteEstimate route = candidate.RouteEstimate;
            float travelSeconds = candidate.Contract.RequiresNavigation
                ? route.RouteLengthMeters / route.EffectiveSpeedMetersPerSecond
                : 0f;
            estimatedSeconds = travelSeconds + candidate.IntrinsicDurationSeconds;
            return IsNonNegativeFinite(estimatedSeconds)
                ? PegaNpcCausalStepEvaluationStatus.Available
                : PegaNpcCausalStepEvaluationStatus.Unknown;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class CausalSearchStepEvaluator : IGoapActionStepEvaluator
        {
            private readonly IReadOnlyDictionary<
                GoapActionCandidate,
                PegaNpcPlanningActionCandidate> _runtimeCandidates;
            private readonly PegaNpcCausalPlanningKnowledge _causalKnowledge;

            public CausalSearchStepEvaluator(
                IReadOnlyDictionary<GoapActionCandidate, PegaNpcPlanningActionCandidate> runtimeCandidates,
                PegaNpcCausalPlanningKnowledge causalKnowledge)
            {
                _runtimeCandidates = runtimeCandidates ?? throw new ArgumentNullException(nameof(runtimeCandidates));
                _causalKnowledge = causalKnowledge ?? throw new ArgumentNullException(nameof(causalKnowledge));
            }

            public GoapActionStepAvailability EvaluateActionStep(
                GoapActionCandidate candidate,
                IGoapHypotheticalStateView stateBeforeStep,
                out string diagnostic)
            {
                diagnostic = string.Empty;
                if (candidate == null ||
                    !_runtimeCandidates.TryGetValue(candidate, out PegaNpcPlanningActionCandidate runtimeCandidate))
                {
                    diagnostic = "The causal search candidate has no PEGA Action-provider binding.";
                    return GoapActionStepAvailability.Unknown;
                }

                PegaNpcCausalStepEvaluationStatus status = EvaluateCausalStep(
                    runtimeCandidate,
                    stateBeforeStep,
                    _causalKnowledge,
                    out _,
                    out _,
                    out diagnostic);
                return status switch
                {
                    PegaNpcCausalStepEvaluationStatus.Available => GoapActionStepAvailability.Available,
                    PegaNpcCausalStepEvaluationStatus.Unavailable => GoapActionStepAvailability.Unavailable,
                    _ => GoapActionStepAvailability.Unknown
                };
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace PEGA.NPC
{
    public static class PEGANpcPlanSelector
    {
        public static PEGANpcPlanSelectionResult Select(
            PEGANpcPlanningInput input,
            PEGANpcDesireContextDefinition activeContext,
            PEGANpcDesireStateSnapshot satisfaction)
        {
            PEGANpcPlanningValidationMode mode = input != null
                ? input.ValidationMode
                : default;
            PEGANpcDesireContext context = activeContext != null
                ? activeContext.Context
                : default;

            if (input == null)
            {
                return InvalidResult(mode, context, "Planning input is required.");
            }

            if (activeContext == null)
            {
                return InvalidResult(mode, context, "An explicit Desire context configuration is required.");
            }

            if (satisfaction == null)
            {
                return InvalidResult(mode, context, "An explicit per-NPC Desire state snapshot is required.");
            }

            if (input.Context != activeContext.Context)
            {
                return InvalidResult(
                    mode,
                    context,
                    $"Planning input context {input.Context} does not match supplied profile context {activeContext.Context}.");
            }

            if (!activeContext.TryValidate(new HashSet<string>(StringComparer.Ordinal), out string contextDiagnostic))
            {
                return InvalidResult(mode, context, contextDiagnostic);
            }

            if (!input.TryValidate(out string inputDiagnostic))
            {
                return InvalidResult(mode, context, inputDiagnostic);
            }

            if (mode != PEGANpcPlanningValidationMode.LiveKnowledge || input.CausalKnowledge == null)
            {
                return InvalidResult(
                    mode,
                    context,
                    "Live plan selection requires a validated causal Knowledge State and known Targets.");
            }

            PEGANpcCausalPlanEvaluation causalEvaluation = PEGANpcCausalPlanEvaluator.Evaluate(
                input,
                activeContext,
                satisfaction,
                input.CausalSearchCandidateEvaluationBudget);
            List<PEGANpcPlanSnapshot> allPlans = new List<PEGANpcPlanSnapshot>(causalEvaluation.Alternatives.Count);
            Dictionary<string, List<PEGANpcPlanSnapshot>> plansByDesire =
                new Dictionary<string, List<PEGANpcPlanSnapshot>>(StringComparer.Ordinal);
            PEGANpcPlanSnapshot selectedPlan = default;
            bool hasSelectedPlan = false;

            for (int alternativeIndex = 0; alternativeIndex < causalEvaluation.Alternatives.Count; alternativeIndex++)
            {
                PEGANpcCausalPlanAlternative alternative = causalEvaluation.Alternatives[alternativeIndex];
                if (alternative.ActionSteps.Count != alternative.Plan.Steps.Count ||
                    alternative.ActionSteps.Count == 0)
                {
                    continue;
                }

                PEGANpcPlanningActionCandidate firstAction = alternative.ActionSteps[0];
                if (firstAction.Action == null ||
                    firstAction.TargetId != alternative.Target.TargetId ||
                    firstAction.RequiredTargetKind != alternative.Target.Kind)
                {
                    continue;
                }

                PEGANpcPlanSnapshot plan = new PEGANpcPlanSnapshot(
                    alternative.DesireId,
                    alternative.DesireName,
                    firstAction,
                    alternative.Target,
                    alternative.Plan.Steps[0].ActionDefinition.Effects,
                    alternative.EstimatedSeconds,
                    alternative.EffectiveDesireWeight,
                    alternative.CostPreference,
                    alternative.Score,
                    input.PlanContinuation.HasValue
                        ? PEGANpcPlanPurpose.PlanContinuationStep
                        : PEGANpcPlanPurpose.DesireGoal);
                allPlans.Add(plan);
                if (!plansByDesire.TryGetValue(plan.DesireId, out List<PEGANpcPlanSnapshot> desirePlans))
                {
                    desirePlans = new List<PEGANpcPlanSnapshot>();
                    plansByDesire.Add(plan.DesireId, desirePlans);
                }

                desirePlans.Add(plan);
                if (!hasSelectedPlan || ComparePlans(plan, selectedPlan) < 0)
                {
                    selectedPlan = plan;
                    hasSelectedPlan = true;
                }
            }

            List<PEGANpcDesirePlanEvaluation> evaluations =
                new List<PEGANpcDesirePlanEvaluation>(activeContext.Configurations.Length);
            for (int desireIndex = 0; desireIndex < activeContext.Configurations.Length; desireIndex++)
            {
                PEGANpcDesireConfiguration configuration = activeContext.Configurations[desireIndex];
                if (configuration == null || configuration.Definition == null)
                {
                    return InvalidResult(mode, context, $"Desire configuration or definition at index {desireIndex} is null.");
                }

                PEGANpcDesireDefinition desire = configuration.Definition;
                satisfaction.TryGet(desire.DesireId, out PEGANpcDesireSatisfactionSnapshot currentSatisfaction);
                bool applicable = configuration.IsApplicable(currentSatisfaction);
                float effectiveWeight = applicable
                    ? configuration.EvaluateEffectiveWeight(currentSatisfaction)
                    : 0f;
                List<PEGANpcPlanSnapshot> desirePlans = plansByDesire.TryGetValue(
                    desire.DesireId,
                    out List<PEGANpcPlanSnapshot> plans)
                    ? plans
                    : new List<PEGANpcPlanSnapshot>();
                desirePlans.Sort(ComparePlans);

                string evaluationDiagnostic = !applicable
                    ? "Desire is not applicable in this context or reached its configured completed-execution limit."
                    : desirePlans.Count == 0
                        ? GetNoPlanDiagnostic(causalEvaluation, input, desire.DesireId)
                        : string.Empty;
                evaluations.Add(new PEGANpcDesirePlanEvaluation(
                    desire.DesireId,
                    desire.DisplayName,
                    applicable,
                    effectiveWeight,
                    desirePlans,
                    evaluationDiagnostic));
            }

            evaluations.Sort((left, right) => string.CompareOrdinal(left.DesireId, right.DesireId));
            string diagnostic = hasSelectedPlan
                ? causalEvaluation.HasIncompleteSearch
                    ? "Selected the highest-ranked known causal alternative; at least one applicable Goal search exhausted its budget or an Action-step evaluation remained unknown."
                    : string.Empty
                : causalEvaluation.HasIncompleteSearch
                    ? "Causal search budget or a hypothetical Action-step evaluation remained inconclusive before a selectable plan could be established."
                    : "No known causal solution exists for the applicable Desires and known Action/Target candidates.";
            return new PEGANpcPlanSelectionResult(
                true,
                mode,
                context,
                evaluations,
                allPlans,
                hasSelectedPlan,
                selectedPlan,
                diagnostic,
                causalEvaluation);
        }

        internal static int ComparePlans(PEGANpcPlanSnapshot left, PEGANpcPlanSnapshot right)
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

            int actionOrder = string.CompareOrdinal(GetStableActionId(left.ActionId), GetStableActionId(right.ActionId));
            return actionOrder != 0
                ? actionOrder
                : left.TargetId.CompareTo(right.TargetId);
        }

        internal static string GetStableActionId(PEGANpcPlanningActionId actionId) => actionId.StableText;

        private static string GetNoPlanDiagnostic(
            PEGANpcCausalPlanEvaluation causalEvaluation,
            PEGANpcPlanningInput input,
            string desireId)
        {
            if (input.PlanContinuation.HasValue &&
                !string.Equals(
                    input.PlanContinuation.Value.DesireId,
                    desireId,
                    StringComparison.Ordinal))
            {
                return "Desire is outside the active pursuit continuation owner.";
            }

            bool hasSearch = false;
            for (int index = 0; index < causalEvaluation.GoalSearches.Count; index++)
            {
                PEGANpcCausalGoalSearchEvaluation search = causalEvaluation.GoalSearches[index];
                if (!string.Equals(search.DesireId, desireId, StringComparison.Ordinal))
                {
                    continue;
                }

                hasSearch = true;
                if (search.Search.Status == ImmersiveGames.Goap.GoapSearchStatus.BudgetExhausted)
                {
                    return "The applicable Goal search exhausted its configured evaluation budget.";
                }

                if (search.HasIncompleteStepEvaluation)
                {
                    return string.IsNullOrWhiteSpace(search.Diagnostic)
                        ? "A hypothetical Action-step evaluation remained unknown."
                        : search.Diagnostic;
                }
            }

            return hasSearch
                ? "No known causal solution exists for this Desire and its known Action/Target candidates."
                : "No known Action/Target candidates were available for this Desire.";
        }

        private static PEGANpcPlanSelectionResult InvalidResult(
            PEGANpcPlanningValidationMode mode,
            PEGANpcDesireContext context,
            string diagnostic) => new PEGANpcPlanSelectionResult(
            false,
            mode,
            context,
            Array.Empty<PEGANpcDesirePlanEvaluation>(),
            Array.Empty<PEGANpcPlanSnapshot>(),
            false,
            default,
            diagnostic);

        internal static bool IsUsableCandidate(PEGANpcPlanningActionCandidate candidate)
        {
            PEGANpcNavigationRouteEstimate route = candidate.RouteEstimate;
            bool deferredRoute = route.Status == PEGANpcPlanningRouteStatus.Deferred &&
                candidate.Action is IPEGANpcCausalActionStepEvaluator &&
                IsNonNegativeFinite(route.RouteLengthMeters) &&
                IsPositiveFinite(route.EffectiveSpeedMetersPerSecond);
            bool usableRoute = candidate.Contract.RequiresNavigation
                ? deferredRoute ||
                  (route.Status == PEGANpcPlanningRouteStatus.Complete &&
                   IsNonNegativeFinite(route.RouteLengthMeters) &&
                   IsPositiveFinite(route.EffectiveSpeedMetersPerSecond))
                : route.Status == PEGANpcPlanningRouteStatus.NotRequired &&
                  route.RouteLengthMeters == 0f && route.EffectiveSpeedMetersPerSecond == 0f;
            return usableRoute &&
                   IsNonNegativeFinite(candidate.IntrinsicDurationSeconds);
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

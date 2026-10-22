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

            List<PEGANpcDesirePlanEvaluation> evaluations =
                new List<PEGANpcDesirePlanEvaluation>(activeContext.Definitions.Length);
            List<PEGANpcPlanSnapshot> allPlans = new List<PEGANpcPlanSnapshot>();
            PEGANpcPlanSnapshot selectedPlan = default;
            bool hasSelectedPlan = false;

            for (int desireIndex = 0; desireIndex < activeContext.Definitions.Length; desireIndex++)
            {
                PEGANpcDesireDefinition desire = activeContext.Definitions[desireIndex];
                if (desire == null)
                {
                    return InvalidResult(mode, context, $"Desire at index {desireIndex} is null.");
                }

                satisfaction.TryGet(desire.DesireId, out PEGANpcDesireSatisfactionSnapshot currentSatisfaction);
                bool applicable = desire.IsApplicable(currentSatisfaction);
                float effectiveWeight = applicable
                    ? desire.EvaluateEffectiveWeight(currentSatisfaction)
                    : 0f;
                List<PEGANpcPlanSnapshot> desirePlans = new List<PEGANpcPlanSnapshot>();

                if (applicable)
                {
                    for (int candidateIndex = 0; candidateIndex < input.ActionCandidates.Count; candidateIndex++)
                    {
                        PEGANpcPlanningActionCandidate candidate = input.ActionCandidates[candidateIndex];
                        if (!candidate.PreconditionsSatisfied ||
                            candidate.PredictedGoal != desire.DesiredGoal ||
                            candidate.RequiredTargetKind != GetRequiredTargetKind(candidate.PredictedGoal) ||
                            !input.TryGetTarget(candidate.TargetId, out PEGANpcPlanningTarget target) ||
                            target.Kind != candidate.RequiredTargetKind ||
                            !IsUsableCandidate(candidate))
                        {
                            continue;
                        }

                        float estimatedSeconds = candidate.RouteEstimate.RouteLengthMeters /
                                                 candidate.RouteEstimate.EffectiveSpeedMetersPerSecond +
                                                 candidate.IntrinsicDurationSeconds;
                        float costPreference = activeContext.EvaluateCostPreference(estimatedSeconds);
                        float score = effectiveWeight * costPreference;
                        if (!IsPositiveFinite(effectiveWeight) || !IsPositiveFinite(costPreference) ||
                            !IsPositiveFinite(score))
                        {
                            continue;
                        }

                        desirePlans.Add(new PEGANpcPlanSnapshot(
                            desire.DesireId,
                            desire.DisplayName,
                            candidate,
                            target,
                            estimatedSeconds,
                            effectiveWeight,
                            costPreference,
                            score));
                    }
                }

                desirePlans.Sort(ComparePlans);
                for (int planIndex = 0; planIndex < desirePlans.Count; planIndex++)
                {
                    PEGANpcPlanSnapshot plan = desirePlans[planIndex];
                    allPlans.Add(plan);
                    if (!hasSelectedPlan || ComparePlans(plan, selectedPlan) < 0)
                    {
                        selectedPlan = plan;
                        hasSelectedPlan = true;
                    }
                }

                string evaluationDiagnostic = !applicable
                    ? "Desire is not applicable in this context or reached its satisfaction/execution limit."
                    : desirePlans.Count == 0
                        ? "Desire is applicable, but no compatible Action has a known Target and complete valid route."
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
            allPlans.Sort(ComparePlans);
            return new PEGANpcPlanSelectionResult(
                true,
                mode,
                context,
                evaluations,
                allPlans,
                hasSelectedPlan,
                selectedPlan,
                hasSelectedPlan ? string.Empty : "No viable one-step plan exists for the applicable Desires.");
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

        internal static string GetStableActionId(PEGANpcPlanningActionId actionId) => actionId switch
        {
            PEGANpcPlanningActionId.ApproachKnownRick => "approach-known-rick",
            PEGANpcPlanningActionId.TravelToEscapeTarget => "travel-to-escape-target",
            _ => "invalid-action"
        };

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

        private static PEGANpcPlanningTargetKind GetRequiredTargetKind(
            PEGANpcDesireGoalKind goal) => goal switch
        {
            PEGANpcDesireGoalKind.ReachKnownCharacterPosition =>
                PEGANpcPlanningTargetKind.CharacterPosition,
            PEGANpcDesireGoalKind.ReachKnownEscapeDestination =>
                PEGANpcPlanningTargetKind.EscapeDestination,
            _ => (PEGANpcPlanningTargetKind)(-1)
        };

        private static bool IsUsableCandidate(PEGANpcPlanningActionCandidate candidate)
        {
            PEGANpcNavigationRouteEstimate route = candidate.RouteEstimate;
            return route.Status == PEGANpcPlanningRouteStatus.Complete &&
                   IsNonNegativeFinite(route.RouteLengthMeters) &&
                   IsPositiveFinite(route.EffectiveSpeedMetersPerSecond) &&
                   IsNonNegativeFinite(candidate.IntrinsicDurationSeconds);
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

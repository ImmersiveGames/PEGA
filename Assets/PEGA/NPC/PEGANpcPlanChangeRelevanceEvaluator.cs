using System;
using Immersive.Framework.Actors;

namespace PEGA.NPC
{
    public enum PEGANpcPlanChangeReason
    {
        None = 0,
        CharacterIdentityChanged = 10,
        CharacterTargetChanged = 20,
        CharacterTargetMayEnablePlan = 30,
        ActiveActionInvalid = 40,
        ActiveActionInterrupted = 50,
        ActiveActionEffectCompleted = 60,
        ActiveActionTargetChanged = 70
    }

    public readonly struct PEGANpcPlanChangeAssessment
    {
        internal PEGANpcPlanChangeAssessment(PEGANpcPlanChangeReason reason)
        {
            Reason = reason;
        }

        public PEGANpcPlanChangeReason Reason { get; }

        public bool RequiresReevaluation => Reason != PEGANpcPlanChangeReason.None;
    }

    /// <summary>
    /// Classifica se uma mudança pode alterar os planos avaliados pelo GOAP.
    /// Não escolhe, substitui, cancela nem executa Actions.
    /// </summary>
    public static class PEGANpcPlanChangeRelevanceEvaluator
    {
        public static PEGANpcPlanChangeAssessment EvaluateKnowledgeChange(
            PEGANpcPlanSelectionResult currentSelection,
            PEGANpcKnowledgeFactChangedEvent change,
            ActorId rickSubjectActorId)
        {
            ValidateSelection(currentSelection, nameof(currentSelection));
            if (change == null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            if (!rickSubjectActorId.IsValid)
            {
                throw new ArgumentException("A valid ActorId for the configured Rick subject is required.", nameof(rickSubjectActorId));
            }

            if (!change.HasValueOrStatusChanged ||
                !change.SubjectActorId.HasValue ||
                change.SubjectActorId.Value != rickSubjectActorId ||
                !IsCharacterPlanningFact(change.FactKind))
            {
                return NoChange();
            }

            PEGANpcPlanningTargetId subjectTargetId = PEGANpcPlanningTargetId.ForActor(rickSubjectActorId);
            if (HasApproachPlanForTarget(currentSelection, subjectTargetId))
            {
                PEGANpcPlanChangeReason reason = change.FactKind switch
                {
                    PEGANpcKnowledgeFactKind.ObservedCharacterIdentity =>
                        PEGANpcPlanChangeReason.CharacterIdentityChanged,
                    PEGANpcKnowledgeFactKind.LastObservedRickPosition =>
                        PEGANpcPlanChangeReason.CharacterTargetChanged,
                    _ => PEGANpcPlanChangeReason.None
                };

                return new PEGANpcPlanChangeAssessment(reason);
            }

            if (change.CurrentStatus != PEGANpcKnowledgeStatus.Known)
            {
                return NoChange();
            }

            return HasApplicableCharacterPositionDesire(currentSelection)
                ? new PEGANpcPlanChangeAssessment(PEGANpcPlanChangeReason.CharacterTargetMayEnablePlan)
                : NoChange();
        }

        public static PEGANpcPlanChangeAssessment EvaluateActionOutcome(
            PEGANpcPlanSelectionResult currentSelection,
            long activeRequestId,
            PEGANpcActionTerminalOutcomeEvent outcome)
        {
            ValidateSelection(currentSelection, nameof(currentSelection));
            if (outcome == null)
            {
                throw new ArgumentNullException(nameof(outcome));
            }

            if (activeRequestId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(activeRequestId));
            }

            if (!currentSelection.HasSelectedPlan)
            {
                return NoChange();
            }

            PEGANpcPlanSnapshot selectedPlan = currentSelection.SelectedPlan;
            PEGANpcActionExecutionSnapshot execution = outcome.Execution;
            if (execution.RequestId != activeRequestId ||
                !TryGetExpectedEffect(selectedPlan.ActionId, out PEGANpcActionEffectKind expectedEffect) ||
                selectedPlan.PredictedEffect != expectedEffect ||
                execution.DeclaredEffect.Kind != expectedEffect)
            {
                return NoChange();
            }

            if (execution.Outcome != PEGANpcActionOutcome.Success &&
                execution.Outcome != PEGANpcActionOutcome.Invalid &&
                execution.Outcome != PEGANpcActionOutcome.Interrupted &&
                execution.Outcome != PEGANpcActionOutcome.Cancelled)
            {
                return NoChange();
            }

            if (execution.Outcome == PEGANpcActionOutcome.Cancelled)
            {
                return NoChange();
            }

            if (execution.DeclaredEffect.IsTargetResolved &&
                !execution.DeclaredEffect.TargetPosition.Equals(selectedPlan.TargetPosition))
            {
                return new PEGANpcPlanChangeAssessment(PEGANpcPlanChangeReason.ActiveActionTargetChanged);
            }

            return execution.Outcome switch
            {
                PEGANpcActionOutcome.Invalid =>
                    new PEGANpcPlanChangeAssessment(PEGANpcPlanChangeReason.ActiveActionInvalid),
                PEGANpcActionOutcome.Interrupted =>
                    new PEGANpcPlanChangeAssessment(PEGANpcPlanChangeReason.ActiveActionInterrupted),
                PEGANpcActionOutcome.Success when HasSuccessfulEffect(execution) =>
                    new PEGANpcPlanChangeAssessment(PEGANpcPlanChangeReason.ActiveActionEffectCompleted),
                _ => NoChange()
            };
        }

        private static void ValidateSelection(
            PEGANpcPlanSelectionResult selection,
            string parameterName)
        {
            if (selection == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (!selection.IsValid)
            {
                throw new ArgumentException(
                    "Plan change relevance requires a valid prior selection snapshot.",
                    parameterName);
            }
        }

        private static bool IsCharacterPlanningFact(PEGANpcKnowledgeFactKind factKind) =>
            factKind == PEGANpcKnowledgeFactKind.ObservedCharacterIdentity ||
            factKind == PEGANpcKnowledgeFactKind.LastObservedRickPosition;

        private static bool HasApproachPlanForTarget(
            PEGANpcPlanSelectionResult selection,
            PEGANpcPlanningTargetId targetId)
        {
            for (int index = 0; index < selection.CandidatePlans.Count; index++)
            {
                PEGANpcPlanSnapshot candidate = selection.CandidatePlans[index];
                if (candidate.ActionId == PEGANpcPlanningActionId.ApproachKnownRick &&
                    candidate.TargetId == targetId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasApplicableCharacterPositionDesire(PEGANpcPlanSelectionResult selection)
        {
            for (int index = 0; index < selection.DesireEvaluations.Count; index++)
            {
                PEGANpcDesirePlanEvaluation desire = selection.DesireEvaluations[index];
                if (desire.IsApplicable &&
                    desire.DesiredGoal == PEGANpcDesireGoalKind.ReachKnownCharacterPosition)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetExpectedEffect(
            PEGANpcPlanningActionId actionId,
            out PEGANpcActionEffectKind effectKind)
        {
            switch (actionId)
            {
                case PEGANpcPlanningActionId.ApproachKnownRick:
                    effectKind = PEGANpcActionEffectKind.ApproachKnownRickPosition;
                    return true;
                case PEGANpcPlanningActionId.TravelToEscapeTarget:
                    effectKind = PEGANpcActionEffectKind.ReachSelectedEscapeDestination;
                    return true;
                case PEGANpcPlanningActionId.InvestigateLastKnownRickPosition:
                    effectKind = PEGANpcActionEffectKind.ReachLocalObservationPosition;
                    return true;
                default:
                    effectKind = default;
                    return false;
            }
        }

        private static bool HasSuccessfulEffect(PEGANpcActionExecutionSnapshot execution)
        {
            PEGANpcActionEffectDeclaration effect = execution.DeclaredEffect;
            float actualDistance = execution.ActualDistanceToTargetMeters;
            float targetRadius = effect.TargetRadiusMeters;
            return execution.EffectObserved &&
                   execution.HasNavigationSnapshot &&
                   effect.IsTargetResolved &&
                   execution.RequestId > 0 &&
                   effect.TargetPosition.Equals(execution.TargetPosition) &&
                   IsNonNegativeFinite(actualDistance) &&
                   IsNonNegativeFinite(targetRadius) &&
                   actualDistance <= targetRadius;
        }

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static PEGANpcPlanChangeAssessment NoChange() =>
            new PEGANpcPlanChangeAssessment(PEGANpcPlanChangeReason.None);
    }
}

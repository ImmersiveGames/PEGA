using System;

namespace PEGA.NPC
{
    public enum PEGANpcPlanChangeReason
    {
        None = 0,
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
                !selectedPlan.PredictedEffect.IsValid ||
                execution.DeclaredEffect.Kind != selectedPlan.PredictedEffect)
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

        private static bool HasSuccessfulEffect(PEGANpcActionExecutionSnapshot execution)
            => execution.EffectObserved && execution.RequestId > 0;

        private static PEGANpcPlanChangeAssessment NoChange() =>
            new PEGANpcPlanChangeAssessment(PEGANpcPlanChangeReason.None);
    }
}

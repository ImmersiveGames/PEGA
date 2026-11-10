using System;
using PEGA.NPC.Actions.Runtime;
namespace PEGA.NPC.Autonomy.Planning
{
    public enum PegaNpcPlanChangeReason
    {
        None = 0,
        ActiveActionInvalid = 40,
        ActiveActionInterrupted = 50,
        ActiveActionEffectCompleted = 60,
        ActiveActionTargetChanged = 70
    }

    public readonly struct PegaNpcPlanChangeAssessment
    {
        internal PegaNpcPlanChangeAssessment(PegaNpcPlanChangeReason reason)
        {
            Reason = reason;
        }

        public PegaNpcPlanChangeReason Reason { get; }

        public bool RequiresReevaluation => Reason != PegaNpcPlanChangeReason.None;
    }

    /// <summary>
    /// Classifica se uma mudança pode alterar os planos avaliados pelo GOAP.
    /// Não escolhe, substitui, cancela nem executa Actions.
    /// </summary>
    public static class PegaNpcPlanChangeRelevanceEvaluator
    {
        public static PegaNpcPlanChangeAssessment EvaluateActionOutcome(
            PegaNpcPlanSelectionResult currentSelection,
            long activeRequestId,
            PegaNpcActionTerminalOutcomeEvent outcome)
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

            PegaNpcPlanSnapshot selectedPlan = currentSelection.SelectedPlan;
            PegaNpcActionExecutionSnapshot execution = outcome.Execution;
            if (execution.RequestId != activeRequestId ||
                !selectedPlan.PredictedEffect.IsValid ||
                execution.DeclaredEffect.Kind != selectedPlan.PredictedEffect)
            {
                return NoChange();
            }

            if (execution.Outcome != PegaNpcActionOutcome.Success &&
                execution.Outcome != PegaNpcActionOutcome.Invalid &&
                execution.Outcome != PegaNpcActionOutcome.Interrupted &&
                execution.Outcome != PegaNpcActionOutcome.Cancelled)
            {
                return NoChange();
            }

            if (execution.Outcome == PegaNpcActionOutcome.Cancelled)
            {
                return NoChange();
            }

            if (execution.DeclaredEffect.IsTargetResolved &&
                !execution.DeclaredEffect.TargetPosition.Equals(selectedPlan.TargetPosition))
            {
                return new PegaNpcPlanChangeAssessment(PegaNpcPlanChangeReason.ActiveActionTargetChanged);
            }

            return execution.Outcome switch
            {
                PegaNpcActionOutcome.Invalid =>
                    new PegaNpcPlanChangeAssessment(PegaNpcPlanChangeReason.ActiveActionInvalid),
                PegaNpcActionOutcome.Interrupted =>
                    new PegaNpcPlanChangeAssessment(PegaNpcPlanChangeReason.ActiveActionInterrupted),
                PegaNpcActionOutcome.Success when HasSuccessfulEffect(execution) =>
                    new PegaNpcPlanChangeAssessment(PegaNpcPlanChangeReason.ActiveActionEffectCompleted),
                _ => NoChange()
            };
        }

        private static void ValidateSelection(
            PegaNpcPlanSelectionResult selection,
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

        private static bool HasSuccessfulEffect(PegaNpcActionExecutionSnapshot execution)
            => execution.EffectObserved && execution.RequestId > 0;

        private static PegaNpcPlanChangeAssessment NoChange() =>
            new PegaNpcPlanChangeAssessment(PegaNpcPlanChangeReason.None);
    }
}

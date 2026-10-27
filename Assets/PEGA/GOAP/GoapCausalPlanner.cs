using System;
using System.Collections.Generic;

namespace ImmersiveGames.Goap
{
    /// <summary>
    /// Identidade opaca de um Target que já foi reconhecido como conhecido pela autoridade de conhecimento do jogo.
    /// O planner não descobre Targets nem consulta World State.
    /// </summary>
    public interface IGoapKnownTarget
    {
    }

    /// <summary>
    /// Uma aplicação candidata de uma Action a um Target conhecido e compatível, se houver. A lista do planner
    /// deve vir da autoridade do jogo que resolve candidatos legitimamente conhecidos; se as condições ou Effects
    /// variarem por Target, a definição fornecida já deve corresponder a esta aplicação.
    /// </summary>
    public sealed class GoapActionCandidate
    {
        public GoapActionCandidate(GoapActionDefinition actionDefinition)
            : this(actionDefinition, null)
        {
        }

        public GoapActionCandidate(
            GoapActionDefinition actionDefinition,
            IGoapKnownTarget target)
        {
            ActionDefinition = actionDefinition ?? throw new ArgumentNullException(nameof(actionDefinition));
            Target = target;
        }

        public GoapActionDefinition ActionDefinition { get; }

        public IGoapKnownTarget Target { get; }

        public bool HasTarget => Target != null;
    }

    /// <summary>Plano causal encontrado, na ordem em que suas aplicações de Action devem ocorrer.</summary>
    public sealed class GoapPlan
    {
        internal GoapPlan(
            GoapGoal goal,
            IReadOnlyList<GoapActionCandidate> steps,
            GoapHypotheticalState finalState)
        {
            Goal = goal ?? throw new ArgumentNullException(nameof(goal));
            Steps = CopySteps(steps);
            FinalState = finalState ?? throw new ArgumentNullException(nameof(finalState));
        }

        public GoapGoal Goal { get; }

        public IReadOnlyList<GoapActionCandidate> Steps { get; }

        public GoapHypotheticalState FinalState { get; }

        private static IReadOnlyList<GoapActionCandidate> CopySteps(
            IReadOnlyList<GoapActionCandidate> steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            GoapActionCandidate[] copy = new GoapActionCandidate[steps.Count];
            for (int index = 0; index < steps.Count; index++)
            {
                copy[index] = steps[index] ?? throw new ArgumentException(
                    "A plan cannot contain a null Action candidate.",
                    nameof(steps));
            }

            return Array.AsReadOnly(copy);
        }
    }

    public enum GoapSearchStatus
    {
        PlanFound = 1,
        NoKnownSolution = 2,
        BudgetExhausted = 3
    }

    /// <summary>Resultado explícito da busca, incluindo o orçamento e quantos candidatos foram avaliados.</summary>
    public sealed class GoapSearchResult
    {
        internal GoapSearchResult(
            GoapSearchStatus status,
            GoapPlan plan,
            int candidateEvaluations,
            int candidateEvaluationBudget)
        {
            if (!Enum.IsDefined(typeof(GoapSearchStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (candidateEvaluationBudget <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateEvaluationBudget));
            }

            if (candidateEvaluations < 0 || candidateEvaluations > candidateEvaluationBudget)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateEvaluations));
            }

            if ((status == GoapSearchStatus.PlanFound) != (plan != null))
            {
                throw new ArgumentException("Only a PlanFound result can contain a plan.", nameof(plan));
            }

            Status = status;
            Plan = plan;
            CandidateEvaluations = candidateEvaluations;
            CandidateEvaluationBudget = candidateEvaluationBudget;
        }

        public GoapSearchStatus Status { get; }

        public GoapPlan Plan { get; }

        public bool HasPlan => Status == GoapSearchStatus.PlanFound;

        public int CandidateEvaluations { get; }

        public int CandidateEvaluationBudget { get; }
    }

    /// <summary>
    /// Bounded enumeration of causal plans. A BudgetExhausted result may contain partial alternatives. An
    /// unknown specialized step evaluation also marks the result incomplete without assuming availability.
    /// </summary>
    public sealed class GoapSearchAlternativesResult
    {
        private readonly IReadOnlyList<GoapPlan> _alternatives;

        internal GoapSearchAlternativesResult(
            GoapSearchStatus status,
            IReadOnlyList<GoapPlan> alternatives,
            int candidateEvaluations,
            int candidateEvaluationBudget,
            bool hasIncompleteStepEvaluation,
            string stepEvaluationDiagnostic)
        {
            if (!Enum.IsDefined(typeof(GoapSearchStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (alternatives == null)
            {
                throw new ArgumentNullException(nameof(alternatives));
            }

            if (candidateEvaluationBudget <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateEvaluationBudget));
            }

            if (candidateEvaluations < 0 || candidateEvaluations > candidateEvaluationBudget)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateEvaluations));
            }

            if ((status == GoapSearchStatus.PlanFound) != (alternatives.Count > 0) &&
                status != GoapSearchStatus.BudgetExhausted)
            {
                throw new ArgumentException(
                    "A complete PlanFound result must contain alternatives, and NoKnownSolution must not.",
                    nameof(alternatives));
            }

            GoapPlan[] copy = new GoapPlan[alternatives.Count];
            for (int index = 0; index < alternatives.Count; index++)
            {
                copy[index] = alternatives[index] ?? throw new ArgumentException(
                    "Plan alternatives cannot contain null entries.",
                    nameof(alternatives));
            }

            Status = status;
            _alternatives = Array.AsReadOnly(copy);
            CandidateEvaluations = candidateEvaluations;
            CandidateEvaluationBudget = candidateEvaluationBudget;
            HasIncompleteStepEvaluation = hasIncompleteStepEvaluation;
            StepEvaluationDiagnostic = stepEvaluationDiagnostic ?? string.Empty;
        }

        public GoapSearchStatus Status { get; }

        public IReadOnlyList<GoapPlan> Alternatives => _alternatives;

        public bool HasAlternatives => _alternatives.Count > 0;

        public bool IsComplete => Status != GoapSearchStatus.BudgetExhausted && !HasIncompleteStepEvaluation;

        public int CandidateEvaluations { get; }

        public int CandidateEvaluationBudget { get; }

        /// <summary>True when an external causal evaluator could not determine whether a candidate step is available.</summary>
        public bool HasIncompleteStepEvaluation { get; }

        public string StepEvaluationDiagnostic { get; }
    }

    public enum GoapActionStepAvailability
    {
        Available = 0,
        Unavailable = 10,
        Unknown = 20
    }

    /// <summary>
    /// Optional domain evaluator for Action availability that depends on the hypothetical state immediately
    /// before a step. Unknown candidates are never expanded and make the result explicitly incomplete.
    /// </summary>
    public interface IGoapActionStepEvaluator
    {
        GoapActionStepAvailability EvaluateActionStep(
            GoapActionCandidate candidate,
            IGoapHypotheticalStateView stateBeforeStep,
            out string diagnostic);
    }

    /// <summary>
    /// Busca em largura por sequências de aplicações de Actions. Cada avaliação de um candidato contra as
    /// Preconditions e o avaliador especializado opcional consome uma unidade do orçamento configurado na chamada.
    /// A ordem da lista desempata caminhos; o mesmo candidato pode ser reutilizado. Para avaliações determinísticas,
    /// entradas iguais e ordenadas produzem os mesmos planos na mesma ordem.
    /// </summary>
    public sealed class GoapCausalPlanner
    {
        public GoapSearchResult Search(
            GoapGoal goal,
            GoapHypotheticalState initialState,
            IReadOnlyList<GoapActionCandidate> knownCandidates,
            int candidateEvaluationBudget)
        {
            if (goal == null)
            {
                throw new ArgumentNullException(nameof(goal));
            }

            if (initialState == null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            if (candidateEvaluationBudget <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(candidateEvaluationBudget),
                    "The candidate evaluation budget must be greater than zero.");
            }

            GoapActionCandidate[] candidates = CopyCandidates(knownCandidates);
            Queue<SearchNode> frontier = new Queue<SearchNode>();
            SearchNode root = new SearchNode(initialState, null, null);
            if (IsConditionTrue(goal.TargetCondition, initialState))
            {
                return CreatePlanResult(goal, root, 0, candidateEvaluationBudget);
            }

            int candidateEvaluations = 0;
            frontier.Enqueue(root);
            while (frontier.Count > 0)
            {
                SearchNode node = frontier.Dequeue();
                for (int index = 0; index < candidates.Length; index++)
                {
                    if (candidateEvaluations >= candidateEvaluationBudget)
                    {
                        return CreateResult(
                            GoapSearchStatus.BudgetExhausted,
                            null,
                            candidateEvaluations,
                            candidateEvaluationBudget);
                    }

                    GoapActionCandidate candidate = candidates[index];
                    candidateEvaluations++;
                    if (!AreSuccessPreconditionsTrue(candidate.ActionDefinition, node.State))
                    {
                        continue;
                    }

                    GoapHypotheticalState nextState = node.State.Project(candidate.ActionDefinition);
                    SearchNode nextNode = new SearchNode(nextState, node, candidate);
                    if (IsConditionTrue(goal.TargetCondition, nextState))
                    {
                        return CreatePlanResult(
                            goal,
                            nextNode,
                            candidateEvaluations,
                            candidateEvaluationBudget);
                    }

                    frontier.Enqueue(nextNode);
                }
            }

            return CreateResult(
                GoapSearchStatus.NoKnownSolution,
                null,
                candidateEvaluations,
                candidateEvaluationBudget);
        }

        /// <summary>
        /// Enumerates every goal-reaching alternative discoverable within the supplied candidate-evaluation
        /// budget. Results follow breadth-first depth and input candidate order. On budget exhaustion, any
        /// alternatives already found are returned with BudgetExhausted status.
        /// </summary>
        public GoapSearchAlternativesResult SearchAlternatives(
            GoapGoal goal,
            GoapHypotheticalState initialState,
            IReadOnlyList<GoapActionCandidate> knownCandidates,
            int candidateEvaluationBudget) =>
            SearchAlternatives(goal, initialState, knownCandidates, candidateEvaluationBudget, null);

        /// <summary>
        /// Enumerates goal-reaching alternatives while applying an optional domain availability check to each
        /// candidate at the exact hypothetical state before that step. The callback does not execute the Action
        /// or apply Effects; the planner projects only after both Preconditions and availability are established.
        /// </summary>
        public GoapSearchAlternativesResult SearchAlternatives(
            GoapGoal goal,
            GoapHypotheticalState initialState,
            IReadOnlyList<GoapActionCandidate> knownCandidates,
            int candidateEvaluationBudget,
            IGoapActionStepEvaluator stepEvaluator)
        {
            if (goal == null)
            {
                throw new ArgumentNullException(nameof(goal));
            }

            if (initialState == null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            if (candidateEvaluationBudget <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(candidateEvaluationBudget),
                    "The candidate evaluation budget must be greater than zero.");
            }

            GoapActionCandidate[] candidates = CopyCandidates(knownCandidates);
            Queue<SearchNode> frontier = new Queue<SearchNode>();
            SearchNode root = new SearchNode(initialState, null, null);
            List<GoapPlan> alternatives = new List<GoapPlan>();
            if (IsConditionTrue(goal.TargetCondition, initialState))
            {
                alternatives.Add(CreatePlan(goal, root));
                return CreateAlternativesResult(
                    GoapSearchStatus.PlanFound,
                    alternatives,
                    0,
                    candidateEvaluationBudget,
                    false,
                    string.Empty);
            }

            int candidateEvaluations = 0;
            bool hasIncompleteStepEvaluation = false;
            string stepEvaluationDiagnostic = string.Empty;
            frontier.Enqueue(root);
            while (frontier.Count > 0)
            {
                SearchNode node = frontier.Dequeue();
                for (int index = 0; index < candidates.Length; index++)
                {
                    if (candidateEvaluations >= candidateEvaluationBudget)
                    {
                        return CreateAlternativesResult(
                            GoapSearchStatus.BudgetExhausted,
                            alternatives,
                            candidateEvaluations,
                            candidateEvaluationBudget,
                            hasIncompleteStepEvaluation,
                            stepEvaluationDiagnostic);
                    }

                    GoapActionCandidate candidate = candidates[index];
                    candidateEvaluations++;
                    if (!AreSuccessPreconditionsTrue(candidate.ActionDefinition, node.State))
                    {
                        continue;
                    }

                    if (stepEvaluator != null)
                    {
                        GoapActionStepAvailability availability = stepEvaluator.EvaluateActionStep(
                            candidate,
                            node.State,
                            out string diagnostic);
                        if (availability != GoapActionStepAvailability.Available)
                        {
                            if (availability == GoapActionStepAvailability.Unknown)
                            {
                                hasIncompleteStepEvaluation = true;
                                if (string.IsNullOrWhiteSpace(stepEvaluationDiagnostic) &&
                                    !string.IsNullOrWhiteSpace(diagnostic))
                                {
                                    stepEvaluationDiagnostic = diagnostic;
                                }
                            }
                            else if (availability != GoapActionStepAvailability.Unavailable)
                            {
                                throw new InvalidOperationException(
                                    "An Action step evaluator returned an unsupported availability status.");
                            }

                            continue;
                        }
                    }

                    GoapHypotheticalState nextState = node.State.Project(candidate.ActionDefinition);
                    SearchNode nextNode = new SearchNode(nextState, node, candidate);
                    if (IsConditionTrue(goal.TargetCondition, nextState))
                    {
                        alternatives.Add(CreatePlan(goal, nextNode));
                        continue;
                    }

                    frontier.Enqueue(nextNode);
                }
            }

            GoapSearchStatus status = alternatives.Count > 0
                ? GoapSearchStatus.PlanFound
                : GoapSearchStatus.NoKnownSolution;
            return CreateAlternativesResult(
                status,
                alternatives,
                candidateEvaluations,
                candidateEvaluationBudget,
                hasIncompleteStepEvaluation,
                stepEvaluationDiagnostic);
        }

        private static GoapActionCandidate[] CopyCandidates(
            IReadOnlyList<GoapActionCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            GoapActionCandidate[] copy = new GoapActionCandidate[candidates.Count];
            for (int index = 0; index < candidates.Count; index++)
            {
                copy[index] = candidates[index] ?? throw new ArgumentException(
                    "Known Action candidates cannot contain null entries.",
                    nameof(candidates));
            }

            return copy;
        }

        private static bool AreSuccessPreconditionsTrue(
            GoapActionDefinition action,
            IGoapStateView state)
        {
            for (int index = 0; index < action.SuccessPreconditions.Count; index++)
            {
                if (!IsConditionTrue(action.SuccessPreconditions[index], state))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsConditionTrue(IGoapCondition condition, IGoapStateView state)
        {
            return GoapConditionEvaluation.Evaluate(condition, state) == GoapTruthValue.True;
        }

        private static GoapSearchResult CreatePlanResult(
            GoapGoal goal,
            SearchNode terminalNode,
            int candidateEvaluations,
            int candidateEvaluationBudget)
        {
            GoapPlan plan = CreatePlan(goal, terminalNode);
            return CreateResult(
                GoapSearchStatus.PlanFound,
                plan,
                candidateEvaluations,
                candidateEvaluationBudget);
        }

        private static GoapPlan CreatePlan(GoapGoal goal, SearchNode terminalNode)
        {
            GoapActionCandidate[] steps = new GoapActionCandidate[terminalNode.Depth];
            SearchNode node = terminalNode;
            for (int index = steps.Length - 1; index >= 0; index--)
            {
                steps[index] = node.ActionCandidate;
                node = node.Previous;
            }

            return new GoapPlan(goal, steps, terminalNode.State);
        }

        private static GoapSearchAlternativesResult CreateAlternativesResult(
            GoapSearchStatus status,
            IReadOnlyList<GoapPlan> alternatives,
            int candidateEvaluations,
            int candidateEvaluationBudget,
            bool hasIncompleteStepEvaluation,
            string stepEvaluationDiagnostic)
        {
            return new GoapSearchAlternativesResult(
                status,
                alternatives,
                candidateEvaluations,
                candidateEvaluationBudget,
                hasIncompleteStepEvaluation,
                stepEvaluationDiagnostic);
        }

        private static GoapSearchResult CreateResult(
            GoapSearchStatus status,
            GoapPlan plan,
            int candidateEvaluations,
            int candidateEvaluationBudget)
        {
            return new GoapSearchResult(status, plan, candidateEvaluations, candidateEvaluationBudget);
        }

        private sealed class SearchNode
        {
            public SearchNode(
                GoapHypotheticalState state,
                SearchNode previous,
                GoapActionCandidate actionCandidate)
            {
                State = state;
                Previous = previous;
                ActionCandidate = actionCandidate;
                Depth = previous == null ? 0 : previous.Depth + 1;
            }

            public GoapHypotheticalState State { get; }

            public SearchNode Previous { get; }

            public GoapActionCandidate ActionCandidate { get; }

            public int Depth { get; }
        }
    }
}

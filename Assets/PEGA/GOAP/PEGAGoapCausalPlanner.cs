using System;
using System.Collections.Generic;

namespace PEGA.GOAP
{
    /// <summary>
    /// Identidade opaca de um Target que já foi reconhecido como conhecido pela autoridade de conhecimento do PEGA.
    /// O planner não descobre Targets nem consulta World State.
    /// </summary>
    public interface IPEGAGoapKnownTarget
    {
    }

    /// <summary>
    /// Uma aplicação candidata de uma Action a um Target conhecido e compatível, se houver. A lista do planner
    /// deve vir da autoridade PEGA que resolve candidatos legitimamente conhecidos; se as condições ou Effects
    /// variarem por Target, a definição fornecida já deve corresponder a esta aplicação.
    /// </summary>
    public sealed class PEGAGoapActionCandidate
    {
        public PEGAGoapActionCandidate(PEGAGoapActionDefinition actionDefinition)
            : this(actionDefinition, null)
        {
        }

        public PEGAGoapActionCandidate(
            PEGAGoapActionDefinition actionDefinition,
            IPEGAGoapKnownTarget target)
        {
            ActionDefinition = actionDefinition ?? throw new ArgumentNullException(nameof(actionDefinition));
            Target = target;
        }

        public PEGAGoapActionDefinition ActionDefinition { get; }

        public IPEGAGoapKnownTarget Target { get; }

        public bool HasTarget => Target != null;
    }

    /// <summary>Plano causal encontrado, na ordem em que suas aplicações de Action devem ocorrer.</summary>
    public sealed class PEGAGoapPlan
    {
        internal PEGAGoapPlan(
            PEGAGoapGoal goal,
            IReadOnlyList<PEGAGoapActionCandidate> steps,
            PEGAGoapHypotheticalState finalState)
        {
            Goal = goal ?? throw new ArgumentNullException(nameof(goal));
            Steps = CopySteps(steps);
            FinalState = finalState ?? throw new ArgumentNullException(nameof(finalState));
        }

        public PEGAGoapGoal Goal { get; }

        public IReadOnlyList<PEGAGoapActionCandidate> Steps { get; }

        public PEGAGoapHypotheticalState FinalState { get; }

        private static IReadOnlyList<PEGAGoapActionCandidate> CopySteps(
            IReadOnlyList<PEGAGoapActionCandidate> steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            PEGAGoapActionCandidate[] copy = new PEGAGoapActionCandidate[steps.Count];
            for (int index = 0; index < steps.Count; index++)
            {
                copy[index] = steps[index] ?? throw new ArgumentException(
                    "A plan cannot contain a null Action candidate.",
                    nameof(steps));
            }

            return Array.AsReadOnly(copy);
        }
    }

    public enum PEGAGoapSearchStatus
    {
        PlanFound = 1,
        NoKnownSolution = 2,
        BudgetExhausted = 3
    }

    /// <summary>Resultado explícito da busca, incluindo o orçamento e quantos candidatos foram avaliados.</summary>
    public sealed class PEGAGoapSearchResult
    {
        internal PEGAGoapSearchResult(
            PEGAGoapSearchStatus status,
            PEGAGoapPlan plan,
            int candidateEvaluations,
            int candidateEvaluationBudget)
        {
            if (!Enum.IsDefined(typeof(PEGAGoapSearchStatus), status))
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

            if ((status == PEGAGoapSearchStatus.PlanFound) != (plan != null))
            {
                throw new ArgumentException("Only a PlanFound result can contain a plan.", nameof(plan));
            }

            Status = status;
            Plan = plan;
            CandidateEvaluations = candidateEvaluations;
            CandidateEvaluationBudget = candidateEvaluationBudget;
        }

        public PEGAGoapSearchStatus Status { get; }

        public PEGAGoapPlan Plan { get; }

        public bool HasPlan => Status == PEGAGoapSearchStatus.PlanFound;

        public int CandidateEvaluations { get; }

        public int CandidateEvaluationBudget { get; }
    }

    /// <summary>
    /// Bounded enumeration of causal plans. A BudgetExhausted result may contain partial alternatives;
    /// callers must retain the status instead of treating those alternatives as a complete search.
    /// </summary>
    public sealed class PEGAGoapSearchAlternativesResult
    {
        private readonly IReadOnlyList<PEGAGoapPlan> _alternatives;

        internal PEGAGoapSearchAlternativesResult(
            PEGAGoapSearchStatus status,
            IReadOnlyList<PEGAGoapPlan> alternatives,
            int candidateEvaluations,
            int candidateEvaluationBudget)
        {
            if (!Enum.IsDefined(typeof(PEGAGoapSearchStatus), status))
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

            if ((status == PEGAGoapSearchStatus.PlanFound) != (alternatives.Count > 0) &&
                status != PEGAGoapSearchStatus.BudgetExhausted)
            {
                throw new ArgumentException(
                    "A complete PlanFound result must contain alternatives, and NoKnownSolution must not.",
                    nameof(alternatives));
            }

            PEGAGoapPlan[] copy = new PEGAGoapPlan[alternatives.Count];
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
        }

        public PEGAGoapSearchStatus Status { get; }

        public IReadOnlyList<PEGAGoapPlan> Alternatives => _alternatives;

        public bool HasAlternatives => _alternatives.Count > 0;

        public bool IsComplete => Status != PEGAGoapSearchStatus.BudgetExhausted;

        public int CandidateEvaluations { get; }

        public int CandidateEvaluationBudget { get; }
    }

    /// <summary>
    /// Busca em largura por sequências de aplicações de Actions. Cada avaliação de um candidato contra as
    /// Preconditions consome uma unidade do orçamento configurado na chamada. A ordem da lista desempata caminhos;
    /// o mesmo candidato pode ser reutilizado. Para condições determinísticas, entradas iguais e ordenadas produzem
    /// o mesmo primeiro plano válido.
    /// </summary>
    public sealed class PEGAGoapCausalPlanner
    {
        public PEGAGoapSearchResult Search(
            PEGAGoapGoal goal,
            PEGAGoapHypotheticalState initialState,
            IReadOnlyList<PEGAGoapActionCandidate> knownCandidates,
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

            PEGAGoapActionCandidate[] candidates = CopyCandidates(knownCandidates);
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
                            PEGAGoapSearchStatus.BudgetExhausted,
                            null,
                            candidateEvaluations,
                            candidateEvaluationBudget);
                    }

                    PEGAGoapActionCandidate candidate = candidates[index];
                    candidateEvaluations++;
                    if (!AreSuccessPreconditionsTrue(candidate.ActionDefinition, node.State))
                    {
                        continue;
                    }

                    PEGAGoapHypotheticalState nextState = node.State.Project(candidate.ActionDefinition);
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
                PEGAGoapSearchStatus.NoKnownSolution,
                null,
                candidateEvaluations,
                candidateEvaluationBudget);
        }

        /// <summary>
        /// Enumerates every goal-reaching alternative discoverable within the supplied candidate-evaluation
        /// budget. Results follow breadth-first depth and input candidate order. On budget exhaustion, any
        /// alternatives already found are returned with BudgetExhausted status.
        /// </summary>
        public PEGAGoapSearchAlternativesResult SearchAlternatives(
            PEGAGoapGoal goal,
            PEGAGoapHypotheticalState initialState,
            IReadOnlyList<PEGAGoapActionCandidate> knownCandidates,
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

            PEGAGoapActionCandidate[] candidates = CopyCandidates(knownCandidates);
            Queue<SearchNode> frontier = new Queue<SearchNode>();
            SearchNode root = new SearchNode(initialState, null, null);
            List<PEGAGoapPlan> alternatives = new List<PEGAGoapPlan>();
            if (IsConditionTrue(goal.TargetCondition, initialState))
            {
                alternatives.Add(CreatePlan(goal, root));
                return CreateAlternativesResult(
                    PEGAGoapSearchStatus.PlanFound,
                    alternatives,
                    0,
                    candidateEvaluationBudget);
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
                        return CreateAlternativesResult(
                            PEGAGoapSearchStatus.BudgetExhausted,
                            alternatives,
                            candidateEvaluations,
                            candidateEvaluationBudget);
                    }

                    PEGAGoapActionCandidate candidate = candidates[index];
                    candidateEvaluations++;
                    if (!AreSuccessPreconditionsTrue(candidate.ActionDefinition, node.State))
                    {
                        continue;
                    }

                    PEGAGoapHypotheticalState nextState = node.State.Project(candidate.ActionDefinition);
                    SearchNode nextNode = new SearchNode(nextState, node, candidate);
                    if (IsConditionTrue(goal.TargetCondition, nextState))
                    {
                        alternatives.Add(CreatePlan(goal, nextNode));
                        continue;
                    }

                    frontier.Enqueue(nextNode);
                }
            }

            PEGAGoapSearchStatus status = alternatives.Count > 0
                ? PEGAGoapSearchStatus.PlanFound
                : PEGAGoapSearchStatus.NoKnownSolution;
            return CreateAlternativesResult(
                status,
                alternatives,
                candidateEvaluations,
                candidateEvaluationBudget);
        }

        private static PEGAGoapActionCandidate[] CopyCandidates(
            IReadOnlyList<PEGAGoapActionCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            PEGAGoapActionCandidate[] copy = new PEGAGoapActionCandidate[candidates.Count];
            for (int index = 0; index < candidates.Count; index++)
            {
                copy[index] = candidates[index] ?? throw new ArgumentException(
                    "Known Action candidates cannot contain null entries.",
                    nameof(candidates));
            }

            return copy;
        }

        private static bool AreSuccessPreconditionsTrue(
            PEGAGoapActionDefinition action,
            IPEGAGoapStateView state)
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

        private static bool IsConditionTrue(IPEGAGoapCondition condition, IPEGAGoapStateView state)
        {
            return PEGAGoapConditionEvaluation.Evaluate(condition, state) == PEGAGoapTruthValue.True;
        }

        private static PEGAGoapSearchResult CreatePlanResult(
            PEGAGoapGoal goal,
            SearchNode terminalNode,
            int candidateEvaluations,
            int candidateEvaluationBudget)
        {
            PEGAGoapPlan plan = CreatePlan(goal, terminalNode);
            return CreateResult(
                PEGAGoapSearchStatus.PlanFound,
                plan,
                candidateEvaluations,
                candidateEvaluationBudget);
        }

        private static PEGAGoapPlan CreatePlan(PEGAGoapGoal goal, SearchNode terminalNode)
        {
            PEGAGoapActionCandidate[] steps = new PEGAGoapActionCandidate[terminalNode.Depth];
            SearchNode node = terminalNode;
            for (int index = steps.Length - 1; index >= 0; index--)
            {
                steps[index] = node.ActionCandidate;
                node = node.Previous;
            }

            return new PEGAGoapPlan(goal, steps, terminalNode.State);
        }

        private static PEGAGoapSearchAlternativesResult CreateAlternativesResult(
            PEGAGoapSearchStatus status,
            IReadOnlyList<PEGAGoapPlan> alternatives,
            int candidateEvaluations,
            int candidateEvaluationBudget)
        {
            return new PEGAGoapSearchAlternativesResult(
                status,
                alternatives,
                candidateEvaluations,
                candidateEvaluationBudget);
        }

        private static PEGAGoapSearchResult CreateResult(
            PEGAGoapSearchStatus status,
            PEGAGoapPlan plan,
            int candidateEvaluations,
            int candidateEvaluationBudget)
        {
            return new PEGAGoapSearchResult(status, plan, candidateEvaluations, candidateEvaluationBudget);
        }

        private sealed class SearchNode
        {
            public SearchNode(
                PEGAGoapHypotheticalState state,
                SearchNode previous,
                PEGAGoapActionCandidate actionCandidate)
            {
                State = state;
                Previous = previous;
                ActionCandidate = actionCandidate;
                Depth = previous == null ? 0 : previous.Depth + 1;
            }

            public PEGAGoapHypotheticalState State { get; }

            public SearchNode Previous { get; }

            public PEGAGoapActionCandidate ActionCandidate { get; }

            public int Depth { get; }
        }
    }
}

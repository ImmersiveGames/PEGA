using System;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using PEGA.Actors;
using UnityEngine;
using UnityEngine.AI;

namespace PEGA.NPC
{
    public enum PEGANpcPlanningValidationMode
    {
        LiveKnowledge = 0,
        IsolatedFixture = 10
    }

    public enum PEGANpcPlanningTargetKind
    {
        CharacterPosition = 0,
        EscapeDestination = 10
    }

    public enum PEGANpcPlanningTargetProvenance
    {
        KnowledgeState = 0,
        DetachedFixture = 10
    }

    public enum PEGANpcPlanningTargetIdentityKind
    {
        Actor = 0,
        StableDataId = 10
    }

    public enum PEGANpcPlanningActionId
    {
        ApproachKnownRick = 10,
        TravelToEscapeTarget = 20
    }

    public enum PEGANpcPlanningRouteStatus
    {
        Complete = 0,
        Partial = 10,
        Invalid = 20
    }

    public readonly struct PEGANpcPlanningTargetId : IEquatable<PEGANpcPlanningTargetId>,
        IComparable<PEGANpcPlanningTargetId>
    {
        private readonly ActorId _actorId;
        private readonly string _stableDataId;

        private PEGANpcPlanningTargetId(
            PEGANpcPlanningTargetIdentityKind identityKind,
            ActorId actorId,
            string stableDataId)
        {
            IdentityKind = identityKind;
            _actorId = actorId;
            _stableDataId = stableDataId ?? string.Empty;
        }

        public PEGANpcPlanningTargetIdentityKind IdentityKind { get; }

        public bool IsValid => IdentityKind switch
        {
            PEGANpcPlanningTargetIdentityKind.Actor => _actorId.IsValid,
            PEGANpcPlanningTargetIdentityKind.StableDataId => !string.IsNullOrWhiteSpace(_stableDataId),
            _ => false
        };

        internal string StableOrderValue => IdentityKind == PEGANpcPlanningTargetIdentityKind.Actor
            ? _actorId.StableText ?? string.Empty
            : _stableDataId ?? string.Empty;

        internal static PEGANpcPlanningTargetId ForActor(ActorId actorId) =>
            actorId.IsValid
                ? new PEGANpcPlanningTargetId(PEGANpcPlanningTargetIdentityKind.Actor, actorId, string.Empty)
                : default;

        internal static PEGANpcPlanningTargetId ForStableDataId(string stableDataId) =>
            !string.IsNullOrWhiteSpace(stableDataId)
                ? new PEGANpcPlanningTargetId(PEGANpcPlanningTargetIdentityKind.StableDataId, default, stableDataId)
                : default;

        public bool Equals(PEGANpcPlanningTargetId other)
        {
            if (IdentityKind != other.IdentityKind)
            {
                return false;
            }

            return IdentityKind switch
            {
                PEGANpcPlanningTargetIdentityKind.Actor => _actorId == other._actorId,
                PEGANpcPlanningTargetIdentityKind.StableDataId =>
                    string.Equals(_stableDataId, other._stableDataId, StringComparison.Ordinal),
                _ => false
            };
        }

        public override bool Equals(object obj) =>
            obj is PEGANpcPlanningTargetId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)IdentityKind * 397) ^ StringComparer.Ordinal.GetHashCode(StableOrderValue);
            }
        }

        public int CompareTo(PEGANpcPlanningTargetId other)
        {
            int kindOrder = ((int)IdentityKind).CompareTo((int)other.IdentityKind);
            return kindOrder != 0
                ? kindOrder
                : string.CompareOrdinal(StableOrderValue, other.StableOrderValue);
        }

        public override string ToString() => IsValid
            ? $"{IdentityKind}:{StableOrderValue}"
            : "InvalidTargetId";

        public static bool operator ==(PEGANpcPlanningTargetId left, PEGANpcPlanningTargetId right) =>
            left.Equals(right);

        public static bool operator !=(PEGANpcPlanningTargetId left, PEGANpcPlanningTargetId right) =>
            !left.Equals(right);
    }

    public readonly struct PEGANpcPlanningTarget
    {
        internal PEGANpcPlanningTarget(
            PEGANpcPlanningTargetId targetId,
            PEGANpcPlanningTargetKind kind,
            PEGANpcPlanningTargetProvenance provenance,
            Vector3 knownPosition,
            bool hasEvidenceTime,
            double evidenceTimeSeconds)
        {
            TargetId = targetId;
            Kind = kind;
            Provenance = provenance;
            KnownPosition = knownPosition;
            HasEvidenceTime = hasEvidenceTime;
            EvidenceTimeSeconds = evidenceTimeSeconds;
        }

        public PEGANpcPlanningTargetId TargetId { get; }

        public PEGANpcPlanningTargetKind Kind { get; }

        public PEGANpcPlanningTargetProvenance Provenance { get; }

        public Vector3 KnownPosition { get; }

        public bool HasEvidenceTime { get; }

        public double EvidenceTimeSeconds { get; }

        internal bool IsValid =>
            TargetId.IsValid &&
            Enum.IsDefined(typeof(PEGANpcPlanningTargetKind), Kind) &&
            Enum.IsDefined(typeof(PEGANpcPlanningTargetProvenance), Provenance) &&
            IsFinite(KnownPosition) &&
            (!HasEvidenceTime || IsNonNegativeFinite(EvidenceTimeSeconds));

        internal static PEGANpcPlanningTarget FromKnownActorPosition(
            ActorId actorId,
            Vector3 knownPosition,
            double evidenceTimeSeconds) => new PEGANpcPlanningTarget(
                PEGANpcPlanningTargetId.ForActor(actorId),
                PEGANpcPlanningTargetKind.CharacterPosition,
                PEGANpcPlanningTargetProvenance.KnowledgeState,
                knownPosition,
                true,
                evidenceTimeSeconds);

        internal static PEGANpcPlanningTarget FromKnownEscapeDestination(
            PEGANpcPlanningTargetId targetId,
            Vector3 knownPosition,
            double evidenceTimeSeconds) => new PEGANpcPlanningTarget(
                targetId,
                PEGANpcPlanningTargetKind.EscapeDestination,
                PEGANpcPlanningTargetProvenance.KnowledgeState,
                knownPosition,
                true,
                evidenceTimeSeconds);

        internal static PEGANpcPlanningTarget DetachedFixture(
            string stableId,
            PEGANpcPlanningTargetKind kind,
            Vector3 knownPosition,
            double evidenceTimeSeconds = 0d) => new PEGANpcPlanningTarget(
                PEGANpcPlanningTargetId.ForStableDataId(stableId),
                kind,
                PEGANpcPlanningTargetProvenance.DetachedFixture,
                knownPosition,
                true,
                evidenceTimeSeconds);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(double value) =>
            value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public readonly struct PEGANpcNavigationRouteEstimate
    {
        internal PEGANpcNavigationRouteEstimate(
            PEGANpcPlanningRouteStatus status,
            float routeLengthMeters,
            float effectiveSpeedMetersPerSecond,
            int agentTypeId,
            int areaMask,
            string diagnostic)
        {
            Status = status;
            RouteLengthMeters = routeLengthMeters;
            EffectiveSpeedMetersPerSecond = effectiveSpeedMetersPerSecond;
            AgentTypeId = agentTypeId;
            AreaMask = areaMask;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public PEGANpcPlanningRouteStatus Status { get; }

        public float RouteLengthMeters { get; }

        public float EffectiveSpeedMetersPerSecond { get; }

        public int AgentTypeId { get; }

        public int AreaMask { get; }

        public string Diagnostic { get; }
    }

    public readonly struct PEGANpcPlanningActionCandidate
    {
        internal PEGANpcPlanningActionCandidate(
            PEGANpcPlanningActionId actionId,
            PEGANpcActionEffectKind predictedEffect,
            PEGANpcDesireGoalKind predictedGoal,
            PEGANpcPlanningTargetId targetId,
            PEGANpcPlanningTargetKind requiredTargetKind,
            bool preconditionsSatisfied,
            PEGAActorMovementSpeedRegime speedRegime,
            PEGANpcNavigationRouteEstimate routeEstimate,
            float intrinsicDurationSeconds)
        {
            ActionId = actionId;
            PredictedEffect = predictedEffect;
            PredictedGoal = predictedGoal;
            TargetId = targetId;
            RequiredTargetKind = requiredTargetKind;
            PreconditionsSatisfied = preconditionsSatisfied;
            SpeedRegime = speedRegime;
            RouteEstimate = routeEstimate;
            IntrinsicDurationSeconds = intrinsicDurationSeconds;
        }

        public PEGANpcPlanningActionId ActionId { get; }

        public PEGANpcActionEffectKind PredictedEffect { get; }

        public PEGANpcDesireGoalKind PredictedGoal { get; }

        public PEGANpcPlanningTargetId TargetId { get; }

        public PEGANpcPlanningTargetKind RequiredTargetKind { get; }

        public bool PreconditionsSatisfied { get; }

        public PEGAActorMovementSpeedRegime SpeedRegime { get; }

        public PEGANpcNavigationRouteEstimate RouteEstimate { get; }

        public float IntrinsicDurationSeconds { get; }

        internal static PEGANpcPlanningActionCandidate DetachedFixture(
            PEGANpcPlanningActionId actionId,
            PEGANpcActionEffectKind predictedEffect,
            PEGANpcDesireGoalKind predictedGoal,
            PEGANpcPlanningTarget target,
            bool preconditionsSatisfied,
            PEGAActorMovementSpeedRegime speedRegime,
            PEGANpcPlanningRouteStatus routeStatus,
            float routeLengthMeters,
            float effectiveSpeedMetersPerSecond,
            float intrinsicDurationSeconds)
        {
            PEGANpcNavigationRouteEstimate estimate = new PEGANpcNavigationRouteEstimate(
                routeStatus,
                routeLengthMeters,
                effectiveSpeedMetersPerSecond,
                -1,
                0,
                "Detached validation fixture.");
            return new PEGANpcPlanningActionCandidate(
                actionId,
                predictedEffect,
                predictedGoal,
                target.TargetId,
                target.Kind,
                preconditionsSatisfied,
                speedRegime,
                estimate,
                intrinsicDurationSeconds);
        }
    }

    public sealed class PEGANpcPlanningInput
    {
        private static readonly IReadOnlyList<PEGANpcPlanningTarget> EmptyTargets =
            Array.AsReadOnly(Array.Empty<PEGANpcPlanningTarget>());
        private static readonly IReadOnlyList<PEGANpcPlanningActionCandidate> EmptyCandidates =
            Array.AsReadOnly(Array.Empty<PEGANpcPlanningActionCandidate>());

        private readonly IReadOnlyList<PEGANpcPlanningTarget> _knownTargets;
        private readonly IReadOnlyList<PEGANpcPlanningActionCandidate> _actionCandidates;

        internal PEGANpcPlanningInput(
            PEGANpcPlanningValidationMode validationMode,
            PEGANpcDesireContext context,
            IReadOnlyList<PEGANpcPlanningTarget> knownTargets,
            IReadOnlyList<PEGANpcPlanningActionCandidate> actionCandidates)
        {
            ValidationMode = validationMode;
            Context = context;
            _knownTargets = Copy(knownTargets);
            _actionCandidates = Copy(actionCandidates);
        }

        public PEGANpcPlanningValidationMode ValidationMode { get; }

        public PEGANpcDesireContext Context { get; }

        public IReadOnlyList<PEGANpcPlanningTarget> KnownTargets => _knownTargets ?? EmptyTargets;

        public IReadOnlyList<PEGANpcPlanningActionCandidate> ActionCandidates =>
            _actionCandidates ?? EmptyCandidates;

        internal bool TryValidate(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!Enum.IsDefined(typeof(PEGANpcPlanningValidationMode), ValidationMode) ||
                !Enum.IsDefined(typeof(PEGANpcDesireContext), Context))
            {
                diagnostic = "Planning mode or context has an unsupported value.";
                return false;
            }

            HashSet<PEGANpcPlanningTargetId> targetIds = new HashSet<PEGANpcPlanningTargetId>();
            PEGANpcPlanningTargetProvenance requiredProvenance =
                ValidationMode == PEGANpcPlanningValidationMode.LiveKnowledge
                    ? PEGANpcPlanningTargetProvenance.KnowledgeState
                    : PEGANpcPlanningTargetProvenance.DetachedFixture;
            for (int index = 0; index < KnownTargets.Count; index++)
            {
                PEGANpcPlanningTarget target = KnownTargets[index];
                if (!target.IsValid || target.Provenance != requiredProvenance)
                {
                    diagnostic = $"Target at index {index} is invalid or has provenance incompatible with {ValidationMode}.";
                    return false;
                }

                if (requiredProvenance == PEGANpcPlanningTargetProvenance.KnowledgeState &&
                    (!target.HasEvidenceTime ||
                     (target.Kind == PEGANpcPlanningTargetKind.CharacterPosition &&
                      target.TargetId.IdentityKind != PEGANpcPlanningTargetIdentityKind.Actor) ||
                     (target.Kind == PEGANpcPlanningTargetKind.EscapeDestination &&
                      target.TargetId.IdentityKind != PEGANpcPlanningTargetIdentityKind.StableDataId)))
                {
                    diagnostic = $"Live Target at index {index} lacks its typed knowledge identity or evidence time.";
                    return false;
                }

                if (!targetIds.Add(target.TargetId))
                {
                    diagnostic = $"Target ID '{target.TargetId}' is duplicated in planning input.";
                    return false;
                }
            }

            for (int index = 0; index < ActionCandidates.Count; index++)
            {
                PEGANpcPlanningActionCandidate candidate = ActionCandidates[index];
                if (!Enum.IsDefined(typeof(PEGANpcPlanningActionId), candidate.ActionId) ||
                    !Enum.IsDefined(typeof(PEGANpcActionEffectKind), candidate.PredictedEffect) ||
                    !Enum.IsDefined(typeof(PEGANpcDesireGoalKind), candidate.PredictedGoal) ||
                    !Enum.IsDefined(typeof(PEGANpcPlanningTargetKind), candidate.RequiredTargetKind) ||
                    !Enum.IsDefined(typeof(PEGAActorMovementSpeedRegime), candidate.SpeedRegime) ||
                    !candidate.TargetId.IsValid ||
                    !HasCompatibleActionContract(candidate))
                {
                    diagnostic = $"Action candidate at index {index} has an invalid or incompatible Action/Effect/Target contract.";
                    return false;
                }

                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    PEGANpcPlanningActionCandidate previous = ActionCandidates[previousIndex];
                    if (previous.ActionId == candidate.ActionId && previous.TargetId == candidate.TargetId)
                    {
                        diagnostic = $"Duplicate Action/Target candidate '{candidate.ActionId}/{candidate.TargetId}'.";
                        return false;
                    }
                }
            }

            return true;
        }

        internal bool TryGetTarget(
            PEGANpcPlanningTargetId targetId,
            out PEGANpcPlanningTarget target)
        {
            for (int index = 0; index < KnownTargets.Count; index++)
            {
                if (KnownTargets[index].TargetId == targetId)
                {
                    target = KnownTargets[index];
                    return true;
                }
            }

            target = default;
            return false;
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.AsReadOnly(Array.Empty<T>());
            }

            T[] copy = new T[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return Array.AsReadOnly(copy);
        }

        private static bool HasCompatibleActionContract(PEGANpcPlanningActionCandidate candidate) =>
            candidate.ActionId switch
            {
                PEGANpcPlanningActionId.ApproachKnownRick =>
                    candidate.PredictedEffect == PEGANpcActionEffectKind.ApproachKnownRickPosition &&
                    candidate.PredictedGoal == PEGANpcDesireGoalKind.ReachKnownCharacterPosition &&
                    candidate.RequiredTargetKind == PEGANpcPlanningTargetKind.CharacterPosition,
                PEGANpcPlanningActionId.TravelToEscapeTarget =>
                    candidate.PredictedEffect == PEGANpcActionEffectKind.ReachSelectedEscapeDestination &&
                    candidate.PredictedGoal == PEGANpcDesireGoalKind.ReachKnownEscapeDestination &&
                    candidate.RequiredTargetKind == PEGANpcPlanningTargetKind.EscapeDestination,
                _ => false
            };
    }

    public readonly struct PEGANpcPlanSnapshot
    {
        internal PEGANpcPlanSnapshot(
            string desireId,
            string desireName,
            PEGANpcPlanningActionCandidate action,
            PEGANpcPlanningTarget target,
            float estimatedSeconds,
            float effectiveDesireWeight,
            float costPreference,
            float score)
        {
            DesireId = desireId ?? string.Empty;
            DesireName = desireName ?? string.Empty;
            ActionId = action.ActionId;
            PredictedEffect = action.PredictedEffect;
            PredictedGoal = action.PredictedGoal;
            TargetId = target.TargetId;
            TargetKind = target.Kind;
            TargetProvenance = target.Provenance;
            TargetPosition = target.KnownPosition;
            EvidenceTimeSeconds = target.EvidenceTimeSeconds;
            RouteLengthMeters = action.RouteEstimate.RouteLengthMeters;
            EffectiveSpeedMetersPerSecond = action.RouteEstimate.EffectiveSpeedMetersPerSecond;
            IntrinsicDurationSeconds = action.IntrinsicDurationSeconds;
            EstimatedSeconds = estimatedSeconds;
            EffectiveDesireWeight = effectiveDesireWeight;
            CostPreference = costPreference;
            Score = score;
            SpeedRegime = action.SpeedRegime;
        }

        public string DesireId { get; }

        public string DesireName { get; }

        public PEGANpcPlanningActionId ActionId { get; }

        public PEGANpcActionEffectKind PredictedEffect { get; }

        public PEGANpcDesireGoalKind PredictedGoal { get; }

        public PEGANpcPlanningTargetId TargetId { get; }

        public PEGANpcPlanningTargetKind TargetKind { get; }

        public PEGANpcPlanningTargetProvenance TargetProvenance { get; }

        public Vector3 TargetPosition { get; }

        public double EvidenceTimeSeconds { get; }

        public float RouteLengthMeters { get; }

        public float EffectiveSpeedMetersPerSecond { get; }

        public float IntrinsicDurationSeconds { get; }

        public float EstimatedSeconds { get; }

        public float EffectiveDesireWeight { get; }

        public float CostPreference { get; }

        public float Score { get; }

        public PEGAActorMovementSpeedRegime SpeedRegime { get; }
    }

    public sealed class PEGANpcDesirePlanEvaluation
    {
        private readonly IReadOnlyList<PEGANpcPlanSnapshot> _plans;

        internal PEGANpcDesirePlanEvaluation(
            string desireId,
            string desireName,
            PEGANpcDesireGoalKind desiredGoal,
            bool applicable,
            float effectiveWeight,
            IReadOnlyList<PEGANpcPlanSnapshot> plans,
            string diagnostic)
        {
            DesireId = desireId ?? string.Empty;
            DesireName = desireName ?? string.Empty;
            DesiredGoal = desiredGoal;
            IsApplicable = applicable;
            EffectiveWeight = effectiveWeight;
            PEGANpcPlanSnapshot[] copy = new PEGANpcPlanSnapshot[plans.Count];
            for (int index = 0; index < plans.Count; index++)
            {
                copy[index] = plans[index];
            }

            _plans = Array.AsReadOnly(copy);
            Diagnostic = diagnostic ?? string.Empty;
        }

        public string DesireId { get; }

        public string DesireName { get; }

        public PEGANpcDesireGoalKind DesiredGoal { get; }

        public bool IsApplicable { get; }

        public float EffectiveWeight { get; }

        public IReadOnlyList<PEGANpcPlanSnapshot> Plans => _plans;

        public bool HasPlan => _plans.Count > 0;

        public string Diagnostic { get; }
    }

    public sealed class PEGANpcPlanSelectionResult
    {
        private readonly IReadOnlyList<PEGANpcDesirePlanEvaluation> _desireEvaluations;
        private readonly IReadOnlyList<PEGANpcPlanSnapshot> _candidatePlans;

        internal PEGANpcPlanSelectionResult(
            bool isValid,
            PEGANpcPlanningValidationMode validationMode,
            PEGANpcDesireContext context,
            IReadOnlyList<PEGANpcDesirePlanEvaluation> desireEvaluations,
            IReadOnlyList<PEGANpcPlanSnapshot> candidatePlans,
            bool hasSelectedPlan,
            PEGANpcPlanSnapshot selectedPlan,
            string diagnostic)
        {
            IsValid = isValid;
            ValidationMode = validationMode;
            Context = context;
            _desireEvaluations = Copy(desireEvaluations);
            _candidatePlans = Copy(candidatePlans);
            HasSelectedPlan = hasSelectedPlan;
            SelectedPlan = selectedPlan;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool IsValid { get; }

        public PEGANpcPlanningValidationMode ValidationMode { get; }

        public PEGANpcDesireContext Context { get; }

        public IReadOnlyList<PEGANpcDesirePlanEvaluation> DesireEvaluations => _desireEvaluations;

        public IReadOnlyList<PEGANpcPlanSnapshot> CandidatePlans => _candidatePlans;

        public bool HasSelectedPlan { get; }

        public PEGANpcPlanSnapshot SelectedPlan { get; }

        public string Diagnostic { get; }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values)
        {
            T[] copy = new T[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    /// A request to continue the Rick pursuit at the last position confirmed by Perception.
    /// It is data for a planner/consumer; it does not select, start, cancel, or execute an Action.
    /// </summary>
    public readonly struct PEGANpcLastKnownRickPursuitIntent
    {
        internal PEGANpcLastKnownRickPursuitIntent(
            ActorId subjectActorId,
            Vector3 lastKnownPosition,
            double lastKnownObservationTimeSeconds,
            double contactLossObservationTimeSeconds)
        {
            SubjectActorId = subjectActorId;
            LastKnownPosition = lastKnownPosition;
            LastKnownObservationTimeSeconds = lastKnownObservationTimeSeconds;
            ContactLossObservationTimeSeconds = contactLossObservationTimeSeconds;
        }

        public bool IsValid =>
            SubjectActorId.IsValid &&
            IsFinite(LastKnownPosition) &&
            IsNonNegativeFinite(LastKnownObservationTimeSeconds) &&
            IsNonNegativeFinite(ContactLossObservationTimeSeconds) &&
            LastKnownObservationTimeSeconds <= ContactLossObservationTimeSeconds;

        public ActorId SubjectActorId { get; }

        public PEGANpcPlanningActionId ActionId => PEGANpcPlanningActionId.ApproachKnownRick;

        public PEGANpcPlanningTargetKind TargetKind => PEGANpcPlanningTargetKind.CharacterPosition;

        public Vector3 LastKnownPosition { get; }

        public double LastKnownObservationTimeSeconds { get; }

        public double ContactLossObservationTimeSeconds { get; }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(double value) =>
            value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>Published when real Perception confirms loss of Rick and a legitimate last position exists.</summary>
    public sealed class PEGANpcLastKnownRickPursuitIntentRequestedEvent : IEvent
    {
        internal PEGANpcLastKnownRickPursuitIntentRequestedEvent(
            PEGANpcLastKnownRickPursuitIntent intent)
        {
            Intent = intent;
        }

        public PEGANpcLastKnownRickPursuitIntent Intent { get; }
    }

    public enum PEGANpcPlanReevaluationTrigger
    {
        RickReacquired = 10,
        RickConsideredEvaded = 20
    }

    /// <summary>
    /// Sinaliza uma mudança do alvo observada ou uma perda de contato que persistiu até o prazo configurado.
    /// Não seleciona, cancela, substitui nem executa planos.
    /// </summary>
    /// <remarks>
    /// RickReacquired identifica planos dependentes do alvo; RickConsideredEvaded solicita avaliação do conjunto
    /// normal de Desejos. O consumidor autônomo é responsável por interpretar o gatilho.
    /// </remarks>
    public sealed class PEGANpcPlanReevaluationRequestedEvent : IEvent
    {
        internal PEGANpcPlanReevaluationRequestedEvent(
            PEGANpcPlanReevaluationTrigger trigger,
            ActorId targetActorId,
            double observationTimeSeconds,
            double requestedAtTimeSeconds)
        {
            Trigger = trigger;
            TargetActorId = targetActorId;
            ObservationTimeSeconds = observationTimeSeconds;
            RequestedAtTimeSeconds = requestedAtTimeSeconds;
        }

        public PEGANpcPlanReevaluationTrigger Trigger { get; }

        public ActorId TargetActorId { get; }

        /// <summary>
        /// Tempo da evidência que motivou o pedido: observação consolidada da readquisição ou amostra de perda.
        /// </summary>
        public double ObservationTimeSeconds { get; }

        /// <summary>Momento de jogo em que o pedido foi emitido.</summary>
        public double RequestedAtTimeSeconds { get; }
    }
}

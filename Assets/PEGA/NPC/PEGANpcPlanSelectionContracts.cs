using System;
using System.Collections.Generic;
using System.Globalization;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using ImmersiveGames.Goap;
using PEGA.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    public enum PEGANpcPlanningValidationMode
    {
        LiveKnowledge = 0,
        IsolatedFixture = 10
    }

    public enum PEGANpcPlanningTargetProvenance
    {
        KnowledgeState = 0,
        DetachedFixture = 10,
        DerivedFromKnowledge = 20
    }

    public enum PEGANpcPlanningTargetIdentityKind
    {
        Actor = 0,
        StableDataId = 10,
        InvestigationSample = 20,
        CharacterSeparationDestination = 30,
        PerceivedAreaInvestigationSample = 40
    }

    public enum PEGANpcPlanPurpose
    {
        DesireGoal = 0,
        PlanContinuationStep = 10
    }

    public enum PEGANpcPlanningRouteStatus
    {
        Complete = 0,
        Partial = 10,
        Invalid = 20,
        NotRequired = 30,
        Unknown = 40,
        Deferred = 50,
        Unreachable = 60
    }

    public readonly struct PEGANpcPlanningTargetId : IEquatable<PEGANpcPlanningTargetId>,
        IComparable<PEGANpcPlanningTargetId>
    {
        private readonly ActorId _actorId;
        private readonly string _stableDataId;
        private readonly double _contactLossObservationTimeSeconds;
        private readonly int _sampleOrdinal;
        private readonly double _presenceObservationTimeSeconds;

        private PEGANpcPlanningTargetId(
            PEGANpcPlanningTargetIdentityKind identityKind,
            ActorId actorId,
            string stableDataId,
            double contactLossObservationTimeSeconds = 0d,
            int sampleOrdinal = 0,
            double presenceObservationTimeSeconds = 0d)
        {
            IdentityKind = identityKind;
            _actorId = actorId;
            _stableDataId = stableDataId ?? string.Empty;
            _contactLossObservationTimeSeconds = contactLossObservationTimeSeconds;
            _sampleOrdinal = sampleOrdinal;
            _presenceObservationTimeSeconds = presenceObservationTimeSeconds;
        }

        public PEGANpcPlanningTargetIdentityKind IdentityKind { get; }

        public bool IsValid => IdentityKind switch
        {
            PEGANpcPlanningTargetIdentityKind.Actor => _actorId.IsValid,
            PEGANpcPlanningTargetIdentityKind.StableDataId => !string.IsNullOrWhiteSpace(_stableDataId),
            PEGANpcPlanningTargetIdentityKind.InvestigationSample =>
                _actorId.IsValid &&
                IsNonNegativeFinite(_contactLossObservationTimeSeconds) &&
                _sampleOrdinal >= 0,
            PEGANpcPlanningTargetIdentityKind.CharacterSeparationDestination => _actorId.IsValid,
            PEGANpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample =>
                IsNonNegativeFinite(_presenceObservationTimeSeconds) &&
                _sampleOrdinal >= 0,
            _ => false
        };

        internal string StableOrderValue => IdentityKind switch
        {
            PEGANpcPlanningTargetIdentityKind.Actor => _actorId.StableText ?? string.Empty,
            PEGANpcPlanningTargetIdentityKind.StableDataId => _stableDataId ?? string.Empty,
            PEGANpcPlanningTargetIdentityKind.InvestigationSample => string.Concat(
                _actorId.StableText ?? string.Empty,
                "|",
                _contactLossObservationTimeSeconds.ToString("R", CultureInfo.InvariantCulture),
                "|",
                _sampleOrdinal.ToString(CultureInfo.InvariantCulture)),
            PEGANpcPlanningTargetIdentityKind.CharacterSeparationDestination => _actorId.StableText ?? string.Empty,
            PEGANpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample => string.Concat(
                _presenceObservationTimeSeconds.ToString("R", CultureInfo.InvariantCulture),
                "|",
                _sampleOrdinal.ToString(CultureInfo.InvariantCulture)),
            _ => string.Empty
        };

        internal ActorId InvestigationSubjectActorId => _actorId;

        internal ActorId KnownActorId => _actorId;

        internal ActorId CharacterSeparationSubjectActorId => _actorId;

        internal double InvestigationLossTimeSeconds => _contactLossObservationTimeSeconds;

        internal int InvestigationSampleOrdinal => _sampleOrdinal;

        internal double PerceivedAreaEvidenceTimeSeconds => _presenceObservationTimeSeconds;

        internal int PerceivedAreaSampleOrdinal => _sampleOrdinal;

        internal static PEGANpcPlanningTargetId ForActor(ActorId actorId) =>
            actorId.IsValid
                ? new PEGANpcPlanningTargetId(PEGANpcPlanningTargetIdentityKind.Actor, actorId, string.Empty)
                : default;

        internal static PEGANpcPlanningTargetId ForStableDataId(string stableDataId) =>
            !string.IsNullOrWhiteSpace(stableDataId)
                ? new PEGANpcPlanningTargetId(PEGANpcPlanningTargetIdentityKind.StableDataId, default, stableDataId)
                : default;

        internal static PEGANpcPlanningTargetId ForInvestigationSample(
            ActorId subjectActorId,
            double contactLossObservationTimeSeconds,
            int sampleOrdinal) =>
            subjectActorId.IsValid &&
            IsNonNegativeFinite(contactLossObservationTimeSeconds) &&
            sampleOrdinal >= 0
                ? new PEGANpcPlanningTargetId(
                    PEGANpcPlanningTargetIdentityKind.InvestigationSample,
                    subjectActorId,
                    string.Empty,
                    contactLossObservationTimeSeconds,
                    sampleOrdinal)
                : default;

        internal static PEGANpcPlanningTargetId ForPerceivedAreaInvestigationSample(
            double presenceObservationTimeSeconds,
            int sampleOrdinal) =>
            IsNonNegativeFinite(presenceObservationTimeSeconds) &&
            sampleOrdinal >= 0
                ? new PEGANpcPlanningTargetId(
                    PEGANpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample,
                    default,
                    string.Empty,
                    sampleOrdinal: sampleOrdinal,
                    presenceObservationTimeSeconds: presenceObservationTimeSeconds)
                : default;

        internal static PEGANpcPlanningTargetId ForCharacterSeparationDestination(ActorId subjectActorId) =>
            subjectActorId.IsValid
                ? new PEGANpcPlanningTargetId(
                    PEGANpcPlanningTargetIdentityKind.CharacterSeparationDestination,
                    subjectActorId,
                    string.Empty)
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
                PEGANpcPlanningTargetIdentityKind.InvestigationSample =>
                    _actorId == other._actorId &&
                    _contactLossObservationTimeSeconds.Equals(other._contactLossObservationTimeSeconds) &&
                    _sampleOrdinal == other._sampleOrdinal,
                PEGANpcPlanningTargetIdentityKind.CharacterSeparationDestination => _actorId == other._actorId,
                PEGANpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample =>
                    _presenceObservationTimeSeconds.Equals(other._presenceObservationTimeSeconds) &&
                    _sampleOrdinal == other._sampleOrdinal,
                _ => false
            };
        }

        public override bool Equals(object obj) =>
            obj is PEGANpcPlanningTargetId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)IdentityKind * 397;
                if (IdentityKind == PEGANpcPlanningTargetIdentityKind.InvestigationSample ||
                    IdentityKind == PEGANpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample)
                {
                    bool isPerceivedAreaSample =
                        IdentityKind == PEGANpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample;
                    double evidenceTime = isPerceivedAreaSample
                        ? _presenceObservationTimeSeconds
                        : _contactLossObservationTimeSeconds;
                    long timeBits = BitConverter.DoubleToInt64Bits(evidenceTime);
                    if (!isPerceivedAreaSample)
                    {
                        hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_actorId.StableText ?? string.Empty);
                    }

                    hash = (hash * 397) ^ (int)timeBits;
                    hash = (hash * 397) ^ (int)(timeBits >> 32);
                    return (hash * 397) ^ _sampleOrdinal;
                }

                return hash ^ StringComparer.Ordinal.GetHashCode(StableOrderValue);
            }
        }

        public int CompareTo(PEGANpcPlanningTargetId other)
        {
            int kindOrder = ((int)IdentityKind).CompareTo((int)other.IdentityKind);
            if (kindOrder != 0)
            {
                return kindOrder;
            }

            if (IdentityKind == PEGANpcPlanningTargetIdentityKind.InvestigationSample)
            {
                int actorOrder = string.CompareOrdinal(_actorId.StableText, other._actorId.StableText);
                if (actorOrder != 0)
                {
                    return actorOrder;
                }

                int lossTimeOrder = _contactLossObservationTimeSeconds.CompareTo(
                    other._contactLossObservationTimeSeconds);
                return lossTimeOrder != 0 ? lossTimeOrder : _sampleOrdinal.CompareTo(other._sampleOrdinal);
            }

            if (IdentityKind == PEGANpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample)
            {
                int evidenceTimeOrder = _presenceObservationTimeSeconds.CompareTo(
                    other._presenceObservationTimeSeconds);
                return evidenceTimeOrder != 0 ? evidenceTimeOrder : _sampleOrdinal.CompareTo(other._sampleOrdinal);
            }

            return string.CompareOrdinal(StableOrderValue, other.StableOrderValue);
        }

        public override string ToString() => IsValid
            ? $"{IdentityKind}:{StableOrderValue}"
            : "InvalidTargetId";

        private static bool IsNonNegativeFinite(double value) =>
            value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);

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
            double evidenceTimeSeconds,
            float maximumRouteDistanceFromAnchorMeters = 0f)
        {
            TargetId = targetId;
            Kind = kind;
            Provenance = provenance;
            KnownPosition = knownPosition;
            HasEvidenceTime = hasEvidenceTime;
            EvidenceTimeSeconds = evidenceTimeSeconds;
            MaximumRouteDistanceFromAnchorMeters = maximumRouteDistanceFromAnchorMeters;
        }

        public PEGANpcPlanningTargetId TargetId { get; }

        public PEGANpcPlanningTargetKind Kind { get; }

        public PEGANpcPlanningTargetProvenance Provenance { get; }

        public Vector3 KnownPosition { get; }

        public bool HasEvidenceTime { get; }

        public double EvidenceTimeSeconds { get; }

        public float MaximumRouteDistanceFromAnchorMeters { get; }

        internal bool IsValid =>
            TargetId.IsValid &&
            Kind.IsValid &&
            Enum.IsDefined(typeof(PEGANpcPlanningTargetProvenance), Provenance) &&
            IsFinite(KnownPosition) &&
            IsNonNegativeFinite(MaximumRouteDistanceFromAnchorMeters) &&
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

        internal static PEGANpcPlanningTarget FromCharacterSeparationPosition(
            ActorId subjectActorId,
            Vector3 derivedPosition,
            double evidenceTimeSeconds) => new PEGANpcPlanningTarget(
                PEGANpcPlanningTargetId.ForCharacterSeparationDestination(subjectActorId),
                PEGANpcPlanningTargetKind.SafeSeparationPosition,
                PEGANpcPlanningTargetProvenance.DerivedFromKnowledge,
                derivedPosition,
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

        internal static PEGANpcPlanningTarget FromLocalObservationPosition(
            PEGANpcPlanningTargetId targetId,
            Vector3 knownPosition,
            double sourcePositionEvidenceTimeSeconds,
            float maximumRouteDistanceFromAnchorMeters) => new PEGANpcPlanningTarget(
                targetId,
                PEGANpcPlanningTargetKind.LocalObservationPosition,
                PEGANpcPlanningTargetProvenance.DerivedFromKnowledge,
                knownPosition,
                true,
                sourcePositionEvidenceTimeSeconds,
                maximumRouteDistanceFromAnchorMeters);

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

        public static PEGANpcNavigationRouteEstimate NotRequired => new PEGANpcNavigationRouteEstimate(
            PEGANpcPlanningRouteStatus.NotRequired,
            0f,
            0f,
            -1,
            0,
            "This Action does not require a navigation route.");
    }

    public readonly struct PEGANpcPlanningActionCandidate
    {
        internal PEGANpcPlanningActionCandidate(
            PEGANpcActionContract contract,
            IPEGANpcActionProvider action,
            PEGANpcPlanningTargetId targetId,
            PEGAActorMovementSpeedRegime speedRegime,
            float intrinsicDurationSeconds)
            : this(
                contract,
                action,
                targetId,
                speedRegime,
                PEGANpcNavigationRouteEstimate.NotRequired,
                intrinsicDurationSeconds)
        {
        }

        internal PEGANpcPlanningActionCandidate(
            PEGANpcActionContract contract,
            IPEGANpcActionProvider action,
            PEGANpcPlanningTargetId targetId,
            PEGAActorMovementSpeedRegime speedRegime,
            PEGANpcNavigationRouteEstimate routeEstimate,
            float intrinsicDurationSeconds)
        {
            Contract = contract;
            Action = action;
            TargetId = targetId;
            SpeedRegime = speedRegime;
            RouteEstimate = routeEstimate;
            IntrinsicDurationSeconds = intrinsicDurationSeconds;
        }

        public PEGANpcActionContract Contract { get; }

        internal IPEGANpcActionProvider Action { get; }

        public PEGANpcPlanningActionId ActionId => Contract.ActionId;

        public PEGANpcActionEffectKind PredictedEffect => Contract.EffectKind;

        public PEGANpcPlanningTargetId TargetId { get; }

        public PEGANpcPlanningTargetKind RequiredTargetKind => Contract.TargetKind;

        public PEGAActorMovementSpeedRegime SpeedRegime { get; }

        public PEGANpcNavigationRouteEstimate RouteEstimate { get; }

        public float IntrinsicDurationSeconds { get; }

        internal bool IsBoundToActionContract => Action == null ||
            (Action.Contract.IsValid &&
             Action.Contract.ActionId == Contract.ActionId &&
             Action.Contract.EffectKind == Contract.EffectKind &&
             Action.Contract.TargetKind == Contract.TargetKind &&
             Action.Contract.RequiresNavigation == Contract.RequiresNavigation);

        internal static PEGANpcPlanningActionCandidate DetachedFixture(
            PEGANpcPlanningActionId actionId,
            PEGANpcActionEffectKind predictedEffect,
            PEGANpcPlanningTarget target,
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
                new PEGANpcActionContract(
                    actionId,
                    predictedEffect,
                    target.Kind,
                    routeStatus != PEGANpcPlanningRouteStatus.NotRequired,
                    "Detached fixture Action."),
                null,
                target.TargetId,
                speedRegime,
                estimate,
                intrinsicDurationSeconds);
        }

        internal PEGANpcPlanningActionCandidate WithRouteEstimate(
            PEGANpcNavigationRouteEstimate routeEstimate) =>
            new PEGANpcPlanningActionCandidate(
                Contract,
                Action,
                TargetId,
                SpeedRegime,
                routeEstimate,
                IntrinsicDurationSeconds);
    }

    /// <summary>
    /// A bounded continuation of an already selected pursuit, derived from this NPC's preserved perception evidence.
    /// The existing Perception source remains the authority for the evasion timeout.
    /// </summary>
    public readonly struct PEGANpcPlanContinuation
    {
        internal PEGANpcPlanContinuation(
            string desireId,
            ActorId subjectActorId,
            double contactLossObservationTimeSeconds,
            Vector3 lastKnownPosition,
            double positionEvidenceTimeSeconds,
            bool lastKnownApproachAttempted,
            bool lastKnownPositionReached)
        {
            DesireId = desireId ?? string.Empty;
            SubjectActorId = subjectActorId;
            ContactLossObservationTimeSeconds = contactLossObservationTimeSeconds;
            LastKnownPosition = lastKnownPosition;
            PositionEvidenceTimeSeconds = positionEvidenceTimeSeconds;
            LastKnownApproachAttempted = lastKnownApproachAttempted || lastKnownPositionReached;
            LastKnownPositionReached = lastKnownPositionReached;
            ProgressCondition = new PEGANpcLastKnownTargetProgressCondition(
                subjectActorId,
                contactLossObservationTimeSeconds,
                positionEvidenceTimeSeconds,
                lastKnownPositionReached);
        }

        public string DesireId { get; }

        public ActorId SubjectActorId { get; }

        public double ContactLossObservationTimeSeconds { get; }

        public Vector3 LastKnownPosition { get; }

        public double PositionEvidenceTimeSeconds { get; }

        public bool LastKnownApproachAttempted { get; }

        public bool LastKnownPositionReached { get; }

        internal IGoapCondition ProgressCondition { get; }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(DesireId) &&
            SubjectActorId.IsValid &&
            IsNonNegativeFinite(ContactLossObservationTimeSeconds) &&
            IsFinite(LastKnownPosition) &&
            ProgressCondition != null &&
            IsNonNegativeFinite(PositionEvidenceTimeSeconds) &&
            PositionEvidenceTimeSeconds <= ContactLossObservationTimeSeconds;

        public PEGANpcPlanContinuation WithLastKnownApproachAttempted() =>
            LastKnownApproachAttempted
                ? this
                : new PEGANpcPlanContinuation(
                    DesireId,
                    SubjectActorId,
                    ContactLossObservationTimeSeconds,
                    LastKnownPosition,
                    PositionEvidenceTimeSeconds,
                    true,
                    LastKnownPositionReached);

        public PEGANpcPlanContinuation WithLastKnownPositionReached() =>
            LastKnownPositionReached
                ? this
                : new PEGANpcPlanContinuation(
                    DesireId,
                    SubjectActorId,
                    ContactLossObservationTimeSeconds,
                    LastKnownPosition,
                    PositionEvidenceTimeSeconds,
                    true,
                    true);

        internal PEGANpcLastKnownTargetContinuationIntent ToIntent() =>
            new PEGANpcLastKnownTargetContinuationIntent(
                SubjectActorId,
                LastKnownPosition,
                PositionEvidenceTimeSeconds,
                ContactLossObservationTimeSeconds);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(double value) =>
            value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);
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
            IReadOnlyList<PEGANpcPlanningActionCandidate> actionCandidates,
            PEGANpcPlanContinuation? planContinuation = null,
            IGoapKnowledgeStateView knowledgeState = null,
            int causalSearchCandidateEvaluationBudget = 128)
        {
            ValidationMode = validationMode;
            Context = context;
            _knownTargets = Copy(knownTargets);
            _actionCandidates = Copy(actionCandidates);
            PlanContinuation = planContinuation;
            CausalSearchCandidateEvaluationBudget = causalSearchCandidateEvaluationBudget;
            if (validationMode == PEGANpcPlanningValidationMode.LiveKnowledge &&
                knowledgeState != null &&
                knowledgeState.Source == GoapStateSource.KnowledgeState &&
                TryValidate(out _))
            {
                CausalKnowledge = new PEGANpcCausalPlanningKnowledge(
                    knowledgeState,
                    KnownTargets);
            }
        }

        public PEGANpcPlanningValidationMode ValidationMode { get; }

        public PEGANpcPlanContinuation? PlanContinuation { get; }

        public PEGANpcDesireContext Context { get; }

        /// <summary>
        /// Read-only causal-planning view for valid live inputs. Isolated validation fixtures intentionally
        /// have no runtime Knowledge State and therefore expose no causal live input.
        /// </summary>
        public PEGANpcCausalPlanningKnowledge CausalKnowledge { get; }

        public int CausalSearchCandidateEvaluationBudget { get; }

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

            if (PlanContinuation.HasValue &&
                (ValidationMode != PEGANpcPlanningValidationMode.LiveKnowledge ||
                 !PlanContinuation.Value.IsValid))
            {
                diagnostic = "A pursuit continuation requires a valid live Perception-backed planning input.";
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
                bool provenanceCompatible = ValidationMode == PEGANpcPlanningValidationMode.LiveKnowledge
                    ? HasLegitimateLiveTargetProvenance(target)
                    : target.Provenance == requiredProvenance;
                if (!target.IsValid || !provenanceCompatible)
                {
                    diagnostic = $"Target at index {index} is invalid or has provenance incompatible with {ValidationMode}.";
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
                if (!candidate.Contract.IsValid ||
                    !candidate.RequiredTargetKind.IsValid ||
                    (candidate.Contract.RequiresNavigation &&
                     !Enum.IsDefined(typeof(PEGAActorMovementSpeedRegime), candidate.SpeedRegime)) ||
                    !candidate.TargetId.IsValid ||
                    !candidate.IsBoundToActionContract ||
                    (ValidationMode == PEGANpcPlanningValidationMode.LiveKnowledge && candidate.Action == null))
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

        private static bool HasLegitimateLiveTargetProvenance(PEGANpcPlanningTarget target)
        {
            return target.HasEvidenceTime &&
                   (target.Provenance == PEGANpcPlanningTargetProvenance.KnowledgeState ||
                    target.Provenance == PEGANpcPlanningTargetProvenance.DerivedFromKnowledge);
        }
    }

    public readonly struct PEGANpcPlanSnapshot
    {
        internal PEGANpcPlanSnapshot(
            string desireId,
            string desireName,
            PEGANpcPlanningActionCandidate action,
            PEGANpcPlanningTarget target,
            IReadOnlyList<IGoapEffect> causalEffects,
            float estimatedSeconds,
            float effectiveDesireWeight,
            float costPreference,
            float score,
            PEGANpcPlanPurpose purpose)
        {
            DesireId = desireId ?? string.Empty;
            DesireName = desireName ?? string.Empty;
            ActionId = action.ActionId;
            PredictedEffect = action.PredictedEffect;
            CausalEffects = CopyCausalEffects(causalEffects);
            TargetId = target.TargetId;
            TargetKind = target.Kind;
            TargetProvenance = target.Provenance;
            TargetPosition = target.KnownPosition;
            EvidenceTimeSeconds = target.EvidenceTimeSeconds;
            MaximumRouteDistanceFromAnchorMeters = target.MaximumRouteDistanceFromAnchorMeters;
            RouteLengthMeters = action.RouteEstimate.RouteLengthMeters;
            EffectiveSpeedMetersPerSecond = action.RouteEstimate.EffectiveSpeedMetersPerSecond;
            IntrinsicDurationSeconds = action.IntrinsicDurationSeconds;
            EstimatedSeconds = estimatedSeconds;
            EffectiveDesireWeight = effectiveDesireWeight;
            CostPreference = costPreference;
            Score = score;
            SpeedRegime = action.SpeedRegime;
            Purpose = purpose;
        }

        public string DesireId { get; }

        public string DesireName { get; }

        public PEGANpcPlanningActionId ActionId { get; }

        public PEGANpcActionEffectKind PredictedEffect { get; }

        /// <summary>Declared GOAP Effects for this Action step; execution may confirm a subset only.</summary>
        public IReadOnlyList<IGoapEffect> CausalEffects { get; }

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

        public PEGANpcPlanPurpose Purpose { get; }

        public float MaximumRouteDistanceFromAnchorMeters { get; }

        private static IReadOnlyList<IGoapEffect> CopyCausalEffects(
            IReadOnlyList<IGoapEffect> causalEffects)
        {
            if (causalEffects == null)
            {
                throw new ArgumentNullException(nameof(causalEffects));
            }

            IGoapEffect[] copy = new IGoapEffect[causalEffects.Count];
            for (int index = 0; index < causalEffects.Count; index++)
            {
                copy[index] = causalEffects[index] ?? throw new ArgumentException(
                    "A plan snapshot cannot contain a null causal Effect.",
                    nameof(causalEffects));
            }

            return Array.AsReadOnly(copy);
        }
    }

    public sealed class PEGANpcDesirePlanEvaluation
    {
        private readonly IReadOnlyList<PEGANpcPlanSnapshot> _plans;

        internal PEGANpcDesirePlanEvaluation(
            string desireId,
            string desireName,
            bool applicable,
            float effectiveWeight,
            IReadOnlyList<PEGANpcPlanSnapshot> plans,
            string diagnostic)
        {
            DesireId = desireId ?? string.Empty;
            DesireName = desireName ?? string.Empty;
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
            string diagnostic,
            PEGANpcCausalPlanEvaluation causalEvaluation = null)
        {
            IsValid = isValid;
            ValidationMode = validationMode;
            Context = context;
            _desireEvaluations = Copy(desireEvaluations);
            _candidatePlans = Copy(candidatePlans);
            HasSelectedPlan = hasSelectedPlan;
            SelectedPlan = selectedPlan;
            Diagnostic = diagnostic ?? string.Empty;
            CausalEvaluation = causalEvaluation;
        }

        public bool IsValid { get; }

        public PEGANpcPlanningValidationMode ValidationMode { get; }

        public PEGANpcDesireContext Context { get; }

        public IReadOnlyList<PEGANpcDesirePlanEvaluation> DesireEvaluations => _desireEvaluations;

        public IReadOnlyList<PEGANpcPlanSnapshot> CandidatePlans => _candidatePlans;

        public bool HasSelectedPlan { get; }

        public PEGANpcPlanSnapshot SelectedPlan { get; }

        public string Diagnostic { get; }

        /// <summary>
        /// Causal alternatives considered by PEGA policy. Live plan formation is owned by the causal planner;
        /// this result retains the alternatives and search statuses used by the selection decision.
        /// </summary>
        public PEGANpcCausalPlanEvaluation CausalEvaluation { get; }

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
    /// A request to continue the Target pursuit at the last position confirmed by Perception.
    /// It is data for a planner/consumer; it does not select, start, cancel, or execute an Action.
    /// </summary>
    public readonly struct PEGANpcLastKnownTargetContinuationIntent
    {
        internal PEGANpcLastKnownTargetContinuationIntent(
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

        public PEGANpcPlanningActionId ActionId => PEGANpcPlanningActionId.ApproachKnownCharacter;

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

    /// <summary>Published when real Perception confirms loss of Target and a legitimate last position exists.</summary>
    public sealed class PEGANpcLastKnownTargetContinuationIntentRequestedEvent : IEvent
    {
        internal PEGANpcLastKnownTargetContinuationIntentRequestedEvent(
            PEGANpcLastKnownTargetContinuationIntent intent)
        {
            Intent = intent;
        }

        public PEGANpcLastKnownTargetContinuationIntent Intent { get; }
    }

    public enum PEGANpcPlanReevaluationTrigger
    {
        TargetReacquired = 10,
        TargetContactLostBeyondWindow = 20
    }

    /// <summary>
    /// Sinaliza uma mudança do alvo observada ou uma perda de contato que persistiu até o prazo configurado.
    /// Não seleciona, cancela, substitui nem executa planos.
    /// </summary>
    /// <remarks>
    /// TargetReacquired identifica planos dependentes do alvo; TargetContactLostBeyondWindow solicita avaliação do conjunto
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

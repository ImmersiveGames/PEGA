using System;
using System.Collections.Generic;
using System.Globalization;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.GOAP;
using PEGA.Movement.Actor;
using PEGA.NPC.Actions.Runtime;
using PEGA.NPC.Desires;
using UnityEngine;
namespace PEGA.NPC.Autonomy.Planning
{
    public enum PegaNpcPlanningValidationMode
    {
        LiveKnowledge = 0,
        IsolatedFixture = 10
    }

    public enum PegaNpcPlanningTargetProvenance
    {
        KnowledgeState = 0,
        DetachedFixture = 10,
        DerivedFromKnowledge = 20
    }

    public enum PegaNpcPlanningTargetIdentityKind
    {
        Actor = 0,
        StableDataId = 10,
        InvestigationSample = 20,
        CharacterSeparationDestination = 30,
        PerceivedAreaInvestigationSample = 40,
        GroupObjectEntryPosition = 50,
        IndividualObjectEntryPosition = 60
    }

    public enum PegaNpcPlanPurpose
    {
        DesireGoal = 0,
        PlanContinuationStep = 10
    }

    public enum PegaNpcPlanningRouteStatus
    {
        Complete = 0,
        Partial = 10,
        Invalid = 20,
        NotRequired = 30,
        Unknown = 40,
        Deferred = 50,
        Unreachable = 60
    }

    public readonly struct PegaNpcPlanningTargetId : IEquatable<PegaNpcPlanningTargetId>,
        IComparable<PegaNpcPlanningTargetId>
    {
        private readonly ActorId _actorId;
        private readonly string _stableDataId;
        private readonly double _contactLossObservationTimeSeconds;
        private readonly int _sampleOrdinal;
        private readonly long _perceivedAreaEpisodeId;
        private readonly ObjectEntryId _objectEntryId;
        private readonly GroupId _knowledgeGroupId;

        private PegaNpcPlanningTargetId(
            PegaNpcPlanningTargetIdentityKind identityKind,
            ActorId actorId,
            string stableDataId,
            double contactLossObservationTimeSeconds = 0d,
            int sampleOrdinal = 0,
            long perceivedAreaEpisodeId = 0,
            ObjectEntryId objectEntryId = default,
            GroupId knowledgeGroupId = default)
        {
            IdentityKind = identityKind;
            _actorId = actorId;
            _stableDataId = stableDataId ?? string.Empty;
            _contactLossObservationTimeSeconds = contactLossObservationTimeSeconds;
            _sampleOrdinal = sampleOrdinal;
            _perceivedAreaEpisodeId = perceivedAreaEpisodeId;
            _objectEntryId = objectEntryId;
            _knowledgeGroupId = knowledgeGroupId;
        }

        public PegaNpcPlanningTargetIdentityKind IdentityKind { get; }

        public bool IsValid => IdentityKind switch
        {
            PegaNpcPlanningTargetIdentityKind.Actor => _actorId.IsValid,
            PegaNpcPlanningTargetIdentityKind.StableDataId => !string.IsNullOrWhiteSpace(_stableDataId),
            PegaNpcPlanningTargetIdentityKind.InvestigationSample =>
                _actorId.IsValid &&
                IsNonNegativeFinite(_contactLossObservationTimeSeconds) &&
                _sampleOrdinal >= 0,
            PegaNpcPlanningTargetIdentityKind.CharacterSeparationDestination => _actorId.IsValid,
            PegaNpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample =>
                _perceivedAreaEpisodeId > 0 &&
                _sampleOrdinal >= 0,
            PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition =>
                _objectEntryId.IsValid && _knowledgeGroupId.IsValid,
            PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition =>
                _objectEntryId.IsValid && _actorId.IsValid,
            _ => false
        };

        internal string StableOrderValue => IdentityKind switch
        {
            PegaNpcPlanningTargetIdentityKind.Actor => _actorId.StableText ?? string.Empty,
            PegaNpcPlanningTargetIdentityKind.StableDataId => _stableDataId ?? string.Empty,
            PegaNpcPlanningTargetIdentityKind.InvestigationSample => string.Concat(
                _actorId.StableText ?? string.Empty,
                "|",
                _contactLossObservationTimeSeconds.ToString("R", CultureInfo.InvariantCulture),
                "|",
                _sampleOrdinal.ToString(CultureInfo.InvariantCulture)),
            PegaNpcPlanningTargetIdentityKind.CharacterSeparationDestination => _actorId.StableText ?? string.Empty,
            PegaNpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample => string.Concat(
                _perceivedAreaEpisodeId.ToString(CultureInfo.InvariantCulture),
                "|",
                _sampleOrdinal.ToString(CultureInfo.InvariantCulture)),
            PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition => string.Concat(
                _objectEntryId.StableText ?? string.Empty,
                "|",
                _knowledgeGroupId.StableText ?? string.Empty),
            PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition => string.Concat(
                _objectEntryId.StableText ?? string.Empty,
                "|",
                _actorId.StableText ?? string.Empty),
            _ => string.Empty
        };

        internal ActorId InvestigationSubjectActorId => _actorId;

        internal ActorId KnownActorId => _actorId;

        internal ActorId KnowledgeActorId => _actorId;

        internal ActorId CharacterSeparationSubjectActorId => _actorId;

        internal double InvestigationLossTimeSeconds => _contactLossObservationTimeSeconds;

        internal int InvestigationSampleOrdinal => _sampleOrdinal;

        internal long PerceivedAreaEpisodeId => _perceivedAreaEpisodeId;

        internal int PerceivedAreaSampleOrdinal => _sampleOrdinal;

        public ObjectEntryId ObjectEntryId => _objectEntryId;

        public GroupId KnowledgeGroupId => _knowledgeGroupId;

        internal static PegaNpcPlanningTargetId ForActor(ActorId actorId) =>
            actorId.IsValid
                ? new PegaNpcPlanningTargetId(PegaNpcPlanningTargetIdentityKind.Actor, actorId, string.Empty)
                : default;

        internal static PegaNpcPlanningTargetId ForStableDataId(string stableDataId) =>
            !string.IsNullOrWhiteSpace(stableDataId)
                ? new PegaNpcPlanningTargetId(PegaNpcPlanningTargetIdentityKind.StableDataId, default, stableDataId)
                : default;

        internal static PegaNpcPlanningTargetId ForInvestigationSample(
            ActorId subjectActorId,
            double contactLossObservationTimeSeconds,
            int sampleOrdinal) =>
            subjectActorId.IsValid &&
            IsNonNegativeFinite(contactLossObservationTimeSeconds) &&
            sampleOrdinal >= 0
                ? new PegaNpcPlanningTargetId(
                    PegaNpcPlanningTargetIdentityKind.InvestigationSample,
                    subjectActorId,
                    string.Empty,
                    contactLossObservationTimeSeconds,
                    sampleOrdinal)
                : default;

        internal static PegaNpcPlanningTargetId ForPerceivedAreaInvestigationSample(
            long episodeId,
            int sampleOrdinal) =>
            episodeId > 0 &&
            sampleOrdinal >= 0
                ? new PegaNpcPlanningTargetId(
                    PegaNpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample,
                    default,
                    string.Empty,
                    sampleOrdinal: sampleOrdinal,
                    perceivedAreaEpisodeId: episodeId)
                : default;

        internal static PegaNpcPlanningTargetId ForCharacterSeparationDestination(ActorId subjectActorId) =>
            subjectActorId.IsValid
                ? new PegaNpcPlanningTargetId(
                    PegaNpcPlanningTargetIdentityKind.CharacterSeparationDestination,
                    subjectActorId,
                    string.Empty)
                : default;

        internal static PegaNpcPlanningTargetId ForGroupObjectEntryPosition(
            ObjectEntryId objectEntryId,
            GroupId knowledgeGroupId) =>
            objectEntryId.IsValid && knowledgeGroupId.IsValid
                ? new PegaNpcPlanningTargetId(
                    PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition,
                    default,
                    string.Empty,
                    objectEntryId: objectEntryId,
                    knowledgeGroupId: knowledgeGroupId)
                : default;

        internal static PegaNpcPlanningTargetId ForIndividualObjectEntryPosition(
            ObjectEntryId objectEntryId,
            ActorId knowledgeActorId) =>
            objectEntryId.IsValid && knowledgeActorId.IsValid
                ? new PegaNpcPlanningTargetId(
                    PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition,
                    knowledgeActorId,
                    string.Empty,
                    objectEntryId: objectEntryId)
                : default;

        public bool Equals(PegaNpcPlanningTargetId other)
        {
            if (IdentityKind != other.IdentityKind)
            {
                return false;
            }

            return IdentityKind switch
            {
                PegaNpcPlanningTargetIdentityKind.Actor => _actorId == other._actorId,
                PegaNpcPlanningTargetIdentityKind.StableDataId =>
                    string.Equals(_stableDataId, other._stableDataId, StringComparison.Ordinal),
                PegaNpcPlanningTargetIdentityKind.InvestigationSample =>
                    _actorId == other._actorId &&
                    _contactLossObservationTimeSeconds.Equals(other._contactLossObservationTimeSeconds) &&
                    _sampleOrdinal == other._sampleOrdinal,
                PegaNpcPlanningTargetIdentityKind.CharacterSeparationDestination => _actorId == other._actorId,
                PegaNpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample =>
                    _perceivedAreaEpisodeId == other._perceivedAreaEpisodeId &&
                    _sampleOrdinal == other._sampleOrdinal,
                PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition =>
                    _objectEntryId == other._objectEntryId &&
                    _knowledgeGroupId == other._knowledgeGroupId,
                PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition =>
                    _objectEntryId == other._objectEntryId &&
                    _actorId == other._actorId,
                _ => false
            };
        }

        public override bool Equals(object obj) =>
            obj is PegaNpcPlanningTargetId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)IdentityKind * 397;
                if (IdentityKind == PegaNpcPlanningTargetIdentityKind.InvestigationSample ||
                    IdentityKind == PegaNpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample)
                {
                    bool isPerceivedAreaSample =
                        IdentityKind == PegaNpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample;
                    if (!isPerceivedAreaSample)
                    {
                        hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_actorId.StableText ?? string.Empty);
                        long timeBits = BitConverter.DoubleToInt64Bits(_contactLossObservationTimeSeconds);
                        hash = (hash * 397) ^ (int)timeBits;
                        hash = (hash * 397) ^ (int)(timeBits >> 32);
                    }
                    else
                    {
                        hash = (hash * 397) ^ _perceivedAreaEpisodeId.GetHashCode();
                    }

                    return (hash * 397) ^ _sampleOrdinal;
                }

                if (IdentityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition)
                {
                    hash = (hash * 397) ^ _objectEntryId.GetHashCode();
                    return (hash * 397) ^ _knowledgeGroupId.GetHashCode();
                }

                if (IdentityKind == PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition)
                {
                    hash = (hash * 397) ^ _objectEntryId.GetHashCode();
                    return (hash * 397) ^ _actorId.GetHashCode();
                }

                return hash ^ StringComparer.Ordinal.GetHashCode(StableOrderValue);
            }
        }

        public int CompareTo(PegaNpcPlanningTargetId other)
        {
            int kindOrder = ((int)IdentityKind).CompareTo((int)other.IdentityKind);
            if (kindOrder != 0)
            {
                return kindOrder;
            }

            if (IdentityKind == PegaNpcPlanningTargetIdentityKind.InvestigationSample)
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

            if (IdentityKind == PegaNpcPlanningTargetIdentityKind.PerceivedAreaInvestigationSample)
            {
                int opportunityOrder = _perceivedAreaEpisodeId.CompareTo(
                    other._perceivedAreaEpisodeId);
                return opportunityOrder != 0 ? opportunityOrder : _sampleOrdinal.CompareTo(other._sampleOrdinal);
            }

            if (IdentityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition)
            {
                int objectEntryOrder = string.CompareOrdinal(
                    _objectEntryId.StableText,
                    other._objectEntryId.StableText);
                return objectEntryOrder != 0
                    ? objectEntryOrder
                    : string.CompareOrdinal(_knowledgeGroupId.StableText, other._knowledgeGroupId.StableText);
            }

            if (IdentityKind == PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition)
            {
                int objectEntryOrder = string.CompareOrdinal(
                    _objectEntryId.StableText,
                    other._objectEntryId.StableText);
                return objectEntryOrder != 0
                    ? objectEntryOrder
                    : string.CompareOrdinal(_actorId.StableText, other._actorId.StableText);
            }

            return string.CompareOrdinal(StableOrderValue, other.StableOrderValue);
        }

        public override string ToString() => IsValid
            ? $"{IdentityKind}:{StableOrderValue}"
            : "InvalidTargetId";

        private static bool IsNonNegativeFinite(double value) =>
            value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);

        public static bool operator ==(PegaNpcPlanningTargetId left, PegaNpcPlanningTargetId right) =>
            left.Equals(right);

        public static bool operator !=(PegaNpcPlanningTargetId left, PegaNpcPlanningTargetId right) =>
            !left.Equals(right);
    }

    public readonly struct PegaNpcPlanningTarget
    {
        internal PegaNpcPlanningTarget(
            PegaNpcPlanningTargetId targetId,
            PegaNpcPlanningTargetKind kind,
            PegaNpcPlanningTargetProvenance provenance,
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

        public PegaNpcPlanningTargetId TargetId { get; }

        public PegaNpcPlanningTargetKind Kind { get; }

        public PegaNpcPlanningTargetProvenance Provenance { get; }

        public ObjectEntryId ObjectEntryId => TargetId.ObjectEntryId;

        public GroupId KnowledgeGroupId => TargetId.KnowledgeGroupId;

        public Vector3 KnownPosition { get; }

        public bool HasEvidenceTime { get; }

        public double EvidenceTimeSeconds { get; }

        public float MaximumRouteDistanceFromAnchorMeters { get; }

        internal bool IsValid =>
            TargetId.IsValid &&
            Kind.IsValid &&
            Enum.IsDefined(typeof(PegaNpcPlanningTargetProvenance), Provenance) &&
            IsFinite(KnownPosition) &&
            IsNonNegativeFinite(MaximumRouteDistanceFromAnchorMeters) &&
            (!HasEvidenceTime || IsNonNegativeFinite(EvidenceTimeSeconds));

        internal static PegaNpcPlanningTarget FromKnownActorPosition(
            ActorId actorId,
            Vector3 knownPosition,
            double evidenceTimeSeconds) => new PegaNpcPlanningTarget(
                PegaNpcPlanningTargetId.ForActor(actorId),
                PegaNpcPlanningTargetKind.CharacterPosition,
                PegaNpcPlanningTargetProvenance.KnowledgeState,
                knownPosition,
                true,
                evidenceTimeSeconds);

        internal static PegaNpcPlanningTarget FromCharacterSeparationPosition(
            ActorId subjectActorId,
            Vector3 derivedPosition,
            double evidenceTimeSeconds) => new PegaNpcPlanningTarget(
                PegaNpcPlanningTargetId.ForCharacterSeparationDestination(subjectActorId),
                PegaNpcPlanningTargetKind.SafeSeparationPosition,
                PegaNpcPlanningTargetProvenance.DerivedFromKnowledge,
                derivedPosition,
                true,
                evidenceTimeSeconds);

        internal static PegaNpcPlanningTarget FromKnownGroupObjectEntryPosition(
            ObjectEntryId objectEntryId,
            GroupId knowledgeGroupId,
            Vector3 knownPosition,
            double evidenceTimeSeconds) => new PegaNpcPlanningTarget(
                PegaNpcPlanningTargetId.ForGroupObjectEntryPosition(objectEntryId, knowledgeGroupId),
                PegaNpcPlanningTargetKind.GroupObjectEntryPosition,
                PegaNpcPlanningTargetProvenance.DerivedFromKnowledge,
                knownPosition,
                true,
                evidenceTimeSeconds);

        internal static PegaNpcPlanningTarget FromKnownIndividualObjectEntryPosition(
            ObjectEntryId objectEntryId,
            ActorId knowledgeActorId,
            Vector3 knownPosition,
            double evidenceTimeSeconds) => new PegaNpcPlanningTarget(
                PegaNpcPlanningTargetId.ForIndividualObjectEntryPosition(objectEntryId, knowledgeActorId),
                PegaNpcPlanningTargetKind.IndividualObjectEntryPosition,
                PegaNpcPlanningTargetProvenance.DerivedFromKnowledge,
                knownPosition,
                true,
                evidenceTimeSeconds);

        internal static PegaNpcPlanningTarget FromLocalObservationPosition(
            PegaNpcPlanningTargetId targetId,
            Vector3 knownPosition,
            double sourcePositionEvidenceTimeSeconds,
            float maximumRouteDistanceFromAnchorMeters) => new PegaNpcPlanningTarget(
                targetId,
                PegaNpcPlanningTargetKind.LocalObservationPosition,
                PegaNpcPlanningTargetProvenance.DerivedFromKnowledge,
                knownPosition,
                true,
                sourcePositionEvidenceTimeSeconds,
                maximumRouteDistanceFromAnchorMeters);

        internal static PegaNpcPlanningTarget DetachedFixture(
            string stableId,
            PegaNpcPlanningTargetKind kind,
            Vector3 knownPosition,
            double evidenceTimeSeconds = 0d) => new PegaNpcPlanningTarget(
                PegaNpcPlanningTargetId.ForStableDataId(stableId),
                kind,
                PegaNpcPlanningTargetProvenance.DetachedFixture,
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

    public readonly struct PegaNpcNavigationRouteEstimate
    {
        internal PegaNpcNavigationRouteEstimate(
            PegaNpcPlanningRouteStatus status,
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

        public PegaNpcPlanningRouteStatus Status { get; }

        public float RouteLengthMeters { get; }

        public float EffectiveSpeedMetersPerSecond { get; }

        public int AgentTypeId { get; }

        public int AreaMask { get; }

        public string Diagnostic { get; }

        public static PegaNpcNavigationRouteEstimate NotRequired => new PegaNpcNavigationRouteEstimate(
            PegaNpcPlanningRouteStatus.NotRequired,
            0f,
            0f,
            -1,
            0,
            "This Action does not require a navigation route.");
    }

    public readonly struct PegaNpcPlanningActionCandidate
    {
        internal PegaNpcPlanningActionCandidate(
            PegaNpcActionContract contract,
            IPegaNpcActionProvider action,
            PegaNpcPlanningTargetId targetId,
            PegaActorMovementSpeedRegime speedRegime,
            float intrinsicDurationSeconds,
            float? targetRadiusOverrideMeters = null)
            : this(
                contract,
                action,
                targetId,
                speedRegime,
                PegaNpcNavigationRouteEstimate.NotRequired,
                intrinsicDurationSeconds,
                targetRadiusOverrideMeters)
        {
        }

        internal PegaNpcPlanningActionCandidate(
            PegaNpcActionContract contract,
            IPegaNpcActionProvider action,
            PegaNpcPlanningTargetId targetId,
            PegaActorMovementSpeedRegime speedRegime,
            PegaNpcNavigationRouteEstimate routeEstimate,
            float intrinsicDurationSeconds,
            float? targetRadiusOverrideMeters = null)
        {
            Contract = contract;
            Action = action;
            TargetId = targetId;
            SpeedRegime = speedRegime;
            RouteEstimate = routeEstimate;
            IntrinsicDurationSeconds = intrinsicDurationSeconds;
            TargetRadiusOverrideMeters = targetRadiusOverrideMeters;
        }

        public PegaNpcActionContract Contract { get; }

        internal IPegaNpcActionProvider Action { get; }

        public PegaNpcPlanningActionId ActionId => Contract.ActionId;

        public PegaNpcActionEffectKind PredictedEffect => Contract.EffectKind;

        public PegaNpcPlanningTargetId TargetId { get; }

        public PegaNpcPlanningTargetKind RequiredTargetKind => Contract.TargetKind;

        public PegaActorMovementSpeedRegime SpeedRegime { get; }

        public PegaNpcNavigationRouteEstimate RouteEstimate { get; }

        public float IntrinsicDurationSeconds { get; }

        /// <summary>Optional per-target radius for stationary actions, supplied by authorized planning inputs.</summary>
        public float? TargetRadiusOverrideMeters { get; }

        internal bool IsBoundToActionContract =>
            (!TargetRadiusOverrideMeters.HasValue || !Contract.RequiresNavigation) &&
            (Action == null ||
             (Action.Contract.IsValid &&
              Action.Contract.ActionId == Contract.ActionId &&
              Action.Contract.EffectKind == Contract.EffectKind &&
              Action.Contract.TargetKind == Contract.TargetKind &&
              Action.Contract.RequiresNavigation == Contract.RequiresNavigation));

        internal static PegaNpcPlanningActionCandidate DetachedFixture(
            PegaNpcPlanningActionId actionId,
            PegaNpcActionEffectKind predictedEffect,
            PegaNpcPlanningTarget target,
            PegaActorMovementSpeedRegime speedRegime,
            PegaNpcPlanningRouteStatus routeStatus,
            float routeLengthMeters,
            float effectiveSpeedMetersPerSecond,
            float intrinsicDurationSeconds)
        {
            PegaNpcNavigationRouteEstimate estimate = new PegaNpcNavigationRouteEstimate(
                routeStatus,
                routeLengthMeters,
                effectiveSpeedMetersPerSecond,
                -1,
                0,
                "Detached validation fixture.");
            return new PegaNpcPlanningActionCandidate(
                new PegaNpcActionContract(
                    actionId,
                    predictedEffect,
                    target.Kind,
                    routeStatus != PegaNpcPlanningRouteStatus.NotRequired,
                    "Detached fixture Action."),
                null,
                target.TargetId,
                speedRegime,
                estimate,
                intrinsicDurationSeconds);
        }

        internal PegaNpcPlanningActionCandidate WithRouteEstimate(
            PegaNpcNavigationRouteEstimate routeEstimate) =>
            new PegaNpcPlanningActionCandidate(
                Contract,
                Action,
                TargetId,
                SpeedRegime,
                routeEstimate,
                IntrinsicDurationSeconds,
                TargetRadiusOverrideMeters);
    }

    /// <summary>
    /// A bounded continuation of an already selected pursuit, derived from this NPC's preserved perception evidence.
    /// The existing Perception source remains the authority for the evasion timeout.
    /// </summary>
    public readonly struct PegaNpcPlanContinuation
    {
        internal PegaNpcPlanContinuation(
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
            ProgressCondition = new PegaNpcLastKnownTargetProgressCondition(
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

        public PegaNpcPlanContinuation WithLastKnownApproachAttempted() =>
            LastKnownApproachAttempted
                ? this
                : new PegaNpcPlanContinuation(
                    DesireId,
                    SubjectActorId,
                    ContactLossObservationTimeSeconds,
                    LastKnownPosition,
                    PositionEvidenceTimeSeconds,
                    true,
                    LastKnownPositionReached);

        public PegaNpcPlanContinuation WithLastKnownPositionReached() =>
            LastKnownPositionReached
                ? this
                : new PegaNpcPlanContinuation(
                    DesireId,
                    SubjectActorId,
                    ContactLossObservationTimeSeconds,
                    LastKnownPosition,
                    PositionEvidenceTimeSeconds,
                    true,
                    true);

        internal PegaNpcLastKnownTargetContinuationIntent ToIntent() =>
            new PegaNpcLastKnownTargetContinuationIntent(
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

    public sealed class PegaNpcPlanningInput
    {
        private static readonly IReadOnlyList<PegaNpcPlanningTarget> EmptyTargets =
            Array.AsReadOnly(Array.Empty<PegaNpcPlanningTarget>());
        private static readonly IReadOnlyList<PegaNpcPlanningActionCandidate> EmptyCandidates =
            Array.AsReadOnly(Array.Empty<PegaNpcPlanningActionCandidate>());

        private readonly IReadOnlyList<PegaNpcPlanningTarget> _knownTargets;
        private readonly IReadOnlyList<PegaNpcPlanningActionCandidate> _actionCandidates;

        internal PegaNpcPlanningInput(
            PegaNpcPlanningValidationMode validationMode,
            PegaNpcDesireContext context,
            IReadOnlyList<PegaNpcPlanningTarget> knownTargets,
            IReadOnlyList<PegaNpcPlanningActionCandidate> actionCandidates,
            PegaNpcPlanContinuation? planContinuation = null,
            IGoapKnowledgeStateView knowledgeState = null,
            int causalSearchCandidateEvaluationBudget = 128,
            IReadOnlyList<IGoapEffect> observedCheckpoints = null)
        {
            ValidationMode = validationMode;
            Context = context;
            _knownTargets = Copy(knownTargets);
            _actionCandidates = Copy(actionCandidates);
            PlanContinuation = planContinuation;
            CausalSearchCandidateEvaluationBudget = causalSearchCandidateEvaluationBudget;
            if (validationMode == PegaNpcPlanningValidationMode.LiveKnowledge &&
                knowledgeState != null &&
                knowledgeState.Source == GoapStateSource.KnowledgeState &&
                TryValidate(out _))
            {
                CausalKnowledge = new PegaNpcCausalPlanningKnowledge(
                    knowledgeState,
                    KnownTargets,
                    observedCheckpoints);
            }
        }

        public PegaNpcPlanningValidationMode ValidationMode { get; }

        public PegaNpcPlanContinuation? PlanContinuation { get; }

        public PegaNpcDesireContext Context { get; }

        /// <summary>
        /// Read-only causal-planning view for valid live inputs. Isolated validation fixtures intentionally
        /// have no runtime Knowledge State and therefore expose no causal live input.
        /// </summary>
        public PegaNpcCausalPlanningKnowledge CausalKnowledge { get; }

        public int CausalSearchCandidateEvaluationBudget { get; }

        public IReadOnlyList<PegaNpcPlanningTarget> KnownTargets => _knownTargets ?? EmptyTargets;

        public IReadOnlyList<PegaNpcPlanningActionCandidate> ActionCandidates =>
            _actionCandidates ?? EmptyCandidates;

        internal bool TryValidate(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!Enum.IsDefined(typeof(PegaNpcPlanningValidationMode), ValidationMode) ||
                !Enum.IsDefined(typeof(PegaNpcDesireContext), Context))
            {
                diagnostic = "Planning mode or context has an unsupported value.";
                return false;
            }

            if (PlanContinuation.HasValue &&
                (ValidationMode != PegaNpcPlanningValidationMode.LiveKnowledge ||
                 !PlanContinuation.Value.IsValid))
            {
                diagnostic = "A pursuit continuation requires a valid live Perception-backed planning input.";
                return false;
            }

            HashSet<PegaNpcPlanningTargetId> targetIds = new HashSet<PegaNpcPlanningTargetId>();
            PegaNpcPlanningTargetProvenance requiredProvenance =
                ValidationMode == PegaNpcPlanningValidationMode.LiveKnowledge
                    ? PegaNpcPlanningTargetProvenance.KnowledgeState
                    : PegaNpcPlanningTargetProvenance.DetachedFixture;
            for (int index = 0; index < KnownTargets.Count; index++)
            {
                PegaNpcPlanningTarget target = KnownTargets[index];
                bool provenanceCompatible = ValidationMode == PegaNpcPlanningValidationMode.LiveKnowledge
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
                PegaNpcPlanningActionCandidate candidate = ActionCandidates[index];
                if (!candidate.Contract.IsValid ||
                    !candidate.RequiredTargetKind.IsValid ||
                    (candidate.Contract.RequiresNavigation &&
                     !Enum.IsDefined(typeof(PegaActorMovementSpeedRegime), candidate.SpeedRegime)) ||
                    !candidate.TargetId.IsValid ||
                    !candidate.IsBoundToActionContract ||
                    (ValidationMode == PegaNpcPlanningValidationMode.LiveKnowledge && candidate.Action == null))
                {
                    diagnostic = $"Action candidate at index {index} has an invalid or incompatible Action/Effect/Target contract.";
                    return false;
                }

                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    PegaNpcPlanningActionCandidate previous = ActionCandidates[previousIndex];
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
            PegaNpcPlanningTargetId targetId,
            out PegaNpcPlanningTarget target)
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

        private static bool HasLegitimateLiveTargetProvenance(PegaNpcPlanningTarget target)
        {
            return target.HasEvidenceTime &&
                   (target.Provenance == PegaNpcPlanningTargetProvenance.KnowledgeState ||
                    target.Provenance == PegaNpcPlanningTargetProvenance.DerivedFromKnowledge);
        }
    }

    public readonly struct PegaNpcPlanSnapshot
    {
        internal PegaNpcPlanSnapshot(
            string desireId,
            string desireName,
            PegaNpcPlanningActionCandidate action,
            PegaNpcPlanningTarget target,
            IReadOnlyList<IGoapEffect> causalEffects,
            float estimatedSeconds,
            float effectiveDesireWeight,
            float costPreference,
            float score,
            PegaNpcPlanPurpose purpose)
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
            TargetRadiusOverrideMeters = action.TargetRadiusOverrideMeters;
            EstimatedSeconds = estimatedSeconds;
            EffectiveDesireWeight = effectiveDesireWeight;
            CostPreference = costPreference;
            Score = score;
            SpeedRegime = action.SpeedRegime;
            Purpose = purpose;
        }

        public string DesireId { get; }

        public string DesireName { get; }

        public PegaNpcPlanningActionId ActionId { get; }

        public PegaNpcActionEffectKind PredictedEffect { get; }

        /// <summary>Declared GOAP Effects for this Action step; execution may confirm a subset only.</summary>
        public IReadOnlyList<IGoapEffect> CausalEffects { get; }

        public PegaNpcPlanningTargetId TargetId { get; }

        public PegaNpcPlanningTargetKind TargetKind { get; }

        public PegaNpcPlanningTargetProvenance TargetProvenance { get; }

        public Vector3 TargetPosition { get; }

        public double EvidenceTimeSeconds { get; }

        public float RouteLengthMeters { get; }

        public float EffectiveSpeedMetersPerSecond { get; }

        public float IntrinsicDurationSeconds { get; }

        public float? TargetRadiusOverrideMeters { get; }

        public float EstimatedSeconds { get; }

        public float EffectiveDesireWeight { get; }

        public float CostPreference { get; }

        public float Score { get; }

        public PegaActorMovementSpeedRegime SpeedRegime { get; }

        public PegaNpcPlanPurpose Purpose { get; }

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

    public sealed class PegaNpcDesirePlanEvaluation
    {
        private readonly IReadOnlyList<PegaNpcPlanSnapshot> _plans;

        internal PegaNpcDesirePlanEvaluation(
            string desireId,
            string desireName,
            bool applicable,
            float effectiveWeight,
            IReadOnlyList<PegaNpcPlanSnapshot> plans,
            string diagnostic)
        {
            DesireId = desireId ?? string.Empty;
            DesireName = desireName ?? string.Empty;
            IsApplicable = applicable;
            EffectiveWeight = effectiveWeight;
            PegaNpcPlanSnapshot[] copy = new PegaNpcPlanSnapshot[plans.Count];
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

        public IReadOnlyList<PegaNpcPlanSnapshot> Plans => _plans;

        public bool HasPlan => _plans.Count > 0;

        public string Diagnostic { get; }
    }

    public sealed class PegaNpcPlanSelectionResult
    {
        private readonly IReadOnlyList<PegaNpcDesirePlanEvaluation> _desireEvaluations;
        private readonly IReadOnlyList<PegaNpcPlanSnapshot> _candidatePlans;

        internal PegaNpcPlanSelectionResult(
            bool isValid,
            PegaNpcPlanningValidationMode validationMode,
            PegaNpcDesireContext context,
            IReadOnlyList<PegaNpcDesirePlanEvaluation> desireEvaluations,
            IReadOnlyList<PegaNpcPlanSnapshot> candidatePlans,
            bool hasSelectedPlan,
            PegaNpcPlanSnapshot selectedPlan,
            string diagnostic,
            PegaNpcCausalPlanEvaluation causalEvaluation = null)
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

        public PegaNpcPlanningValidationMode ValidationMode { get; }

        public PegaNpcDesireContext Context { get; }

        public IReadOnlyList<PegaNpcDesirePlanEvaluation> DesireEvaluations => _desireEvaluations;

        public IReadOnlyList<PegaNpcPlanSnapshot> CandidatePlans => _candidatePlans;

        public bool HasSelectedPlan { get; }

        public PegaNpcPlanSnapshot SelectedPlan { get; }

        public string Diagnostic { get; }

        /// <summary>
        /// Causal alternatives considered by PEGA policy. Live plan formation is owned by the causal planner;
        /// this result retains the alternatives and search statuses used by the selection decision.
        /// </summary>
        public PegaNpcCausalPlanEvaluation CausalEvaluation { get; }

        internal PegaNpcPlanSelectionResult WithSelectedPlan(PegaNpcPlanSnapshot selectedPlan) =>
            new PegaNpcPlanSelectionResult(
                IsValid,
                ValidationMode,
                Context,
                _desireEvaluations,
                _candidatePlans,
                true,
                selectedPlan,
                Diagnostic,
                CausalEvaluation);

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
    public readonly struct PegaNpcLastKnownTargetContinuationIntent
    {
        internal PegaNpcLastKnownTargetContinuationIntent(
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

        public PegaNpcPlanningActionId ActionId => PegaNpcPlanningActionId.ApproachKnownCharacter;

        public PegaNpcPlanningTargetKind TargetKind => PegaNpcPlanningTargetKind.CharacterPosition;

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
    public sealed class PegaNpcLastKnownTargetContinuationIntentRequestedEvent : IEvent
    {
        internal PegaNpcLastKnownTargetContinuationIntentRequestedEvent(
            PegaNpcLastKnownTargetContinuationIntent intent)
        {
            Intent = intent;
        }

        public PegaNpcLastKnownTargetContinuationIntent Intent { get; }
    }

    public enum PegaNpcPlanReevaluationTrigger
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
    public sealed class PegaNpcPlanReevaluationRequestedEvent : IEvent
    {
        internal PegaNpcPlanReevaluationRequestedEvent(
            PegaNpcPlanReevaluationTrigger trigger,
            ActorId targetActorId,
            double observationTimeSeconds,
            double requestedAtTimeSeconds)
        {
            Trigger = trigger;
            TargetActorId = targetActorId;
            ObservationTimeSeconds = observationTimeSeconds;
            RequestedAtTimeSeconds = requestedAtTimeSeconds;
        }

        public PegaNpcPlanReevaluationTrigger Trigger { get; }

        public ActorId TargetActorId { get; }

        /// <summary>
        /// Tempo da evidência que motivou o pedido: observação consolidada da readquisição ou amostra de perda.
        /// </summary>
        public double ObservationTimeSeconds { get; }

        /// <summary>Momento de jogo em que o pedido foi emitido.</summary>
        public double RequestedAtTimeSeconds { get; }
    }
}

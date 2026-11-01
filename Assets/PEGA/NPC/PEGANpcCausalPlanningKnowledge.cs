using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using ImmersiveGames.Goap;
using PEGA.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// Read-only PEGA knowledge contract composed from this Actor's Individual state and its currently authorized
    /// Group facts. It preserves source scope and provenance and never copies Group facts into Individual state.
    /// </summary>
    public sealed class PEGANpcKnowledgeReadView
    {
        private readonly GameObject _actorRoot;
        private readonly PEGANpcKnowledgeState _individualKnowledge;
        private readonly PEGAActorGroupMembership _groupMembership;

        internal PEGANpcKnowledgeReadView(
            GameObject actorRoot,
            PEGANpcKnowledgeState individualKnowledge,
            PEGAActorGroupMembership groupMembership)
        {
            _actorRoot = actorRoot ?? throw new ArgumentNullException(nameof(actorRoot));
            _individualKnowledge = individualKnowledge ??
                throw new ArgumentNullException(nameof(individualKnowledge));
            _groupMembership = groupMembership;
        }

        public PEGAKnowledgeFact<ActorProfileId> GetObservedCharacterIdentity(ActorId subjectActorId)
        {
            PEGAKnowledgeFact<ActorProfileId> individual =
                _individualKnowledge.GetObservedCharacterIdentity(subjectActorId);
            if (individual.Status != PEGAKnowledgeStatus.Unknown ||
                !TryGetAuthorizedGroupActorFacts(subjectActorId, out IReadOnlyList<GroupActorFacts> candidates))
            {
                return individual;
            }

            GroupActorFacts selected = default;
            List<PEGAKnowledgeEvidence> selectedEvidence = new List<PEGAKnowledgeEvidence>();
            bool found = false;
            for (int index = 0; index < candidates.Count; index++)
            {
                GroupActorFacts candidate = candidates[index];
                if (!candidate.Identity.TryGetValue(out ScriptableObject identityAsset) ||
                    !(identityAsset is ActorProfile authoredProfile) ||
                    !authoredProfile.TryGetActorProfileId(out ActorProfileId authoredId, out _) ||
                    !candidate.Profile.TryGetActorProfileId(out ActorProfileId correlatedId, out _) ||
                    authoredId != correlatedId)
                {
                    continue;
                }

                if (!found || candidate.Identity.ObservationTimeSeconds > selected.Identity.ObservationTimeSeconds)
                {
                    selected = candidate;
                    selectedEvidence = new List<PEGAKnowledgeEvidence>(candidate.Identity.Evidence);
                    found = true;
                }
                else if (candidate.Identity.ObservationTimeSeconds == selected.Identity.ObservationTimeSeconds)
                {
                    AddEvidence(selectedEvidence, candidate.Identity.Evidence);
                }
            }

            if (!found || !selected.Profile.TryGetActorProfileId(out ActorProfileId profileId, out _))
            {
                return individual;
            }

            selected.State.LogFactConsumed(
                _actorRoot,
                PEGAActorGroupKnowledgeFacts.EntityIdentity.For(selected.Profile),
                "PEGANpcKnowledgeReadView.GetObservedCharacterIdentity");
            return PEGAKnowledgeFact<ActorProfileId>.Create(
                PEGAKnowledgeStatus.Known,
                selected.Identity.HasObservationTime,
                selected.Identity.ObservationTimeSeconds,
                selectedEvidence,
                selected.Identity.InvalidationCauses,
                profileId,
                profileId,
                true,
                PEGAKnowledgeScope.Group);
        }

        public PEGAKnowledgeFact<Vector3> GetKnownCharacterPosition(ActorId subjectActorId)
        {
            PEGAKnowledgeFact<Vector3> individual =
                _individualKnowledge.GetLastObservedCharacterPosition(subjectActorId);
            if (individual.Status == PEGAKnowledgeStatus.Invalidated ||
                !TryGetAuthorizedGroupActorFacts(subjectActorId, out IReadOnlyList<GroupActorFacts> candidates))
            {
                return individual;
            }

            GroupActorFacts selected = default;
            List<PEGAKnowledgeEvidence> selectedEvidence = new List<PEGAKnowledgeEvidence>();
            Vector3 selectedPosition = default;
            bool found = false;
            bool conflicting = false;
            for (int index = 0; index < candidates.Count; index++)
            {
                GroupActorFacts candidate = candidates[index];
                if (!candidate.Identity.TryGetValue(out _) ||
                    !candidate.Location.TryGetValue(out Vector3 position) ||
                    !(candidate.LocationSubject is PEGAActorProfileLocationSubjectEvidence captured) ||
                    !candidate.Source.TryGetCurrentLocationSubjectEvidence(candidate.Profile,
                        subjectActorId, out IPEGAActorGroupLocationSubjectEvidence current, out _) ||
                    !(current is PEGAActorProfileLocationSubjectEvidence currentActor) ||
                    !captured.Equals(currentActor))
                {
                    continue;
                }

                if (!found || candidate.Location.ObservationTimeSeconds > selected.Location.ObservationTimeSeconds)
                {
                    selected = candidate;
                    selectedPosition = position;
                    selectedEvidence = new List<PEGAKnowledgeEvidence>(candidate.Location.Evidence);
                    found = true;
                    conflicting = false;
                }
                else if (candidate.Location.ObservationTimeSeconds == selected.Location.ObservationTimeSeconds)
                {
                    AddEvidence(selectedEvidence, candidate.Location.Evidence);
                    if (!position.Equals(selectedPosition))
                    {
                        conflicting = true;
                    }
                }
            }

            if (!found)
            {
                return individual;
            }

            if (individual.TryGetValue(out _) &&
                individual.ObservationTimeSeconds >= selected.Location.ObservationTimeSeconds)
            {
                return individual;
            }

            if (conflicting)
            {
                return PEGAKnowledgeFact<Vector3>.Create(
                    PEGAKnowledgeStatus.Invalidated,
                    true,
                    selected.Location.ObservationTimeSeconds,
                    selectedEvidence,
                    PEGAKnowledgeInvalidationCause.ConflictingEvidence,
                    default,
                    default,
                    false,
                    PEGAKnowledgeScope.Group);
            }

            selected.State.LogFactConsumed(
                _actorRoot,
                PEGAActorGroupKnowledgeFacts.EntityInitialLocation.For(selected.Profile),
                "PEGANpcKnowledgeReadView.GetKnownCharacterPosition");
            return PEGAKnowledgeFact<Vector3>.Create(
                PEGAKnowledgeStatus.Known,
                selected.Location.HasObservationTime,
                selected.Location.ObservationTimeSeconds,
                selectedEvidence,
                selected.Location.InvalidationCauses,
                selectedPosition,
                selectedPosition,
                true,
                PEGAKnowledgeScope.Group);
        }

        /// <summary>Reads prior Group facts for any designer-authored entity identity asset.</summary>
        public bool TryGetKnownGroupEntityFacts(
            ScriptableObject entityIdentity,
            out PEGAKnowledgeFact<ScriptableObject> identityFact,
            out PEGAKnowledgeFact<Vector3> initialLocationFact,
            out IPEGAActorGroupLocationSubjectEvidence locationSubjectEvidence,
            out string diagnostic)
        {
            identityFact = PEGAKnowledgeFact<ScriptableObject>.Unknown(
                PEGAKnowledgeScope.Group);
            initialLocationFact = PEGAKnowledgeFact<Vector3>.Unknown(
                PEGAKnowledgeScope.Group);
            locationSubjectEvidence = null;
            diagnostic = "This Actor has no active Group membership component.";
            if (_groupMembership == null ||
                !_groupMembership.TryGetAuthorizedKnowledgeStates(
                    out IReadOnlyList<PEGAActorGroupKnowledgeState> states, out diagnostic))
            {
                return false;
            }

            bool locationConflict = false;
            List<PEGAKnowledgeEvidence> selectedIdentityEvidence = new List<PEGAKnowledgeEvidence>();
            List<PEGAKnowledgeEvidence> selectedLocationEvidence = new List<PEGAKnowledgeEvidence>();
            for (int index = 0; index < states.Count; index++)
            {
                if (!states[index].TryGetKnownEntityFacts(_actorRoot, entityIdentity,
                        out PEGAKnowledgeFact<ScriptableObject> groupIdentity,
                        out PEGAKnowledgeFact<Vector3> groupLocation,
                        out IPEGAActorGroupLocationSubjectEvidence subjectEvidence,
                        out diagnostic))
                {
                    return false;
                }

                if (groupIdentity.HasValue &&
                    (!identityFact.HasValue || groupIdentity.ObservationTimeSeconds > identityFact.ObservationTimeSeconds))
                {
                    identityFact = groupIdentity;
                    selectedIdentityEvidence = new List<PEGAKnowledgeEvidence>(groupIdentity.Evidence);
                }
                else if (groupIdentity.HasValue && identityFact.HasValue &&
                         groupIdentity.ObservationTimeSeconds == identityFact.ObservationTimeSeconds)
                {
                    AddEvidence(selectedIdentityEvidence, groupIdentity.Evidence);
                }

                if (groupLocation.HasValue &&
                    (!initialLocationFact.HasValue ||
                     groupLocation.ObservationTimeSeconds > initialLocationFact.ObservationTimeSeconds))
                {
                    initialLocationFact = groupLocation;
                    selectedLocationEvidence = new List<PEGAKnowledgeEvidence>(groupLocation.Evidence);
                    locationSubjectEvidence = subjectEvidence;
                    locationConflict = false;
                }
                else if (groupLocation.HasValue && initialLocationFact.HasValue &&
                         groupLocation.ObservationTimeSeconds == initialLocationFact.ObservationTimeSeconds)
                {
                    AddEvidence(selectedLocationEvidence, groupLocation.Evidence);
                    if (groupLocation.TryGetValue(out Vector3 otherPosition) &&
                        initialLocationFact.TryGetValue(out Vector3 selectedPosition) &&
                        !otherPosition.Equals(selectedPosition))
                    {
                        locationConflict = true;
                    }
                }
            }

            if (identityFact.HasValue)
            {
                identityFact = PEGAKnowledgeFact<ScriptableObject>.Create(
                    PEGAKnowledgeStatus.Known,
                    identityFact.HasObservationTime,
                    identityFact.ObservationTimeSeconds,
                    selectedIdentityEvidence,
                    identityFact.InvalidationCauses,
                    identityFact.TryGetValue(out ScriptableObject identityValue) ? identityValue : default,
                    identityFact.TryGetValue(out ScriptableObject identityCandidate) ? identityCandidate : default,
                    identityFact.HasCandidate,
                    PEGAKnowledgeScope.Group);
            }

            if (initialLocationFact.HasValue)
            {
                initialLocationFact = PEGAKnowledgeFact<Vector3>.Create(
                    PEGAKnowledgeStatus.Known,
                    initialLocationFact.HasObservationTime,
                    initialLocationFact.ObservationTimeSeconds,
                    selectedLocationEvidence,
                    initialLocationFact.InvalidationCauses,
                    initialLocationFact.TryGetValue(out Vector3 locationValue) ? locationValue : default,
                    initialLocationFact.TryGetValue(out Vector3 locationCandidate) ? locationCandidate : default,
                    initialLocationFact.HasCandidate,
                    PEGAKnowledgeScope.Group);
            }

            if (locationConflict)
            {
                initialLocationFact = PEGAKnowledgeFact<Vector3>.Create(
                    PEGAKnowledgeStatus.Invalidated, true,
                    initialLocationFact.ObservationTimeSeconds,
                    selectedLocationEvidence,
                    PEGAKnowledgeInvalidationCause.ConflictingEvidence,
                    default,
                    default,
                    false,
                    PEGAKnowledgeScope.Group);
                locationSubjectEvidence = null;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static void AddEvidence(
            List<PEGAKnowledgeEvidence> target,
            IReadOnlyList<PEGAKnowledgeEvidence> additions)
        {
            for (int index = 0; index < additions.Count; index++)
            {
                PEGAKnowledgeEvidence candidate = additions[index];
                bool alreadyPresent = false;
                for (int existingIndex = 0; existingIndex < target.Count; existingIndex++)
                {
                    PEGAKnowledgeEvidence existing = target[existingIndex];
                    if (existing.Origin.Equals(candidate.Origin) &&
                        ReferenceEquals(existing.Source, candidate.Source) &&
                        Equals(existing.Payload, candidate.Payload) &&
                        existing.ObservationTimeSeconds == candidate.ObservationTimeSeconds)
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (!alreadyPresent)
                {
                    target.Add(candidate);
                }
            }
        }

        public bool TryGetKnownGroupEntityFacts(
            GroupId groupId,
            ScriptableObject entityIdentity,
            out PEGAKnowledgeFact<ScriptableObject> identityFact,
            out PEGAKnowledgeFact<Vector3> initialLocationFact,
            out IPEGAActorGroupLocationSubjectEvidence locationSubjectEvidence,
            out string diagnostic)
        {
            identityFact = PEGAKnowledgeFact<ScriptableObject>.Unknown(PEGAKnowledgeScope.Group);
            initialLocationFact = PEGAKnowledgeFact<Vector3>.Unknown(PEGAKnowledgeScope.Group);
            locationSubjectEvidence = null;
            diagnostic = "This Actor has no active Group membership component.";
            if (_groupMembership == null || !_groupMembership.TryGetAuthorizedKnowledgeStates(
                    out IReadOnlyList<PEGAActorGroupKnowledgeState> states, out diagnostic))
            {
                return false;
            }

            for (int index = 0; index < states.Count; index++)
            {
                if (states[index].GroupProfile != null &&
                    states[index].GroupProfile.TryGetGroupId(out GroupId candidateId, out _) &&
                    candidateId == groupId)
                {
                    return states[index].TryGetKnownEntityFacts(_actorRoot, entityIdentity,
                        out identityFact, out initialLocationFact, out locationSubjectEvidence,
                        out diagnostic);
                }
            }

            diagnostic = "This Actor has no current authorized knowledge access to the requested GroupId.";
            return false;
        }

        private bool TryGetAuthorizedGroupActorFacts(
            ActorId subjectActorId,
            out IReadOnlyList<GroupActorFacts> facts)
        {
            facts = Array.Empty<GroupActorFacts>();
            if (_groupMembership == null || !_groupMembership.TryGetAuthorizedKnowledgeStates(
                    out IReadOnlyList<PEGAActorGroupKnowledgeState> states, out _))
            {
                return false;
            }

            var matches = new List<GroupActorFacts>();
            for (int index = 0; index < states.Count; index++)
            {
                PEGAActorGroupKnowledgeState state = states[index];
                PEGAActorProfileKnowledgeSource source =
                    state.FindAuthorizedKnowledgeSource<PEGAActorProfileKnowledgeSource>();
                if (source == null || !source.TryGetCurrentActorProfile(subjectActorId,
                        out ActorProfile profile, out _, out _) ||
                    !state.TryGetKnownEntityFacts(_actorRoot, profile,
                        out PEGAKnowledgeFact<ScriptableObject> identity,
                        out PEGAKnowledgeFact<Vector3> location,
                        out IPEGAActorGroupLocationSubjectEvidence subjectEvidence, out _))
                {
                    continue;
                }

                matches.Add(new GroupActorFacts(state, source, profile, identity, location, subjectEvidence));
            }

            facts = matches;
            return matches.Count > 0;
        }

        private readonly struct GroupActorFacts
        {
            internal GroupActorFacts(PEGAActorGroupKnowledgeState state,
                PEGAActorProfileKnowledgeSource source, ActorProfile profile,
                PEGAKnowledgeFact<ScriptableObject> identity,
                PEGAKnowledgeFact<Vector3> location,
                IPEGAActorGroupLocationSubjectEvidence locationSubject)
            {
                State = state;
                Source = source;
                Profile = profile;
                Identity = identity;
                Location = location;
                LocationSubject = locationSubject;
            }

            internal PEGAActorGroupKnowledgeState State { get; }
            internal PEGAActorProfileKnowledgeSource Source { get; }
            internal ActorProfile Profile { get; }
            internal PEGAKnowledgeFact<ScriptableObject> Identity { get; }
            internal PEGAKnowledgeFact<Vector3> Location { get; }
            internal IPEGAActorGroupLocationSubjectEvidence LocationSubject { get; }
        }
    }

    /// <summary>
    /// Read-only GOAP view over one NPC's existing Knowledge State. Facts retain their status and evidence
    /// metadata so conditions can distinguish unknown, invalidated, and known values.
    /// </summary>
    public sealed class PEGANpcGoapKnowledgeStateView : IGoapKnowledgeStateView
    {
        private readonly PEGANpcKnowledgeState _knowledgeState;
        private readonly IReadOnlyList<IGoapKnowledgeCapability> _capabilities;

        internal PEGANpcGoapKnowledgeStateView(
            PEGANpcKnowledgeState knowledgeState,
            IReadOnlyList<IGoapKnowledgeCapability> capabilities)
        {
            _knowledgeState = knowledgeState ?? throw new ArgumentNullException(nameof(knowledgeState));
            if (capabilities == null)
            {
                throw new ArgumentNullException(nameof(capabilities));
            }

            List<IGoapKnowledgeCapability> copy = new List<IGoapKnowledgeCapability>(capabilities.Count);
            bool includesKnowledgeState = false;
            for (int index = 0; index < capabilities.Count; index++)
            {
                IGoapKnowledgeCapability capability = capabilities[index] ??
                    throw new ArgumentException("Knowledge capabilities cannot contain null entries.", nameof(capabilities));
                if (object.ReferenceEquals(capability, knowledgeState))
                {
                    includesKnowledgeState = true;
                }

                copy.Add(capability);
            }

            if (!includesKnowledgeState)
            {
                copy.Add(knowledgeState);
            }

            _capabilities = Array.AsReadOnly(copy.ToArray());
        }

        public GoapStateSource Source => GoapStateSource.KnowledgeState;

        public IReadOnlyList<IGoapKnowledgeCapability> Capabilities => _capabilities;

        public PEGAKnowledgeFact<PEGANpcPerceivedAreaPresence> GetPerceivedAreaPresence() =>
            _knowledgeState.GetPerceivedAreaPresence();

        public PEGAKnowledgeFact<ActorProfileId> GetObservedCharacterIdentity(ActorId subjectActorId) =>
            _knowledgeState.GetObservedCharacterIdentity(subjectActorId);

        public PEGAKnowledgeFact<bool> GetCharacterVisualContact(ActorId subjectActorId) =>
            _knowledgeState.GetCharacterVisualContact(subjectActorId);

        public PEGAKnowledgeFact<Vector3> GetLastObservedCharacterPosition(ActorId subjectActorId) =>
            _knowledgeState.GetLastObservedCharacterPosition(subjectActorId);
    }

    /// <summary>A GOAP Target handle backed by an immutable, already-known PEGA planning Target.</summary>
    public sealed class PEGANpcGoapKnownTarget : IGoapKnownTarget
    {
        internal PEGANpcGoapKnownTarget(PEGANpcPlanningTarget target)
        {
            Target = target;
        }

        public PEGANpcPlanningTarget Target { get; }
    }

    /// <summary>Generic predicted arrival at a known planning Target, independent of any Desire or Goal.</summary>
    public sealed class PEGANpcTargetReachedEffect : IGoapEffect
    {
        public PEGANpcTargetReachedEffect(
            PEGANpcPlanningTargetId targetId,
            PEGANpcPlanningTargetKind targetKind,
            double evidenceTimeSeconds)
        {
            if (!targetId.IsValid || !targetKind.IsValid ||
                double.IsNaN(evidenceTimeSeconds) || double.IsInfinity(evidenceTimeSeconds) || evidenceTimeSeconds < 0d)
            {
                throw new ArgumentException(
                    "A valid planning Target identity, kind and evidence time are required.",
                    nameof(targetId));
            }

            TargetId = targetId;
            TargetKind = targetKind;
            EvidenceTimeSeconds = evidenceTimeSeconds;
        }

        public PEGANpcPlanningTargetId TargetId { get; }

        public PEGANpcPlanningTargetKind TargetKind { get; }

        public double EvidenceTimeSeconds { get; }
    }

    /// <summary>Temporary progress condition owned by the established last-known-target continuation.</summary>
    internal sealed class PEGANpcLastKnownTargetProgressCondition : IGoapCondition
    {
        private readonly ActorId _subjectActorId;
        private readonly double _contactLossObservationTimeSeconds;
        private readonly double _positionEvidenceTimeSeconds;
        private readonly bool _lastKnownPositionReached;

        public PEGANpcLastKnownTargetProgressCondition(
            ActorId subjectActorId,
            double contactLossObservationTimeSeconds,
            double positionEvidenceTimeSeconds,
            bool lastKnownPositionReached)
        {
            _subjectActorId = subjectActorId;
            _contactLossObservationTimeSeconds = contactLossObservationTimeSeconds;
            _positionEvidenceTimeSeconds = positionEvidenceTimeSeconds;
            _lastKnownPositionReached = lastKnownPositionReached;
        }

        public GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!(state is IGoapEffectStateView effectState))
            {
                return GoapTruthValue.Unknown;
            }

            for (int index = 0; index < effectState.Effects.Count; index++)
            {
                if (!(effectState.Effects[index] is PEGANpcTargetReachedEffect effect) ||
                    Math.Abs(effect.EvidenceTimeSeconds - _positionEvidenceTimeSeconds) > 0.000001d)
                {
                    continue;
                }

                bool reachedLastKnownPosition =
                    effect.TargetKind == PEGANpcPlanningTargetKind.CharacterPosition &&
                    effect.TargetId == PEGANpcPlanningTargetId.ForActor(_subjectActorId);
                bool reachedInvestigationPosition =
                    effect.TargetKind == PEGANpcPlanningTargetKind.LocalObservationPosition &&
                    effect.TargetId.IdentityKind == PEGANpcPlanningTargetIdentityKind.InvestigationSample &&
                    effect.TargetId.InvestigationSubjectActorId == _subjectActorId &&
                    Math.Abs(effect.TargetId.InvestigationLossTimeSeconds -
                             _contactLossObservationTimeSeconds) <= 0.000001d;
                if ((!_lastKnownPositionReached && reachedLastKnownPosition) ||
                    (_lastKnownPositionReached && reachedInvestigationPosition))
                {
                    return GoapTruthValue.True;
                }
            }

            return GoapTruthValue.Unknown;
        }
    }

    /// <summary>
    /// Causal-planning inputs adapted from a validated live NPC input. It references the NPC's existing
    /// Knowledge State and wraps its known Targets; it does not copy or discover world facts.
    /// </summary>
    public sealed class PEGANpcCausalPlanningKnowledge
    {
        private readonly IReadOnlyList<PEGANpcGoapKnownTarget> _knownTargets;

        internal PEGANpcCausalPlanningKnowledge(
            IGoapKnowledgeStateView knowledgeState,
            IReadOnlyList<PEGANpcPlanningTarget> knownTargets)
        {
            if (knowledgeState == null || knowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                throw new ArgumentException(
                    "Causal planning requires the Actor's explicit Knowledge State view.",
                    nameof(knowledgeState));
            }

            if (knownTargets == null)
            {
                throw new ArgumentNullException(nameof(knownTargets));
            }

            KnowledgeState = knowledgeState;
            InitialState = new GoapHypotheticalState(KnowledgeState);

            PEGANpcGoapKnownTarget[] targets = new PEGANpcGoapKnownTarget[knownTargets.Count];
            for (int index = 0; index < knownTargets.Count; index++)
            {
                PEGANpcPlanningTarget target = knownTargets[index];
                if (!target.IsValid || !target.HasEvidenceTime ||
                    (target.Provenance != PEGANpcPlanningTargetProvenance.KnowledgeState &&
                     target.Provenance != PEGANpcPlanningTargetProvenance.DerivedFromKnowledge))
                {
                    throw new ArgumentException(
                        $"Live causal Target at index {index} is invalid or lacks legitimate knowledge evidence.",
                        nameof(knownTargets));
                }

                targets[index] = new PEGANpcGoapKnownTarget(target);
            }

            _knownTargets = Array.AsReadOnly(targets);
        }

        public IGoapKnowledgeStateView KnowledgeState { get; }

        public GoapHypotheticalState InitialState { get; }

        public IReadOnlyList<PEGANpcGoapKnownTarget> KnownTargets => _knownTargets;

        public bool TryGetKnownTarget(
            PEGANpcPlanningTargetId targetId,
            out PEGANpcGoapKnownTarget knownTarget)
        {
            for (int index = 0; index < _knownTargets.Count; index++)
            {
                PEGANpcGoapKnownTarget candidate = _knownTargets[index];
                if (candidate.Target.TargetId == targetId)
                {
                    knownTarget = candidate;
                    return true;
                }
            }

            knownTarget = null;
            return false;
        }

        /// <summary>
        /// Returns only known Targets whose declared kind matches the requested Action Target kind.
        /// Action-specific conditions remain the responsibility of that Action's planning contract.
        /// </summary>
        public IReadOnlyList<PEGANpcGoapKnownTarget> GetCompatibleTargets(
            PEGANpcPlanningTargetKind requiredTargetKind)
        {
            if (!requiredTargetKind.IsValid)
            {
                throw new ArgumentException("A valid Target kind is required.", nameof(requiredTargetKind));
            }

            List<PEGANpcGoapKnownTarget> compatibleTargets = new List<PEGANpcGoapKnownTarget>();
            for (int index = 0; index < _knownTargets.Count; index++)
            {
                PEGANpcGoapKnownTarget target = _knownTargets[index];
                if (target.Target.Kind == requiredTargetKind)
                {
                    compatibleTargets.Add(target);
                }
            }

            return compatibleTargets.AsReadOnly();
        }
    }
}

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

        public PEGANpcKnowledgeFact<ActorProfileId> GetObservedCharacterIdentity(ActorId subjectActorId)
        {
            PEGANpcKnowledgeFact<ActorProfileId> individual =
                _individualKnowledge.GetObservedCharacterIdentity(subjectActorId);
            if (individual.Status != PEGANpcKnowledgeStatus.Unknown ||
                !TryGetAuthorizedGroupActorFacts(subjectActorId, out IReadOnlyList<GroupActorFacts> candidates))
            {
                return individual;
            }

            GroupActorFacts selected = default;
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
                    found = true;
                }
            }

            if (!found || !selected.Profile.TryGetActorProfileId(out ActorProfileId profileId, out _))
            {
                return individual;
            }

            selected.State.LogEntityFactConsumed(
                _actorRoot,
                selected.Profile,
                PEGAActorGroupKnowledgeFactKind.EntityIdentity,
                "PEGANpcKnowledgeReadView.GetObservedCharacterIdentity");
            return new PEGANpcKnowledgeFact<ActorProfileId>(
                PEGANpcKnowledgeStatus.Known,
                selected.Identity.HasObservationTime,
                selected.Identity.ObservationTimeSeconds,
                PEGANpcEvidenceOrigin.GroupPriorIdentity,
                PEGANpcKnowledgeInvalidationCause.None,
                profileId,
                PEGANpcKnowledgeScope.Group);
        }

        public PEGANpcKnowledgeFact<Vector3> GetKnownCharacterPosition(ActorId subjectActorId)
        {
            PEGANpcKnowledgeFact<Vector3> individual =
                _individualKnowledge.GetLastObservedCharacterPosition(subjectActorId);
            if (individual.Status == PEGANpcKnowledgeStatus.Invalidated ||
                !TryGetAuthorizedGroupActorFacts(subjectActorId, out IReadOnlyList<GroupActorFacts> candidates))
            {
                return individual;
            }

            GroupActorFacts selected = default;
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
                    found = true;
                    conflicting = false;
                }
                else if (candidate.Location.ObservationTimeSeconds == selected.Location.ObservationTimeSeconds &&
                         !position.Equals(selectedPosition))
                {
                    conflicting = true;
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
                return new PEGANpcKnowledgeFact<Vector3>(
                    PEGANpcKnowledgeStatus.Invalidated,
                    true,
                    selected.Location.ObservationTimeSeconds,
                    PEGANpcEvidenceOrigin.AuthorizedInitialLocation,
                    PEGANpcKnowledgeInvalidationCause.ConflictingEvidence,
                    default,
                    PEGANpcKnowledgeScope.Group);
            }

            selected.State.LogEntityFactConsumed(
                _actorRoot,
                selected.Profile,
                PEGAActorGroupKnowledgeFactKind.EntityInitialLocation,
                "PEGANpcKnowledgeReadView.GetKnownCharacterPosition");
            return new PEGANpcKnowledgeFact<Vector3>(
                PEGANpcKnowledgeStatus.Known,
                selected.Location.HasObservationTime,
                selected.Location.ObservationTimeSeconds,
                PEGANpcEvidenceOrigin.AuthorizedInitialLocation,
                PEGANpcKnowledgeInvalidationCause.None,
                selectedPosition,
                PEGANpcKnowledgeScope.Group);
        }

        /// <summary>Reads prior Group facts for any designer-authored entity identity asset.</summary>
        public bool TryGetKnownGroupEntityFacts(
            ScriptableObject entityIdentity,
            out PEGANpcKnowledgeFact<ScriptableObject> identityFact,
            out PEGANpcKnowledgeFact<Vector3> initialLocationFact,
            out IPEGAActorGroupLocationSubjectEvidence locationSubjectEvidence,
            out string diagnostic)
        {
            identityFact = PEGANpcKnowledgeFact<ScriptableObject>.UnknownForScope(
                PEGANpcKnowledgeScope.Group);
            initialLocationFact = PEGANpcKnowledgeFact<Vector3>.UnknownForScope(
                PEGANpcKnowledgeScope.Group);
            locationSubjectEvidence = null;
            diagnostic = "This Actor has no active Group membership component.";
            if (_groupMembership == null ||
                !_groupMembership.TryGetAuthorizedKnowledgeStates(
                    out IReadOnlyList<PEGAActorGroupKnowledgeState> states, out diagnostic))
            {
                return false;
            }

            bool locationConflict = false;
            for (int index = 0; index < states.Count; index++)
            {
                if (!states[index].TryGetKnownEntityFacts(_actorRoot, entityIdentity,
                        out PEGANpcKnowledgeFact<ScriptableObject> groupIdentity,
                        out PEGANpcKnowledgeFact<Vector3> groupLocation,
                        out IPEGAActorGroupLocationSubjectEvidence subjectEvidence,
                        out diagnostic))
                {
                    return false;
                }

                if (groupIdentity.HasValue &&
                    (!identityFact.HasValue || groupIdentity.ObservationTimeSeconds > identityFact.ObservationTimeSeconds))
                {
                    identityFact = groupIdentity;
                }

                if (groupLocation.HasValue &&
                    (!initialLocationFact.HasValue ||
                     groupLocation.ObservationTimeSeconds > initialLocationFact.ObservationTimeSeconds))
                {
                    initialLocationFact = groupLocation;
                    locationSubjectEvidence = subjectEvidence;
                    locationConflict = false;
                }
                else if (groupLocation.HasValue && initialLocationFact.HasValue &&
                         groupLocation.ObservationTimeSeconds == initialLocationFact.ObservationTimeSeconds &&
                         groupLocation.TryGetValue(out Vector3 otherPosition) &&
                         initialLocationFact.TryGetValue(out Vector3 selectedPosition) &&
                         !otherPosition.Equals(selectedPosition))
                {
                    locationConflict = true;
                }
            }

            if (locationConflict)
            {
                initialLocationFact = new PEGANpcKnowledgeFact<Vector3>(
                    PEGANpcKnowledgeStatus.Invalidated, true,
                    initialLocationFact.ObservationTimeSeconds,
                    PEGANpcEvidenceOrigin.AuthorizedInitialLocation,
                    PEGANpcKnowledgeInvalidationCause.ConflictingEvidence,
                    default,
                    PEGANpcKnowledgeScope.Group);
                locationSubjectEvidence = null;
            }

            diagnostic = string.Empty;
            return true;
        }

        public bool TryGetKnownGroupEntityFacts(
            GroupId groupId,
            ScriptableObject entityIdentity,
            out PEGANpcKnowledgeFact<ScriptableObject> identityFact,
            out PEGANpcKnowledgeFact<Vector3> initialLocationFact,
            out IPEGAActorGroupLocationSubjectEvidence locationSubjectEvidence,
            out string diagnostic)
        {
            identityFact = PEGANpcKnowledgeFact<ScriptableObject>.UnknownForScope(PEGANpcKnowledgeScope.Group);
            initialLocationFact = PEGANpcKnowledgeFact<Vector3>.UnknownForScope(PEGANpcKnowledgeScope.Group);
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
                        out PEGANpcKnowledgeFact<ScriptableObject> identity,
                        out PEGANpcKnowledgeFact<Vector3> location,
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
                PEGANpcKnowledgeFact<ScriptableObject> identity,
                PEGANpcKnowledgeFact<Vector3> location,
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
            internal PEGANpcKnowledgeFact<ScriptableObject> Identity { get; }
            internal PEGANpcKnowledgeFact<Vector3> Location { get; }
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

        public PEGANpcKnowledgeFact<PEGANpcPerceivedAreaPresence> GetPerceivedAreaPresence() =>
            _knowledgeState.GetPerceivedAreaPresence();

        public PEGANpcKnowledgeFact<ActorProfileId> GetObservedCharacterIdentity(ActorId subjectActorId) =>
            _knowledgeState.GetObservedCharacterIdentity(subjectActorId);

        public PEGANpcKnowledgeFact<bool> GetCharacterVisualContact(ActorId subjectActorId) =>
            _knowledgeState.GetCharacterVisualContact(subjectActorId);

        public PEGANpcKnowledgeFact<Vector3> GetLastObservedCharacterPosition(ActorId subjectActorId) =>
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

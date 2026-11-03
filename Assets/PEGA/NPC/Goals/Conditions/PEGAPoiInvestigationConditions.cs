using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using ImmersiveGames.Goap;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>Effect published only after the investigation fact was accepted and read back from Group Knowledge.</summary>
    public sealed class PEGAPoiInvestigationCompletedEffect : IGoapEffect
    {
        public PEGAPoiInvestigationCompletedEffect(ObjectEntryId objectEntryId, GroupId groupId)
        {
            if (!objectEntryId.IsValid || !groupId.IsValid)
            {
                throw new ArgumentException("Investigation completion requires a logical ObjectEntryId and GroupId.");
            }

            ObjectEntryId = objectEntryId;
            GroupId = groupId;
        }

        public ObjectEntryId ObjectEntryId { get; }

        public GroupId GroupId { get; }
    }

    internal sealed class PEGAPoiInvestigationCanStartCondition : IGoapCondition
    {
        private readonly ObjectEntryId _objectEntryId;
        private readonly GroupId _groupId;
        private readonly Vector3 _knownPosition;
        private readonly double _positionEvidenceTimeSeconds;

        public PEGAPoiInvestigationCanStartCondition(
            ObjectEntryId objectEntryId,
            GroupId groupId,
            Vector3 knownPosition,
            double positionEvidenceTimeSeconds)
        {
            _objectEntryId = objectEntryId;
            _groupId = groupId;
            _knownPosition = knownPosition;
            _positionEvidenceTimeSeconds = positionEvidenceTimeSeconds;
        }

        public GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (!PEGAPoiGroupKnowledge.TryGetStateView(state, out IGoapKnowledgeStateView knowledgeState) ||
                !PEGAPoiGroupKnowledge.TryGetReader(knowledgeState, out PEGAActorGroupKnowledgeReader reader) ||
                !IsCurrentlyPending(reader, Time.timeAsDouble) ||
                !(state is IGoapEffectStateView))
            {
                return GoapTruthValue.Unknown;
            }

            PEGANpcTargetReachedEffect latestArrival = null;
            if (state is IGoapHypotheticalStateView hypothetical)
            {
                latestArrival = FindLatestArrival(hypothetical.ProjectedEffects);
                if (latestArrival == null)
                {
                    latestArrival = FindLatestArrival(hypothetical.ObservedEffects);
                }
            }
            else if (state is IGoapObservedStateView observed)
            {
                latestArrival = FindLatestArrival(observed.ObservedEffects);
            }

            // A checkpoint proves arrival only until a later projected arrival establishes a different position.
            return latestArrival != null &&
                   latestArrival.TargetId.IdentityKind == PEGANpcPlanningTargetIdentityKind.GroupObjectEntryPosition &&
                   latestArrival.TargetId.ObjectEntryId == _objectEntryId &&
                   latestArrival.KnowledgeGroupId == _groupId &&
                   latestArrival.TargetPosition.Equals(_knownPosition) &&
                   Math.Abs(latestArrival.EvidenceTimeSeconds - _positionEvidenceTimeSeconds) <= 0.000001d
                ? GoapTruthValue.True
                : GoapTruthValue.Unknown;
        }

        private static PEGANpcTargetReachedEffect FindLatestArrival(IReadOnlyList<IGoapEffect> effects)
        {
            for (int index = effects.Count - 1; index >= 0; index--)
            {
                if (effects[index] is PEGANpcTargetReachedEffect reached)
                {
                    return reached;
                }
            }

            return null;
        }

        private bool IsCurrentlyPending(PEGAActorGroupKnowledgeReader reader, double queryTimeSeconds)
        {
            PEGAActorGroupKnowledgeReadResult<bool> required = reader.ReadGroupFact(
                _groupId,
                PEGAObjectEntryKnowledgeFacts.InvestigationRequired.For(_objectEntryId),
                queryTimeSeconds);
            PEGAActorGroupKnowledgeReadResult<bool> completed = reader.ReadGroupFact(
                _groupId,
                PEGAObjectEntryKnowledgeFacts.InvestigationCompleted.For(_objectEntryId),
                queryTimeSeconds);
            PEGAActorGroupKnowledgeReadResult<Vector3> position = reader.ReadGroupFact(
                _groupId,
                PEGAObjectEntryKnowledgeFacts.Position.For(_objectEntryId),
                queryTimeSeconds);
            return required.IsAuthorized && required.Fact.Scope == PEGAKnowledgeScope.Group &&
                   required.Fact.TryGetValue(out bool isRequired) && isRequired &&
                   completed.IsAuthorized && completed.Fact.Scope == PEGAKnowledgeScope.Group &&
                   completed.Fact.TryGetValue(out bool isCompleted) && !isCompleted &&
                   position.IsAuthorized && position.Fact.Scope == PEGAKnowledgeScope.Group &&
                   position.Fact.TryGetValue(out Vector3 currentPosition) && currentPosition.Equals(_knownPosition) &&
                   position.Fact.HasObservationTime &&
                   Math.Abs(position.Fact.ObservationTimeSeconds - _positionEvidenceTimeSeconds) <= 0.000001d;
        }
    }

    internal static class PEGAPoiGroupKnowledge
    {
        public static bool TryGetReader(
            IGoapKnowledgeStateView knowledgeState,
            out PEGAActorGroupKnowledgeReader reader)
        {
            reader = null;
            if (knowledgeState == null || knowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                return false;
            }

            for (int index = 0; index < knowledgeState.Capabilities.Count; index++)
            {
                if (!(knowledgeState.Capabilities[index] is PEGAActorGroupKnowledgeReader candidate))
                {
                    continue;
                }

                if (reader != null && reader != candidate)
                {
                    reader = null;
                    return false;
                }

                reader = candidate;
            }

            return reader != null;
        }

        public static bool TryGetStateView(IGoapStateView state, out IGoapKnowledgeStateView knowledgeState)
        {
            knowledgeState = state as IGoapKnowledgeStateView;
            if (knowledgeState == null && state is IGoapHypotheticalStateView hypothetical)
            {
                knowledgeState = hypothetical.KnowledgeState;
            }

            if (knowledgeState == null && state is IGoapObservedStateView observed)
            {
                knowledgeState = observed.KnowledgeState;
            }

            return knowledgeState != null && knowledgeState.Source == GoapStateSource.KnowledgeState;
        }
    }

    public readonly struct PEGANpcPoiInvestigationEvidence : IEquatable<PEGANpcPoiInvestigationEvidence>
    {
        public PEGANpcPoiInvestigationEvidence(
            ActorId actorId,
            GroupId groupId,
            ObjectEntryId objectEntryId,
            long executionId)
        {
            ActorId = actorId;
            GroupId = groupId;
            ObjectEntryId = objectEntryId;
            ExecutionId = executionId;
        }

        public ActorId ActorId { get; }
        public GroupId GroupId { get; }
        public ObjectEntryId ObjectEntryId { get; }
        public long ExecutionId { get; }

        public bool IsValid => ActorId.IsValid && GroupId.IsValid && ObjectEntryId.IsValid && ExecutionId > 0;

        public bool Equals(PEGANpcPoiInvestigationEvidence other) =>
            ActorId == other.ActorId && GroupId == other.GroupId &&
            ObjectEntryId == other.ObjectEntryId && ExecutionId == other.ExecutionId;

        public override bool Equals(object obj) => obj is PEGANpcPoiInvestigationEvidence other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ActorId.GetHashCode();
                hash = (hash * 397) ^ GroupId.GetHashCode();
                hash = (hash * 397) ^ ObjectEntryId.GetHashCode();
                return (hash * 397) ^ ExecutionId.GetHashCode();
            }
        }
    }
}

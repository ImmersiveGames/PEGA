using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.GOAP;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Group;
using UnityEngine;
namespace PEGA.NPC.Goals.Conditions
{
    /// <summary>Effect published only after the investigation fact was accepted and read back from Group Knowledge.</summary>
    public sealed class PegaPoiInvestigationCompletedEffect : IGoapEffect, IGoapEffectSatisfaction
    {
        public PegaPoiInvestigationCompletedEffect(ObjectEntryId objectEntryId, GroupId groupId)
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

        public GoapTruthValue EvaluateSatisfaction(IGoapStateView state)
        {
            if (state is IGoapHypotheticalStateView hypothetical)
            {
                return IsPresent(hypothetical.ObservedEffects) || IsPresent(hypothetical.ProjectedEffects)
                    ? GoapTruthValue.True
                    : GoapTruthValue.False;
            }

            if (state is IGoapEffectStateView effectState)
            {
                return IsPresent(effectState.Effects)
                    ? GoapTruthValue.True
                    : GoapTruthValue.False;
            }

            return GoapTruthValue.Unknown;
        }

        private bool IsPresent(IReadOnlyList<IGoapEffect> effects)
        {
            for (int index = 0; index < effects.Count; index++)
            {
                if (effects[index] is PegaPoiInvestigationCompletedEffect completed &&
                    completed.ObjectEntryId == ObjectEntryId &&
                    completed.GroupId == GroupId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class PegaPoiInvestigationCanStartCondition : IGoapCondition
    {
        private readonly ObjectEntryId _objectEntryId;
        private readonly GroupId _groupId;
        private readonly Vector3 _knownPosition;
        private readonly double _positionEvidenceTimeSeconds;

        public PegaPoiInvestigationCanStartCondition(
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
            if (!PegaPoiGroupKnowledge.TryGetStateView(state, out IGoapKnowledgeStateView knowledgeState) ||
                !PegaPoiGroupKnowledge.TryGetReader(knowledgeState, out PegaActorGroupKnowledgeReader reader) ||
                !IsCurrentlyPending(reader, Time.timeAsDouble) ||
                !(state is IGoapEffectStateView))
            {
                return GoapTruthValue.Unknown;
            }

            PegaNpcTargetReachedEffect latestArrival = null;
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
                   latestArrival.TargetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition &&
                   latestArrival.TargetId.ObjectEntryId == _objectEntryId &&
                   latestArrival.KnowledgeGroupId == _groupId &&
                   latestArrival.TargetPosition.Equals(_knownPosition) &&
                   Math.Abs(latestArrival.EvidenceTimeSeconds - _positionEvidenceTimeSeconds) <= 0.000001d
                ? GoapTruthValue.True
                : GoapTruthValue.Unknown;
        }

        private static PegaNpcTargetReachedEffect FindLatestArrival(IReadOnlyList<IGoapEffect> effects)
        {
            for (int index = effects.Count - 1; index >= 0; index--)
            {
                if (effects[index] is PegaNpcTargetReachedEffect reached)
                {
                    return reached;
                }
            }

            return null;
        }

        private bool IsCurrentlyPending(PegaActorGroupKnowledgeReader reader, double queryTimeSeconds)
        {
            PegaActorGroupKnowledgeReadResult<bool> required = reader.ReadGroupFact(
                _groupId,
                PegaObjectEntryKnowledgeFacts.InvestigationRequired.For(_objectEntryId),
                queryTimeSeconds);
            PegaActorGroupKnowledgeReadResult<bool> completed = reader.ReadGroupFact(
                _groupId,
                PegaObjectEntryKnowledgeFacts.InvestigationCompleted.For(_objectEntryId),
                queryTimeSeconds);
            PegaActorGroupKnowledgeReadResult<Vector3> position = reader.ReadGroupFact(
                _groupId,
                PegaObjectEntryKnowledgeFacts.Position.For(_objectEntryId),
                queryTimeSeconds);
            return required.IsAuthorized && required.Fact.Scope == PegaKnowledgeScope.Group &&
                   required.Fact.TryGetValue(out bool isRequired) && isRequired &&
                   completed.IsAuthorized && completed.Fact.Scope == PegaKnowledgeScope.Group &&
                   completed.Fact.TryGetValue(out bool isCompleted) && !isCompleted &&
                   position.IsAuthorized && position.Fact.Scope == PegaKnowledgeScope.Group &&
                   position.Fact.TryGetValue(out Vector3 currentPosition) && currentPosition.Equals(_knownPosition) &&
                   position.Fact.HasObservationTime &&
                   Math.Abs(position.Fact.ObservationTimeSeconds - _positionEvidenceTimeSeconds) <= 0.000001d;
        }
    }

    internal static class PegaPoiGroupKnowledge
    {
        public static bool TryGetReader(
            IGoapKnowledgeStateView knowledgeState,
            out PegaActorGroupKnowledgeReader reader)
        {
            reader = null;
            if (knowledgeState == null || knowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                return false;
            }

            for (int index = 0; index < knowledgeState.Capabilities.Count; index++)
            {
                if (!(knowledgeState.Capabilities[index] is PegaActorGroupKnowledgeReader candidate))
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

    public readonly struct PegaNpcPoiInvestigationEvidence : IEquatable<PegaNpcPoiInvestigationEvidence>
    {
        public PegaNpcPoiInvestigationEvidence(
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

        public bool Equals(PegaNpcPoiInvestigationEvidence other) =>
            ActorId == other.ActorId && GroupId == other.GroupId &&
            ObjectEntryId == other.ObjectEntryId && ExecutionId == other.ExecutionId;

        public override bool Equals(object obj) => obj is PegaNpcPoiInvestigationEvidence other && Equals(other);

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

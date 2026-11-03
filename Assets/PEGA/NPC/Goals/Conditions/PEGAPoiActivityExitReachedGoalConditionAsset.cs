using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using ImmersiveGames.Goap;
using UnityEngine;

namespace PEGA.NPC
{
    [CreateAssetMenu(
        fileName = "PoiActivityExitReachedGoalCondition",
        menuName = "PEGA/NPC/GOAP/POI Activity Exit Reached Goal Condition")]
    public sealed class PEGAPoiActivityExitReachedGoalConditionAsset : PEGANpcGoapConditionAsset
    {
        [SerializeField]
        [Tooltip("Group Profile whose authorized Activity Exit fact and position define this Goal.")]
        private GroupProfile activityGroup;

        public override GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (!(state is IGoapEffectStateView) ||
                !PEGAPoiGroupKnowledge.TryGetStateView(state, out IGoapKnowledgeStateView knowledgeState) ||
                !PEGAPoiGroupKnowledge.TryGetReader(knowledgeState, out PEGAActorGroupKnowledgeReader reader) ||
                !TryGetGroupId(out GroupId groupId))
            {
                return GoapTruthValue.Unknown;
            }

            IReadOnlyList<IGoapEffect> effects;
            if (state is IGoapHypotheticalStateView hypothetical)
            {
                effects = hypothetical.ProjectedEffects;
            }
            else if (state is IGoapObservedStateView observed)
            {
                effects = observed.ObservedEffects;
            }
            else
            {
                return GoapTruthValue.Unknown;
            }

            for (int index = 0; index < effects.Count; index++)
            {
                if (!(effects[index] is PEGANpcTargetReachedEffect reached) ||
                    reached.TargetId.IdentityKind != PEGANpcPlanningTargetIdentityKind.GroupObjectEntryPosition ||
                    reached.KnowledgeGroupId != groupId || !reached.HasTargetPosition)
                {
                    continue;
                }

                PEGAActorGroupKnowledgeReadResult<bool> isExit = reader.ReadGroupFact(
                    groupId,
                    PEGAObjectEntryKnowledgeFacts.ActivityExit.For(reached.ObjectEntryId),
                    Time.timeAsDouble);
                PEGAActorGroupKnowledgeReadResult<Vector3> position = reader.ReadGroupFact(
                    groupId,
                    PEGAObjectEntryKnowledgeFacts.Position.For(reached.ObjectEntryId),
                    Time.timeAsDouble);
                if (isExit.IsAuthorized && isExit.Fact.Scope == PEGAKnowledgeScope.Group &&
                    isExit.Fact.TryGetValue(out bool exit) && exit &&
                    position.IsAuthorized && position.Fact.Scope == PEGAKnowledgeScope.Group &&
                    position.Fact.TryGetValue(out Vector3 knownPosition) &&
                    knownPosition.Equals(reached.TargetPosition) &&
                    position.Fact.HasObservationTime &&
                    Math.Abs(position.Fact.ObservationTimeSeconds - reached.EvidenceTimeSeconds) <= 0.000001d)
                {
                    return GoapTruthValue.True;
                }
            }

            return GoapTruthValue.Unknown;
        }

        public override bool TryValidate(out string diagnostic)
        {
            bool valid = TryGetGroupId(out _);
            diagnostic = valid
                ? string.Empty
                : "The POI activity exit Goal requires a valid Group Profile.";
            return valid;
        }

        private bool TryGetGroupId(out GroupId groupId)
        {
            groupId = default;
            return activityGroup != null &&
                   activityGroup.TryGetGroupId(out groupId, out _) &&
                   groupId.IsValid;
        }
    }
}

using System;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using ImmersiveGames.Goap;
using UnityEngine;

namespace PEGA.NPC
{
    [CreateAssetMenu(
        fileName = "PoiInvestigationCompletedGoalCondition",
        menuName = "PEGA/NPC/GOAP/POI Investigation Completed Goal Condition")]
    public sealed class PEGAPoiInvestigationCompletedGoalConditionAsset : PEGANpcGoapConditionAsset
    {
        [SerializeField]
        [Tooltip("Group Profile that owns the investigation manifest and completion facts.")]
        private GroupProfile activityGroup;

        public override GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (!(state is IGoapEffectStateView) ||
                !TryGetGroupId(out GroupId groupId) ||
                !PEGAPoiGroupKnowledge.TryGetStateView(state, out IGoapKnowledgeStateView knowledgeState) ||
                !PEGAPoiGroupKnowledge.TryGetReader(knowledgeState, out PEGAActorGroupKnowledgeReader reader))
            {
                return GoapTruthValue.Unknown;
            }

            if (state is IGoapHypotheticalStateView hypothetical)
            {
                for (int index = 0; index < hypothetical.ProjectedEffects.Count; index++)
                {
                    if (hypothetical.ProjectedEffects[index] is PEGAPoiInvestigationCompletedEffect effect &&
                        effect.GroupId == groupId &&
                        IsCurrentlyPending(reader, groupId, effect.ObjectEntryId))
                    {
                        return GoapTruthValue.True;
                    }
                }

                return GoapTruthValue.Unknown;
            }

            if (!(state is IGoapObservedStateView observed))
            {
                return GoapTruthValue.Unknown;
            }

            for (int index = 0; index < observed.ObservedEffects.Count; index++)
            {
                if (observed.ObservedEffects[index] is PEGAPoiInvestigationCompletedEffect effect &&
                    effect.GroupId == groupId &&
                    IsCompletedByInvestigationEvidence(reader, groupId, effect.ObjectEntryId))
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
                : "POI investigation completion Goal requires a valid activity Group Profile.";
            return valid;
        }

        private bool TryGetGroupId(out GroupId groupId)
        {
            groupId = default;
            return activityGroup != null &&
                   activityGroup.TryGetGroupId(out groupId, out _) &&
                   groupId.IsValid;
        }

        private static bool IsCurrentlyPending(
            PEGAActorGroupKnowledgeReader reader,
            GroupId groupId,
            ObjectEntryId objectEntryId)
        {
            PEGAActorGroupKnowledgeReadResult<bool> required = reader.ReadGroupFact(
                groupId,
                PEGAObjectEntryKnowledgeFacts.InvestigationRequired.For(objectEntryId),
                Time.timeAsDouble);
            PEGAActorGroupKnowledgeReadResult<bool> completed = reader.ReadGroupFact(
                groupId,
                PEGAObjectEntryKnowledgeFacts.InvestigationCompleted.For(objectEntryId),
                Time.timeAsDouble);
            return required.IsAuthorized && required.Fact.Scope == PEGAKnowledgeScope.Group &&
                   required.Fact.TryGetValue(out bool isRequired) && isRequired &&
                   completed.IsAuthorized && completed.Fact.Scope == PEGAKnowledgeScope.Group &&
                   completed.Fact.TryGetValue(out bool isCompleted) && !isCompleted;
        }

        private static bool IsCompletedByInvestigationEvidence(
            PEGAActorGroupKnowledgeReader reader,
            GroupId groupId,
            ObjectEntryId objectEntryId)
        {
            PEGAActorGroupKnowledgeReadResult<bool> required = reader.ReadGroupFact(
                groupId,
                PEGAObjectEntryKnowledgeFacts.InvestigationRequired.For(objectEntryId),
                Time.timeAsDouble);
            PEGAActorGroupKnowledgeReadResult<bool> completed = reader.ReadGroupFact(
                groupId,
                PEGAObjectEntryKnowledgeFacts.InvestigationCompleted.For(objectEntryId),
                Time.timeAsDouble);
            if (!required.IsAuthorized || required.Fact.Scope != PEGAKnowledgeScope.Group ||
                !required.Fact.TryGetValue(out bool isRequired) || !isRequired ||
                !completed.IsAuthorized || completed.Fact.Scope != PEGAKnowledgeScope.Group ||
                !completed.Fact.TryGetValue(out bool isCompleted) || !isCompleted ||
                !completed.Fact.HasObservationTime)
            {
                return false;
            }

            for (int evidenceIndex = 0; evidenceIndex < completed.Fact.Evidence.Count; evidenceIndex++)
            {
                PEGAKnowledgeEvidence evidence = completed.Fact.Evidence[evidenceIndex];
                if (Math.Abs(evidence.ObservationTimeSeconds - completed.Fact.ObservationTimeSeconds) <= 0.000001d &&
                    evidence.Source is PEGANpcPoiInvestigationEvidence source &&
                    source.ObjectEntryId == objectEntryId && source.GroupId == groupId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

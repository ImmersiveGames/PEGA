using System;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.GOAP;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Group;
using UnityEngine;
namespace PEGA.NPC.Goals.Conditions
{
    [CreateAssetMenu(
        fileName = "PoiInvestigationCompletedGoalCondition",
        menuName = "PEGA/NPC/GOAP/POI Investigation Completed Goal Condition")]
    public sealed class PegaPoiInvestigationCompletedGoalConditionAsset : PegaNpcGoapConditionAsset
    {
        [SerializeField]
        [Tooltip("Group Profile that owns the investigation manifest and completion facts.")]
        private GroupProfile activityGroup;

        public override GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (!(state is IGoapEffectStateView) ||
                !TryGetGroupId(out GroupId groupId) ||
                !PegaPoiGroupKnowledge.TryGetStateView(state, out IGoapKnowledgeStateView knowledgeState) ||
                !PegaPoiGroupKnowledge.TryGetReader(knowledgeState, out PegaActorGroupKnowledgeReader reader))
            {
                return GoapTruthValue.Unknown;
            }

            if (state is IGoapHypotheticalStateView hypothetical)
            {
                for (int index = 0; index < hypothetical.ProjectedEffects.Count; index++)
                {
                    if (hypothetical.ProjectedEffects[index] is PegaPoiInvestigationCompletedEffect effect &&
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
                if (observed.ObservedEffects[index] is PegaPoiInvestigationCompletedEffect effect &&
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
            PegaActorGroupKnowledgeReader reader,
            GroupId groupId,
            ObjectEntryId objectEntryId)
        {
            PegaActorGroupKnowledgeReadResult<bool> required = reader.ReadGroupFact(
                groupId,
                PegaObjectEntryKnowledgeFacts.InvestigationRequired.For(objectEntryId),
                Time.timeAsDouble);
            PegaActorGroupKnowledgeReadResult<bool> completed = reader.ReadGroupFact(
                groupId,
                PegaObjectEntryKnowledgeFacts.InvestigationCompleted.For(objectEntryId),
                Time.timeAsDouble);
            return required.IsAuthorized && required.Fact.Scope == PegaKnowledgeScope.Group &&
                   required.Fact.TryGetValue(out bool isRequired) && isRequired &&
                   completed.IsAuthorized && completed.Fact.Scope == PegaKnowledgeScope.Group &&
                   completed.Fact.TryGetValue(out bool isCompleted) && !isCompleted;
        }

        private static bool IsCompletedByInvestigationEvidence(
            PegaActorGroupKnowledgeReader reader,
            GroupId groupId,
            ObjectEntryId objectEntryId)
        {
            PegaActorGroupKnowledgeReadResult<bool> required = reader.ReadGroupFact(
                groupId,
                PegaObjectEntryKnowledgeFacts.InvestigationRequired.For(objectEntryId),
                Time.timeAsDouble);
            PegaActorGroupKnowledgeReadResult<bool> completed = reader.ReadGroupFact(
                groupId,
                PegaObjectEntryKnowledgeFacts.InvestigationCompleted.For(objectEntryId),
                Time.timeAsDouble);
            if (!required.IsAuthorized || required.Fact.Scope != PegaKnowledgeScope.Group ||
                !required.Fact.TryGetValue(out bool isRequired) || !isRequired ||
                !completed.IsAuthorized || completed.Fact.Scope != PegaKnowledgeScope.Group ||
                !completed.Fact.TryGetValue(out bool isCompleted) || !isCompleted ||
                !completed.Fact.HasObservationTime)
            {
                return false;
            }

            for (int evidenceIndex = 0; evidenceIndex < completed.Fact.Evidence.Count; evidenceIndex++)
            {
                PegaKnowledgeEvidence evidence = completed.Fact.Evidence[evidenceIndex];
                if (Math.Abs(evidence.ObservationTimeSeconds - completed.Fact.ObservationTimeSeconds) <= 0.000001d &&
                    evidence.Source is PegaNpcPoiInvestigationEvidence source &&
                    source.ObjectEntryId == objectEntryId && source.GroupId == groupId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

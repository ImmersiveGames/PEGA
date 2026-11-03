using System;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using ImmersiveGames.Goap;
using UnityEngine;

namespace PEGA.NPC
{
    public enum PEGAPoiInvestigationActivityConditionKind
    {
        HasPendingInvestigations = 0,
        AllRequiredInvestigationsCompleted = 10
    }

    [CreateAssetMenu(
        fileName = "PoiInvestigationActivityCondition",
        menuName = "PEGA/NPC/GOAP/POI Investigation Activity Condition")]
    public sealed class PEGAPoiInvestigationActivityConditionAsset : PEGANpcGoapConditionAsset
    {
        [SerializeField]
        [Tooltip("Group Profile whose current, membership-authorized Knowledge defines this activity.")]
        private GroupProfile activityGroup;

        [SerializeField]
        private PEGAPoiInvestigationActivityConditionKind conditionKind;

        public override GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (state == null || state.Source != GoapStateSource.KnowledgeState ||
                !(state is IGoapKnowledgeStateView knowledgeState) ||
                !TryGetGroupId(out GroupId groupId) ||
                !PEGAPoiGroupKnowledge.TryGetReader(knowledgeState, out PEGAActorGroupKnowledgeReader reader))
            {
                return GoapTruthValue.Unknown;
            }

            PEGAActorGroupKnowledgeFactSetReadResult<ObjectEntryId, bool> requiredFacts =
                reader.ReadGroupFacts(PEGAObjectEntryKnowledgeFacts.InvestigationRequired, Time.timeAsDouble);
            if (!requiredFacts.IsAuthorized)
            {
                return GoapTruthValue.Unknown;
            }

            bool hasRequiredPoi = false;
            bool hasPendingPoi = false;
            bool hasUnknownRequirement = false;
            bool hasUnknownCompletion = false;
            for (int groupIndex = 0; groupIndex < requiredFacts.Groups.Count; groupIndex++)
            {
                PEGAActorGroupKnowledgeFactSetSnapshot<ObjectEntryId, bool> group = requiredFacts.Groups[groupIndex];
                if (group.GroupId != groupId)
                {
                    continue;
                }

                for (int factIndex = 0; factIndex < group.Facts.Count; factIndex++)
                {
                    PEGAKnowledgeFactSnapshot<ObjectEntryId, bool> requiredSnapshot = group.Facts[factIndex];
                    if (!requiredSnapshot.Key.IsValid ||
                        requiredSnapshot.Fact.Scope != PEGAKnowledgeScope.Group)
                    {
                        continue;
                    }

                    if (!requiredSnapshot.Fact.TryGetValue(out bool isRequired))
                    {
                        hasUnknownRequirement = true;
                        continue;
                    }

                    if (!isRequired)
                    {
                        continue;
                    }

                    hasRequiredPoi = true;
                    PEGAActorGroupKnowledgeReadResult<bool> completion = reader.ReadGroupFact(
                        groupId,
                        PEGAObjectEntryKnowledgeFacts.InvestigationCompleted.For(requiredSnapshot.Key.AddressValue),
                        Time.timeAsDouble);
                    if (!completion.IsAuthorized || completion.Fact.Scope != PEGAKnowledgeScope.Group ||
                        !completion.Fact.TryGetValue(out bool isCompleted))
                    {
                        hasUnknownCompletion = true;
                        continue;
                    }

                    if (!isCompleted)
                    {
                        hasPendingPoi = true;
                    }
                }
            }

            if (!hasRequiredPoi)
            {
                return GoapTruthValue.Unknown;
            }

            return conditionKind switch
            {
                PEGAPoiInvestigationActivityConditionKind.HasPendingInvestigations =>
                    hasPendingPoi
                        ? GoapTruthValue.True
                        : hasUnknownRequirement || hasUnknownCompletion
                            ? GoapTruthValue.Unknown
                            : GoapTruthValue.False,
                PEGAPoiInvestigationActivityConditionKind.AllRequiredInvestigationsCompleted =>
                    hasPendingPoi
                        ? GoapTruthValue.False
                        : hasUnknownRequirement || hasUnknownCompletion
                            ? GoapTruthValue.Unknown
                            : GoapTruthValue.True,
                _ => GoapTruthValue.Unknown
            };
        }

        public override bool TryValidate(out string diagnostic)
        {
            bool valid = Enum.IsDefined(typeof(PEGAPoiInvestigationActivityConditionKind), conditionKind) &&
                         TryGetGroupId(out _);
            diagnostic = valid
                ? string.Empty
                : "The POI investigation applicability condition requires a valid Group Profile and condition kind.";
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

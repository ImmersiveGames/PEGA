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
    public enum PegaPoiInvestigationActivityConditionKind
    {
        HasPendingInvestigations = 0,
        AllRequiredInvestigationsCompleted = 10
    }

    [CreateAssetMenu(
        fileName = "PoiInvestigationActivityCondition",
        menuName = "PEGA/NPC/GOAP/POI Investigation Activity Condition")]
    public sealed class PegaPoiInvestigationActivityConditionAsset : PegaNpcGoapConditionAsset
    {
        [SerializeField]
        [Tooltip("Group Profile whose current, membership-authorized Knowledge defines this activity.")]
        private GroupProfile activityGroup;

        [SerializeField]
        private PegaPoiInvestigationActivityConditionKind conditionKind;

        public override GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (state == null || state.Source != GoapStateSource.KnowledgeState ||
                !(state is IGoapKnowledgeStateView knowledgeState) ||
                !TryGetGroupId(out GroupId groupId) ||
                !PegaPoiGroupKnowledge.TryGetReader(knowledgeState, out PegaActorGroupKnowledgeReader reader))
            {
                return GoapTruthValue.Unknown;
            }

            PegaActorGroupKnowledgeFactSetReadResult<ObjectEntryId, bool> requiredFacts =
                reader.ReadGroupFacts(PegaObjectEntryKnowledgeFacts.InvestigationRequired, Time.timeAsDouble);
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
                PegaActorGroupKnowledgeFactSetSnapshot<ObjectEntryId, bool> group = requiredFacts.Groups[groupIndex];
                if (group.GroupId != groupId)
                {
                    continue;
                }

                for (int factIndex = 0; factIndex < group.Facts.Count; factIndex++)
                {
                    PegaKnowledgeFactSnapshot<ObjectEntryId, bool> requiredSnapshot = group.Facts[factIndex];
                    if (!requiredSnapshot.Key.IsValid ||
                        requiredSnapshot.Fact.Scope != PegaKnowledgeScope.Group)
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
                    PegaActorGroupKnowledgeReadResult<bool> completion = reader.ReadGroupFact(
                        groupId,
                        PegaObjectEntryKnowledgeFacts.InvestigationCompleted.For(requiredSnapshot.Key.AddressValue),
                        Time.timeAsDouble);
                    if (!completion.IsAuthorized || completion.Fact.Scope != PegaKnowledgeScope.Group ||
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
                PegaPoiInvestigationActivityConditionKind.HasPendingInvestigations =>
                    hasPendingPoi
                        ? GoapTruthValue.True
                        : hasUnknownRequirement || hasUnknownCompletion
                            ? GoapTruthValue.Unknown
                            : GoapTruthValue.False,
                PegaPoiInvestigationActivityConditionKind.AllRequiredInvestigationsCompleted =>
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
            bool valid = Enum.IsDefined(typeof(PegaPoiInvestigationActivityConditionKind), conditionKind) &&
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

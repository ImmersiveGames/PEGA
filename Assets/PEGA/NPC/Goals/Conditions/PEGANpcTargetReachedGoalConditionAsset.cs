using System;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.GOAP;
using PEGA.NPC.Actions.Runtime;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Group;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
namespace PEGA.NPC.Goals.Conditions
{
    /// <summary>
    /// Goal condition for arriving at a Target selected by its open, stable Target kind and identity domain.
    /// This is one reusable condition implementation; other Goals can provide different condition assets.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PEGANpcTargetReachedGoalCondition",
        menuName = "PEGA/NPC/GOAP/Target Reached Goal Condition")]
    public sealed class PegaNpcTargetReachedGoalConditionAsset : PegaNpcGoapConditionAsset
    {
        [SerializeField]
        [Tooltip("Categoria estável e aberta do Target que satisfaz esta condição, por exemplo character-position.")]
        private string targetKind;

        [SerializeField]
        [Tooltip("Domínio de identidade do Target que satisfaz esta condição.")]
        private PegaNpcPlanningTargetIdentityKind targetIdentityKind;

        [SerializeField]
        [Tooltip("Em resultados observados, exige contato visual atual do Actor associado ao Target; projeções causais continuam avaliando somente a chegada prevista.")]
        private bool requireCurrentCharacterContactWhenObserved;

        [SerializeField]
        [Tooltip("Object Entry lógico cuja posição conhecida precisa ter sido alcançada.")]
        private ObjectEntryIdReference requiredObjectEntry;

        [SerializeField]
        [Tooltip("Group cuja membership efetiva autorizou a posição usada por este Goal.")]
        private GroupProfile requiredKnowledgeGroup;

        public override GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!(state is IGoapEffectStateView effectState))
            {
                return GoapTruthValue.Unknown;
            }

            string requiredKind = PegaNpcPlanningTargetKind.FromStableText(targetKind).StableText;
            if (string.IsNullOrWhiteSpace(requiredKind) || !Enum.IsDefined(typeof(PegaNpcPlanningTargetIdentityKind), targetIdentityKind))
            {
                return GoapTruthValue.Unknown;
            }

            for (int index = 0; index < effectState.Effects.Count; index++)
            {
                if (effectState.Effects[index] is PegaNpcTargetReachedEffect reachedEffect &&
                    reachedEffect.TargetKind.StableText == requiredKind &&
                    reachedEffect.TargetId.IdentityKind == targetIdentityKind)
                {
                    if (targetIdentityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition)
                    {
                        if (!TryGetRequiredGroupAndObjectEntry(
                                out ObjectEntryId requiredEntryId,
                                out GroupId requiredGroupId) ||
                            reachedEffect.TargetId.ObjectEntryId != requiredEntryId ||
                            reachedEffect.KnowledgeGroupId != requiredGroupId ||
                            !IsEffectBackedByCurrentGroupKnowledge(state, reachedEffect))
                        {
                            continue;
                        }
                    }

                    if (requireCurrentCharacterContactWhenObserved &&
                        state.Source == GoapStateSource.WorldState)
                    {
                        return EvaluateObservedCharacterContact(state, reachedEffect.TargetId.KnownActorId);
                    }

                    return GoapTruthValue.True;
                }
            }

            // Effect views may describe only one Action or one hypothetical branch, so absence is not False.
            return GoapTruthValue.Unknown;
        }

        private static GoapTruthValue EvaluateObservedCharacterContact(
            IGoapStateView state,
            Immersive.Framework.Actors.ActorId subjectActorId)
        {
            if (!(state is IGoapObservedStateView observedState) || !subjectActorId.IsValid)
            {
                return GoapTruthValue.Unknown;
            }

            for (int index = 0; index < observedState.KnowledgeState.Capabilities.Count; index++)
            {
                if (!(observedState.KnowledgeState.Capabilities[index] is PegaNpcKnowledgeState knowledgeState))
                {
                    continue;
                }

                PegaKnowledgeFact<bool> visualContact = knowledgeState.GetCharacterVisualContact(subjectActorId);
                if (visualContact.TryGetValue(out bool hasVisualContact))
                {
                    return hasVisualContact
                        ? GoapTruthValue.True
                        : GoapTruthValue.False;
                }

                return GoapTruthValue.Unknown;
            }

            return GoapTruthValue.Unknown;
        }

        public override bool TryValidate(out string diagnostic)
        {
            bool valid = !string.IsNullOrWhiteSpace(targetKind) &&
                         PegaNpcPlanningTargetKind.FromStableText(targetKind).IsValid &&
                         Enum.IsDefined(typeof(PegaNpcPlanningTargetIdentityKind), targetIdentityKind);
            if (valid && targetIdentityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition)
            {
                valid = TryGetRequiredGroupAndObjectEntry(out _, out _);
            }
            diagnostic = valid
                ? string.Empty
                : "Target reached condition requires a stable Target kind and supported identity domain; Group Object Entry positions also require a valid ObjectEntryId and Group Profile.";
            return valid;
        }

        private bool TryGetRequiredGroupAndObjectEntry(
            out ObjectEntryId objectEntryId,
            out GroupId groupId)
        {
            objectEntryId = default;
            groupId = default;
            return requiredObjectEntry.TryGetObjectEntryId(out objectEntryId) &&
                   requiredKnowledgeGroup != null &&
                   requiredKnowledgeGroup.TryGetGroupId(out groupId, out _) &&
                   objectEntryId.IsValid && groupId.IsValid;
        }

        private bool IsEffectBackedByCurrentGroupKnowledge(
            IGoapStateView state,
            PegaNpcTargetReachedEffect effect)
        {
            IGoapKnowledgeStateView knowledgeState = state as IGoapKnowledgeStateView;
            if (knowledgeState == null && state is IGoapHypotheticalStateView hypothetical)
            {
                knowledgeState = hypothetical.KnowledgeState;
            }

            if (knowledgeState == null && state is IGoapObservedStateView observed)
            {
                knowledgeState = observed.KnowledgeState;
            }

            if (knowledgeState == null || !effect.HasTargetPosition)
            {
                return false;
            }

            if (!TryGetRequiredGroupAndObjectEntry(out ObjectEntryId objectEntryId, out GroupId groupId))
            {
                return false;
            }

            PegaKnowledgeFactKey<ObjectEntryId, Vector3> key = PegaObjectEntryKnowledgeFacts.Position.For(objectEntryId);
            for (int index = 0; index < knowledgeState.Capabilities.Count; index++)
            {
                if (!(knowledgeState.Capabilities[index] is PegaActorGroupKnowledgeReader reader))
                {
                    continue;
                }

                PegaActorGroupKnowledgeReadResult<Vector3> result = reader.ReadGroupFact(
                    groupId,
                    key,
                    Time.timeAsDouble);
                if (!result.IsAuthorized ||
                    result.Fact.Scope != PegaKnowledgeScope.Group ||
                    !result.Fact.TryGetValue(out Vector3 currentPosition) ||
                    !result.Fact.HasObservationTime ||
                    Math.Abs(result.Fact.ObservationTimeSeconds - effect.EvidenceTimeSeconds) > 0.000001d ||
                    !currentPosition.Equals(effect.TargetPosition))
                {
                    return false;
                }

                return true;
            }

            return false;
        }
    }
}

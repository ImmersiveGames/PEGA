using System;
using ImmersiveGames.Goap;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// Goal condition for arriving at a Target selected by its open, stable Target kind and identity domain.
    /// This is one reusable condition implementation; other Goals can provide different condition assets.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PEGANpcTargetReachedGoalCondition",
        menuName = "PEGA/NPC/GOAP/Target Reached Goal Condition")]
    public sealed class PEGANpcTargetReachedGoalConditionAsset : PEGANpcGoapConditionAsset
    {
        [SerializeField]
        [Tooltip("Categoria estável e aberta do Target que satisfaz esta condição, por exemplo character-position.")]
        private string targetKind;

        [SerializeField]
        [Tooltip("Domínio de identidade do Target que satisfaz esta condição.")]
        private PEGANpcPlanningTargetIdentityKind targetIdentityKind;

        [SerializeField]
        [Tooltip("Em resultados observados, exige contato visual atual do Actor associado ao Target; projeções causais continuam avaliando somente a chegada prevista.")]
        private bool requireCurrentCharacterContactWhenObserved;

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

            string requiredKind = PEGANpcPlanningTargetKind.FromStableText(targetKind).StableText;
            if (string.IsNullOrWhiteSpace(requiredKind) || !Enum.IsDefined(typeof(PEGANpcPlanningTargetIdentityKind), targetIdentityKind))
            {
                return GoapTruthValue.Unknown;
            }

            for (int index = 0; index < effectState.Effects.Count; index++)
            {
                if (effectState.Effects[index] is PEGANpcTargetReachedEffect reachedEffect &&
                    reachedEffect.TargetKind.StableText == requiredKind &&
                    reachedEffect.TargetId.IdentityKind == targetIdentityKind)
                {
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
                if (!(observedState.KnowledgeState.Capabilities[index] is PEGANpcKnowledgeState knowledgeState))
                {
                    continue;
                }

                PEGANpcKnowledgeFact<bool> visualContact = knowledgeState.GetCharacterVisualContact(subjectActorId);
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
                         PEGANpcPlanningTargetKind.FromStableText(targetKind).IsValid &&
                         Enum.IsDefined(typeof(PEGANpcPlanningTargetIdentityKind), targetIdentityKind);
            diagnostic = valid
                ? string.Empty
                : "Target reached condition requires a stable Target kind and a supported identity domain.";
            return valid;
        }
    }
}

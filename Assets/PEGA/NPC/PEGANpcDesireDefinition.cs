using System;
using UnityEngine;

namespace PEGA.NPC
{
    [CreateAssetMenu(
        fileName = "PEGANpcDesireDefinition",
        menuName = "PEGA/NPC/Desire Definition")]
    public sealed class PEGANpcDesireDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Identificador estável PEGA usado para compor este Desejo e localizar sua satisfação individual.")]
        private string desireId;

        [SerializeField]
        [Tooltip("Nome legível deste Desejo para diagnóstico e configuração.")]
        private string displayName;

        [SerializeField]
        [Tooltip("Predicado de estado desejado, avaliado separadamente dos Effects das Actions disponíveis.")]
        private PEGANpcDesireGoalKind desiredGoal;

        public string DesireId => desireId;

        public string DisplayName => displayName;

        public PEGANpcDesireGoalKind DesiredGoal => desiredGoal;

        internal bool Matches(PEGANpcObservedActionEffect observedEffect) =>
            observedEffect.IsValid && observedEffect.GoalKind == desiredGoal;

        internal bool TryValidate(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (string.IsNullOrWhiteSpace(desireId))
            {
                diagnostic = "Desire ID must be a non-empty stable PEGA identifier.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                diagnostic = $"Desire '{desireId}' requires a readable display name.";
                return false;
            }

            if (!Enum.IsDefined(typeof(PEGANpcDesireGoalKind), desiredGoal))
            {
                diagnostic = $"Desire '{desireId}' uses an unsupported goal predicate.";
                return false;
            }

            return true;
        }
    }
}

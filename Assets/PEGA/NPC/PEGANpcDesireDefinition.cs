using System;
using PEGA.GOAP;
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
        [Tooltip("Condição GOAP componível que descreve o estado desejado, independente das Actions disponíveis.")]
        private PEGANpcGoapConditionAsset desiredGoalCondition;

        public string DesireId => desireId;

        public string DisplayName => displayName;

        public PEGANpcGoapConditionAsset DesiredGoalCondition => desiredGoalCondition;

        public PEGAGoapGoal CreateGoal() => desiredGoalCondition != null
            ? new PEGAGoapGoal(desiredGoalCondition)
            : throw new InvalidOperationException($"Desire '{desireId}' has no Goal condition.");

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

            if (desiredGoalCondition == null)
            {
                diagnostic = $"Desire '{desireId}' requires a composable GOAP Goal condition asset.";
                return false;
            }

            if (!desiredGoalCondition.TryValidate(out diagnostic))
            {
                diagnostic = $"Desire '{desireId}' Goal condition: {diagnostic}";
                return false;
            }

            return true;
        }
    }
}

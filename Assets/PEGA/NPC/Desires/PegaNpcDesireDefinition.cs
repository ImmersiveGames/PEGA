using System;
using PEGA.GOAP;
using PEGA.NPC.Goals.Conditions;
using UnityEngine;
namespace PEGA.NPC.Desires
{
    [CreateAssetMenu(
        fileName = "New Desire Definition",
        menuName = "PEGA/NPC/Desire Definition")]
    public sealed class PegaNpcDesireDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Stable internal ID used to track this Desire. Keep it unchanged when editing an existing definition.")]
        private string desireId;

        [SerializeField]
        [Tooltip("Designer-facing name for this motivation.")]
        private string displayName;

        [SerializeField]
        [Tooltip("Existing success condition that defines when this Desire is fulfilled, independently of the available behaviors.")]
        private PegaNpcGoapConditionAsset desiredGoalCondition;

        public string DesireId => desireId;

        public string DisplayName => displayName;

        public PegaNpcGoapConditionAsset DesiredGoalCondition => desiredGoalCondition;

        public GoapGoal CreateGoal() => desiredGoalCondition != null
            ? new GoapGoal(desiredGoalCondition)
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

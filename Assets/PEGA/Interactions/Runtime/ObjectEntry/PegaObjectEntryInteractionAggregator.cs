using System;
using System.Collections.Generic;
using UnityEngine;
namespace PEGA.Interactions.Runtime.ObjectEntry
{
    [Serializable]
    public struct PegaObjectInteractionOffer
    {
        [SerializeField]
        private PegaObjectInteractionAction action;

        [SerializeField]
        private PegaObjectInteractionParticipantMask participants;

        public PegaObjectInteractionAction Action => action;
        public PegaObjectInteractionParticipantMask Participants => participants;
    }

    /// <summary>Compõe ações e disponibilidade por participante sem implementar regras das operações.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/Interaction/Object Entry/Interaction Aggregator")]
    public sealed class PegaObjectEntryInteractionAggregator : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Actions offered by this Object Entry and the participants allowed to request each one.")]
        private PegaObjectInteractionOffer[] offers = Array.Empty<PegaObjectInteractionOffer>();

        public int OfferCount => offers != null ? offers.Length : 0;

        /// <summary>Checks authored participant availability without evaluating the action's runtime eligibility.</summary>
        public bool IsOfferedTo(
            PegaObjectInteractionAction action,
            PegaObjectInteractionParticipant participant)
        {
            if (action == null || action.gameObject != gameObject ||
                (participant != PegaObjectInteractionParticipant.Player &&
                 participant != PegaObjectInteractionParticipant.Npc) ||
                !TryValidateOfferComposition(out _))
            {
                return false;
            }

            PegaObjectInteractionParticipantMask participantMask = ToMask(participant);
            for (int index = 0; index < offers.Length; index++)
            {
                PegaObjectInteractionOffer offer = offers[index];
                if (offer.Action == action &&
                    (offer.Participants & participantMask) == participantMask)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetAvailableActions(
            PegaObjectInteractionRequest request,
            out IReadOnlyList<PegaObjectInteractionAction> actions,
            out string diagnostic)
        {
            actions = Array.Empty<PegaObjectInteractionAction>();
            if (!TryValidate(request, out diagnostic))
            {
                return false;
            }

            List<PegaObjectInteractionAction> available = new List<PegaObjectInteractionAction>();
            PegaObjectInteractionParticipantMask participant = ToMask(request.Participant);
            for (int index = 0; index < offers.Length; index++)
            {
                PegaObjectInteractionOffer offer = offers[index];
                if ((offer.Participants & participant) != participant ||
                    !offer.Action.CanStart(request, out _))
                {
                    continue;
                }

                available.Add(offer.Action);
            }

            actions = available.Count == 0
                ? Array.Empty<PegaObjectInteractionAction>()
                : Array.AsReadOnly(available.ToArray());
            diagnostic = string.Empty;
            return true;
        }

        public bool TryStart(
            PegaObjectInteractionAction action,
            PegaObjectInteractionRequest request,
            out long executionId,
            out string diagnostic)
        {
            executionId = 0;
            if (!TryValidate(request, out diagnostic) || action == null)
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "A composed action is required.";
                }

                return false;
            }

            PegaObjectInteractionParticipantMask participant = ToMask(request.Participant);
            for (int index = 0; index < offers.Length; index++)
            {
                PegaObjectInteractionOffer offer = offers[index];
                if (offer.Action == action && (offer.Participants & participant) == participant)
                {
                    return action.TryStart(request, out executionId, out diagnostic);
                }
            }

            diagnostic = "The action is not offered by this Object Entry to the requesting participant.";
            return false;
        }

        private bool TryValidate(
            PegaObjectInteractionRequest request,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy || !request.IsValid ||
                request.TargetRoot != gameObject || offers == null)
            {
                diagnostic = "The aggregator requires an active Object Entry root, a valid request and explicit offers.";
                return false;
            }

            return TryValidateOfferComposition(out diagnostic);
        }

        private bool TryValidateOfferComposition(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (offers == null)
            {
                diagnostic = "The aggregator requires explicit offers.";
                return false;
            }

            for (int index = 0; index < offers.Length; index++)
            {
                PegaObjectInteractionOffer offer = offers[index];
                PegaObjectInteractionParticipantMask supported = PegaObjectInteractionParticipantMask.Both;
                if (offer.Action == null || offer.Action.gameObject != gameObject ||
                    offer.Participants == PegaObjectInteractionParticipantMask.None ||
                    (offer.Participants & ~supported) != 0)
                {
                    diagnostic = $"Object Interaction offer {index} must reference an action on this exact root and a supported participant.";
                    return false;
                }

                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    if (offers[previousIndex].Action == offer.Action)
                    {
                        diagnostic = "Each action component can be offered only once by an aggregator.";
                        return false;
                    }
                }
            }

            return true;
        }

        private static PegaObjectInteractionParticipantMask ToMask(
            PegaObjectInteractionParticipant participant) => participant switch
        {
            PegaObjectInteractionParticipant.Player => PegaObjectInteractionParticipantMask.Player,
            PegaObjectInteractionParticipant.Npc => PegaObjectInteractionParticipantMask.Npc,
            _ => PegaObjectInteractionParticipantMask.None
        };
    }
}

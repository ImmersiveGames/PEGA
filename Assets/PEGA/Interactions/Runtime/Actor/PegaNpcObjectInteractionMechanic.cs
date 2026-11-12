using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.Interactions.Runtime.Actor
{
    /// <summary>Recebe do GOAP a operação e o alvo já escolhidos e encaminha a execução compartilhada.</summary>
    [AddComponentMenu("PEGA/Interaction/Actor/NPC Object Interaction Mechanic")]
    public sealed class PegaNpcObjectInteractionMechanic : PegaActorObjectInteractionMechanic
    {
        private void Awake()
        {
            InitializeSharedLogger();
            InteractionLogger.Debug(
                "Actor object interaction mechanic initialized.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("playerIntentPath", false));
        }

        public bool TryStartRequestedAction(
            PegaObjectInteractionAction action,
            PegaObjectInteractionRequest request,
            out long executionId,
            out string diagnostic) =>
            TryStartSharedExecution(action, request, out executionId, out diagnostic);
    }
}

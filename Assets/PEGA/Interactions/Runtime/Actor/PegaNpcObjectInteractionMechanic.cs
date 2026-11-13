using Immersive.Logging.Records;
using PEGA.Interactions.Runtime.ObjectEntry;
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

        public bool TryStartRequestedAction(
            PegaObjectEntryPickUpAction action,
            PegaObjectInteractionRequest request,
            out long executionId,
            out PegaObjectEntryPickUpStartStatus status,
            out string diagnostic)
        {
            executionId = 0;
            status = PegaObjectEntryPickUpStartStatus.InvalidContract;
            diagnostic = string.Empty;
            if (!isActiveAndEnabled || action == null ||
                !object.ReferenceEquals(request.ActorRoot, gameObject))
            {
                diagnostic = "The Actor interaction mechanic requires its own active Actor root and an explicit Pick Up action.";
                return false;
            }

            PegaObjectEntryInteractionAggregator aggregator = request.TargetRoot != null
                ? request.TargetRoot.GetComponent<PegaObjectEntryInteractionAggregator>()
                : null;
            if (aggregator == null)
            {
                diagnostic = "The current Object Entry has no active interaction aggregator.";
                return false;
            }

            bool started = aggregator.TryStart(
                action,
                request,
                out executionId,
                out status,
                out diagnostic);
            InteractionLogger.Info(
                started
                    ? "Actor Pick Up interaction request started."
                    : "Actor Pick Up interaction request was rejected with a typed availability result.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("participant", request.Participant),
                LogFields.Field("objectEntryId", request.ObjectEntryId.StableText),
                LogFields.Field("objectRoot", request.TargetRoot != null ? request.TargetRoot.name : string.Empty),
                LogFields.Field("action", action.GetType().Name),
                LogFields.Field("executionId", executionId),
                LogFields.Field("startStatus", status),
                LogFields.Field("diagnostic", diagnostic));
            return started;
        }
    }
}

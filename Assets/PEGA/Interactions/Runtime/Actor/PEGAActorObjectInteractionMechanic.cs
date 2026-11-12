using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.Interactions.Runtime.ObjectEntry;
using UnityEngine;

namespace PEGA.Interactions.Runtime.Actor
{
    /// <summary>Compartilha a validação e o encaminhamento de execução das mecânicas de interação dos Actors.</summary>
    [DisallowMultipleComponent]
    public abstract class PegaActorObjectInteractionMechanic : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.ObjectInteraction";

        private ScopedLogger _log;

        protected ScopedLogger InteractionLogger
        {
            get
            {
                InitializeSharedLogger();
                return _log;
            }
        }

        protected void InitializeSharedLogger()
        {
            if (_log == null)
            {
                _log = PegaLogging.CreateScopedLogger<PegaActorObjectInteractionMechanic>(
                    LogCategory,
                    gameObject.name);
            }
        }

        protected bool TryStartSharedExecution(
            PegaObjectInteractionAction action,
            PegaObjectInteractionRequest request,
            out long executionId,
            out string diagnostic)
        {
            executionId = 0;
            diagnostic = string.Empty;
            if (!isActiveAndEnabled || action == null ||
                !object.ReferenceEquals(request.ActorRoot, gameObject))
            {
                diagnostic = "The Actor interaction mechanic requires its own active Actor root and an explicit composed action.";
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

            ScopedLogger log = InteractionLogger;
            bool started = aggregator.TryStart(action, request, out executionId, out diagnostic);
            log.Info(
                started
                    ? "Actor object interaction request started."
                    : "Actor object interaction request was rejected.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("participant", request.Participant),
                LogFields.Field("objectEntryId", request.ObjectEntryId.StableText),
                LogFields.Field("objectRoot", request.TargetRoot.name),
                LogFields.Field("action", action.GetType().Name),
                LogFields.Field("executionId", executionId),
                LogFields.Field("diagnostic", diagnostic));
            return started;
        }
    }
}

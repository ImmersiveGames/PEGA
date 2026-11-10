using Immersive.Framework.ObjectEntry;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;
namespace PEGA.Interactions.Runtime.ObjectEntry
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PegaObjectEntryPhysicalItem))]
    [AddComponentMenu("PEGA/Interaction/Object Entry/Actions/Rob")]
    public sealed class PegaObjectEntryRobAction : PegaObjectInteractionAction
    {
        private const string LogCategory = "PEGA.Gameplay.ObjectInteraction";

        [SerializeField, Min(0f)]
        [Tooltip("Distância máxima tridimensional entre as raízes do Actor e do item (m).")]
        private float interactionRangeMeters = 1.25f;

        [SerializeField, Min(0f)]
        [Tooltip("Tempo contínuo em segundos antes de confirmar a transferência do item.")]
        private float executionDurationSeconds = 1f;

        private double _startedAtTimeSeconds;
        private PegaActorPhysicalInventory _reservedInventory;
        private PegaObjectEntryPhysicalItem _reservedItem;
        private ScopedLogger _log;

        public override string DisplayName => "Roubar";

        public float InteractionRangeMeters => interactionRangeMeters;

        public float ExecutionDurationSeconds => executionDurationSeconds;

        public bool TryGetConfiguration(
            out PegaRobObjectEntryConfigurationSnapshot parameters,
            out string diagnostic)
        {
            parameters = default;
            return TryBuildConfiguration(out parameters, out diagnostic);
        }

        protected override bool CanExecute(
            PegaObjectInteractionRequest request,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!TryBuildConfiguration(
                    out PegaRobObjectEntryConfigurationSnapshot parameters,
                    out diagnostic))
            {
                return false;
            }

            if (!TryGetRequirements(request, out PegaActorPhysicalInventory inventory,
                    out PegaObjectEntryPhysicalItem item, out diagnostic) ||
                !item.IsAvailableOnGround || !inventory.CanAccept(item, out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Object Entry is no longer available on the ground."
                    : diagnostic;
                return false;
            }

            if (parameters.ObjectEntryId != request.ObjectEntryId ||
                Vector3.Distance(request.ActorRoot.transform.position, request.TargetRoot.transform.position) >
                parameters.InteractionRangeMeters)
            {
                diagnostic = parameters.ObjectEntryId != request.ObjectEntryId
                    ? "The authored Rob parameters do not address this Object Entry identity."
                    : "The Actor is outside the configured Rob interaction range.";
                return false;
            }

            return true;
        }

        protected override bool BeginExecution(
            PegaObjectInteractionRequest request,
            long executionId,
            out string diagnostic)
        {
            _reservedInventory = null;
            _reservedItem = null;
            if (!TryGetRequirements(request, out PegaActorPhysicalInventory inventory,
                    out PegaObjectEntryPhysicalItem item, out diagnostic))
            {
                return false;
            }

            if (!item.TryReserve(this, executionId))
            {
                diagnostic = "Another interaction already reserved this physical Object Entry.";
                return false;
            }

            _reservedInventory = inventory;
            _reservedItem = item;
            _startedAtTimeSeconds = Time.timeAsDouble;
            diagnostic = string.Empty;
            return true;
        }

        protected override void TickExecution(
            PegaObjectInteractionRequest request,
            long executionId)
        {
            if (!TryBuildConfiguration(
                    out PegaRobObjectEntryConfigurationSnapshot parameters,
                    out string diagnostic) ||
                _reservedInventory == null || _reservedItem == null ||
                _reservedInventory.gameObject != request.ActorRoot ||
                _reservedItem.gameObject != request.TargetRoot ||
                parameters.ObjectEntryId != request.ObjectEntryId ||
                !_reservedItem.HasReservation(this, executionId) ||
                !_reservedInventory.CanAcceptReserved(
                    request,
                    _reservedItem,
                    this,
                    executionId,
                    out diagnostic) ||
                Vector3.Distance(request.ActorRoot.transform.position, request.TargetRoot.transform.position) >
                parameters.InteractionRangeMeters)
            {
                CompleteExecution(
                    executionId,
                    PegaObjectInteractionExecutionStatus.Failed,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "The Actor, item, capacity, occurrence authority or interaction range changed before the transfer committed."
                        : diagnostic);
                return;
            }

            if (Time.timeAsDouble - _startedAtTimeSeconds < parameters.ExecutionDurationSeconds)
            {
                return;
            }

            if (!_reservedInventory.TryAcceptReserved(
                    request,
                    _reservedItem,
                    this,
                    executionId,
                    out diagnostic))
            {
                CompleteExecution(
                    executionId,
                    PegaObjectInteractionExecutionStatus.Failed,
                    diagnostic);
                return;
            }

            CompleteExecution(
                executionId,
                PegaObjectInteractionExecutionStatus.Succeeded,
                "The Object Entry was transferred to the Actor's physical inventory.");
        }

        protected override void OnExecutionCompleted(
            PegaObjectInteractionRequest request,
            long executionId,
            PegaObjectInteractionExecutionStatus status)
        {
            if (status != PegaObjectInteractionExecutionStatus.Succeeded && _reservedItem != null)
            {
                _reservedItem.ReleaseReservation(this, executionId);
            }

            if (status == PegaObjectInteractionExecutionStatus.Succeeded)
            {
                _log ??= PegaLogging.CreateScopedLogger<PegaObjectEntryRobAction>(
                    LogCategory,
                    gameObject.name);
                _log.Info(
                    "Object Entry was transferred to an Actor's physical inventory.",
                    LogFields.Field("participant", request.Participant),
                    LogFields.Field("actorId", request.ActorId.StableText),
                    LogFields.Field("objectEntryId", request.ObjectEntryId.StableText),
                    LogFields.Field("executionId", executionId));
            }

            _reservedInventory = null;
            _reservedItem = null;
        }

        private static bool TryGetRequirements(
            PegaObjectInteractionRequest request,
            out PegaActorPhysicalInventory inventory,
            out PegaObjectEntryPhysicalItem item,
            out string diagnostic)
        {
            inventory = request.ActorRoot != null
                ? request.ActorRoot.GetComponent<PegaActorPhysicalInventory>()
                : null;
            item = request.TargetRoot != null
                ? request.TargetRoot.GetComponent<PegaObjectEntryPhysicalItem>()
                : null;
            if (!request.IsValid || inventory == null || inventory.gameObject != request.ActorRoot ||
                item == null || item.gameObject != request.TargetRoot ||
                !item.TryGetObjectEntryId(out ObjectEntryId itemId) || itemId != request.ObjectEntryId)
            {
                diagnostic = "Rob requires this Actor's physical inventory and a physical-item component on the exact Object Entry root.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryBuildConfiguration(
            out PegaRobObjectEntryConfigurationSnapshot parameters,
            out string diagnostic)
        {
            parameters = default;
            if (!IsNonNegativeFinite(interactionRangeMeters) ||
                !IsNonNegativeFinite(executionDurationSeconds))
            {
                diagnostic = "Rob requires finite, non-negative interaction range and execution duration values.";
                return false;
            }

            ObjectEntryDeclaration declaration = GetComponent<ObjectEntryDeclaration>();
            if (declaration == null ||
                !declaration.TryGetObjectEntryId(out ObjectEntryId objectEntryId) ||
                !objectEntryId.IsValid)
            {
                diagnostic = "Rob requires an ObjectEntryDeclaration on the same root as its action component.";
                return false;
            }

            parameters = new PegaRobObjectEntryConfigurationSnapshot(
                objectEntryId,
                interactionRangeMeters,
                executionDurationSeconds);
            diagnostic = string.Empty;
            return true;
        }

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

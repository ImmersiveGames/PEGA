using Immersive.Framework.ObjectEntry;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;
namespace PEGA.Interactions.Runtime.ObjectEntry
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PegaObjectEntryPhysicalItem))]
    [AddComponentMenu("PEGA/Interaction/Object Entry/Actions/Pick Up")]
    public sealed class PegaObjectEntryPickUpAction : PegaObjectInteractionAction
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

        public override string DisplayName => "Pick Up";

        public float InteractionRangeMeters => interactionRangeMeters;

        public float ExecutionDurationSeconds => executionDurationSeconds;

        public bool TryGetConfiguration(
            out PegaPickUpObjectEntryConfigurationSnapshot parameters,
            out string diagnostic)
        {
            parameters = default;
            return TryBuildConfiguration(out parameters, out diagnostic);
        }

        public bool TryAssessStart(
            PegaObjectInteractionRequest request,
            out PegaObjectEntryPickUpStartStatus status,
            out string diagnostic)
        {
            status = PegaObjectEntryPickUpStartStatus.InvalidRequest;
            diagnostic = string.Empty;
            if (!request.IsValid || request.TargetRoot != gameObject)
            {
                diagnostic = "Pick Up requires a valid interaction request addressed to this exact Object Entry root.";
                return false;
            }

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                status = PegaObjectEntryPickUpStartStatus.ActionInactive;
                diagnostic = "The Pick Up action component is inactive.";
                return false;
            }

            if (!TryBuildConfiguration(out PegaPickUpObjectEntryConfigurationSnapshot parameters, out diagnostic))
            {
                status = PegaObjectEntryPickUpStartStatus.InvalidConfiguration;
                return false;
            }

            if (!request.TryValidateCurrentAuthority(out diagnostic))
            {
                status = PegaObjectEntryPickUpStartStatus.InvalidAuthority;
                return false;
            }

            if (!TryGetRequirements(
                    request,
                    out PegaActorPhysicalInventory inventory,
                    out PegaObjectEntryPhysicalItem item,
                    out diagnostic))
            {
                status = PegaObjectEntryPickUpStartStatus.InvalidContract;
                return false;
            }

            if (!inventory.TryValidateForInteraction(out diagnostic))
            {
                status = PegaObjectEntryPickUpStartStatus.InvalidContract;
                return false;
            }

            if (parameters.ObjectEntryId != request.ObjectEntryId)
            {
                status = PegaObjectEntryPickUpStartStatus.InvalidConfiguration;
                diagnostic = "The authored Pick Up parameters do not address this Object Entry identity.";
                return false;
            }

            if (Vector3.Distance(request.ActorRoot.transform.position, request.TargetRoot.transform.position) >
                parameters.InteractionRangeMeters)
            {
                status = PegaObjectEntryPickUpStartStatus.OutsideInteractionRange;
                diagnostic = "The Actor is outside the configured Pick Up interaction range.";
                return false;
            }

            if (inventory.Contains(request.ObjectEntryId))
            {
                status = PegaObjectEntryPickUpStartStatus.AlreadyInInventory;
                diagnostic = "This Object Entry is already present in the requesting Actor's physical inventory.";
                return false;
            }

            if (item.IsReserved)
            {
                status = PegaObjectEntryPickUpStartStatus.TemporarilyReserved;
                diagnostic = "Another interaction currently reserves this physical Object Entry.";
                return false;
            }

            if (!item.IsAvailableOnGround)
            {
                status = PegaObjectEntryPickUpStartStatus.NoLongerOnGround;
                diagnostic = "The Object Entry is no longer available on the ground.";
                return false;
            }

            if (!inventory.HasFreeCapacity)
            {
                status = PegaObjectEntryPickUpStartStatus.InventoryFull;
                diagnostic = "The Actor's physical inventory has no free item slot.";
                return false;
            }

            if (ExecutionStatus == PegaObjectInteractionExecutionStatus.Running)
            {
                status = PegaObjectEntryPickUpStartStatus.ActionAlreadyRunning;
                diagnostic = "The Pick Up action component is already executing.";
                return false;
            }

            status = PegaObjectEntryPickUpStartStatus.Ready;
            diagnostic = string.Empty;
            return true;
        }

        internal bool TryStart(
            PegaObjectInteractionRequest request,
            out long executionId,
            out PegaObjectEntryPickUpStartStatus status,
            out string diagnostic)
        {
            executionId = 0;
            if (!TryAssessStart(request, out status, out diagnostic))
            {
                return false;
            }

            if (base.TryStart(request, out executionId, out diagnostic))
            {
                status = PegaObjectEntryPickUpStartStatus.Ready;
                return true;
            }

            if (!TryAssessStart(request, out status, out string currentDiagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(currentDiagnostic) ? diagnostic : currentDiagnostic;
                return false;
            }

            status = PegaObjectEntryPickUpStartStatus.StartRejected;
            if (string.IsNullOrWhiteSpace(diagnostic))
            {
                diagnostic = "The Pick Up start was rejected after its preconditions were assessed.";
            }

            return false;
        }

        public bool TryGetRemainingExecutionDurationSeconds(
            long executionId,
            float plannedDurationSeconds,
            out float remainingDurationSeconds,
            out string diagnostic)
        {
            remainingDurationSeconds = 0f;
            if (!TryGetExecution(
                    executionId,
                    out PegaObjectInteractionExecutionStatus status,
                    out diagnostic) ||
                status != PegaObjectInteractionExecutionStatus.Running ||
                !TryBuildConfiguration(
                    out PegaPickUpObjectEntryConfigurationSnapshot parameters,
                    out diagnostic) ||
                parameters.ExecutionDurationSeconds != plannedDurationSeconds ||
                float.IsNaN(plannedDurationSeconds) ||
                float.IsInfinity(plannedDurationSeconds) ||
                plannedDurationSeconds < 0f)
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The current Pick Up execution does not match its planned finite duration.";
                }

                return false;
            }

            double elapsedSeconds = System.Math.Max(0d, Time.timeAsDouble - _startedAtTimeSeconds);
            remainingDurationSeconds = Mathf.Max(0f, plannedDurationSeconds - (float)elapsedSeconds);
            if (float.IsNaN(remainingDurationSeconds) || float.IsInfinity(remainingDurationSeconds))
            {
                remainingDurationSeconds = 0f;
                diagnostic = "The active Pick Up execution produced a non-finite remaining duration.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        protected override bool CanExecute(
            PegaObjectInteractionRequest request,
            out string diagnostic)
        {
            return TryAssessStart(
                request,
                out PegaObjectEntryPickUpStartStatus _,
                out diagnostic);
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
                    out PegaPickUpObjectEntryConfigurationSnapshot parameters,
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
                "Pick Up completed: the Object Entry was transferred to the Actor's physical inventory.");
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
                _log ??= PegaLogging.CreateScopedLogger<PegaObjectEntryPickUpAction>(
                    LogCategory,
                    gameObject.name);
                _log.Info(
                    "Pick Up completed: Object Entry was transferred to an Actor's physical inventory.",
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
                diagnostic = "Pick Up requires this Actor's physical inventory and a physical-item component on the exact Object Entry root.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryBuildConfiguration(
            out PegaPickUpObjectEntryConfigurationSnapshot parameters,
            out string diagnostic)
        {
            parameters = default;
            if (!IsNonNegativeFinite(interactionRangeMeters) ||
                !IsNonNegativeFinite(executionDurationSeconds))
            {
                diagnostic = "Pick Up requires finite, non-negative interaction range and execution duration values.";
                return false;
            }

            ObjectEntryDeclaration declaration = GetComponent<ObjectEntryDeclaration>();
            if (declaration == null ||
                !declaration.TryGetObjectEntryId(out ObjectEntryId objectEntryId) ||
                !objectEntryId.IsValid)
            {
                diagnostic = "Pick Up requires an ObjectEntryDeclaration on the same root as its action component.";
                return false;
            }

            parameters = new PegaPickUpObjectEntryConfigurationSnapshot(
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

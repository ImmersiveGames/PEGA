using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.Interactions.Runtime.ObjectEntry;
using UnityEngine;
namespace PEGA.Interactions.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorDeclaration))]
    [AddComponentMenu("PEGA/Actors/Physical Inventory")]
    public sealed class PegaActorPhysicalInventory : MonoBehaviour
    {
        [SerializeField, Min(1)]
        [Tooltip("Physical item slots available to this Actor.")]
        private int capacity = 1;

        [SerializeField]
        [Tooltip("Actor-local position used to display carried Object Entries; objects remain under their existing Activity owner.")]
        private Vector3 carriedItemAnchorOffset = new Vector3(0f, 1.15f, 0f);

        [SerializeField, Min(0.05f)]
        [Tooltip("Spacing between carried items in the physical display.")]
        private float carriedItemSpacingMeters = 0.25f;

        private readonly List<PegaObjectEntryPhysicalItem> _items =
            new List<PegaObjectEntryPhysicalItem>();

        public int Capacity => capacity;
        public int Count
        {
            get
            {
                PruneDestroyedItems();
                return _items.Count;
            }
        }

        public bool HasFreeCapacity =>
            TryValidateActor(out _) && Count < capacity;

        private void LateUpdate()
        {
            PruneDestroyedItems();
            for (int index = 0; index < _items.Count; index++)
            {
                PegaObjectEntryPhysicalItem item = _items[index];
                if (item == null)
                {
                    continue;
                }

                Vector3 localOffset = carriedItemAnchorOffset + Vector3.right *
                    (index * carriedItemSpacingMeters);
                item.transform.position = transform.TransformPoint(localOffset);
            }
        }

        public bool Contains(ObjectEntryId objectEntryId)
        {
            PruneDestroyedItems();
            if (!objectEntryId.IsValid)
            {
                return false;
            }

            for (int index = 0; index < _items.Count; index++)
            {
                if (_items[index] != null && _items[index].ObjectEntryId == objectEntryId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanAccept(PegaObjectEntryPhysicalItem item, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!TryValidateActor(out diagnostic) || item == null || !item.IsAvailableOnGround ||
                !item.TryGetObjectEntryId(out ObjectEntryId objectEntryId))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The item is not available on the ground or has no valid ObjectEntryId."
                    : diagnostic;
                return false;
            }

            if (!HasFreeCapacity)
            {
                diagnostic = "The Actor's physical inventory has no free item slot.";
                return false;
            }

            if (Contains(objectEntryId))
            {
                diagnostic = "This ObjectEntryId is already present in the Actor's physical inventory.";
                return false;
            }

            return true;
        }

        internal bool CanAcceptReserved(
            PegaObjectInteractionRequest request,
            PegaObjectEntryPhysicalItem item,
            PegaObjectInteractionAction action,
            long executionId,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!request.TryValidateCurrentAuthority(out diagnostic) ||
                request.ActorRoot != gameObject || request.TargetRoot != (item != null ? item.gameObject : null) ||
                request.ObjectEntryId != (item != null ? item.ObjectEntryId : default) ||
                !TryValidateActor(out diagnostic) || item == null ||
                !item.TryGetObjectEntryId(out ObjectEntryId objectEntryId) ||
                !item.HasReservation(action, executionId))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The reserved Object Entry is no longer available to this interaction execution."
                    : diagnostic;
                return false;
            }

            if (!HasFreeCapacity)
            {
                diagnostic = "The Actor's physical inventory has no free item slot.";
                return false;
            }

            if (Contains(objectEntryId))
            {
                diagnostic = "This ObjectEntryId is already present in the Actor's physical inventory.";
                return false;
            }

            return true;
        }

        internal bool TryAcceptReserved(
            PegaObjectInteractionRequest request,
            PegaObjectEntryPhysicalItem item,
            PegaObjectInteractionAction action,
            long executionId,
            out string diagnostic)
        {
            if (!CanAcceptReserved(request, item, action, executionId, out diagnostic))
            {
                return false;
            }

            if (_items.Count == _items.Capacity)
            {
                _items.Capacity = Mathf.Max(1, _items.Count + 1);
            }

            if (!item.CommitPossession(action, executionId))
            {
                diagnostic = "The item reservation changed before the inventory transaction could commit.";
                return false;
            }

            _items.Add(item);
            diagnostic = string.Empty;
            return true;
        }

        private void PruneDestroyedItems()
        {
            for (int index = _items.Count - 1; index >= 0; index--)
            {
                if (_items[index] == null)
                {
                    _items.RemoveAt(index);
                }
            }
        }

        private bool TryValidateActor(out string diagnostic)
        {
            diagnostic = string.Empty;
            ActorDeclaration declaration = GetComponent<ActorDeclaration>();
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy || declaration == null || capacity < 1 ||
                !IsFinite(carriedItemAnchorOffset) || !IsPositiveFinite(carriedItemSpacingMeters))
            {
                diagnostic = "The Actor requires an active Framework identity and a valid physical inventory configuration.";
                return false;
            }

            try
            {
                if (!declaration.ActorId.IsValid)
                {
                    diagnostic = "The Actor's Framework ActorId is invalid.";
                    return false;
                }
            }
            catch (System.ArgumentException exception)
            {
                diagnostic = exception.Message;
                return false;
            }

            return true;
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsPositiveFinite(float value) => value > 0f && IsFinite(value);
    }
}

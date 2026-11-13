using Immersive.Framework.ObjectEntry;
using UnityEngine;
namespace PEGA.Interactions.Runtime.ObjectEntry
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ObjectEntryDeclaration))]
    [AddComponentMenu("PEGA/Interaction/Object Entry/Physical Item")]
    public sealed class PegaObjectEntryPhysicalItem : MonoBehaviour
    {
        private ObjectEntryDeclaration _declaration;
        private bool _availableOnGround = true;
        private PegaObjectInteractionAction _reservationOwner;
        private long _reservationId;

        public bool IsAvailableOnGround => _availableOnGround && _reservationOwner == null;

        internal bool IsReserved => _reservationOwner != null;

        internal bool IsReservedBy(GameObject actorRoot) =>
            _reservationOwner != null &&
            _reservationOwner.IsExecutingForActor(actorRoot, _reservationId);

        public ObjectEntryId ObjectEntryId =>
            _declaration != null && _declaration.TryGetObjectEntryId(out ObjectEntryId id) ? id : default;

        public bool TryGetObjectEntryId(out ObjectEntryId objectEntryId)
        {
            objectEntryId = default;
            return _declaration != null && _declaration.TryGetObjectEntryId(out objectEntryId);
        }

        private void Awake()
        {
            _declaration = GetComponent<ObjectEntryDeclaration>();
        }

        internal bool TryReserve(PegaObjectInteractionAction action, long executionId)
        {
            if (!_availableOnGround || _reservationOwner != null || action == null || executionId <= 0)
            {
                return false;
            }

            _reservationOwner = action;
            _reservationId = executionId;
            return true;
        }

        internal bool HasReservation(PegaObjectInteractionAction action, long executionId) =>
            _availableOnGround && _reservationOwner == action && _reservationId == executionId;

        internal bool ReleaseReservation(PegaObjectInteractionAction action, long executionId)
        {
            if (!HasReservation(action, executionId))
            {
                return false;
            }

            _reservationOwner = null;
            _reservationId = 0;
            return true;
        }

        internal bool CommitPossession(PegaObjectInteractionAction action, long executionId)
        {
            if (!HasReservation(action, executionId))
            {
                return false;
            }

            _availableOnGround = false;
            _reservationOwner = null;
            _reservationId = 0;
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                ObjectEntryDeclaration nearestDeclaration = collider != null
                    ? collider.GetComponentInParent<ObjectEntryDeclaration>(true)
                    : null;
                if (collider != null && nearestDeclaration == _declaration)
                {
                    collider.enabled = false;
                }
            }

            return true;
        }
    }
}

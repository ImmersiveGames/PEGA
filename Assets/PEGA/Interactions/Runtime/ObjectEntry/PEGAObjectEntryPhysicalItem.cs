using System.Collections.Generic;
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
        private Collider[] _possessedColliders = System.Array.Empty<Collider>();
        private bool[] _colliderStatesBeforePossession = System.Array.Empty<bool>();
        private float _possessedPlanarRadiusMeters;
        private float _possessedLowestColliderOffsetMeters;

        public bool IsAvailableOnGround => _availableOnGround && _reservationOwner == null;

        internal bool IsReserved => _reservationOwner != null;

        internal float PossessedPlanarRadiusMeters => _possessedPlanarRadiusMeters;

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
            var possessedColliders = new List<Collider>();
            var colliderStates = new List<bool>();
            float planarRadiusMeters = 0f;
            float lowestColliderOffsetMeters = float.PositiveInfinity;
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                ObjectEntryDeclaration nearestDeclaration = collider != null
                    ? collider.GetComponentInParent<ObjectEntryDeclaration>(true)
                    : null;
                if (collider != null && nearestDeclaration == _declaration)
                {
                    possessedColliders.Add(collider);
                    colliderStates.Add(collider.enabled);
                    if (collider.enabled)
                    {
                        Bounds bounds = collider.bounds;
                        Vector3 centerOffset = bounds.center - transform.position;
                        float centerPlanarOffsetMeters =
                            new Vector2(centerOffset.x, centerOffset.z).magnitude;
                        float planarHalfExtentsMeters =
                            new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
                        planarRadiusMeters = Mathf.Max(
                            planarRadiusMeters,
                            centerPlanarOffsetMeters + planarHalfExtentsMeters);
                        lowestColliderOffsetMeters = Mathf.Min(
                            lowestColliderOffsetMeters,
                            bounds.min.y - transform.position.y);
                    }
                    collider.enabled = false;
                }
            }

            _possessedColliders = possessedColliders.ToArray();
            _colliderStatesBeforePossession = colliderStates.ToArray();
            _possessedPlanarRadiusMeters = planarRadiusMeters;
            _possessedLowestColliderOffsetMeters =
                float.IsInfinity(lowestColliderOffsetMeters) ? 0f : lowestColliderOffsetMeters;

            return true;
        }

        internal bool TryReleasePossession(Vector3 groundPosition)
        {
            if (_availableOnGround || _reservationOwner != null ||
                !IsFinite(groundPosition))
            {
                return false;
            }

            transform.position = groundPosition -
                Vector3.up * _possessedLowestColliderOffsetMeters;
            for (int index = 0; index < _possessedColliders.Length; index++)
            {
                Collider collider = _possessedColliders[index];
                if (collider != null && index < _colliderStatesBeforePossession.Length)
                {
                    collider.enabled = _colliderStatesBeforePossession[index];
                }
            }

            _possessedColliders = System.Array.Empty<Collider>();
            _colliderStatesBeforePossession = System.Array.Empty<bool>();
            _possessedPlanarRadiusMeters = 0f;
            _possessedLowestColliderOffsetMeters = 0f;
            _availableOnGround = true;
            return true;
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

using PEGA.ObjectSystems.Interfaces;
using PEGA.ObjectSystems.ObjectsScriptables;
using UnityEngine;
using UnityEngine.AI;

namespace PEGA.ObjectSystems
{
    [RequireComponent(typeof(CharacterController))]
    public class ObjectMovement : MonoBehaviour
    {
        protected internal Vector2 InputVector = Vector2.zero;
        private Camera _mainCamera;
        private ObjectMaster _objectMaster;
        private IMovementStrategy _movementStrategy;
        
        internal CharacterController CharacterController;
        internal ObjectDataScriptable ObjectData;
        internal NavMeshAgent NavMeshAgent;
        internal Transform Target;

        #region Unity Methods
        protected virtual void Awake()
        {
            SetInitialReferences();
        }

        protected virtual void FixedUpdate()
        {
            _movementStrategy.Gravity(this);
            _movementStrategy.Rotate(this);
            _movementStrategy.Move(this);
        }
        #endregion

        protected virtual void SetInitialReferences()
        {
            _objectMaster = GetComponent<ObjectMaster>();
            ObjectData = _objectMaster.objectData;
            _mainCamera = Camera.main;
        }

        protected void SetMovementStrategy(IMovementStrategy strategy)
        {
            _movementStrategy = strategy;
        }

        internal Vector3 CalculateMovement()
        {
            var forward = _mainCamera.transform.forward;
            var right = _mainCamera.transform.right;

            forward.y = 0;
            right.y = 0;

            forward.Normalize();
            right.Normalize();

            return forward * InputVector.y + right * InputVector.x;
        }
        

    }
}

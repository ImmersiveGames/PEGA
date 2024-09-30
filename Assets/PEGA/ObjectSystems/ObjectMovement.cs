using PEGA.ObjectSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems
{
    [RequireComponent(typeof(CharacterController))]
    public class ObjectMovement : MonoBehaviour, IObjectAction
    {
        protected Vector2 InputVector = Vector2.zero;
        private Camera _mainCamera;
        private CharacterController _characterController;

        private ObjectMaster _objectMaster;

        #region Unity Methods
        protected virtual void Awake()
        {
            SetInitialReferences();
        }

        protected virtual void FixedUpdate()
        {
            ApplyGravity();
            RotatePlayer();
            ExecuteAction();
        }
        #endregion

        protected virtual void SetInitialReferences()
        {
            _objectMaster = GetComponent<ObjectMaster>();
            _characterController = GetComponent<CharacterController>();
            _mainCamera = Camera.main;
        }

        protected virtual void ApplyGravity()
        {
            if (_characterController.isGrounded) return;

            var gravityVector = new Vector3(0, Physics.gravity.y * _objectMaster.objectData.gravityModifier * Time.deltaTime, 0);
            _characterController.Move(gravityVector);
        }

        protected virtual void RotatePlayer()
        {
            if (InputVector == Vector2.zero) return;

            var desiredDirection = new Vector3(InputVector.x, 0f, InputVector.y);
            var targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _objectMaster.objectData.rotationSpeed * Time.fixedDeltaTime);
        }

        private Vector3 CalculateMovement()
        {
            var forward = _mainCamera.transform.forward;
            var right = _mainCamera.transform.right;

            forward.y = 0;
            right.y = 0;

            forward.Normalize();
            right.Normalize();

            return forward * InputVector.y + right * InputVector.x;
        }

        public virtual void ExecuteAction()
        {
            // Calcula o movimento com o CharacterController
            var movement = CalculateMovement();
            _characterController.Move(movement * (_objectMaster.objectData.speed * Time.deltaTime));
        }
    }
}

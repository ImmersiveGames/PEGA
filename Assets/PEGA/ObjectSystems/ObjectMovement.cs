using PEGA.ObjectSystems.Interfaces;
using PEGA.ObjectSystems.Modifications;
using PEGA.ObjectSystems.ObjectsScriptables;
using UnityEngine;
using UnityEngine.AI;

namespace PEGA.ObjectSystems
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(ObjectGravity))]
    public class ObjectMovement : MonoBehaviour
    {
        [Header("Configurações Base")]
        [SerializeField] protected internal float moveSpeedBase = 5.0f; // Velocidade base de movimento
        
        protected internal Vector2 InputVector = Vector2.zero;
        private Camera _mainCamera;
        private ObjectMaster _objectMaster;
        private IMovementStrategy _movementStrategy;
        
        internal ObjectGravity ObjectGravity;
        internal CharacterController CharacterController;
        internal ModifierController ModifierController;
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
            if (_movementStrategy == null) return;
            // Executa rotação e movimento
            _movementStrategy.Rotate(this);
            _movementStrategy.Move(this);
        }


        #endregion

        protected virtual void SetInitialReferences()
        {
            _objectMaster = GetComponent<ObjectMaster>();
            ObjectData = _objectMaster.objectData;
            CharacterController = GetComponent<CharacterController>();
            ModifierController = GetComponent<ModifierController>();
            ObjectGravity = GetComponent<ObjectGravity>();
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

            forward.y = 0; // Elimina influência vertical
            right.y = 0;

            return (forward.normalized * InputVector.y + right.normalized * InputVector.x);
        }

        #region Modifiers (Power-Ups)

        /// <summary>
        /// Adiciona um modificador.
        /// </summary>
        public virtual void AddModifier(Modifier modifier)
        {
            ModifierController.AddModifier(modifier);
        }

        /// <summary>
        /// Remove um modificador.
        /// </summary>
        public virtual void RemoveModifier(string type)
        {
            ModifierController.RemoveModifier(type);
        }

        #endregion
    }
}

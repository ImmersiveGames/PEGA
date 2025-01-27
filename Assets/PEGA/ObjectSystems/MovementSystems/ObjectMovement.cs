using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.AnimatorSystem;
using PEGA.ObjectSystems.Interfaces;
using PEGA.ObjectSystems.Modifications;
using PEGA.ObjectSystems.MovementSystems.Strategies;
using PEGA.ObjectSystems.ObjectsScriptables;
using PEGA.ObjectSystems.PlayerSystems;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class ObjectMovement : MonoBehaviour
    {
        [Header("Movement Settings")] [SerializeField]
        protected string movementParameterName;

        [SerializeField] private MovementSettings movementSettings;
        
        private float _actualSpeed;
        private float _rotationPerFrame;
        private float _actualGravity;
        private Vector3 _actualMovement;
        private Vector3 _appliedMovement;

        private IMovementController _controller; //Controla os input de como vai ser manipulado o objeto podendo ser Input ou AI

        private CharacterController _characterController;
        private PlayerMaster _playerMaster;
        private AttributesBaseData _attributesBaseData;
        private ModifierController _modifierController;
        private AnimationHandler _animationHandler;

        private GravityHandler _gravityHandler;
        private MovementState _movementState;
        private JumpHandler _jumpHandler;
        private bool _isJumping;

        #region Unity Methods

        private void Awake()
        {
            InitializeComponents();
            InitializeAttributes();
            _actualGravity = _jumpHandler.Gravity;
        }

        private void OnEnable()
        {
            _controller.InitializeInput();
        }

        private void Update()
        {
            _modifierController.UpdateModifiers(Time.deltaTime);
            
            HandleRotate();
            UpdateAnimations();
            HandleAcceleration();

            // Move o personagem
            _characterController.Move(_appliedMovement * Time.deltaTime);

            UpdateStates();

            // Calcula a gravidade com base no estado atual
            _gravityHandler.CalculateGravity(ref _actualMovement, ref _appliedMovement, _actualGravity, _movementState);

            // Lida com a lógica de pulo via JumpHandler
            _jumpHandler.HandleJump(ref _actualMovement, ref _appliedMovement, _controller.IsJumpPressed, ref _isJumping);

            // Atualiza animações com base no estado de movimento
            UpdateAnimations();

            DebugManager.Log<ObjectMovement>($"Current Movement State: {_movementState.CurrentState}");
        }

        private void OnDisable()
        {
            _controller.DisableInput();
        }

        #endregion

        private void UpdateStates()
        {
            // Atualiza o estado de movimento com base no chão e no movimento vertical
            if (_characterController.isGrounded)
            {
                _movementState.CurrentState = MovementStateType.Grounded;
            }
            else if (_movementState.CurrentState == MovementStateType.Jumping && _actualMovement.y <= 0.0f)
            {
                _movementState.CurrentState = MovementStateType.FallingFromJump;
            }
            else if (_movementState.CurrentState != MovementStateType.Jumping && _actualMovement.y <= 0.0f)
            {
                _movementState.CurrentState = MovementStateType.FallingFree;
            }
        }
        
        private void HandleRotate()
        {
            Vector3 positionToLookAt;
            positionToLookAt.x = _actualMovement.x;
            positionToLookAt.y = 0.0f;
            positionToLookAt.z = _actualMovement.z;

            var currentRotation = transform.rotation;
            if (_controller.InputVector == Vector2.zero) return;
            var targetRotation = Quaternion.LookRotation(positionToLookAt);
            transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, _rotationPerFrame * Time.deltaTime);
        }

        private void HandleAcceleration()
        {
            //Debug.Log($"Gravity = {_actualMovement.y}");
            var speedModifier = _modifierController.GetModifierValue("Speed");
            _actualMovement.z = _controller.InputVector.y * _actualSpeed + speedModifier;
            _actualMovement.x = _controller.InputVector.x * _actualSpeed + speedModifier;

            _appliedMovement.x = _actualMovement.x;
            _appliedMovement.z = _actualMovement.z;
        }

        #region Animation Handlers

        private void UpdateAnimations()
        {
            _animationHandler.HandleMovementAnimation(_controller.InputVector);
        }

        #endregion

        #region Initialization

        private void InitializeComponents()
        {
            _playerMaster = GetComponent<PlayerMaster>();
            _modifierController = GetComponent<ModifierController>();
            _characterController = GetComponent<CharacterController>();
            _animationHandler = new AnimationHandler(GetComponent<ObjectAnimator>(), movementParameterName);
            _controller = new InputMovementController(GetComponent<PlayerInputHandler>());
            _controller.InitializeInput();

            _gravityHandler = new GravityHandler(movementSettings);
            _movementState = new MovementState();
            _jumpHandler = new JumpHandler(movementSettings, _modifierController, _movementState, _animationHandler);
        }

        private void InitializeAttributes()
        {
            var attributes = _playerMaster.attributesBaseData;

            _actualSpeed = movementSettings.baseSpeed + attributes.attAgility;
            _rotationPerFrame = movementSettings.baseSpeed + attributes.attAgility + attributes.attBase;
            _actualGravity = movementSettings.gravity;
        }

        #endregion
        
    }
}
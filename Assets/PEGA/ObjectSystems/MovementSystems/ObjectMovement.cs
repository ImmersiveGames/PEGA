using ImmersiveGames.InputSystems;
using ImmersiveGames.Utils;
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

        private bool _isJumping;
        private float _initialJumpVelocity;

        private IMovementController
            _controller; //Controla os input de como vai ser manipulado o objeto podendo ser Input ou AI

        private CharacterController _characterController;
        private PlayerMaster _playerMaster;
        private AttributesBaseData _attributesBaseData;
        private ModifierController _modifierController;
        private AnimationHandler _animationHandler;

        #region Unity Methods

        private void Awake()
        {
            InitializeComponents();
            InitializeAttributes();
            CalculateJumpVariables();
        }

        private void OnEnable()
        {
            _controller.InitializeInput();
        }

        private void Update()
        {
            HandleRotate();
            UpdateAnimations();
            HandleAcceleration();

            _characterController.Move(_appliedMovement * Time.deltaTime);

            HandleGravity(); // Aplicar a gravidade por último
            HandleJump(); // Chamar o pulo antes da gravidade
        }

        private void OnDisable()
        {
            _controller.DisableInput();
        }

        #endregion

        private void HandleGravity()
        {
            var isFalling = _actualMovement.y <= 0.0f || !_controller.IsJumpPressed;

            if (_characterController.isGrounded)
            {
                _actualMovement.y = movementSettings.gravityGround;
            }
            else if (isFalling)
            {
                _animationHandler.SetJumpState(false);
                var previousYVelocity = _actualMovement.y;
                _actualMovement.y += _actualGravity * movementSettings.fallMultiplier * Time.deltaTime;
                _appliedMovement.y = Mathf.Max(previousYVelocity + _actualMovement.y, movementSettings.maxFallVelocity);
            }
            else
            {
                var previousYVelocity = _actualMovement.y;
                _actualMovement.y += _actualGravity * Time.deltaTime;
                _appliedMovement.y = previousYVelocity + _actualMovement.y;
            }
        }

        private void HandleJump()
        {
            //Debug.Log($"CAido: {_characterController.isGrounded}, Pulando: {_isJumping}, Apertou: `{_controller.IsJumpPressed}");
            // Verifica se o personagem está no chão
            if (!_characterController.isGrounded) return;
            // Só permite pular se o botão foi pressionado novamente
            if (!_isJumping && _controller.IsJumpPressed)
            {
                _isJumping = true;
                _animationHandler.SetJumpState(true);
                _actualMovement.y = _initialJumpVelocity;
                _appliedMovement.y = _initialJumpVelocity;
            }
            else if (!_controller.IsJumpPressed) // Reseta o estado do pulo se o botão foi liberado
            {
                _isJumping = false;
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
        }

        private void InitializeAttributes()
        {
            var attributes = _playerMaster.attributesBaseData;

            _actualSpeed = movementSettings.baseSpeed + attributes.attAgility;
            _rotationPerFrame = movementSettings.baseSpeed + attributes.attAgility + attributes.attBase;
            _actualGravity = movementSettings.gravity;
        }

        private void CalculateJumpVariables()
        {
            
            JumpHelper.CalculateJumpVariables(
                movementSettings.maxJumpHeight, 
                movementSettings.maxJumpTime, 
                out _actualGravity, out _initialJumpVelocity);
        }

        #endregion
        
    }
}
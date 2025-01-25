using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.Interfaces;
using PEGA.ObjectSystems.Modifications;
using PEGA.ObjectSystems.ObjectsScriptables;
using PEGA.ObjectSystems.PlayerSystems;
using PEGA.ObjectSystems.Strategies.Controller;
using UnityEngine;

namespace PEGA.ObjectSystems
{
    public class MovementHandler : MonoBehaviour
    {
        [Header("Movement Settings")] [SerializeField]
        protected string movementParameterName;

        [SerializeField] protected float baseSpeed = 10f;
        [SerializeField] protected float gravity = -9.8f;
        [SerializeField] protected float gravityGround = -0.5f;

        [Header("Jump Settings")] [SerializeField]
        protected float initialJumpVelocity;

        [SerializeField] protected float maxJumpHeight = 1.0f;
        [SerializeField] protected float maxJumpTime = 0.5f;
        [SerializeField] protected float fallMultiplier = 2f;
        [SerializeField] protected float maxFallVelocity = -30f;

        private float _actualSpeed;
        private float _rotationPerFrame;
        private float _actualGravity;
        private Vector3 _actualMovement;
        private Vector3 _appliedMovement;

        private bool _isJumping;

        private IMovementController
            _controller; //Controla os input de como vai ser manipulado o objeto podendo ser Input ou AI

        private CharacterController _characterController;
        private PlayerMaster _playerMaster;
        private AttributesBaseData _attributesBaseData;
        private ModifierController _modifierController;
        private ObjectAnimator _objectAnimator;

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
            HandleAnimations();
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
                _actualMovement.y = gravityGround;
            }
            else if (isFalling)
            {
                _objectAnimator.JumpStartAnimation(false);
                var previousYVelocity = _actualMovement.y;
                _actualMovement.y += gravity * fallMultiplier * Time.deltaTime;
                _appliedMovement.y = Mathf.Max(previousYVelocity + _actualMovement.y, maxFallVelocity);
            }
            else
            {
                var previousYVelocity = _actualMovement.y;
                _actualMovement.y += gravity * Time.deltaTime;
                _appliedMovement.y = previousYVelocity + _actualMovement.y;
            }
        }

        private void HandleJump()
        {
            Debug.Log($"CAido: {_characterController.isGrounded}, Pulando: {_isJumping}, Apertou: `{_controller.IsJumpPressed}");
            // Verifica se o personagem está no chão
            if (!_characterController.isGrounded) return;
            // Só permite pular se o botão foi pressionado novamente
            if (!_isJumping && _controller.IsJumpPressed)
            {
                _objectAnimator.JumpStartAnimation(true);
                _isJumping = true;
                _actualMovement.y = initialJumpVelocity;
                _appliedMovement.y = initialJumpVelocity;
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

        private void HandleAnimations()
        {
            var moveMagnitude = _controller.InputVector.magnitude;
            if (_objectAnimator)
                _objectAnimator.WalkAnimation(movementParameterName,
                    moveMagnitude); // Define o parâmetro do Blend Tree de movimento
        }

        #endregion

        #region Initialization

        private void InitializeComponents()
        {
            _playerMaster = GetComponent<PlayerMaster>();
            _modifierController = GetComponent<ModifierController>();
            _characterController = GetComponent<CharacterController>();
            _objectAnimator = GetComponent<ObjectAnimator>();
            _controller = new InputMovementController(GetComponent<PlayerInputHandler>());
            _controller.InitializeInput();
        }

        private void InitializeAttributes()
        {
            var attributes = _playerMaster.attributesBaseData;
            _actualSpeed = baseSpeed + attributes.attAgility;
            _rotationPerFrame = baseSpeed + attributes.attAgility + attributes.attBase;
        }

        private void CalculateJumpVariables()
        {
            float timeToApex = maxJumpTime / 2;
            gravity = (-2 * maxJumpHeight) / Mathf.Pow(timeToApex, 2);
            initialJumpVelocity = (2 * maxJumpHeight) / timeToApex;
        }

        #endregion
        
    }
}
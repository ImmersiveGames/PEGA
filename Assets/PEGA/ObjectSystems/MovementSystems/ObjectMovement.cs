using System;
using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.AnimatorSystem;
using PEGA.ObjectSystems.Interfaces;
using PEGA.ObjectSystems.Modifications;
using PEGA.ObjectSystems.MovementSystems.Handlers;
using PEGA.ObjectSystems.MovementSystems.Strategies;
using PEGA.ObjectSystems.ObjectsScriptables;
using PEGA.ObjectSystems.PlayerSystems;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class ObjectMovement : MonoBehaviour
    {
        [Header("Movement Settings")] 
        [SerializeField] private string movementParameterName;

        [SerializeField] private MovementSettings movementSettings; // Movimento configurado no inspector

        private float _actualGravity;
        private Vector3 _actualMovement;
        private Vector3 _appliedMovement;
        private bool _isJumping;
        
        private VerticalMovementState _verticalMovementState;
        private HorizontalMovementState _horizontalMovementState;

        private IMovementController _controller; // Controlador de input
        private CharacterController _characterController;
        private ModifierController _modifierController;

        private AnimationHandler _animationHandler;
        private GravityHandler _gravityHandler;
        private JumpHandler _jumpHandler;
        private MovementHandler _movementHandler;
        
        public event Action<VerticalMovementStateType> OnVerticalStateChanged;
        public event Action<HorizontalMovementType> OnHorizontalStateChanged;

        #region Unity Methods

        private void Awake()
        {
            // Configura os componentes do sistema
            InitializeComponents();
        }

        private void OnEnable()
        {
            _controller.InitializeInput();
        }

        private void Update()
        {
            _modifierController.UpdateModifiers(Time.deltaTime);

            // Atualiza gravidade
            _gravityHandler.CalculateGravity(ref _actualMovement, ref _appliedMovement, _actualGravity, _verticalMovementState);

            // Gerencia o pulo
            _jumpHandler.HandleJump(ref _actualMovement, ref _appliedMovement, _controller.IsJumpPressed, isJumping: ref _isJumping);

            // Atualiza o estado do movimento
            UpdateVerticalState();
            UpdateHorizontalState();

            // Atualiza movimentação horizontal
            _movementHandler.UpdateMovement(ref _actualMovement, ref _appliedMovement);

            // Aplica o movimento calculado
            MovementHandler.ApplyMovement(_appliedMovement, _characterController);

            // Atualiza animações
            UpdateAnimations();

            // Debug opcional
#if UNITY_EDITOR
            DebugManager.Log<ObjectMovement>($"Current Movement State: {_verticalMovementState.CurrentState}");
#endif
        }

        private void OnDisable()
        {
            _controller.DisableInput();
        }

        #endregion

        #region Initialization

        private void Initialize(IMovementController controller, ModifierController modifierController, MovementSettings settings, AttributesBaseData attributes)
        {
            // Permite a inicialização via injeção de dependências
            _controller = controller;
            _modifierController = modifierController;
            movementSettings = settings;

            // Criação dos componentes necessários
            _animationHandler = new AnimationHandler(GetComponent<ObjectAnimator>());
            _gravityHandler = new GravityHandler(movementSettings);
            _verticalMovementState = new VerticalMovementState();
            _horizontalMovementState = new HorizontalMovementState(movementSettings.walkThreshold);
            _jumpHandler = new JumpHandler(movementSettings, _modifierController, _verticalMovementState);
            _movementHandler = new MovementHandler(transform, _controller, movementSettings, attributes, _modifierController);

            // Configura gravidade inicial
            _actualGravity = _jumpHandler.Gravity;
        }

        private void InitializeComponents()
        {
            // Inicializa automaticamente se não usar injeção de dependência
            var playerMaster = GetComponent<PlayerMaster>();
            Initialize(
                new InputMovementController(GetComponent<PlayerInputHandler>()),
                GetComponent<ModifierController>(),
                movementSettings,
                playerMaster.attributesBaseData
            );

            _characterController = GetComponent<CharacterController>();
        }

        #endregion
        #region State Updates

        private void UpdateVerticalState()
        {
            var previousState = _verticalMovementState.CurrentState;
            _verticalMovementState.UpdateState(_actualMovement, _isJumping, _characterController.isGrounded);

            if (_verticalMovementState.CurrentState == previousState) return;
            OnVerticalStateChanged?.Invoke(_verticalMovementState.CurrentState);
            Debug.Log($"Vertical State Changed: {_verticalMovementState.CurrentState}");
        }

        private void UpdateHorizontalState()
        {
            var previousState = _horizontalMovementState.CurrentState;
            _horizontalMovementState.UpdateState(_actualMovement);

            if (_horizontalMovementState.CurrentState == previousState) return;
            OnHorizontalStateChanged?.Invoke(_horizontalMovementState.CurrentState);
            Debug.Log($"Horizontal State Changed: {_horizontalMovementState.CurrentState}");
        }

        #endregion

        #region Animation Handlers

        private void UpdateAnimations()
        {
            // Atualiza animação com base no estado horizontal
            switch (_horizontalMovementState.CurrentState)
            {
                case HorizontalMovementType.Idle:
                    _animationHandler.SetIdle();
                    break;

                case HorizontalMovementType.Walking:
                case HorizontalMovementType.Running:
                    _animationHandler.HandleMovementAnimation(_controller.InputVector);
                    break;
                default:
                    _animationHandler.SetIdle();
                    break;
            }

            // Atualiza animação com base no estado vertical
            switch (_verticalMovementState.CurrentState)
            {
                case VerticalMovementStateType.Jumping:
                    _animationHandler.SetJumping(true);
                    break;
                case VerticalMovementStateType.FallingFromJump:
                case VerticalMovementStateType.FallingFree:
                    break;

                case VerticalMovementStateType.Grounded:
                    _animationHandler.SetJumping(false);
                    break;
                default:
                    _animationHandler.SetJumping(false);
                    break;
            }
        }


        #endregion
    }
}

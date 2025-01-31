﻿using System;
using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.AnimatorSystem;
using PEGA.ObjectSystems.Modifications;
using PEGA.ObjectSystems.MovementSystems.Drivers;
using PEGA.ObjectSystems.MovementSystems.Handlers;
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
        private bool _isDashing;
        
        private VerticalMovementState _verticalMovementState;
        private HorizontalMovementState _horizontalMovementState;

        private MovementControllerSwitcher _movementSwitcher;
        private CharacterInputHandler _characterInput;

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
            _characterInput.ActivateActionMap(ActionMapKey.Player);
        }

        private void Update()
        {
            _modifierController.UpdateModifiers(Time.deltaTime);

            // Atualiza gravidade
            _gravityHandler.CalculateGravity(ref _actualMovement, ref _appliedMovement, _actualGravity, _verticalMovementState);

            // Gerencia o pulo
            _jumpHandler.HandleJump(ref _actualMovement, ref _appliedMovement, _characterInput.IsActionPressed("Jump"), isJumping: ref _isJumping);

            // Atualiza o estado do movimento
            UpdateVerticalState();
            UpdateHorizontalState();

            // Atualiza movimentação horizontal
            _movementHandler.UpdateMovement(ref _actualMovement, ref _appliedMovement);

            // Sobrescreve o movimento no dash
            DashHandler();
            
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
            _characterInput.DeactivateCurrentActionMap();
        }

        #endregion

        #region Initialization

        private void Initialize(CharacterInputHandler characterInputHandler, ModifierController modifierController, MovementSettings settings, AttributesBaseData attributes)
        {
            // Permite a inicialização via injeção de dependências
            _movementSwitcher.SetDriver(new PlayerMovementDriver(characterInputHandler));
            _characterInput = characterInputHandler;
            _modifierController = modifierController;
            movementSettings = settings;

            // Criação dos componentes necessários
            _animationHandler = new AnimationHandler(GetComponent<ObjectAnimator>());
            _gravityHandler = new GravityHandler(movementSettings);
            _verticalMovementState = new VerticalMovementState();
            _horizontalMovementState = new HorizontalMovementState(movementSettings.walkThreshold);
            _jumpHandler = new JumpHandler(movementSettings, _modifierController, _verticalMovementState);
            _movementHandler = new MovementHandler(transform, _characterInput, movementSettings, attributes, _modifierController);

            // Configura gravidade inicial
            _actualGravity = _jumpHandler.Gravity;
        }

        private void InitializeComponents()
        {
            // Inicializa automaticamente se não usar injeção de dependência
            var playerMaster = GetComponent<PlayerMaster>();
            
            _movementSwitcher = new MovementControllerSwitcher();
            var characterInput = GetComponent<CharacterInputHandler>();
            _movementSwitcher.SetDriver(new PlayerMovementDriver(characterInput));
            
            Initialize(
                GetComponent<CharacterInputHandler>(),
                GetComponent<ModifierController>(),
                movementSettings,
                playerMaster.attributesBaseData
            );

            _characterController = GetComponent<CharacterController>();
        }
        #endregion

        private void DashHandler()
        {
            if (_characterInput.IsActionPressed("Dash") && !_isDashing)
            {
                _isDashing = true;

                // Calcula o multiplicador e aplica ao movimento diretamente
                _appliedMovement.x *= 4;
                _appliedMovement.z *= 4;
                _actualMovement.x *= 4;
                _actualMovement.z *= 4;

                _animationHandler.SetDashing(true);
                Debug.Log("Dash iniciado!");
            }
            else if (!_characterInput.IsActionPressed("Dash") && _isDashing)
            {
                _isDashing = false;

                _animationHandler.SetDashing(false);
                Debug.Log("Dash finalizado!");
            }
        }
        
        
        #region State Updates

        private void UpdateVerticalState()
        {
            var previousState = _verticalMovementState.CurrentState;
            _verticalMovementState.UpdateState(_actualMovement, _isJumping, _characterController.isGrounded);

            if (_verticalMovementState.CurrentState == previousState) return;
            OnVerticalStateChanged?.Invoke(_verticalMovementState.CurrentState);
            DebugManager.Log<ObjectMovement>($"Vertical State Changed: {_verticalMovementState.CurrentState}");
        }

        private void UpdateHorizontalState()
        {
            var previousState = _horizontalMovementState.CurrentState;
            
            _horizontalMovementState.UpdateState(_actualMovement,_isDashing);
            Debug.Log($"Previous: {previousState}, Atual : {_horizontalMovementState.CurrentState}");
            if (_horizontalMovementState.CurrentState == previousState) return;
            OnHorizontalStateChanged?.Invoke(_horizontalMovementState.CurrentState);
            DebugManager.Log<ObjectMovement>($"Horizontal State Changed: {_horizontalMovementState.CurrentState}");
        }

        #endregion

        #region Animation Handlers

        private void UpdateAnimations()
        {
            // Atualiza animação com base no estado horizontal
            switch (_horizontalMovementState.CurrentState)
            {
                case HorizontalMovementType.Idle:
                    //_animationHandler.SetIdle();
                    break;

                case HorizontalMovementType.Walking:
                case HorizontalMovementType.Running:
                    _animationHandler.HandleMovementAnimation(_characterInput.GetMovementDirection());
                    break;
                case HorizontalMovementType.Dashing:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
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
            }
        }


        #endregion
    }
}

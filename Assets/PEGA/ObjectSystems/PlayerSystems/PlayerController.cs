using System;
using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using PEGA.GamePlaySystems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.PlayerSystems
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float speed = 5f;
        public float gravityModifier = 1f;
        public float rotationSpeed = 10f;
        
        private Vector2 _inputVector = Vector2.zero;
        
        private CharacterController _characterController;
        private PlayerMaster _playerMaster;
        private GamePlayManager _gamePlayManager;
        private Camera _mainCamera;

        #region UnityMethods
        private void Awake()
        {
            SetInitialReferences();
        }

        private void Start()
        {
            InitializeInput();
        }

        private void FixedUpdate()
        {
            // Aplicar gravidade
            ApplyGravity();
            // Rotacionar o jogador para a direção de movimento
            RotatePlayer();
            // Movimentar o jogador
            MovePlayer();
            /*// Atualizar a animação do jogador
            UpdateAnimation();*/
        }

        private void OnDisable()
        {
            throw new NotImplementedException();
        }

        #endregion
        
        private void SetInitialReferences()
        {
            _gamePlayManager = GamePlayManager.Instance;
            _playerMaster = GetComponent<PlayerMaster>();
            _characterController = GetComponent<CharacterController>();
            _mainCamera = Camera.main;
        }
        private void InitializeInput()
        {
            InputGameManager.ActionManager.ActivateActionMap(ActionManager.GameActionMaps.Player);
            InputGameManager.RegisterAxisAction("Axis_Move", InputAxisPerformed, InputAxisCanceled);
        }

        private void ApplyGravity()
        {
            if (_characterController.isGrounded) return;
            // Aplicar a gravidade usando o CharacterController
            var gravityVector = new Vector3(0, Physics.gravity.y * gravityModifier * Time.deltaTime, 0);
            _characterController.Move(gravityVector);
        }
        private void RotatePlayer()
        {
            if (_inputVector == Vector2.zero) return;
            // Calcular a direção de rotação com base no input
            var desiredDirection = new Vector3(_inputVector.x, 0f, _inputVector.y);
            var targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
            // Rodar gradualmente em direção à nova direção
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
        private void MovePlayer()
        {
            // Calcula o movimento na direção para a frente do jogador
            var movement = CalculateMovement();
            _characterController.Move(movement * (speed * Time.deltaTime));
        }
        private Vector3 CalculateMovement()
        {
            var forward = _mainCamera.transform.forward;
            var right = _mainCamera.transform.right;

            forward.y = 0;
            right.y = 0;

            forward.Normalize();
            right.Normalize();

            return forward * _inputVector.y +
                   right * _inputVector.x;
        }

        #region Input Actions

        private void InputAxisPerformed(InputAction.CallbackContext context)
        {
            _inputVector = context.ReadValue<Vector2>().normalized;
            _playerMaster.OnEventPlayerMasterAxisMovement(_inputVector);
            DebugManager.Log<PlayerController>($"Eixo performed: {_inputVector}");
        }

        private void InputAxisCanceled(InputAction.CallbackContext context)
        {
            _inputVector = Vector2.zero;
            _playerMaster.OnEventPlayerMasterAxisMovement(_inputVector);
            DebugManager.Log<PlayerController>($"Eixo Canceled: {_inputVector}");
        }

        #endregion
    }
}
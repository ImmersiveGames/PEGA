using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.Strategies.Controller
{
    public class InputMovementController : IMovementController
    {
        private readonly PlayerInputHandler _playerInputHandler;
        private Vector2 _inputVector;

        public Vector3 MoveVector => new(_inputVector.x, 0f, _inputVector.y);
        public Vector2 InputVector => _inputVector;
        
        public bool IsJumpPressed { get; private set; }

        public InputMovementController(PlayerInputHandler inputHandler)
        {
            _playerInputHandler = inputHandler;
        }

        public void InitializeInput()
        {
            // Ativa o mapa de ação local para este jogador
            _playerInputHandler.ActivateActionMap("Player");
            
            // Registro de ações do tipo eixo
            _playerInputHandler.ActionManager.RegisterAction("Axis_Move_Performed", InputAxisPerformed);
            _playerInputHandler.ActionManager.RegisterAction("Axis_Move_Cancel", InputAxisCanceled);
            _playerInputHandler.ActionManager.RegisterAction("Axis_Move_Start", InputAxisStart);
            
            _playerInputHandler.ActionManager.RegisterAction("Jump_Start", OnJumpInput);
            _playerInputHandler.ActionManager.RegisterAction("Jump_Cancel", OnJumpInput);

            DebugManager.Log<InputMovementController>($"Mapa de ação atual: {_playerInputHandler.ActionManager.IsLocalActionMapActive(ActionManager.GameActionMaps.Player)}");
        }

        public void DisableInput()
        {
            _playerInputHandler.ActionManager.UnregisterAction("Axis_Move_Performed", InputAxisPerformed);
            _playerInputHandler.ActionManager.UnregisterAction("Axis_Move_Cancel", InputAxisCanceled);
            _playerInputHandler.ActionManager.UnregisterAction("Axis_Move_Start", InputAxisStart);
            
            _playerInputHandler.ActionManager.UnregisterAction("Jump_Start", OnJumpInput);
            _playerInputHandler.ActionManager.UnregisterAction("Jump_Cancel", OnJumpInput);
        }

        private void InputAxisStart(InputAction.CallbackContext context)
        {
            _inputVector = context.ReadValue<Vector2>();
        }
        private void InputAxisCanceled(InputAction.CallbackContext context)
        {
            _inputVector = context.ReadValue<Vector2>();
        }
        private void InputAxisPerformed(InputAction.CallbackContext context)
        {
            _inputVector = context.ReadValue<Vector2>().normalized;
        }
        private void OnJumpInput(InputAction.CallbackContext context)
        {
            IsJumpPressed = context.ReadValueAsButton();
            Debug.Log($"Botão de pulo pressionado: {IsJumpPressed}");
        }
    }
}
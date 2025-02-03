using System;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems
{
    public class CharacterInputHandler
    {
        private readonly PlayerInput _playerInput;
        private readonly ActionMapManager _actionMapManager;
        public ActionManager ActionManager { get; private set; }

        public event Action<string, InputAction.CallbackContext> OnInputReceived;

        public CharacterInputHandler(PlayerInput playerInput)
        {
            _playerInput = playerInput ?? throw new ArgumentNullException(nameof(playerInput));
            _actionMapManager = new ActionMapManager(_playerInput);
            ActionManager = new ActionManager(_playerInput);

            // Disparar eventos de input
            ActionManager.OnActionTriggered += (action, context) => OnInputReceived?.Invoke(action, context);
        }

        public void ActivateActionMap(ActionMapKey actionMap) => _actionMapManager.ActivateActionMap(actionMap);
        public void DeactivateCurrentActionMap() => _actionMapManager.RestorePreviousActionMap();

        public void RegisterAction(string actionName, Action<InputAction.CallbackContext> callback)
        {
            ActionManager.RegisterAction(actionName, callback);
        }

        public void UnregisterAction(string actionName, Action<InputAction.CallbackContext> callback)
        {
            ActionManager.UnregisterAction(actionName, callback);
        }
        

        public Vector2 GetMovementDirection() => GetActionValue<Vector2>(ActionsNames.AxisMove);
        public bool IsActionPressed(ActionsNames actionName) => GetActionValue<float>(actionName) > 0.5f;

        private T GetActionValue<T>(ActionsNames actionName) where T : struct
        {
            if (_playerInput == null || _playerInput.actions == null) return default;
            var action = _playerInput.actions[actionName.ToString()];
            return action?.ReadValue<T>() ?? default;
        }
    }
}
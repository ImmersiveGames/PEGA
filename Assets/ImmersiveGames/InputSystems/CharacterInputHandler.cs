using System;
using ImmersiveGames.InputSystems.Interface;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems
{
    public class CharacterInputHandler : MonoBehaviour, IControllerInput
    {
        private PlayerInput _playerInput;
        private ActionMapManager _actionMapManager;

        public ActionManager ActionManager { get; private set; }
        public event Action<string, InputAction.CallbackContext> OnInputReceived;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            if (_playerInput == null)
            {
                Debug.LogError("[CharacterInputHandler] PlayerInput não encontrado!");
                return;
            }

            ActionManager = new ActionManager(_playerInput);
            _actionMapManager = new ActionMapManager(_playerInput);
            ActionManager.OnActionTriggered += (action, context) => OnInputReceived?.Invoke(action, context);
        }

        public void ActivateActionMap(ActionMapKey actionMapName)
        {
            if (_actionMapManager.IsActionMapActive(actionMapName)) return; // Já está ativo, não faz nada
            _actionMapManager.ActivateActionMap(actionMapName);
        }


        public void DeactivateCurrentActionMap()
        {
            _actionMapManager.RestorePreviousActionMap();
        }
        
        private T GetActionValue<T>(string actionName) where T : struct
        {
            if (_playerInput == null || _playerInput.actions == null) return default;
    
            var action = _playerInput.actions[actionName];
            return action?.ReadValue<T>() ?? default;
        }
        
        public Vector2 GetMovementDirection() => GetActionValue<Vector2>("Axis_Move");
        public bool IsActionPressed(string actionName) => GetActionValue<float>(actionName) > 0.5f;

        public void SetEnabled(bool state)
        {
            _playerInput.enabled = state;
        }
    }
}
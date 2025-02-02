using System;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.MovementSystems.Drivers;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class MovementController : MonoBehaviour
    {
        private MovementControllerSwitcher _movementSwitcher;
        private CharacterInputHandler _characterInput;
        private IMovementDriver _currentDriver;

        private void Awake()
        {
            _movementSwitcher = new MovementControllerSwitcher();
            var playerInput = GetComponent<PlayerInput>();

            if (playerInput != null)
            {
                _characterInput = new CharacterInputHandler(playerInput);
                SetDriver(new PlayerMovementDriver(_characterInput));
            }
        }

        private void OnEnable()
        {
            _characterInput?.ActivateActionMap(ActionMapKey.Player);
        }

        private void OnDisable()
        {
            _characterInput?.DeactivateCurrentActionMap();
        }

        public void SetDriver(IMovementDriver driver)
        {
            if (_currentDriver == driver) return;
            _movementSwitcher.SetDriver(driver);
            _currentDriver = driver;
        }

        // 🔥 ÚNICO lugar onde os inputs são expostos!
        public Vector2 GetMovementPressing() => _currentDriver?.GetMovementPressing() ?? Vector2.zero;
        public bool IsJumpPressing() => _currentDriver?.IsJumpPressing() ?? false;
        public bool IsDashPressing() => _currentDriver?.IsDashPressing() ?? false;

        // 🔥 Expondo métodos para registrar e remover ações dinamicamente
        public void RegisterAction(string actionName, Action<InputAction.CallbackContext> callback)
        {
            _characterInput?.RegisterAction(actionName, callback);
        }

        public void UnregisterAction(string actionName, Action<InputAction.CallbackContext> callback)
        {
            _characterInput?.UnregisterAction(actionName, callback);
        }

        public IMovementDriver GetCurrentDriver => _movementSwitcher.GetCurrentDriver();

    }
}

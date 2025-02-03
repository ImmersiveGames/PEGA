using System;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems
{
    public sealed class ActionMapManager
    {
        private readonly PlayerInput _playerInput;
        private string _previousActionMap;

        public ActionMapManager(PlayerInput playerInput)
        {
            _playerInput = playerInput ?? throw new ArgumentNullException(nameof(playerInput));
        }

        public void ActivateActionMap(ActionMapKey actionMapName)
        {
            if (_playerInput.currentActionMap?.name == actionMapName.ToString()) return;

            _previousActionMap = _playerInput.currentActionMap?.name;
            _playerInput.SwitchCurrentActionMap(actionMapName.ToString());
        }

        public void RestorePreviousActionMap()
        {
            if (!string.IsNullOrEmpty(_previousActionMap))
            {
                _playerInput.SwitchCurrentActionMap(_previousActionMap);
            }
        }

        public bool IsActionMapActive(ActionMapKey actionMapName)
        {
            return _playerInput.currentActionMap?.name == actionMapName.ToString();
        }
    }

    public enum ActionMapKey
    {
        Player,
        UiControls
    }
}
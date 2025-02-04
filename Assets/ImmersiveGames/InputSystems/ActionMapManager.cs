using System;
using System.Collections.Generic;
using ImmersiveGames.DebugSystems;
using PEGA.InputActions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems
{
    public sealed class ActionMapManager
    {
        private readonly PlayerInput _playerInput;
        private readonly Stack<string> _previousActionMaps = new Stack<string>();

        public ActionMapManager(PlayerInput playerInput)
        {
            _playerInput = playerInput ?? throw new ArgumentNullException(nameof(playerInput));
        }

        public void ActivateActionMap(ActionMapKey actionMapName)
        {
            var currentMap = _playerInput.currentActionMap?.name;
            DebugManager.Log<ActionMapManager>($"Mapa Atual {currentMap}");
            if (currentMap == actionMapName.ToString()) return;

            if (!string.IsNullOrEmpty(currentMap))
                _previousActionMaps.Push(currentMap);

            _playerInput.SwitchCurrentActionMap(actionMapName.ToString());
            DebugManager.Log<ActionMapManager>($"Ativou o Mapa de Ação para {actionMapName}");
        }

        public void RestorePreviousActionMap()
        {
            if (_previousActionMaps.Count <= 0) return;
            var previousMap = _previousActionMaps.Pop();
            _playerInput.SwitchCurrentActionMap(previousMap);
            DebugManager.Log<ActionMapManager>($"Reverteu o Mapa de Ação para {previousMap}");
        }

        public bool IsActionMapActive(ActionMapKey actionMapName)
        {
            return _playerInput.currentActionMap?.name == actionMapName.ToString();
        }

        public void ClearHistory() => _previousActionMaps.Clear();
    }
    
}
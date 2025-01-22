using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveGames.DebugSystems;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems
{
    public sealed class ActionManager
    {
        #region Events

        public event Action<string, InputAction.CallbackContext> OnActionTriggered;
        public event Action OnActionComplete;

        #endregion

        #region Fields

        private readonly Dictionary<string, List<Action<InputAction.CallbackContext>>> _actionListeners = new();
        private readonly PlayerInput _playerInput;

        private GameActionMaps _currentActionMap;
        private GameActionMaps _previousActionMap;

        #endregion

        #region Constructor

        public ActionManager(PlayerInput playerInput)
        {
            _playerInput = playerInput ?? throw new ArgumentNullException(nameof(playerInput));
            RegisterAllActions();
        }

        #endregion

        #region Public API - Registration

        public void RegisterAction(string actionName, Action<InputAction.CallbackContext> callback)
        {
            if (!_actionListeners.ContainsKey(actionName))
                _actionListeners[actionName] = new List<Action<InputAction.CallbackContext>>();

            if (!_actionListeners[actionName].Contains(callback))
                _actionListeners[actionName].Add(callback);
        }

        public void UnregisterAction(string actionName, Action<InputAction.CallbackContext> callback)
        {
            if (_actionListeners.TryGetValue(actionName, out var actionListener))
                actionListener.Remove(callback);
        }

        #endregion

        #region Public API - Local Action Map Management

        public void ActivateLocalActionMap(GameActionMaps actionMapName)
        {
            if (_currentActionMap == actionMapName) return;

            _previousActionMap = _currentActionMap;
            _currentActionMap = actionMapName;

            SwitchActionMap(actionMapName);
            DebugManager.Log<ActionManager>($"[Local ActionMap] '{actionMapName}' ativado para o jogador.");
        }

        public void RestoreLocalActionMap()
        {
            ActivateLocalActionMap(_previousActionMap);
        }

        public bool IsLocalActionMapActive(GameActionMaps actionMapName)
        {
            return _currentActionMap == actionMapName;
        }

        #endregion

        #region Internal Logic

        private void RegisterAllActions()
        {
            var actionMaps = _playerInput.actions.actionMaps;

            foreach (var action in actionMaps.SelectMany(map => map.actions))
            {
                RegisterCallbacks(action, "_Start", "_Performed", "_Cancel");
            }
        }

        private void RegisterCallbacks(InputAction action, params string[] suffixes)
        {
            foreach (var suffix in suffixes)
            {
                switch (suffix)
                {
                    case "_Start":
                        action.started += context => NotifyListeners(action.name + suffix, context);
                        break;
                    case "_Performed":
                        action.performed += context => NotifyListeners(action.name + suffix, context);
                        break;
                    case "_Cancel":
                        action.canceled += context => NotifyListeners(action.name + suffix, context);
                        break;
                }
            }
        }

        private void NotifyListeners(string actionName, InputAction.CallbackContext context)
        {
            OnActionTriggered?.Invoke(actionName, context);

            if (!_actionListeners.TryGetValue(actionName, out var listeners)) return;
            foreach (var listener in listeners)
                listener?.Invoke(context);
        }

        private void SwitchActionMap(GameActionMaps actionMapName)
        {
            var actionMap = _playerInput.actions.FindActionMap(actionMapName.ToString());
            if (actionMap == null)
            {
                DebugManager.LogWarning<ActionManager>($"[ActionManager] ActionMap '{actionMapName}' não encontrado.");
                return;
            }

            _playerInput.SwitchCurrentActionMap(actionMapName.ToString());
        }

        #endregion

        #region Enums

        public enum GameActionMaps
        {
            Player,
            UiControls,
            BriefingRoom,
            Notifications,
            Shopping,
            HubControl
        }

        #endregion

        private void OnOnActionComplete()
        {
            OnActionComplete?.Invoke();
        }
    }
}

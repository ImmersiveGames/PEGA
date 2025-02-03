using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems
{
    public sealed class ActionManager
    {
        public event Action<string, InputAction.CallbackContext> OnActionTriggered;

        private readonly Dictionary<string, List<Action<InputAction.CallbackContext>>> _actionListeners = new();
        private readonly PlayerInput _playerInput;

        public ActionManager(PlayerInput playerInput)
        {
            _playerInput = playerInput ?? throw new ArgumentNullException(nameof(playerInput));
            RegisterAllActions();
        }

        public void RegisterAction(string actionName, Action<InputAction.CallbackContext> callback)
        {
            if (!_actionListeners.ContainsKey(actionName))
                _actionListeners[actionName] = new List<Action<InputAction.CallbackContext>>();

            _actionListeners[actionName].Add(callback);
        }

        public void UnregisterAction(string actionName, Action<InputAction.CallbackContext> callback)
        {
            if (!_actionListeners.TryGetValue(actionName, out var actionListener)) return;
            
            actionListener.Remove(callback);
            if (_actionListeners[actionName].Count == 0)
                _actionListeners.Remove(actionName);
        }

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
            {
                listener?.Invoke(context);
            }
        }
    }
}
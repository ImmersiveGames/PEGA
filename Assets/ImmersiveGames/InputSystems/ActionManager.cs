using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveGames.DebugSystems;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems
{
    public sealed class ActionManager
    {
        #region Events and Actions Handling
        
        internal event Action<string, InputAction.CallbackContext> EventOnActionTriggered;
        internal event Action EventOnActionComplete;
        
        private static readonly Dictionary<string, List<Action<InputAction.CallbackContext>>> ActionListeners =
                    new Dictionary<string, List<Action<InputAction.CallbackContext>>>();
        internal void RegisterAction(string actionName, Action<InputAction.CallbackContext> callback)
        {
            if (!ActionListeners.TryGetValue(actionName, out var listener))
            {
                listener = new List<Action<InputAction.CallbackContext>>();
                ActionListeners[actionName] = listener;
            }

            listener.Add(callback);
        }

        public void UnregisterAction(string actionName, Action<InputAction.CallbackContext> callback)
        {
            if (ActionListeners.TryGetValue(actionName, out var listener))
            {
                listener.Remove(callback);
            }
        }

        private void NotifyObservers(string actionName, InputAction.CallbackContext context)
        {
            // Notifique todos os observadores globais
            EventOnActionTriggered?.Invoke(actionName, context);

            // Notifique observadores específicos para a ação
            if (!ActionListeners.TryGetValue(actionName, out var listeners)) return;
            foreach (var listener in listeners)
            {
                listener?.Invoke(context);
            }
        }
        #endregion
        
        #region Action Map Management
        
        private const GameActionMaps DefaultGameActionMaps = GameActionMaps.UiControls;
        private GameActionMaps _lastGameActionMaps;
        private GameActionMaps _currentGameActionMaps;
        public void RestoreActionMap()
        {
            ActivateActionMap(_lastGameActionMaps);
        }

        public bool IsActiveActionMap(GameActionMaps actionMapName)
        {
            return _currentGameActionMaps == actionMapName;
        }

        public void ActivateActionMap(GameActionMaps actionMapName)
        {
            // Check if the requested action map is already active
            if (_currentGameActionMaps == actionMapName)
            {
                DebugManager.Log<ActionManager>($"[Action Map] '{actionMapName}' is already active.");
                return;
            }

            _lastGameActionMaps = _currentGameActionMaps;

            // Disable all action maps
            foreach (var actionMap in _inputActions.asset.actionMaps)
            {
                actionMap.Disable();
            }

            // Activate the requested action map
            var mapToActivate = _inputActions.asset.FindActionMap(actionMapName.ToString());
            if (mapToActivate != null)
            {
                mapToActivate.Enable();
                _currentGameActionMaps = actionMapName;
            }
            else
            {
                DebugManager.LogWarning<ActionManager>($"Action Map '{actionMapName}' not found.");
            }

            DebugManager.Log<ActionManager>($"[Action Map] '{actionMapName}' activated.");
        }
        
        #endregion
        
        #region Initialization and All Registration

        // Utiliza a instância de PlayersInputActions do initializer
        private readonly PegaInputActions _inputActions;
        internal ActionManager(PegaInputActions inputActions)
        {
            _inputActions = inputActions;
            RegisterAllActions();
        }

        private void RegisterAllActions()
        {
            var actionMaps = _inputActions.asset.actionMaps;
            foreach (var action in actionMaps.Select(actionMap => actionMap.actions).SelectMany(actions => actions))
            {
                if (IsSpecialAction(action))
                {
                    action.started += (context) => HandleActions(action, context);
                }
                else
                if (IsHoldAction(action))
                {
                    action.started += (context) => HandleActions(action, context);
                    action.canceled += (context) => HandleActions(action, context);
                }
                else
                if (IsAxisAction(action))
                {
                    action.performed += (context) => HandleActions(action, context);
                    action.canceled += (context) => HandleActions(action, context);
                }
                else
                {
                    action.performed += (context) => NotifyObservers(action.name, context);
                }
            }

            // Adicione um método para ativar/desativar Action Maps
            ActivateActionMap(DefaultGameActionMaps); // Troque "DefaultMap" pelo nome do seu Action Map padrão
        }
        
        #endregion

        #region Special Actions Handling

        /*private void HandleSpecialAction(InputAction action, InputAction.CallbackContext context)
        {
            // Lógica específica para ação especial;
            DebugManager.Log<ActionManager>($"Special Action {action.name} Performed in context {context}");
        }*/

        private static bool IsSpecialAction(InputAction action)
        {
            // Adicione aqui a lógica para determinar se uma ação é especial ou não
            return action.name.StartsWith("Special");
        }
        private void HandleActions(InputAction action, InputAction.CallbackContext context)
        {
            var completeName = action.name;

            if (context.performed)
            {
                completeName += "_Performed";
                //DebugManager.Log<ActionManager>($"Action {completeName} Performed in Performed");
            }
            else if (context.started)
            {
                completeName += "_Start";
                //DebugManager.Log<ActionManager>($"Action {completeName} Performed in Started");
            }
            else if (context.canceled)
            {
                completeName += "_Cancel";
                //DebugManager.Log<ActionManager>($"Action {completeName} Performed in Cancel");
            }

            if (!ActionListeners.TryGetValue(completeName, out var listener)) return;
            foreach (var activeAction in listener)
            {
                activeAction?.Invoke(context);
            }
        }

        private bool IsHoldAction(InputAction action)
        {
            // Adicione aqui a lógica para determinar se uma ação é especial ou não
            return action.name.StartsWith("Hold_");
        }
        private bool IsAxisAction(InputAction action)
        {
            // Adicione aqui a lógica para determinar se uma ação é de eixo ou não
            return action.name.StartsWith("Axis_");
        }

        #endregion

        #region Axis Input Handling

        /*private void HandleAxisAction(InputAction action, InputAction.CallbackContext context)
        {
            // Lógica específica para ação especial;
            DebugManager.Log<ActionManager>($"Axis Action {action.name} Performed in context {context}");
        }*/

        #endregion
        public enum GameActionMaps
        {
            Player, UiControls, BriefingRoom, Notifications, Shopping, HubControl
        }

        public void OnEventOnActionComplete()
        {
            EventOnActionComplete?.Invoke();
        }
    }
    
}

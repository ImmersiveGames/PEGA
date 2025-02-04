using System;
using System.Collections.Generic;
using PEGA.InputActions;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems
{
    public sealed class ActionManager
    {
        // Evento geral para notificar quando qualquer ação for disparada.
        public event Action<ActionsKey, InputAction.CallbackContext> OnActionTriggered;

        // Dicionário para armazenar listeners com base na tupla (ActionsKey, ActionPhase).
        private readonly Dictionary<(ActionsKey, ActionPhase), List<Action<InputAction.CallbackContext>>> _actionListeners =
            new Dictionary<(ActionsKey, ActionPhase), List<Action<InputAction.CallbackContext>>>();

        private readonly InputActionAsset _inputActionAsset;

        /// <summary>
        /// Construtor que recebe o InputActionAsset e registra todas as ações.
        /// </summary>
        public ActionManager(InputActionAsset inputActionAsset)
        {
            _inputActionAsset = inputActionAsset ?? throw new ArgumentNullException(nameof(inputActionAsset));
            RegisterAllActions();
        }

        /// <summary>
        /// Registra um callback para uma ação e fase específica.
        /// </summary>
        public void RegisterAction(ActionsKey actionKey, ActionPhase phase, Action<InputAction.CallbackContext> callback)
        {
            var key = (actionKey, phase);
            if (!_actionListeners.TryGetValue(key, out var list))
            {
                list = new List<Action<InputAction.CallbackContext>>();
                _actionListeners[key] = list;
            }
            list.Add(callback);
        }

        /// <summary>
        /// Remove um callback para uma ação e fase específica.
        /// </summary>
        public void UnregisterAction(ActionsKey actionKey, ActionPhase phase, Action<InputAction.CallbackContext> callback)
        {
            var key = (actionKey, phase);
            if (_actionListeners.TryGetValue(key, out var list))
            {
                list.Remove(callback);
                if (list.Count == 0)
                {
                    _actionListeners.Remove(key);
                }
            }
        }

        /// <summary>
        /// Registra os callbacks para todas as InputActions contidas no InputActionAsset.
        /// </summary>
        private void RegisterAllActions()
        {
            foreach (var actionMap in _inputActionAsset.actionMaps)
            {
                foreach (var action in actionMap.actions)
                {
                    if (Enum.TryParse(action.name, out ActionsKey actionKey))
                    {
                        RegisterActionCallbacks(action, actionKey);
                    }
                }
            }
        }

        /// <summary>
        /// Associa os eventos de uma InputAction às notificações dos listeners para cada fase.
        /// </summary>
        private void RegisterActionCallbacks(InputAction action, ActionsKey actionKey)
        {
            action.started += context => NotifyListeners(actionKey, ActionPhase.Started, context);
            action.performed += context => NotifyListeners(actionKey, ActionPhase.Performed, context);
            action.canceled += context => NotifyListeners(actionKey, ActionPhase.Canceled, context);
        }

        /// <summary>
        /// Notifica os listeners registrados para a ação e fase especificadas.
        /// </summary>
        public void NotifyListeners(ActionsKey actionKey, ActionPhase phase, InputAction.CallbackContext context)
        {
            OnActionTriggered?.Invoke(actionKey, context);

            var key = (actionKey, phase);
            if (_actionListeners.TryGetValue(key, out var listeners))
            {
                foreach (var listener in listeners)
                {
                    listener?.Invoke(context);
                }
            }
        }

        /// <summary>
        /// Verifica se uma determinada ação está ativa (pressionada).
        /// </summary>
        public bool IsActionActive(ActionsKey actionKey)
        {
            foreach (var actionMap in _inputActionAsset.actionMaps)
            {
                var action = actionMap.FindAction(actionKey.ToString());
                if (action != null && action.IsPressed())
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Retorna o valor de uma ação do tipo T.
        /// </summary>
        public T GetActionValue<T>(ActionsKey actionKey) where T : struct
        {
            foreach (var actionMap in _inputActionAsset.actionMaps)
            {
                var action = actionMap.FindAction(actionKey.ToString());
                if (action != null)
                {
                    return action.ReadValue<T>();
                }
            }
            return default;
        }
    }

    /// <summary>
    /// Define as fases de execução de uma ação.
    /// </summary>
    public enum ActionPhase
    {
        Started,
        Performed,
        Canceled
    }
}

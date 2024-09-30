using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems
{
    public class InputGameManager : MonoBehaviour
    {
        private static PegaInputActions _inputActions;
        private static ActionManager _actionManager;

        // Propriedades para acessar instâncias únicas de PlayersInputActions e ActionManager
        private static PegaInputActions InputActions
        {
            get
            {
                if (_inputActions != null) return _inputActions;
                _inputActions = new PegaInputActions();
                _inputActions.Enable();
                return _inputActions;
            }
        }

        internal static ActionManager ActionManager
        {
            get
            {
                if (_actionManager != null) return _actionManager;
                _actionManager = new ActionManager(InputActions);
                return _actionManager;
            }
        }
        protected internal static void RegisterAction(string actionName, Action<InputAction.CallbackContext> callbackComplete, Action<InputAction.CallbackContext> callbackCancel)
        {
            ActionManager.RegisterAction($"{actionName}_Start", callbackComplete);
            ActionManager.RegisterAction($"{actionName}_Cancel", callbackCancel);
        }
        protected internal static void RegisterAxisAction(string actionName, Action<InputAction.CallbackContext> callbackPerformed, Action<InputAction.CallbackContext> callbackCancel)
        {
            ActionManager.RegisterAction($"{actionName}_Performed", callbackPerformed);
            ActionManager.RegisterAction($"{actionName}_Cancel", callbackCancel);
        }
        protected internal static void RegisterAction(string actionName, Action<InputAction.CallbackContext> callback)
        {
            ActionManager.RegisterAction(actionName, callback);
        }
        protected internal static void UnregisterAction(string actionName, Action<InputAction.CallbackContext> callback)
        {
            ActionManager.UnregisterAction(actionName, callback);
        }
        protected internal static void UnregisterHoldAction(string actionName, Action<InputAction.CallbackContext> callbackComplete, Action<InputAction.CallbackContext> callbackCancel)
        {
            ActionManager.UnregisterAction($"{actionName}_Start", callbackComplete);
            ActionManager.UnregisterAction($"{actionName}_Cancel", callbackCancel);
        }
        protected internal static void UnregisterAxisAction(string actionName, Action<InputAction.CallbackContext> callbackPerformed, Action<InputAction.CallbackContext> callbackCancel)
        {
            ActionManager.UnregisterAction($"{actionName}_Performed", callbackPerformed);
            ActionManager.UnregisterAction($"{actionName}_Cancel", callbackCancel);
        }

        protected static void DisableInputs()
        {
            _inputActions?.Disable();
        }

        private void OnDestroy()
        {
            // Desativa os Inputs ao desabilitar o objeto
            _inputActions?.Disable();
            _actionManager = null;
        }
    }
}
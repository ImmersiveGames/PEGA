using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems
{
    public class PlayerInputHandler : MonoBehaviour
    {
        #region Fields

        private PlayerInput _playerInput;
        private ActionManager _actionManager;

        #endregion

        #region Properties

        public ActionManager ActionManager => _actionManager ??= new ActionManager(_playerInput);

        public int PlayerIndex => _playerInput?.playerIndex ?? -1;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            if (_playerInput == null)
            {
                Debug.LogError("[PlayerInputHandler] PlayerInput não encontrado!");
                return;
            }

            _actionManager = new ActionManager(_playerInput);
        }

        private void OnDestroy()
        {
            _actionManager = null;
        }

        #endregion

        #region Public Methods

        public void ActivateActionMap(string actionMapName)
        {
            try
            {
                var actionMap = Enum.Parse<ActionManager.GameActionMaps>(actionMapName);
                ActionManager.ActivateLocalActionMap(actionMap);
            }
            catch (ArgumentException)
            {
                Debug.LogError($"[PlayerInputHandler] ActionMap inválido: {actionMapName}");
            }
        }

        public void DeactivateCurrentActionMap()
        {
            ActionManager.RestoreLocalActionMap();
        }

        #endregion
    }
}
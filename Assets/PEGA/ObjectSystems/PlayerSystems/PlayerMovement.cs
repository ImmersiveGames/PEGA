﻿using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.PlayerSystems
{
    public class PlayerMovement : ObjectMovement
    {
        private PlayerMaster _playerMaster;
        private void Start()
        {
            InitializeInput();
        }

        protected override void SetInitialReferences()
        {
            base.SetInitialReferences();
            _playerMaster = GetComponent<PlayerMaster>();
        }

        private void InitializeInput()
        {
            InputGameManager.ActionManager.ActivateActionMap(ActionManager.GameActionMaps.Player);
            InputGameManager.RegisterAxisAction("Axis_Move", InputAxisPerformed, InputAxisCanceled);
        }
        #region Input Actions

        private void InputAxisPerformed(InputAction.CallbackContext context)
        {
            InputVector = context.ReadValue<Vector2>().normalized;
            _playerMaster.OnEventPlayerMasterAxisMovement(InputVector);
            DebugManager.Log<PlayerMovement>($"Eixo performed: {InputVector}");
        }

        private void InputAxisCanceled(InputAction.CallbackContext context)
        {
            InputVector = Vector2.zero;
            _playerMaster.OnEventPlayerMasterAxisMovement(InputVector);
            DebugManager.Log<PlayerMovement>($"Eixo Canceled: {InputVector}");
        }

        #endregion
        
    }
}
using System;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.PlayerSystems
{
    public class PlayerJump : ObjectJump
    {

        #region UNity Methods
        protected override void OnEnable()
        {
            base.OnEnable();
            InputGameManager.RegisterAction("Jump", AttemptJump);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            InputGameManager.UnregisterAction("Jump", AttemptJump);
        }

        #endregion
        
        
        protected override void SetInitialReferences()
        {
            base.SetInitialReferences();
        }

        private void AttemptJump(InputAction.CallbackContext obj)
        {
            //Tentativa de Pulo via Input
            AttemptJump();
        }

        protected override void AttemptJump()
        {
            base.AttemptJump();
        }
    }
    
}
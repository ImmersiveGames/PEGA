using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.PlayerSystems
{
    public class PlayerJump : ObjectJump
    {
        private PlayerInputHandler _playerInputHandler;
        private PlayerMaster _playerMaster;

        #region Unity Methods
        
        private void OnEnable()
        {
            // Registro da ação "Jump" no ActionManager
            _playerInputHandler.ActionManager.RegisterAction("Jump_Performed", AttemptJump);
        }

        private void OnDisable()
        {

            // Cancelamento do registro da ação "Jump"
            _playerInputHandler.ActionManager.UnregisterAction("Jump_Performed", AttemptJump);
        }

        #endregion

        protected override void SetInitialReferences()
        {
            base.SetInitialReferences();
            _playerInputHandler = GetComponent<PlayerInputHandler>();
            _playerMaster = GetComponent<PlayerMaster>();
        }

        private void AttemptJump(InputAction.CallbackContext obj)
        {
            // Tentativa de pulo via input
            AttemptJump();
        }
    }
}
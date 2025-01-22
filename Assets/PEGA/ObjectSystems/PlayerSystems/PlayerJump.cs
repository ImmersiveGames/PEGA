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

        private void Awake()
        {
            _playerInputHandler = GetComponent<PlayerInputHandler>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _playerMaster = GetComponent<PlayerMaster>();

            // Registro da ação "Jump" no ActionManager
            _playerInputHandler.ActionManager.RegisterAction("Jump_Performed", AttemptJump);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // Cancelamento do registro da ação "Jump"
            _playerInputHandler.ActionManager.UnregisterAction("Jump_Performed", AttemptJump);
        }

        #endregion
        

        private void AttemptJump(InputAction.CallbackContext obj)
        {
            // Tentativa de pulo via input
            AttemptJump();
            DebugManager.Log<ObjectJump>($"JUMP {_playerMaster.playerIndex}");
        }
    }
}
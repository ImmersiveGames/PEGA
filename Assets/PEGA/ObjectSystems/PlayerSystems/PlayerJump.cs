using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.PlayerSystems
{
    public class PlayerJump : ObjectJump
    {
        private PlayerInputHandler _playerInputHandler;
        private ObjectAnimator _animator;

        #region Unity Methods

        private void OnEnable()
        {
            ObjectGravity.OnFallingAfterJump += StartDown;
            ObjectGravity.OnFallingWithoutJump += StartFalling;
            
            // Registro da ação "Jump" no ActionManager
            _playerInputHandler.ActionManager.RegisterAction("Jump_Start", OnJumpInput);
            _playerInputHandler.ActionManager.RegisterAction("Jump_Cancel", OnJumpInput);
        }

        private void OnDisable()
        {
            ObjectGravity.OnFallingAfterJump -= StartDown;
            ObjectGravity.OnFallingWithoutJump -= StartFalling;
            // Cancelamento do registro da ação "Jump"
            _playerInputHandler.ActionManager.UnregisterAction("Jump_Start", OnJumpInput);
            _playerInputHandler.ActionManager.UnregisterAction("Jump_Cancel", OnJumpInput);
        }

        #endregion

        protected override void SetInitialReferences()
        {
            base.SetInitialReferences();
            _playerInputHandler = GetComponent<PlayerInputHandler>();
            _animator = GetComponent<ObjectAnimator>();
        }

        private void OnJumpInput(InputAction.CallbackContext context)
        {
            IsJumpPressed = context.ReadValueAsButton();
            DebugManager.Log<PlayerJump>($"Botão de pulo pressionado: {IsJumpPressed}");
        }

        protected override void StartJump()
        {
            DebugManager.Log<PlayerJump>("StartJump");
            _animator.JumpAnimation();
        }

        protected override void FinishJump()
        {
            DebugManager.Log<PlayerJump>("FinishJump");
            //_animator.IdleAnimation();
        }

        private void StartDown()
        {
            DebugManager.Log<PlayerJump>("StartDown");
            _animator.JumpEndAnimation();
        }

        private void StartFalling()
        {
            DebugManager.Log<PlayerJump>("StartFalling");
            //_animator.JumpEndAnimation();
        }
    }
}
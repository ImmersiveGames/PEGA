using ImmersiveGames.DebugSystems;
using UnityEngine;

namespace PEGA.ObjectSystems
{
    public abstract class ObjectJump : MonoBehaviour
    {
        [SerializeField] protected float maxJumpHeight = 1.0f;
        [SerializeField] protected float maxJumpTime = 0.5f;

        private float _initialJumpVelocity;
        protected bool IsJumpPressed;
        private bool _isJumping;

        protected ObjectGravity ObjectGravity;
        private CharacterController _characterController;

        private void Awake()
        {
            SetInitialReferences();
        }

        private void FixedUpdate()
        {
            ExecuteAction();
        }

        protected virtual void SetInitialReferences()
        {
            ObjectGravity = GetComponent<ObjectGravity>();
            _characterController = GetComponent<CharacterController>();
            SetupJumpVariables();
        }

        private void SetupJumpVariables()
        {
            // Configura os valores iniciais de gravidade e pulo
            var timeToApex = maxJumpTime / 2;
            ObjectGravity.SetGravityJump(maxJumpHeight, timeToApex);
            _initialJumpVelocity = 2 * maxJumpHeight / timeToApex;
        }

        public void ExecuteAction()
        {
            ObjectGravity.InJump(IsJumpPressed);
            if (!_isJumping && _characterController.isGrounded && IsJumpPressed)
            {
                //SetupJumpVariables(); //TODO: for debug only
                StartJump();
                _isJumping = true;
                ObjectGravity.SetCurrentVerticalVelocity(_initialJumpVelocity * .5f);

                DebugManager.Log<ObjectJump>($"Pulo iniciado. Velocidade inicial: {_initialJumpVelocity}");
            }
            if (IsJumpPressed || !_isJumping || !_characterController.isGrounded) return;
            DebugManager.Log<ObjectJump>($"Pulo Encerrado. Velocidade inicial: {_initialJumpVelocity}");
            _isJumping = false;
            FinishJump();
        }

        protected abstract void StartJump();
        protected abstract void FinishJump();
    }
}
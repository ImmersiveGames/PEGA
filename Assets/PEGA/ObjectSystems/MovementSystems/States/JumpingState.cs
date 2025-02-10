using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class JumpingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Jump;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;
        private readonly AnimatorHandler _animator;
        public JumpingState(MovementContext currentMovementContext, MovementStateFactory factory) : base(currentMovementContext, factory)
        {
            _ctx = currentMovementContext;
            _factory = factory;
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
        }

        protected internal override void EnterState()
        {
            // 📌 Armazena posição inicial e tempo do pulo no Contexto
            _ctx.jumpStartPosition = _ctx.transform.position;
            _ctx.maxJumpHeight = _ctx.jumpStartPosition.y;
            _ctx.jumpStartTime = Time.time;
            DebugManager.Log<JumpingState>($"🏃 Entering JumpState | TimeInDash={_ctx.TimeInDash} | Velocity={_ctx.StoredMomentum}");
            //
            
            _animator.SetBool("Jump", true);
            _ctx.isJumping = true;
            _ctx.CalculateJumpVariables();
            HandleJump();
            base.EnterState();
        }

        protected override void UpdateState()
        {
            _ctx.ApplyGravity(falling:false);
            base.UpdateState();
        }

        public override void ExitState()
        {
            _ctx.maxJumpHeight = _ctx.transform.position.y;
            _animator.SetBool("Jump", false);
            base.ExitState();
        }

        protected override void CheckSwitchState()
        {
            if (_ctx.movement.y <= 0 || !_ctx.InputDriver.IsJumpingPress)
            {
                SwitchState(_factory.GetState(StatesNames.Dawn));
            }
        }

        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubState()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
        private void HandleJump()
        {
            // 🔹 Usa a nova velocidade vertical calculada
            _ctx.movement.y = _ctx.initialJumpVelocity;
            _ctx.appliedMovement.y = _ctx.initialJumpVelocity;
        }

    }
}
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class JumpingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Jump;
        private readonly AnimatorHandler _animator;
        public JumpingState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext, factory)
        {
            IsRootState = true;
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
        }

        protected internal override void EnterState()
        {
            _animator.SetBool("Jump", true);
            InitializeSubState();
            base.EnterState();
            Ctx.isJumping = true;
            Ctx.CalculateJumpVariables();
            HandleJump();
        }

        protected override void UpdateState()
        {
            Ctx.ApplyGravity(falling:false);
        }

        protected override void ExitState()
        {
            _animator.SetBool("Jump", false);
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (Ctx.movement.y <= 0 || !Ctx.MovementDriver.IsJumpingPress)
            {
                SwitchState(Factory.Down());
            }
        }

        public sealed override void InitializeSubState()
        {
            if (Ctx.MovementDriver.IsDashPress)
            {
                SwitchSubState(Factory.Dash());
            }
            else if (Ctx.movementDirection == Vector2.zero )
            {
                SwitchSubState(Factory.Idle());
            }
            else
            {
                SwitchSubState(Factory.Walk());
            }
        }
        private void HandleJump()
        {
            var horizontalSpeed = Ctx.StoredMomentum.magnitude;

            // 🔹 Define um multiplicador para controlar a influência do Dash no impulso inicial do pulo
            var dashJumpInfluence = Mathf.Lerp(Ctx.movementSettings.minDashJumpInfluence, Ctx.movementSettings.maxDashJumpInfluence, Ctx.TimeInDash / Ctx.movementSettings.dashDuration);

            // 🔹 Aplica o impulso do Dash no pulo, mas limita o efeito
            var impulsoFinal = horizontalSpeed * dashJumpInfluence;
            var maxJumpBoost = Ctx.movementSettings.maxJumpHeight * Ctx.movementSettings.momentumMultiply;
            impulsoFinal = Mathf.Min(impulsoFinal, maxJumpBoost);

            // 🔹 Agora, initialJumpVelocity já foi atualizado em `CalculateJumpVariables()`
            Ctx.movement.y = Ctx.initialJumpVelocity + impulsoFinal;
            Ctx.appliedMovement.y = Ctx.movement.y;
        }

    }
}
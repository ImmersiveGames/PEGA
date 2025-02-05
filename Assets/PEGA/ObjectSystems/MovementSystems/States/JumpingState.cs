using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class JumpingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Jump;

        public JumpingState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext,
            factory)
        {
            IsRootState = true;
        }

        protected internal override void EnterState()
        {
            InitializeSubState();
            base.EnterState();
            Ctx.isJumping = true;
            Ctx.CalculateJumpVariables();
            HandleJump();
        }

        protected override void UpdateState()
        {
            Ctx.HandleGravityJump();
        }

        protected override void ExitState()
        {
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
                SetSubState(Factory.Dash());
            }
            else if (Ctx.movementDirection == Vector2.zero )
            {
                SetSubState(Factory.Idle());
            }
            else
            {
                SetSubState(Factory.Walk());
            }
        }

        private void HandleJump()
        {
            Ctx.movement.y = Ctx.initialJumpVelocity;
            Ctx.appliedMovement.y = Ctx.initialJumpVelocity;
        }
    }
}
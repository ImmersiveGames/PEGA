using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class FallingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Fall;
        public FallingState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext, factory)
        {
            IsRootState = true;
        }

        protected internal override void EnterState()
        {
            InitializeSubState();
            Ctx.isFalling = true;
            Ctx.CalculateJumpVariables();
            base.EnterState();
        }

        protected override void UpdateState()
        {
            Ctx.ApplyGravity(falling:true);
        }

        protected override void ExitState()
        {
            Ctx.isFalling = false;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (Ctx.transform.position.y < Ctx.fallMaxHeight)
            {
                SwitchState(Factory.Dead());
                return;
            }
            if (Ctx.CharacterController.isGrounded)
            {
                SwitchState(Factory.Grounded());
            }
        }

        public sealed override void InitializeSubState()
        {
            if (Ctx.movementDirection == Vector2.zero )
            {
                SwitchSubState(Factory.Idle());
            }
            else
            {
                SwitchSubState(Factory.Walk());
            }
        }
    }
}
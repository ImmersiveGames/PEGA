using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class GroundedState: BaseState
    {
        protected override StatesNames StateName => StatesNames.Grounded;
        public GroundedState(MovementContext currentMovementContext, StateFactory factory): base(currentMovementContext,factory)
        {
            IsRootState = true;
            
        }
        protected internal override void EnterState()
        {
            InitializeSubState();
            base.EnterState();
            Ctx.isGrounded = true;
   
            Ctx.movement.y = Ctx.movementSettings.gravityGround;
            Ctx.appliedMovement.y = Ctx.movementSettings.gravityGround;
        }

        protected override void UpdateState()
        {
            //Debug.Log($"Subindo; {Ctx.movement.y}");
            //Debug.Log($"Update - Grounded - no chão: {Ctx.CharacterController.isGrounded}");
        }

        protected override void ExitState()
        {
            Ctx.isGrounded = false;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (!Ctx.CharacterController.isGrounded && Ctx.isJumping == false)
            {
                SwitchState(Factory.Fall());
            }
            else
            {
                if (Ctx.MovementDriver.IsJumpingPress)
                {
                    SwitchState(Factory.Jump());
                }
            }
        }


        public sealed override void InitializeSubState()
        {
            //SetSubState(Factory.Idle());
            if (Ctx.MovementDriver.IsDashPress)
            {
                SetSubState(Factory.Dash());
                return;
            }
            if (Ctx.movementDirection == Vector2.zero )
            {
                SetSubState(Factory.Idle());
            }
            else
            {
                SetSubState(Factory.Walk());
            }
        }
    }
}
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class JumpingDownState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Dawn;
        public JumpingDownState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext, factory)
        {
            IsRootState = true;
        }

        protected internal override void EnterState()
        {
            InitializeSubState();
            Ctx.CalculateJumpVariables();
            base.EnterState();
        }

        protected override void UpdateState()
        {
            Ctx.HandleGravityFall();
        }

        protected override void ExitState()
        {
            Ctx.isJumping = false;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (Ctx.CharacterController.isGrounded)
            {
                SwitchState(Factory.Grounded());
            }
            //TODO: Aqui também precisa depois de um tempo considerar que esta caindo no infinito
        }

        public sealed override void InitializeSubState()
        {
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
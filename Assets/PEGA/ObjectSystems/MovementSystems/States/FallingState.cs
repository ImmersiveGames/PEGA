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
            var previousYVelocity = Ctx.movement.y;
            Ctx.movement.y += Ctx.gravity * Ctx.movementSettings.fallMultiplier * Time.deltaTime;
            Ctx.appliedMovement.y = Mathf.Max((previousYVelocity + Ctx.movement.y) *.5f, Ctx.movementSettings.maxFallVelocity);
            
        }

        protected override void ExitState()
        {
            Ctx.isFalling = false;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (Ctx.CharacterController.isGrounded)
            {
                SwitchState(Factory.Grounded());
            }
        }

        public sealed override void InitializeSubState()
        {
            //TODO:Usar o SetSubState para definir todos as condições para cada substate e instancia-los pela fabrica.
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
    }
}
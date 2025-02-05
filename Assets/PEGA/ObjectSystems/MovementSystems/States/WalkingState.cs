using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class WalkingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Walk;
        private readonly AnimatorHandler _animator;
        public WalkingState(MovementContext currentMovementContext, StateFactory factory): base(currentMovementContext,factory)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
        }
        protected internal override void EnterState()
        {
            _animator.SetFloat("Movement", Ctx.movementDirection.magnitude);
            base.EnterState();
            Ctx.isWalking = true;
            //aqui ele aplica a lógica de animação
        }

        protected override void UpdateState()
        {
            Ctx.ApplyMovement();
           CheckSwitchState();//Manter por último
        }

        protected override void ExitState()
        {
            base.ExitState();
            Ctx.isWalking = false;
        }

        public override void CheckSwitchState()
        {
            if (!Ctx.CanDashAgain && !Ctx.MovementDriver.IsDashPress)
            {
                Ctx.CanDashAgain = true;
            }
            
            if (Ctx.MovementDriver.IsDashPress && Ctx.CanDashAgain)
            {
                Ctx.CanDashAgain = false;
                CurrentSuperstate.SwitchSubState(Factory.Dash());
                return;
            }
            if (Ctx.movementDirection == Vector2.zero)
            {
                CurrentSuperstate.SwitchSubState(Factory.Idle());
            }
        }

        public sealed override void InitializeSubState()
        {
        }
    }
}
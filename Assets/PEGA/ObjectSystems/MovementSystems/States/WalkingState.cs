using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class WalkingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Walk;
        public WalkingState(MovementContext currentMovementContext, StateFactory factory): base(currentMovementContext,factory)
        {
        }
        protected internal override void EnterState()
        {
            base.EnterState();
            Ctx.isWalking = true;
            //aqui ele aplica a lógica de animação
        }

        protected override void UpdateState()
        {
            Ctx.ApplyMovement();
            Debug.Log($"Update - walk");
            //aqui ele aplicou os modificadores do apply e current
           CheckSwitchState();//Manter por último
        }

        protected override void ExitState()
        {
            base.ExitState();
            Ctx.isWalking = false;
        }

        public override void CheckSwitchState()
        {
            Debug.Log($"CheckSwitchState - Walk");
            
            if (Ctx.MovementDriver.IsDashPress)
            {
                CurrentSuperstate.SetSubState(Factory.Dash());
                return;
            }
            if (Ctx.movementDirection == Vector2.zero)
            {
                CurrentSuperstate.SetSubState(Factory.Idle());
            }
        }

        public sealed override void InitializeSubState()
        {
        }
    }
}
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine.States
{
    public class WalkingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Walk;
        public WalkingState(StateMachineContext currentStateMachineContext, StateFactory factory): base(currentStateMachineContext,factory)
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
            Ctx.movement.x = Ctx.directionPressed.x * Ctx.movementSettings.baseSpeed;
            Ctx.movement.z = Ctx.directionPressed.y * Ctx.movementSettings.baseSpeed;
            
            Ctx.appliedMovement.x = Ctx.movement.x;
            Ctx.appliedMovement.z = Ctx.movement.z;
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
            if (Ctx.directionPressed == Vector2.zero)
            {
                _currentSuperstate.SetSubState(Factory.Idle());
            }
            Debug.Log($"[WalkingState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            Debug.Log($"[WalkingState] InitializeSubState");
        }
    }
}
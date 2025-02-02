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
            if (Ctx.movement == Vector3.zero)
            {
                SwitchState(Factory.Idle()); 
            }
            Debug.Log($"[WalkingState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            Debug.Log($"[WalkingState] InitializeSubState");
        }
    }
}
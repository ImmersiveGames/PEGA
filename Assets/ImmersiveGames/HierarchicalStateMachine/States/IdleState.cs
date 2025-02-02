using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine.States
{
    public class IdleState: BaseState
    {
        protected override StatesNames StateName => StatesNames.Idle;
        public IdleState(StateMachineContext currentStateMachineContext, StateFactory factory) : base(currentStateMachineContext,factory)
        {
        }

        protected override void UpdateState()
        {
            CheckSwitchState();//manter por último
        }

        public override void CheckSwitchState()
        {
            /*if (Ctx.movement != Vector3.zero)
            {
                SwitchState(Factory.Walk()); 
            }*/
            
            Debug.Log($"[IdleState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            Debug.Log($"[IdleState] InitializeSubState");
        }
    }
}
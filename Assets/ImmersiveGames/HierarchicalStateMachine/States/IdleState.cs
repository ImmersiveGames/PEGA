using ImmersiveGames.HierarchicalStateMachine.Interface;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine.States
{
    public class IdleState: BaseState, ISubState
    {
        protected override StatesNames StateName => StatesNames.Idle;
        public IdleState(ContextStates currentContext, StateFactory factory) : base(currentContext,factory)
        {
        }
        public override void EnterState()
        {
            base.EnterState();
            //aqui ele zerou o apply movement
            Debug.Log($"[IdleState] EnterState");
        }

        protected override void UpdateState()
        {
            CheckSwitchState();//manter por ultimo
            Debug.Log($"[IdleState] UpdateState");
        }

        protected override void ExitState()
        {
            base.ExitState();
            Debug.Log($"[IdleState] ExitState");
        }

        public override void CheckSwitchState()
        {
            if (Ctx.movement != Vector3.zero)
            {
                SwitchState(Factory.Walk()); 
            }
            
            Debug.Log($"[IdleState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            Debug.Log($"[IdleState] InitializeSubState");
        }

        public bool IsValidSuperState(BaseState superState)
        {
            return superState is GroundedState;
        }
    }
}
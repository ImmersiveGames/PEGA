using UnityEngine;

namespace ImmersiveGames.StateMachine.States
{
    public class IdleState: BaseState
    {
        public IdleState(ContextStates currentContext, StateFactory factory) : base(currentContext,factory)
        {
        }
        public override void EnterState()
        {
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
            Debug.Log($"[IdleState] ExitState");
        }

        public override void CheckSwitchState()
        {
            if (Ctx.movement != Vector3.zero)
            {
                Factory.Walk(); 
            }
            
            Debug.Log($"[IdleState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            Debug.Log($"[IdleState] InitializeSubState");
        }

    }
}
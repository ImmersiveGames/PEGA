using UnityEngine;

namespace ImmersiveGames.StateMachine.States
{
    public class WalkingState : BaseState
    {
        public WalkingState(ContextStates currentContext, StateFactory factory): base(currentContext,factory)
        {
        }
        public override void EnterState()
        {
            Ctx.isWalking = true;
            //aqui ele aplica a logica de animação
            Debug.Log($"[WalkingState] Enter State");
        }

        protected override void UpdateState()
        {
            //aqui ele aplicou os modificadores do apply e current
            CheckSwitchState();//Manter por ultimo
            Debug.Log($"[WalkingState] UpdateState");
        }

        protected override void ExitState()
        {
            Ctx.isWalking = false;
            Debug.Log($"[WalkingState] ExitState");
        }

        public override void CheckSwitchState()
        {
            if (Ctx.movement == Vector3.zero)
            {
                Factory.Idle(); 
            }
            Debug.Log($"[WalkingState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            Debug.Log($"[WalkingState] InitializeSubState");
        }
    }
}
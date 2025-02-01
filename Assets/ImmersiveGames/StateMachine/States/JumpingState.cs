using ImmersiveGames.StateMachine.Interface;
using UnityEngine;

namespace ImmersiveGames.StateMachine.States
{
    public class JumpingState:BaseState, IRootState
    {
        public JumpingState(ContextStates currentContext, StateFactory factory): base(currentContext,factory)
        {
            IsRootState = true;
        }
        public override void EnterState()
        {
            InitializeSubState();
            Ctx.isJumping = true;
            Debug.Log($"[JumpingState] Enter State");
        }

        protected override void UpdateState()
        {
            HandleGravity();
            CheckSwitchState();//Manter por ultimo
            Debug.Log($"[JumpingState] UpdateState");
        }

        protected override void ExitState()
        {
            Debug.Log($"[JumpingState] ExitState");
            Ctx.isJumping = false;
        }

        public override void CheckSwitchState()
        {
            if (Ctx.isGrounded)
            {
                SwitchState(Factory.Grounded());
            }
            Debug.Log($"[JumpingState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            Debug.Log($"[JumpingState] InitializeSubState");
        }

        public void HandleGravity()
        {
            Debug.Log($"[JumpingState] Logica da gravidade");
        }
    }
}
using ImmersiveGames.HierarchicalStateMachine.Interface;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine.States
{
    public class JumpingState:BaseState, IRootState
    {
        protected override StatesNames StateName => StatesNames.Jump;
        public JumpingState(ContextStates currentContext, StateFactory factory): base(currentContext,factory)
        {
            IsRootState = true;
        }
        public override void EnterState()
        {
            base.EnterState();
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
            base.ExitState();
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
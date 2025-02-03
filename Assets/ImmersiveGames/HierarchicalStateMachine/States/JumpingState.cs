using ImmersiveGames.HierarchicalStateMachine.Interface;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine.States
{
    public class JumpingState:BaseState, IRootState
    {
        protected override StatesNames StateName => StatesNames.Jump;
        public JumpingState(StateMachineContext currentStateMachineContext, StateFactory factory): base(currentStateMachineContext,factory)
        {
            IsRootState = true;
        }
        protected internal override void EnterState()
        {
            CalculateJumpVariables();
            base.EnterState();
            HandleJump();
            InitializeSubState();
        }

        protected override void UpdateState()
        {
            HandleGravity();
            CheckSwitchState();//Manter por último
        }

        protected override void ExitState()
        {
            Ctx.isJumping = false;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (!Ctx.jumpPressed || Ctx.movement.y <= 0.0f)
            {
                SwitchState(Factory.Fall());
            }
            Debug.Log($"[JumpingState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            SetSubState(Ctx.movement is { x: 0, z: 0 } ? Factory.Idle() : Factory.Walk());
            //Debug.Log($"[JumpingState] InitializeSubState");
        }

        public void HandleGravity()
        {
            var previousYVelocity = Ctx.movement.y;
            Ctx.movement.y += Ctx.gravity * Time.deltaTime;
            Ctx.appliedMovement.y = previousYVelocity + Ctx.movement.y;
        }

        private void HandleJump()
        {
            Ctx.isJumping = true;
            Ctx.movement.y = Ctx.initialJumpVelocity;
            Ctx.appliedMovement.y = Ctx.initialJumpVelocity;
            
        }
        private void CalculateJumpVariables()
        {
            var timeToApex = Ctx.movementSettings.maxJumpTime / 2;
            Ctx.gravity = (-2 * Ctx.movementSettings.maxJumpHeight) / Mathf.Pow(timeToApex, 2);
            Ctx.initialJumpVelocity = (2 * Ctx.movementSettings.maxJumpHeight) / timeToApex;
        }
    }
}
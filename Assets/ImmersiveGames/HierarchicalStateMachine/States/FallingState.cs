using ImmersiveGames.HierarchicalStateMachine.Interface;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine.States
{
    public class FallingState : BaseState, IRootState
    {
        protected override StatesNames StateName => StatesNames.Fall;
        public FallingState(StateMachineContext currentStateMachineContext, StateFactory factory) : base(currentStateMachineContext, factory)
        {
            IsRootState = true;
            
        }

        protected internal override void EnterState()
        {
            Ctx.isFalling = true;
            base.EnterState();
            InitializeSubState();
        }

        protected override void UpdateState()
        {
            HandleGravity();
            CheckSwitchState();//Manter por último
        }

        protected override void ExitState()
        {
            Ctx.isFalling = false;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (Ctx.CharacterController.isGrounded)
            {
                SwitchState(Factory.Grounded());
            }

            //Debug.Log($"[FallingState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            //TODO:Usar o SetSubState para definir todos as condições para cada substate e instancia-los pela fabrica.
            SetSubState(Ctx.movement is { x: 0, z: 0 } ? Factory.Idle() : Factory.Walk());
            //Debug.Log($"[FallingState] InitializeSubState");
        }

        //Vamos precisar de um método de gravidade aqui HandleGravity no video 6:50
        public void HandleGravity()
        {
            var previousYVelocity = Ctx.movement.y;
            Ctx.movement.y += Ctx.gravity * Ctx.movementSettings.fallMultiplier * Time.deltaTime;
            Ctx.appliedMovement.y = Mathf.Max((previousYVelocity + Ctx.movement.y), Ctx.movementSettings.maxFallVelocity);
            //Debug.Log($"[FallingState] HandleGravity");
        }
    }
}
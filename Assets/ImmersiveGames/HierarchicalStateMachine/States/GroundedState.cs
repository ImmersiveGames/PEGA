using ImmersiveGames.HierarchicalStateMachine.Interface;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine.States
{
    public class GroundedState: BaseState, IRootState
    {
        protected override StatesNames StateName => StatesNames.Grounded;
        public GroundedState(StateMachineContext currentStateMachineContext, StateFactory factory): base(currentStateMachineContext,factory)
        {
            IsRootState = true;
            
        }
        protected internal override void EnterState()
        {
            Ctx.isGrounded = true;
            base.EnterState();
            InitializeSubState();
            HandleGravity();
        }

        protected override void UpdateState()
        {
            CheckSwitchState();//manter por último
            //Debug.Log($"Altura: {Ctx.appliedMovement.y}");
        }

        protected override void ExitState()
        {
            Ctx.isGrounded = false;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            //TODO: logica para definir os estados irmãos (jump e fall)
            if (Ctx.jumpPressed)
            {
                SwitchState(Factory.Jump());
            }
            Debug.Log($"[GroundedState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            //TODO:Usar o SetSubState para definir todos as condições para cada substate e instancia-los pela fabrica.
            SetSubState(Ctx.movement is { x: 0, z: 0 } ? Factory.Idle() : Factory.Walk());
            //Debug.Log($"[GroundedState] InitializeSubState");
        }

        public void HandleGravity()
        {
            //TODO:Como esse é o primeiro estado ele precisa setar a gravidade no eixo y no apply e current
            //Debug.Log($"[GroundedState] Gravity");
            Ctx.movement.y = Ctx.movementSettings.gravityGround;
            Ctx.appliedMovement.y = Ctx.movementSettings.gravityGround;
        }
    }
}
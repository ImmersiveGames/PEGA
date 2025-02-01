using ImmersiveGames.HierarchicalStateMachine.Interface;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine.States
{
    public class GroundedState: BaseState, IRootState
    {
        protected override StatesNames StateName => StatesNames.Grounded;
        public GroundedState(ContextStates currentContext, StateFactory factory): base(currentContext,factory)
        {
            IsRootState = true;
            
        }
        public override void EnterState()
        {
            base.EnterState();
            InitializeSubState();
            Ctx.isGrounded = true;
            HandleGravity();
            Debug.Log($"[GroundedState] EnterState");
        }

        protected override void UpdateState()
        {
            CheckSwitchState();//manter por ultimo
            Debug.Log($"[GroundedState] UpdateState");
        }

        protected override void ExitState()
        {
            base.ExitState();
            Ctx.isGrounded = false;
            Debug.Log($"[GroundedState] ExitState");
        }

        public override void CheckSwitchState()
        {
            //TODO: logica para definir os estados irmãos (jump e fall)
            Debug.Log($"[GroundedState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            //TODO:Usar o SetSubState para definir todos as condições para cada substate e instancia-los pela fabrica.
            SetSubState(Ctx.movement == Vector3.zero ? Factory.Idle() : Factory.Walk());
            Debug.Log($"[GroundedState] InitializeSubState");
        }

        public void HandleGravity()
        {
            //TODO:Como esse é o primeiro estado ele precisa setar a gravidade no eixo y no apply e current
            Debug.Log($"[GroundedState] Gravity");
        }
    }
}
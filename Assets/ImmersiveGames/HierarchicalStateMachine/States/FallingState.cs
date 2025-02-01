using ImmersiveGames.HierarchicalStateMachine.Interface;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine.States
{
    public class FallingState : BaseState, IRootState
    {
        protected override StatesNames StateName => StatesNames.Fall;
        public FallingState(ContextStates currentContext, StateFactory factory) : base(currentContext, factory)
        {
            IsRootState = true;
            
        }

        public override void EnterState()
        {
            base.EnterState();
            InitializeSubState();
            Debug.Log($"[FallingState] EnterState");
        }

        protected override void UpdateState()
        {
            HandleGravity();
            CheckSwitchState();//Manter por ultimo
            Debug.Log($"[FallingState] UpdateState");
        }

        protected override void ExitState()
        {
            base.ExitState();
            Debug.Log($"[FallingState] ExitState");
        }

        public override void CheckSwitchState()
        {
            if (Ctx.isGrounded)
            {
                SwitchState(Factory.Grounded());
            }

            Debug.Log($"[FallingState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            //TODO:Usar o SetSubState para definir todos as condições para cada substate e instancia-los pela fabrica.
            SetSubState(Ctx.movement == Vector3.zero ? Factory.Idle() : Factory.Walk());
            Debug.Log($"[FallingState] InitializeSubState");
        }

        //Vamos precisar de um metodo de gravidade aqui HandleGravity no video 6:50
        public void HandleGravity()
        {
            Debug.Log($"[FallingState] HandleGravity");
        }
    }
}
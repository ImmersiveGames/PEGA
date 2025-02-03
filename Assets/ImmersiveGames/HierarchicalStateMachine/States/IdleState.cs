using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine.States
{
    public class IdleState: BaseState
    {
        protected override StatesNames StateName => StatesNames.Idle;
        public IdleState(StateMachineContext currentStateMachineContext, StateFactory factory) : base(currentStateMachineContext,factory)
        {
        }

        protected internal override void EnterState()
        {
            base.EnterState();
            Ctx.movement.x = 0;
            Ctx.movement.z = 0;
        }

        protected override void UpdateState()
        {
            CheckSwitchState();//manter por último
        }

        public override void CheckSwitchState()
        {
            if (Ctx.directionPressed != Vector2.zero)
            {
                _currentSuperstate.SetSubState(Factory.Walk()); // ✅ Troca corretamente para Walking
            }
            
            Debug.Log($"[IdleState] CheckSwitchState");
        }

        public sealed override void InitializeSubState()
        {
            Debug.Log($"[IdleState] InitializeSubState");
        }
    }
}
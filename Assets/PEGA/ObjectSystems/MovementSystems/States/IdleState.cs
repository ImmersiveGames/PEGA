using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class IdleState: BaseState
    {
        protected override StatesNames StateName => StatesNames.Idle;
        public IdleState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext,factory)
        {
        }

        protected internal override void EnterState()
        {
            base.EnterState();
            Ctx.isWalking = false;
            Ctx.appliedMovement.x = 0;
            Ctx.appliedMovement.z = 0;
        }

        protected override void UpdateState()
        {
            Debug.Log($"Update - Idle");
            CheckSwitchState(); //Subs Precisam atualizar aqui. E sempre no fim.
        }

        public override void CheckSwitchState()
        {
            Debug.Log($"CheckSwitchState - Idle");
            if (Ctx.MovementDriver.IsDashPress)
            {
                CurrentSuperstate.SetSubState(Factory.Dash());
                return;
            }
            if (Ctx.movementDirection != Vector2.zero)
            {
                CurrentSuperstate.SetSubState(Factory.Walk());
            }
        }

        public sealed override void InitializeSubState()
        {
            //Debug.Log($"[IdleState] InitializeSubState");
        }
    }
}
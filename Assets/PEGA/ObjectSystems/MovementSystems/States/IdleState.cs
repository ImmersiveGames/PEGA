using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class IdleState: BaseState
    {
        protected override StatesNames StateName => StatesNames.Idle;
        private readonly AnimatorHandler _animator;
        public IdleState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext,factory)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
        }

        protected internal override void EnterState()
        {
            _animator.SetFloat("Movement", 0);
            _animator.SetFloat("Idle", 0);
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
            if (!Ctx.CanDashAgain && !Ctx.MovementDriver.IsDashPress)
            {
                Ctx.CanDashAgain = true;
            }
            
            if (Ctx.MovementDriver.IsDashPress && Ctx.CanDashAgain)
            {
                Ctx.CanDashAgain = false;
                CurrentSuperstate.SwitchSubState(Factory.Dash());
                return;
            }
            if (Ctx.movementDirection != Vector2.zero)
            {
                CurrentSuperstate.SwitchSubState(Factory.Walk());
            }
        }

        public sealed override void InitializeSubState()
        {
            //Debug.Log($"[IdleState] InitializeSubState");
        }
    }
}
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class DashState : BaseState
    {
        private float _dashTime;
        public DashState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext, factory)
        {
        }

        protected override StatesNames StateName => StatesNames.Dash;
        
        protected internal override void EnterState()
        {
            base.EnterState();
            Ctx.isDashing = true;
            _dashTime = Ctx.dashDuration;
            //aqui ele aplica a lógica de animação
        }
        protected override void UpdateState()
        {
            Ctx.ApplyMovement(Ctx.dashMultiply); // Dash usa um multiplicador
            _dashTime -= Time.deltaTime;
            CheckSwitchState(); //Subs Precisam atualizar aqui. E sempre no fim.
        }
        protected override void ExitState()
        {
            Ctx.isDashing = false;
            _dashTime = 0;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (!Ctx.MovementDriver.IsDashPress)
            {
                if (!Ctx.CharacterController.isGrounded)
                {
                    SwitchState(Factory.Fall()); // 🔹 Se não está no chão, cai!
                }
                else
                {
                    CurrentSuperstate.SetSubState(Ctx.movementDirection == Vector2.zero ? Factory.Idle() : Factory.Walk());
                }
            }
        }

        public override void InitializeSubState()
        {
        }
    }
}
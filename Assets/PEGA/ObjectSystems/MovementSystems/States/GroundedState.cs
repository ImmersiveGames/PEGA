using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class GroundedState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Grounded;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;

        public GroundedState(MovementContext currentMovementContext, MovementStateFactory factory) : base(
            currentMovementContext, factory)
        {
            _ctx = currentMovementContext;
            _factory = factory;
        }

        protected internal override void EnterState()
        {
            _ctx.isGrounded = true;
            base.EnterState();

            _ctx.movement.y = _ctx.movementSettings.gravityGround;
            _ctx.appliedMovement.y = _ctx.movementSettings.gravityGround;
        }

        protected override void UpdateState()
        {
            base.UpdateState();
        }

        public override void ExitState()
        {
            _ctx.isGrounded = false;
            base.ExitState();
        }

        protected override void CheckSwitchState()
        {
            //Verifica se ja soltou o botão para liberar o pulo
            if (!_ctx.CanJumpAgain && !_ctx.InputDriver.IsJumpingPress)
            {
                _ctx.CanJumpAgain = true;
            }

            //Debug.Log("Can Jump Again?: " + Ctx.CanJumpAgain);
            if (!_ctx.CharacterController.isGrounded)
            {
                //Queda de plataforma
                SwitchState(_factory.GetState(StatesNames.Fall));
                return;
            }

            if (!_ctx.CharacterController.isGrounded || !_ctx.InputDriver.IsJumpingPress 
                                                     || _ctx.isJumping || !_ctx.CanJumpAgain) return;
            _ctx.CanJumpAgain = false;
            SwitchState(_factory.GetState(StatesNames.Jump));
        }

        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubState()
        {
            SwitchSubState(_ctx.InputDriver.GetMovementDirection() == Vector2.zero ? _factory.GetState(StatesNames.Idle) : _factory.GetState(StatesNames.Walk));
        }

    }
}


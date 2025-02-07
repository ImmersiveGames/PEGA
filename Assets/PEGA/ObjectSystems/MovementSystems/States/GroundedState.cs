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
            IsRootState = true;
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
            //Debug.Log($"Subindo; {Ctx.movement.y}");
            //Debug.Log($"Update - Grounded - no chão: {Ctx.CharacterController.isGrounded}");
        }

        protected override void ExitState()
        {
            _ctx.isGrounded = false;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            //Verifica se ja soltou o botão para liberar o pulo
            if (!_ctx.CanJumpAgain && !_ctx.ActualDriver.IsJumpingPress)
            {
                _ctx.CanJumpAgain = true;
            }

            //Debug.Log("Can Jump Again?: " + Ctx.CanJumpAgain);
            if (!_ctx.CharacterController.isGrounded)
            {
                //Queda de plataforma
                SwitchState(_factory.Fall());
                return;
            }

            if (!_ctx.CharacterController.isGrounded || !_ctx.ActualDriver.IsJumpingPress 
                                                     || _ctx.isJumping || !_ctx.CanJumpAgain) return;
            _ctx.CanJumpAgain = false;
            SwitchState(_factory.Jump());
        }

        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubState()
        {
            SwitchSubState(_ctx.movementDirection == Vector2.zero ? _factory.Idle() : _factory.Walk());
        }

    }
}


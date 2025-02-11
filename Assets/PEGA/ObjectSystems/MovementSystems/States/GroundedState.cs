using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class GroundedState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Grounded;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;

        public GroundedState(MovementContext currentMovementContext, MovementStateFactory factory) : base(currentMovementContext)
        {
            _ctx = currentMovementContext;
            _factory = factory;
        }

        public override void OnEnter()
        {
            _ctx.isGrounded = true;
            _ctx.isFalling = false;
            base.OnEnter();

            _ctx.movement.y = _ctx.movementSettings.gravityGround;
            _ctx.appliedMovement.y = _ctx.movementSettings.gravityGround;
        }

        public override void Tick()
        {
            //Verifica se ja soltou o botão para liberar o pulo
            if (_ctx.CharacterController.isGrounded && !_ctx.CanJumpAgain && !_ctx.InputDriver.IsJumpingPress)
            {
                _ctx.CanJumpAgain = true;
            }
            base.Tick();
        }

        public override void OnExit()
        {
            _ctx.isGrounded = false;
            base.OnExit();
        }
        
        protected override void SetupTransitions()
        {
            // 🔹 Definição das transições de estado principal (muda o DashState inteiro)
            AddTransition(_factory.GetState(StatesNames.Fall), () => !_ctx.CharacterController.isGrounded);
            
            AddTransition(_factory.GetState(StatesNames.Jump), PredicateJump);
            
            // 🔹 Definição das transições de subestado (muda apenas dentro do estado pai)
            AddSubStateTransition(_factory.GetState(StatesNames.Idle), () => _ctx.CharacterController.isGrounded && _ctx.InputDriver.GetMovementDirection() == Vector2.zero);
            AddSubStateTransition(_factory.GetState(StatesNames.Walk), () => _ctx.CharacterController.isGrounded && _ctx.InputDriver.GetMovementDirection() != Vector2.zero);
            AddTransition(_factory.GetState(StatesNames.Dash), PredicateDash);
        }

        private bool PredicateJump()
        {
            var resolve = _ctx.CharacterController.isGrounded 
                          && _ctx.InputDriver.IsJumpingPress 
                          && !_ctx.isJumping 
                          && _ctx.CanJumpAgain;
            if (resolve)
            {
                _ctx.CanJumpAgain = false;
            }
            return resolve;
        }
        private bool PredicateDash()
        {
            var resolve = _ctx.CharacterController.isGrounded
                          && _ctx.InputDriver.IsDashPress
                          && !_ctx.isDashing
                          && _ctx.CanDashAgain;
            if (resolve)
            {
                _ctx.CanDashAgain = false;
            }

            return resolve;
        }

    }
}


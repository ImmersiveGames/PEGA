using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class FallingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Fall;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;

        private const float GroundedBufferTime = 0.1f;
        private float _fallStartTime;

        public FallingState(MovementContext currentMovementContext, MovementStateFactory factory) : base(currentMovementContext)
        {
            _ctx = currentMovementContext;
            _factory = factory;
        }

        public override void OnEnter()
        {
            _ctx.isFalling = true;
            _fallStartTime = Time.time;
            _ctx.CalculateJumpVariables();
            base.OnEnter();
        }

        public override void Tick()
        {
            _ctx.ApplyGravity(falling: true);
            base.Tick();
        }
        
        protected override void SetupTransitions()
        {
            // 🔹 Definição das transições de estado principal (muda o DashState inteiro)
            AddTransition(_factory.GetState(StatesNames.Dead), () => _ctx.transform.position.y < _ctx.fallMaxHeight);
            
            AddTransition(_factory.GetState(StatesNames.Grounded), () => _ctx.CharacterController.isGrounded && (Time.time - _fallStartTime) > GroundedBufferTime);

        }

    }
}
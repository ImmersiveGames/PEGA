using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class JumpingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Jump;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;
        private readonly AnimatorHandler _animator;
        public JumpingState(MovementContext currentMovementContext, MovementStateFactory factory) : base(currentMovementContext)
        {
            _ctx = currentMovementContext;
            _factory = factory;
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
        }

        public override void OnEnter()
        {
            // 📌 Armazena posição inicial e tempo do pulo no Contexto
            _ctx.jumpStartPosition = _ctx.transform.position;
            _ctx.maxJumpHeight = _ctx.jumpStartPosition.y;
            _ctx.jumpStartTime = Time.time;
            _ctx.StoredMomentum = _ctx.CharacterController.velocity.magnitude;
            DebugManager.Log<JumpingState>($"🏃 Entering JumpState | TimeInDash={_ctx.TimeInDash} | Velocity={_ctx.StoredMomentum}");
            //
            
            _animator.SetBool("Jump", true);
            _ctx.isJumping = true;
            _ctx.CalculateJumpVariables();
            HandleJump();
            base.OnEnter();
        }

        public override void Tick()
        {
            _ctx.ApplyGravity(falling:false);
            base.Tick();
        }

        public override void OnExit()
        {
            _ctx.maxJumpHeight = _ctx.transform.position.y;
            _animator.SetBool("Jump", false);
            base.OnExit();
        }
        protected override void SetupTransitions()
        {
            // 🔹 Definição das transições de estado principal (muda o DashState inteiro)
            AddTransition(_factory.GetState(StatesNames.Dawn), () => _ctx.movement.y <= 0 || !_ctx.InputDriver.IsJumpingPress);

        }
        private void HandleJump()
        {
            // 🔹 Usa a nova velocidade vertical calculada
            _ctx.movement.y = _ctx.initialJumpVelocity;
            _ctx.appliedMovement.y = _ctx.initialJumpVelocity;
        }

    }
}
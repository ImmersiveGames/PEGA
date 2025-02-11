using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class IdleState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Idle;
        private readonly MovementContext _ctx;
        private readonly AnimatorHandler _animator;

        public IdleState(MovementContext currentMovementContext) : base(currentMovementContext)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
            _ctx = currentMovementContext;
        }

        public override void OnEnter()
        {
            _animator.SetFloat("Movement", 0);
            _animator.SetFloat("Idle", 0);
            _ctx.isWalking = false;
            _ctx.appliedMovement.x = 0;
            _ctx.appliedMovement.z = 0;
            base.OnEnter();
        }

        public override void Tick()
        {
            if (_ctx.CharacterController.isGrounded && !_ctx.CanDashAgain && !_ctx.InputDriver.IsDashPress && !_ctx.DashingCooldown)
            {
                _ctx.CanDashAgain = true;
            }
            DebugManager.Log<IdleState>($"Update - Idle");
            base.Tick();
        }
    }
}
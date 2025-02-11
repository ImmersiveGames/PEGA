using ImmersiveGames.HierarchicalStateMachine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class WalkingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Walk;
        private readonly MovementContext _ctx;
        private readonly AnimatorHandler _animator;
        public WalkingState(MovementContext currentMovementContext ): base(currentMovementContext)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
            _ctx = currentMovementContext;
        }

        public override void OnEnter()
        {
            _animator.SetFloat("Movement", _ctx.InputDriver.GetMovementDirection().magnitude);
            _ctx.isWalking = true;
            base.OnEnter();
            //aqui ele aplica a lógica de animação
        }

        public override void Tick()
        {
            if (_ctx.CharacterController.isGrounded && !_ctx.CanDashAgain && !_ctx.InputDriver.IsDashPress && !_ctx.DashingCooldown)
            {
                _ctx.CanDashAgain = true;
            }
            _ctx.ApplyMovement(_ctx.InputDriver.GetMovementDirection());
            base.Tick();//Manter por último
        }

        public override void OnExit()
        {
            base.OnExit();
            _ctx.isWalking = false;
        }
        
    }
}
using ImmersiveGames.HierarchicalStateMachine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class FallingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Fall;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;

        public FallingState(MovementContext currentMovementContext, MovementStateFactory factory) : base(currentMovementContext,
            factory)
        {
            IsRootState = true;
            _ctx = currentMovementContext;
            _factory = factory;
        }

        protected internal override void EnterState()
        {
            _ctx.isFalling = true;
            _ctx.CalculateJumpVariables();
            base.EnterState();
        }

        protected override void UpdateState()
        {
            _ctx.ApplyGravity(falling: true);
        }

        protected override void ExitState()
        {
            _ctx.isFalling = false;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (Ctx.transform.position.y < _ctx.fallMaxHeight)
            {
                SwitchState(Factory.Dead());
                return;
            }

            if (_ctx.CharacterController.isGrounded)
            {
                SwitchState(_factory.Grounded());
            }
        }

        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubState()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}
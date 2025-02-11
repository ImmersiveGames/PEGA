using ImmersiveGames.HierarchicalStateMachine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class FallingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Fall;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;

        public FallingState(MovementContext currentMovementContext, MovementStateFactory factory) : base(currentMovementContext)
        {
            _ctx = currentMovementContext;
            _factory = factory;
        }

        protected internal override void OnEnter()
        {
            _ctx.isFalling = true;
            _ctx.CalculateJumpVariables();
            base.OnEnter();
        }

        protected override void Tick()
        {
            _ctx.ApplyGravity(falling: true);
            base.Tick();
        }

        protected override void OnExit()
        {
            _ctx.isFalling = false;
            base.OnExit();
        }

        protected override void CheckSwitchState()
        {
            if (_ctx.transform.position.y < _ctx.fallMaxHeight)
            {
                SwitchState(_factory.GetState(StatesNames.Dead));
                return;
            }

            if (_ctx.CharacterController.isGrounded)
            {
                SwitchState(_factory.GetState(StatesNames.Grounded));
            }
        }

        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubStatesOnEnter()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}
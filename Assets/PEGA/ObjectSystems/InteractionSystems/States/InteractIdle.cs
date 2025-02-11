using ImmersiveGames.HierarchicalStateMachine;

namespace PEGA.ObjectSystems.InteractionSystems.States
{
    public class InteractIdle : BaseState
    {
        private readonly HsmFactory _factory;

        public InteractIdle(StateContext currentMovementContext, HsmFactory factory) : base(currentMovementContext)
        {
            _factory = factory;
        }

        protected override StatesNames StateName=> StatesNames.InteractIdle;

        public override void Tick()
        {

        }
    }
}
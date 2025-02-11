using ImmersiveGames.HierarchicalStateMachine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class DeadState: BaseState
    {
        private readonly HsmFactory _factory;

        public DeadState(StateContext currentMovementContext, HsmFactory factory) : base(currentMovementContext)
        {
            _factory = factory;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            Ctx.GlobalNotifyStateEnter(StatesNames.Dead);
        }

        protected override StatesNames StateName => StatesNames.Dead;
    }
}
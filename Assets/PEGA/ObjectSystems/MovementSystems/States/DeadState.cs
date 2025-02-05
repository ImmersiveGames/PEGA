using ImmersiveGames.HierarchicalStateMachine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class DeadState: BaseState
    {
        public DeadState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext, factory)
        {
            IsRootState = true;
        }

        protected internal override void EnterState()
        {
            base.EnterState();
            Ctx.GlobalNotifyStateEnter(StatesNames.Dead);
        }

        protected override StatesNames StateName => StatesNames.Dead;
        protected override void UpdateState()
        {
            throw new System.NotImplementedException();
        }

        public override void CheckSwitchState()
        {
            throw new System.NotImplementedException();
        }

        public override void InitializeSubState()
        {
            throw new System.NotImplementedException();
        }
    }
}
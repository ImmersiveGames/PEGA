using ImmersiveGames.HierarchicalStateMachine;

namespace PEGA.ObjectSystems.InteractionSystems.States
{
    public class InteractIdle : BaseState
    {
        public InteractIdle(StateContext currentMovementContext, HsmFactory factory) : base(currentMovementContext, factory)
        {
        }

        public override StatesNames StateName=> StatesNames.InteractIdle;
        protected override void UpdateState()
        {

        }

        public override void CheckSwitchState()
        {

        }

        protected override void InitializeSubState()
        {
 
        }
    }
}
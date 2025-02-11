using ImmersiveGames.HierarchicalStateMachine;

namespace PEGA.ObjectSystems.InteractionSystems.States
{
    public class InteractIdle : BaseState
    {
        public InteractIdle(StateContext currentMovementContext, HsmFactory factory) : base(currentMovementContext)
        {
        }

        protected override StatesNames StateName=> StatesNames.InteractIdle;
        protected override void Tick()
        {

        }

        protected override void CheckSwitchState()
        {

        }

        protected override void InitializeSubStatesOnEnter()
        {
 
        }
    }
}
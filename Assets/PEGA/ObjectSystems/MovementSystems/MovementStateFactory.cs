using ImmersiveGames.HierarchicalStateMachine;
using PEGA.ObjectSystems.MovementSystems.States;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class MovementStateFactory : HsmFactory
    {
        public MovementStateFactory(MovementContext currentMovementContext) : base(currentMovementContext)
        {
            States[StatesNames.Idle] = new IdleState(currentMovementContext,this);
            States[StatesNames.Grounded] = new GroundedState(currentMovementContext,this);
            States[StatesNames.Jump] = new JumpingState(currentMovementContext,this);
            States[StatesNames.Walk] = new WalkingState(currentMovementContext,this);
            States[StatesNames.Fall] = new FallingState(currentMovementContext,this);
            States[StatesNames.Dash] = new DashState(currentMovementContext,this);
            States[StatesNames.Dawn] = new JumpingDownState(currentMovementContext,this);
        }

        public BaseState Idle()
        {
            return States[StatesNames.Idle];
        }

        public BaseState Grounded()
        {
            return States[StatesNames.Grounded];
        }

        public BaseState Jump()
        {
            return States[StatesNames.Jump];
        }

        public BaseState Walk()
        {
            return States[StatesNames.Walk];
        }
        public BaseState Fall()
        {
            return States[StatesNames.Fall];
        }
        public BaseState Down()
        {
            return States[StatesNames.Dawn];
        }
        public BaseState Dash()
        {
            return States[StatesNames.Dash];
        }
        
    }
}
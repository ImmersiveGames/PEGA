using System.Collections.Generic;
using PEGA.ObjectSystems.MovementSystems;
using PEGA.ObjectSystems.MovementSystems.States;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public class StateFactory
    {
        private readonly Dictionary<StatesNames, BaseState> _states = new Dictionary<StatesNames, BaseState>();

        public StateFactory(MovementContext currentMovementContext)
        {
            _states[StatesNames.Idle] = new IdleState(currentMovementContext,this);
            _states[StatesNames.Grounded] = new GroundedState(currentMovementContext,this);
            _states[StatesNames.Jump] = new JumpingState(currentMovementContext,this);
            _states[StatesNames.Walk] = new WalkingState(currentMovementContext,this);
            _states[StatesNames.Fall] = new FallingState(currentMovementContext,this);
            _states[StatesNames.Dash] = new DashState(currentMovementContext,this);
            _states[StatesNames.Dawn] = new JumpingDownState(currentMovementContext,this);
            _states[StatesNames.Dead] = new DeadState(currentMovementContext,this);
            
        }

        public BaseState Idle()
        {
            return _states[StatesNames.Idle];
        }

        public BaseState Grounded()
        {
            return _states[StatesNames.Grounded];
        }

        public BaseState Jump()
        {
            return _states[StatesNames.Jump];
        }

        public BaseState Walk()
        {
            return _states[StatesNames.Walk];
        }
        public BaseState Fall()
        {
            return _states[StatesNames.Fall];
        }
        public BaseState Down()
        {
            return _states[StatesNames.Dawn];
        }
        public BaseState Dash()
        {
            return _states[StatesNames.Dash];
        }
        public BaseState Dead()
        {
            return _states[StatesNames.Dead];
        }
        
    }

    public enum StatesNames
    {
        Idle,Grounded,Jump,Walk,Fall,Dash,Dawn,
        Dead
    }
}
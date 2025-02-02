using System.Collections.Generic;
using ImmersiveGames.HierarchicalStateMachine.States;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public class StateFactory
    {
        private readonly Dictionary<StatesNames, BaseState> _states = new Dictionary<StatesNames, BaseState>();

        public StateFactory(StateMachineContext currentStateMachineContext)
        {
            _states[StatesNames.Idle] = new IdleState(currentStateMachineContext,this);
            _states[StatesNames.Grounded] = new GroundedState(currentStateMachineContext,this);
            _states[StatesNames.Jump] = new JumpingState(currentStateMachineContext,this);
            _states[StatesNames.Walk] = new WalkingState(currentStateMachineContext,this);
            _states[StatesNames.Fall] = new FallingState(currentStateMachineContext,this);
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
        
    }

    public enum StatesNames
    {
        Idle,Grounded,Jump,Walk,Fall
    }
}
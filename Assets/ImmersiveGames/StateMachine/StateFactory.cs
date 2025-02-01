using System.Collections.Generic;
using ImmersiveGames.StateMachine.States;

namespace ImmersiveGames.StateMachine
{
    public class StateFactory
    {
        private readonly Dictionary<StatesNames, BaseState> _states = new Dictionary<StatesNames, BaseState>();

        public StateFactory(ContextStates currentContext)
        {
            _states[StatesNames.Idle] = new IdleState(currentContext,this);
            _states[StatesNames.Grounded] = new GroundedState(currentContext,this);
            _states[StatesNames.Jump] = new JumpingState(currentContext,this);
            _states[StatesNames.Walk] = new WalkingState(currentContext,this);
            _states[StatesNames.Fall] = new FallingState(currentContext,this);
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

    internal enum StatesNames
    {
        Idle,Grounded,Jump,Walk,Fall
    }
}
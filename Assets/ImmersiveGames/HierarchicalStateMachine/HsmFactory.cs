using System.Collections.Generic;
using PEGA.ObjectSystems.MovementSystems.States;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public class HsmFactory
    {
        protected readonly Dictionary<StatesNames, BaseState> States = new Dictionary<StatesNames, BaseState>();

        protected HsmFactory(StateContext currentContext)
        {
            States[StatesNames.Dead] = new DeadState(currentContext,this);
        }
        public BaseState Dead()
        {
            return States[StatesNames.Dead];
        }
    }
    
    
    public enum StatesNames
    {
        Idle,Grounded,Jump,Walk,Fall,Dash,Dawn,
        Dead,
        InteractIdle
    }
}
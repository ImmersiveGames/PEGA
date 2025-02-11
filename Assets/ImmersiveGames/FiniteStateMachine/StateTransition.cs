using System;

namespace ImmersiveGames.FiniteStateMachine
{
    public class StateTransition
    {
        public IState To { get; }
        public Func<bool> Condition { get; }

        public StateTransition(IState to, Func<bool> condition)
        {
            To = to ?? throw new ArgumentNullException(nameof(to));
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }
        
    }
}
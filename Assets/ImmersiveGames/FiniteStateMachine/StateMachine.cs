using System;
using System.Collections.Generic;

namespace ImmersiveGames.FiniteStateMachine
{
    public class StateMachine
    {
        private IState _currentState;
        private Dictionary<Type, List<StateTransition>> _transitions = new();
        private List<StateTransition> _currentTransitions = new();
        private List<StateTransition> _anyTransitions = new();
        private static readonly List<StateTransition> EmptyTransitions = new();

        public IState CurrentState => _currentState;

        public void Tick()
        {
            var transition = GetTransition();
            if (transition != null)
                SetState(transition.To);
            
            _currentState?.Tick();
        }

        public void SetState(IState newState)
        {
            if (newState == _currentState)
                return;

            _currentState?.OnExit();
            _currentState = newState;

            _transitions.TryGetValue(_currentState.GetType(), out _currentTransitions);
            _currentTransitions ??= EmptyTransitions;

            _currentState.OnEnter();
        }

        public void AddTransition(IState from, IState to, Func<bool> predicate)
        {
            if (!_transitions.TryGetValue(from.GetType(), out var transitions))
            {
                transitions = new List<StateTransition>();
                _transitions[from.GetType()] = transitions;
            }
            
            transitions.Add(new StateTransition(to, predicate));
        }

        public void AddAnyTransition(IState state, Func<bool> predicate)
        {
            _anyTransitions.Add(new StateTransition(state, predicate));
        }

        private StateTransition GetTransition()
        {
            foreach (var transition in _anyTransitions)
                if (transition.Condition())
                    return transition;
            
            foreach (var transition in _currentTransitions)
                if (transition.Condition())
                    return transition;

            return null;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveGames.TesteHSM
{
    public class State
    {
        public string StateName { get; }
        protected readonly StateMachine StateMachine;
        protected readonly Dictionary<string, State> _transitions = new();

        public State(string stateName, StateMachine stateMachine)
        {
            StateName = stateName;
            StateMachine = stateMachine;
            StateMachine.RegisterState(this);
        }

        public virtual void Enter()
        {
            Debug.Log($"🟢 ENTER: {StateName}");
        }

        public virtual void Exit()
        {
            Debug.Log($"🔴 EXIT: {StateName}");
        }

        public virtual void Update()
        {
            Debug.Log($"🔄 UPDATE: {StateName}");
        }

        public void AddTransition(string trigger, State targetState)
        {
            _transitions[trigger] = targetState;
        }

        public virtual bool TryTransition(string trigger)
        {
            if (_transitions.TryGetValue(trigger, out var nextState))
            {
                StateMachine.SetState(nextState);
                return true;
            }

            return false;
        }
    }
}
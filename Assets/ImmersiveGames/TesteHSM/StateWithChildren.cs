using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ImmersiveGames.TesteHSM
{
    public class StateWithChildren : State
    {
        private readonly Dictionary<string, State> _children = new();
        public State ActiveChild;
        public StateWithChildren Owner { get; }

        public StateWithChildren(string stateName, StateMachine stateMachine, StateWithChildren owner = null)
            : base(stateName, stateMachine)
        {
            Owner = owner;
        }

        public void AddChild(State child)
        {
            _children[child.StateName] = child;
        }

        public void SetActiveChild(string stateName)
        {
            if (!_children.TryGetValue(stateName, out var child)) return;

            if (ActiveChild != null)
            {
                ActiveChild.Exit();
            }

            ActiveChild = child;
            ActiveChild.Enter();
        }

        public override void Update()
        {
            base.Update();
            ActiveChild?.Update();
        }

        public override bool TryTransition(string trigger)
        {
            if (_transitions.TryGetValue(trigger, out var nextState))
            {
                if (_children.ContainsKey(nextState.StateName))
                {
                    SetActiveChild(nextState.StateName);
                    return true;
                }
                else if (Owner != null)
                {
                    return Owner.TryTransition(trigger);
                }
            }

            return false;
        }

        public void ExitChildren()
        {
            if (ActiveChild != null)
            {
                //OnExitState?.Invoke(_activeChild.StateName);
                ActiveChild.Exit();
                ActiveChild = null;
            }
        }

        public void SetStateWithFirstChild()
        {
            if (_children.Count > 0 && ActiveChild == null) // Evita reentradas
            {
                var firstChild = _children.Values.First();
                SetActiveChild(firstChild.StateName);
            }
        }
    }
}
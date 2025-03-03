using System;
using UnityEngine;
using System.Collections.Generic;

namespace ImmersiveGames.TesteHSM
{
    public class StateMachine
    {
        private State _activeState;
        private readonly Dictionary<string, State> _allStates = new();

        public event Action<string> OnEnterState;
        public event Action<string> OnExitState;

        public void RegisterState(State state)
        {
            _allStates[state.StateName] = state;
        }

        public T GetState<T>(string stateName) where T : State
        {
            return _allStates.ContainsKey(stateName) ? _allStates[stateName] as T : null;
        }

        public void SetState(State newState)
        {
            if (_activeState == newState) return; // Evita reentradas desnecessárias

            if (_activeState != null)
            {
                // Se o estado atual tem filhos, sai primeiro do filho ativo
                if (_activeState is StateWithChildren activeWithChildren)
                {
                    activeWithChildren.ExitChildren();
                }

                OnExitState?.Invoke(_activeState.StateName);
                _activeState.Exit();
            }

            _activeState = newState;

            if (_activeState != null)
            {
                OnEnterState?.Invoke(_activeState.StateName);
                _activeState.Enter();

                // Apenas ativa o primeiro filho se o estado for um pai e não foi ativado antes
                if (_activeState is StateWithChildren newWithChildren && newWithChildren.ActiveChild == null)
                {
                    newWithChildren.SetStateWithFirstChild();
                }
            }
        }




        public bool TryTransition(string trigger)
        {
            return _activeState?.TryTransition(trigger) ?? false;
        }

        public void Update()
        {
            _activeState?.Update();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveGames.StateModuleSystems
{
    public class StateManager : MonoBehaviour
    {
        [SerializeField] private Blackboard blackboard;
        [SerializeField] private StateModule initialState;
        private readonly List<StateModule> _activeStates = new List<StateModule>();

        void Start()
        {
            if (initialState != null) ActivateState(initialState);
        }

        void Update()
        {
            foreach (var state in _activeStates.ToArray())
            {
                state.UpdateState();
            }

            Debug.Log($"Estados Ativos: {GetActiveStatesLog()}");
        }

        public void ActivateState(StateModule newState)
        {
            if (_activeStates.Contains(newState)) return;

            // Remove apenas estados de prioridade MAIOR que podem ser interrompidos
            _activeStates.RemoveAll(s => 
                s.Priority < newState.Priority && 
                s.CanBeInterrupted()
            );

            _activeStates.Add(newState);
            newState.Enter();
            Debug.Log($"[STATE] Entrou: {newState.GetType().Name}");
        }

        public void DeactivateState(StateModule state)
        {
            if (_activeStates.Contains(state))
            {
                state.Exit();
                _activeStates.Remove(state);
                Debug.Log($"[STATE] Saiu: {state.GetType().Name}");

                if (_activeStates.Count == 0 && initialState != null)
                {
                    ActivateState(initialState);
                }
            }
        }

        private string GetActiveStatesLog()
        {
            if (_activeStates.Count == 0) return "Nenhum";
            return string.Join(" | ", _activeStates.ConvertAll(s => s.GetType().Name));
        }
    }
}
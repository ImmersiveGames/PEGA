using System;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine.States
{
    public abstract class BaseState
    {
        public event Action<StatesNames> OnStateEntered;
        public event Action<StatesNames> OnStateExited;
        
        protected bool IsRootState = false;
        protected readonly StateMachineContext Ctx;
        protected readonly StateFactory Factory;
        protected BaseState _currentSuperstate;
        private BaseState _currentSubState;

        protected abstract StatesNames StateName { get; }
        protected BaseState(StateMachineContext currentStateMachineContext, StateFactory factory)
        {
            Ctx = currentStateMachineContext;
            Factory = factory;
        }
        public void UpdateStates()
        {
            UpdateState();
            _currentSubState?.UpdateStates();
        }
        public void EnterStates()
        {
            EnterState();
            _currentSubState?.EnterStates();
        }
        public void ExitStates()
        {
            ExitState();
            _currentSubState?.ExitStates();
        }
        protected internal virtual void EnterState()
        {
            OnStateEntered?.Invoke(StateName);
            Debug.Log($"[{StateName}] Enter");
        }
        
        protected abstract void UpdateState();

        protected virtual void ExitState()
        {
            OnStateExited?.Invoke(StateName);
            Debug.Log($"[{StateName}] Exit");
        }
        public abstract void CheckSwitchState();
        public abstract void InitializeSubState();
        
        protected void SwitchState(BaseState newState)
        {
            // 🔹 Sai do subestado atual antes de sair do estado principal
            _currentSubState?.ExitState();

            // 🔹 Sai do superestado
            ExitState();

            // 🔹 Entra no novo estado
            newState.EnterState();

            if (IsRootState)
            {
                Ctx.CurrentState = newState; // Se for root, troca no ContextStates
            }
            else
            {
                _currentSuperstate?.SetSubState(newState); // Se for subestado, troca dentro do superestado
            }
        }

        private void SetSuperState(BaseState newSuperState)
        {
            _currentSuperstate = newSuperState;
        }

        protected internal void SetSubState(BaseState newSubState)
        {
            // 🔹 Sai do subestado atual ANTES de trocar
            _currentSubState?.ExitState();

            // 🔹 Atualiza o subestado e chama o Enter
            _currentSubState = newSubState;
            _currentSubState.EnterState();

            // 🔹 Define este estado como superestado do novo subestado
            newSubState.SetSuperState(this);
        }

    }
}
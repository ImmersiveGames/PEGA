using System;
using ImmersiveGames.HierarchicalStateMachine.Interface;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine.States
{
    public abstract class BaseState
    {
        public event Action<StatesNames> OnStateEntered;
        public event Action<StatesNames> OnStateExited;
        
        protected bool IsRootState = false;
        protected readonly ContextStates Ctx;
        protected readonly StateFactory Factory;
        private BaseState _currentSuperstate;
        private BaseState _currentSubState;

        protected abstract StatesNames StateName { get; }
        protected BaseState(ContextStates currentContext, StateFactory factory)
        {
            Ctx = currentContext;
            Factory = factory;
        }
        public void UpdateStates()
        {
            UpdateState();
            _currentSubState?.UpdateStates();
        }
        public void ExitStates()
        {
            ExitState();
            _currentSubState?.ExitStates();
        }
        public virtual void EnterState()
        {
            OnStateEntered?.Invoke(StateName);
        }
        
        protected abstract void UpdateState();

        protected virtual void ExitState()
        {
            OnStateExited?.Invoke(StateName);
        }
        public abstract void CheckSwitchState();
        public abstract void InitializeSubState();
        
        protected void SwitchState(BaseState newState)
        {
            ExitState();
            
            newState.EnterState();

            if(IsRootState)
                Ctx.CurrentState = newState;
            else
            {
                _currentSuperstate?.SetSubState(newState);
            }
        }

        private void SetSuperState(BaseState newSuperState)
        {
            _currentSuperstate = newSuperState;
        }

        protected void SetSubState(BaseState newSubState)
        {
            if (newSubState is ISubState sub && !sub.IsValidSuperState(this)) {
                Debug.LogError("Superestado inválido para este subestado!");
                return;
            }
            _currentSubState = newSubState; //define um sub state para ele
            newSubState.SetSuperState(this);//ao mesmo tempo que torna este superstate do próximo.
        }
    }
}
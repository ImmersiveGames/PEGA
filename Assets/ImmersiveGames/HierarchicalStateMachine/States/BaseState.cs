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
        private BaseState _currentSuperstate;
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
            _currentSubState = newSubState; //define um sub state para ele
            newSubState.SetSuperState(this);//ao mesmo tempo que torna este superstate do próximo.
        }
    }
}
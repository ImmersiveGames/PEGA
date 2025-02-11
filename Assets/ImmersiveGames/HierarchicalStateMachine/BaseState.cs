using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveGames.DebugSystems;
using ImmersiveGames.FiniteStateMachine;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public abstract class BaseState : IHierarchicalState
    {
        #region Events and Fields

        public event Action<StatesNames> OnStateEntered;
        public event Action<StatesNames> OnStateExited;

        private readonly List<StateTransition> _stateTransitions = new();
        private readonly List<StateTransition> _subStateTransitions = new();
        
        private IHierarchicalState _currentSuperstate;
        private IHierarchicalState _currentSubState;
        
        protected readonly IStateContext Ctx;

        #endregion

        #region Abstract Properties

        protected abstract StatesNames StateName { get; }

        #endregion

        #region Constructor

        protected BaseState(IStateContext currentMovementContext)
        {
            Ctx = currentMovementContext;
        }

        #endregion

        #region Public Interface

        public void UpdateStates()
        {
            Tick();
            _currentSubState?.UpdateStates();
        }

        public virtual void OnEnter()
        {
            SetupTransitions();
            InitializeSubState();
            NotifyStateEntry();
            DebugManager.Log<BaseState>($"[{StateName}] Enter");
        }

        public virtual void Tick()
        {
            CheckSwitchState();
        }

        public virtual void OnExit()
        {
            NotifyStateExit();
            CleanupResources();
            DebugManager.Log<BaseState>($"[{StateName}] Exit");
        }

        public void SetSuperState(IHierarchicalState newSuperState)
        {
            _currentSuperstate = newSuperState;
        }

        public void SwitchSubState(IState newSubState)
        {
            if (_currentSubState == newSubState) return;

            _currentSubState?.OnExit();
            _currentSubState = (IHierarchicalState)newSubState;
            _currentSubState.OnEnter();
            _currentSubState.SetSuperState(this);
        }

        #endregion

        #region State Transition Logic

        private void CheckSwitchState()
        {
            CheckMainStateTransitions();
            CheckSubStateTransitions();
        }

        private void CheckMainStateTransitions()
        {
            foreach (var transition in _stateTransitions)
            {
                if (transition.Condition())
                {
                    SwitchState(transition.To);
                    return;
                }
            }
        }

        private void CheckSubStateTransitions()
        {
            foreach (var transition in _subStateTransitions)
            {
                if (transition.Condition())
                {
                    SwitchSubState(transition.To);
                    return;
                }
            }
        }

        private void SwitchState(IState newState)
        {
            if (newState == this) return;

            _currentSubState?.OnExit();
            OnExit();
            newState.OnEnter();

            UpdateContextState(newState);
        }

        private void UpdateContextState(IState newState)
        {
            if (_currentSuperstate == null)
            {
                Ctx.CurrentState = (IHierarchicalState)newState;
            }
            else
            {
                _currentSuperstate.SwitchSubState(newState);
            }
        }

        #endregion

        #region Transitions Configuration

        protected virtual void SetupTransitions() { }

        protected void AddTransition(IState toState, Func<bool> condition)
        {
            _stateTransitions.Add(new StateTransition(toState, condition));
        }

        protected void AddSubStateTransition(IState toSubState, Func<bool> condition)
        {
            _subStateTransitions.Add(new StateTransition(toSubState, condition));
        }

        #endregion

        #region Helper Methods

        private void InitializeSubState()
        {
            var initialTransition = _subStateTransitions.FirstOrDefault(t => t.Condition());
            if (initialTransition != null)
            {
                SwitchSubState(initialTransition.To);
            }
        }

        private void NotifyStateEntry()
        {
            Ctx.GlobalNotifyStateEnter(StateName);
            OnStateEntered?.Invoke(StateName);
        }

        private void NotifyStateExit()
        {
            Ctx.GlobalNotifyStateExit(StateName);
            OnStateExited?.Invoke(StateName);
        }

        private void CleanupResources()
        {
            OnStateEntered = null;
            OnStateExited = null;
        }

        #endregion
    }
}
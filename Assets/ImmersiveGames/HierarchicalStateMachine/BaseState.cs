using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveGames.DebugSystems;
using ImmersiveGames.FiniteStateMachine;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public abstract class BaseState: IHierarchicalState
    {
        public event Action<StatesNames> OnStateEntered;
        public event Action<StatesNames> OnStateExited;
        
        private readonly List<StateTransition> _stateTransitions = new(); // Transições entre estados principais
        private readonly List<StateTransition> _subStateTransitions = new(); // Transições entre sobestados

        protected readonly IStateContext Ctx;
        private IHierarchicalState _currentSuperstate;
        private IHierarchicalState _currentSubState;

        protected abstract StatesNames StateName { get; }

        protected BaseState(IStateContext currentMovementContext)
        {
            Ctx = currentMovementContext;
        }

        public void UpdateStates()
        {
            Tick();
            _currentSubState?.UpdateStates();
        }

        public virtual void OnEnter()
        {
            SetupTransitions(); // 🔹 Chama o método de transições antes de entrar no estado
            InitializeSubState(); // 🔹 Garante que um subestado é ativado imediatamente
            Ctx.GlobalNotifyStateEnter(StateName);
            OnStateEntered?.Invoke(StateName);
            DebugManager.Log<BaseState>($"[{StateName}] Enter");
        }

        public virtual void Tick()
        {
            CheckSwitchState(); // 🔹 Agora só verifica mudanças dentro do mesmo nível
        }

        public virtual void OnExit()
        {
            Ctx.GlobalNotifyStateExit(StateName);
            OnStateExited?.Invoke(StateName);
            UnsubscribeEvents();
            DebugManager.Log<BaseState>($"[{StateName}] Exit");
        }

        #region Transitions
        /// <summary>
        /// Método opcional para configurar transições (substituído nos estados concretos)
        /// </summary>
        protected virtual void SetupTransitions() { } // 🔹 Método vazio por padrão

        /// <summary>
        /// Adiciona uma transição entre estados principais.
        /// </summary>
        protected void AddTransition(IState toState, Func<bool> condition)
        {
            _stateTransitions.Add(new StateTransition(toState, condition));
        }

        /// <summary>
        /// Adiciona uma transição entre sobestados.
        /// </summary>
        protected void AddSubStateTransition(IState toSubState, Func<bool> condition)
        {
            _subStateTransitions.Add(new StateTransition(toSubState, condition));
        }
        #endregion

        /// <summary>
        /// Verifica se há uma transição válida dentro do mesmo nível hierárquico.
        /// </summary>
        private void CheckSwitchState()
        {
            foreach (var transition in _stateTransitions.Where(transition => transition.Condition()))
            {
                SwitchState(transition.To);
                return;
            }

            // 🔹 Só verifica transições de subestado dentro do mesmo nível, sem chamar o superestado
            foreach (var transition in _subStateTransitions.Where(transition => transition.Condition()))
            {
                SwitchSubState(transition.To);
                return;
            }
        }

        /// <summary>
        /// Ativa imediatamente o subestado inicial ao entrar no estado pai.
        /// </summary>
        private void InitializeSubState()
        {
            foreach (var transition in _subStateTransitions.Where(transition => transition.Condition()))
            {
                SwitchSubState(transition.To);
                return;
            }
        }

        private void SwitchState(IState newState)
        {
            if (newState == this) return;

            _currentSubState?.OnExit();
            OnExit();
            newState.OnEnter();

            if (_currentSuperstate == null)
            {
                Ctx.CurrentState = (IHierarchicalState)newState;
            }
            else
            {
                _currentSuperstate.SwitchSubState(newState);
            }
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

        private void UnsubscribeEvents()
        {
            OnStateEntered = null;
            OnStateExited = null;
        }
    }
}

using System;
using ImmersiveGames.DebugSystems;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public abstract class BaseState
    {
        public event Action<StatesNames> OnStateEntered;
        public event Action<StatesNames> OnStateExited;
        
        protected bool IsRootState = false;
        protected readonly IStateContext Ctx;
        protected readonly HsmFactory Factory;
        protected BaseState CurrentSuperstate;
        private BaseState _currentSubState;

        public abstract StatesNames StateName { get; }
        protected BaseState(StateContext currentMovementContext, HsmFactory factory)
        {
            Ctx = currentMovementContext;
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
            InitializeSubState();
            Ctx.GlobalNotifyStateEnter(StateName);
            OnStateEntered?.Invoke(StateName);
            DebugManager.Log<BaseState>($"[{StateName}] Enter");
        }

        protected virtual void UpdateState()
        {
            CheckSwitchState(); 
        }

        public virtual void ExitState()
        {
            Ctx.GlobalNotifyStateExit(StateName);
            OnStateExited?.Invoke(StateName);
            UnsubscribeEvents();
            DebugManager.Log<BaseState>($"[{StateName}] Exit");
        }
        //Cada estado cuida de como vai transicionar entre seus irmãos hierárquicos não sub estados.
        protected abstract void CheckSwitchState();
        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected abstract void InitializeSubState();
        
        protected void SwitchState(BaseState newState)
        {
            // 🔹 Sai do subestado atual antes de sair do estado principal
            _currentSubState?.ExitState();

            // 🔹 Sai do superestado
            ExitState();

            // 🔹 Entra no novo estado
            newState.EnterState();

            //Necessário se for um root state
            if (IsRootState)
            {
                Ctx.CurrentState = newState; // Se for root, troca no ContextStates
            }
            else
            {
                StateTransitionManager.SwitchState(this, newState, Ctx);
                //CurrentSuperstate?.SwitchSubState(newState); // Se for subestado, troca dentro do superestado
            }
        }

        private void SetSuperState(BaseState newSuperState)
        {
            CurrentSuperstate = newSuperState;
        }

        protected internal void SwitchSubState(BaseState newSubState)
        {
            // 🔹 Sai do subestado atual ANTES de trocar
            _currentSubState?.ExitState();

            // 🔹 Atualiza o subestado e chama o Enter
            _currentSubState = newSubState;
            _currentSubState.EnterState();

            // 🔹 Define este estado como superestado do novo subestado
            newSubState.SetSuperState(this);
        }

        private void UnsubscribeEvents()
        {
            OnStateEntered = null;
            OnStateExited = null;
        }

    }
}
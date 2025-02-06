using System;
using ImmersiveGames.DebugSystems;
using PEGA.ObjectSystems.MovementSystems;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public abstract class BaseState
    {
        public event Action<StatesNames> OnStateEntered;
        public event Action<StatesNames> OnStateExited;
        
        protected bool IsRootState = false;
        protected readonly MovementContext Ctx;
        protected readonly StateFactory Factory;
        protected BaseState CurrentSuperstate;
        private BaseState _currentSubState;

        protected abstract StatesNames StateName { get; }
        protected BaseState(MovementContext currentMovementContext, StateFactory factory)
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
        
        protected abstract void UpdateState();

        protected virtual void ExitState()
        {
            Ctx.GlobalNotifyStateExit(StateName);
            OnStateExited?.Invoke(StateName);
            DebugManager.Log<BaseState>($"[{StateName}] Exit");
        }
        //Cada estado cuida de como vai transicionar entre seus irmãos hierárquicos não sub estados.
        public abstract void CheckSwitchState();
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
                CurrentSuperstate?.SwitchSubState(newState); // Se for subestado, troca dentro do superestado
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

    }
}
using System;
using ImmersiveGames.DebugSystems;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public abstract class BaseState
    {
        public event Action<StatesNames> OnStateEntered;
        public event Action<StatesNames> OnStateExited;
        
        protected readonly IStateContext Ctx;
        protected readonly HsmFactory Factory;
        protected BaseState CurrentSuperstate;
        private BaseState _currentSubState;

        protected abstract StatesNames StateName { get; }

        protected BaseState(IStateContext currentMovementContext, HsmFactory factory)
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

        // Cada estado cuida de como vai transicionar para seus irmãos (não subestados).
        protected abstract void CheckSwitchState();
        
        // Inicializa qual sub estado deve ser ativado ao entrar nesse estado
        protected abstract void InitializeSubState();
        
        /// <summary>
        /// Troca de estado de maneira segura, sem depender de IsRootState
        /// </summary>
        protected void SwitchState(BaseState newState)
        {
            if (newState == this) return; // 🔹 Evita trocas desnecessárias

            // 🔹 Sai do subestado atual antes de sair do estado principal
            _currentSubState?.ExitState();

            // 🔹 Sai do estado atual
            ExitState();

            // 🔹 Entra no novo estado
            newState.EnterState();

            // 🔹 Se não houver um superestado, significa que este é o estado raiz
            if (CurrentSuperstate == null)
            {
                Ctx.CurrentState = newState; // 🔹 Atualiza o contexto com o novo estado
            }
            else
            {
                // 🔹 Se for um subestado, troca dentro do superestado
                CurrentSuperstate.SwitchSubState(newState);
            }
        }

        private void SetSuperState(BaseState newSuperState)
        {
            CurrentSuperstate = newSuperState;
        }

        /// <summary>
        /// Troca de subestado de maneira segura
        /// </summary>
        protected internal void SwitchSubState(BaseState newSubState)
        {
            if (_currentSubState == newSubState) return; // 🔹 Evita reinicializações desnecessárias

            // 🔹 Sai do subestado atual antes de trocar
            _currentSubState?.ExitState();

            // 🔹 Atualiza o subestado e o ativa
            _currentSubState = newSubState;
            _currentSubState.EnterState();

            // 🔹 Define o superestado do novo subestado
            newSubState.SetSuperState(this);
        }

        private void UnsubscribeEvents()
        {
            OnStateEntered = null;
            OnStateExited = null;
        }
    }
}

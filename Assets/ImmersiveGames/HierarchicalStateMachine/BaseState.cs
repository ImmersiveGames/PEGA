using System;
using ImmersiveGames.DebugSystems;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public abstract class BaseState
    {
        public event Action<StatesNames> OnStateEntered;
        public event Action<StatesNames> OnStateExited;
        
        protected readonly IStateContext Ctx;
        protected BaseState InMySuperState;
        private BaseState _currentSubState;

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
        protected internal virtual void OnEnter()
        {
            InitializeSubStatesOnEnter();
            Ctx.GlobalNotifyStateEnter(StateName);
            OnStateEntered?.Invoke(StateName);
            DebugManager.Log<BaseState>($"[{StateName}] Enter");
        }

        protected virtual void Tick()
        {
            CheckSwitchState(); 
        }

        protected virtual void OnExit()
        {
            Ctx.GlobalNotifyStateExit(StateName);
            OnStateExited?.Invoke(StateName);
            UnsubscribeEvents();
            DebugManager.Log<BaseState>($"[{StateName}] Exit");
        }

        // Cada estado cuida de como vai transicionar para seus irmãos (não subestados).
        protected abstract void CheckSwitchState();
        
        // Inicializa qual sub estado deve ser ativado ao entrar nesse estado
        protected abstract void InitializeSubStatesOnEnter();
        
        /// <summary>
        /// Troca de estado de maneira segura, sem depender de IsRootState
        /// </summary>
        protected void SwitchState(BaseState newState)
        {
            if (newState == this) return; // 🔹 Evita trocas desnecessárias

            // 🔹 Sai do subestado atual antes de sair do estado principal
            _currentSubState?.OnExit();

            // 🔹 Sai do estado atual
            OnExit();

            // 🔹 Entra no novo estado
            newState.OnEnter();

            // 🔹 Se não houver um superestado, significa que este é o estado raiz
            if (InMySuperState == null)
            {
                Ctx.CurrentState = newState; // 🔹 Atualiza o contexto com o novo estado
            }
            else
            {
                // 🔹 Se for um subestado, troca dentro do superestado
                InMySuperState.SwitchSubState(newState);
            }
        }

        private void SetSuperState(BaseState newSuperState)
        {
            InMySuperState = newSuperState;
        }

        /// <summary>
        /// Troca de subestado de maneira segura
        /// </summary>
        protected internal void SwitchSubState(BaseState newSubState)
        {
            if (_currentSubState == newSubState) return; // 🔹 Evita reinicializações desnecessárias

            // 🔹 Sai do subestado atual antes de trocar
            _currentSubState?.OnExit();

            // 🔹 Atualiza o subestado e o ativa
            _currentSubState = newSubState;
            _currentSubState.OnEnter();

            // 🔹 Define como o próprio superestado do novo subestado
            newSubState.SetSuperState(this);
        }

        private void UnsubscribeEvents()
        {
            OnStateEntered = null;
            OnStateExited = null;
        }
    }
}

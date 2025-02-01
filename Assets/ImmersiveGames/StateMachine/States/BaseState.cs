namespace ImmersiveGames.StateMachine.States
{
    public abstract class BaseState
    {
        protected bool IsRootState = false;
        protected readonly ContextStates Ctx;
        protected readonly StateFactory Factory;
        private BaseState _currentSuperstate;
        private BaseState _currentSubState;

        protected BaseState(ContextStates currentContext, StateFactory factory)
        {
            Ctx = currentContext;
            Factory = factory;
        }
        public abstract void EnterState();
        protected abstract void UpdateState();
        protected abstract void ExitState();
        public abstract void CheckSwitchState();
        public abstract void InitializeSubState();

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

        protected void SwitchState(BaseState newState)
        {
            ExitState();
            
            newState.ExitState();

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
            newSubState.SetSuperState(this);//ao mesmo tempo que torna este superstate do proximo.
        }
    }
}
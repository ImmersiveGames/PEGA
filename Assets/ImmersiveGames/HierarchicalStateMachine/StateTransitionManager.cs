namespace ImmersiveGames.HierarchicalStateMachine
{
    public abstract class StateTransitionManager
    {
        public static void SwitchState(BaseState currentState, BaseState newState, IStateContext ctx)
        {
            currentState.ExitState();
            newState.EnterState();
            ctx.GlobalNotifyStateEnter(newState.StateName);
        }
    }
}
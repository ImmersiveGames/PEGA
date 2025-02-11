namespace ImmersiveGames.HierarchicalStateMachine
{
    public interface IStateContext
    {
        void GlobalNotifyStateEnter(StatesNames state);
        void GlobalNotifyStateExit(StatesNames state);
        IHierarchicalState CurrentState { get; set; }
    }
}
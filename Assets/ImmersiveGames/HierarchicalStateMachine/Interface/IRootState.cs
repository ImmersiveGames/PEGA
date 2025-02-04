namespace ImmersiveGames.HierarchicalStateMachine.Interface
{
    public interface ISubState {
        bool IsValidSuperState(BaseState superState);
    }
}
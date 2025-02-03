using ImmersiveGames.HierarchicalStateMachine.States;

namespace ImmersiveGames.HierarchicalStateMachine.Interface
{
    public interface IRootState
    {
        void HandleGravity();
        
    }
    public interface ISubState {
        bool IsValidSuperState(BaseState superState);
    }
}
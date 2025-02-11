using ImmersiveGames.FiniteStateMachine;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public interface IHierarchicalState: IState
    {
        void UpdateStates();
        void SwitchSubState(IState newState);
        void SetSuperState(IHierarchicalState newSuperState);
    }
}
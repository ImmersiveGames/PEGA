using ImmersiveGames.HierarchicalStateMachine;
using PEGA.ObjectSystems.InteractionSystems.States;

namespace PEGA.ObjectSystems.InteractionSystems
{
    public class InteractionFactory : HsmFactory
    {
        public InteractionFactory(InteractionContext currentInteractionContext) : base(currentInteractionContext)
        {
            RegisterState(StatesNames.InteractIdle, () => new InteractIdle(currentInteractionContext, this));
        }
    }
}
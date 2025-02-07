using ImmersiveGames.HierarchicalStateMachine;
using PEGA.ObjectSystems.MovementSystems.Drivers;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.InteractionSystems
{
    public class InteractionsController : DriverController
    {
        private InteractionFactory _interactionStates;
        private InteractionContext _interactionContext;

        protected void Awake()
        {
            _interactionContext = GetComponent<InteractionContext>();
            Initialize(
                new PlayerMovementDriver(GetComponent<PlayerInput>()), 
                new NullMovementDriver(transform), 
                _interactionContext
            );
            
            _interactionStates = new InteractionFactory(_interactionContext); //Cria a fabric usando este contexto
            _interactionContext.CurrentState = _interactionStates.GetState(StatesNames.InteractIdle); //cria um estado corrente para iniciar o jogo em grounded (um dos roots)
            _interactionContext.CurrentState.EnterState(); //Inicia o Grounded para iniciar o jogo em um estado.
            
        }
        protected override void Update()
        {
            base.Update();
            _interactionContext.CurrentState.CheckSwitchState();
            
            //_movementContext.movementDirection = Context.ActualDriver.GetMovementDirection();
            
            _interactionContext.CurrentState.UpdateStates();
        }
    }
}
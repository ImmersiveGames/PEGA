using ImmersiveGames.HierarchicalStateMachine;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.MovementSystems.Drivers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.InteractionSystems
{
    public class InteractionsController : MonoBehaviour
    {
        private InteractionFactory _interactionStates;
        private InteractionContext _interactionContext;
        
        private DriverController _driverController;

        private void Awake()
        {
            _interactionContext = GetComponent<InteractionContext>();
            _driverController = new DriverController(new PlayerInputDriver(GetComponent<PlayerInput>()),
                new NullInputDriver(transform));
            
            _interactionStates = new InteractionFactory(_interactionContext); //Cria a fabric usando este contexto
            _interactionContext.CurrentState = _interactionStates.GetState(StatesNames.InteractIdle); //cria um estado corrente para iniciar o jogo em grounded (um dos roots)
            _interactionContext.CurrentState.EnterState(); //Inicia o Grounded para iniciar o jogo em um estado.
            
        }
        private void Update()
        {
            _driverController.Update();
            _interactionContext.CurrentState.CheckSwitchState();
            
            //_movementContext.movementDirection = Context.ActualDriver.GetMovementDirection();
            
            _interactionContext.CurrentState.UpdateStates();
        }
    }
}
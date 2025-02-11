using System;
using ImmersiveGames.HierarchicalStateMachine;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.DriverSystems;
using PEGA.ObjectSystems.DriverSystems.Drivers;
using PEGA.ObjectSystems.DriverSystems.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.InteractionSystems
{
    public class InteractionsController : MonoBehaviour
    {
        private InteractionFactory _interactionStates;
        private InteractionContext _interactionContext;

        [SerializeField]
        private DriverType driverType;

        private void Awake()
        {
            _interactionContext = GetComponent<InteractionContext>();
            _interactionContext.InputDriver = InputDriverFactory.CreateDriver(driverType, transform);
            
            _interactionStates = new InteractionFactory(_interactionContext); //Cria a fabric usando este contexto
            _interactionContext.CurrentState = _interactionStates.GetState(StatesNames.InteractIdle); //cria um estado corrente para iniciar o jogo em grounded (um dos roots)
            _interactionContext.CurrentState.OnEnter(); //Inicia o Grounded para iniciar o jogo em um estado.
            
        }
        private void Update()
        {
            _interactionContext.InputDriver.UpdateDriver();
            
            _interactionContext.CurrentState.UpdateStates();
        }
        private IInputDriver SetObjectDriver(DriverType newDriverType)
        {
            IInputDriver driver = newDriverType switch
            {
                DriverType.Player => new PlayerInputDriver(GetComponent<PlayerInput>()),
                DriverType.AI => new NullInputDriver(transform),
                _ => throw new ArgumentOutOfRangeException(nameof(newDriverType), newDriverType, null)
            };
            var driverController = new DriverController(driver);
            return driverController.GetActualDriver();
        }
    }
}
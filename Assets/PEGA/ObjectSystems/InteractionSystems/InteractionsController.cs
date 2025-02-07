using ImmersiveGames.HierarchicalStateMachine;

namespace PEGA.ObjectSystems.InteractionSystems
{
    public class InteractionsController : DriverController
    {
        private InteractionFactory _interactionStates;
        private InteractionContext _interactionContext;

        protected override void Awake()
        {
            Context = _interactionContext = GetComponent<InteractionContext>();
            base.Awake();
            _interactionStates = new InteractionFactory(_interactionContext); //Cria a fabric usando este contexto
            Context.CurrentState = _interactionStates.InteractIdle(); //cria um estado corrente para iniciar o jogo em grounded (um dos roots)
            Context.CurrentState.EnterState(); //Inicia o Grounded para iniciar o jogo em um estado.
            
        }
        protected override void Update()
        {
            base.Update();
            Context.CurrentState.CheckSwitchState();
            
            //_movementContext.movementDirection = Context.ActualDriver.GetMovementDirection();
            
            Context.CurrentState.UpdateStates();
        }
    }
}
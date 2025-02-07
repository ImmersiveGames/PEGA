using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class MovementController : DriverController
    {
        private CharacterController _characterController;
        private MovementStateFactory _movementStates;
        private MovementContext _movementContext;

        protected override void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            Context = _movementContext = GetComponent<MovementContext>();
            
            base.Awake();

            _movementStates = new MovementStateFactory(_movementContext); //Cria a fabric usando este contexto
   
            Context.CurrentState = _movementStates.Fall(); //cria um estado corrente para iniciar o jogo em grounded (um dos roots)
            Context.CurrentState.EnterState(); //Inicia o Grounded para iniciar o jogo em um estado.
        }
        protected override void Update()
        {
            base.Update();
            Context.CurrentState.CheckSwitchState();
            _movementContext.movementDirection = Context.ActualDriver.GetMovementDirection();
            
            Context.CurrentState.UpdateStates();
            
            HandleRotate();
            _characterController.Move(_movementContext.appliedMovement * Time.deltaTime);
            DebugManager.Log<MovementController>($"Movement Direction: {_movementContext.movementDirection}, Jump: {Context.ActualDriver.IsJumpingPress}, Dash: {Context.ActualDriver.IsDashPress}");
        }
        private void HandleRotate()
        {
            Vector3 positionToLookAt;
            positionToLookAt.x = Context.ActualDriver.GetMovementDirection().x;
            positionToLookAt.y = 0.0f;
            positionToLookAt.z = Context.ActualDriver.GetMovementDirection().y;

            var currentRotation = transform.rotation;
            if (Context.ActualDriver.GetMovementDirection() == Vector2.zero) return;
            var targetRotation = Quaternion.LookRotation(positionToLookAt);
            transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, _movementContext.rotationPerFrame * Time.deltaTime);
        }
        
    }
}
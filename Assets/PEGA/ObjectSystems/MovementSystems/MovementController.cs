using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.MovementSystems.Drivers;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class MovementController : MonoBehaviour
    {
        private CharacterController _characterController;
        private MovementStateFactory _movementStates;
        private MovementContext _movementContext;
        
        
        protected void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _movementContext = GetComponent<MovementContext>();
            _movementStates = new MovementStateFactory(_movementContext); //Cria a fabric usando este contexto
   
            _movementContext.CurrentState = _movementStates.GetState(StatesNames.Fall); //cria um estado corrente para iniciar o jogo em grounded (um dos roots)
            _movementContext.CurrentState.EnterState(); //Inicia o Grounded para iniciar o jogo em um estado.
        }
        private void Update()
        {
            
            _movementContext.CurrentState.CheckSwitchState();
            _movementContext.movementDirection = _movementContext.InputDriver.GetMovementDirection();
            
            _movementContext.CurrentState.UpdateStates();
            
            HandleRotate();
            _characterController.Move(_movementContext.appliedMovement * Time.deltaTime);
            DebugManager.Log<MovementController>($"Movement Direction: {_movementContext.movementDirection}, Jump: {_movementContext.InputDriver.IsJumpingPress}, Dash: {_movementContext.InputDriver.IsDashPress}");
        }
        private void HandleRotate()
        {
            Vector3 positionToLookAt;
            positionToLookAt.x = _movementContext.InputDriver.GetMovementDirection().x;
            positionToLookAt.y = 0.0f;
            positionToLookAt.z = _movementContext.InputDriver.GetMovementDirection().y;

            var currentRotation = transform.rotation;
            if (_movementContext.InputDriver.GetMovementDirection() == Vector2.zero) return;
            var targetRotation = Quaternion.LookRotation(positionToLookAt);
            transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, _movementContext.rotationPerFrame * Time.deltaTime);
        }
        
    }
}
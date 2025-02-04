using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using PEGA.ObjectSystems.MovementSystems.Drivers;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class MovementController : MonoBehaviour
    {
        private MovementContext _context;
        private CharacterController _characterController;
        private StateFactory _states;
        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _context = GetComponent<MovementContext>();
            SwitchToPlayerControl();
            _states = new StateFactory(_context); //Cria a fabric usando este contexto
            _context.CurrentState = _states.Fall(); //cria um estado corrente para iniciar o jogo em grounded (um dos roots)
            _context.CurrentState.EnterState(); //Inicia o Grounded para iniciar o jogo em um estado.
        }
        private void Update()
        {
            if (_context.MovementDriver == null) return;
            _context.MovementDriver.UpdateDriver(); // Atualiza estados de input.
            
            _context.CurrentState.CheckSwitchState();
            _context.movementDirection = _context.MovementDriver.GetMovementDirection();
            
            _context.CurrentState.UpdateStates();
            
            HandleRotate();
            _characterController.Move(_context.appliedMovement * Time.deltaTime);
            DebugManager.Log<MovementController>($"Movement Direction: {_context.movementDirection}, Jump: {_context.MovementDriver.IsJumpingPress}, Dash: {_context.MovementDriver.IsDashPress}");
        }
        private void HandleRotate()
        {
            Vector3 positionToLookAt;
            positionToLookAt.x = _context.MovementDriver.GetMovementDirection().x;
            positionToLookAt.y = 0.0f;
            positionToLookAt.z = _context.MovementDriver.GetMovementDirection().y;

            var currentRotation = transform.rotation;
            if (_context.MovementDriver.GetMovementDirection() == Vector2.zero) return;
            var targetRotation = Quaternion.LookRotation(positionToLookAt);
            transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, _context.rotationPerFrame * Time.deltaTime);
        }
        private void SetInputSource(IMovementDriver movementDriver)
        {
            if (_context.MovementDriver == movementDriver) return;
            
            _context.MovementDriver?.ExitDriver();
            _context.MovementDriver = movementDriver;
            _context.MovementDriver.InitializeDriver();
        }

        private void SwitchToPlayerControl()
        {
            SetInputSource(new PlayerMovementDriver(GetComponent<PlayerInput>()));
        }

        public void SwitchToAIControl()
        {
            SetInputSource(new NullMovementDriver(transform));
        }

        private void ResetHistoryDriver()
        {
            _context.MovementDriver?.Reset();
        }
    }
}
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public class DriverController : MonoBehaviour
    {
        private IMovementDriver _playerDriver;
        private IMovementDriver _aiDriver;
        private StateContext _context;

        protected virtual void Update()
        {
            _context.ActualDriver?.UpdateDriver(); // Atualiza estados de input.
        }
        public void Initialize(IMovementDriver playerDriver, IMovementDriver aiDriver, StateContext context)
        {
            _playerDriver = playerDriver;
            _aiDriver = aiDriver;
            _context = context;

            SwitchToPlayerControl(); // ✅ Definimos o input inicial aqui
        }
        private void SetInputSource(IMovementDriver newDriver)
        {
            if (_context.ActualDriver == newDriver) return;

            _context.ActualDriver?.ExitDriver();
            _context.ActualDriver = newDriver;
            _context.ActualDriver.InitializeDriver();
        }
        
        public void SwitchToPlayerControl()
        {
            SetInputSource(_playerDriver);
        }

        public void SwitchToAIControl()
        {
            SetInputSource(_aiDriver);
        }

        private void ResetHistoryDriver()
        {
            _context.ActualDriver?.Reset();
        }
   
    }
}
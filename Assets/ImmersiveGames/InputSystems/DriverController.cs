using ImmersiveGames.HierarchicalStateMachine;
using PEGA.ObjectSystems.MovementSystems.Interfaces;

namespace ImmersiveGames.InputSystems
{
    public class DriverController
    {
        private readonly IMovementDriver _playerDriver;
        private readonly IMovementDriver _aiDriver;
        private readonly StateContext _context;

        public DriverController(IMovementDriver playerDriver, IMovementDriver aiDriver, StateContext context)
        {
            _playerDriver = playerDriver;
            _aiDriver = aiDriver;
            _context = context;

            SwitchToPlayerControl(); // ✅ Definimos o input inicial aqui
        }

        public void Update()
        {
            _context.ActualDriver?.UpdateDriver(); // Atualiza estados de input.
        }
        private void SetInputSource(IMovementDriver newDriver)
        {
            if (_context.ActualDriver == newDriver) return;

            _context.ActualDriver?.ExitDriver();
            _context.ActualDriver = newDriver;
            _context.ActualDriver.InitializeDriver();
        }

        private void SwitchToPlayerControl()
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
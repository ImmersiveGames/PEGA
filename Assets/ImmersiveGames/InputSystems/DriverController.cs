using ImmersiveGames.HierarchicalStateMachine;
using PEGA.ObjectSystems.MovementSystems.Interfaces;

namespace ImmersiveGames.InputSystems
{
    public class DriverController
    {
        private readonly IInputDriver _playerDriver;
        private readonly IInputDriver _aiDriver;

        private IInputDriver _actualDriver;
        

        public DriverController(IInputDriver playerDriver, IInputDriver aiDriver)
        {
            _playerDriver = playerDriver;
            _aiDriver = aiDriver;
            //_context = context;

            SwitchToPlayerControl(); // ✅ Definimos o input inicial aqui
        }
        public IInputDriver GetActualDriver() => _actualDriver;

        public void Update()
        {
            _actualDriver?.UpdateDriver(); // Atualiza estados de input.
        }

        private void SwitchDriveControl(IInputDriver newDriver)
        {
            if (_actualDriver == newDriver) return;

            _actualDriver?.ExitDriver();
            _actualDriver = newDriver;
            _actualDriver.InitializeDriver();
        }
        
        public void SwitchToPlayerControl()
        {
            SwitchDriveControl(_playerDriver);
        }

        public void SwitchToAIControl()
        {
            SwitchDriveControl(_aiDriver);
        }

        private void ResetHistoryDriver()
        {
            _actualDriver?.Reset();
        }
   
    }
}
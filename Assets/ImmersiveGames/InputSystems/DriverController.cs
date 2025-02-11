using PEGA.ObjectSystems.DriverSystems.Interfaces;

namespace ImmersiveGames.InputSystems
{
    public class DriverController
    {
        private readonly IInputDriver _playerDriver;
        private readonly IInputDriver _aiDriver;

        private IInputDriver _inputDriver;
        public DriverController(IInputDriver actualDriver)
        {
            SwitchDriveControl(actualDriver);
        }
        public IInputDriver GetActualDriver() => _inputDriver;

        private void SwitchDriveControl(IInputDriver newDriver)
        {
            if (_inputDriver == newDriver) return;

            _inputDriver?.ExitDriver();
            _inputDriver = newDriver;
            _inputDriver.InitializeDriver();
        }
        

        private void ResetHistoryDriver()
        {
            _inputDriver?.Reset();
        }
   
    }
}
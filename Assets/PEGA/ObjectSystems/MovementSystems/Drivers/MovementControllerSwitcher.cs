using System;
using PEGA.ObjectSystems.MovementSystems.Interfaces;

namespace PEGA.ObjectSystems.MovementSystems.Drivers
{
    public class MovementControllerSwitcher
    {
        private IMovementDriver _currentDriver;
        public event Action<IMovementDriver> OnDriverChanged;

        public void SetDriver(IMovementDriver driver)
        {
            if (_currentDriver == driver) return; // Evita trocas desnecessárias

            _currentDriver = driver;
            OnDriverChanged?.Invoke(driver);
        }

        public IMovementDriver GetCurrentDriver() => _currentDriver;
    }
}
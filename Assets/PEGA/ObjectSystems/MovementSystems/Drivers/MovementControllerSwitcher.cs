using System;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

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
        public Type GetCurrentDriverType() => _currentDriver?.GetType();
        public Vector2 GetMovementInput() => _currentDriver?.GetMovementInput() ?? Vector2.zero;
        public bool IsJumping() => _currentDriver?.IsJumping() ?? false;
        public bool IsDashing() => _currentDriver?.IsDashing() ?? false;
    }
}
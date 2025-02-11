using PEGA.ObjectSystems.DriverSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.DriverSystems.Drivers
{
    public class NullInputDriver : IInputDriver
    {
        private readonly Transform _target;
        public NullInputDriver(Transform target)
        {
            _target = target;
        }

        public Vector2 GetMovementDirection()
        {
            return _target.position;
        }

        public bool CanJumpAgain { get; set; } = false;
        public bool CanDashAgain { get; set; } = false;
        public bool IsJumpingPress { get; set; } = false;
        public bool IsDashPress { get; set; } = false;

        public void InitializeDriver()
        {
            throw new System.NotImplementedException();
        }

        public void ExitDriver()
        {
            throw new System.NotImplementedException();
        }

        public void UpdateDriver()
        {
            throw new System.NotImplementedException();
        }

        public void Reset()
        {
            throw new System.NotImplementedException();
        }
    }

}
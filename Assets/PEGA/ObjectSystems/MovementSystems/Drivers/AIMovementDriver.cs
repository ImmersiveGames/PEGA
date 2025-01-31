using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Drivers
{
    public class AIMovementDriver : IMovementDriver
    {
        private readonly Vector2 _direction;

        public AIMovementDriver(Vector2 direction) => _direction = direction;

        public Vector2 GetMovementInput() => _direction;
        public bool IsJumping() => false;
        public bool IsDashing() => false;
    }
}
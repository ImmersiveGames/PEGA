using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Drivers
{
    public class AIMovementDriver : IMovementDriver
    {
        private readonly Vector2 _direction;

        public AIMovementDriver(Vector2 direction) => _direction = direction;

        public Vector2 GetMovementPressing() => _direction;
        public bool IsJumpPressing() => false;
        public bool IsDashPressing() => false;
    }
}
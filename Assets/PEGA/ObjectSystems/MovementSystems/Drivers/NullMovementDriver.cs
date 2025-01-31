using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Drivers
{
    public class NullMovementDriver : IMovementDriver
    {
        public Vector2 GetMovementInput() => Vector2.zero;
        public bool IsJumping() => false;
        public bool IsDashing() => false;
    }

}
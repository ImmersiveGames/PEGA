using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Drivers
{
    public class NullMovementDriver : IMovementDriver
    {
        public Vector2 GetMovementPressing() => Vector2.zero;
        public bool IsJumpPressing() => false;
        public bool IsDashPressing() => false;
    }

}
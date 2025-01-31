using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Interfaces
{
    public interface IMovementDriver
    {
        Vector2 GetMovementInput();
        bool IsJumping();
        bool IsDashing();
    }

}
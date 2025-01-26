using UnityEngine;

namespace PEGA.ObjectSystems.Interfaces
{
    public interface IMovementController
    {
        void InitializeInput();
        Vector3 MoveVector { get;}
        Vector2 InputVector { get;}
        bool IsJumpPressed { get;}
        void DisableInput();
    }
}
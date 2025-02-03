using UnityEngine;

namespace PEGA.ObjectSystems.Interfaces
{
    public interface IMovementControllerXXX
    {
        void InitializeInput();
        Vector2 InputVector { get;}
        bool IsJumpPressed { get;}
        bool IsDashPressed { get;}
        void DisableInput();
    }
}
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Interfaces
{
    public interface IMovementDriver
    {
        Vector2 GetMovementPressing();
        bool IsJumpPressing();
        bool IsDashPressing();
    }

    public enum ActionsNames
    {
        Jump,
        Dash,
        AxisMove
    }

}
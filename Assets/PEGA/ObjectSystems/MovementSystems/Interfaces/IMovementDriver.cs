using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Interfaces
{
    public interface IMovementDriver
    {
        Vector2 GetMovementDirection();
        bool IsJumpingPress { get; }
        bool IsDashPress { get; }
        
        void InitializeDriver();
        void ExitDriver();
        void UpdateDriver(); // Atualiza os estados do input a cada frame.
        void Reset();
    }
}
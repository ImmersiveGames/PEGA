using PEGA.ObjectSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Strategies
{
    public class MockMovementController : IMovementController
    {
        public Vector2 InputVector => Vector2.zero; // Mock sem movimento
        public bool IsJumpPressed => false; // Mock sem pulo
        public bool IsDashPressed => false; // Mock sem dash

        public void InitializeInput()
        {
            Debug.Log("[MockMovementController] Inicializado");
        }

        public void DisableInput()
        {
            Debug.Log("[MockMovementController] Desativado");
        }
    }
}
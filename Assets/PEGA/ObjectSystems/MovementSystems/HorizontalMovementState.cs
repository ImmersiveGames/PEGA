using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class HorizontalMovementState
    {
        public HorizontalMovementType CurrentState { get; private set; }

        private readonly float _walkThreshold; // Velocidade para ser considerado "andando"

        public HorizontalMovementState(float walkThreshold)
        {
            _walkThreshold = walkThreshold;
        }

        public void UpdateState(Vector3 movement)
        {
            // Calcula a magnitude do movimento horizontal
            var horizontalSpeed = new Vector3(movement.x, 0, movement.z).magnitude;
            Debug.Log($"SPEED MOve: {horizontalSpeed}");

            HorizontalMovementType newState;

            if (horizontalSpeed <= 0.01f)
            {
                newState = HorizontalMovementType.Idle; // Parado
            }
            else if (horizontalSpeed < _walkThreshold)
            {
                newState = HorizontalMovementType.Walking; // Andando
            }
            else
            {
                newState = HorizontalMovementType.Running; // Correndo
            }

            // Se o estado mudou, notifica
            if (newState == CurrentState) return;
            CurrentState = newState;
        }
    }

    // Tipos de estados horizontais
    public enum HorizontalMovementType
    {
        Idle,      // Parado
        Walking,   // Andando
        Running    // Correndo
    }
}
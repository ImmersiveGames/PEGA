using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class VerticalMovementState
    {
        public VerticalMovementStateType CurrentState { get; private set; }

        public void UpdateState(Vector3 actualMovement, bool isJumping, bool isGrounded)
        {
            VerticalMovementStateType newState;

            if (isGrounded && actualMovement.y <= 0.0f)
            {
                newState = VerticalMovementStateType.Grounded;
            }
            else if (isJumping)
            {
                newState = actualMovement.y > 0.0f
                    ? VerticalMovementStateType.Jumping
                    : VerticalMovementStateType.FallingFromJump;
            }
            else
            {
                newState = VerticalMovementStateType.FallingFree;
            }

            // Notifica a mudança de estado somente se houver alteração
            if (newState == CurrentState) return;
            CurrentState = newState;
        }
    }
    public enum VerticalMovementStateType
    {
        Grounded,
        Jumping,
        FallingFromJump,
        FallingFree
    }
}
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Handlers
{
    public class GravityHandler
    {
        private readonly MovementSettings _movementSettings;

        public GravityHandler(MovementSettings movementSettings)
        {
            _movementSettings = movementSettings;
        }

        public void CalculateGravity(
            ref Vector3 actualMovement,
            ref Vector3 appliedMovement,
            float actualGravity,
            VerticalMovementState state) // Retorna se está caindo
        {
            switch (state.CurrentState)
            {
                case VerticalMovementStateType.Grounded:
                    actualMovement.y = _movementSettings.gravityGround;
                    break;
                case VerticalMovementStateType.FallingFromJump or VerticalMovementStateType.FallingFree:
                {
                    var previousYVelocity = actualMovement.y;
                    actualMovement.y += actualGravity * _movementSettings.fallMultiplier * Time.deltaTime;
                    appliedMovement.y = Mathf.Max(previousYVelocity + actualMovement.y, _movementSettings.maxFallVelocity);
                    break;
                }
                case VerticalMovementStateType.Jumping:
                {
                    var previousYVelocity = actualMovement.y;
                    actualMovement.y += actualGravity * Time.deltaTime;
                    appliedMovement.y = previousYVelocity + actualMovement.y;
                    break;
                }
            }
        }
    }
}
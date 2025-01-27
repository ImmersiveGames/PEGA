using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems
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
            MovementState state) // Retorna se está caindo
        {
            switch (state.CurrentState)
            {
                case MovementStateType.Grounded:
                    actualMovement.y = _movementSettings.gravityGround;
                    break;
                case MovementStateType.FallingFromJump or MovementStateType.FallingFree:
                {
                    var previousYVelocity = actualMovement.y;
                    actualMovement.y += actualGravity * _movementSettings.fallMultiplier * Time.deltaTime;
                    appliedMovement.y = Mathf.Max(previousYVelocity + actualMovement.y, _movementSettings.maxFallVelocity);
                    break;
                }
                case MovementStateType.Jumping:
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
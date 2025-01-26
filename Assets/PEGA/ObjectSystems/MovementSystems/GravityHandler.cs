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
            bool isGrounded,
            bool isFalling) // Retorna se está caindo
        {

            if (isGrounded)
            {
                actualMovement.y = _movementSettings.gravityGround;
            }
            else if (isFalling)
            {
                var previousYVelocity = actualMovement.y;
                actualMovement.y += actualGravity * _movementSettings.fallMultiplier * Time.deltaTime;
                appliedMovement.y = Mathf.Max(previousYVelocity + actualMovement.y, _movementSettings.maxFallVelocity);
            }
            else
            {
                var previousYVelocity = actualMovement.y;
                actualMovement.y += actualGravity * Time.deltaTime;
                appliedMovement.y = previousYVelocity + actualMovement.y;
            }
        }
    }
}
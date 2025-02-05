using ImmersiveGames.HierarchicalStateMachine;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class MovementContext : MonoBehaviour
    {
        public MovementSettings movementSettings;
        
        public Vector3 movement;
        public Vector3 appliedMovement;
        public Vector2 movementDirection;
        public float rotationPerFrame = 15f;
        
        public float gravity;
        public float initialJumpVelocity;

        [Range(0.01f,1f)]
        public float dashDuration;
        [Range(1f,20f)]
        public float dashMultiply;
        
        public bool isWalking;
        public bool isGrounded;
        public bool isJumping;
        public bool isFalling;
        public bool isDashing;

        public bool canJumpAgain;
        public bool canDashAgain;

        internal IMovementDriver MovementDriver;
        internal CharacterController CharacterController;
        internal BaseState CurrentState;

        private void Awake()
        {
            CharacterController = GetComponent<CharacterController>();
            gravity = movementSettings.gravity;
            canJumpAgain = true;
            canDashAgain = true;
        }
        
        public void ApplyMovement(float speedMultiplier = 1f)
        {
            movement.x = movementDirection.x * movementSettings.baseSpeed;
            movement.z = movementDirection.y * movementSettings.baseSpeed;

            appliedMovement.x = movement.x * speedMultiplier;
            appliedMovement.z = movement.z * speedMultiplier;
        }
        public void CalculateJumpVariables()
        {
            var timeToApex = movementSettings.maxJumpTime / 2;
            gravity = (-2 * movementSettings.maxJumpHeight) / Mathf.Pow(timeToApex, 2);
            initialJumpVelocity = (2 * movementSettings.maxJumpHeight) / timeToApex;
        }

        private float HandleGravity(float speedMultiplier = 1f)
        {
            var previousYVelocity = movement.y;
            movement.y += gravity * speedMultiplier * Time.deltaTime;
            return previousYVelocity;
        }

        public void HandleGravityFall()
        {
            var previousYVelocity = HandleGravity(movementSettings.fallMultiplier);
            appliedMovement.y = Mathf.Max((previousYVelocity + movement.y) *.5f, movementSettings.maxFallVelocity);
        }

        public void HandleGravityJump()
        {
            var previousYVelocity = HandleGravity();
            appliedMovement.y = previousYVelocity + movement.y;
        }

    }
}
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems
{
    [CreateAssetMenu(fileName = "MovementData", menuName = "ImmersiveGames/PEGA/MovementSettings", order = 102)]
    public class MovementSettings : ScriptableObject
    {
        [Header("Movement Settings")]
        public float baseSpeed = 10f;
        public float gravityGround = -0.5f;
        public float gravity = -9.8f;
        [Header("Jump Settings")]
        public float maxJumpHeight = 1.0f;
        public float maxJumpTime = 0.5f;
        public float maxSpeedBoost = 4;
        public float impulseMultiply = 0.2f;
        [Header("Fall Settings")]
        [Range(1f,10f)]
        public float fallMultiplier = 2f;
        public float maxFallVelocity = -30f;
        [Header("Dash Settings")]
        [Range(0.01f,1f)]
        public float dashDuration = 0.3f;
        [Range(1f,20f)]
        public float dashMultiply = 5.0f;
        [Range(0f, 1f)]
        public float idleDashMultiply = 0.6f;
        [Range(0f, 1f)] 
        public float dashCooldownTime = 0.2f;
    }
}
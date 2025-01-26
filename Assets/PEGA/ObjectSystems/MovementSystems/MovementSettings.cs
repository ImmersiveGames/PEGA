using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems
{
    [CreateAssetMenu(fileName = "MovementData", menuName = "ImmersiveGames/PEGA/MovementSettings", order = 102)]
    public class MovementSettings : ScriptableObject
    {
        [Header("Movement Settings")]
        public float baseSpeed = 10f;
        public float gravity = -9.8f;
        public float gravityGround = -0.5f;
        [Header("Jump Settings")]
        public float maxJumpHeight = 1.0f;
        public float maxJumpTime = 0.5f;
        public float fallMultiplier = 2f;
        public float maxFallVelocity = -30f;
    }
}
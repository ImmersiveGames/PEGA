using UnityEngine;

namespace ImmersiveGames.Utils
{
    public static class CalculateMovementHelper
    {
        public static void CalculateJumpVariables(float maxJumpHeight, float maxJumpTime, out float gravity, out float initialJumpVelocity)
        {
            var timeToApex = maxJumpTime / 2;
            gravity = (-2 * maxJumpHeight) / Mathf.Pow(timeToApex, 2);
            initialJumpVelocity = (2 * maxJumpHeight) / timeToApex;
        }
    }
}
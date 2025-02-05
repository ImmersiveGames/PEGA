using System;
using ImmersiveGames.HierarchicalStateMachine;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class MovementContext : MonoBehaviour
    {
        public event Action<StatesNames> OnStateEnter;
        public event Action<StatesNames> OnStateExit;
        
        public MovementSettings movementSettings;
        
        public Vector3 movement;
        public Vector3 appliedMovement;
        public Vector2 movementDirection;
        public float rotationPerFrame = 15f;
        public float fallMaxHeight = -50f;
        
        public float gravity;
        public float initialJumpVelocity;
        
        public bool isWalking;
        public bool isGrounded;
        public bool isJumping;
        public bool isFalling;
        public bool isDashing;

        internal bool CanJumpAgain;
        internal bool CanDashAgain;
        internal Vector3 StoredMomentum;
        internal float TimeInDash;

        internal IMovementDriver MovementDriver;
        internal CharacterController CharacterController;
        internal BaseState CurrentState;

        private void Awake()
        {
            CharacterController = GetComponent<CharacterController>();
            gravity = movementSettings.gravity;
            CanJumpAgain = true;
            CanDashAgain = true;
        }

        #region Movment Calculation

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
    
            // 🔹 Mantém o momentum horizontal
            StoredMomentum = new Vector3(appliedMovement.x, 0, appliedMovement.z);
            var horizontalSpeed = StoredMomentum.magnitude;

            // 🔹 Define um multiplicador de influência do Dash na altura
            var heightMultiply = Mathf.Lerp(movementSettings.minDashJumpInfluence, movementSettings.maxDashJumpInfluence, TimeInDash / movementSettings.dashDuration);

            // 🔹 Ajusta a altura máxima do pulo dinamicamente
            var newHeightMax = movementSettings.maxJumpHeight + (horizontalSpeed * heightMultiply);
    
            // 🔹 Limita a altura máxima para evitar saltos absurdos
            var maxBoost = movementSettings.maxJumpHeight * movementSettings.momentumMultiply;
            newHeightMax = Mathf.Min(newHeightMax, movementSettings.maxJumpHeight + maxBoost);
    
            // 🔹 Calcula a nova gravidade e a velocidade inicial do pulo com base na altura ajustada
            gravity = (-2 * newHeightMax) / Mathf.Pow(timeToApex, 2);
            initialJumpVelocity = (2 * newHeightMax) / timeToApex;
        }
        public void ApplyGravity(bool falling)
        {
            var multiplier = falling ? movementSettings.fallMultiplier : 1f;
            var previousYVelocity = movement.y;
            movement.y += gravity * multiplier * Time.deltaTime;
            appliedMovement.y = falling ? Mathf.Max((previousYVelocity + movement.y) * 0.5f, movementSettings.maxFallVelocity)
                : previousYVelocity + movement.y;
        }

        #endregion

        #region Call Events

        public void GlobalNotifyStateEnter(StatesNames newState)
        {
            Debug.Log($"Chamou o Evento paa o Estado : {newState}");
            OnStateEnter?.Invoke(newState);
        }
        public void GlobalNotifyStateExit(StatesNames newState)
        {
            OnStateExit?.Invoke(newState);
        }

        #endregion

    }
}
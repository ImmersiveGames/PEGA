using System;
using ImmersiveGames.DebugSystems;
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
        internal float StoredMomentum;
        internal float TimeInDash;

        internal IMovementDriver MovementDriver;
        internal CharacterController CharacterController;
        internal BaseState CurrentState;
        public float maxJumpHeight;
        public Vector3 jumpStartPosition;
        public float jumpStartTime;

        public float realBaseSpeed;
        
        
        public float dashCooldown;

        private void Awake()
        {
            CharacterController = GetComponent<CharacterController>();
            gravity = movementSettings.gravity;
            //Calcular as variáveis base
            realBaseSpeed = movementSettings.baseSpeed;
            //
            CanJumpAgain = true;
            CanDashAgain = true;
        }

        #region Movment Calculation

        public void ApplyMovement(float speedMultiplier = 1f)
        {
            movement.x = movementDirection.x * realBaseSpeed;
            movement.z = movementDirection.y * realBaseSpeed;

            appliedMovement.x = movement.x * speedMultiplier;
            appliedMovement.z = movement.z * speedMultiplier;
        }
        public void CalculateJumpVariables()
        {
            var timeToApex = movementSettings.maxJumpTime / 2;
    
            // 🔹 Altura base do pulo (sem modificações)
            var maxRealJumpHeight = movementSettings.maxJumpHeight;
            
            var currentSpeed = CharacterController.velocity.magnitude;

            // 🔹 Calcula a diferença entre a velocidade atual e a base
            var speedDifference = Mathf.Max(currentSpeed - realBaseSpeed, 0f);
    
            // 🔹 Aplica um ajuste não-linear (sqrt) e limita o impulso máximo
            var speedBoost = Mathf.Sqrt(speedDifference) * movementSettings.impulseMultiply; // Fator de escala ajustável

            speedBoost = Mathf.Min(speedBoost, movementSettings.maxSpeedBoost);

            // 🔹 Aplica o impulso à altura do pulo
            maxRealJumpHeight += speedBoost;

            // 🔹 Cálculo da gravidade e velocidade inicial
            gravity = (-2 * maxRealJumpHeight) / Mathf.Pow(timeToApex, 2);
            initialJumpVelocity = Mathf.Sqrt(2 * -gravity * maxRealJumpHeight);
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
            DebugManager.Log<MovementContext>($"Chamou o Evento paa o Estado : {newState}");
            OnStateEnter?.Invoke(newState);
        }
        public void GlobalNotifyStateExit(StatesNames newState)
        {
            OnStateExit?.Invoke(newState);
        }

        #endregion

    }
}
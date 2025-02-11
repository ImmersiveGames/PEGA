using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using ImmersiveGames.Utils;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems
{
    [DefaultExecutionOrder(-10)]
    public class MovementContext : StateContext
    {
        public MovementSettings movementSettings;
        [Header("Movement Settings")]
        public Vector3 movement;
        public Vector3 appliedMovement;
        public float rotationPerFrame = 15f;
        
        public float maxJumpHeight;
        
        public float fallMaxHeight = -50f;
        [Header("Debug Only - Hide inspector")]
        [SerializeField]internal bool isWalking;
        [SerializeField]internal bool isGrounded;
        [SerializeField]internal bool isJumping;
        [SerializeField]internal bool isFalling;
        [SerializeField]internal bool isDashing;
        
        [Header("Debug Only")]
        [SerializeField]internal float realBaseSpeed;
        [SerializeField]internal float initialJumpVelocity;
        [SerializeField]internal Vector3 jumpStartPosition;
        [SerializeField]internal float jumpStartTime;
        internal float TimeInDash;//Debug only

        private float _realGravity;
        private CountdownManager _timers;
        
        internal bool CanJumpAgain;
        internal bool CanDashAgain;
        
        internal float StoredMomentum;
        internal bool DashingCooldown;
        
        internal CharacterController CharacterController;
        internal IInputDriver InputDriver;

        private void Awake()
        {
            CharacterController = GetComponent<CharacterController>();
            DashingCooldown = false;
            _timers = new CountdownManager();
            _timers.RegisterAutoCountdown("dash_cooldown", movementSettings.dashCooldownTime, () => DashingCooldown, () =>
            {
                DebugManager.Log<MovementContext>("Dash pronta para uso!");
                DashingCooldown = false;
            });
            
            _realGravity = movementSettings.gravity;
            //Calcular as variáveis base
            realBaseSpeed = movementSettings.baseSpeed;
            //
            CanJumpAgain = true;
            CanDashAgain = true;
        }

        private void Update()
        {
            // 🔹 Reduz o cooldown do Dash ao longo do tempo
            _timers.Update(Time.deltaTime);
        }

        #region Movment Calculation

        public void ApplyMovement(Vector2 direction, float speedMultiplier = 1f)
        {
            if (InputDriver == null) return;
            movement.x =  direction.x * realBaseSpeed;
            movement.z = direction.y * realBaseSpeed;

            appliedMovement.x = movement.x * speedMultiplier;
            appliedMovement.z = movement.z * speedMultiplier;
        }
        public void CalculateJumpVariables()
        {
            var timeToApex = movementSettings.maxJumpTime / 2;
    
            // 🔹 Altura base do pulo (sem modificações)
            var maxRealJumpHeight = movementSettings.maxJumpHeight;
            
            Debug.Log($"SPEED: {CharacterController.velocity.magnitude}");
            var currentSpeed = StoredMomentum;

            // 🔹 Calcula a diferença entre a velocidade atual e a base
            var speedDifference = Mathf.Max(currentSpeed - realBaseSpeed, 0f);
    
            // 🔹 Aplica um ajuste não-linear (sqrt) e limita o impulso máximo
            var speedBoost = Mathf.Sqrt(speedDifference) * movementSettings.impulseMultiply; // Fator de escala ajustável

            speedBoost = Mathf.Min(speedBoost, movementSettings.maxSpeedBoost);

            // 🔹 Aplica o impulso à altura do pulo
            maxRealJumpHeight += speedBoost;

            // 🔹 Cálculo da gravidade e velocidade inicial
            _realGravity = (-2 * maxRealJumpHeight) / Mathf.Pow(timeToApex, 2);
            initialJumpVelocity = Mathf.Sqrt(2 * -_realGravity * maxRealJumpHeight);
        }

        public void ApplyGravity(bool falling)
        {
            var multiplier = falling ? movementSettings.fallMultiplier : 1f;
            var previousYVelocity = movement.y;
            movement.y += _realGravity * multiplier * Time.deltaTime;
            appliedMovement.y = falling ? Mathf.Max((previousYVelocity + movement.y) * 0.5f, movementSettings.maxFallVelocity)
                : previousYVelocity + movement.y;
        }

        #endregion

    }
}
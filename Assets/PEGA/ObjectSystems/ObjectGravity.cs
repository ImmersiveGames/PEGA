using ImmersiveGames.DebugSystems;
using UnityEngine;
using System;

namespace PEGA.ObjectSystems
{
    public class ObjectGravity : MonoBehaviour
    {
        [Header("Configurações de Gravidade")]
        [SerializeField] private float gravityBase = -9.8f; // Gravidade padrão
        [SerializeField] private float groundedGravity = -2f; // Gravidade mínima no chão
        [SerializeField] private float fallMultiplayer = 2.0f;
        [SerializeField] private float maxFallSpeed = -50f; // Velocidade máxima de queda

        private bool _pressJump;
        private bool _hasJumped; // Indica se o objeto já pulou
        private bool _isFalling;
        private bool _wasFalling; // Rastreamento do estado anterior de queda
        private float CurrentVerticalVelocity { get; set; }
        private CharacterController _characterController;

        public event Action OnFallingAfterJump; // Evento para queda após pulo
        public event Action OnFallingWithoutJump; // Evento para queda sem pulo

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        private void FixedUpdate()
        {
            UpdateGravity();
            CheckFallingState(); // Atualiza os estados de queda
        }

        public void SetGravityJump(float maxJumpHeight, float timeToApex)
        {
            gravityBase = (-2 * maxJumpHeight) / Mathf.Pow(timeToApex, 2);
        }

        private void UpdateGravity()
        {
            // Lógica principal de gravidade
            if (_characterController.isGrounded)
            {
                // No chão: reseta variáveis relacionadas ao salto e queda
                CurrentVerticalVelocity = groundedGravity;
                _hasJumped = false; // Se estamos no chão, significa que não estamos mais pulando
            }
            else if (CurrentVerticalVelocity > 0.0f && _pressJump)
            {
                // Durante o pulo
                _hasJumped = true;
                CurrentVerticalVelocity = VelocityVerlet(CurrentVerticalVelocity, gravityBase);
            }
            else
            {
                // Durante a queda
                CurrentVerticalVelocity = VelocityVerlet(CurrentVerticalVelocity, gravityBase * fallMultiplayer);
                CurrentVerticalVelocity = Mathf.Max(CurrentVerticalVelocity, maxFallSpeed);
                DebugManager.Log<ObjectGravity>($"Caindo. Velocidade vertical: {CurrentVerticalVelocity}.");
            }
        }

        private static float VelocityVerlet(float currentVelocity, float gravity)
        {
            var newYVelocity = currentVelocity + (gravity * Time.deltaTime);
            return (currentVelocity + newYVelocity) * .5f;
        }

        private void CheckFallingState()
        {
            // Verifica se o objeto está caindo
            _isFalling = !_characterController.isGrounded && CurrentVerticalVelocity < 0;

            // Detecta a transição para o estado de queda
            if (_isFalling && !_wasFalling)
            {
                if (_hasJumped)
                {
                    // Dispara o evento de queda após pulo
                    OnFallingAfterJump?.Invoke();
                    DebugManager.Log<ObjectGravity>("O objeto está caindo após um pulo!");
                }
                else
                {
                    // Dispara o evento de queda sem pulo
                    OnFallingWithoutJump?.Invoke();
                    DebugManager.Log<ObjectGravity>("O objeto está caindo sem ter pulado!");
                }
            }

            // Atualiza o estado anterior
            _wasFalling = _isFalling;
        }

        public float GetGravityVelocity()
        {
            // Retorna a velocidade vertical atual
            return CurrentVerticalVelocity;
        }

        public void SetCurrentVerticalVelocity(float newVerticalVelocity)
        {
            CurrentVerticalVelocity = newVerticalVelocity;
        }

        public void InJump(bool inJump)
        {
            _pressJump = inJump;
        }
    }
}

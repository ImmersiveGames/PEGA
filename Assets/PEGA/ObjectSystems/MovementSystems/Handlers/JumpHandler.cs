using ImmersiveGames.Utils;
using PEGA.ObjectSystems.Modifications;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Handlers
{
    public class JumpHandler
    {
        private readonly MovementSettings _movementSettings;
        private readonly ModifierController _modifierController;
        private readonly VerticalMovementState _verticalMovementState;

        private float _initialJumpVelocity;
        private float _gravity;

        public float Gravity => _gravity; // Expor valor calculado

        public JumpHandler(MovementSettings movementSettings, ModifierController modifierController, VerticalMovementState verticalMovementState)
        {
            _movementSettings = movementSettings;
            _modifierController = modifierController;
            _verticalMovementState = verticalMovementState;
            // Calcula os valores iniciais do pulo
            CalculateJumpVariables();
            Debug.Log($"Initial: {_initialJumpVelocity}, Gravity: {_gravity}");
        }

        public void HandleJump(ref Vector3 actualMovement, ref Vector3 appliedMovement, bool isJumpPressed, ref bool isJumping)
        {
            // Permite pular apenas se estiver no chão e não pulando
            if (_verticalMovementState.CurrentState == VerticalMovementStateType.Grounded && isJumpPressed && !isJumping)
            {
                isJumping = true;

                var jumpBoost = _modifierController.GetModifierValue(ModifierKeys.JumpBoost);
                actualMovement.y = _initialJumpVelocity + jumpBoost;
                appliedMovement.y = _initialJumpVelocity + jumpBoost;
            }
            else if (!isJumpPressed)
            {
                isJumping = false; // Permite um novo pulo após liberar o botão
            }
            Debug.Log($"Initial: {_initialJumpVelocity}, Gravity: {_gravity}, Mov: {appliedMovement.y}");
        }

        private void CalculateJumpVariables()
        {
            CalculateMovementHelper.CalculateJumpVariables(
                _movementSettings.maxJumpHeight,
                _movementSettings.maxJumpTime,
                out _gravity, // Armazena gravidade calculada
                out _initialJumpVelocity); // Armazena força inicial do pulo
        }
    }
}

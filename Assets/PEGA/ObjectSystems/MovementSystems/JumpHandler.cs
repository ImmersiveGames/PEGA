using ImmersiveGames.Utils;
using PEGA.ObjectSystems.AnimatorSystem;
using PEGA.ObjectSystems.Modifications;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class JumpHandler
    {
        private AnimationHandler _animationHandler;
        private readonly MovementSettings _movementSettings;
        private readonly ModifierController _modifierController;
        private readonly MovementState _movementState;

        private float _initialJumpVelocity;
        private float _gravity;

        public float Gravity => _gravity; // Expor valor calculado

        public JumpHandler(MovementSettings movementSettings, ModifierController modifierController, MovementState movementState, AnimationHandler animationHandler)
        {
            _movementSettings = movementSettings;
            _modifierController = modifierController;
            _movementState = movementState;
            _animationHandler = animationHandler;

            // Calcula os valores iniciais do pulo
            CalculateJumpVariables();
            
        }

        public void HandleJump(ref Vector3 actualMovement, ref Vector3 appliedMovement, bool isJumpPressed, ref bool isJumping)
        {
            if (_movementState.CurrentState == MovementStateType.Grounded)
            {
                if (actualMovement.y <= 0.0f)
                {
                    _movementState.CurrentState = MovementStateType.Grounded;
                }

                if (!isJumping && isJumpPressed)
                {
                    _animationHandler.SetJumpState(true);
                    isJumping = true;
                    _movementState.CurrentState = MovementStateType.Jumping;

                    var jumpBoost = _modifierController.GetModifierValue("JumpBoost");
                    actualMovement.y = _initialJumpVelocity + jumpBoost;
                    appliedMovement.y = _initialJumpVelocity + jumpBoost;
                }
                else if (!isJumpPressed)
                {
                    isJumping = false;
                }
            }
            else if (_movementState.CurrentState == MovementStateType.Jumping)
            {
                if (actualMovement.y <= 0.0f || !isJumpPressed)
                {
                    _animationHandler.SetJumpState(false);
                    _movementState.CurrentState = MovementStateType.FallingFromJump;
                }
            }
        }

        private void CalculateJumpVariables()
        {
            JumpHelper.CalculateJumpVariables(
                _movementSettings.maxJumpHeight,
                _movementSettings.maxJumpTime,
                out _gravity, // Armazena gravidade calculada
                out _initialJumpVelocity); // Armazena força inicial do pulo
        }
    }
}

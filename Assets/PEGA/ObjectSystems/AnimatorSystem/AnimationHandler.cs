using UnityEngine;

namespace PEGA.ObjectSystems.AnimatorSystem
{
    public class AnimationHandler
    {
        private readonly AnimatorHandler _animatorHandler;
        
        private const string MovementParameter = "Movement";
        private const string JumpParameter = "Jump";
        private const string DashParameter = "Dash";
        private const string IdleParameter = "Idle";

        public AnimationHandler(AnimatorHandler animatorHandler)
        {
            _animatorHandler = animatorHandler;
        }

        // Animação de Movimento (Andando/Parado/Correndo)
        public void HandleMovementAnimation(Vector2 inputVector)
        {
            var moveMagnitude = inputVector.magnitude;
            _animatorHandler.SetFloat(MovementParameter, moveMagnitude);
        }

        // Animação de Pulo
        public void SetJumping(bool isJumping)
        {
            _animatorHandler.SetBool(JumpParameter, isJumping);
        }
        // Animação de Pulo
        public void SetDashing(bool isDashing)
        {
            _animatorHandler.SetBool(DashParameter, isDashing);
        }

        // Animação de Idle
        public void SetIdle()
        {
            _animatorHandler.SetTrigger(IdleParameter);
        }
    }
}
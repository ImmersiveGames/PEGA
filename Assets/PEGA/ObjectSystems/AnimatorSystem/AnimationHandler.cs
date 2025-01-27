using UnityEngine;

namespace PEGA.ObjectSystems.AnimatorSystem
{
    public class AnimationHandler
    {
        private readonly ObjectAnimator _objectAnimator;
        
        private const string MovementParameter = "Movement";
        private const string JumpParameter = "Jump";
        private const string IdleParameter = "Idle";

        public AnimationHandler(ObjectAnimator objectAnimator)
        {
            _objectAnimator = objectAnimator;
        }

        // Animação de Movimento (Andando/Parado/Correndo)
        public void HandleMovementAnimation(Vector2 inputVector)
        {
            var moveMagnitude = inputVector.magnitude;
            _objectAnimator.SetFloat(MovementParameter, moveMagnitude);
        }

        // Animação de Pulo
        public void SetJumping(bool isJumping)
        {
            _objectAnimator.SetBool(JumpParameter, isJumping);
        }

        // Animação de Idle
        public void SetIdle()
        {
            _objectAnimator.SetTrigger(IdleParameter);
        }
    }
}
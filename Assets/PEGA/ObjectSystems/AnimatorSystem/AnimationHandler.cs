using UnityEngine;

namespace PEGA.ObjectSystems.AnimatorSystem
{
    public class AnimationHandler
    {
        private readonly ObjectAnimator _objectAnimator;
        private readonly string _movementParameterName;

        public AnimationHandler(ObjectAnimator objectAnimator, string movementParameterName)
        {
            _objectAnimator = objectAnimator;
            _movementParameterName = movementParameterName;
        }

        public void HandleMovementAnimation(Vector2 inputVector)
        {
            var moveMagnitude = inputVector.magnitude;
            _objectAnimator.WalkAnimation(_movementParameterName, moveMagnitude);
        }

        public void SetJumpState(bool isJumping)
        {
            _objectAnimator.JumpStartAnimation(isJumping);
        }

        public void SetIdle()
        {
            _objectAnimator.IdleAnimation();
        }
    }
}
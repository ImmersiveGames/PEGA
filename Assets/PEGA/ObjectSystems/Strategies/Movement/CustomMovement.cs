using PEGA.ObjectSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.Strategies.Movement
{
    public class CustomMovement : IMovementStrategy
    {
        // Com esse tpo de movimento o Navmesh não se aplica no objeto,
        // Ou seja gravidade, velocidade do navmesh são ignoradas e usado a do Scriptable

        public void Gravity(ObjectMovement context)
        {
            if (context.CharacterController == null) return;
            if (context.CharacterController.isGrounded) return;

            var gravityVector = new Vector3(0, Physics.gravity.y * context.ObjectData.gravityModifier * Time.deltaTime, 0);
            context.CharacterController.Move(gravityVector);
        }

        public void Move(ObjectMovement context)
        {
            if (context.CharacterController == null) return;
            var movement = context.CalculateMovement();
            context.CharacterController.Move(movement * (context.ObjectData.speed * Time.deltaTime));
        }

        public void Rotate(ObjectMovement context)
        {
            if (context.InputVector == Vector2.zero) return;

            var desiredDirection = new Vector3(context.InputVector.x, 0f, context.InputVector.y);
            var targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);

            context.transform.rotation = Quaternion.Slerp(context.transform.rotation, targetRotation, context.ObjectData.rotationSpeed * Time.fixedDeltaTime);
        }
    }
}
using PEGA.ObjectSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.Strategies.Movement
{
    public class CharacterControllerMovement : IMovementStrategy
    {
        public void Move(ObjectMovement context)
        {
            if (context.CharacterController == null) return;

            // Calcula a velocidade final com modificadores
            var speedModifier = context.ModifierController.GetModifierValue("Speed");
            var finalMoveSpeed = context.moveSpeedBase + speedModifier;

            // Calcula o movimento horizontal baseado na entrada do jogador
            var horizontalMovement = context.CalculateMovement() * (finalMoveSpeed * Time.deltaTime);

            // Obtém a gravidade atual do GravitySystem
            var gravityVelocity = context.ObjectGravity.GetGravityVelocity();

            // Soma o movimento horizontal e vertical (gravidade)
            var totalMovement = horizontalMovement;
            totalMovement.y = gravityVelocity * Time.deltaTime;

            // Move o CharacterController e atualiza o estado de colisão
            context.CharacterController.Move(totalMovement);
        }


        public void Rotate(ObjectMovement context)
        {
            var movementDirection = context.CalculateMovement();

            if (!(movementDirection.sqrMagnitude > 0.01f)) return;
            var targetRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
            context.transform.rotation = Quaternion.Slerp(
                context.transform.rotation,
                targetRotation,
                context.ObjectData.rotationSpeed * Time.fixedDeltaTime
            );
        }
    }
}
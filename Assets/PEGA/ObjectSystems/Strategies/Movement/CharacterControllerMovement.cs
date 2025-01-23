using PEGA.ObjectSystems.Interfaces;
using PEGA.ObjectSystems.Modifications;
using UnityEngine;

namespace PEGA.ObjectSystems.Strategies.Movement
{
    public class CharacterControllerMovement : IMovementStrategy
    {
        // Com esse tpo de movimento o Navmesh não se aplica no objeto,
        // Ou seja gravidade, velocidade do navmesh são ignoradas e usado a do Scriptable

        public void Gravity(ObjectMovement context)
        {
            if (!context.CharacterController.isGrounded)
            {
                // Obtemos o modificador de gravidade
                float gravityModifier = context.ModifierController.GetModifierValue("Gravity");

                // Calcula a gravidade final
                float finalGravity = context.gravityBase + gravityModifier;

                // Aplica a gravidade
                context.VerticalMovement.y -= finalGravity * Time.deltaTime;
            }
            else if (context.VerticalMovement.y < 0)
            {
                // Zera o movimento vertical ao tocar o chão
                context.VerticalMovement.y = 0;
            }
        }


        public void Move(ObjectMovement context)
        {
            if (context.CharacterController == null) return;

            // Calcula a velocidade final com modificadores
            float speedModifier = context.ModifierController.GetModifierValue("Speed");
            float finalMoveSpeed = context.moveSpeedBase + speedModifier;

            // Calcula o movimento com base na direção da câmera
            Vector3 horizontalMovement = context.CalculateMovement() * (finalMoveSpeed * Time.deltaTime);

            // Soma o movimento vertical (gravidade) com o horizontal
            Vector3 totalMovement = horizontalMovement + context.VerticalMovement;

            // Move o CharacterController
            CollisionFlags collisionFlags = context.CharacterController.Move(totalMovement);

            // Atualiza estado de "no chão"
            context.IsGrounded = context.CharacterController.isGrounded;

            // Se está no chão, zera o movimento vertical
            if (context.IsGrounded && context.VerticalMovement.y < 0)
            {
                context.VerticalMovement.y = 0;
            }
        }


        public void Rotate(ObjectMovement context)
        {
            // Calcula a direção de movimento apenas se houver input significativo
            var movementDirection = context.CalculateMovement();

            if (movementDirection.sqrMagnitude > 0.01f) // Pequenos inputs são ignorados
            {
                var targetRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
                context.transform.rotation = Quaternion.Slerp(
                    context.transform.rotation,
                    targetRotation,
                    context.ObjectData.rotationSpeed * Time.fixedDeltaTime
                );
            }
        }


        
    }
}
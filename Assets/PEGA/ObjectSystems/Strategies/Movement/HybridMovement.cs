using PEGA.ObjectSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.Strategies.Movement
{
    public class HybridMovement : IMovementStrategy
    {
        // Com esse tipo de movimento o Navmesh afeta a gravidade e a velocidade do objeto,
        // porém ele também é modificado pelo controller e pelos scriptable object
        public void Move(ObjectMovement context)
        {
            if (context.CharacterController == null || context.NavMeshAgent == null) return;
            if (context.Target == null) return;

            // Define o destino, mas não deixa o NavMeshAgent mover o personagem
            context.NavMeshAgent.SetDestination(context.Target.position);

            // Pega a próxima direção que o NavMeshAgent sugere
            var direction = context.NavMeshAgent.steeringTarget - context.transform.position;
            direction.y = 0;  // Ignora a componente Y para não interferir na gravidade

            // Atualiza o InputVector com base na direção desejada pelo NavMesh
            context.InputVector = new Vector2(direction.x, direction.z).normalized;

            // Calcula o movimento com base na posição da câmera e InputVector
            var movement = context.CalculateMovement() * (context.ObjectData.speed * Time.deltaTime);

            // Aplica o movimento usando o CharacterController
            context.CharacterController.Move(movement);
        }

        public void Rotate(ObjectMovement context)
        {
            if (context.InputVector == Vector2.zero) return;

            // Calcula a direção desejada com base no InputVector (que foi atualizado no Move)
            var desiredDirection = new Vector3(context.InputVector.x, 0f, context.InputVector.y);

            // Rotaciona o personagem suavemente na direção desejada
            Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
            context.transform.rotation = Quaternion.Slerp(context.transform.rotation, targetRotation, context.ObjectData.rotationSpeed * Time.fixedDeltaTime);
        }
    
        public void Gravity(ObjectMovement context)
        {
            if (context.CharacterController.isGrounded) return;

            var gravityVector = new Vector3(0, Physics.gravity.y * context.ObjectData.gravityModifier * Time.deltaTime, 0);
            context.CharacterController.Move(gravityVector);
        }
    }
}

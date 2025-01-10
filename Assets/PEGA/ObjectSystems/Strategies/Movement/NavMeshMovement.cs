using PEGA.ObjectSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.Strategies.Movement
{
    public class NavMeshMovement :IMovementStrategy
    {
        // Com esse tpo de movimento o Nevmash controla a gravidade e velocidade,
        public void Gravity(ObjectMovement context)
        {
            //throw new System.NotImplementedException();
        }

        public void Move(ObjectMovement context)
        {
            if (context.NavMeshAgent == null) return;
            if (context.Target != null)
            {
                context.NavMeshAgent.SetDestination(context.Target.position);
            }

            // Atualiza o InputVector com base no NavMeshAgent
            context.InputVector = new Vector2(context.NavMeshAgent.desiredVelocity.x, context.NavMeshAgent.desiredVelocity.z).normalized;
        }

        public void Rotate(ObjectMovement context)
        {
            //throw new System.NotImplementedException();
        }
    }
}
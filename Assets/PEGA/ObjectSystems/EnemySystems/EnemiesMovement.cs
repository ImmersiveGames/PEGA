using UnityEngine;
using UnityEngine.AI;

namespace PEGA.ObjectSystems.EnemySystems
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemiesMovement : ObjectMovement
    {
        private NavMeshAgent _navMeshAgent;
        private Transform _target;

        protected override void SetInitialReferences()
        {
            base.SetInitialReferences();
            _navMeshAgent = GetComponent<NavMeshAgent>();
            _target = GameObject.FindGameObjectWithTag("Player").transform; // Assume que o inimigo segue o jogador
        }

        protected override void FixedUpdate()
        {
            ApplyGravity();
            RotatePlayer();
            MoveTowardsTarget();
        }

        private void MoveTowardsTarget()
        {
            if (_target != null)
            {
                _navMeshAgent.SetDestination(_target.position);
            }

            // Atualiza o InputVector com base no NavMeshAgent
            InputVector = new Vector2(_navMeshAgent.desiredVelocity.x, _navMeshAgent.desiredVelocity.z).normalized;
        }

        public override void ExecuteAction()
        {
            // Deixe o NavMeshAgent controlar o movimento
            // Se necessário, adicione outras lógicas, como ataque ao jogador.
        }
    }
}
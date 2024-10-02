using PEGA.ObjectSystems.Strategies.Movement;
using UnityEngine;
using UnityEngine.AI;

namespace PEGA.ObjectSystems.EnemySystems
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemiesMovement : ObjectMovement
    {
        protected override void Awake()
        {
            base.Awake();
            SetMovementStrategy(new NavMeshMovement());
        }

        protected override void SetInitialReferences()
        {
            base.SetInitialReferences();
            CharacterController = GetComponent<CharacterController>();
            NavMeshAgent = GetComponent<NavMeshAgent>();
            NavMeshAgent.updateRotation = true; // Desativa o controle automático da rotação
            NavMeshAgent.updatePosition = true; // Desativa a movimentação automática completa, mas você pode manter ativado

            //TODO: Mudar para uma localização de player melhor
            Target = GameObject.FindGameObjectWithTag("Player").transform; // Assume que o inimigo segue o jogador
        }
    }
}
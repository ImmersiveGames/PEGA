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
            NavMeshAgent.updateRotation = true; // Des/Ativa o controle automático da rotação
            NavMeshAgent.updatePosition = true; // Des/Ativa a movimentação automática completa, mas você pode manter ativado
        }

        
    }
}
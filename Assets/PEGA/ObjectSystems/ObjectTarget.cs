
using UnityEngine;
using UnityEngine.AI;

namespace PEGA.ObjectSystems
{
    public class ObjectTarget : MonoBehaviour
    {
        public Transform chaseObject;
        public Transform myTarget;

        private bool inDestiny;

        private NavMeshAgent _agent;
        #region Unity Methods

        protected virtual void Awake()
        {
            SetInitialReferences();
            _agent = GetComponent<NavMeshAgent>();
            inDestiny = false;
        }

        protected virtual void Update()
        {
            if (chaseObject != myTarget)
            {
                SetTarget();
            }

            CheckFinalDestination();
        }

        #endregion
        
        protected virtual void SetInitialReferences()
        {
            //SetTarget();
        }

        protected virtual void SetTarget()
        {
            //TODO: Mudar para uma localização de player melhor
            var hasPlayer = GameObject.FindGameObjectWithTag("Player").transform;
            inDestiny = false;
            myTarget = chaseObject == null ? hasPlayer : // Assume que o inimigo segue o jogador
                chaseObject;
        }

        private void CheckFinalDestination()
        {
            // Verifica se o agente chegou ao destino
            if (_agent.pathPending || !(_agent.remainingDistance <= _agent.stoppingDistance)) return;
            if (!_agent.hasPath || _agent.velocity.sqrMagnitude == 0f && inDestiny == false)
            {
                OnDestinationReached();
            }
        }
        
        protected virtual void OnDestinationReached()
        {
            inDestiny = true;
            // Ação ao alcançar o ponto final
            Debug.Log("Destino alcançado!");
        }
        
    }
}
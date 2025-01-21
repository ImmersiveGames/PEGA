using UnityEngine;
using UnityEngine.AI;

public class EnemyBehavior : MonoBehaviour
{
    [Header("Navigation Settings")]
    public Transform forTestsObject; // Objeto alternativo para testes
    public Transform enemyBase; // Base do inimigo para entregar objetos coletáveis

    [Header("Action Settings")]
    [SerializeField] private Transform backpackTransform; // Ponto onde o objeto será "anexado"
    [SerializeField] private LayerMask collectablesLayer; // Camada dos objetos coletáveis
    [SerializeField] private LayerMask baseLayer; // Camada dos objetos base
    [SerializeField] private LayerMask playerLayer; // Camada do jogador
    [SerializeField] private Vector3 detectionBoxSize = new Vector3(1f, 1f, 1f); // Tamanho da área de detecção
    [SerializeField] private float detectionDistance = 1f; // Distância máxima para detectar objetos

    [Header("Collectables Pool")]
    [SerializeField] private Transform[] collectablesPool; // Pool de objetos coletáveis configurada manualmente no Inspector
    private bool[] collectablesAvailable; // Indica quais objetos do pool ainda podem ser selecionados

    public Transform chaseObject = null; // Objeto atualmente perseguido
    private Transform currentObjectInBackpack = null; // Objeto atualmente na "mochila"
    private NavMeshAgent _agent;
    private bool waitingForInput = true; // Define se o inimigo está aguardando o comando inicial
    private bool inDestiny = false; // Controle para saber se chegou ao destino

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        InitializeCollectablesAvailability();
    }

    private void Update()
    {
        if (waitingForInput)
        {
            if (Input.GetKeyDown(KeyCode.K))
            {
                ToggleChaseObject();
                waitingForInput = false; // Para de esperar input e inicia o comportamento
            }
            return;
        }

        if (chaseObject != null)
        {
            _agent.SetDestination(chaseObject.position);
            CheckFinalDestination();
        }
        else
        {
            Transform player = GameObject.FindObjectOfType<PlayerController>()?.transform;
            if (player != null)
            {
                _agent.SetDestination(player.position);
                CheckFinalDestination();
            }
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            ToggleChaseObject();
        }
    }

    private void ToggleChaseObject()
    {
        if (chaseObject == null)
        {
            if (currentObjectInBackpack != null)
            {
                chaseObject = enemyBase;
                Debug.Log("Prioridade: entregando o objeto atual na base antes de buscar outro.");
            }
            else
            {
                chaseObject = GetRandomAvailableCollectable();
                if (chaseObject != null)
                {
                    Debug.Log($"Alvo alterado para objeto da pool: {chaseObject.name}");
                }
                else
                {
                    Debug.Log("Nenhum objeto disponível na pool de coletáveis.");
                }
            }
        }
        else
        {
            chaseObject = null;
            Debug.Log("Alvo alterado para o Player.");
        }
    }

    private void InitializeCollectablesAvailability()
    {
        collectablesAvailable = new bool[collectablesPool.Length];
        for (int i = 0; i < collectablesAvailable.Length; i++)
        {
            collectablesAvailable[i] = true;
        }
    }

    private Transform GetRandomAvailableCollectable()
    {
        var availableIndices = new System.Collections.Generic.List<int>();
        for (int i = 0; i < collectablesAvailable.Length; i++)
        {
            if (collectablesAvailable[i]) availableIndices.Add(i);
        }

        if (availableIndices.Count == 0) return null;

        int randomIndex = availableIndices[Random.Range(0, availableIndices.Count)];
        return collectablesPool[randomIndex];
    }

    private void CheckFinalDestination()
    {
        if (_agent.pathPending || !(_agent.remainingDistance <= _agent.stoppingDistance)) return;

        if (!_agent.hasPath || _agent.velocity.sqrMagnitude == 0f && !inDestiny)
        {
            OnDestinationReached();
        }
    }

    private void OnDestinationReached()
    {
        inDestiny = true;

        var chase = (chaseObject != null)? chaseObject:FindObjectOfType<PlayerController>()?.transform;;

        if (chase != null)
        {
            Vector3 direction = (chase.position - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }

        if (chaseObject == null)
        {
            DetectAndInteractWithPlayer();
            return;
        }

        if (currentObjectInBackpack != null && chaseObject == enemyBase)
        {
            PlaceObjectInBase();
        }
        else
        {
            AttachObjectToBackpack();
        }
    }

    private void DetectAndInteractWithPlayer()
    {
        Vector3 boxCenter = transform.position + transform.forward * detectionDistance;
        Collider[] nearbyObjects = Physics.OverlapBox(boxCenter, detectionBoxSize / 2, transform.rotation, playerLayer);

        foreach (var obj in nearbyObjects)
        {
            PlayerController playerController = obj.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.ParalyzePlayer(3f);
                waitingForInput = true;
                Debug.Log("Paralisou o jogador e voltou ao estado inicial.");
                chaseObject = GetRandomAvailableCollectable();
                return;
            }
        }

        Debug.Log("Nenhum jogador encontrado na área.");
    }

    private void AttachObjectToBackpack()
    {
        Vector3 boxCenter = transform.position + transform.forward * detectionDistance;
        Collider[] nearbyObjects = Physics.OverlapBox(boxCenter, detectionBoxSize / 2, transform.rotation, collectablesLayer);

        foreach (var obj in nearbyObjects)
        {
            if (obj.CompareTag("Collectable") && currentObjectInBackpack == null)
            {
                obj.transform.position = backpackTransform.position;
                obj.transform.SetParent(backpackTransform);
                currentObjectInBackpack = obj.transform;
                chaseObject = enemyBase;
                break;
            }
        }
    }

    private void PlaceObjectInBase()
    {
        Vector3 boxCenter = transform.position + transform.forward * detectionDistance;
        Collider[] nearbyBases = Physics.OverlapBox(boxCenter, detectionBoxSize / 2, transform.rotation, baseLayer);

        foreach (var obj in nearbyBases)
        {
            if (obj.CompareTag("Collectable_Base") && currentObjectInBackpack != null)
            {
                currentObjectInBackpack.SetParent(obj.transform);
                currentObjectInBackpack.position = obj.transform.position;
                RemoveCollectableFromPool(currentObjectInBackpack);
                currentObjectInBackpack = null;
                chaseObject = null;
                break;
            }
        }
    }

    private void RemoveCollectableFromPool(Transform collectable)
    {
        for (int i = 0; i < collectablesPool.Length; i++)
        {
            if (collectablesPool[i] == collectable)
            {
                collectablesAvailable[i] = false;
                break;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 boxCenter = transform.position + transform.forward * detectionDistance;
        Gizmos.DrawWireCube(boxCenter, detectionBoxSize);
    }
}

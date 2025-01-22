using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Components References")]
    [SerializeField] CharacterController characterController; // Componente Character Controller
    [SerializeField] Animator animator; // Componente de animação do jogador

    [Header("Movement Settings")]
    [SerializeField] float speed = 5f; // Velocidade do jogador
    [SerializeField] float rotationSpeed = 10f; // Velocidade de rotação do jogador
    [SerializeField] string movementParameterName = "Movement"; // Nome do parâmetro do Blend Tree de movimento
    [SerializeField] float gravityModifier;
    
    [Header("Action Settings")]
    [SerializeField] Vector3 interactionBoxSize; // Distância máxima para interagir com a porta
    [SerializeField] LayerMask interactableLayer; // Camadas de objetos interativos
    [SerializeField] Transform backpackTransform = null;

    InputActionAsset inputAsset;
    InputActionMap player;
    InputAction move;
    InputAction action;

    Transform currentObjectInBackpack = null;
    bool onAction = false;

    private void Awake()
    {
        inputAsset = this.GetComponent<PlayerInput>().actions;
        player = inputAsset.FindActionMap("Player");
        move = player.FindAction("Move");
        action = player.FindAction("Action");

        player.Enable();

        move.performed += ctx => OnMovePerformed(ctx.ReadValue<Vector2>());
        move.canceled += ctx => OnMoveCanceled();
        action.performed += ctx => OnActionPerformed();
    }

    public void OnMovePerformed(Vector2 input)
    {
        if (onAction) return;

        moveInput = input;
    }

    private void OnMoveCanceled()
    {
        moveInput = Vector2.zero;
    }

    private Vector2 moveInput;

    private void FixedUpdate()
    {
        // Aplicar gravidade
        ApplyGravity();
        // Rotacionar o jogador para a direção de movimento
        if (!isParalyzed) // Só rotaciona normalmente se não estiver paralisado
            RotatePlayer();
        // Movimentar o jogador
        MovePlayer();
        // Atualizar a animação do jogador
        UpdateAnimation();

        // Efeito visual de paralisia (giro no eixo Y)
        if (isParalyzed)
        {
            RotateWhileParalyzed();
        }
    }


    public void ToogleOnAction(bool value) 
    {
        onAction = value;
        OnMoveCanceled();
    }

    public void OnActionOff()
    {
        onAction = false;
    }

    private void MovePlayer()
    {
        // Calcula o movimento na direção para a frente do jogador
        Vector3 movement = CalculateMovement();
        characterController.Move(movement * speed * Time.deltaTime);
    }

    private void RotatePlayer()
    {
        if (moveInput != Vector2.zero)
        {
            // Calcular a direção de rotação com base no input
            Vector3 desiredDirection = new Vector3(moveInput.x, 0f, moveInput.y);
            Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
            // Rodar gradualmente em direção à nova direção
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private Vector3 CalculateMovement()
    {
        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        return forward * moveInput.y +
               right * moveInput.x;
    }

    private void UpdateAnimation()
    {
        float moveMagnitude = moveInput.magnitude;
        animator.SetFloat(movementParameterName, moveMagnitude); // Define o parâmetro do Blend Tree de movimento
    }

    private void ApplyGravity()
    {
        if (!characterController.isGrounded)
        {
            // Aplicar a gravidade usando o CharacterController
            Vector3 gravityVector = new Vector3(0, Physics.gravity.y * gravityModifier * Time.deltaTime, 0);
            characterController.Move(gravityVector);
        }
    }

    private void OnActionPerformed() 
    {

        Debug.Log("Chamei a função de ação");

        Vector3 boxCenter = transform.position + transform.forward * interactionBoxSize.z;
        boxCenter.y = transform.position.y + interactionBoxSize.y / 2;

        Collider[] objects = Physics.OverlapBox(boxCenter, interactionBoxSize / 2, transform.rotation, interactableLayer);
        
        if (objects.Length == 0) return;

        foreach (Collider item in objects)
        {
            Debug.Log("I interacted with: " + item.name);

            if (item.gameObject.CompareTag("Collectable") && currentObjectInBackpack == null) 
            {                
                item.transform.position = backpackTransform.position;
                item.transform.SetParent(backpackTransform);
                currentObjectInBackpack = item.transform;
                break;
            }
            else if (item.gameObject.CompareTag("Collectable_Base")) 
            {
                if (currentObjectInBackpack != null) 
                {
                    float yPlace = item.transform.position.y + 
                                   ((item.transform.localScale.y / 2) +
                                   (currentObjectInBackpack.localScale.y / 2));
                    Vector3 placePosition = new Vector3(item.transform.position.x, yPlace, item.transform.position.z);
                    currentObjectInBackpack.transform.position = placePosition;
                    currentObjectInBackpack.SetParent(item.transform);
                    currentObjectInBackpack = null;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 boxCenter = transform.position + transform.forward * interactionBoxSize.z;
        boxCenter.y = transform.position.y + interactionBoxSize.y / 2;
        Gizmos.DrawWireCube(boxCenter, interactionBoxSize); // Desenha a caixa de detecção no editor
    }
    
    private Coroutine paralysisCoroutine;
    [Header("Visual Effects")]
    [SerializeField] private float paralysisRotationSpeed = 720f; // Velocidade de rotação durante a paralisação
    private bool isParalyzed = false; // Flag para saber se o jogador está paralisado
    public void ParalyzePlayer(float duration)
    {
        if (paralysisCoroutine != null)
        {
            StopCoroutine(paralysisCoroutine); // Evita múltiplas paralisações simultâneas
        }

        paralysisCoroutine = StartCoroutine(ParalysisRoutine(duration));
    }

    private IEnumerator ParalysisRoutine(float duration)
    {
        Debug.Log("Jogador paralisado.");
        ToogleOnAction(true); // Impede a movimentação
        isParalyzed = true;   // Ativa o estado de paralisia (e efeito visual)
    
        yield return new WaitForSeconds(duration);
    
        ToogleOnAction(false); // Permite a movimentação novamente
        isParalyzed = false;   // Desativa o estado de paralisia (e efeito visual)
        Debug.Log("Jogador recuperou o controle.");
    }

    private void RotateWhileParalyzed()
    {
        transform.Rotate(Vector3.up, paralysisRotationSpeed * Time.fixedDeltaTime, Space.Self);
    }


}
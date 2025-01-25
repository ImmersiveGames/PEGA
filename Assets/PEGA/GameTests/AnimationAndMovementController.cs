using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AnimationAndMovementController : MonoBehaviour
{
    // Hash
    int isWalkingHash;
    int isRunningHash;

    int isJumpingHash;

    // Deklarasi variabel referensi
    CharacterController characterController;
    Animator animator;

    PegaInputActions playerInput;

    // Variabel Input Player
    Vector2 currentMovementInput;
    Vector3 currentMovement;

    Vector3 currentRunMovement;

    // Gravity
    float groundedGravity = -.05f;

    float gravity = -9.8f;

    // Boolean
    bool isMovementPressed;

    bool isRunPressed;

    // Jump Variable
    bool isJumping = false;
    bool isJumpPressed = false;
    float initialJumpVelocity;
    float maxJumpHeight = 4f;
    float maxJumpTime = .75f;

    float maxJumpSpeed;

    // Run Variable
    float runMultiplier = 3.0f;

    // Rotation
    float rotationFactorPerFrame = 15.0f;

    //Kode yang dijalankan sebelum Start di Perputaran Unity Event 
    void Awake()
    {
        // Reference Variables
        playerInput = new PegaInputActions();
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        // Hash
        isWalkingHash = Animator.StringToHash("isWalking");
        isRunningHash = Animator.StringToHash("isRunning");
        isJumpingHash = Animator.StringToHash("isJumping");

        // Move Input
        playerInput.Player.Axis_Move.started += onMovementInput;
        playerInput.Player.Axis_Move.canceled += onMovementInput;
        playerInput.Player.Axis_Move.performed += onMovementInput;


        // Jump Input
        playerInput.Player.Jump.started += onJump;
        playerInput.Player.Jump.canceled += onJump;

        setupJumpValue();
    }
    // Menyiapkan Niiai Jump

    void setupJumpValue()
    {
        float timeToApex = maxJumpTime / 2;
        gravity = (-2 * maxJumpHeight) / Mathf.Pow(timeToApex, 2);
        initialJumpVelocity = (2 * maxJumpHeight) / timeToApex;
    }

    void onJump(InputAction.CallbackContext context)
    {
        isJumpPressed = context.ReadValueAsButton();
    }

    void onRun(InputAction.CallbackContext context)
    {
        isRunPressed = context.ReadValueAsButton();
    }

    void onMovementInput(InputAction.CallbackContext context)
    {
        currentMovementInput = context.ReadValue<Vector2>();

        currentMovement.x = currentMovementInput.x;
        currentMovement.z = currentMovementInput.y;

        currentRunMovement.x = currentMovementInput.x * runMultiplier;
        currentRunMovement.z = currentMovementInput.y * runMultiplier;

        isMovementPressed = currentMovementInput.x != 0 || currentMovementInput.y != 0;
    }

    void handleJump()
    {
        if (!isJumping && characterController.isGrounded && isJumpPressed)
        {
            isJumping = true;
            currentMovement.y = initialJumpVelocity * .5f;
            currentRunMovement.y = initialJumpVelocity * .5f;
        }
        else if (isJumping && !characterController.isGrounded && !isJumpPressed)
        {
            isJumping = false;
        }
    }

    void handleAnimation()
    {
        /*bool isWalking=animator.GetBool(isWalkingHash);
        bool isJumping=animator.GetBool(isJumpingHash);
        bool isRunning=animator.GetBool(isRunningHash);
        if(isMovementPressed && !isWalking)
        {
            animator.SetBool(isWalkingHash, true);

        }
        else if(!isMovementPressed && isWalking)
        {
            animator.SetBool(isWalkingHash,false);

        }
        if((isMovementPressed && isRunPressed) && !isRunning){
            animator.SetBool(isRunningHash, true);
        }
        else if((!isMovementPressed || !isRunPressed) && isRunning){
            animator.SetBool(isRunningHash,false);
        }

        if(!isJumping && isJumpPressed && characterController.isGrounded){
            animator.SetBool(isJumpingHash,true);
            Debug.Log(isJumpingHash);
        }
        else if (isJumping && !isJumpPressed && characterController.isGrounded) {
            animator.SetBool(isJumpingHash,false);
        }*/
    }

    void handleRotation()
    {
        if (isMovementPressed)
        {
            // Calculate the target rotation based on the movement input direction
            Vector3 movementDirection = new Vector3(currentMovement.x, 0f, currentMovement.z);
            Quaternion targetRotation = Quaternion.LookRotation(movementDirection);

            // Smoothly rotate towards the target rotation
            transform.rotation =
                Quaternion.Slerp(transform.rotation, targetRotation, rotationFactorPerFrame * Time.deltaTime);
        }
    }

    void handleGravity()
    {
        float fallMultiplier = 2.0f;
        bool isFalling = currentMovement.y <= 0.0f;

        if (characterController.isGrounded)
        {
            currentMovement.y = groundedGravity;
            currentRunMovement.y = groundedGravity;
        }
        else if (isFalling)
        {
            float previousYVelocity = currentMovement.y;
            float newYVelocity = currentMovement.y + (gravity * fallMultiplier * Time.deltaTime);
            float nextYVelocity = Mathf.Max((previousYVelocity + newYVelocity) * .5f,-20f);
            currentMovement.y = nextYVelocity;
            currentRunMovement.y = nextYVelocity;
        }
        else
        {
            float previousYVelocity = currentMovement.y;
            float newYVelocity = currentMovement.y + (gravity * Time.deltaTime);
            float nextYVelocity = (previousYVelocity + newYVelocity) * .5f;
            currentMovement.y = nextYVelocity;
            currentRunMovement.y = nextYVelocity;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Handle
        handleRotation();
        handleAnimation();
        handleJump();
        //characterController.Move(currentMovement * Time.deltaTime);

        if (isRunPressed)
        {
            characterController.Move(currentRunMovement * Time.deltaTime);
        }
        else
        {
            characterController.Move(currentMovement * Time.deltaTime);
        }

        handleGravity();
    }

    void OnEnable()
    {
        playerInput.Player.Enable();
    }

    void OnDisable()
    {
        playerInput.Player.Disable();
    }
}
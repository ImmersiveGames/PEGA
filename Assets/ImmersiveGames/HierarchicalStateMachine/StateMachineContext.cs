using ImmersiveGames.HierarchicalStateMachine.States;
using ImmersiveGames.Utils;
using PEGA.ObjectSystems.MovementSystems;
using UnityEngine;
using UnityEngine.Serialization;

namespace ImmersiveGames.HierarchicalStateMachine
{
    public class StateMachineContext : MonoBehaviour
    {
        private MovementController _movementController; // sem expor diretamente os inputs o ideal é tornar isso publico.
        
        private StateFactory _states;
        
        //TODO: Separar em Scriptables objects.

        protected internal CharacterController CharacterController;

        public MovementSettings movementSettings;
        
        public bool isGrounded;
        public bool isJumping;
        public bool isWalking;
        public bool isFalling;
        public Vector3 movement;
        public Vector3 appliedMovement;
        public float rotationPerFrame;

        public float gravity;
        public float initialJumpVelocity;


        public BaseState CurrentState { get; set; }
        //TODO: Não sei se vale ter as variáveis expostas aqui as para debug acho que vale
        [Header("Input Pressing")]
        public Vector2 directionPressed;
        public bool jumpPressed;
        public bool dashPressed;
        

        private void UpdateInputs()
        {
            directionPressed = _movementController.GetMovementPressing();
            jumpPressed = _movementController.IsJumpPressing();
            dashPressed = _movementController.IsDashPressing();
        }

        //TODO: Ele adiciona variáveis que pega do input mas acho que isso não vem ao caso agora



        private void Awake()
        {
            
            //movement = _movementController.GetMovementInput();
            
            //ele também faz o awake das variáveis aqui.
            //TODO: inclusive o de jump state que eu transformei em static. mas como é só calculo não importa agora.
            
            //muita coisa para um contexto.
            _movementController = GetComponent<MovementController>();
            CharacterController = GetComponent<CharacterController>();
            
            _states = new StateFactory(this); //Cria a fabric usando este contexto
            CurrentState = _states.Grounded(); //cria um estado corrente para iniciar o jogo em grounded (um dos roots)
            CurrentState.EnterState(); //Inicia o Grounded para iniciar o jogo em um estado.
        }
        private void Start()
        {
            
            //TODO: aqui se der problema com a gravidade ele sugere iniciar o character.move aqui com apply
        }

        private void Update()
        {
            UpdateInputs();
            HandleRotate();
            HandleMovement();
            
            //Debug.Log($"[StateMachineContext] To no chão {CharacterController.isGrounded}");
            
            //ele coloca a rotação aqui também junto com o character move!! e aqui controla o character.move
            //TODO: o character move acho que faz sentido estar aqui mas ele pode estar num separado que é o ideal.
            CurrentState.UpdateStates();
            
            CharacterController.Move(appliedMovement * Time.deltaTime);
        }
        
        private void HandleRotate()
        {
            Vector3 positionToLookAt;
            positionToLookAt.x = movement.x;
            positionToLookAt.y = 0.0f;
            positionToLookAt.z = movement.z;

            var currentRotation = transform.rotation;
            if (_movementController.GetMovementPressing() == Vector2.zero) return;
            var targetRotation = Quaternion.LookRotation(positionToLookAt);
            transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, rotationPerFrame * Time.deltaTime);
        }
        private void HandleMovement()
        {
            /*var speedModifier = _modifierController.GetModifierValue(ModifierKeys.SpeedMultiplay);
            speedModifier = (speedModifier == 0) ? 1f : speedModifier;*/

            /*movement.x = _movementController.GetMovementPressing().x * movementSettings.baseSpeed;//(_actualSpeed * speedModifier);
            movement.z = _movementController.GetMovementPressing().y * movementSettings.baseSpeed;//(_actualSpeed * speedModifier);

            appliedMovement.x = movement.x;
            appliedMovement.z = movement.z;*/
        }
        
    }
}
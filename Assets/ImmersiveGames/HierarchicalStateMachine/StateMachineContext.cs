using ImmersiveGames.HierarchicalStateMachine.States;
using ImmersiveGames.Utils;
using PEGA.ObjectSystems.MovementSystems;
using UnityEngine;

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
            
            //Debug.Log($"[StateMachineContext] To no chão {CharacterController.isGrounded}");
            
            //ele coloca a rotação aqui também junto com o character move!! e aqui controla o character.move
            //TODO: o character move acho que faz sentido estar aqui mas ele pode estar num separado que é o ideal.
            CurrentState.UpdateStates();
            
            CharacterController.Move(appliedMovement * Time.deltaTime);
        }
        
        
    }
}
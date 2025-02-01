using ImmersiveGames.StateMachine.States;
using UnityEngine;

namespace ImmersiveGames.StateMachine
{
    public class ContextStates : MonoBehaviour
    {
        private StateFactory _states;

        public CharacterController characterController;
        
        [SerializeField] public Vector3 movement;
        [SerializeField] public bool isGrounded;
        [SerializeField] public bool isWalking;
        [SerializeField] public bool isJumping;
        [SerializeField] public bool isFalling;
        [SerializeField] public bool isDashing;

        public BaseState CurrentState { get; set; }

        //TODO: Ele adiciona variáveis que pega do input mas acho que isso não vem ao caso agora


        private void Start()
        {
            //TODO: aqui se der problema com a gravidade ele sugere iniciar o character.move aqui com apply
        }

        private void Awake()
        {
            //ele também faz o awake das variáveis aqui.
            //TODO: inclusive o de jump state que eu transformei em static. mas como é só calculo não importa agora.
            
            //muita coisa para um contexto.
            characterController = GetComponent<CharacterController>();
            _states = new StateFactory(this); //Cria a fabric usando este contexto
            CurrentState = _states.Grounded(); //cria um estado corrente para iniciar o jogo em grounded (um dos roots)
            CurrentState.EnterState(); //Inicia o Grounded pra iniciar o jogo dentro de um estado.
        }

        private void Update()
        {
            
            //ele coloca a rotação aqui também junto com o character move!! e aqui controla o charater.move
            //TODO: o character move acho que faz sentid estar aqui mas ele pode estar num separado que é o ideal.
            CurrentState.UpdateStates();
        }
    }
}
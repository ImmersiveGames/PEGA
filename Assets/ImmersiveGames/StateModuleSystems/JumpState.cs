using UnityEngine;

namespace ImmersiveGames.StateModuleSystems
{ 
    public class JumpState : StateModule
    {
        public override void Enter()
        {
            Debug.Log("[JUMP] Entrou");
            blackboard.isJumping = true;

            // Mantém o movimento ativo (se estiver ocorrendo)
            if (blackboard.moveInput != Vector2.zero)
            {
                StateManager.ActivateState(GetComponent<MoveState>());
            }
        }

        public override void Exit()
        {
            Debug.Log("[JUMP] Saiu");
            blackboard.isJumping = false;
        }

        public override void UpdateState()
        {
            Debug.Log("[JUMP] Executando...");
        }

        public override bool CanBeInterrupted() => false;

        private void HandleJumpReleased()
        {
            StateManager.DeactivateState(this);
        }

        // Novo método para ouvir o evento de pulo
        private void OnEnable()
        {
            InputHandler.OnJumpPressed += HandleJumpPressed;
        }

        private void OnDisable()
        {
            InputHandler.OnJumpPressed -= HandleJumpPressed;
        }

        private void HandleJumpPressed()
        {
            // Ativa o JumpState quando o botão de pulo é pressionado
            StateManager.ActivateState(this);
        }
    }
}
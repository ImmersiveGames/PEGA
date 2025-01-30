using UnityEngine;

namespace ImmersiveGames.StateModuleSystems
{
    public class IdleState : StateModule
    {
        public override void Enter()
        {
            Debug.Log("[IDLE] Entrou");
            InputHandler.OnMoveInput += HandleMoveInput;
        }

        public override void Exit()
        {
            Debug.Log("[IDLE] Saiu");
            InputHandler.OnMoveInput -= HandleMoveInput;
        }

        public override void UpdateState() { }

        public override bool CanBeInterrupted() => true;

        private void HandleMoveInput(Vector2 direction)
        {
            blackboard.moveInput = direction;
            if (direction != Vector2.zero)
            {
                StateManager.ActivateState(GetComponent<MoveState>());
            }
        }
    }
}
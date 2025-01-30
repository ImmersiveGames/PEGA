using UnityEngine;

namespace ImmersiveGames.StateModuleSystems
{
    public class MoveState : StateModule
    {
        public override void Enter()
        {
            Debug.Log("[MOVE] Entrou");
            InputHandler.OnMoveInput += HandleMoveInput;
        }

        public override void Exit()
        {
            Debug.Log("[MOVE] Saiu");
            InputHandler.OnMoveInput -= HandleMoveInput;
        }

        public override void UpdateState()
        {
            Debug.Log($"[MOVE] Input Atual: {blackboard.moveInput}");
        }

        public override bool CanBeInterrupted() => true;

        private void HandleMoveInput(Vector2 direction)
        {
            blackboard.moveInput = direction;
            if (direction == Vector2.zero)
            {
                StateManager.DeactivateState(this);
            }
        }
    }
}
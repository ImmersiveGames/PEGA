using UnityEngine;

namespace ImmersiveGames.StateModuleSystems
{
    public class TempMoveActivator : MonoBehaviour
    {
        [SerializeField] private MoveState moveState;

        private void OnEnable()
        {
            InputHandler.OnMoveInput += OnMove;
        }

        private void OnDisable()
        {
            InputHandler.OnMoveInput -= OnMove;
        }

        private void OnMove(Vector2 direction)
        {
            if (direction != Vector2.zero)
            {
                GetComponent<StateManager>().ActivateState(moveState);
            }
        }
    }
}
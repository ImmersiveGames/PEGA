using Immersive.Framework.PlayerParticipation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.Prototyping.PlayerPlaceholder
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerGameplayInputReader))]
    public sealed class PlayerMovementPlaceholder : MonoBehaviour
    {
        [SerializeField] private InputActionReference moveAction;
        [SerializeField, Min(0f)] private float movementSpeed = 4f;

        private CharacterController _characterController;
        private PlayerGameplayInputReader _inputReader;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _inputReader = GetComponent<PlayerGameplayInputReader>();

            if (_characterController == null || _inputReader == null ||
                moveAction == null || moveAction.action == null)
            {
                Debug.LogError(
                    "PlayerMovementPlaceholder requires a CharacterController, PlayerGameplayInputReader and an authored Move action reference on the Actor.",
                    this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!_inputReader.TryReadValue(moveAction, out Vector2 input))
            {
                return;
            }

            Vector2 direction = Vector2.ClampMagnitude(input, 1f);
            Vector3 horizontalMovement =
                new Vector3(direction.x, 0f, direction.y) *
                (movementSpeed * Time.deltaTime);

            _characterController.Move(horizontalMovement);
        }
    }
}

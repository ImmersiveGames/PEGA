using Immersive.Framework.PlayerParticipation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerGameplayInputReader))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PEGAPlayerMovementMechanic : MonoBehaviour
    {
        private const float DirectionThreshold = 0.0001f;

        [Header("Input")]
        [SerializeField]
        private InputActionReference moveAction;

        [Header("Movement")]
        [SerializeField, Min(0f)]
        private float maxSpeed = 4f;

        [SerializeField, Min(0f)]
        private float acceleration = 8f;

        [SerializeField, Min(0f)]
        private float rotationSpeed = 360f;

        private IPlayerGameplayInputReader _inputReader;
        private CharacterController _characterController;
        private Vector2 _moveInput;
        private Vector3 _currentVelocity;
        private bool _isConfigured;
        private bool _inputReadErrorLogged;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerGameplayInputReader>();
            _characterController = GetComponent<CharacterController>();
            _isConfigured = ValidateConfiguration();
            if (!_isConfigured)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            if (!_isConfigured)
            {
                return;
            }

            if (!_inputReader.RuntimeGameplayAvailable)
            {
                _moveInput = Vector2.zero;
                _currentVelocity = Vector3.zero;
                return;
            }

            if (!_inputReader.TryReadValue(moveAction, out Vector2 move))
            {
                _moveInput = Vector2.zero;
                _currentVelocity = Vector3.zero;
                if (!_inputReadErrorLogged)
                {
                    Debug.LogError(
                        $"{nameof(PEGAPlayerMovementMechanic)} could not read the authored Move action. {_inputReader.Diagnostic}",
                        this);
                    _inputReadErrorLogged = true;
                }

                return;
            }

            _inputReadErrorLogged = false;
            _moveInput = Vector2.ClampMagnitude(move, 1f);
            MoveActor(Time.deltaTime);
        }

        private void MoveActor(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            // O yaw fixo do #29 está alinhado ao mundo; X/Z coincidem com a base da câmera.
            Vector3 targetVelocity =
                new Vector3(_moveInput.x, 0f, _moveInput.y) * maxSpeed;
            Vector3 requestedVelocity = Vector3.MoveTowards(
                _currentVelocity,
                targetVelocity,
                acceleration * deltaTime);

            Vector3 previousPosition = transform.position;
            _characterController.Move(requestedVelocity * deltaTime);

            Vector3 actualDisplacement = transform.position - previousPosition;
            actualDisplacement.y = 0f;
            _currentVelocity = Vector3.ClampMagnitude(
                actualDisplacement / deltaTime,
                maxSpeed);

            if (_currentVelocity.sqrMagnitude <= DirectionThreshold)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(
                _currentVelocity,
                Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * deltaTime);
        }

        private bool ValidateConfiguration()
        {
            if (_inputReader == null)
            {
                Debug.LogError(
                    $"{nameof(PEGAPlayerMovementMechanic)} requires {nameof(PlayerGameplayInputReader)} on the Actor root.",
                    this);
                return false;
            }

            if (_characterController == null)
            {
                Debug.LogError(
                    $"{nameof(PEGAPlayerMovementMechanic)} requires {nameof(CharacterController)} on the Actor root.",
                    this);
                return false;
            }

            if (moveAction == null)
            {
                Debug.LogError(
                    $"{nameof(PEGAPlayerMovementMechanic)} requires an authored Move InputActionReference.",
                    this);
                return false;
            }

            if (!IsPositiveFinite(maxSpeed) ||
                !IsPositiveFinite(acceleration) ||
                !IsPositiveFinite(rotationSpeed))
            {
                Debug.LogError(
                    $"{nameof(PEGAPlayerMovementMechanic)} requires positive finite speed, acceleration and rotation settings.",
                    this);
                return false;
            }

            return true;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

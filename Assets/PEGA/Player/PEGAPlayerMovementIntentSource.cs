using Immersive.Framework.PlayerParticipation;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Actors;
using PEGA.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerGameplayInputReader))]
    public sealed class PEGAPlayerMovementIntentSource : MonoBehaviour, IPEGAPlanarMovementIntentSource
    {
        private const string LogCategory = "PEGA.Gameplay.Movement";
        private const float InputMagnitudeThreshold = 0.0001f;

        [Header("Input")]
        [SerializeField]
        private InputActionReference moveAction;

        [SerializeField, Range(0f, 0.95f)]
        [Tooltip("Deadzone adicional em magnitude normalizada (0–0,95), aplicada após o Input System.")]
        private float additionalMoveDeadzone;

        [SerializeField, Min(0.1f)]
        [Tooltip("Expoente sem unidade: 1 mantém resposta linear; valores maiores reduzem a intensidade em baixas deflexões.")]
        private float analogResponseExponent = 1f;

        private IPlayerGameplayInputReader _inputReader;
        private ScopedLogger _log;
        private bool _isConfigured;
        private bool _inputReadErrorLogged;
        private bool _hasObservedAvailability;
        private bool _lastGameplayAvailable;
        private bool _lastGameplayReady;
        private bool _lastHasBinding;
        private PlayerGameplayInputAvailability _lastAvailability;
        private int _lastBindingRevision;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerGameplayInputReader>();
            _log = PEGALogging.CreateScopedLogger<PEGAPlayerMovementIntentSource>(
                LogCategory,
                gameObject.name);
            _log.Debug(
                "Player movement intent source initialized.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("source", nameof(PEGAPlayerMovementIntentSource)),
                LogFields.Field("moveAction", moveAction != null ? moveAction.name : "unassigned"),
                LogFields.Field("additionalDeadzone", additionalMoveDeadzone),
                LogFields.Field("analogResponseExponent", analogResponseExponent));
            _isConfigured = ValidateConfiguration();
            if (!_isConfigured)
            {
                enabled = false;
                return;
            }

            ObserveInputAvailability();
        }

        bool IPEGAPlanarMovementIntentSource.TryGetIntent(
            float deltaTime,
            out PEGAPlanarMovementIntent intent)
        {
            intent = PEGAPlanarMovementIntent.Zero;
            bool gameplayAvailable = _inputReader != null && ObserveInputAvailability();
            if (!_isConfigured || !gameplayAvailable)
            {
                return false;
            }

            if (!_inputReader.TryReadValue(moveAction, out Vector2 move))
            {
                if (!_inputReadErrorLogged)
                {
                    _log.Error(
                        $"{nameof(PEGAPlayerMovementIntentSource)} could not read the authored Move action.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("moveAction", moveAction != null ? moveAction.name : "unassigned"),
                        LogFields.Field("diagnostic", _inputReader.Diagnostic));
                    _inputReadErrorLogged = true;
                }

                return false;
            }

            _inputReadErrorLogged = false;
            Vector2 responsiveMove = ApplyInputResponse(move);
            float magnitude = responsiveMove.magnitude;
            if (magnitude <= InputMagnitudeThreshold)
            {
                return true;
            }

            // A câmera P1 autorada no #29 usa yaw fixo alinhado ao mundo; sua base planar é X/Z.
            Vector3 worldDirection = new Vector3(responsiveMove.x, 0f, responsiveMove.y).normalized;
            intent = new PEGAPlanarMovementIntent(worldDirection, magnitude);
            return true;
        }

        private Vector2 ApplyInputResponse(Vector2 input)
        {
            Vector2 clampedInput = Vector2.ClampMagnitude(input, 1f);
            float inputMagnitude = clampedInput.magnitude;
            if (inputMagnitude <= additionalMoveDeadzone ||
                inputMagnitude <= InputMagnitudeThreshold)
            {
                return Vector2.zero;
            }

            float normalizedMagnitude = Mathf.InverseLerp(
                additionalMoveDeadzone,
                1f,
                inputMagnitude);
            float responseMagnitude = Mathf.Pow(
                normalizedMagnitude,
                analogResponseExponent);
            return clampedInput / inputMagnitude * responseMagnitude;
        }

        private bool ValidateConfiguration()
        {
            if (_inputReader == null)
            {
                _log.Error(
                    $"{nameof(PEGAPlayerMovementIntentSource)} requires {nameof(PlayerGameplayInputReader)} on the Player Actor root.",
                    LogFields.Field("actorRoot", gameObject.name));
                return false;
            }

            if (moveAction == null ||
                !IsFinite(additionalMoveDeadzone) ||
                additionalMoveDeadzone < 0f ||
                additionalMoveDeadzone > 0.95f ||
                !IsPositiveFinite(analogResponseExponent))
            {
                _log.Error(
                    $"{nameof(PEGAPlayerMovementIntentSource)} requires an authored Move action and valid analog response settings.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("moveActionAssigned", moveAction != null),
                    LogFields.Field("additionalDeadzone", additionalMoveDeadzone),
                    LogFields.Field("analogResponseExponent", analogResponseExponent));
                return false;
            }

            return true;
        }

        private bool ObserveInputAvailability()
        {
            bool hasBinding = _inputReader.HasCurrentGameplayBinding;
            bool gameplayReady = _inputReader.GameplayReady;
            PlayerGameplayInputAvailability availability = _inputReader.RuntimeGameplayAvailability;
            bool gameplayAvailable = _inputReader.RuntimeGameplayAvailable;
            int bindingRevision = _inputReader.BindingRevision;

            bool bindingStateChanged = !_hasObservedAvailability ||
                hasBinding != _lastHasBinding ||
                gameplayReady != _lastGameplayReady ||
                availability != _lastAvailability ||
                bindingRevision != _lastBindingRevision;

            if (gameplayAvailable != _lastGameplayAvailable && _hasObservedAvailability)
            {
                _log.Debug(
                    gameplayAvailable
                        ? "Player gameplay input became available through the Input Gate."
                        : "Player gameplay input became unavailable through the Input Gate.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("gameplayAvailable", gameplayAvailable),
                    LogFields.Field("gameplayReady", gameplayReady),
                    LogFields.Field("hasCurrentBinding", hasBinding),
                    LogFields.Field("availability", availability),
                    LogFields.Field("bindingRevision", bindingRevision),
                    LogFields.Field("diagnostic", _inputReader.Diagnostic));
            }
            else if (bindingStateChanged)
            {
                string message = !_hasObservedAvailability
                    ? gameplayAvailable
                        ? "Player gameplay input is available at source initialization."
                        : "Player gameplay input is initially unavailable until P1 joins and the Input Gate allows gameplay."
                    : "Player gameplay input binding or Input Gate state changed.";
                LogField[] fields =
                {
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("gameplayAvailable", gameplayAvailable),
                    LogFields.Field("gameplayReady", gameplayReady),
                    LogFields.Field("hasCurrentBinding", hasBinding),
                    LogFields.Field("availability", availability),
                    LogFields.Field("bindingRevision", bindingRevision),
                    LogFields.Field("diagnostic", _inputReader.Diagnostic)
                };

                if (!_hasObservedAvailability && gameplayAvailable)
                {
                    _log.Debug(message, fields);
                }
                else
                {
                    _log.Debug(message, fields);
                }
            }
            _hasObservedAvailability = true;
            _lastGameplayAvailable = gameplayAvailable;
            _lastGameplayReady = gameplayReady;
            _lastHasBinding = hasBinding;
            _lastAvailability = availability;
            _lastBindingRevision = bindingRevision;
            return gameplayAvailable;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && IsFinite(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

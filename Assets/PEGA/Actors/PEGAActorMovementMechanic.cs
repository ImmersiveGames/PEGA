using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.Actors
{
    public interface IPEGAPlanarMovementIntentSource
    {
        bool TryGetIntent(float deltaTime, out PEGAPlanarMovementIntent intent);
    }

    public readonly struct PEGAPlanarMovementIntent
    {
        private const float DirectionThreshold = 0.0001f;

        public Vector3 Direction { get; }
        public float Magnitude { get; }

        public PEGAPlanarMovementIntent(Vector3 direction, float magnitude)
        {
            if (!IsFinite(direction.x) ||
                !IsFinite(direction.y) ||
                !IsFinite(direction.z))
            {
                throw new System.ArgumentException(
                    "Movement intent direction must be finite.",
                    nameof(direction));
            }

            if (!IsFinite(magnitude) ||
                magnitude < 0f ||
                magnitude > 1f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(magnitude),
                    "Movement intent requires finite planar direction and normalized magnitude in [0, 1].");
            }

            direction.y = 0f;
            float directionSqrMagnitude = direction.sqrMagnitude;
            if (directionSqrMagnitude <= DirectionThreshold * DirectionThreshold)
            {
                if (magnitude > 0f)
                {
                    throw new System.ArgumentException(
                        "Movement intent with positive magnitude requires a non-zero planar direction.",
                        nameof(direction));
                }

                Direction = Vector3.zero;
                Magnitude = 0f;
                return;
            }

            Direction = direction / Mathf.Sqrt(directionSqrMagnitude);
            Magnitude = magnitude;
        }

        public static PEGAPlanarMovementIntent Zero => default;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PEGAActorMovementMechanic : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.Movement";
        private const float InputMagnitudeThreshold = 0.0001f;
        private const float MaximumPlanarTurnAngleDegrees = 180f;
        private const float IntentDirectionLogThresholdDegrees = 30f;
        private const float IntentMagnitudeLogThreshold = 0.2f;
        private const float PhysicalMotionLogThresholdSpeed = 0.05f;

        [Header("Intent Source")]
        [SerializeField]
        [Tooltip("Fonte PEGA de intenção planar vinculada explicitamente a este Actor.")]
        private MonoBehaviour intentSource;

        [Header("Movement")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Velocidade planar máxima do Actor (m/s).")]
        private float maxSpeed = 4f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Aceleração normal ao arrancar ou recuperar velocidade (m/s²).")]
        private float acceleration = 8f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Desaceleração normal ao soltar ou reduzir o input (m/s²).")]
        private float deceleration = 8f;

        [Header("Directional Response")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Velocidade efetiva mínima para analisar o ângulo da manobra (m/s). Abaixo dela, usa a aceleração normal de partida.")]
        private float minimumTurnAnalysisSpeed = 0.5f;

        [SerializeField, Range(1f, 179f)]
        [Tooltip("Ângulo (°) em que termina a transição para curva suave e começa a transição para curva fechada.")]
        private float closedCurveStartAngleDegrees = 25f;

        [SerializeField, Range(1f, 179f)]
        [Tooltip("Ângulo (°) no qual a resposta de curva fechada e a redução de velocidade chegam ao valor completo.")]
        private float closedCurveFullAngleDegrees = 100f;

        [SerializeField, Range(1f, 179f)]
        [Tooltip("Ângulo (°) a partir do qual a resposta de inversão e a frenagem reversa começam a transição.")]
        private float reversalStartAngleDegrees = 145f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Aceleração vetorial em curvas suaves, após a transição desde aceleração/frenagem normal (m/s²).")]
        private float smoothCurveResponseAcceleration = 16f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Aceleração vetorial quando a curva fechada atinge sua faixa completa (m/s²).")]
        private float closedCurveResponseAcceleration = 18f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Aceleração vetorial no regime de inversão (m/s²).")]
        private float reversalResponseAcceleration = 20f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Fração da velocidade-alvo máxima preservada em curva fechada; 1 mantém toda a velocidade e o valor deve ser maior que zero.")]
        private float closedCurveSpeedRetention = 0.9f;

        [SerializeField, Min(0f)]
        [Tooltip("Aceleração adicional contra a componente da velocidade que se opõe ao input durante a inversão (m/s²). Não afeta a frenagem ao soltar.")]
        private float reversalBrakingAcceleration = 8f;

        [Header("Actor Orientation")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Velocidade angular de orientação do Actor, independente da velocidade de deslocamento (°/s).")]
        private float rotationSpeed = 360f;

        private IPEGAPlanarMovementIntentSource _intentSource;
        private MonoBehaviour _intentSourceBehaviour;
        private CharacterController _characterController;
        private Vector3 _moveDirection;
        private float _moveMagnitude;
        private Vector3 _currentVelocity;
        private bool _isConfigured;
        private ScopedLogger _log;
        private bool _hasObservedIntent;
        private bool _lastLoggedMoving;
        private Vector3 _lastLoggedDirection;
        private float _lastLoggedMagnitude;
        private bool _hasObservedPhysicalMotion;
        private bool _lastObservedPhysicalMotion;

        public Vector3 CurrentPlanarVelocity => _currentVelocity;

        public bool HasCurrentMoveIntent => _isConfigured && HasValidMoveInput();

        public Vector3 CurrentMoveDirection => HasCurrentMoveIntent ? _moveDirection : Vector3.zero;

        private void Awake()
        {
            _log = PEGALogging.CreateScopedLogger<PEGAActorMovementMechanic>(
                LogCategory,
                gameObject.name);
            _intentSourceBehaviour = intentSource;
            _intentSource = intentSource as IPEGAPlanarMovementIntentSource;
            _characterController = GetComponent<CharacterController>();
            _isConfigured = ValidateConfiguration();
            if (!_isConfigured)
            {
                enabled = false;
                return;
            }

            _log.Info(
                "Shared movement executor configured.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("mechanic", nameof(PEGAActorMovementMechanic)),
                LogFields.Field("intentSource", _intentSourceBehaviour.GetType().FullName),
                LogFields.Field("maxSpeedMps", maxSpeed),
                LogFields.Field("accelerationMps2", acceleration),
                LogFields.Field("decelerationMps2", deceleration),
                LogFields.Field("minimumTurnAnalysisSpeedMps", minimumTurnAnalysisSpeed),
                LogFields.Field("closedCurveStartAngleDegrees", closedCurveStartAngleDegrees),
                LogFields.Field("closedCurveFullAngleDegrees", closedCurveFullAngleDegrees),
                LogFields.Field("reversalStartAngleDegrees", reversalStartAngleDegrees),
                LogFields.Field("smoothCurveResponseAccelerationMps2", smoothCurveResponseAcceleration),
                LogFields.Field("closedCurveResponseAccelerationMps2", closedCurveResponseAcceleration),
                LogFields.Field("reversalResponseAccelerationMps2", reversalResponseAcceleration),
                LogFields.Field("closedCurveSpeedRetention", closedCurveSpeedRetention),
                LogFields.Field("reversalBrakingAccelerationMps2", reversalBrakingAcceleration),
                LogFields.Field("rotationSpeedDegreesPerSecond", rotationSpeed));
        }

        private void Update()
        {
            if (!_isConfigured)
            {
                return;
            }

            if (!_intentSourceBehaviour.isActiveAndEnabled ||
                !_intentSource.TryGetIntent(Time.deltaTime, out PEGAPlanarMovementIntent intent))
            {
                _moveDirection = Vector3.zero;
                _moveMagnitude = 0f;
                _currentVelocity = Vector3.zero;
                LogIntentState(PEGAPlanarMovementIntent.Zero, false);
                LogPhysicalMotionState();
                return;
            }

            _moveDirection = intent.Direction;
            _moveMagnitude = intent.Magnitude;
            MoveActor(Time.deltaTime);
            LogIntentState(intent, true);
            LogPhysicalMotionState();
        }

        private void MoveActor(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 targetVelocity = _moveDirection * (_moveMagnitude * maxSpeed);
            Vector3 requestedVelocity = CalculateRequestedVelocity(
                targetVelocity,
                deltaTime);
            RotateActorTowardInput(deltaTime);

            Vector3 previousPosition = transform.position;
            _characterController.Move(requestedVelocity * deltaTime);

            Vector3 actualDisplacement = transform.position - previousPosition;
            actualDisplacement.y = 0f;
            _currentVelocity = Vector3.ClampMagnitude(
                actualDisplacement / deltaTime,
                maxSpeed);
        }

        private void RotateActorTowardInput(float deltaTime)
        {
            Vector3 desiredDirection = _moveDirection;
            if (!HasValidMoveInput())
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(
                desiredDirection,
                Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * deltaTime);
        }

        private Vector3 CalculateRequestedVelocity(Vector3 targetVelocity, float deltaTime)
        {
            Vector3 planarVelocity = _currentVelocity;
            planarVelocity.y = 0f;

            float currentSpeed = planarVelocity.magnitude;
            float targetSpeed = targetVelocity.magnitude;

            if (!HasValidMoveInput())
            {
                return Vector3.MoveTowards(
                    planarVelocity,
                    Vector3.zero,
                    deceleration * deltaTime);
            }

            if (currentSpeed <= minimumTurnAnalysisSpeed)
            {
                return Vector3.MoveTowards(
                    planarVelocity,
                    targetVelocity,
                    acceleration * deltaTime);
            }

            Vector3 desiredDirection = _moveDirection;
            float angleDegrees = Vector3.Angle(planarVelocity, desiredDirection);
            float smoothCurveWeight = CalculateAngleTransitionWeight(
                0f,
                closedCurveStartAngleDegrees,
                angleDegrees);
            float closedCurveWeight = CalculateAngleTransitionWeight(
                closedCurveStartAngleDegrees,
                closedCurveFullAngleDegrees,
                angleDegrees);
            float reversalWeight = CalculateAngleTransitionWeight(
                reversalStartAngleDegrees,
                MaximumPlanarTurnAngleDegrees,
                angleDegrees);

            float normalResponseAcceleration = targetSpeed < currentSpeed
                ? deceleration
                : acceleration;
            float responseAcceleration = Mathf.Lerp(
                normalResponseAcceleration,
                smoothCurveResponseAcceleration,
                smoothCurveWeight);
            responseAcceleration = Mathf.Lerp(
                responseAcceleration,
                closedCurveResponseAcceleration,
                closedCurveWeight);
            responseAcceleration = Mathf.Lerp(
                responseAcceleration,
                reversalResponseAcceleration,
                reversalWeight);

            float targetSpeedRetention = Mathf.Lerp(
                1f,
                closedCurveSpeedRetention,
                closedCurveWeight);
            float retainedTargetSpeed = Mathf.Min(
                targetSpeed,
                maxSpeed * targetSpeedRetention);
            Vector3 retainedTargetVelocity = desiredDirection * retainedTargetSpeed;

            float opposingSpeed = Mathf.Max(
                0f,
                -Vector3.Dot(planarVelocity, desiredDirection));
            float reversalBrakeStep = Mathf.Min(
                opposingSpeed,
                reversalBrakingAcceleration * reversalWeight * deltaTime);
            Vector3 velocityAfterReversalBraking =
                planarVelocity + desiredDirection * reversalBrakeStep;

            Vector3 requestedVelocity = Vector3.MoveTowards(
                velocityAfterReversalBraking,
                retainedTargetVelocity,
                responseAcceleration * deltaTime);
            return Vector3.ClampMagnitude(requestedVelocity, maxSpeed);
        }

        private bool HasValidMoveInput() =>
            _moveMagnitude > InputMagnitudeThreshold &&
            _moveDirection.sqrMagnitude > InputMagnitudeThreshold * InputMagnitudeThreshold;

        private static float CalculateAngleTransitionWeight(
            float startAngleDegrees,
            float endAngleDegrees,
            float angleDegrees)
        {
            float normalizedAngle = Mathf.InverseLerp(
                startAngleDegrees,
                endAngleDegrees,
                angleDegrees);
            return Mathf.SmoothStep(0f, 1f, normalizedAngle);
        }

        private bool ValidateConfiguration()
        {
            if (intentSource == null)
            {
                _log.Error(
                    $"{nameof(PEGAActorMovementMechanic)} requires an explicitly assigned PEGA movement intent source on the Actor root.",
                    LogFields.Field("actorRoot", gameObject.name));
                return false;
            }

            if (_intentSource == null || intentSource.gameObject != gameObject)
            {
                _log.Error(
                    $"{nameof(PEGAActorMovementMechanic)} requires an intent source implementing {nameof(IPEGAPlanarMovementIntentSource)} on the same Actor root.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("intentSource", intentSource.GetType().FullName));
                return false;
            }

            if (_characterController == null)
            {
                _log.Error(
                    $"{nameof(PEGAActorMovementMechanic)} requires {nameof(CharacterController)} on the Actor root.",
                    LogFields.Field("actorRoot", gameObject.name));
                return false;
            }

            if (!IsPositiveFinite(maxSpeed) ||
                !IsPositiveFinite(acceleration) ||
                !IsPositiveFinite(deceleration) ||
                !IsPositiveFinite(minimumTurnAnalysisSpeed) ||
                minimumTurnAnalysisSpeed >= maxSpeed ||
                !IsFinite(closedCurveStartAngleDegrees) ||
                closedCurveStartAngleDegrees <= 0f ||
                !IsFinite(closedCurveFullAngleDegrees) ||
                closedCurveFullAngleDegrees <= closedCurveStartAngleDegrees ||
                !IsFinite(reversalStartAngleDegrees) ||
                reversalStartAngleDegrees <= closedCurveFullAngleDegrees ||
                reversalStartAngleDegrees >= MaximumPlanarTurnAngleDegrees ||
                !IsPositiveFinite(smoothCurveResponseAcceleration) ||
                !IsPositiveFinite(closedCurveResponseAcceleration) ||
                !IsPositiveFinite(reversalResponseAcceleration) ||
                !IsPositiveFinite(closedCurveSpeedRetention) ||
                closedCurveSpeedRetention > 1f ||
                !IsNonNegativeFinite(reversalBrakingAcceleration) ||
                !IsPositiveFinite(rotationSpeed))
            {
                _log.Error(
                    $"{nameof(PEGAActorMovementMechanic)} requires valid movement, ordered angle thresholds, directional response, reversal braking and orientation settings.",
                    LogFields.Field("actorRoot", gameObject.name));
                return false;
            }

            return true;
        }

        private void LogIntentState(PEGAPlanarMovementIntent intent, bool sourceActive)
        {
            bool isMoving = sourceActive && intent.Magnitude > InputMagnitudeThreshold;
            bool shouldLog = !_hasObservedIntent || isMoving != _lastLoggedMoving;
            bool materiallyChanged = isMoving && _lastLoggedMoving &&
                (Vector3.Angle(_lastLoggedDirection, intent.Direction) >= IntentDirectionLogThresholdDegrees ||
                 Mathf.Abs(_lastLoggedMagnitude - intent.Magnitude) >= IntentMagnitudeLogThreshold);

            if (!shouldLog && !materiallyChanged)
            {
                return;
            }

            if (!_hasObservedIntent)
            {
                _hasObservedIntent = true;
                _lastLoggedMoving = isMoving;
                _lastLoggedDirection = intent.Direction;
                _lastLoggedMagnitude = intent.Magnitude;
                if (isMoving)
                {
                    _log.Info(
                        "Shared movement executor applied movement intent.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("mechanic", nameof(PEGAActorMovementMechanic)),
                        LogFields.Field("intentSource", _intentSourceBehaviour.GetType().FullName),
                        LogFields.Field("intentMagnitude", intent.Magnitude),
                        LogFields.Field("intentDirectionX", intent.Direction.x),
                        LogFields.Field("intentDirectionZ", intent.Direction.z),
                        LogFields.Field("actualVelocityX", _currentVelocity.x),
                        LogFields.Field("actualVelocityZ", _currentVelocity.z));
                }

                return;
            }

            if (isMoving != _lastLoggedMoving)
            {
                _lastLoggedMoving = isMoving;
                _lastLoggedDirection = intent.Direction;
                _lastLoggedMagnitude = intent.Magnitude;
                if (isMoving)
                {
                    _log.Info(
                        "Shared movement executor resumed receiving movement intent.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("mechanic", nameof(PEGAActorMovementMechanic)),
                        LogFields.Field("intentSource", _intentSourceBehaviour.GetType().FullName),
                        LogFields.Field("intentMagnitude", intent.Magnitude),
                        LogFields.Field("intentDirectionX", intent.Direction.x),
                        LogFields.Field("intentDirectionZ", intent.Direction.z),
                        LogFields.Field("actualVelocityX", _currentVelocity.x),
                        LogFields.Field("actualVelocityZ", _currentVelocity.z));
                }
                else
                {
                    _log.Info(
                        "Shared movement executor stopped receiving movement intent.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("mechanic", nameof(PEGAActorMovementMechanic)),
                        LogFields.Field("intentSource", _intentSourceBehaviour.GetType().FullName),
                        LogFields.Field("sourceActive", sourceActive),
                        LogFields.Field("actualVelocityX", _currentVelocity.x),
                        LogFields.Field("actualVelocityZ", _currentVelocity.z));
                }

                return;
            }

            _lastLoggedDirection = intent.Direction;
            _lastLoggedMagnitude = intent.Magnitude;
            _log.Debug(
                "Shared movement executor applied a materially changed intent.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("intentSource", _intentSourceBehaviour.GetType().FullName),
                LogFields.Field("intentMagnitude", intent.Magnitude),
                LogFields.Field("intentDirectionX", intent.Direction.x),
                LogFields.Field("intentDirectionZ", intent.Direction.z),
                LogFields.Field("actualVelocityX", _currentVelocity.x),
                LogFields.Field("actualVelocityZ", _currentVelocity.z));
        }

        private void LogPhysicalMotionState()
        {
            bool isMoving = _currentVelocity.sqrMagnitude >
                PhysicalMotionLogThresholdSpeed * PhysicalMotionLogThresholdSpeed;

            if (!_hasObservedPhysicalMotion)
            {
                _hasObservedPhysicalMotion = true;
                _lastObservedPhysicalMotion = isMoving;
                if (!isMoving)
                {
                    return;
                }
            }
            else if (isMoving == _lastObservedPhysicalMotion)
            {
                return;
            }
            else
            {
                _lastObservedPhysicalMotion = isMoving;
            }

            _log.Info(
                isMoving
                    ? "Actor movement began under the shared executor."
                    : "Actor reached rest under the shared executor.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("mechanic", nameof(PEGAActorMovementMechanic)),
                LogFields.Field("intentSource", _intentSourceBehaviour.GetType().FullName),
                LogFields.Field("planarSpeedMps", _currentVelocity.magnitude),
                LogFields.Field("velocityX", _currentVelocity.x),
                LogFields.Field("velocityZ", _currentVelocity.z),
                LogFields.Field("restThresholdMps", PhysicalMotionLogThresholdSpeed));
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

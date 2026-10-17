using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.Actors
{
    [DisallowMultipleComponent]
    public sealed class PEGAActorMovementAnimationPresenter : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.Movement";

        private enum LocomotionState
        {
            Idle,
            Walk,
            Run
        }

        private static readonly int WalkParameter = Animator.StringToHash("Walk");
        private static readonly int RunParameter = Animator.StringToHash("Run");

        [Header("Locomotion Presentation")]
        [SerializeField, Min(0f)]
        [Tooltip("Velocidade planar abaixo da qual o visual apresenta Idle (m/s).")]
        private float idleSpeedThresholdMetersPerSecond = 0.1f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Velocidade planar que inicia a apresentação Run ao acelerar a partir de Walk (m/s).")]
        private float runSpeedThresholdMetersPerSecond = 3.5f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Velocidade planar abaixo da qual a apresentação retorna de Run para Walk (m/s). Deve ser maior que o limiar de Idle e menor que o limiar de entrada em Run.")]
        private float runExitSpeedThresholdMetersPerSecond = 3.25f;

        private Animator _animator;
        private PEGAActorMovementMechanic _movementMechanic;
        private LocomotionState _currentState;
        private bool _hasCurrentState;
        private ScopedLogger _log;

        private void Awake()
        {
            _log = PEGALogging.CreateScopedLogger<PEGAActorMovementAnimationPresenter>(
                LogCategory,
                gameObject.name);
            _animator = GetComponentInChildren<Animator>(true);
            _movementMechanic = GetComponentInParent<PEGAActorMovementMechanic>();

            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _log.Info(
                "Shared movement animation presenter configured.",
                LogFields.Field("visualRoot", gameObject.name),
                LogFields.Field("actorRoot", _movementMechanic.gameObject.name),
                LogFields.Field("animator", _animator.name),
                LogFields.Field("idleSpeedThresholdMps", idleSpeedThresholdMetersPerSecond),
                LogFields.Field("runSpeedThresholdMps", runSpeedThresholdMetersPerSecond),
                LogFields.Field("runExitSpeedThresholdMps", runExitSpeedThresholdMetersPerSecond),
                LogFields.Field("rootMotion", _animator.applyRootMotion));
            ApplyState(LocomotionState.Idle, 0f);
        }

        private void LateUpdate()
        {
            if (_movementMechanic == null || !_movementMechanic.isActiveAndEnabled)
            {
                ApplyState(LocomotionState.Idle, 0f);
                return;
            }

            Vector3 planarVelocity = _movementMechanic.CurrentPlanarVelocity;
            float planarSpeed = new Vector2(planarVelocity.x, planarVelocity.z).magnitude;
            LocomotionState nextState = ResolveState(planarSpeed);
            ApplyState(nextState, planarSpeed);
        }

        private bool ValidateConfiguration()
        {
            if (_animator == null)
            {
                _log.Error(
                    $"{nameof(PEGAActorMovementAnimationPresenter)} requires an Animator in this visual hierarchy.",
                    LogFields.Field("visualRoot", gameObject.name));
                return false;
            }

            if (_movementMechanic == null)
            {
                _log.Error(
                    $"{nameof(PEGAActorMovementAnimationPresenter)} requires a parent {nameof(PEGAActorMovementMechanic)}.",
                    LogFields.Field("visualRoot", gameObject.name));
                return false;
            }

            if (!IsFiniteNonNegative(idleSpeedThresholdMetersPerSecond) ||
                !IsFinitePositive(runSpeedThresholdMetersPerSecond) ||
                !IsFinitePositive(runExitSpeedThresholdMetersPerSecond) ||
                runExitSpeedThresholdMetersPerSecond <= idleSpeedThresholdMetersPerSecond ||
                runExitSpeedThresholdMetersPerSecond >= runSpeedThresholdMetersPerSecond)
            {
                _log.Error(
                    $"{nameof(PEGAActorMovementAnimationPresenter)} requires thresholds ordered as Idle < Run exit < Run entry, with non-negative Idle and positive Run thresholds.",
                    LogFields.Field("visualRoot", gameObject.name),
                    LogFields.Field("idleSpeedThresholdMps", idleSpeedThresholdMetersPerSecond),
                    LogFields.Field("runExitSpeedThresholdMps", runExitSpeedThresholdMetersPerSecond),
                    LogFields.Field("runSpeedThresholdMps", runSpeedThresholdMetersPerSecond));
                return false;
            }

            if (_animator.applyRootMotion)
            {
                _log.Error(
                    $"{nameof(PEGAActorMovementAnimationPresenter)} requires Animator Root Motion to be disabled; physical movement belongs to {nameof(PEGAActorMovementMechanic)}.",
                    LogFields.Field("visualRoot", gameObject.name),
                    LogFields.Field("animator", _animator.name));
                return false;
            }

            if (!HasBooleanParameter(WalkParameter) || !HasBooleanParameter(RunParameter))
            {
                _log.Error(
                    $"{nameof(PEGAActorMovementAnimationPresenter)} requires Boolean Animator parameters 'Walk' and 'Run'.",
                    LogFields.Field("visualRoot", gameObject.name),
                    LogFields.Field("animator", _animator.name));
                return false;
            }

            return true;
        }

        private LocomotionState ResolveState(float planarSpeed)
        {
            if (planarSpeed <= idleSpeedThresholdMetersPerSecond)
            {
                return LocomotionState.Idle;
            }

            if (_hasCurrentState &&
                _currentState == LocomotionState.Run &&
                planarSpeed >= runExitSpeedThresholdMetersPerSecond)
            {
                return LocomotionState.Run;
            }

            return planarSpeed >= runSpeedThresholdMetersPerSecond
                ? LocomotionState.Run
                : LocomotionState.Walk;
        }

        private void ApplyState(LocomotionState state, float planarSpeed)
        {
            if (_hasCurrentState && _currentState == state)
            {
                return;
            }

            _animator.SetBool(WalkParameter, state == LocomotionState.Walk);
            _animator.SetBool(RunParameter, state == LocomotionState.Run);
            _currentState = state;
            _hasCurrentState = true;

            _log.Info(
                "Movement presentation state changed.",
                LogFields.Field(
                    "actorRoot",
                    _movementMechanic != null ? _movementMechanic.gameObject.name : "unavailable"),
                LogFields.Field("visualRoot", gameObject.name),
                LogFields.Field("state", state.ToString()),
                LogFields.Field("planarSpeedMps", planarSpeed),
                LogFields.Field("idleSpeedThresholdMps", idleSpeedThresholdMetersPerSecond),
                LogFields.Field("runExitSpeedThresholdMps", runExitSpeedThresholdMetersPerSecond),
                LogFields.Field("runSpeedThresholdMps", runSpeedThresholdMetersPerSecond));
        }

        private bool HasBooleanParameter(int parameterHash)
        {
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.nameHash == parameterHash)
                {
                    return parameter.type == AnimatorControllerParameterType.Bool;
                }
            }

            return false;
        }

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}

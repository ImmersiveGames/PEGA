using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Actors;
using PEGA.Diagnostics;
using UnityEngine;
using UnityEngine.AI;

namespace PEGA.NPC
{
    public enum PEGANpcNavigationOutcome
    {
        Running = 0,
        Success = 10,
        Invalid = 20,
        Interrupted = 30,
        Cancelled = 40
    }

    public readonly struct PEGANpcNavigationSnapshot
    {
        internal PEGANpcNavigationSnapshot(
            long requestId,
            PEGANpcNavigationOutcome outcome,
            Vector3 targetPosition,
            bool hasPath,
            bool pathPending,
            NavMeshPathStatus pathStatus,
            bool isOnNavMesh,
            float routeLengthMeters,
            float effectiveSpeedMetersPerSecond,
            float actualDistanceToTargetMeters,
            float actualSpeedMetersPerSecond,
            float maximumUnsynchronizedPositionOffsetMeters,
            float maximumPositionReconciliationResidualMeters,
            string diagnostic)
        {
            RequestId = requestId;
            Outcome = outcome;
            TargetPosition = targetPosition;
            HasPath = hasPath;
            PathPending = pathPending;
            PathStatus = pathStatus;
            IsOnNavMesh = isOnNavMesh;
            RouteLengthMeters = routeLengthMeters;
            EffectiveSpeedMetersPerSecond = effectiveSpeedMetersPerSecond;
            ActualDistanceToTargetMeters = actualDistanceToTargetMeters;
            ActualSpeedMetersPerSecond = actualSpeedMetersPerSecond;
            MaximumUnsynchronizedPositionOffsetMeters = maximumUnsynchronizedPositionOffsetMeters;
            MaximumPositionReconciliationResidualMeters = maximumPositionReconciliationResidualMeters;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public long RequestId { get; }
        public PEGANpcNavigationOutcome Outcome { get; }
        public Vector3 TargetPosition { get; }
        public bool HasPath { get; }
        public bool PathPending { get; }
        public NavMeshPathStatus PathStatus { get; }
        public bool IsOnNavMesh { get; }
        public float RouteLengthMeters { get; }
        public float EffectiveSpeedMetersPerSecond { get; }
        public float ActualDistanceToTargetMeters { get; }
        public float ActualSpeedMetersPerSecond { get; }
        public float MaximumUnsynchronizedPositionOffsetMeters { get; }
        public float MaximumPositionReconciliationResidualMeters { get; }
        public string Diagnostic { get; }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [AddComponentMenu("PEGA/NPC/NavMesh Movement Intent Source")]
    public sealed class PEGANpcNavMeshMovementIntentSource : MonoBehaviour,
        IPEGAPlanarMovementIntentSource,
        IPEGAPlanarMovementFeedbackSink
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.Navigation";
        private const float PathDirectionThreshold = 0.0001f;

        [Header("Physical Reconciliation")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Divergência planar residual máxima após alinhar a simulação NavMesh à posição física do Actor (m). Acima dela, a Action termina como Invalid; nenhum Transform é teletransportado.")]
        private float maximumPositionDivergenceMeters = 0.5f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Tempo contínuo sem deslocamento físico suficiente antes de invalidar uma rota que ainda solicita movimento (s).")]
        private float blockedMovementTimeoutSeconds = 0.75f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Diferença planar a partir da qual a divergência simulada/física é registrada como aviso (m).")]
        private float positionDivergenceWarningMeters = 0.1f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Fração mínima do deslocamento solicitado que deve ser observada fisicamente para considerar que o Actor avançou.")]
        private float minimumPhysicalMovementRatio = 0.15f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Velocidade solicitada mínima para iniciar a medição de bloqueio físico (m/s).")]
        private float minimumRequestedSpeedForBlockDetection = 0.2f;

        private NavMeshAgent _agent;
        private PEGAActorMovementMechanic _movementMechanic;
        private CharacterController _characterController;
        private ScopedLogger _log;
        private long _requestSequence;
        private long _currentRequestId;
        private bool _hasActiveRequest;
        private PEGANpcNavigationOutcome _currentOutcome;
        private PEGAActorMovementSpeedRegime _speedRegime;
        private Vector3 _targetPosition;
        private float _effectiveSpeedMetersPerSecond;
        private float _blockedDurationSeconds;
        private float _maximumUnsynchronizedPositionOffsetMeters;
        private float _maximumPositionReconciliationResidualMeters;
        private float _routeLengthMeters;
        private bool _hasCachedRouteLength;
        private bool _warnedPositionDivergence;
        private bool _reportedUnsynchronizedPositionOffset;
        private bool _canCompleteWithoutPath;
        private string _diagnostic = string.Empty;
        private float _lastDesiredSpeedMetersPerSecond;

        public bool IsReady =>
            isActiveAndEnabled &&
            _agent != null &&
            _agent.enabled &&
            _movementMechanic != null &&
            _movementMechanic.isActiveAndEnabled &&
            _characterController != null &&
            _characterController.enabled;

        public float MaximumPositionReconciliationResidualMeters => maximumPositionDivergenceMeters;

        public float CurrentMaximumUnsynchronizedPositionOffsetMeters =>
            _maximumUnsynchronizedPositionOffsetMeters;

        public float CurrentMaximumPositionReconciliationResidualMeters =>
            _maximumPositionReconciliationResidualMeters;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _movementMechanic = GetComponent<PEGAActorMovementMechanic>();
            _characterController = GetComponent<CharacterController>();
            _log = PEGALogging.CreateScopedLogger<PEGANpcNavMeshMovementIntentSource>(
                LogCategory,
                gameObject.name);

            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            // A única autoridade de posição e orientação é a mecânica PEGA.
            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.isStopped = true;

            _log.Info(
                "NPC NavMesh route provider configured for externally executed movement.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("agentTypeId", _agent.agentTypeID),
                LogFields.Field("agentRadiusMeters", _agent.radius),
                LogFields.Field("agentHeightMeters", _agent.height),
                LogFields.Field("transformPositionAuthority", nameof(PEGAActorMovementMechanic)),
                LogFields.Field("characterController", _characterController != null && _characterController.enabled));
        }

        public bool TryBeginNavigation(
            Vector3 targetPosition,
            PEGAActorMovementSpeedRegime speedRegime,
            float arrivalRadiusMeters,
            out long requestId,
            out string diagnostic)
        {
            requestId = 0;
            diagnostic = string.Empty;

            if (!IsReady)
            {
                diagnostic = "NPC navigation source, NavMeshAgent and shared movement mechanic must be active.";
                return false;
            }

            if (_hasActiveRequest)
            {
                diagnostic = "This NPC already owns an active navigation request; a second Action cannot replace it.";
                return false;
            }

            if (!IsFinite(targetPosition) || !IsNonNegativeFinite(arrivalRadiusMeters))
            {
                diagnostic = "Navigation target and arrival radius must be finite; arrival radius cannot be negative.";
                return false;
            }

            if (!_agent.isOnNavMesh)
            {
                diagnostic = "NavMeshAgent is not placed on a baked NavMesh at the Actor's physical position.";
                return false;
            }

            if (!_movementMechanic.TryGetMaximumSpeed(
                    speedRegime,
                    out float effectiveSpeed,
                    out diagnostic))
            {
                return false;
            }

            Vector3 initialUnsynchronizedPosition = _agent.nextPosition;
            float initialUnsynchronizedOffset = PlanarDistance(initialUnsynchronizedPosition, transform.position);
            _agent.nextPosition = transform.position;
            float initialResidual = PlanarDistance(_agent.nextPosition, transform.position);
            if (initialResidual > maximumPositionDivergenceMeters)
            {
                diagnostic = "NavMesh simulation could not be reconciled with the physical Actor position at request start.";
                return false;
            }

            _agent.speed = effectiveSpeed;
            _agent.stoppingDistance = arrivalRadiusMeters;
            _agent.isStopped = false;

            _requestSequence++;
            if (_requestSequence <= 0)
            {
                _agent.isStopped = true;
                diagnostic = "Navigation request sequence exhausted.";
                return false;
            }

            _currentRequestId = _requestSequence;
            _targetPosition = targetPosition;
            _speedRegime = speedRegime;
            _effectiveSpeedMetersPerSecond = effectiveSpeed;
            _blockedDurationSeconds = 0f;
            _maximumUnsynchronizedPositionOffsetMeters = initialUnsynchronizedOffset;
            _maximumPositionReconciliationResidualMeters = initialResidual;
            _hasCachedRouteLength = false;
            _routeLengthMeters = 0f;
            _warnedPositionDivergence = false;
            _reportedUnsynchronizedPositionOffset = false;
            _diagnostic = string.Empty;
            _currentOutcome = PEGANpcNavigationOutcome.Running;
            _canCompleteWithoutPath = PlanarDistance(transform.position, targetPosition) <= arrivalRadiusMeters;
            _hasActiveRequest = true;

            if (initialUnsynchronizedOffset >= positionDivergenceWarningMeters)
            {
                _reportedUnsynchronizedPositionOffset = true;
                LogPositionReconciliation(
                    initialUnsynchronizedPosition,
                    transform.position,
                    initialUnsynchronizedOffset,
                    initialResidual,
                    "request-start");
            }

            if (!_canCompleteWithoutPath && !_agent.SetDestination(targetPosition))
            {
                FinishRequest(
                    _currentRequestId,
                    PEGANpcNavigationOutcome.Invalid,
                    "NavMeshAgent rejected the requested destination.");
                requestId = _currentRequestId;
                diagnostic = _diagnostic;
                return false;
            }

            requestId = _currentRequestId;
            _log.Info(
                "NPC navigation request started.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("requestId", requestId),
                LogFields.Field("speedRegime", speedRegime),
                LogFields.Field("effectiveSpeedMps", effectiveSpeed),
                LogFields.Field("arrivalRadiusMeters", arrivalRadiusMeters),
                LogFields.Field("targetX", targetPosition.x),
                LogFields.Field("targetY", targetPosition.y),
                LogFields.Field("targetZ", targetPosition.z));
            return true;
        }

        public bool TryGetSnapshot(long requestId, out PEGANpcNavigationSnapshot snapshot)
        {
            snapshot = default;
            if (requestId <= 0 || requestId != _currentRequestId)
            {
                return false;
            }

            if (_hasActiveRequest && !_agent.isOnNavMesh)
            {
                FinishRequest(
                    requestId,
                    PEGANpcNavigationOutcome.Invalid,
                    "NavMeshAgent lost its baked NavMesh while the CharacterController was moving the Actor.");
            }

            bool pathPending = _hasActiveRequest && _agent.pathPending;
            bool hasPath = _hasActiveRequest && _agent.hasPath;
            NavMeshPathStatus pathStatus = _hasActiveRequest
                ? _agent.pathStatus
                : NavMeshPathStatus.PathInvalid;

            if (_hasActiveRequest && !pathPending && !_canCompleteWithoutPath &&
                (!hasPath || pathStatus != NavMeshPathStatus.PathComplete))
            {
                FinishRequest(
                    requestId,
                    PEGANpcNavigationOutcome.Invalid,
                    $"NavMesh path is not complete ({pathStatus}).");
                pathPending = false;
                hasPath = false;
                pathStatus = NavMeshPathStatus.PathInvalid;
            }

            if (_hasActiveRequest && !pathPending && hasPath && !_hasCachedRouteLength)
            {
                _routeLengthMeters = CalculatePathLength(_agent.path.corners);
                _hasCachedRouteLength = true;
                _log.Info(
                    "NPC NavMesh path became available.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("requestId", requestId),
                    LogFields.Field("pathStatus", pathStatus),
                    LogFields.Field("routeLengthMeters", _routeLengthMeters),
                    LogFields.Field("effectiveSpeedMps", _effectiveSpeedMetersPerSecond));
            }

            float actualDistance = PlanarDistance(transform.position, _targetPosition);
            float actualSpeed = _movementMechanic != null
                ? _movementMechanic.CurrentPlanarVelocity.magnitude
                : 0f;

            snapshot = new PEGANpcNavigationSnapshot(
                requestId,
                _currentOutcome,
                _targetPosition,
                hasPath,
                pathPending,
                pathStatus,
                _agent != null && _agent.isOnNavMesh,
                _routeLengthMeters,
                _effectiveSpeedMetersPerSecond,
                actualDistance,
                actualSpeed,
                _maximumUnsynchronizedPositionOffsetMeters,
                _maximumPositionReconciliationResidualMeters,
                _diagnostic);
            return true;
        }

        public void FinishRequest(
            long requestId,
            PEGANpcNavigationOutcome outcome,
            string diagnostic)
        {
            if (requestId <= 0 || requestId != _currentRequestId || !_hasActiveRequest)
            {
                return;
            }

            if (outcome == PEGANpcNavigationOutcome.Running)
            {
                return;
            }

            _hasActiveRequest = false;
            _currentOutcome = outcome;
            _diagnostic = diagnostic ?? string.Empty;
            _blockedDurationSeconds = 0f;

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
                _agent.nextPosition = transform.position;
            }
        }

        bool IPEGAPlanarMovementIntentSource.TryGetIntent(
            float deltaTime,
            out PEGAPlanarMovementIntent intent)
        {
            intent = PEGAPlanarMovementIntent.Zero;
            if (!IsReady)
            {
                return false;
            }

            if (!_hasActiveRequest)
            {
                return true;
            }

            if (!_agent.isOnNavMesh)
            {
                FinishRequest(
                    _currentRequestId,
                    PEGANpcNavigationOutcome.Invalid,
                    "NavMeshAgent is no longer attached to the baked NavMesh.");
                return true;
            }

            // The NavMesh simulation advances independently while updatePosition is disabled.
            // Reconcile it to the last physical position before checking the residual and
            // sampling steering, so a transient offset cannot invalidate the request first.
            if (!ReconcileAgentPosition(transform.position, "before-physical-move", true))
            {
                return true;
            }

            Vector3 desiredVelocity = _agent.desiredVelocity;
            desiredVelocity.y = 0f;
            if (!IsFinite(desiredVelocity))
            {
                FinishRequest(
                    _currentRequestId,
                    PEGANpcNavigationOutcome.Invalid,
                    "NavMeshAgent produced a non-finite desired velocity.");
                return true;
            }

            _lastDesiredSpeedMetersPerSecond = desiredVelocity.magnitude;
            float magnitude = Mathf.Clamp01(
                _lastDesiredSpeedMetersPerSecond / _effectiveSpeedMetersPerSecond);
            intent = _lastDesiredSpeedMetersPerSecond <= PathDirectionThreshold
                ? new PEGAPlanarMovementIntent(
                    Vector3.zero,
                    0f,
                    _speedRegime)
                : new PEGAPlanarMovementIntent(
                    desiredVelocity,
                    magnitude,
                    _speedRegime);
            return true;
        }

        void IPEGAPlanarMovementFeedbackSink.OnMovementExecuted(
            Vector3 previousPosition,
            Vector3 actualPosition,
            Vector3 requestedVelocity,
            float deltaTime)
        {
            if (!_hasActiveRequest || deltaTime <= 0f)
            {
                return;
            }

            // CharacterController.Move has now produced the authoritative position for this
            // frame. Reconcile immediately so the next NavMesh simulation step starts there.
            if (!ReconcileAgentPosition(actualPosition, "after-physical-move", false))
            {
                return;
            }

            Vector3 actualDisplacement = actualPosition - previousPosition;
            actualDisplacement.y = 0f;
            float requestedSpeed = requestedVelocity.magnitude;
            float actualDistance = actualDisplacement.magnitude;

            if (_lastDesiredSpeedMetersPerSecond >= minimumRequestedSpeedForBlockDetection &&
                requestedSpeed >= minimumRequestedSpeedForBlockDetection &&
                actualDistance < requestedSpeed * deltaTime * minimumPhysicalMovementRatio)
            {
                _blockedDurationSeconds += deltaTime;
            }
            else
            {
                _blockedDurationSeconds = 0f;
            }

            if (_blockedDurationSeconds >= blockedMovementTimeoutSeconds)
            {
                FinishRequest(
                    _currentRequestId,
                    PEGANpcNavigationOutcome.Invalid,
                    "NavMesh requested movement but CharacterController displacement remained below the configured physical progress threshold.");
                _log.Warning(
                    "NPC navigation invalidated after physical movement stalled.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("requestId", _currentRequestId),
                    LogFields.Field("blockedDurationSeconds", _blockedDurationSeconds),
                    LogFields.Field("requestedSpeedMps", requestedSpeed),
                    LogFields.Field("actualDisplacementMeters", actualDistance));
            }
        }

        private bool ValidateConfiguration()
        {
            if (_agent == null || _movementMechanic == null || _characterController == null)
            {
                _log.Error(
                    $"{nameof(PEGANpcNavMeshMovementIntentSource)} requires {nameof(NavMeshAgent)}, {nameof(PEGAActorMovementMechanic)} and {nameof(CharacterController)} on the same Actor root.",
                    LogFields.Field("actorRoot", gameObject.name));
                return false;
            }

            if (!IsPositiveFinite(maximumPositionDivergenceMeters) ||
                !IsPositiveFinite(blockedMovementTimeoutSeconds) ||
                !IsPositiveFinite(positionDivergenceWarningMeters) ||
                positionDivergenceWarningMeters > maximumPositionDivergenceMeters ||
                !IsFinite(minimumPhysicalMovementRatio) ||
                minimumPhysicalMovementRatio <= 0f || minimumPhysicalMovementRatio > 1f ||
                !IsPositiveFinite(minimumRequestedSpeedForBlockDetection))
            {
                _log.Error(
                    $"{nameof(PEGANpcNavMeshMovementIntentSource)} has invalid reconciliation or physical-progress thresholds.",
                    LogFields.Field("actorRoot", gameObject.name));
                return false;
            }

            if (!IsPositiveFinite(_agent.radius) || !IsPositiveFinite(_agent.height))
            {
                _log.Error(
                    $"{nameof(NavMeshAgent)} requires positive radius and height.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("radiusMeters", _agent.radius),
                    LogFields.Field("heightMeters", _agent.height));
                return false;
            }

            return true;
        }

        private void RecordPositionReconciliationResidual(float residualMeters, string stage)
        {
            _maximumPositionReconciliationResidualMeters = Mathf.Max(
                _maximumPositionReconciliationResidualMeters,
                residualMeters);

            if (residualMeters > maximumPositionDivergenceMeters)
            {
                LogPositionReconciliationFailure(residualMeters, stage);
                FinishRequest(
                    _currentRequestId,
                    PEGANpcNavigationOutcome.Invalid,
                    "NavMesh simulation could not reconcile to the physical CharacterController position within the configured residual tolerance.");
                return;
            }

            if (residualMeters >= positionDivergenceWarningMeters && !_warnedPositionDivergence)
            {
                _warnedPositionDivergence = true;
                LogPositionReconciliationResidualWarning(residualMeters, stage);
            }
            else if (residualMeters < positionDivergenceWarningMeters)
            {
                _warnedPositionDivergence = false;
            }
        }

        private bool ReconcileAgentPosition(
            Vector3 physicalPosition,
            string stage,
            bool reportUnsynchronizedOffset)
        {
            if (!_agent.isOnNavMesh)
            {
                FinishRequest(
                    _currentRequestId,
                    PEGANpcNavigationOutcome.Invalid,
                    $"NavMeshAgent left the baked NavMesh during position reconciliation ({stage}).");
                return false;
            }

            Vector3 unsynchronizedPosition = _agent.nextPosition;
            float unsynchronizedOffset = PlanarDistance(unsynchronizedPosition, physicalPosition);
            _maximumUnsynchronizedPositionOffsetMeters = Mathf.Max(
                _maximumUnsynchronizedPositionOffsetMeters,
                unsynchronizedOffset);
            _agent.nextPosition = physicalPosition;
            float residualDivergence = PlanarDistance(_agent.nextPosition, physicalPosition);

            if (reportUnsynchronizedOffset)
            {
                if (unsynchronizedOffset >= positionDivergenceWarningMeters &&
                    !_reportedUnsynchronizedPositionOffset)
                {
                    _reportedUnsynchronizedPositionOffset = true;
                    LogPositionReconciliation(
                        unsynchronizedPosition,
                        physicalPosition,
                        unsynchronizedOffset,
                        residualDivergence,
                        stage);
                }
            }

            RecordPositionReconciliationResidual(residualDivergence, stage);
            return _hasActiveRequest;
        }

        private void LogPositionReconciliation(
            Vector3 unsynchronizedPosition,
            Vector3 physicalPosition,
            float unsynchronizedOffsetMeters,
            float residualDivergenceMeters,
            string stage)
        {
            _log.Info(
                "NPC NavMesh simulation position was reconciled to the physical Actor position.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("requestId", _currentRequestId),
                LogFields.Field("stage", stage),
                LogFields.Field("unsynchronizedOffsetMeters", unsynchronizedOffsetMeters),
                LogFields.Field("residualDivergenceMeters", residualDivergenceMeters),
                LogFields.Field("maximumAcceptedResidualMeters", maximumPositionDivergenceMeters),
                LogFields.Field("simulationXBeforeSync", unsynchronizedPosition.x),
                LogFields.Field("simulationZBeforeSync", unsynchronizedPosition.z),
                LogFields.Field("physicalX", physicalPosition.x),
                LogFields.Field("physicalZ", physicalPosition.z));
        }

        private void LogPositionReconciliationFailure(float residualMeters, string stage)
        {
            _log.Warning(
                "NPC NavMesh simulation could not reconcile with the physical Actor position within the configured residual.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("requestId", _currentRequestId),
                LogFields.Field("stage", stage),
                LogFields.Field("residualDivergenceMeters", residualMeters),
                LogFields.Field("maximumAcceptedResidualMeters", maximumPositionDivergenceMeters),
                LogFields.Field("simulationX", _agent != null ? _agent.nextPosition.x : 0f),
                LogFields.Field("simulationZ", _agent != null ? _agent.nextPosition.z : 0f),
                LogFields.Field("physicalX", transform.position.x),
                LogFields.Field("physicalZ", transform.position.z));
        }

        private void LogPositionReconciliationResidualWarning(float residualMeters, string stage)
        {
            _log.Warning(
                "NPC NavMesh position reconciliation left a measurable residual after synchronization.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("requestId", _currentRequestId),
                LogFields.Field("stage", stage),
                LogFields.Field("residualDivergenceMeters", residualMeters),
                LogFields.Field("warningThresholdMeters", positionDivergenceWarningMeters),
                LogFields.Field("maximumAcceptedResidualMeters", maximumPositionDivergenceMeters),
                LogFields.Field("simulationX", _agent != null ? _agent.nextPosition.x : 0f),
                LogFields.Field("simulationZ", _agent != null ? _agent.nextPosition.z : 0f),
                LogFields.Field("physicalX", transform.position.x),
                LogFields.Field("physicalZ", transform.position.z));
        }

        private static float CalculatePathLength(Vector3[] corners)
        {
            if (corners == null || corners.Length < 2)
            {
                return 0f;
            }

            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsPositiveFinite(float value) =>
            value > 0f && IsFinite(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && IsFinite(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

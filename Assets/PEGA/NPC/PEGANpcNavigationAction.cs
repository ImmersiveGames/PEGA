using System;
using Immersive.Foundation.Events;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Actors;
using PEGA.Diagnostics;
using UnityEngine;
using UnityEngine.AI;

namespace PEGA.NPC
{
    public enum PEGANpcActionOutcome
    {
        NotStarted = 0,
        Running = 10,
        Success = 20,
        Invalid = 30,
        Interrupted = 40,
        Cancelled = 50
    }

    public enum PEGANpcActionEffectKind
    {
        ApproachKnownRickPosition = 10,
        ReachSelectedEscapeDestination = 20,
        ReachLocalObservationPosition = 30
    }

    public readonly struct PEGANpcActionEffectDeclaration
    {
        internal PEGANpcActionEffectDeclaration(
            PEGANpcActionEffectKind kind,
            bool isTargetResolved,
            Vector3 targetPosition,
            float targetRadiusMeters)
        {
            Kind = kind;
            IsTargetResolved = isTargetResolved;
            TargetPosition = targetPosition;
            TargetRadiusMeters = targetRadiusMeters;
        }

        public PEGANpcActionEffectKind Kind { get; }
        public bool IsTargetResolved { get; }
        public Vector3 TargetPosition { get; }
        public float TargetRadiusMeters { get; }
    }

    public readonly struct PEGANpcObservedActionEffect
    {
        internal PEGANpcObservedActionEffect(
            PEGANpcActionEffectKind kind,
            PEGANpcDesireGoalKind goalKind,
            long requestId,
            Vector3 targetPosition,
            float targetRadiusMeters,
            float actualDistanceToTargetMeters,
            double observedAtSeconds)
        {
            Kind = kind;
            GoalKind = goalKind;
            RequestId = requestId;
            TargetPosition = targetPosition;
            TargetRadiusMeters = targetRadiusMeters;
            ActualDistanceToTargetMeters = actualDistanceToTargetMeters;
            ObservedAtSeconds = observedAtSeconds;
            IsValid = true;
        }

        public PEGANpcActionEffectKind Kind { get; }

        public PEGANpcDesireGoalKind GoalKind { get; }

        public long RequestId { get; }

        public Vector3 TargetPosition { get; }

        public float TargetRadiusMeters { get; }

        public float ActualDistanceToTargetMeters { get; }

        public double ObservedAtSeconds { get; }

        public bool IsValid { get; }
    }

    public readonly struct PEGANpcActionExecutionSnapshot
    {
        internal PEGANpcActionExecutionSnapshot(
            PEGANpcActionOutcome outcome,
            bool hasNavigationSnapshot,
            PEGANpcNavigationSnapshot navigation,
            PEGANpcActionEffectDeclaration declaredEffect,
            string diagnostic)
        {
            Outcome = outcome;
            HasNavigationSnapshot = hasNavigationSnapshot;
            DeclaredEffect = declaredEffect;
            EffectObserved = outcome == PEGANpcActionOutcome.Success;
            RequestId = navigation.RequestId;
            TargetPosition = hasNavigationSnapshot
                ? navigation.TargetPosition
                : declaredEffect.IsTargetResolved
                    ? declaredEffect.TargetPosition
                    : Vector3.zero;
            HasPath = navigation.HasPath;
            PathPending = navigation.PathPending;
            PathStatus = navigation.PathStatus;
            IsOnNavMesh = navigation.IsOnNavMesh;
            RouteLengthMeters = navigation.RouteLengthMeters;
            EffectiveSpeedMetersPerSecond = navigation.EffectiveSpeedMetersPerSecond;
            ActualDistanceToTargetMeters = navigation.ActualDistanceToTargetMeters;
            ActualSpeedMetersPerSecond = navigation.ActualSpeedMetersPerSecond;
            MaximumUnsynchronizedPositionOffsetMeters = navigation.MaximumUnsynchronizedPositionOffsetMeters;
            MaximumPositionReconciliationResidualMeters = navigation.MaximumPositionReconciliationResidualMeters;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public PEGANpcActionOutcome Outcome { get; }
        public bool HasNavigationSnapshot { get; }
        public PEGANpcActionEffectDeclaration DeclaredEffect { get; }
        public bool EffectObserved { get; }
        public long RequestId { get; }
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

    /// <summary>Expõe o snapshot final de uma execução de Action de navegação.</summary>
    public sealed class PEGANpcActionTerminalOutcomeEvent : IEvent
    {
        internal PEGANpcActionTerminalOutcomeEvent(
            PEGANpcActionExecutionSnapshot execution,
            double completedAtTimeSeconds)
        {
            Execution = execution;
            CompletedAtTimeSeconds = completedAtTimeSeconds;
        }

        public PEGANpcActionExecutionSnapshot Execution { get; }

        public double CompletedAtTimeSeconds { get; }
    }

    // Cada Action concreta impede sua própria duplicação; Actions irmãs precisam coexistir no mesmo Actor para o GOAP selecioná-las.
    [RequireComponent(typeof(PEGANpcNavMeshMovementIntentSource))]
    public abstract class PEGANpcNavigationAction : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.Actions";

        [SerializeField]
        [Tooltip("Fonte NavMesh do mesmo Actor, usada somente para solicitar uma rota e obter estado de execução.")]
        private PEGANpcNavMeshMovementIntentSource navigationSource;

        [SerializeField, Min(0f)]
        [Tooltip("Distância planar de chegada ao Target (m).")]
        private float arrivalRadiusMeters = 1.25f;

        private ScopedLogger _log;
        private long _requestId;
        private bool _hasNavigationSnapshot;
        private PEGANpcNavigationSnapshot _navigationSnapshot;
        private bool _hasEffectTarget;
        private Vector3 _effectTarget;
        private double _completedAtTimeSeconds;
        private string _diagnostic = string.Empty;

        [NonSerialized]
        private bool _loggerInitialized;

        [NonSerialized]
        private EventBus<PEGANpcActionTerminalOutcomeEvent> _terminalOutcomeEvents;

        protected abstract PEGANpcActionEffectKind ActionEffectKind { get; }

        protected abstract PEGANpcDesireGoalKind ActionGoalKind { get; }

        public PEGANpcActionOutcome Outcome { get; private set; } = PEGANpcActionOutcome.NotStarted;

        public PEGANpcActionExecutionSnapshot CurrentExecution => new PEGANpcActionExecutionSnapshot(
            Outcome,
            _hasNavigationSnapshot,
            _navigationSnapshot,
            DeclaredEffect,
            _diagnostic);

        /// <summary>Assina resultados terminais desta Action.</summary>
        /// <remarks>
        /// A entrega é síncrona e ocorre depois que o snapshot terminal foi gravado e registrado. Exceções de
        /// assinantes são registradas e isoladas para preservar o resultado e a entrega aos demais assinantes.
        /// </remarks>
        public IEventBinding SubscribeToTerminalOutcomes(Action<PEGANpcActionTerminalOutcomeEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureTerminalOutcomeEventsInitialized();
            return _terminalOutcomeEvents.Subscribe(outcome =>
            {
                try
                {
                    handler(outcome);
                }
                catch (Exception exception)
                {
                    LogSubscriberFailure(handler, outcome, exception);
                }
            });
        }

        public bool UnsubscribeFromTerminalOutcomes(IEventBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            return _terminalOutcomeEvents != null && _terminalOutcomeEvents.Unsubscribe(binding);
        }

        protected virtual void OnDestroy()
        {
            _terminalOutcomeEvents?.Clear();
            _terminalOutcomeEvents = null;
        }

        public PEGANpcActionEffectDeclaration DeclaredEffect => new PEGANpcActionEffectDeclaration(
            ActionEffectKind,
            _hasEffectTarget,
            _effectTarget,
            arrivalRadiusMeters);

        public bool TryGetObservedEffect(out PEGANpcObservedActionEffect observedEffect)
        {
            observedEffect = default;
            if (Outcome != PEGANpcActionOutcome.Success || !_hasNavigationSnapshot)
            {
                return false;
            }

            PEGANpcActionEffectDeclaration declaration = DeclaredEffect;
            float actualDistance = _navigationSnapshot.ActualDistanceToTargetMeters;
            if (!declaration.IsTargetResolved ||
                _navigationSnapshot.RequestId <= 0 ||
                !IsFinite(declaration.TargetPosition) ||
                !IsNonNegativeFinite(declaration.TargetRadiusMeters) ||
                !IsNonNegativeFinite(actualDistance) ||
                actualDistance > declaration.TargetRadiusMeters ||
                (_navigationSnapshot.TargetPosition - declaration.TargetPosition).sqrMagnitude > 0.000001f)
            {
                return false;
            }

            observedEffect = new PEGANpcObservedActionEffect(
                declaration.Kind,
                ActionGoalKind,
                _navigationSnapshot.RequestId,
                _navigationSnapshot.TargetPosition,
                declaration.TargetRadiusMeters,
                actualDistance,
                _completedAtTimeSeconds);
            return true;
        }

        public float IntrinsicDurationSeconds => 0f;

        protected float ArrivalRadiusMeters => arrivalRadiusMeters;

        protected bool TryEstimateNavigationRoute(
            Vector3 targetPosition,
            PEGAActorMovementSpeedRegime speedRegime,
            out PEGANpcNavigationRouteEstimate estimate,
            out string diagnostic)
        {
            estimate = default;
            if (!isActiveAndEnabled || navigationSource == null ||
                navigationSource.gameObject != gameObject || !navigationSource.IsReady)
            {
                diagnostic = "The Action requires an active NavMesh movement source on the same NPC root.";
                return false;
            }

            return navigationSource.TryEstimateRoute(
                targetPosition,
                speedRegime,
                arrivalRadiusMeters,
                out estimate,
                out diagnostic);
        }

        protected virtual bool ArePreconditionsStillValid(out string diagnostic)
        {
            diagnostic = string.Empty;
            return true;
        }

        protected virtual bool IsExpectedPreconditionInvalidation() => false;

        protected virtual void Awake()
        {
            EnsureLoggerInitialized();
        }

        protected virtual void OnDisable()
        {
            if (Outcome == PEGANpcActionOutcome.Running)
            {
                Terminate(
                    PEGANpcActionOutcome.Cancelled,
                    "Action component was disabled while executing.",
                    PEGANpcNavigationOutcome.Cancelled);
            }
        }

        protected bool TryStartNavigation(
            Vector3 targetPosition,
            PEGAActorMovementSpeedRegime speedRegime,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (Outcome == PEGANpcActionOutcome.Running)
            {
                diagnostic = "This Action already owns an active navigation request.";
                return false;
            }

            ResetExecution();
            _effectTarget = targetPosition;
            _hasEffectTarget = IsFinite(targetPosition);
            if (!isActiveAndEnabled || navigationSource == null ||
                navigationSource.gameObject != gameObject || !navigationSource.IsReady)
            {
                diagnostic = "The Action requires an active NavMesh movement source on the same NPC root.";
                CompleteWithoutNavigation(PEGANpcActionOutcome.Invalid, targetPosition, diagnostic);
                return false;
            }

            if (!IsNonNegativeFinite(arrivalRadiusMeters))
            {
                diagnostic = "Arrival radius must be finite and non-negative.";
                CompleteWithoutNavigation(PEGANpcActionOutcome.Invalid, targetPosition, diagnostic);
                return false;
            }

            bool started = navigationSource.TryBeginNavigation(
                targetPosition,
                speedRegime,
                arrivalRadiusMeters,
                out _requestId,
                out diagnostic);

            if (_requestId > 0 && navigationSource.TryGetSnapshot(
                    _requestId,
                    out _navigationSnapshot))
            {
                _hasNavigationSnapshot = true;
            }

            if (!started)
            {
                if (_requestId <= 0)
                {
                    CompleteWithoutNavigation(
                        PEGANpcActionOutcome.Invalid,
                        targetPosition,
                        string.IsNullOrEmpty(diagnostic)
                            ? "Navigation request was rejected."
                            : diagnostic);
                    return false;
                }

                Complete(
                    PEGANpcActionOutcome.Invalid,
                    string.IsNullOrEmpty(diagnostic)
                        ? "Navigation request was rejected."
                        : diagnostic,
                    PEGANpcNavigationOutcome.Invalid);
                return false;
            }

            Outcome = PEGANpcActionOutcome.Running;
            _diagnostic = string.Empty;
            _log.Debug(
                "NPC navigation Action started.",
                LogFields.Field("action", GetType().Name),
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("requestId", _requestId),
                LogFields.Field("intrinsicDurationSeconds", IntrinsicDurationSeconds),
                LogFields.Field("arrivalRadiusMeters", arrivalRadiusMeters),
                LogFields.Field("speedRegime", speedRegime),
                LogFields.Field("declaredEffect", ActionEffectKind),
                LogFields.Field("targetX", targetPosition.x),
                LogFields.Field("targetY", targetPosition.y),
                LogFields.Field("targetZ", targetPosition.z));
            return true;
        }

        protected void RejectStart(string diagnostic)
        {
            if (Outcome == PEGANpcActionOutcome.Running)
            {
                return;
            }

            ResetExecution();
            CompleteWithoutNavigation(
                PEGANpcActionOutcome.Invalid,
                Vector3.zero,
                string.IsNullOrEmpty(diagnostic) ? "Action preconditions are not satisfied." : diagnostic);
        }

        public bool CancelExecution(string diagnostic = "Action owner cancelled execution.") =>
            Terminate(PEGANpcActionOutcome.Cancelled, diagnostic, PEGANpcNavigationOutcome.Cancelled);

        public bool InterruptExecution(string diagnostic = "Configured event interrupted execution.") =>
            Terminate(PEGANpcActionOutcome.Interrupted, diagnostic, PEGANpcNavigationOutcome.Interrupted);

        protected virtual void Update()
        {
            if (Outcome != PEGANpcActionOutcome.Running)
            {
                return;
            }

            if (!ArePreconditionsStillValid(out string preconditionDiagnostic))
            {
                Complete(
                    PEGANpcActionOutcome.Invalid,
                    string.IsNullOrEmpty(preconditionDiagnostic)
                        ? "Action preconditions stopped being valid during execution."
                        : preconditionDiagnostic,
                    PEGANpcNavigationOutcome.Invalid,
                    IsExpectedPreconditionInvalidation());
                return;
            }

            if (navigationSource == null || !navigationSource.IsReady || !navigationSource.TryGetSnapshot(
                    _requestId,
                    out _navigationSnapshot))
            {
                Complete(
                    PEGANpcActionOutcome.Invalid,
                    "The active navigation request no longer has an observable snapshot.",
                    PEGANpcNavigationOutcome.Invalid);
                return;
            }

            _hasNavigationSnapshot = true;
            if (_navigationSnapshot.Outcome == PEGANpcNavigationOutcome.Invalid)
            {
                Complete(
                    PEGANpcActionOutcome.Invalid,
                    _navigationSnapshot.Diagnostic,
                    PEGANpcNavigationOutcome.Invalid);
                return;
            }

            if (_navigationSnapshot.Outcome != PEGANpcNavigationOutcome.Running)
            {
                PEGANpcActionOutcome outcome = _navigationSnapshot.Outcome switch
                {
                    PEGANpcNavigationOutcome.Success => PEGANpcActionOutcome.Success,
                    PEGANpcNavigationOutcome.Cancelled => PEGANpcActionOutcome.Cancelled,
                    PEGANpcNavigationOutcome.Interrupted => PEGANpcActionOutcome.Interrupted,
                    _ => PEGANpcActionOutcome.Invalid
                };
                Complete(outcome, _navigationSnapshot.Diagnostic, _navigationSnapshot.Outcome);
                return;
            }

            if (_navigationSnapshot.ActualDistanceToTargetMeters <= arrivalRadiusMeters)
            {
                Complete(
                    PEGANpcActionOutcome.Success,
                    "The physical Actor reached the selected navigation Target.",
                    PEGANpcNavigationOutcome.Success);
            }
        }

        private bool Terminate(
            PEGANpcActionOutcome outcome,
            string diagnostic,
            PEGANpcNavigationOutcome navigationOutcome)
        {
            if (Outcome != PEGANpcActionOutcome.Running)
            {
                return false;
            }

            Complete(outcome, diagnostic, navigationOutcome);
            return true;
        }

        private void Complete(
            PEGANpcActionOutcome outcome,
            string diagnostic,
            PEGANpcNavigationOutcome navigationOutcome,
            bool expectedPreconditionInvalidation = false)
        {
            _diagnostic = diagnostic ?? string.Empty;
            _completedAtTimeSeconds = Time.timeAsDouble;
            if (navigationSource != null && _requestId > 0)
            {
                navigationSource.FinishRequest(_requestId, navigationOutcome, _diagnostic);
            }

            Outcome = outcome;
            PEGANpcActionExecutionSnapshot result = CurrentExecution;
            if (outcome == PEGANpcActionOutcome.Invalid && !expectedPreconditionInvalidation)
            {
                _log.Warning(
                    "NPC navigation Action failed before reaching its target.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("action", GetType().Name),
                    LogFields.Field("diagnostic", result.Diagnostic));
            }

            _log.Debug(
                "NPC navigation Action finished.",
                LogFields.Field("action", GetType().Name),
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("requestId", result.RequestId),
                LogFields.Field("result", result.Outcome),
                LogFields.Field("declaredEffect", result.DeclaredEffect.Kind),
                LogFields.Field("effectTargetResolved", result.DeclaredEffect.IsTargetResolved),
                LogFields.Field("effectObserved", result.EffectObserved),
                LogFields.Field("effectTargetRadiusMeters", result.DeclaredEffect.TargetRadiusMeters),
                LogFields.Field("effectTargetX", result.DeclaredEffect.TargetPosition.x),
                LogFields.Field("effectTargetY", result.DeclaredEffect.TargetPosition.y),
                LogFields.Field("effectTargetZ", result.DeclaredEffect.TargetPosition.z),
                LogFields.Field("targetX", result.TargetPosition.x),
                LogFields.Field("targetY", result.TargetPosition.y),
                LogFields.Field("targetZ", result.TargetPosition.z),
                LogFields.Field("hasPath", result.HasPath),
                LogFields.Field("pathPending", result.PathPending),
                LogFields.Field("pathStatus", result.PathStatus),
                LogFields.Field("routeLengthMeters", result.RouteLengthMeters),
                LogFields.Field("effectiveSpeedMps", result.EffectiveSpeedMetersPerSecond),
                LogFields.Field("actualDistanceToTargetMeters", result.ActualDistanceToTargetMeters),
                LogFields.Field("actualSpeedMps", result.ActualSpeedMetersPerSecond),
                LogFields.Field("maximumUnsynchronizedPositionOffsetMeters", result.MaximumUnsynchronizedPositionOffsetMeters),
                LogFields.Field("maximumPositionReconciliationResidualMeters", result.MaximumPositionReconciliationResidualMeters),
                LogFields.Field("diagnostic", result.Diagnostic));
            PublishTerminalOutcome(result);
        }

        private void CompleteWithoutNavigation(
            PEGANpcActionOutcome outcome,
            Vector3 targetPosition,
            string diagnostic)
        {
            _diagnostic = diagnostic ?? string.Empty;
            _completedAtTimeSeconds = Time.timeAsDouble;
            _navigationSnapshot = default;
            _hasNavigationSnapshot = false;
            Outcome = outcome;
            PEGANpcActionEffectDeclaration declaredEffect = DeclaredEffect;
            _log.Error(
                "NPC navigation Action could not start.",
                LogFields.Field("action", GetType().Name),
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("result", outcome),
                LogFields.Field("declaredEffect", declaredEffect.Kind),
                LogFields.Field("effectTargetResolved", declaredEffect.IsTargetResolved),
                LogFields.Field("effectObserved", false),
                LogFields.Field("effectTargetRadiusMeters", declaredEffect.TargetRadiusMeters),
                LogFields.Field("targetX", targetPosition.x),
                LogFields.Field("targetY", targetPosition.y),
                LogFields.Field("targetZ", targetPosition.z),
                LogFields.Field("diagnostic", _diagnostic));
            PublishTerminalOutcome(CurrentExecution);
        }

        private void PublishTerminalOutcome(PEGANpcActionExecutionSnapshot execution)
        {
            if (_terminalOutcomeEvents == null)
            {
                return;
            }

            _terminalOutcomeEvents.Publish(new PEGANpcActionTerminalOutcomeEvent(
                execution,
                _completedAtTimeSeconds));
        }

        private void EnsureTerminalOutcomeEventsInitialized()
        {
            _terminalOutcomeEvents ??= new EventBus<PEGANpcActionTerminalOutcomeEvent>();
        }

        private void LogSubscriberFailure(
            Action<PEGANpcActionTerminalOutcomeEvent> handler,
            PEGANpcActionTerminalOutcomeEvent outcome,
            Exception exception)
        {
            EnsureLoggerInitialized();
            _log.Error(
                "NPC terminal Action subscriber failed; remaining subscribers will still be notified.",
                LogFields.Field("action", GetType().Name),
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("requestId", outcome.Execution.RequestId),
                LogFields.Field("result", outcome.Execution.Outcome),
                LogFields.Field("subscriberMethod", handler.Method.Name),
                LogFields.Field("exceptionType", exception.GetType().FullName),
                LogFields.Field("exceptionMessage", exception.Message));
        }

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PEGALogging.CreateScopedLogger<PEGANpcNavigationAction>(LogCategory, gameObject.name);
            _loggerInitialized = true;
        }

        private void ResetExecution()
        {
            _requestId = 0;
            _hasNavigationSnapshot = false;
            _navigationSnapshot = default;
            _hasEffectTarget = false;
            _effectTarget = default;
            _completedAtTimeSeconds = 0d;
            _diagnostic = string.Empty;
            Outcome = PEGANpcActionOutcome.NotStarted;
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

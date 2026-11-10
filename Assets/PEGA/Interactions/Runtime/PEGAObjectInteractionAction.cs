using UnityEngine;
namespace PEGA.Interactions.Runtime
{
    /// <summary>Uma operação independente composta no agregador de interação de um Object Entry.</summary>
    public abstract class PegaObjectInteractionAction : MonoBehaviour
    {
        private long _nextExecutionId;
        private long _currentExecutionId;
        private PegaObjectInteractionExecutionStatus _executionStatus;
        private PegaObjectInteractionRequest _request;
        private string _executionDiagnostic = string.Empty;

        public abstract string DisplayName { get; }

        public PegaObjectInteractionExecutionStatus ExecutionStatus => _executionStatus;

        public long CurrentExecutionId => _currentExecutionId;

        public string ExecutionDiagnostic => _executionDiagnostic ?? string.Empty;

        internal bool CanStart(
            PegaObjectInteractionRequest request,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy || _executionStatus == PegaObjectInteractionExecutionStatus.Running)
            {
                diagnostic = "The action component is inactive or already executing.";
                return false;
            }

            if (!request.IsValid || request.TargetRoot != gameObject)
            {
                diagnostic = "The action requires a valid Actor request addressed to its exact Object Entry root.";
                return false;
            }

            if (!request.TryValidateCurrentAuthority(out diagnostic))
            {
                return false;
            }

            return CanExecute(request, out diagnostic);
        }

        internal bool TryStart(
            PegaObjectInteractionRequest request,
            out long executionId,
            out string diagnostic)
        {
            executionId = 0;
            if (!CanStart(request, out diagnostic))
            {
                return false;
            }

            if (_nextExecutionId == long.MaxValue)
            {
                diagnostic = "This action component exhausted its local execution ID range.";
                return false;
            }

            _currentExecutionId = ++_nextExecutionId;
            _executionStatus = PegaObjectInteractionExecutionStatus.Running;
            _request = request;
            _executionDiagnostic = string.Empty;
            executionId = _currentExecutionId;

            if (!BeginExecution(request, executionId, out diagnostic))
            {
                if (_executionStatus == PegaObjectInteractionExecutionStatus.Running)
                {
                    CompleteExecution(
                        executionId,
                        PegaObjectInteractionExecutionStatus.Failed,
                        diagnostic);
                }

                return false;
            }

            return true;
        }

        public bool TryGetExecution(
            long executionId,
            out PegaObjectInteractionExecutionStatus status,
            out string diagnostic)
        {
            status = PegaObjectInteractionExecutionStatus.NotStarted;
            diagnostic = string.Empty;
            if (executionId <= 0 || executionId != _currentExecutionId)
            {
                diagnostic = "The requested interaction execution is no longer the action component's current execution.";
                return false;
            }

            status = _executionStatus;
            diagnostic = ExecutionDiagnostic;
            return status != PegaObjectInteractionExecutionStatus.NotStarted;
        }

        public bool TryCancelExecution(long executionId, string diagnostic)
        {
            return CompleteExecution(
                executionId,
                PegaObjectInteractionExecutionStatus.Cancelled,
                string.IsNullOrWhiteSpace(diagnostic)
                    ? "The interaction execution was cancelled by its owner."
                    : diagnostic);
        }

        protected PegaObjectInteractionRequest CurrentRequest => _request;

        protected abstract bool CanExecute(
            PegaObjectInteractionRequest request,
            out string diagnostic);

        protected abstract bool BeginExecution(
            PegaObjectInteractionRequest request,
            long executionId,
            out string diagnostic);

        protected virtual void TickExecution(
            PegaObjectInteractionRequest request,
            long executionId)
        {
        }

        protected virtual void OnExecutionCompleted(
            PegaObjectInteractionRequest request,
            long executionId,
            PegaObjectInteractionExecutionStatus status)
        {
        }

        protected bool CompleteExecution(
            long executionId,
            PegaObjectInteractionExecutionStatus status,
            string diagnostic)
        {
            if (executionId <= 0 || executionId != _currentExecutionId ||
                _executionStatus != PegaObjectInteractionExecutionStatus.Running ||
                (status != PegaObjectInteractionExecutionStatus.Succeeded &&
                 status != PegaObjectInteractionExecutionStatus.Failed &&
                 status != PegaObjectInteractionExecutionStatus.Cancelled))
            {
                return false;
            }

            _executionStatus = status;
            _executionDiagnostic = diagnostic ?? string.Empty;
            OnExecutionCompleted(_request, executionId, status);
            return true;
        }

        protected virtual void Update()
        {
            if (_executionStatus == PegaObjectInteractionExecutionStatus.Running)
            {
                if (!_request.TryValidateCurrentAuthority(out string diagnostic))
                {
                    CompleteExecution(
                        _currentExecutionId,
                        PegaObjectInteractionExecutionStatus.Failed,
                        diagnostic);
                    return;
                }

                TickExecution(_request, _currentExecutionId);
            }
        }

        protected virtual void OnDisable()
        {
            if (_executionStatus == PegaObjectInteractionExecutionStatus.Running)
            {
                CompleteExecution(
                    _currentExecutionId,
                    PegaObjectInteractionExecutionStatus.Failed,
                    "The action component was disabled during its execution.");
            }
        }
    }
}

using System;
using Immersive.Framework.PlayerParticipation;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
namespace PEGA.Interactions.Runtime.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerGameplayInputReader))]
    [AddComponentMenu("PEGA/Interaction/Player/Object Entry Intent Source")]
    public sealed class PegaPlayerObjectEntryInteractionIntentSource : MonoBehaviour,
        IPegaObjectInteractionIntentSource
    {
        private const string LogCategory = "PEGA.Gameplay.PlayerInteractionIntent";

        [SerializeField]
        [Tooltip("Input Action consultada pelo PlayerGameplayInputReader; deve pertencer ao Action Map de gameplay autorado.")]
        private InputActionReference interactAction;

        private PlayerGameplayInputReader _inputReader;
        private ScopedLogger _log;
        private bool _isConfigured;
        private bool _hasObservedAvailability;
        private bool _lastGameplayAvailable;
        private bool _lastGameplayReady;
        private bool _lastHasBinding;
        private PlayerGameplayInputAvailability _lastAvailability;
        private int _lastObservedBindingRevision;
        private string _lastActionResolutionDiagnostic = string.Empty;
        private string _lastInputReadDiagnostic = string.Empty;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerGameplayInputReader>();
            _log = PegaLogging.CreateScopedLogger<PegaPlayerObjectEntryInteractionIntentSource>(
                LogCategory,
                gameObject.name);

            _isConfigured = ValidateConfiguration();
            if (!_isConfigured)
            {
                enabled = false;
                return;
            }

            _log.Debug(
                "Player interaction intent source initialized.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("inputReaderAvailable", _inputReader != null),
                LogFields.Field("interactAction", GetInteractActionName()));
            ObserveInputAvailability();
        }

        bool IPegaObjectInteractionIntentSource.TryGetIntent(
            out PegaObjectInteractionIntent intent)
        {
            intent = PegaObjectInteractionIntent.None;
            if (!_isConfigured || !isActiveAndEnabled || _inputReader == null ||
                !ObserveInputAvailability())
            {
                return false;
            }

            if (interactAction == null || interactAction.action == null)
            {
                ReportActionResolutionFailure(
                    "Assign a valid InputActionReference to the Interact Action field.");
                return false;
            }

            _lastActionResolutionDiagnostic = string.Empty;
            if (!_inputReader.TryWasPressedThisFrame(interactAction, out bool wasPressed))
            {
                ReportInputReadFailure(_inputReader.Diagnostic);
                return false;
            }

            _lastInputReadDiagnostic = string.Empty;
            if (wasPressed)
            {
                intent = PegaObjectInteractionIntent.Request;
                _log.Info(
                    "Player interaction intent was produced.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("action", GetInteractActionName()),
                    LogFields.Field("intent", nameof(PegaObjectInteractionIntent)));
            }

            return true;
        }

        private bool ValidateConfiguration()
        {
            if (_inputReader == null)
            {
                _log.Error(
                    $"{nameof(PegaPlayerObjectEntryInteractionIntentSource)} requires {nameof(PlayerGameplayInputReader)} on the Player Actor root.",
                    LogFields.Field("actorRoot", gameObject.name));
                return false;
            }

            if (interactAction == null || interactAction.action == null)
            {
                _log.Error(
                    "Player interaction intent source requires a valid authored Interact InputActionReference.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("interactActionAssigned", interactAction != null));
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
            bool stateChanged = !_hasObservedAvailability ||
                hasBinding != _lastHasBinding ||
                gameplayReady != _lastGameplayReady ||
                availability != _lastAvailability ||
                gameplayAvailable != _lastGameplayAvailable ||
                bindingRevision != _lastObservedBindingRevision;

            if (stateChanged)
            {
                _log.Debug(
                    "Player interaction intent input availability changed.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("gameplayAvailable", gameplayAvailable),
                    LogFields.Field("gameplayReady", gameplayReady),
                    LogFields.Field("hasCurrentBinding", hasBinding),
                    LogFields.Field("availability", availability),
                    LogFields.Field("bindingRevision", bindingRevision),
                    LogFields.Field("diagnostic", _inputReader.Diagnostic));
            }

            _hasObservedAvailability = true;
            _lastGameplayAvailable = gameplayAvailable;
            _lastGameplayReady = gameplayReady;
            _lastHasBinding = hasBinding;
            _lastAvailability = availability;
            _lastObservedBindingRevision = bindingRevision;
            return gameplayAvailable;
        }

        private string GetInteractActionName()
        {
            InputAction action = interactAction != null ? interactAction.action : null;
            return action == null
                ? "unassigned"
                : action.actionMap != null
                    ? $"{action.actionMap.name}/{action.name}"
                    : action.name;
        }

        private void ReportActionResolutionFailure(string diagnostic)
        {
            string currentDiagnostic = diagnostic ?? string.Empty;
            if (string.Equals(_lastActionResolutionDiagnostic, currentDiagnostic, StringComparison.Ordinal))
            {
                return;
            }

            _lastActionResolutionDiagnostic = currentDiagnostic;
            _log.Error(
                "Player Interact InputActionReference is unavailable.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("action", GetInteractActionName()),
                LogFields.Field("bindingRevision", _inputReader != null ? _inputReader.BindingRevision : -1),
                LogFields.Field("diagnostic", currentDiagnostic));
        }

        private void ReportInputReadFailure(string diagnostic)
        {
            string currentDiagnostic = diagnostic ?? string.Empty;
            if (string.Equals(_lastInputReadDiagnostic, currentDiagnostic, StringComparison.Ordinal))
            {
                return;
            }

            _lastInputReadDiagnostic = currentDiagnostic;
            _log.Error(
                "Player Gameplay Input Reader could not read the Interact action.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("action", GetInteractActionName()),
                LogFields.Field("bindingRevision", _inputReader != null ? _inputReader.BindingRevision : -1),
                LogFields.Field("diagnostic", currentDiagnostic));
        }
    }
}

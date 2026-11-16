using Immersive.Framework.PlayerParticipation;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Attacks.Runtime;
using PEGA.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.Attacks.Runtime.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerGameplayInputReader))]
    [AddComponentMenu("PEGA/Combat/Player/Attack Intent Source")]
    public sealed class PegaPlayerAttackIntentSource : MonoBehaviour, IPegaActorAttackIntentSource
    {
        private const string LogCategory = "PEGA.Gameplay.PlayerAttackIntent";
        private const string AttackEventLogCategory = "PEGA.Gameplay.Attack.Events.Player";

        [SerializeField]
        [Tooltip("Input Action independente de Interact, lida pela instância atual do PlayerGameplayInputReader.")]
        private InputActionReference attackAction;

        private PlayerGameplayInputReader _inputReader;
        private ScopedLogger _log;
        private ScopedLogger _attackEventLog;
        private bool _isConfigured;
        private bool _hasObservedAvailability;
        private bool _lastGameplayAvailable;
        private bool _lastGameplayReady;
        private bool _lastHasBinding;
        private PlayerGameplayInputAvailability _lastAvailability;
        private int _lastBindingRevision;
        private string _lastInputDiagnostic = string.Empty;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerGameplayInputReader>();
            _log = PegaLogging.CreateScopedLogger<PegaPlayerAttackIntentSource>(
                LogCategory,
                gameObject.name);
            _attackEventLog = PegaLogging.CreateScopedLogger<PegaPlayerAttackIntentSource>(
                AttackEventLogCategory,
                gameObject.name);
            _isConfigured = ValidateConfiguration();
            if (!_isConfigured)
            {
                enabled = false;
                return;
            }

            _log.Debug(
                "Player attack intent source initialized.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("action", attackAction.name));
            ObserveInputAvailability();
        }

        bool IPegaActorAttackIntentSource.TryGetIntent(out PegaActorAttackIntent intent)
        {
            intent = PegaActorAttackIntent.None;
            if (!_isConfigured || !isActiveAndEnabled || _inputReader == null ||
                !ObserveInputAvailability())
            {
                return false;
            }

            if (!_inputReader.TryWasPressedThisFrame(attackAction, out bool wasPressed))
            {
                ReportInputFailure(_inputReader.Diagnostic);
                return false;
            }

            _lastInputDiagnostic = string.Empty;
            if (wasPressed)
            {
                intent = PegaActorAttackIntent.Request;
                _attackEventLog.Info(
                    "Player attack intent was produced.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("action", attackAction.name),
                    LogFields.Field("intent", nameof(PegaActorAttackIntent)));
            }

            return true;
        }

        private bool ValidateConfiguration()
        {
            if (_inputReader == null || attackAction == null || attackAction.action == null)
            {
                _log.Error(
                    "Player attack intent source requires PlayerGameplayInputReader and a valid authored Attack InputActionReference on the same Actor root.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("inputReaderAvailable", _inputReader != null),
                    LogFields.Field("attackActionAssigned", attackAction != null),
                    LogFields.Field("attackActionValid", attackAction != null && attackAction.action != null));
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
            bool changed = !_hasObservedAvailability ||
                hasBinding != _lastHasBinding ||
                gameplayReady != _lastGameplayReady ||
                availability != _lastAvailability ||
                bindingRevision != _lastBindingRevision ||
                gameplayAvailable != _lastGameplayAvailable;

            if (changed)
            {
                _hasObservedAvailability = true;
                _lastHasBinding = hasBinding;
                _lastGameplayReady = gameplayReady;
                _lastAvailability = availability;
                _lastBindingRevision = bindingRevision;
                _lastGameplayAvailable = gameplayAvailable;
                _log.Info(
                    gameplayAvailable
                        ? "Player attack input became available."
                        : "Player attack input is unavailable.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("action", attackAction.name),
                    LogFields.Field("hasCurrentBinding", hasBinding),
                    LogFields.Field("gameplayReady", gameplayReady),
                    LogFields.Field("runtimeAvailability", availability),
                    LogFields.Field("bindingRevision", bindingRevision),
                    LogFields.Field("diagnostic", _inputReader.Diagnostic));
            }

            return gameplayAvailable;
        }

        private void ReportInputFailure(string diagnostic)
        {
            string current = diagnostic ?? string.Empty;
            if (string.Equals(_lastInputDiagnostic, current, System.StringComparison.Ordinal))
            {
                return;
            }

            _lastInputDiagnostic = current;
            _log.Error(
                "Player Gameplay Input Reader could not read the Attack action.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("action", attackAction.name),
                LogFields.Field("bindingRevision", _inputReader != null ? _inputReader.BindingRevision : -1),
                LogFields.Field("diagnostic", current));
        }
    }
}

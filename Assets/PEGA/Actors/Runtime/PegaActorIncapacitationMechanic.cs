using System;
using System.Collections;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.Actors.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorDeclaration))]
    [AddComponentMenu("PEGA/Actors/Incapacitation")]
    public sealed class PegaActorIncapacitationMechanic : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.Attack.Events";

        [SerializeField, Min(0.1f), InspectorName("Recovery Duration (s)")]
        [Tooltip("Time this Actor remains incapacitated before recovering. The initial value is a calibration default; tune it for the mission.")]
        private float recoveryDurationSeconds = 5f;

        private ActorDeclaration _actorDeclaration;
        private ScopedLogger _log;
        private EventBus<PegaActorIncapacitationChangedEvent> _stateEvents;
        private Coroutine _recoveryCoroutine;
        private double _recoveryDeadlineTimeSeconds;
        private long _recoverySequence;
        private bool _isIncapacitated;
        private bool _isConfigured;
        private bool _hasAwakened;

        public float RecoveryDurationSeconds => recoveryDurationSeconds;

        public bool IsIncapacitated => _isIncapacitated;

        public bool IsAvailable => isActiveAndEnabled && (!_hasAwakened || _isConfigured);

        public float RecoveryRemainingSeconds => _isIncapacitated
            ? (float)Math.Max(0d, _recoveryDeadlineTimeSeconds - Time.timeAsDouble)
            : 0f;

        private void Awake()
        {
            _hasAwakened = true;
            _log = PegaLogging.CreateScopedLogger<PegaActorIncapacitationMechanic>(
                LogCategory,
                gameObject.name);
            _actorDeclaration = GetComponent<ActorDeclaration>();
            _isConfigured = ValidateConfiguration();
            if (!_isConfigured)
            {
                enabled = false;
                return;
            }

            _log.Debug(
                "Actor incapacitation state initialized.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("recoveryDurationSeconds", recoveryDurationSeconds));
        }

        private void OnEnable()
        {
            if (_isConfigured && _isIncapacitated)
            {
                StartRecoveryTimer(Math.Max(0d, _recoveryDeadlineTimeSeconds - Time.timeAsDouble));
            }
        }

        private void OnDisable()
        {
            if (_recoveryCoroutine != null)
            {
                StopCoroutine(_recoveryCoroutine);
                _recoveryCoroutine = null;
            }
        }

        public IEventBinding SubscribeToIncapacitationChanges(
            Action<PegaActorIncapacitationChangedEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _stateEvents ??= new EventBus<PegaActorIncapacitationChangedEvent>();
            return _stateEvents.Subscribe(change =>
            {
                try
                {
                    handler(change);
                }
                catch (Exception exception)
                {
                    _log?.Error(
                        "Actor incapacitation event subscriber failed.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("subscriberMethod", handler.Method.Name),
                        LogFields.Field("exceptionType", exception.GetType().FullName),
                        LogFields.Field("exceptionMessage", exception.Message));
                }
            });
        }

        public bool UnsubscribeFromIncapacitationChanges(IEventBinding binding) =>
            binding != null && _stateEvents != null && _stateEvents.Unsubscribe(binding);

        public bool TryBeginIncapacitation(ActorId causedByActorId, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!IsAvailable || !causedByActorId.IsValid)
            {
                diagnostic = "Incapacitation requires an active configured Actor and a valid causing ActorId.";
                return false;
            }

            if (_isIncapacitated)
            {
                diagnostic = "The Actor is already incapacitated; its recovery timer will not restart.";
                return false;
            }

            _isIncapacitated = true;
            _recoverySequence++;
            _recoveryDeadlineTimeSeconds = Time.timeAsDouble + recoveryDurationSeconds;
            StartRecoveryTimer(recoveryDurationSeconds);
            PublishIncapacitationChanged(causedByActorId, true);
            _log.Info(
                "Actor became incapacitated after a confirmed attack impact.",
                LogFields.Field("actorId", GetActorIdForLog().StableText),
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("causedByActorId", causedByActorId.StableText),
                LogFields.Field("recoveryDurationSeconds", recoveryDurationSeconds),
                LogFields.Field("changedAtTimeSeconds", Time.timeAsDouble));
            return true;
        }

        private void StartRecoveryTimer(double remainingSeconds)
        {
            if (!_isIncapacitated || _recoveryCoroutine != null || !isActiveAndEnabled)
            {
                return;
            }

            if (remainingSeconds <= 0d)
            {
                CompleteRecovery(_recoverySequence);
                return;
            }

            _recoveryCoroutine = StartCoroutine(RecoverAfterDelay(
                (float)remainingSeconds,
                _recoverySequence));
        }

        private IEnumerator RecoverAfterDelay(float remainingSeconds, long recoverySequence)
        {
            yield return new WaitForSeconds(remainingSeconds);
            CompleteRecovery(recoverySequence);
        }

        private void CompleteRecovery(long recoverySequence)
        {
            if (recoverySequence != _recoverySequence || !_isIncapacitated)
            {
                return;
            }

            _recoveryCoroutine = null;
            _isIncapacitated = false;
            double recoveredAtTimeSeconds = Time.timeAsDouble;
            PublishIncapacitationChanged(default, false);
            _log.Info(
                "Actor recovered from incapacitation.",
                LogFields.Field("actorId", GetActorIdForLog().StableText),
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("recoveredAtTimeSeconds", recoveredAtTimeSeconds),
                LogFields.Field("recoveryDurationSeconds", recoveryDurationSeconds));
        }

        private void PublishIncapacitationChanged(ActorId causedByActorId, bool isIncapacitated)
        {
            _stateEvents ??= new EventBus<PegaActorIncapacitationChangedEvent>();
            _stateEvents.Publish(new PegaActorIncapacitationChangedEvent(
                gameObject,
                _actorDeclaration.ActorId,
                isIncapacitated,
                causedByActorId,
                Time.timeAsDouble));
        }

        private ActorId GetActorIdForLog()
        {
            try
            {
                return _actorDeclaration != null ? _actorDeclaration.ActorId : default;
            }
            catch (ArgumentException)
            {
                return default;
            }
        }

        private bool ValidateConfiguration()
        {
            if (_actorDeclaration == null ||
                !IsPositiveFinite(recoveryDurationSeconds))
            {
                _log.Error(
                    "Actor incapacitation mechanic requires an Actor declaration and a positive finite recovery duration.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("actorDeclarationAvailable", _actorDeclaration != null),
                    LogFields.Field("recoveryDurationSeconds", recoveryDurationSeconds));
                return false;
            }

            try
            {
                if (!_actorDeclaration.ActorId.IsValid)
                {
                    _log.Error(
                        "Actor incapacitation mechanic requires a valid Framework ActorId.",
                        LogFields.Field("actorRoot", gameObject.name));
                    return false;
                }
            }
            catch (ArgumentException exception)
            {
                _log.Error(
                    "Actor incapacitation mechanic could not resolve its Framework ActorId.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("diagnostic", exception.Message));
                return false;
            }

            return true;
        }

        private void OnDestroy()
        {
            _stateEvents?.Clear();
            _stateEvents = null;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

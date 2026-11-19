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
    [RequireComponent(typeof(PegaActorParticipationState))]
    [AddComponentMenu("PEGA/Actors/Incapacitation")]
    public sealed class PegaActorIncapacitationMechanic : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.Attack.Events";

        [SerializeField, Min(0.1f), InspectorName("Recovery Duration (s)")]
        [Tooltip("Time this Actor remains incapacitated before recovering. The initial value is a calibration default; tune it for the mission.")]
        private float recoveryDurationSeconds = 5f;

        [SerializeField, Min(0f), InspectorName("Post-Recovery Attack Protection (s)")]
        [Tooltip("Time after recovery when this Actor can move and act but ignores attack impacts. Set to 0 to disable the protection window.")]
        private float postRecoveryAttackProtectionSeconds = 0.8f;

        private ActorDeclaration _actorDeclaration;
        private PegaActorParticipationState _participationState;
        private ScopedLogger _log;
        private EventBus<PegaActorIncapacitationChangedEvent> _stateEvents;
        private Coroutine _recoveryCoroutine;
        private Coroutine _recoveryProtectionCoroutine;
        private double _recoveryDeadlineTimeSeconds;
        private double _recoveryProtectionDeadlineTimeSeconds;
        private long _recoverySequence;
        private long _recoveryProtectionSequence;
        private bool _isIncapacitated;
        private bool _isAttackProtected;
        private bool _isConfigured;
        private bool _hasAwakened;

        public float RecoveryDurationSeconds => recoveryDurationSeconds;

        public float PostRecoveryAttackProtectionSeconds => postRecoveryAttackProtectionSeconds;

        public bool IsIncapacitated => _isIncapacitated;

        public bool IsAttackProtected =>
            _isAttackProtected && Time.timeAsDouble < _recoveryProtectionDeadlineTimeSeconds;

        public bool IsAvailable => isActiveAndEnabled && (!_hasAwakened || _isConfigured) &&
                                   (_participationState == null || _participationState.IsParticipating);

        public float RecoveryRemainingSeconds => _isIncapacitated
            ? (float)Math.Max(0d, _recoveryDeadlineTimeSeconds - Time.timeAsDouble)
            : 0f;

        public float AttackProtectionRemainingSeconds => IsAttackProtected
            ? (float)Math.Max(0d, _recoveryProtectionDeadlineTimeSeconds - Time.timeAsDouble)
            : 0f;

        public double AttackProtectionEndsAtTimeSeconds =>
            IsAttackProtected ? _recoveryProtectionDeadlineTimeSeconds : 0d;

        private void Awake()
        {
            _hasAwakened = true;
            _log = PegaLogging.CreateScopedLogger<PegaActorIncapacitationMechanic>(
                LogCategory,
                gameObject.name);
            _actorDeclaration = GetComponent<ActorDeclaration>();
            _participationState = GetComponent<PegaActorParticipationState>();
            _isConfigured = ValidateConfiguration();
            if (!_isConfigured)
            {
                enabled = false;
                return;
            }

            _log.Debug(
                "Actor incapacitation state initialized.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("recoveryDurationSeconds", recoveryDurationSeconds),
                LogFields.Field("postRecoveryAttackProtectionSeconds", postRecoveryAttackProtectionSeconds));
        }

        private void OnEnable()
        {
            if (!_isConfigured || _participationState == null || !_participationState.IsParticipating)
            {
                return;
            }

            if (_isIncapacitated)
            {
                StartRecoveryTimer(Math.Max(0d, _recoveryDeadlineTimeSeconds - Time.timeAsDouble));
            }
            else if (_isAttackProtected)
            {
                StartRecoveryProtectionTimer(
                    Math.Max(0d, _recoveryProtectionDeadlineTimeSeconds - Time.timeAsDouble));
            }
        }

        private void OnDisable()
        {
            if (_recoveryCoroutine != null)
            {
                StopCoroutine(_recoveryCoroutine);
                _recoveryCoroutine = null;
            }

            if (_recoveryProtectionCoroutine != null)
            {
                StopCoroutine(_recoveryProtectionCoroutine);
                _recoveryProtectionCoroutine = null;
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

            if (IsAttackProtected)
            {
                diagnostic = "The Actor is inside its post-recovery attack protection window.";
                return false;
            }

            CancelExpiredRecoveryProtection();

            _isIncapacitated = true;
            _recoverySequence++;
            _recoveryDeadlineTimeSeconds = Time.timeAsDouble + recoveryDurationSeconds;
            StartRecoveryTimer(recoveryDurationSeconds);
            PublishIncapacitationChanged(causedByActorId);
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
            if (!_isIncapacitated || _recoveryCoroutine != null || !isActiveAndEnabled ||
                _participationState == null || !_participationState.IsParticipating)
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

            if (_participationState == null || !_participationState.IsParticipating)
            {
                _recoveryCoroutine = null;
                return;
            }

            _recoveryCoroutine = null;
            _isIncapacitated = false;
            double recoveredAtTimeSeconds = Time.timeAsDouble;
            _isAttackProtected = postRecoveryAttackProtectionSeconds > 0f;
            _recoveryProtectionSequence++;
            _recoveryProtectionDeadlineTimeSeconds = _isAttackProtected
                ? recoveredAtTimeSeconds + postRecoveryAttackProtectionSeconds
                : 0d;
            if (_isAttackProtected)
            {
                StartRecoveryProtectionTimer(postRecoveryAttackProtectionSeconds);
            }

            PublishIncapacitationChanged(default);
            _log.Info(
                "Actor recovered from incapacitation.",
                LogFields.Field("actorId", GetActorIdForLog().StableText),
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("recoveredAtTimeSeconds", recoveredAtTimeSeconds),
                LogFields.Field("recoveryDurationSeconds", recoveryDurationSeconds),
                LogFields.Field("attackProtected", _isAttackProtected),
                LogFields.Field("attackProtectionDurationSeconds", postRecoveryAttackProtectionSeconds),
                LogFields.Field("attackProtectionEndsAtTimeSeconds", AttackProtectionEndsAtTimeSeconds));
        }

        private void StartRecoveryProtectionTimer(double remainingSeconds)
        {
            if (!_isAttackProtected || _recoveryProtectionCoroutine != null || !isActiveAndEnabled)
            {
                return;
            }

            if (remainingSeconds <= 0d)
            {
                CompleteRecoveryProtection(_recoveryProtectionSequence);
                return;
            }

            _recoveryProtectionCoroutine = StartCoroutine(EndRecoveryProtectionAfterDelay(
                (float)remainingSeconds,
                _recoveryProtectionSequence));
        }

        private IEnumerator EndRecoveryProtectionAfterDelay(float remainingSeconds, long protectionSequence)
        {
            yield return new WaitForSeconds(remainingSeconds);
            CompleteRecoveryProtection(protectionSequence);
        }

        private void CompleteRecoveryProtection(long protectionSequence)
        {
            if (protectionSequence != _recoveryProtectionSequence || !_isAttackProtected)
            {
                return;
            }

            _recoveryProtectionCoroutine = null;
            _isAttackProtected = false;
            _recoveryProtectionDeadlineTimeSeconds = 0d;
            PublishIncapacitationChanged(default);
            _log.Info(
                "Actor post-recovery attack protection ended.",
                LogFields.Field("actorId", GetActorIdForLog().StableText),
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("protectionEndedAtTimeSeconds", Time.timeAsDouble));
        }

        private void CancelExpiredRecoveryProtection()
        {
            if (!_isAttackProtected)
            {
                return;
            }

            _isAttackProtected = false;
            _recoveryProtectionDeadlineTimeSeconds = 0d;
            _recoveryProtectionSequence++;
            if (_recoveryProtectionCoroutine != null)
            {
                StopCoroutine(_recoveryProtectionCoroutine);
                _recoveryProtectionCoroutine = null;
            }
        }

        private void PublishIncapacitationChanged(ActorId causedByActorId)
        {
            _stateEvents ??= new EventBus<PegaActorIncapacitationChangedEvent>();
            _stateEvents.Publish(new PegaActorIncapacitationChangedEvent(
                gameObject,
                _actorDeclaration.ActorId,
                _isIncapacitated,
                IsAttackProtected,
                AttackProtectionEndsAtTimeSeconds,
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
                _participationState == null ||
                !IsPositiveFinite(recoveryDurationSeconds) ||
                !IsNonNegativeFinite(postRecoveryAttackProtectionSeconds))
            {
                _log.Error(
                    "Actor incapacitation mechanic requires an Actor declaration, a positive finite recovery duration and a non-negative finite post-recovery attack protection duration.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("actorDeclarationAvailable", _actorDeclaration != null),
                    LogFields.Field("participationStateAvailable", _participationState != null),
                    LogFields.Field("recoveryDurationSeconds", recoveryDurationSeconds),
                    LogFields.Field("postRecoveryAttackProtectionSeconds", postRecoveryAttackProtectionSeconds));
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

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

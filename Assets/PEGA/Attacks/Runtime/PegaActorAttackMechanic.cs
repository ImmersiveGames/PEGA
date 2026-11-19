using System;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Actors.Runtime;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.Attacks.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PegaActorIncapacitationMechanic))]
    [RequireComponent(typeof(PegaActorParticipationState))]
    [AddComponentMenu("PEGA/Combat/Actor/Attack Mechanic")]
    public sealed class PegaActorAttackMechanic : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.Attack";
        private const string AttackEventLogCategory = "PEGA.Gameplay.Attack.Events";

        [Header("Player Intent")]
        [SerializeField]
        [Tooltip("Fonte opcional de intenção do Player vinculada a este Actor. NPCs recebem a mesma mecânica pelo GOAP.")]
        private MonoBehaviour playerIntentSource;

        [Header("Impact Region")]
        [SerializeField, Min(0.01f), InspectorName("Attack Capacity")]
        [Tooltip("Confrontation capacity used against the target's current Resistance. This is a threshold, not accumulated damage.")]
        private float attackCapacity = 1f;

        [SerializeField, Min(0.05f), InspectorName("Attack Reach (m)")]
        [Tooltip("Alcance radial do ataque a partir do centro físico do Actor (m). Valor inicial para calibração no V01.")]
        private float attackReachMeters = 1.25f;

        [SerializeField, Range(1f, 90f), InspectorName("Front Tolerance (°)")]
        [Tooltip("Ângulo máximo para cada lado da orientação física do Actor em que um contato pode atingir (°).")]
        private float frontToleranceDegrees = 55f;

        [SerializeField]
        [Tooltip("Mostra a região de ataque de todos os Actors sem seleção na Scene View. Desligado por padrão para evitar sobreposição.")]
        private bool showRegionWhenUnselected;

        private CharacterController _characterController;
        private PegaActorIncapacitationMechanic _incapacitationMechanic;
        private PegaActorParticipationState _participationState;
        private IPegaActorAttackIntentSource _playerIntentSource;
        private ActorOccurrenceQueryBinding _localOccurrenceQuery;
        private IActorOccurrenceRecognitionQuery _localRecognitionQuery;
        private IActorOccurrenceRecognitionQuery _activityRecognitionQuery;
        private ScopedLogger _log;
        private ScopedLogger _attackEventLog;
        private EventBus<PegaActorAttackAttemptedEvent> _attemptEvents;
        private EventBus<PegaActorAttackResolvedEvent> _resolvedEvents;
        private long _nextAttemptId;
        private bool _isConfigured;

        public float AttackReachMeters => attackReachMeters;
        public float FrontToleranceDegrees => frontToleranceDegrees;
        public float AttackCapacity => attackCapacity;

        private void Awake()
        {
            _log = PegaLogging.CreateScopedLogger<PegaActorAttackMechanic>(LogCategory, gameObject.name);
            _attackEventLog = PegaLogging.CreateScopedLogger<PegaActorAttackMechanic>(
                AttackEventLogCategory,
                gameObject.name);
            _characterController = GetComponent<CharacterController>();
            _incapacitationMechanic = GetComponent<PegaActorIncapacitationMechanic>();
            _participationState = GetComponent<PegaActorParticipationState>();
            _localOccurrenceQuery = GetComponent<ActorOccurrenceQueryBinding>();
            _localRecognitionQuery = _localOccurrenceQuery as IActorOccurrenceRecognitionQuery;

            if (playerIntentSource != null)
            {
                _playerIntentSource = playerIntentSource as IPegaActorAttackIntentSource;
                if (_playerIntentSource == null || playerIntentSource.gameObject != gameObject)
                {
                    _log.Error(
                        "Player attack intent source must implement the PEGA attack intent contract on the same Actor root.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("intentSource", playerIntentSource.name));
                    enabled = false;
                    return;
                }
            }

            _isConfigured = ValidateConfiguration();
            if (!_isConfigured)
            {
                enabled = false;
                return;
            }

            _log.Info(
                "Actor attack mechanic initialized.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("playerIntentPath", _playerIntentSource != null),
                LogFields.Field("attackReachMeters", attackReachMeters),
                LogFields.Field("frontToleranceDegrees", frontToleranceDegrees),
                LogFields.Field("localRecognitionQueryAvailable", _localRecognitionQuery != null));
        }

        private void Update()
        {
            if (!_isConfigured || !isActiveAndEnabled || _playerIntentSource == null ||
                !_playerIntentSource.TryGetIntent(out PegaActorAttackIntent intent) ||
                !intent.IsRequested)
            {
                return;
            }

            TryPerformAttack(intent, out _);
        }

        public IEventBinding SubscribeToAttempts(Action<PegaActorAttackAttemptedEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _attemptEvents ??= new EventBus<PegaActorAttackAttemptedEvent>();
            return _attemptEvents.Subscribe(attempt =>
            {
                try
                {
                    handler(attempt);
                }
                catch (Exception exception)
                {
                    LogSubscriberFailure("attempt", nameof(PegaActorAttackAttemptedEvent), handler, exception);
                }
            });
        }

        public bool UnsubscribeFromAttempts(IEventBinding binding) =>
            binding != null && _attemptEvents != null && _attemptEvents.Unsubscribe(binding);

        public IEventBinding SubscribeToResolutions(Action<PegaActorAttackResolvedEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _resolvedEvents ??= new EventBus<PegaActorAttackResolvedEvent>();
            return _resolvedEvents.Subscribe(resolved =>
            {
                try
                {
                    handler(resolved);
                }
                catch (Exception exception)
                {
                    LogSubscriberFailure("resolution", nameof(PegaActorAttackResolvedEvent), handler, exception);
                }
            });
        }

        public bool UnsubscribeFromResolutions(IEventBinding binding) =>
            binding != null && _resolvedEvents != null && _resolvedEvents.Unsubscribe(binding);

        public bool TryBindActivityRecognitionQuery(
            IActorOccurrenceRecognitionQuery recognitionQuery,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (recognitionQuery == null || _playerIntentSource == null || !isActiveAndEnabled || !_isConfigured)
            {
                diagnostic = "The Activity-owned recognition query can only bind to an active Player Actor attack mechanic.";
                return false;
            }

            if (_activityRecognitionQuery != null &&
                !ReferenceEquals(_activityRecognitionQuery, recognitionQuery))
            {
                diagnostic = "The Player Actor attack mechanic already has a different Activity-owned recognition query bound.";
                return false;
            }

            _activityRecognitionQuery = recognitionQuery;
            _log.Info(
                "Activity-owned Actor recognition query was connected to the Player attack mechanic.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("recognitionQueryType", recognitionQuery.GetType().Name));
            return true;
        }

        public void UnbindActivityRecognitionQuery(IActorOccurrenceRecognitionQuery recognitionQuery)
        {
            if (!ReferenceEquals(_activityRecognitionQuery, recognitionQuery))
            {
                return;
            }

            _activityRecognitionQuery = null;
            _log?.Info(
                "Activity-owned Actor recognition query was removed from the Player attack mechanic.",
                LogFields.Field("actorRoot", gameObject.name));
        }

        public bool IsPositionInAttackRegion(Vector3 knownTargetPosition)
        {
            if (!_isConfigured || _participationState == null || !_participationState.IsParticipating ||
                !IsFinite(knownTargetPosition))
            {
                return false;
            }

            Vector3 origin = GetImpactOrigin();
            Vector3 targetDelta = knownTargetPosition - origin;
            targetDelta.y = 0f;
            if (targetDelta.magnitude > attackReachMeters)
            {
                return false;
            }

            Vector3 forward = GetPlanarForward();
            if (forward.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            if (targetDelta.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            return Vector3.Angle(forward, targetDelta) <= frontToleranceDegrees;
        }

        public bool TryPerformAttack(
            PegaActorAttackIntent intent,
            out PegaActorAttackResolution resolution)
        {
            resolution = default;
            if (!intent.IsRequested || !_isConfigured || !isActiveAndEnabled ||
                _participationState == null || !_participationState.IsParticipating)
            {
                return false;
            }

            if (_incapacitationMechanic == null || !_incapacitationMechanic.IsAvailable ||
                _incapacitationMechanic.IsIncapacitated)
            {
                return false;
            }

            Vector3 origin = GetImpactOrigin();
            Vector3 direction = GetPlanarForward();
            long attemptId = ++_nextAttemptId;
            double attemptedAt = Time.timeAsDouble;
            var attempted = new PegaActorAttackAttemptedEvent(
                attemptId,
                gameObject,
                origin,
                direction,
                intent.RequestedTargetOccurrenceId,
                attemptedAt);

            _attackEventLog.Info(
                "Actor attack attempt started.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("attemptId", attemptId),
                LogFields.Field("physicalForward", direction),
                LogFields.Field("origin", origin),
                LogFields.Field("requestedTargetOccurrenceId", intent.RequestedTargetOccurrenceId.HasValue
                    ? intent.RequestedTargetOccurrenceId.Value.ToString()
                    : "none"));
            PublishAttempt(attempted);

            if (direction.sqrMagnitude <= 0.000001f)
            {
                resolution = CreateMiss(
                    attemptId,
                    origin,
                    direction,
                    "The Actor has no planar physical forward direction at impact.");
                PublishResolution(resolution);
                return true;
            }

            IActorOccurrenceRecognitionQuery recognitionQuery =
                _playerIntentSource != null
                    ? _activityRecognitionQuery
                    : _localRecognitionQuery;
            if (recognitionQuery == null)
            {
                resolution = CreateUnavailable(
                    attemptId,
                    origin,
                    direction,
                    "Actor occurrence recognition query is unavailable.");
                PublishResolution(resolution);
                return true;
            }

            ActorOccurrenceRecognitionResult attackerRecognition =
                recognitionQuery.Recognize(_characterController);
            if (!attackerRecognition.Succeeded ||
                attackerRecognition.Occurrence.ActorRoot != gameObject)
            {
                resolution = CreateUnavailable(
                    attemptId,
                    origin,
                    direction,
                    $"The attacker's own Actor occurrence is unavailable or does not match this physical root: {attackerRecognition.Status}. {attackerRecognition.Diagnostic}");
                PublishResolution(resolution);
                return true;
            }

            ActorId aggressorActorId = attackerRecognition.Occurrence.ActorId;
            ActorOccurrenceId aggressorOccurrenceId = attackerRecognition.Occurrence.Id;

            Collider[] candidates = Physics.OverlapSphere(
                origin,
                attackReachMeters,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            List<PegaActorAttackContact> contacts = new List<PegaActorAttackContact>();
            string lastRejection = candidates.Length == 0 ? "No physical Collider overlapped the attack reach." :
                "No current recognized Actor was inside the attack region.";

            for (int index = 0; index < candidates.Length; index++)
            {
                Collider candidate = candidates[index];
                if (candidate == null || !candidate.enabled)
                {
                    continue;
                }

                ActorOccurrenceRecognitionResult recognized = recognitionQuery.Recognize(candidate);
                if (recognized.Status != ActorOccurrenceRecognitionStatus.Resolved)
                {
                    lastRejection = $"Physical Actor recognition returned {recognized.Status}: {recognized.Diagnostic}";
                    continue;
                }

                ActorOccurrence occurrence = recognized.Occurrence;
                if (occurrence.ActorRoot == null || occurrence.ActorRoot == gameObject)
                {
                    continue;
                }

                PegaActorParticipationState targetParticipation =
                    occurrence.ActorRoot.GetComponent<PegaActorParticipationState>();
                if (targetParticipation != null && !targetParticipation.IsParticipating)
                {
                    lastRejection = "The recognized Actor occurrence has ended gameplay participation.";
                    continue;
                }

                if (intent.RequestedTargetOccurrenceId.HasValue &&
                    !occurrence.Id.Equals(intent.RequestedTargetOccurrenceId.Value))
                {
                    continue;
                }

                Vector3 closestPoint = candidate.ClosestPoint(origin);
                Vector3 contactDelta = closestPoint - origin;
                float distanceMeters = contactDelta.magnitude;
                if (!IsFinite(distanceMeters) || distanceMeters > attackReachMeters)
                {
                    lastRejection = "The recognized Actor was outside attack reach at impact.";
                    continue;
                }

                contactDelta.y = 0f;
                if (contactDelta.sqrMagnitude <= 0.000001f)
                {
                    contactDelta = occurrence.ActorRoot.transform.position - origin;
                    contactDelta.y = 0f;
                }

                if (contactDelta.sqrMagnitude <= 0.000001f ||
                    Vector3.Angle(direction, contactDelta) > frontToleranceDegrees)
                {
                    lastRejection = "The recognized Actor was outside the Actor's front region at impact.";
                    continue;
                }

                if (!ContainsOccurrence(contacts, occurrence.Id))
                {
                    PegaActorImpactReceiverMechanic targetReceiver =
                        occurrence.ActorRoot.GetComponent<PegaActorImpactReceiverMechanic>();
                    PegaActorImpactResolution impactResolution = targetReceiver != null
                        ? targetReceiver.ReceiveAttackImpact(
                            gameObject,
                            aggressorActorId,
                            aggressorOccurrenceId,
                            occurrence,
                            contactDelta,
                            attackCapacity)
                        : default;
                    if (targetReceiver == null ||
                        impactResolution.Outcome == PegaActorImpactOutcome.Unavailable)
                    {
                        _attackEventLog.Warning(
                            "A physically confirmed Actor contact has no available attack impact receiver.",
                            LogFields.Field("attackerActorId", aggressorActorId.StableText),
                            LogFields.Field("targetActorId", occurrence.ActorId.StableText),
                            LogFields.Field("targetActorOccurrenceId", occurrence.Id.ToString()),
                            LogFields.Field("targetRoot", occurrence.ActorRoot.name),
                            LogFields.Field("diagnostic", impactResolution.Diagnostic));
                    }
                    contacts.Add(new PegaActorAttackContact(occurrence, impactResolution));
                }
            }

            string diagnostic = contacts.Count > 0
                ? "At least one current Actor contact was physically observed inside the configured region."
                : lastRejection;
            resolution = new PegaActorAttackResolution(
                attemptId,
                contacts.Count > 0 ? PegaActorAttackResultKind.Hit : PegaActorAttackResultKind.Miss,
                origin,
                direction,
                Array.AsReadOnly(contacts.ToArray()),
                diagnostic,
                Time.timeAsDouble);
            PublishResolution(resolution);
            PublishResolvedImpacts(aggressorActorId, aggressorOccurrenceId, resolution.Contacts);
            return true;
        }

        private static void PublishResolvedImpacts(
            ActorId aggressorActorId,
            ActorOccurrenceId aggressorOccurrenceId,
            IReadOnlyList<PegaActorAttackContact> contacts)
        {
            for (int index = 0; index < contacts.Count; index++)
            {
                PegaActorAttackContact contact = contacts[index];
                if (contact.ImpactResolution.Outcome == PegaActorImpactOutcome.Unavailable ||
                    contact.ActorRoot == null)
                {
                    continue;
                }

                PegaActorImpactReceiverMechanic targetReceiver =
                    contact.ActorRoot.GetComponent<PegaActorImpactReceiverMechanic>();
                targetReceiver?.PublishResolvedAttackImpact(
                    aggressorActorId,
                    aggressorOccurrenceId,
                    contact.Occurrence,
                    contact.ImpactResolution);
            }
        }

        private bool ValidateConfiguration()
        {
            if (_characterController == null ||
                _incapacitationMechanic == null ||
                _participationState == null ||
                !IsPositiveFinite(attackCapacity) ||
                !IsPositiveFinite(attackReachMeters) ||
                !IsFinite(frontToleranceDegrees) ||
                frontToleranceDegrees < 1f ||
                frontToleranceDegrees > 90f)
            {
                _log.Error(
                    "Actor attack mechanic requires an incapacitation state component, positive attack capacity and reach and a front tolerance in [1, 90] degrees.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("characterControllerAvailable", _characterController != null),
                    LogFields.Field("incapacitationMechanicAvailable", _incapacitationMechanic != null),
                    LogFields.Field("participationStateAvailable", _participationState != null),
                    LogFields.Field("attackCapacity", attackCapacity),
                    LogFields.Field("attackReachMeters", attackReachMeters),
                    LogFields.Field("frontToleranceDegrees", frontToleranceDegrees));
                return false;
            }

            return true;
        }

        private Vector3 GetImpactOrigin()
        {
            CharacterController characterController = _characterController != null
                ? _characterController
                : GetComponent<CharacterController>();
            return characterController != null ? characterController.bounds.center : transform.position;
        }

        private Vector3 GetPlanarForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.zero;
        }

        private PegaActorAttackResolution CreateMiss(
            long attemptId,
            Vector3 origin,
            Vector3 direction,
            string diagnostic) =>
            new PegaActorAttackResolution(
                attemptId,
                PegaActorAttackResultKind.Miss,
                origin,
                direction,
                Array.Empty<PegaActorAttackContact>(),
                diagnostic,
                Time.timeAsDouble);

        private PegaActorAttackResolution CreateUnavailable(
            long attemptId,
            Vector3 origin,
            Vector3 direction,
            string diagnostic) =>
            new PegaActorAttackResolution(
                attemptId,
                PegaActorAttackResultKind.Unavailable,
                origin,
                direction,
                Array.Empty<PegaActorAttackContact>(),
                diagnostic,
                Time.timeAsDouble);

        private static bool ContainsOccurrence(
            IReadOnlyList<PegaActorAttackContact> contacts,
            ActorOccurrenceId occurrenceId)
        {
            for (int index = 0; index < contacts.Count; index++)
            {
                if (contacts[index].OccurrenceId.Equals(occurrenceId))
                {
                    return true;
                }
            }

            return false;
        }

        private void PublishAttempt(PegaActorAttackAttemptedEvent attempted)
        {
            if (_attemptEvents == null)
            {
                return;
            }

            _attemptEvents.Publish(attempted);
        }

        private void PublishResolution(PegaActorAttackResolution attackResolution)
        {
            string message = attackResolution.Result switch
            {
                PegaActorAttackResultKind.Hit => "Actor attack resolved as a hit.",
                PegaActorAttackResultKind.Miss => "Actor attack resolved as a miss.",
                PegaActorAttackResultKind.Unavailable => "Actor attack result could not be validated.",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(attackResolution),
                    attackResolution.Result,
                    "Unknown Actor attack result.")
            };
            LogField[] fields =
            {
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("attemptId", attackResolution.AttemptId),
                LogFields.Field("result", attackResolution.Result),
                LogFields.Field("contactCount", attackResolution.Contacts.Count),
                LogFields.Field("contactActorIds", DescribeContacts(attackResolution.Contacts)),
                LogFields.Field("contactImpactOutcomes", DescribeImpactOutcomes(attackResolution.Contacts)),
                LogFields.Field("origin", attackResolution.Origin),
                LogFields.Field("physicalForward", attackResolution.Direction),
                LogFields.Field("attackCapacity", attackCapacity),
                LogFields.Field("attackReachMeters", attackReachMeters),
                LogFields.Field("frontToleranceDegrees", frontToleranceDegrees),
                LogFields.Field("diagnostic", attackResolution.Diagnostic)
            };
            if (attackResolution.IsAvailable)
            {
                _attackEventLog.Info(message, fields);
            }
            else
            {
                _attackEventLog.Error(message, fields);
            }
            _resolvedEvents ??= new EventBus<PegaActorAttackResolvedEvent>();
            _resolvedEvents.Publish(new PegaActorAttackResolvedEvent(attackResolution));
        }

        private string DescribeContacts(IReadOnlyList<PegaActorAttackContact> contacts)
        {
            if (contacts == null || contacts.Count == 0)
            {
                return "none";
            }

            string[] contactIds = new string[contacts.Count];
            for (int index = 0; index < contacts.Count; index++)
            {
                contactIds[index] = $"{contacts[index].ActorId}:{contacts[index].OccurrenceId}";
            }

            return string.Join(",", contactIds);
        }

        private static string DescribeImpactOutcomes(IReadOnlyList<PegaActorAttackContact> contacts)
        {
            if (contacts == null || contacts.Count == 0)
            {
                return "none";
            }

            string[] outcomes = new string[contacts.Count];
            for (int index = 0; index < contacts.Count; index++)
            {
                PegaActorAttackContact contact = contacts[index];
                PegaActorImpactResolution impact = contact.ImpactResolution;
                outcomes[index] = $"{contact.ActorId}:{impact.Outcome}[capacity={impact.AttackCapacity},resistance={impact.Resistance},itemsBefore={impact.CarriedItemCountBeforeImpact?.ToString() ?? "unknown"},itemsDropped={impact.DroppedItemCount}]";
            }

            return string.Join(",", outcomes);
        }

        private void LogSubscriberFailure(
            string eventKind,
            string eventType,
            Delegate handler,
            Exception exception) =>
            _log.Error(
                "Actor attack event subscriber failed; remaining subscribers will still be notified.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("eventKind", eventKind),
                LogFields.Field("eventType", eventType),
                LogFields.Field("subscriberMethod", handler.Method.Name),
                LogFields.Field("exceptionType", exception.GetType().FullName),
                LogFields.Field("exceptionMessage", exception.Message));

        private void OnDestroy()
        {
            _attemptEvents?.Clear();
            _resolvedEvents?.Clear();
            _attemptEvents = null;
            _resolvedEvents = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (showRegionWhenUnselected)
            {
                DrawAttackRegion();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showRegionWhenUnselected)
            {
                DrawAttackRegion();
            }
        }

        private void DrawAttackRegion()
        {
            if (!IsPositiveFinite(attackReachMeters) || !IsFinite(frontToleranceDegrees))
            {
                return;
            }

            Vector3 origin = GetImpactOrigin();
            Vector3 forward = GetPlanarForward();
            if (forward.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector3 left = Quaternion.AngleAxis(-frontToleranceDegrees, Vector3.up) * forward;
            Vector3 right = Quaternion.AngleAxis(frontToleranceDegrees, Vector3.up) * forward;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(origin, attackReachMeters);
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 1f);
            Gizmos.DrawLine(origin, origin + forward * attackReachMeters);
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            Gizmos.DrawLine(origin, origin + left * attackReachMeters);
            Gizmos.DrawLine(origin, origin + right * attackReachMeters);
            Gizmos.DrawLine(origin + left * attackReachMeters, origin + right * attackReachMeters);
        }
#endif

        private static bool IsPositiveFinite(float value) =>
            IsFinite(value) && value > 0f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}

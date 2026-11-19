using System;
using System.Linq;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Actors.Runtime;
using PEGA.Diagnostics;
using PEGA.Interactions.Runtime;
using PEGA.Movement.Actor;
using UnityEngine;

namespace PEGA.Attacks.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorDeclaration))]
    [RequireComponent(typeof(PegaActorMovementMechanic))]
    [RequireComponent(typeof(PegaActorIncapacitationMechanic))]
    [RequireComponent(typeof(PegaActorParticipationState))]
    [AddComponentMenu("PEGA/Combat/Actor/Impact Receiver")]
    public sealed class PegaActorImpactReceiverMechanic : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.Attack.Events";

        [Header("Confrontation")]
        [SerializeField, Min(0.01f), InspectorName("Resistance")]
        [Tooltip("Attack capacity this Actor must receive to become incapacitated. This is a confrontation threshold, not health or accumulated damage.")]
        private float resistance = 1f;

        [Header("Impact Motion")]
        [SerializeField, Min(0.01f), InspectorName("Knockback Distance (m)")]
        [Tooltip("Planar distance requested from the Actor's shared CharacterController motor after a valid hit.")]
        private float knockbackDistanceMeters = 1f;

        [SerializeField, Min(0.01f), InspectorName("Knockback Duration (s)")]
        [Tooltip("Duration of the planar knockback request through the Actor's shared CharacterController motor.")]
        private float knockbackDurationSeconds = 0.25f;

        private ActorDeclaration _actorDeclaration;
        private PegaActorMovementMechanic _movementMechanic;
        private PegaActorIncapacitationMechanic _incapacitationMechanic;
        private PegaActorParticipationState _participationState;
        private PegaActorPhysicalInventory _physicalInventory;
        private EventBus<PegaActorImpactResolvedEvent> _impactEvents;
        private ScopedLogger _log;
        private bool _isConfigured;
        private bool _hasAwakened;

        public float Resistance => resistance;

        public bool IsAvailable => isActiveAndEnabled && (!_hasAwakened || _isConfigured) &&
                                   (_participationState == null || _participationState.IsParticipating);

        private void Awake()
        {
            _hasAwakened = true;
            _log = PegaLogging.CreateScopedLogger<PegaActorImpactReceiverMechanic>(
                LogCategory,
                gameObject.name);
            _actorDeclaration = GetComponent<ActorDeclaration>();
            _movementMechanic = GetComponent<PegaActorMovementMechanic>();
            _incapacitationMechanic = GetComponent<PegaActorIncapacitationMechanic>();
            _participationState = GetComponent<PegaActorParticipationState>();
            _physicalInventory = GetComponent<PegaActorPhysicalInventory>();
            _isConfigured = ValidateConfiguration();
            if (!_isConfigured)
            {
                enabled = false;
                return;
            }

            _log.Debug(
                "Actor impact receiver initialized.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("resistance", resistance),
                LogFields.Field("knockbackDistanceMeters", knockbackDistanceMeters),
                LogFields.Field("knockbackDurationSeconds", knockbackDurationSeconds),
                LogFields.Field("physicalInventoryAvailable", _physicalInventory != null));
        }

        public IEventBinding SubscribeToImpacts(Action<PegaActorImpactResolvedEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _impactEvents ??= new EventBus<PegaActorImpactResolvedEvent>();
            return _impactEvents.Subscribe(impactEvent =>
            {
                try
                {
                    handler(impactEvent);
                }
                catch (Exception exception)
                {
                    _log?.Error(
                        "Actor impact event subscriber failed; remaining subscribers will still be notified.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("subscriberMethod", handler.Method.Name),
                        LogFields.Field("exceptionType", exception.GetType().FullName),
                        LogFields.Field("exceptionMessage", exception.Message));
                }
            });
        }

        public bool UnsubscribeFromImpacts(IEventBinding binding) =>
            binding != null && _impactEvents != null && _impactEvents.Unsubscribe(binding);

        internal PegaActorImpactResolution ReceiveAttackImpact(
            GameObject aggressorActorRoot,
            ActorId aggressorActorId,
            ActorOccurrenceId aggressorOccurrenceId,
            ActorOccurrence targetOccurrence,
            Vector3 knockbackDirection,
            float attackCapacity)
        {
            if (!_isConfigured || !isActiveAndEnabled ||
                _participationState == null || !_participationState.IsParticipating ||
                !_incapacitationMechanic.IsAvailable ||
                aggressorActorRoot == null ||
                !aggressorActorId.IsValid ||
                !aggressorOccurrenceId.IsValid ||
                !targetOccurrence.IsValid ||
                targetOccurrence.ActorRoot != gameObject ||
                !IsFinite(knockbackDirection) ||
                !IsPositiveFinite(attackCapacity))
            {
                return new PegaActorImpactResolution(
                    PegaActorImpactOutcome.Unavailable,
                    attackCapacity,
                    resistance,
                    0,
                    null,
                    false,
                    "The receiver is inactive or the impact identity, direction, capacity or Actor state is invalid.");
            }

            Vector3 planarDirection = knockbackDirection;
            planarDirection.y = 0f;
            if (planarDirection.sqrMagnitude <= 0.000001f)
            {
                return new PegaActorImpactResolution(
                    PegaActorImpactOutcome.Unavailable,
                    attackCapacity,
                    resistance,
                    0,
                    null,
                    false,
                    "The impact has no planar direction for knockback.");
            }

            planarDirection.Normalize();
            if (_incapacitationMechanic.IsAttackProtected)
            {
                string protectionDiagnostic =
                    "The Actor is inside its post-recovery attack protection window; the impact produced no consequences.";
                double protectionEndsAtTimeSeconds =
                    _incapacitationMechanic.AttackProtectionEndsAtTimeSeconds;
                var protectedResolution = new PegaActorImpactResolution(
                    PegaActorImpactOutcome.Protected,
                    attackCapacity,
                    resistance,
                    0,
                    null,
                    false,
                    protectionDiagnostic,
                    protectionEndsAtTimeSeconds);
                _log.Info(
                    "Actor attack impact was ignored during post-recovery protection.",
                    LogFields.Field("aggressorActorId", aggressorActorId.StableText),
                    LogFields.Field("aggressorActorOccurrenceId", aggressorOccurrenceId.ToString()),
                    LogFields.Field("targetActorId", GetActorIdForLog()),
                    LogFields.Field("targetActorOccurrenceId", targetOccurrence.Id.ToString()),
                    LogFields.Field("targetRoot", gameObject.name),
                    LogFields.Field("impactOutcome", PegaActorImpactOutcome.Protected),
                    LogFields.Field("attackCapacity", attackCapacity),
                    LogFields.Field("resistance", resistance),
                    LogFields.Field("droppedItemCount", 0),
                    LogFields.Field("knockbackApplied", false),
                    LogFields.Field("attackProtectionEndsAtTimeSeconds", protectionEndsAtTimeSeconds),
                    LogFields.Field("diagnostic", protectionDiagnostic));
                return protectedResolution;
            }

            int? carriedItemCountBeforeImpact = null;
            int droppedItemCount = 0;
            string itemDropDiagnostic = string.Empty;
            if (_physicalInventory != null)
            {
                if (_physicalInventory.TryGetCarriedItemCountForImpact(
                        out int carriedItemCount,
                        out string inventoryDiagnostic))
                {
                    carriedItemCountBeforeImpact = carriedItemCount;
                }
                else
                {
                    _log.Error(
                        "Actor impact could not validate the physical inventory count before applying consequences.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("aggressorActorId", aggressorActorId.StableText),
                        LogFields.Field("diagnostic", inventoryDiagnostic));
                }

                if (!_physicalInventory.DropAllItems(planarDirection, out droppedItemCount, out itemDropDiagnostic))
                {
                    carriedItemCountBeforeImpact = null;
                    _log.Error(
                        "Actor impact could not release every physical inventory item.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("aggressorActorId", aggressorActorId.StableText),
                        LogFields.Field("droppedItemCount", droppedItemCount),
                        LogFields.Field("diagnostic", itemDropDiagnostic));
                }
            }

            bool wasAlreadyIncapacitated = _incapacitationMechanic.IsIncapacitated;
            bool knockbackApplied = false;
            string knockbackDiagnostic = string.Empty;
            string incapacitationDiagnostic = string.Empty;
            if (!wasAlreadyIncapacitated)
            {
                knockbackApplied = _movementMechanic.TryApplyKnockback(
                    planarDirection,
                    knockbackDistanceMeters,
                    knockbackDurationSeconds,
                    out knockbackDiagnostic);
            }

            PegaActorImpactOutcome outcome;
            if (wasAlreadyIncapacitated)
            {
                outcome = PegaActorImpactOutcome.AlreadyIncapacitated;
            }
            else if (attackCapacity >= resistance)
            {
                outcome = _incapacitationMechanic.TryBeginIncapacitation(
                    aggressorActorId,
                    out incapacitationDiagnostic)
                    ? PegaActorImpactOutcome.Incapacitated
                    : _incapacitationMechanic.IsIncapacitated
                        ? PegaActorImpactOutcome.AlreadyIncapacitated
                        : PegaActorImpactOutcome.Unavailable;
            }
            else
            {
                outcome = PegaActorImpactOutcome.RemainedActive;
            }

            string diagnostic = string.Join(" ", new[]
            {
                itemDropDiagnostic,
                knockbackDiagnostic,
                incapacitationDiagnostic,
                outcome == PegaActorImpactOutcome.RemainedActive
                    ? "Attack capacity was below resistance; the Actor remains active without accumulated damage."
                    : string.Empty,
                wasAlreadyIncapacitated
                    ? "The Actor was already incapacitated; the recovery timer was not restarted."
                    : string.Empty
            }.Where(part => !string.IsNullOrWhiteSpace(part)));

            var resolution = new PegaActorImpactResolution(
                outcome,
                attackCapacity,
                resistance,
                droppedItemCount,
                carriedItemCountBeforeImpact,
                knockbackApplied,
                diagnostic);
            _log.Info(
                "Actor received a confirmed attack impact.",
                LogFields.Field("aggressorActorId", aggressorActorId.StableText),
                LogFields.Field("aggressorActorOccurrenceId", aggressorOccurrenceId.ToString()),
                LogFields.Field("targetActorId", GetActorIdForLog()),
                LogFields.Field("targetActorOccurrenceId", targetOccurrence.Id.ToString()),
                LogFields.Field("targetRoot", gameObject.name),
                LogFields.Field("attackCapacity", attackCapacity),
                LogFields.Field("resistance", resistance),
                LogFields.Field("impactOutcome", outcome),
                LogFields.Field("carriedItemCountBeforeImpact", carriedItemCountBeforeImpact),
                LogFields.Field("droppedItemCount", droppedItemCount),
                LogFields.Field("knockbackApplied", knockbackApplied),
                LogFields.Field("recoveryDurationSeconds", outcome == PegaActorImpactOutcome.Incapacitated
                    ? _incapacitationMechanic.RecoveryDurationSeconds
                    : 0f),
                LogFields.Field("diagnostic", diagnostic));
            return resolution;
        }

        internal void PublishResolvedAttackImpact(
            ActorId aggressorActorId,
            ActorOccurrenceId aggressorOccurrenceId,
            ActorOccurrence targetOccurrence,
            PegaActorImpactResolution resolution)
        {
            if (_impactEvents == null)
            {
                return;
            }

            _impactEvents.Publish(new PegaActorImpactResolvedEvent(
                aggressorActorId,
                aggressorOccurrenceId,
                targetOccurrence,
                resolution));
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
                _movementMechanic == null ||
                _incapacitationMechanic == null ||
                _participationState == null ||
                !IsPositiveFinite(resistance) ||
                !IsPositiveFinite(knockbackDistanceMeters) ||
                !IsPositiveFinite(knockbackDurationSeconds))
            {
                _log.Error(
                    "Actor impact receiver requires an Actor declaration, shared movement mechanic, configured incapacitation state, participation state and positive finite resistance and knockback settings.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("actorDeclarationAvailable", _actorDeclaration != null),
                    LogFields.Field("movementMechanicAvailable", _movementMechanic != null),
                    LogFields.Field("incapacitationMechanicAvailable", _incapacitationMechanic != null),
                    LogFields.Field("participationStateAvailable", _participationState != null),
                    LogFields.Field("resistance", resistance),
                    LogFields.Field("knockbackDistanceMeters", knockbackDistanceMeters),
                    LogFields.Field("knockbackDurationSeconds", knockbackDurationSeconds));
                return false;
            }

            try
            {
                if (!_actorDeclaration.ActorId.IsValid)
                {
                    _log.Error(
                        "Actor impact receiver requires a valid Framework ActorId.",
                        LogFields.Field("actorRoot", gameObject.name));
                    return false;
                }
            }
            catch (ArgumentException exception)
            {
                _log.Error(
                    "Actor impact receiver could not resolve its Framework ActorId.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("diagnostic", exception.Message));
                return false;
            }

            return true;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && IsFinite(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private void OnDestroy()
        {
            _impactEvents?.Clear();
            _impactEvents = null;
        }
    }
}

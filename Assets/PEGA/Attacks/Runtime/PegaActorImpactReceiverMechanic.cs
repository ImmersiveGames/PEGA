using System;
using System.Linq;
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
        private PegaActorPhysicalInventory _physicalInventory;
        private ScopedLogger _log;
        private bool _isConfigured;
        private bool _hasAwakened;

        public float Resistance => resistance;

        public bool IsAvailable => isActiveAndEnabled && (!_hasAwakened || _isConfigured);

        private void Awake()
        {
            _hasAwakened = true;
            _log = PegaLogging.CreateScopedLogger<PegaActorImpactReceiverMechanic>(
                LogCategory,
                gameObject.name);
            _actorDeclaration = GetComponent<ActorDeclaration>();
            _movementMechanic = GetComponent<PegaActorMovementMechanic>();
            _incapacitationMechanic = GetComponent<PegaActorIncapacitationMechanic>();
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

        internal PegaActorImpactResolution ReceiveAttackImpact(
            ActorId aggressorActorId,
            ActorOccurrenceId aggressorOccurrenceId,
            Vector3 knockbackDirection,
            float attackCapacity)
        {
            if (!_isConfigured || !isActiveAndEnabled ||
                !_incapacitationMechanic.IsAvailable ||
                !aggressorActorId.IsValid ||
                !IsFinite(knockbackDirection) ||
                !IsPositiveFinite(attackCapacity))
            {
                return new PegaActorImpactResolution(
                    PegaActorImpactOutcome.Unavailable,
                    attackCapacity,
                    resistance,
                    0,
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
                    false,
                    "The impact has no planar direction for knockback.");
            }

            planarDirection.Normalize();
            int droppedItemCount = 0;
            string itemDropDiagnostic = string.Empty;
            if (_physicalInventory != null &&
                !_physicalInventory.DropAllItems(planarDirection, out droppedItemCount, out itemDropDiagnostic))
            {
                _log.Error(
                    "Actor impact could not release every physical inventory item.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("aggressorActorId", aggressorActorId.StableText),
                    LogFields.Field("droppedItemCount", droppedItemCount),
                    LogFields.Field("diagnostic", itemDropDiagnostic));
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
                knockbackApplied,
                diagnostic);
            _log.Info(
                "Actor received a confirmed attack impact.",
                LogFields.Field("aggressorActorId", aggressorActorId.StableText),
                LogFields.Field("aggressorActorOccurrenceId", aggressorOccurrenceId.ToString()),
                LogFields.Field("targetActorId", GetActorIdForLog()),
                LogFields.Field("targetRoot", gameObject.name),
                LogFields.Field("attackCapacity", attackCapacity),
                LogFields.Field("resistance", resistance),
                LogFields.Field("impactOutcome", outcome),
                LogFields.Field("droppedItemCount", droppedItemCount),
                LogFields.Field("knockbackApplied", knockbackApplied),
                LogFields.Field("recoveryDurationSeconds", outcome == PegaActorImpactOutcome.Incapacitated
                    ? _incapacitationMechanic.RecoveryDurationSeconds
                    : 0f),
                LogFields.Field("diagnostic", diagnostic));
            return resolution;
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
                !_incapacitationMechanic.IsAvailable ||
                !IsPositiveFinite(resistance) ||
                !IsPositiveFinite(knockbackDistanceMeters) ||
                !IsPositiveFinite(knockbackDurationSeconds))
            {
                _log.Error(
                    "Actor impact receiver requires an Actor declaration, shared movement mechanic, configured incapacitation state and positive finite resistance and knockback settings.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("actorDeclarationAvailable", _actorDeclaration != null),
                    LogFields.Field("movementMechanicAvailable", _movementMechanic != null),
                    LogFields.Field("incapacitationMechanicAvailable", _incapacitationMechanic != null),
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
    }
}

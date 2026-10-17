using System;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Actors;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorOccurrenceReferenceBinding))]
    public sealed class PEGANpcWanderFleeMovementIntentSource : MonoBehaviour, IPEGAPlanarMovementIntentSource
    {
        private const string LogCategory = "PEGA.Gameplay.Movement";
        private const float DirectionThreshold = 0.0001f;
        private const float PersistentUnavailableWarningSeconds = 10f;

        [Header("Target")]
        [SerializeField]
        [Tooltip("Binding pública do Framework, configurada para o perfil P1 e mantida na raiz deste Actor.")]
        private ActorOccurrenceReferenceBinding targetPlayerReference;

        [Header("Wander")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Intervalo (s) para sortear uma nova direção planar durante Wander.")]
        private float wanderDirectionChangeInterval = 2.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Magnitude normalizada da intenção durante Wander; a velocidade é definida pelo executor deste Actor.")]
        private float wanderMagnitude = 0.55f;

        [SerializeField]
        [Tooltip("Semente local do sorteio de direções; não altera o estado aleatório global do jogo.")]
        private int wanderRandomSeed = 104;

        [Header("Flee")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Distância planar (m) que inicia Flee quando Rick se aproxima.")]
        private float fleeEnterRadius = 2f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Distância planar (m) que encerra Flee; deve ser maior que o raio de entrada para criar histerese.")]
        private float fleeExitRadius = 3.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Magnitude normalizada da intenção durante Flee; a velocidade é definida pelo executor deste Actor.")]
        private float fleeMagnitude = 1f;

        private System.Random _random;
        private Vector3 _wanderDirection;
        private float _timeUntilNextDirection;
        private bool _isFleeing;
        private bool _isConfigured;
        private bool _resolutionErrorLogged;
        private bool _persistentUnavailableWarningLogged;
        private bool _isObservingInitialReference;
        private bool _referenceChangedDuringInitialObservation;
        private float _referenceUnavailableSince = -1f;
        private string _lastReferenceDiagnostic = string.Empty;
        private ScopedLogger _log;

        private void Awake()
        {
            _log = PEGALogging.CreateScopedLogger<PEGANpcWanderFleeMovementIntentSource>(
                LogCategory,
                gameObject.name);
            _random = new System.Random(wanderRandomSeed);
            _isConfigured = ValidateConfiguration();
            if (!_isConfigured)
            {
                enabled = false;
                return;
            }

            ChooseWanderDirection();
            _log.Info(
                "NPC Wander initialized.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("initialState", "Wander"),
                LogFields.Field("wanderDirectionChangeIntervalSeconds", wanderDirectionChangeInterval),
                LogFields.Field("wanderMagnitude", wanderMagnitude),
                LogFields.Field("wanderRandomSeed", wanderRandomSeed),
                LogFields.Field("initialDirectionX", _wanderDirection.x),
                LogFields.Field("initialDirectionZ", _wanderDirection.z),
                LogFields.Field("fleeEnterRadiusMeters", fleeEnterRadius),
                LogFields.Field("fleeExitRadiusMeters", fleeExitRadius),
                LogFields.Field("fleeMagnitude", fleeMagnitude));
        }

        private void OnEnable()
        {
            if (!_isConfigured || targetPlayerReference == null)
            {
                return;
            }

            targetPlayerReference.Changed += OnTargetReferenceChanged;
            ObserveInitialTargetReference();
        }

        private void OnDisable()
        {
            if (targetPlayerReference != null)
            {
                targetPlayerReference.Changed -= OnTargetReferenceChanged;
            }
        }

        bool IPEGAPlanarMovementIntentSource.TryGetIntent(
            float deltaTime,
            out PEGAPlanarMovementIntent intent)
        {
            intent = PEGAPlanarMovementIntent.Zero;
            if (!_isConfigured || !IsFinite(deltaTime) || deltaTime < 0f)
            {
                return false;
            }

            if (deltaTime == 0f)
            {
                return true;
            }

            bool hasPlayerOccurrence = targetPlayerReference.TryGetCurrent(
                out ActorOccurrenceResolution resolution);
            ObserveResolutionDiagnostic(resolution);
            if (!hasPlayerOccurrence)
            {
                if (resolution.Status == ActorOccurrenceResolutionStatus.Unavailable)
                {
                    _resolutionErrorLogged = false;
                    CheckPersistentUnavailable(resolution);

                    if (_isFleeing)
                    {
                        _isFleeing = false;
                        ChooseWanderDirection();
                        LogFleeToWander(
                            "playerOccurrenceUnavailable",
                            float.NaN,
                            resolution);
                    }

                    intent = GetWanderIntent(deltaTime);
                    return true;
                }

                _referenceUnavailableSince = -1f;
                if (!_resolutionErrorLogged)
                {
                    _log.Error(
                        $"{nameof(PEGANpcWanderFleeMovementIntentSource)} cannot resolve its configured Player occurrence.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("resolutionStatus", resolution.Status),
                        LogFields.Field("diagnostic", resolution.Diagnostic));
                    _resolutionErrorLogged = true;
                }

                return false;
            }

            _resolutionErrorLogged = false;
            _referenceUnavailableSince = -1f;
            _persistentUnavailableWarningLogged = false;
            Transform playerRoot = resolution.Occurrence.ActorRoot.transform;
            Vector3 directionAwayFromPlayer = transform.position - playerRoot.position;
            directionAwayFromPlayer.y = 0f;
            float distanceSquared = directionAwayFromPlayer.sqrMagnitude;

            if (_isFleeing)
            {
                if (distanceSquared >= fleeExitRadius * fleeExitRadius)
                {
                    _isFleeing = false;
                    ChooseWanderDirection();
                    LogFleeToWander(
                        "fleeExitRadiusReached",
                        Mathf.Sqrt(distanceSquared),
                        resolution);
                }
            }
            else if (distanceSquared <= fleeEnterRadius * fleeEnterRadius)
            {
                _isFleeing = true;
                LogWanderToFlee(Mathf.Sqrt(distanceSquared), resolution);
            }

            if (!_isFleeing)
            {
                intent = GetWanderIntent(deltaTime);
                return true;
            }

            if (distanceSquared <= DirectionThreshold * DirectionThreshold)
            {
                return true;
            }

            intent = new PEGAPlanarMovementIntent(directionAwayFromPlayer, fleeMagnitude);
            return true;
        }

        private PEGAPlanarMovementIntent GetWanderIntent(float deltaTime)
        {
            _timeUntilNextDirection -= deltaTime;
            if (_timeUntilNextDirection <= 0f)
            {
                ChooseWanderDirection();
            }

            return new PEGAPlanarMovementIntent(_wanderDirection, wanderMagnitude);
        }

        private void ChooseWanderDirection()
        {
            float angleRadians = (float)(_random.NextDouble() * Math.PI * 2d);
            _wanderDirection = new Vector3(
                Mathf.Sin(angleRadians),
                0f,
                Mathf.Cos(angleRadians));
            _timeUntilNextDirection = wanderDirectionChangeInterval;
        }

        private bool ValidateConfiguration()
        {
            if (targetPlayerReference == null || targetPlayerReference.gameObject != gameObject)
            {
                _log.Error(
                    $"{nameof(PEGANpcWanderFleeMovementIntentSource)} requires an explicit {nameof(ActorOccurrenceReferenceBinding)} on the same Actor root.",
                    LogFields.Field("actorRoot", gameObject.name));
                return false;
            }

            if (!targetPlayerReference.TryGetReference(
                    out ActorOccurrenceReference reference,
                    out string diagnostic) ||
                reference.Kind != ActorOccurrenceReferenceKind.PlayerSlot)
            {
                _log.Error(
                    $"{nameof(PEGANpcWanderFleeMovementIntentSource)} requires a valid Player Slot reference.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("referenceKind", reference.Kind),
                    LogFields.Field("diagnostic", diagnostic));
                return false;
            }

            if (!IsPositiveFinite(wanderDirectionChangeInterval) ||
                !IsPositiveFinite(fleeEnterRadius) ||
                !IsPositiveFinite(fleeExitRadius) ||
                fleeExitRadius <= fleeEnterRadius ||
                !IsNormalizedMagnitude(wanderMagnitude) ||
                !IsNormalizedMagnitude(fleeMagnitude))
            {
                _log.Error(
                    $"{nameof(PEGANpcWanderFleeMovementIntentSource)} requires positive intervals/radii, an exit radius greater than the entry radius, and normalized magnitudes in [0, 1].",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("wanderInterval", wanderDirectionChangeInterval),
                    LogFields.Field("fleeEnterRadius", fleeEnterRadius),
                    LogFields.Field("fleeExitRadius", fleeExitRadius),
                    LogFields.Field("wanderMagnitude", wanderMagnitude),
                    LogFields.Field("fleeMagnitude", fleeMagnitude));
                return false;
            }

            return true;
        }

        private void ObserveInitialTargetReference()
        {
            _referenceChangedDuringInitialObservation = false;
            _isObservingInitialReference = true;
            bool hasReference = targetPlayerReference.TryGetReference(
                out ActorOccurrenceReference reference,
                out string referenceDiagnostic);
            bool hasCurrent = targetPlayerReference.TryGetCurrent(
                out ActorOccurrenceResolution resolution);
            _isObservingInitialReference = false;
            _lastReferenceDiagnostic = resolution.Diagnostic;
            _referenceUnavailableSince = resolution.Status == ActorOccurrenceResolutionStatus.Unavailable
                ? Time.unscaledTime
                : -1f;

            _log.Debug(
                "Actor occurrence reference binding initialized.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("binding", nameof(ActorOccurrenceReferenceBinding)),
                LogFields.Field("referenceConfigured", hasReference),
                LogFields.Field("referenceKind", hasReference ? reference.Kind.ToString() : "invalid"),
                LogFields.Field("playerSlot", hasReference && reference.Kind == ActorOccurrenceReferenceKind.PlayerSlot
                    ? reference.PlayerSlotId.StableText
                    : string.Empty),
                LogFields.Field("resolutionStatus", resolution.Status),
                LogFields.Field("available", hasCurrent),
                LogFields.Field("diagnostic", hasReference ? resolution.Diagnostic : referenceDiagnostic));

            if (hasCurrent && !_referenceChangedDuringInitialObservation)
            {
                LogOccurrenceAvailable("Actor occurrence is already available at source initialization.", resolution.Occurrence);
            }
        }

        private void OnTargetReferenceChanged(ActorOccurrenceReferenceChange change)
        {
            if (_isObservingInitialReference)
            {
                _referenceChangedDuringInitialObservation = true;
            }

            ActorOccurrenceResolution previous = change.Previous;
            ActorOccurrenceResolution current = change.Current;
            _lastReferenceDiagnostic = current.Diagnostic;
            _referenceUnavailableSince = current.Status == ActorOccurrenceResolutionStatus.Unavailable
                ? Time.unscaledTime
                : -1f;
            _persistentUnavailableWarningLogged = false;
            _resolutionErrorLogged = false;

            switch (change.Kind)
            {
                case ActorOccurrenceReferenceChangeKind.Available:
                    LogOccurrenceAvailable("P1 Actor occurrence became available after Player composition.", current.Occurrence);
                    break;
                case ActorOccurrenceReferenceChangeKind.Replaced:
                    _log.Info(
                        "P1 Actor occurrence was replaced.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("previousOccurrenceId", GetOccurrenceId(previous)),
                        LogFields.Field("currentOccurrenceId", GetOccurrenceId(current)),
                        LogFields.Field("currentActorId", current.Occurrence.ActorId.ToString()),
                        LogFields.Field("currentPhysicalActorRoot", current.Occurrence.ActorRoot.name),
                        LogFields.Field("playerSlot", current.Occurrence.PlayerSlotId.StableText));
                    break;
                case ActorOccurrenceReferenceChangeKind.Invalidated:
                    _log.Warning(
                        "P1 Actor occurrence reference was invalidated.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("previousOccurrenceId", GetOccurrenceId(previous)),
                        LogFields.Field("resolutionStatus", current.Status),
                        LogFields.Field("diagnostic", current.Diagnostic));
                    break;
                case ActorOccurrenceReferenceChangeKind.Unavailable:
                    if (previous.Succeeded)
                    {
                        _log.Info(
                            "P1 Actor occurrence became unavailable.",
                            LogFields.Field("actorRoot", gameObject.name),
                            LogFields.Field("previousOccurrenceId", GetOccurrenceId(previous)),
                            LogFields.Field("resolutionStatus", current.Status),
                            LogFields.Field("diagnostic", current.Diagnostic));
                    }
                    else
                    {
                        _log.Debug(
                            "P1 Actor occurrence resolution state changed while still unavailable.",
                            LogFields.Field("actorRoot", gameObject.name),
                            LogFields.Field("resolutionStatus", current.Status),
                            LogFields.Field("diagnostic", current.Diagnostic));
                    }
                    break;
                case ActorOccurrenceReferenceChangeKind.BindingReleased:
                    _log.Debug(
                        "Actor occurrence reference binding was released with its Framework composition scope.",
                        LogFields.Field("actorRoot", gameObject.name),
                        LogFields.Field("previousOccurrenceId", GetOccurrenceId(previous)),
                        LogFields.Field("resolutionStatus", current.Status),
                        LogFields.Field("diagnostic", current.Diagnostic));
                    break;
            }
        }

        private void ObserveResolutionDiagnostic(ActorOccurrenceResolution resolution)
        {
            if (string.Equals(_lastReferenceDiagnostic, resolution.Diagnostic, StringComparison.Ordinal))
            {
                return;
            }

            _lastReferenceDiagnostic = resolution.Diagnostic;
            _log.Debug(
                "Actor occurrence resolution details changed.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("resolutionStatus", resolution.Status),
                LogFields.Field("diagnostic", resolution.Diagnostic));
        }

        private void CheckPersistentUnavailable(ActorOccurrenceResolution resolution)
        {
            if (_referenceUnavailableSince < 0f)
            {
                _referenceUnavailableSince = Time.unscaledTime;
                return;
            }

            float unavailableSeconds = Time.unscaledTime - _referenceUnavailableSince;
            if (_persistentUnavailableWarningLogged || unavailableSeconds < PersistentUnavailableWarningSeconds)
            {
                return;
            }

            _persistentUnavailableWarningLogged = true;
            _log.Warning(
                "P1 Actor occurrence remains unavailable after the startup grace period; verify Activity composition and the P1 Join.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("unavailableSeconds", unavailableSeconds),
                LogFields.Field("expectedReferenceKind", ActorOccurrenceReferenceKind.PlayerSlot),
                LogFields.Field("resolutionStatus", resolution.Status),
                LogFields.Field("diagnostic", resolution.Diagnostic));
        }

        private void LogWanderToFlee(float distance, ActorOccurrenceResolution resolution)
        {
            _log.Info(
                "NPC state changed from Wander to Flee.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("distanceFromRickMeters", distance),
                LogFields.Field("fleeEnterRadiusMeters", fleeEnterRadius),
                LogFields.Field("fleeExitRadiusMeters", fleeExitRadius),
                LogFields.Field("decisionThresholdMeters", fleeEnterRadius),
                LogFields.Field("playerOccurrenceId", GetOccurrenceId(resolution)),
                LogFields.Field("playerSlot", GetPlayerSlot(resolution)),
                LogFields.Field("playerActorId", GetActorId(resolution)),
                LogFields.Field("playerPhysicalActorRoot", GetPhysicalActorRootName(resolution)),
                LogFields.Field("fleeMagnitude", fleeMagnitude));
        }

        private void LogFleeToWander(
            string reason,
            float distance,
            ActorOccurrenceResolution resolution)
        {
            _log.Info(
                "NPC state changed from Flee to Wander.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("reason", reason),
                LogFields.Field("distanceFromRickMeters", IsFinite(distance) ? (object)distance : "unavailable"),
                LogFields.Field("fleeEnterRadiusMeters", fleeEnterRadius),
                LogFields.Field("fleeExitRadiusMeters", fleeExitRadius),
                LogFields.Field("decisionThresholdMeters", reason == "fleeExitRadiusReached"
                    ? (object)fleeExitRadius
                    : "unavailable"),
                LogFields.Field("playerOccurrenceId", GetOccurrenceId(resolution)),
                LogFields.Field("playerSlot", GetPlayerSlot(resolution)),
                LogFields.Field("playerActorId", GetActorId(resolution)),
                LogFields.Field("playerPhysicalActorRoot", GetPhysicalActorRootName(resolution)),
                LogFields.Field("diagnostic", resolution.Diagnostic),
                LogFields.Field("wanderMagnitude", wanderMagnitude));
        }

        private void LogOccurrenceAvailable(string message, ActorOccurrence occurrence)
        {
            _log.Info(
                message,
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("referenceKind", ActorOccurrenceReferenceKind.PlayerSlot),
                LogFields.Field("playerSlot", occurrence.PlayerSlotId.StableText),
                LogFields.Field("occurrenceId", occurrence.Id.ToString()),
                LogFields.Field("actorId", occurrence.ActorId.ToString()),
                LogFields.Field("physicalActorRoot", occurrence.ActorRoot.name));
        }

        private static string GetOccurrenceId(ActorOccurrenceResolution resolution) =>
            resolution.Succeeded ? resolution.Occurrence.Id.ToString() : string.Empty;

        private static string GetPlayerSlot(ActorOccurrenceResolution resolution) =>
            resolution.Succeeded ? resolution.Occurrence.PlayerSlotId.StableText : string.Empty;

        private static string GetActorId(ActorOccurrenceResolution resolution) =>
            resolution.Succeeded ? resolution.Occurrence.ActorId.ToString() : string.Empty;

        private static string GetPhysicalActorRootName(ActorOccurrenceResolution resolution) =>
            resolution.Succeeded ? resolution.Occurrence.ActorRoot.name : string.Empty;

        private static bool IsNormalizedMagnitude(float value) =>
            IsFinite(value) && value >= 0f && value <= 1f;

        private static bool IsPositiveFinite(float value) =>
            IsFinite(value) && value > 0f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

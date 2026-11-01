using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.Authoring;
using Immersive.Framework.PlayerParticipation;
using Immersive.Framework.PlayerSlots;
using Immersive.Framework.RuntimeContent;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    internal sealed class PEGAActorProfileLocationSubjectEvidence : IPEGAActorGroupLocationSubjectEvidence
    {
        public PEGAActorProfileLocationSubjectEvidence(
            ActorProfileId actorProfileId,
            ActorId actorId,
            RuntimeContentOwner owner,
            PlayerSlotId playerSlotId,
            ActorOccurrenceId occurrenceId)
        {
            ActorProfileId = actorProfileId;
            ActorId = actorId;
            Owner = owner;
            PlayerSlotId = playerSlotId;
            OccurrenceId = occurrenceId;
        }

        public ActorProfileId ActorProfileId { get; }

        public ActorId ActorId { get; }

        public RuntimeContentOwner Owner { get; }

        public PlayerSlotId PlayerSlotId { get; }

        public ActorOccurrenceId OccurrenceId { get; }

        public bool Equals(PEGAActorProfileLocationSubjectEvidence other) =>
            other != null && ActorProfileId == other.ActorProfileId && ActorId == other.ActorId &&
            Owner.Equals(other.Owner) && PlayerSlotId == other.PlayerSlotId && OccurrenceId == other.OccurrenceId;
    }

    /// <summary>
    /// Produces only explicitly authorized initial location facts for ActorProfile identities.
    /// It reads Framework Session correlation and published Actor occurrences, then stops observing
    /// transforms after the one-time fact has been accepted.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/Actors/Actor Profile Knowledge Source")]
    public sealed class PEGAActorProfileKnowledgeSource : PEGAActorGroupKnowledgeSource
    {
        private const string LogCategory = "PEGA.Gameplay.Knowledge.ActorProfileSource";

        [SerializeField]
        [Tooltip("Observação oficial da Player Session usada para correlacionar ActorProfileId, Slot e ActorId.")]
        private PlayerSessionObserver playerSessionObserver;

        [SerializeField]
        [Tooltip("Bindings oficiais de ocorrência física por Player Slot, necessários somente para localização inicial.")]
        private ActorOccurrenceReferenceBinding[] actorOccurrenceBindings =
            Array.Empty<ActorOccurrenceReferenceBinding>();

        [NonSerialized]
        private HashSet<string> _loggedResolutionStates;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        [NonSerialized]
        private bool _sourceReady;

        protected override void OnKnowledgeSourceEntered()
        {
            _sourceReady = false;
            _loggedResolutionStates ??= new HashSet<string>(StringComparer.Ordinal);
            _loggedResolutionStates.Clear();

            if (playerSessionObserver == null)
            {
                EnsureLoggerInitialized();
                _log.Error(
                    "Actor Profile knowledge source has invalid authoring and will keep Actor identity and location unresolved.",
                    LogFields.Field("reason", "A Player Session Observer is required for ActorId-to-ActorProfile correlation."));
                return;
            }

            bool hasLocationAuthorizedProfiles = HasLocationAuthorizedActorProfiles();
            if (hasLocationAuthorizedProfiles && !TryValidateOccurrenceBindings(out string diagnostic))
            {
                EnsureLoggerInitialized();
                _log.Error(
                    "Actor Profile location knowledge source has invalid authoring and will keep location unknown.",
                    LogFields.Field("reason", diagnostic));
                return;
            }

            _sourceReady = true;
            if (!hasLocationAuthorizedProfiles)
            {
                return;
            }

            playerSessionObserver.Changed += OnPlayerSessionChanged;
            for (int index = 0; index < actorOccurrenceBindings.Length; index++)
            {
                actorOccurrenceBindings[index].Changed += OnActorOccurrenceChanged;
            }

            RefreshAuthorizedInitialKnowledge();
        }

        protected override void OnKnowledgeSourceExited()
        {
            StopObserving();
            _sourceReady = false;
            _loggedResolutionStates?.Clear();
        }

        internal override void RefreshAuthorizedInitialKnowledge()
        {
            if (!IsKnowledgeSourceActive || !_sourceReady || KnowledgeState == null ||
                !KnowledgeState.IsOwnerActive || playerSessionObserver == null)
            {
                return;
            }

            IReadOnlyList<PEGAActorGroupPriorKnowledgeDefinition> definitions =
                KnowledgeState.PriorEntityKnowledge;
            for (int index = 0; index < definitions.Count; index++)
            {
                PEGAActorGroupPriorKnowledgeDefinition definition = definitions[index];
                if (definition == null ||
                    definition.Mode != PEGAActorGroupPriorKnowledgeMode.IdentityAndInitialLocation ||
                    !(definition.EntityIdentity is ActorProfile actorProfile) ||
                    KnowledgeState.HasKnownInitialLocation(actorProfile))
                {
                    continue;
                }

                TryCaptureInitialLocation(actorProfile);
            }
        }

        internal bool TryGetCurrentActorProfile(
            ActorId actorId,
            out ActorProfile actorProfile,
            out ActorProfileId actorProfileId,
            out string diagnostic)
        {
            actorProfile = null;
            actorProfileId = default;
            if (!IsKnowledgeSourceActive || !_sourceReady || playerSessionObserver == null || !actorId.IsValid ||
                !playerSessionObserver.TryGetObservation(
                    out PlayerSessionScopedObservationSnapshot observation))
            {
                diagnostic = "Current ActorProfileId correlation is unavailable from the scoped Player Session.";
                return false;
            }

            int matches = 0;
            for (int index = 0; index < observation.Slots.Count; index++)
            {
                PlayerSessionScopedSlotObservation slot = observation.Slots[index];
                if (!slot.HasCurrentActorEvidence || !slot.CurrentActor.HasCurrentActor ||
                    !slot.CurrentActor.ActorEvidence.IsValid ||
                    slot.CurrentActor.ActorEvidence.ActorId != actorId)
                {
                    continue;
                }

                ActorProfile selectedProfile = slot.Slot.SelectedActorProfile;
                if (selectedProfile == null ||
                    !selectedProfile.TryGetActorProfileId(out ActorProfileId selectedProfileId, out _ ) ||
                    selectedProfileId != slot.CurrentActor.ActorEvidence.ActorProfileId)
                {
                    continue;
                }

                matches++;
                actorProfile = selectedProfile;
                actorProfileId = selectedProfileId;
            }

            if (matches != 1)
            {
                actorProfile = null;
                actorProfileId = default;
                diagnostic = matches == 0
                    ? "No current Player Slot correlates this ActorId to an ActorProfileId."
                    : "More than one current Player Slot correlates the ActorId; identity resolution is ambiguous.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        internal bool TryGetCurrentLocationSubjectEvidence(
            ActorProfile actorProfile,
            ActorId actorId,
            out IPEGAActorGroupLocationSubjectEvidence locationSubjectEvidence,
            out string diagnostic)
        {
            locationSubjectEvidence = null;
            diagnostic = string.Empty;
            if (!IsKnowledgeSourceActive || !_sourceReady || !actorId.IsValid ||
                !TryResolveActorProfileOccurrence(
                    actorProfile,
                    actorId,
                    out ActorProfileId actorProfileId,
                    out PlayerActorCorrelationEvidence actorEvidence,
                    out ActorOccurrence occurrence,
                    out _,
                    out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The current Actor occurrence cannot be correlated to the captured location evidence."
                    : diagnostic;
                return false;
            }

            locationSubjectEvidence = new PEGAActorProfileLocationSubjectEvidence(
                actorProfileId,
                actorEvidence.ActorId,
                actorEvidence.Owner,
                actorEvidence.PlayerSlotId,
                occurrence.Id);
            diagnostic = string.Empty;
            return true;
        }

        private void TryCaptureInitialLocation(ActorProfile actorProfile)
        {
            if (!TryResolveActorProfileOccurrence(
                    actorProfile,
                    null,
                    out ActorProfileId expectedProfileId,
                    out PlayerActorCorrelationEvidence evidence,
                    out ActorOccurrence occurrence,
                    out string resolutionStatus,
                    out string diagnostic))
            {
                LogResolution(expectedProfileId.IsValid ? expectedProfileId.StableText : "unavailable",
                    resolutionStatus,
                    diagnostic);
                return;
            }

            var locationSubjectEvidence = new PEGAActorProfileLocationSubjectEvidence(
                expectedProfileId,
                evidence.ActorId,
                evidence.Owner,
                evidence.PlayerSlotId,
                occurrence.Id);
            if (!TryRecordInitialLocation(
                    actorProfile,
                    locationSubjectEvidence,
                    occurrence.ActorRoot.transform.position,
                    Time.timeAsDouble,
                    string.Concat(
                        "ActorId=", evidence.ActorId.StableText,
                        "; Slot=", evidence.PlayerSlotId.StableText,
                        "; Owner=", evidence.Owner.StableText,
                        "; Occurrence=", occurrence.Id.Value),
                    out string recordDiagnostic))
            {
                LogResolution(expectedProfileId.StableText, "GroupUnavailable", recordDiagnostic);
                return;
            }

            LogResolution(expectedProfileId.StableText, "Resolved", string.Concat(
                "ActorId=", evidence.ActorId.StableText,
                "; Slot=", evidence.PlayerSlotId.StableText,
                "; Owner=", evidence.Owner.StableText,
                "; Occurrence=", occurrence.Id.Value));
        }

        private bool TryResolveActorProfileOccurrence(
            ActorProfile actorProfile,
            ActorId? requiredActorId,
            out ActorProfileId actorProfileId,
            out PlayerActorCorrelationEvidence actorEvidence,
            out ActorOccurrence occurrence,
            out string resolutionStatus,
            out string diagnostic)
        {
            actorProfileId = default;
            actorEvidence = default;
            occurrence = default;
            diagnostic = string.Empty;
            if (actorProfile == null ||
                !actorProfile.TryGetActorProfileId(out actorProfileId, out diagnostic))
            {
                resolutionStatus = "InvalidIdentity";
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "A valid ActorProfile identity is required."
                    : diagnostic;
                return false;
            }

            if (playerSessionObserver == null || !playerSessionObserver.TryGetObservation(
                    out PlayerSessionScopedObservationSnapshot observation))
            {
                resolutionStatus = "SessionUnavailable";
                diagnostic = playerSessionObserver != null
                    ? playerSessionObserver.Diagnostic
                    : "A Player Session Observer is required.";
                return false;
            }

            int matchCount = 0;
            for (int index = 0; index < observation.Slots.Count; index++)
            {
                PlayerSessionScopedSlotObservation slot = observation.Slots[index];
                if (!slot.HasCurrentActorEvidence || !slot.CurrentActor.HasCurrentActor ||
                    !slot.CurrentActor.ActorEvidence.IsValid ||
                    slot.CurrentActor.ActorEvidence.ActorProfileId != actorProfileId ||
                    requiredActorId.HasValue && slot.CurrentActor.ActorEvidence.ActorId != requiredActorId.Value)
                {
                    continue;
                }

                matchCount++;
                actorEvidence = slot.CurrentActor.ActorEvidence;
            }

            if (matchCount != 1)
            {
                resolutionStatus = matchCount == 0 ? "ActorUnavailable" : "AmbiguousActorProfile";
                diagnostic = matchCount == 0
                    ? "No unique current Player Slot correlates this ActorProfileId to a physical Actor."
                    : "Multiple current Player Slots correlate to the authored ActorProfileId; location remains unknown.";
                return false;
            }

            if (!TryGetOccurrenceBinding(
                    actorEvidence.PlayerSlotId,
                    out ActorOccurrenceReferenceBinding binding,
                    out diagnostic))
            {
                resolutionStatus = "BindingUnavailable";
                return false;
            }

            if (!binding.TryGetReference(out ActorOccurrenceReference reference, out diagnostic) ||
                reference.Kind != ActorOccurrenceReferenceKind.PlayerSlot ||
                reference.PlayerSlotId != actorEvidence.PlayerSlotId)
            {
                resolutionStatus = "InvalidOccurrenceBinding";
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "Occurrence binding must select the exact Player Slot represented by Framework evidence."
                    : diagnostic;
                return false;
            }

            if (!binding.TryGetCurrent(out ActorOccurrenceResolution resolution) || !resolution.Succeeded)
            {
                resolutionStatus = resolution.Status.ToString();
                diagnostic = resolution.Diagnostic;
                return false;
            }

            occurrence = resolution.Occurrence;
            if (!occurrence.IsValid || occurrence.ActorId != actorEvidence.ActorId ||
                !occurrence.Owner.Equals(actorEvidence.Owner) ||
                !occurrence.HasPlayerSlot || occurrence.PlayerSlotId != actorEvidence.PlayerSlotId)
            {
                resolutionStatus = "CorrelationMismatch";
                diagnostic = "Published occurrence does not match the current ActorId, Slot and owner correlation evidence.";
                occurrence = default;
                return false;
            }

            resolutionStatus = "Resolved";
            diagnostic = string.Empty;
            return true;
        }

        private bool HasLocationAuthorizedActorProfiles()
        {
            if (KnowledgeState == null)
            {
                return false;
            }

            IReadOnlyList<PEGAActorGroupPriorKnowledgeDefinition> definitions =
                KnowledgeState.PriorEntityKnowledge;
            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] != null &&
                    definitions[index].Mode == PEGAActorGroupPriorKnowledgeMode.IdentityAndInitialLocation &&
                    definitions[index].EntityIdentity is ActorProfile)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryValidateOccurrenceBindings(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (actorOccurrenceBindings == null)
            {
                diagnostic = "Actor occurrence bindings are required for authorized initial location.";
                return false;
            }

            var seenSlots = new HashSet<PlayerSlotId>();
            for (int index = 0; index < actorOccurrenceBindings.Length; index++)
            {
                ActorOccurrenceReferenceBinding binding = actorOccurrenceBindings[index];
                if (binding == null || !binding.TryGetReference(
                        out ActorOccurrenceReference reference, out diagnostic) ||
                    reference.Kind != ActorOccurrenceReferenceKind.PlayerSlot ||
                    !reference.PlayerSlotId.IsValid)
                {
                    diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                        ? "Each occurrence binding must select a valid Player Slot."
                        : diagnostic;
                    return false;
                }

                if (!seenSlots.Add(reference.PlayerSlotId))
                {
                    diagnostic = "Actor occurrence bindings cannot repeat the same Player Slot.";
                    return false;
                }
            }

            diagnostic = seenSlots.Count == 0
                ? "At least one Player Slot occurrence binding is required."
                : string.Empty;
            return seenSlots.Count > 0;
        }

        private bool TryGetOccurrenceBinding(
            PlayerSlotId playerSlotId,
            out ActorOccurrenceReferenceBinding occurrenceReference,
            out string diagnostic)
        {
            occurrenceReference = null;
            if (actorOccurrenceBindings == null)
            {
                diagnostic = "Actor occurrence bindings are unavailable.";
                return false;
            }

            for (int index = 0; index < actorOccurrenceBindings.Length; index++)
            {
                ActorOccurrenceReferenceBinding binding = actorOccurrenceBindings[index];
                if (binding != null && binding.TryGetReference(
                        out ActorOccurrenceReference reference, out _) &&
                    reference.Kind == ActorOccurrenceReferenceKind.PlayerSlot &&
                    reference.PlayerSlotId == playerSlotId)
                {
                    occurrenceReference = binding;
                    diagnostic = occurrenceReference != null
                        ? string.Empty
                        : "The matching Player Slot occurrence binding is missing.";
                    return occurrenceReference != null;
                }
            }

            diagnostic = string.Concat("No occurrence binding is configured for Player Slot '",
                playerSlotId.StableText, "'.");
            return false;
        }

        private void OnPlayerSessionChanged(PlayerSessionChange change) =>
            RefreshAuthorizedInitialKnowledge();

        private void OnActorOccurrenceChanged(ActorOccurrenceReferenceChange change) =>
            RefreshAuthorizedInitialKnowledge();

        private void StopObserving()
        {
            if (playerSessionObserver != null)
            {
                playerSessionObserver.Changed -= OnPlayerSessionChanged;
            }

            if (actorOccurrenceBindings == null)
            {
                return;
            }

            for (int index = 0; index < actorOccurrenceBindings.Length; index++)
            {
                if (actorOccurrenceBindings[index] != null)
                {
                    actorOccurrenceBindings[index].Changed -= OnActorOccurrenceChanged;
                }
            }
        }

        private void LogResolution(string profileId, string status, string diagnostic)
        {
            _loggedResolutionStates ??= new HashSet<string>(StringComparer.Ordinal);
            string key = string.Concat(profileId, "|", status, "|", diagnostic);
            if (!_loggedResolutionStates.Add(key))
            {
                return;
            }

            EnsureLoggerInitialized();
            if (string.Equals(status, "Resolved", StringComparison.Ordinal))
            {
                _log.Info(
                    "Authorized initial Actor location source resolved a current Framework occurrence.",
                    LogFields.Field("actorProfileId", profileId),
                    LogFields.Field("resolution", diagnostic),
                    LogFields.Field("continuousTracking", false));
            }
            else
            {
                _log.Debug(
                    "Authorized initial Actor location remains unknown while occurrence evidence is unavailable or ambiguous.",
                    LogFields.Field("actorProfileId", profileId),
                    LogFields.Field("resolutionStatus", status),
                    LogFields.Field("reason", diagnostic));
            }
        }

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PEGALogging.CreateScopedLogger<PEGAActorProfileKnowledgeSource>(
                LogCategory,
                gameObject.name);
            _loggerInitialized = true;
        }
    }
}

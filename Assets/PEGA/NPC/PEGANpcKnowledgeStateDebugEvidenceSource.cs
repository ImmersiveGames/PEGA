#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// Temporary Inspector tool for writing explicitly synthetic evidence into this NPC's Knowledge State.
    /// Remove this component from scene objects before making a player build.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PEGANpcKnowledgeStateDebugEvidenceSource : MonoBehaviour
    {
        private const string LogPrefix = "[PEGA #68 SYNTHETIC DEBUG]";

        [Header("Synthetic identity claims")]
        [SerializeField, InspectorName("Rick identity profile"), Tooltip("Actor Profile used as a synthetic Rick claim for this NPC's active Player Slot subject. The binding supplies only ActorId; it does not verify the player's actual profile.")]
        private ActorProfile _rickIdentityProfile;

        [SerializeField, InspectorName("Second identity profile"), Tooltip("Actor Profile used as a synthetic second identity claim keyed by this NPC root's own ActorId (not its actual profile), and as the conflicting claim for the current player subject.")]
        private ActorProfile _otherCharacterProfile;

        [SerializeField, InspectorName("Synthetic Rick position (m)"), Tooltip("Synthetic last observed position in Unity units. This value is entered here and never read from a Transform.")]
        private Vector3 _syntheticRickPosition = new Vector3(3f, 0f, 5f);

        [Header("Synthetic evidence metadata")]
        [SerializeField, InspectorName("Simulated evidence origin"), Tooltip("Synthetic provenance written into Knowledge State. The selected origin is simulated; this component does not produce real perception or action evidence.")]
        private PEGANpcEvidenceOrigin _syntheticOrigin = PEGANpcEvidenceOrigin.Perception;

        [SerializeField, InspectorName("Baseline time (s)"), Min(0f), Tooltip("Synthetic observation time in seconds for the baseline facts.")]
        private float _baselineObservationTimeSeconds = 10f;

        [SerializeField, InspectorName("Area absence time (s)"), Min(0f), Tooltip("Synthetic observation time in seconds for recording an empty perceived area. Must be later than the baseline.")]
        private float _areaAbsenceObservationTimeSeconds = 15f;

        [SerializeField, InspectorName("Contact loss time (s)"), Min(0f), Tooltip("Synthetic observation time in seconds for losing visual contact. Must be later than the baseline.")]
        private float _contactLossObservationTimeSeconds = 20f;

        [SerializeField, InspectorName("Identity conflict time (s)"), Min(0f), Tooltip("Shared synthetic observation time in seconds for both conflicting identity claims.")]
        private float _identityConflictObservationTimeSeconds = 30f;

        [ContextMenu("Generate Baseline Facts (Synthetic)")]
        private void GenerateBaselineFacts()
        {
            if (!TryGetContext(out PEGANpcKnowledgeState state, out ActorId subjectActorId, out ActorId otherSubjectActorId) ||
                !TryGetProfileId(_rickIdentityProfile, "Rick identity profile", out ActorProfileId rickProfileId) ||
                !TryGetProfileId(_otherCharacterProfile, "second identity profile", out ActorProfileId otherProfileId) ||
                !TryValidateOrigin() ||
                !TryValidateSyntheticInputs())
            {
                return;
            }

            if (rickProfileId == otherProfileId)
            {
                LogError("Rick and the other character must use different ActorProfileIds.");
                return;
            }

            PEGANpcKnowledgeFact<bool> existingPresence = state.GetPerceivedAreaPresence();
            PEGANpcKnowledgeFact<ActorProfileId> existingIdentity = state.GetObservedCharacterIdentity(subjectActorId);
            PEGANpcKnowledgeFact<ActorProfileId> existingOtherIdentity = state.GetObservedCharacterIdentity(otherSubjectActorId);
            PEGANpcKnowledgeFact<bool> existingContact = state.GetRickVisualContact(subjectActorId);
            PEGANpcKnowledgeFact<Vector3> existingPosition = state.GetLastObservedRickPosition(subjectActorId);

            if (IsNewerThan(existingPresence, _baselineObservationTimeSeconds) ||
                IsNewerThan(existingIdentity, _baselineObservationTimeSeconds) ||
                IsNewerThan(existingOtherIdentity, _baselineObservationTimeSeconds) ||
                IsNewerThan(existingContact, _baselineObservationTimeSeconds) ||
                IsNewerThan(existingPosition, _baselineObservationTimeSeconds))
            {
                LogError("Baseline time is older than stored evidence. Stop and restart Play Mode before replaying this scenario.");
                return;
            }

            double observationTime = _baselineObservationTimeSeconds;
            LogWrite("perceivedAreaPresence", state.ObservePerceivedAreaPresence(
                true, _syntheticOrigin, observationTime));
            LogWrite("characterIdentity", state.ObserveCharacterIdentity(
                subjectActorId, rickProfileId, _syntheticOrigin, observationTime));
            LogWrite("characterIdentity (second subject)", state.ObserveCharacterIdentity(
                otherSubjectActorId, otherProfileId, _syntheticOrigin, observationTime));
            LogWrite("rickVisualContact", state.ObserveRickVisualContact(
                subjectActorId, true, _syntheticOrigin, observationTime));
            LogWrite("lastObservedRickPosition", state.ObserveRickPosition(
                subjectActorId, _syntheticRickPosition, _syntheticOrigin, observationTime));

            PEGANpcKnowledgeFact<bool> presence = state.GetPerceivedAreaPresence();
            PEGANpcKnowledgeFact<ActorProfileId> identity = state.GetObservedCharacterIdentity(subjectActorId);
            PEGANpcKnowledgeFact<ActorProfileId> otherIdentity = state.GetObservedCharacterIdentity(otherSubjectActorId);
            PEGANpcKnowledgeFact<bool> contact = state.GetRickVisualContact(subjectActorId);
            PEGANpcKnowledgeFact<Vector3> position = state.GetLastObservedRickPosition(subjectActorId);

            bool passed = IsKnownValue(presence, true, observationTime) &&
                          IsKnownValue(identity, rickProfileId, observationTime) &&
                          IsKnownValue(otherIdentity, otherProfileId, observationTime) &&
                          IsKnownValue(contact, true, observationTime) &&
                          IsKnownValue(position, _syntheticRickPosition, observationTime);

            LogCheck(
                "Baseline facts",
                passed,
                $"observer='{state.gameObject.name}', player subject ActorId='{subjectActorId.StableText}', second subject ActorId='{otherSubjectActorId.StableText}', time={observationTime:0.###}s");
            PrintSnapshot(state, subjectActorId, otherSubjectActorId);
        }

        [ContextMenu("Record Perceived Area Absence (Synthetic)")]
        private void RecordPerceivedAreaAbsence()
        {
            if (!TryGetContext(out PEGANpcKnowledgeState state, out ActorId subjectActorId, out ActorId otherSubjectActorId) ||
                !TryValidateOrigin() ||
                !TryValidateSyntheticInputs())
            {
                return;
            }

            PEGANpcKnowledgeFact<bool> presenceBefore = state.GetPerceivedAreaPresence();
            if (!presenceBefore.TryGetValue(out bool isPresent) || !isPresent)
            {
                LogError("Generate baseline facts first. Perceived area presence must be known true before recording absence.");
                return;
            }

            if (_areaAbsenceObservationTimeSeconds <= presenceBefore.ObservationTimeSeconds)
            {
                LogError("Area-absence time must be later than the current presence evidence. Adjust the Inspector value or restart Play Mode.");
                return;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identityBefore = state.GetObservedCharacterIdentity(subjectActorId);
            PEGANpcKnowledgeFact<ActorProfileId> otherIdentityBefore = state.GetObservedCharacterIdentity(otherSubjectActorId);
            PEGANpcKnowledgeFact<bool> contactBefore = state.GetRickVisualContact(subjectActorId);
            PEGANpcKnowledgeFact<Vector3> positionBefore = state.GetLastObservedRickPosition(subjectActorId);

            LogWrite("perceivedAreaPresence", state.ObservePerceivedAreaPresence(
                false,
                _syntheticOrigin,
                _areaAbsenceObservationTimeSeconds));

            PEGANpcKnowledgeFact<bool> presenceAfter = state.GetPerceivedAreaPresence();
            bool unrelatedFactsPreserved =
                AreSameFact(identityBefore, state.GetObservedCharacterIdentity(subjectActorId)) &&
                AreSameFact(otherIdentityBefore, state.GetObservedCharacterIdentity(otherSubjectActorId)) &&
                AreSameFact(contactBefore, state.GetRickVisualContact(subjectActorId)) &&
                AreSameFact(positionBefore, state.GetLastObservedRickPosition(subjectActorId));
            bool passed = IsKnownValue(presenceAfter, false, _areaAbsenceObservationTimeSeconds) && unrelatedFactsPreserved;
            LogCheck(
                "Perceived area absence is known false",
                passed,
                $"{FormatFact(presenceAfter, value => value.ToString())}; unrelatedFactsPreserved={unrelatedFactsPreserved}");
            PrintSnapshot(state, subjectActorId, otherSubjectActorId);
        }

        [ContextMenu("Record Rick Contact Loss (Synthetic)")]
        private void RecordRickContactLoss()
        {
            if (!TryGetContext(out PEGANpcKnowledgeState state, out ActorId subjectActorId, out ActorId otherSubjectActorId) ||
                !TryValidateOrigin() ||
                !TryValidateSyntheticInputs())
            {
                return;
            }

            PEGANpcKnowledgeFact<bool> contactBefore = state.GetRickVisualContact(subjectActorId);
            PEGANpcKnowledgeFact<Vector3> positionBefore = state.GetLastObservedRickPosition(subjectActorId);
            PEGANpcKnowledgeFact<ActorProfileId> otherIdentityBefore = state.GetObservedCharacterIdentity(otherSubjectActorId);
            if (!contactBefore.TryGetValue(out bool hasContact) || !hasContact || !positionBefore.HasValue)
            {
                LogError("Generate baseline facts first. Contact must be known true and a last observed position must exist.");
                return;
            }

            if (_contactLossObservationTimeSeconds <= contactBefore.ObservationTimeSeconds)
            {
                LogError("Contact-loss time must be later than the current contact evidence. Adjust the Inspector value or restart Play Mode.");
                return;
            }

            LogWrite("rickVisualContact", state.ObserveRickVisualContact(
                subjectActorId,
                false,
                _syntheticOrigin,
                _contactLossObservationTimeSeconds));

            PEGANpcKnowledgeFact<bool> contactAfter = state.GetRickVisualContact(subjectActorId);
            PEGANpcKnowledgeFact<Vector3> positionAfter = state.GetLastObservedRickPosition(subjectActorId);
            PEGANpcKnowledgeFact<ActorProfileId> otherIdentityAfter = state.GetObservedCharacterIdentity(otherSubjectActorId);
            bool positionPreserved = AreSameFact(positionBefore, positionAfter);
            bool otherSubjectPreserved = AreSameFact(otherIdentityBefore, otherIdentityAfter);
            bool passed = IsKnownValue(contactAfter, false, _contactLossObservationTimeSeconds) &&
                          positionPreserved &&
                          otherSubjectPreserved;

            LogCheck(
                "Contact loss preserves last observed position",
                passed,
                $"contact={FormatFact(contactAfter, value => value.ToString())}; positionPreserved={positionPreserved}; otherSubjectPreserved={otherSubjectPreserved}");
            PrintSnapshot(state, subjectActorId, otherSubjectActorId);
        }

        [ContextMenu("Generate Identity Conflict (Rick Then Other Profile)")]
        private void GenerateIdentityConflictRickThenOtherProfile()
        {
            GenerateIdentityConflict(rickProfileFirst: true);
        }

        [ContextMenu("Generate Identity Conflict (Other Profile Then Rick)")]
        private void GenerateIdentityConflictOtherProfileThenRick()
        {
            GenerateIdentityConflict(rickProfileFirst: false);
        }

        [ContextMenu("Print Knowledge Snapshot (Synthetic Debug)")]
        private void PrintKnowledgeSnapshot()
        {
            if (TryGetContext(out PEGANpcKnowledgeState state, out ActorId subjectActorId, out ActorId otherSubjectActorId))
            {
                PrintSnapshot(state, subjectActorId, otherSubjectActorId);
            }
        }

        private void GenerateIdentityConflict(bool rickProfileFirst)
        {
            if (!TryGetContext(out PEGANpcKnowledgeState state, out ActorId subjectActorId, out ActorId otherSubjectActorId) ||
                !TryGetProfileId(_rickIdentityProfile, "Rick identity profile", out ActorProfileId rickProfileId) ||
                !TryGetProfileId(_otherCharacterProfile, "second identity profile", out ActorProfileId conflictingProfileId) ||
                !TryValidateOrigin() ||
                !TryValidateSyntheticInputs())
            {
                return;
            }

            if (rickProfileId == conflictingProfileId)
            {
                LogError("The two Actor Profiles must have different ActorProfileIds to create a contradiction.");
                return;
            }

            PEGANpcKnowledgeFact<ActorProfileId> otherIdentityBefore = state.GetObservedCharacterIdentity(otherSubjectActorId);
            if (!IsKnownValue(otherIdentityBefore, conflictingProfileId, _baselineObservationTimeSeconds))
            {
                LogError("Generate baseline facts first so the second subject identity is already known independently.");
                return;
            }

            PEGANpcKnowledgeFact<ActorProfileId> currentIdentity = state.GetObservedCharacterIdentity(subjectActorId);
            if (currentIdentity.HasObservationTime &&
                currentIdentity.ObservationTimeSeconds >= _identityConflictObservationTimeSeconds)
            {
                LogError("Identity evidence already exists at or after the conflict time. Stop and restart Play Mode before testing the other arrival order.");
                return;
            }

            double observationTime = _identityConflictObservationTimeSeconds;
            ActorProfileId firstProfile = rickProfileFirst ? rickProfileId : conflictingProfileId;
            ActorProfileId secondProfile = rickProfileFirst ? conflictingProfileId : rickProfileId;

            PEGANpcKnowledgeWriteResult firstResult = state.ObserveCharacterIdentity(
                subjectActorId, firstProfile, _syntheticOrigin, observationTime);
            PEGANpcKnowledgeWriteResult secondResult = state.ObserveCharacterIdentity(
                subjectActorId, secondProfile, _syntheticOrigin, observationTime);
            LogWrite("characterIdentity first claim", firstResult);
            LogWrite("characterIdentity second claim", secondResult);

            PEGANpcKnowledgeFact<ActorProfileId> finalIdentity = state.GetObservedCharacterIdentity(subjectActorId);
            PEGANpcKnowledgeFact<ActorProfileId> otherIdentityAfter = state.GetObservedCharacterIdentity(otherSubjectActorId);
            bool passed = finalIdentity.Status == PEGANpcKnowledgeStatus.Invalidated &&
                          (finalIdentity.InvalidationCauses & PEGANpcKnowledgeInvalidationCause.ConflictingEvidence) != 0 &&
                          finalIdentity.HasObservationTime &&
                          finalIdentity.ObservationTimeSeconds == observationTime &&
                          finalIdentity.Origins == _syntheticOrigin &&
                          AreSameFact(otherIdentityBefore, otherIdentityAfter);

            string order = rickProfileFirst ? "Rick profile then second profile" : "second profile then Rick profile";
            LogCheck(
                "Same-time identity conflict",
                passed,
                $"order='{order}', final={FormatFact(finalIdentity, value => value.StableText)}, playerSubject='{subjectActorId.StableText}', secondSubjectUnchanged={AreSameFact(otherIdentityBefore, otherIdentityAfter)}");
            PrintSnapshot(state, subjectActorId, otherSubjectActorId);
        }

        private bool TryGetContext(
            out PEGANpcKnowledgeState state,
            out ActorId subjectActorId,
            out ActorId otherSubjectActorId)
        {
            state = null;
            subjectActorId = default;
            otherSubjectActorId = default;

            if (!Application.isPlaying)
            {
                LogError("Enter Play Mode before using this context-menu action.");
                return false;
            }

            state = GetComponentInParent<PEGANpcKnowledgeState>();
            if (state == null)
            {
                LogError($"No parent NPC root has {nameof(PEGANpcKnowledgeState)}. Attach this tool beneath the existing NPC root; it never adds components.");
                return false;
            }

            ActorOccurrenceReferenceBinding playerReference = state.GetComponent<ActorOccurrenceReferenceBinding>();
            if (playerReference == null)
            {
                LogError($"The Knowledge State owner has no {nameof(ActorOccurrenceReferenceBinding)} for the current player occurrence.");
                return false;
            }

            if (!playerReference.TryGetReference(
                    out ActorOccurrenceReference playerReferenceSelector,
                    out string playerReferenceIssue))
            {
                LogError($"The NPC's occurrence binding must select a Player Slot: {playerReferenceIssue}");
                return false;
            }

            if (playerReferenceSelector.Kind != ActorOccurrenceReferenceKind.PlayerSlot)
            {
                LogError($"The NPC's occurrence binding must select a Player Slot, but selects {playerReferenceSelector.Kind}.");
                return false;
            }

            if (!playerReference.TryGetCurrent(out ActorOccurrenceResolution playerResolution) ||
                !playerResolution.Succeeded)
            {
                LogError($"The NPC's Player Slot binding did not resolve a current player Actor occurrence: {playerResolution.Diagnostic}");
                return false;
            }

            subjectActorId = playerResolution.Occurrence.ActorId;

            ActorDeclaration observerDeclaration = state.GetComponent<ActorDeclaration>();
            if (observerDeclaration == null)
            {
                LogError($"The Knowledge State owner has no {nameof(ActorDeclaration)} to identify its own ActorId.");
                return false;
            }

            if (!observerDeclaration.TryCreateDescriptor(
                    nameof(PEGANpcKnowledgeStateDebugEvidenceSource),
                    out ActorDescriptor observerDescriptor,
                    out ActorSetIssue actorIssue))
            {
                LogError($"The NPC's own ActorDeclaration does not provide a valid ActorId: {actorIssue.Message}");
                return false;
            }

            otherSubjectActorId = observerDescriptor.ActorId;
            if (subjectActorId == otherSubjectActorId)
            {
                LogError("The resolved player ActorId and this NPC's ActorId must be distinct to demonstrate separate identity facts.");
                return false;
            }

            return true;
        }

        private bool TryGetProfileId(ActorProfile profile, string label, out ActorProfileId profileId)
        {
            profileId = default;
            if (profile == null)
            {
                LogError($"Assign an {nameof(ActorProfile)} to the '{label}' field.");
                return false;
            }

            if (!profile.TryGetActorProfileId(out profileId, out string issue))
            {
                LogError($"The '{label}' is invalid: {issue}");
                return false;
            }

            return true;
        }

        private bool TryValidateOrigin()
        {
            if (_syntheticOrigin == PEGANpcEvidenceOrigin.Perception ||
                _syntheticOrigin == PEGANpcEvidenceOrigin.ActionResult)
            {
                return true;
            }

            LogError("Select exactly one supported simulated origin: Perception or ActionResult.");
            return false;
        }

        private bool TryValidateSyntheticInputs()
        {
            if (!IsFinite(_baselineObservationTimeSeconds) || _baselineObservationTimeSeconds < 0f ||
                !IsFinite(_areaAbsenceObservationTimeSeconds) ||
                _areaAbsenceObservationTimeSeconds <= _baselineObservationTimeSeconds ||
                !IsFinite(_contactLossObservationTimeSeconds) ||
                _contactLossObservationTimeSeconds <= _baselineObservationTimeSeconds ||
                !IsFinite(_identityConflictObservationTimeSeconds) ||
                _identityConflictObservationTimeSeconds <= _baselineObservationTimeSeconds ||
                !IsFinite(_syntheticRickPosition.x) ||
                !IsFinite(_syntheticRickPosition.y) ||
                !IsFinite(_syntheticRickPosition.z))
            {
                LogError("Use finite non-negative times, set contact loss and conflict later than the baseline, and enter a finite synthetic position.");
                return false;
            }

            return true;
        }

        private void PrintSnapshot(
            PEGANpcKnowledgeState state,
            ActorId subjectActorId,
            ActorId otherSubjectActorId)
        {
            PEGANpcKnowledgeFact<bool> presence = state.GetPerceivedAreaPresence();
            PEGANpcKnowledgeFact<ActorProfileId> identity = state.GetObservedCharacterIdentity(subjectActorId);
            PEGANpcKnowledgeFact<ActorProfileId> otherIdentity = state.GetObservedCharacterIdentity(otherSubjectActorId);
            PEGANpcKnowledgeFact<bool> contact = state.GetRickVisualContact(subjectActorId);
            PEGANpcKnowledgeFact<Vector3> position = state.GetLastObservedRickPosition(subjectActorId);

            Debug.Log(
                $"{LogPrefix} Snapshot for observer '{state.gameObject.name}', subject ActorId '{subjectActorId.StableText}'\n" +
                $"  perceivedAreaPresence: {FormatFact(presence, value => value.ToString())}\n" +
                $"  characterIdentity: {FormatFact(identity, value => value.StableText)}\n" +
                $"  secondSubjectIdentity ({otherSubjectActorId.StableText}): {FormatFact(otherIdentity, value => value.StableText)}\n" +
                $"  rickVisualContact: {FormatFact(contact, value => value.ToString())}\n" +
                $"  lastObservedRickPosition: {FormatFact(position, value => value.ToString("F2"))}",
                this);
        }

        private void LogWrite(string factName, PEGANpcKnowledgeWriteResult result)
        {
            PEGANpcKnowledgeState state = GetComponentInParent<PEGANpcKnowledgeState>();
            string observerName = state == null ? gameObject.name : state.gameObject.name;
            Debug.Log($"{LogPrefix} Write '{factName}' on observer '{observerName}': {result}.", this);
        }

        private void LogCheck(string checkName, bool passed, string details)
        {
            string message = $"{LogPrefix} {(passed ? "PASS" : "FAIL")} '{checkName}': {details}";
            if (passed)
            {
                Debug.Log(message, this);
            }
            else
            {
                Debug.LogError(message, this);
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"{LogPrefix} {message}", this);
        }

        private static string FormatFact<T>(PEGANpcKnowledgeFact<T> fact, Func<T, string> formatValue)
        {
            string value = fact.TryGetValue(out T storedValue)
                ? formatValue(storedValue)
                : "unavailable";
            string time = fact.HasObservationTime
                ? $"{fact.ObservationTimeSeconds:0.###}s"
                : "unavailable";

            return $"status={fact.Status}, value={value}, time={time}, origins={fact.Origins}, invalidation={fact.InvalidationCauses}";
        }

        private static bool IsKnownValue<T>(PEGANpcKnowledgeFact<T> fact, T expectedValue, double expectedTime)
        {
            return fact.Status == PEGANpcKnowledgeStatus.Known &&
                   fact.TryGetValue(out T actualValue) &&
                   EqualityComparer<T>.Default.Equals(actualValue, expectedValue) &&
                   fact.HasObservationTime &&
                   fact.ObservationTimeSeconds == expectedTime;
        }

        private static bool IsNewerThan<T>(PEGANpcKnowledgeFact<T> fact, double observationTime)
        {
            return fact.HasObservationTime && fact.ObservationTimeSeconds > observationTime;
        }

        private static bool AreSameFact<T>(PEGANpcKnowledgeFact<T> left, PEGANpcKnowledgeFact<T> right)
        {
            if (left.Status != right.Status ||
                left.HasObservationTime != right.HasObservationTime ||
                left.ObservationTimeSeconds != right.ObservationTimeSeconds ||
                left.Origins != right.Origins ||
                left.InvalidationCauses != right.InvalidationCauses)
            {
                return false;
            }

            bool leftHasValue = left.TryGetValue(out T leftValue);
            bool rightHasValue = right.TryGetValue(out T rightValue);
            return leftHasValue == rightHasValue &&
                   (!leftHasValue || EqualityComparer<T>.Default.Equals(leftValue, rightValue));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
#endif

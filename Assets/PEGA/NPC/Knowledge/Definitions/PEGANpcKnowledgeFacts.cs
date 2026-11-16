using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
using UnityEngine.Scripting;
namespace PEGA.NPC.Knowledge.Definitions
{
    public readonly struct PegaNpcCharacterIncapacitationObservation
    {
        public PegaNpcCharacterIncapacitationObservation(
            ActorOccurrenceId actorOccurrenceId,
            bool isIncapacitated,
            long observationSequence)
        {
            ActorOccurrenceId = actorOccurrenceId;
            IsIncapacitated = isIncapacitated;
            ObservationSequence = observationSequence;
        }

        public ActorOccurrenceId ActorOccurrenceId { get; }
        public bool IsIncapacitated { get; }
        public long ObservationSequence { get; }
    }

    /// <summary>Definitions for the current NPC facts; these are adapters over the shared fact mechanism.</summary>
    [Preserve]
    internal static class PegaNpcKnowledgeFacts
    {
        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<byte, PegaNpcPerceivedAreaPresence> PerceivedAreaPresence =
            new PegaKnowledgeFactDefinition<byte, PegaNpcPerceivedAreaPresence>(
                "pega.npc.perceived-area-presence",
                "Perceived area presence",
                new PegaLatestEvidenceKnowledgePolicy<PegaNpcPerceivedAreaPresence>(
                    areEqual: ArePerceivedAreaPresenceEqual,
                    validateValue: value => value.IsValid ? string.Empty : "Perceived presence evidence is invalid."),
                address => address == 0,
                allowsDesignerAuthoredInitialFact: false);

        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<byte, PegaNpcPerceivedAreaPresence> UnresolvedRadialPresence =
            new PegaKnowledgeFactDefinition<byte, PegaNpcPerceivedAreaPresence>(
                "pega.npc.unresolved-radial-presence",
                "Unresolved radial presence",
                new PegaLatestEvidenceKnowledgePolicy<PegaNpcPerceivedAreaPresence>(
                    areEqual: ArePerceivedAreaPresenceEqual,
                    validateValue: value => value.IsValid ? string.Empty : "Unresolved radial presence evidence is invalid."),
                address => address == 0,
                allowsDesignerAuthoredInitialFact: false);

        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<ActorId, bool> IsPlayer =
            new PegaKnowledgeFactDefinition<ActorId, bool>(
                "pega.actor.is-player",
                "Is player",
                new PegaLatestEvidenceKnowledgePolicy<bool>(),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: false);

        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<ActorId, bool> CharacterVisualContact =
            new PegaKnowledgeFactDefinition<ActorId, bool>(
                "pega.actor.character-visual-contact",
                "Character visual contact",
                new PegaLatestEvidenceKnowledgePolicy<bool>(),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: false);

        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<ActorId, PegaNpcCharacterIncapacitationObservation> CharacterIncapacitated =
            new PegaKnowledgeFactDefinition<ActorId, PegaNpcCharacterIncapacitationObservation>(
                "pega.actor.character-incapacitated",
                "Character incapacitated",
                new PegaNpcCharacterIncapacitationKnowledgePolicy(),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: false);

        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<ActorId, Vector3> LastObservedCharacterPosition =
            new PegaKnowledgeFactDefinition<ActorId, Vector3>(
                "pega.actor.last-observed-character-position",
                "Last observed character position",
                new PegaLatestEvidenceKnowledgePolicy<Vector3>(
                    areEqual: AreExactlyEqual,
                    validateValue: value => IsFinite(value)
                        ? string.Empty
                        : "Position contains a non-finite coordinate."),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: true);

        public static bool TryGetCharacterSubject(
            PegaKnowledgeFactChangedEvent change,
            out ActorId subjectActorId)
        {
            subjectActorId = default;
            if (change?.FactKey?.Address is not ActorId address || !address.IsValid)
            {
                return false;
            }

            subjectActorId = address;
            return true;
        }

        public static bool IsCharacterFactChangeFor(
            PegaKnowledgeFactChangedEvent change,
            ActorId subjectActorId) =>
            change != null && subjectActorId.IsValid &&
            (change.IsFor(IsPlayer.For(subjectActorId)) ||
             change.IsFor(CharacterVisualContact.For(subjectActorId)) ||
             change.IsFor(LastObservedCharacterPosition.For(subjectActorId)));

        private sealed class PegaNpcCharacterIncapacitationKnowledgePolicy :
            IPegaKnowledgeFactPolicy<PegaNpcCharacterIncapacitationObservation>
        {
            private readonly PegaLatestEvidenceKnowledgePolicy<PegaNpcCharacterIncapacitationObservation>
                _latestEvidencePolicy = new PegaLatestEvidenceKnowledgePolicy<
                    PegaNpcCharacterIncapacitationObservation>(
                    areEqual: AreSameObservedState,
                    validateValue: value =>
                        value.ActorOccurrenceId.IsValid && value.ObservationSequence > 0
                            ? string.Empty
                            : "Incapacitation evidence requires a valid occurrence and observation sequence.");

            public PegaKnowledgeFactUpdate<PegaNpcCharacterIncapacitationObservation> Observe(
                PegaKnowledgeFact<PegaNpcCharacterIncapacitationObservation> current,
                PegaNpcCharacterIncapacitationObservation value,
                PegaKnowledgeEvidence evidence,
                PegaKnowledgeScope scope)
            {
                if (!value.ActorOccurrenceId.IsValid || value.ObservationSequence <= 0)
                {
                    return new PegaKnowledgeFactUpdate<PegaNpcCharacterIncapacitationObservation>(
                        PegaKnowledgeWriteResult.RejectedInvalid,
                        current,
                        false);
                }

                if (!current.HasObservationTime ||
                    evidence.ObservationTimeSeconds != current.ObservationTimeSeconds)
                {
                    return _latestEvidencePolicy.Observe(current, value, evidence, scope);
                }

                long currentSequence = GetCurrentObservationSequence(current);
                if (value.ObservationSequence < currentSequence)
                {
                    return new PegaKnowledgeFactUpdate<PegaNpcCharacterIncapacitationObservation>(
                        PegaKnowledgeWriteResult.IgnoredStale,
                        current,
                        false);
                }

                if (value.ObservationSequence == currentSequence)
                {
                    return _latestEvidencePolicy.Observe(current, value, evidence, scope);
                }

                return ReplaceAtSameTime(current, value, evidence, scope);
            }

            public PegaKnowledgeFactUpdate<PegaNpcCharacterIncapacitationObservation> Invalidate(
                PegaKnowledgeFact<PegaNpcCharacterIncapacitationObservation> current,
                PegaKnowledgeEvidence evidence,
                PegaKnowledgeScope scope) =>
                _latestEvidencePolicy.Invalidate(current, evidence, scope);

            public PegaKnowledgeFact<PegaNpcCharacterIncapacitationObservation> EvaluateValidity(
                PegaKnowledgeFact<PegaNpcCharacterIncapacitationObservation> current,
                double queryTimeSeconds,
                PegaKnowledgeScope scope) =>
                _latestEvidencePolicy.EvaluateValidity(current, queryTimeSeconds, scope);

            private static PegaKnowledgeFactUpdate<PegaNpcCharacterIncapacitationObservation> ReplaceAtSameTime(
                PegaKnowledgeFact<PegaNpcCharacterIncapacitationObservation> current,
                PegaNpcCharacterIncapacitationObservation value,
                PegaKnowledgeEvidence evidence,
                PegaKnowledgeScope scope)
            {
                bool logicalStateChanged =
                    !current.TryGetValue(out PegaNpcCharacterIncapacitationObservation previous) ||
                    !AreSameObservedState(previous, value);
                var evidenceItems = logicalStateChanged
                    ? new List<PegaKnowledgeEvidence>()
                    : new List<PegaKnowledgeEvidence>(current.Evidence);
                bool evidenceAlreadyPresent = false;
                for (int index = 0; index < evidenceItems.Count; index++)
                {
                    PegaKnowledgeEvidence existing = evidenceItems[index];
                    if (existing.Origin.Equals(evidence.Origin) &&
                        ReferenceEquals(existing.Source, evidence.Source) &&
                        Equals(existing.Payload, evidence.Payload) &&
                        existing.ObservationTimeSeconds == evidence.ObservationTimeSeconds)
                    {
                        evidenceAlreadyPresent = true;
                        break;
                    }
                }

                if (!evidenceAlreadyPresent)
                {
                    evidenceItems.Add(evidence);
                }

                var updated = PegaKnowledgeFact<PegaNpcCharacterIncapacitationObservation>.Create(
                    PegaKnowledgeStatus.Known,
                    true,
                    evidence.ObservationTimeSeconds,
                    evidenceItems,
                    PegaKnowledgeInvalidationCause.None,
                    value,
                    value,
                    true,
                    scope);
                return new PegaKnowledgeFactUpdate<PegaNpcCharacterIncapacitationObservation>(
                    PegaKnowledgeWriteResult.Replaced,
                    updated,
                    logicalStateChanged);
            }

            private static long GetCurrentObservationSequence(
                PegaKnowledgeFact<PegaNpcCharacterIncapacitationObservation> fact)
            {
                if (fact.TryGetValue(out PegaNpcCharacterIncapacitationObservation value))
                {
                    return value.ObservationSequence;
                }

                return fact.HasCandidate ? fact.Candidate.ObservationSequence : 0L;
            }

            private static bool AreSameObservedState(
                PegaNpcCharacterIncapacitationObservation left,
                PegaNpcCharacterIncapacitationObservation right) =>
                left.ActorOccurrenceId == right.ActorOccurrenceId &&
                left.IsIncapacitated == right.IsIncapacitated;
        }

        public static bool HasPerceptionBackedPlayerFact(PegaKnowledgeFact<bool> fact) =>
            fact.TryGetValue(out bool isPlayer) &&
            isPlayer &&
            fact.HasObservationTime &&
            fact.HasOrigin(PegaNpcEvidenceOrigin.Perception);

        public static bool HasPerceptionBackedPlayerFactAtOrBefore(
            PegaKnowledgeFact<bool> fact,
            double observationTimeSeconds) =>
            HasPerceptionBackedPlayerFact(fact) &&
            fact.ObservationTimeSeconds <= observationTimeSeconds + 0.000001d;

        public static bool IsPerceptionContactChange(
            PegaKnowledgeFactChangedEvent change,
            ActorId subjectActorId) =>
            change != null && subjectActorId.IsValid &&
            change.IsFor(CharacterVisualContact.For(subjectActorId)) &&
            change.Evidence.Origin.Is(PegaNpcEvidenceOrigin.Perception);

        private static bool ArePerceivedAreaPresenceEqual(
            PegaNpcPerceivedAreaPresence left,
            PegaNpcPerceivedAreaPresence right) =>
            left.IsEquivalentTo(right);

        private static bool AreExactlyEqual(Vector3 left, Vector3 right) =>
            left.x == right.x && left.y == right.y && left.z == right.z;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

}

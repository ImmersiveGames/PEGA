using System;
using Immersive.Framework.Actors;
using UnityEngine.Scripting;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>Definitions for the current NPC facts; these are adapters over the shared fact mechanism.</summary>
    [Preserve]
    internal static class PEGANpcKnowledgeFacts
    {
        [Preserve]
        public static readonly PEGAKnowledgeFactDefinition<byte, PEGANpcPerceivedAreaPresence> PerceivedAreaPresence =
            new PEGAKnowledgeFactDefinition<byte, PEGANpcPerceivedAreaPresence>(
                "pega.npc.perceived-area-presence",
                "Perceived area presence",
                new PEGALatestEvidenceKnowledgePolicy<PEGANpcPerceivedAreaPresence>(
                    areEqual: ArePerceivedAreaPresenceEqual,
                    validateValue: value => value.IsValid ? string.Empty : "Perceived presence evidence is invalid."),
                address => address == 0,
                allowsDesignerAuthoredInitialFact: false);

        [Preserve]
        public static readonly PEGAKnowledgeFactDefinition<ActorId, bool> IsPlayer =
            new PEGAKnowledgeFactDefinition<ActorId, bool>(
                "pega.actor.is-player",
                "Is player",
                new PEGALatestEvidenceKnowledgePolicy<bool>(),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: false);

        [Preserve]
        public static readonly PEGAKnowledgeFactDefinition<ActorId, bool> CharacterVisualContact =
            new PEGAKnowledgeFactDefinition<ActorId, bool>(
                "pega.actor.character-visual-contact",
                "Character visual contact",
                new PEGALatestEvidenceKnowledgePolicy<bool>(),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: false);

        [Preserve]
        public static readonly PEGAKnowledgeFactDefinition<ActorId, Vector3> LastObservedCharacterPosition =
            new PEGAKnowledgeFactDefinition<ActorId, Vector3>(
                "pega.actor.last-observed-character-position",
                "Last observed character position",
                new PEGALatestEvidenceKnowledgePolicy<Vector3>(
                    areEqual: AreExactlyEqual,
                    validateValue: value => IsFinite(value)
                        ? string.Empty
                        : "Position contains a non-finite coordinate."),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: false);

        public static bool TryGetCharacterSubject(
            PEGAKnowledgeFactChangedEvent change,
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
            PEGAKnowledgeFactChangedEvent change,
            ActorId subjectActorId) =>
            change != null && subjectActorId.IsValid &&
            (change.IsFor(IsPlayer.For(subjectActorId)) ||
             change.IsFor(CharacterVisualContact.For(subjectActorId)) ||
             change.IsFor(LastObservedCharacterPosition.For(subjectActorId)));

        public static bool HasPerceptionBackedPlayerFact(PEGAKnowledgeFact<bool> fact) =>
            fact.TryGetValue(out bool isPlayer) &&
            isPlayer &&
            fact.HasObservationTime &&
            fact.HasOrigin(PEGANpcEvidenceOrigin.Perception);

        public static bool HasPerceptionBackedPlayerFactAtOrBefore(
            PEGAKnowledgeFact<bool> fact,
            double observationTimeSeconds) =>
            HasPerceptionBackedPlayerFact(fact) &&
            fact.ObservationTimeSeconds <= observationTimeSeconds + 0.000001d;

        public static bool IsPerceptionContactChange(
            PEGAKnowledgeFactChangedEvent change,
            ActorId subjectActorId) =>
            change != null && subjectActorId.IsValid &&
            change.IsFor(CharacterVisualContact.For(subjectActorId)) &&
            change.Evidence.Origin.Is(PEGANpcEvidenceOrigin.Perception);

        private static bool ArePerceivedAreaPresenceEqual(
            PEGANpcPerceivedAreaPresence left,
            PEGANpcPerceivedAreaPresence right) =>
            left.IsPresent == right.IsPresent &&
            AreExactlyEqual(left.ObserverPosition, right.ObserverPosition) &&
            left.PerceivedAreaRadiusMeters == right.PerceivedAreaRadiusMeters;

        private static bool AreExactlyEqual(Vector3 left, Vector3 right) =>
            left.x == right.x && left.y == right.y && left.z == right.z;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

}

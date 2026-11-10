using Immersive.Framework.Actors;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
using UnityEngine.Scripting;
namespace PEGA.NPC.Knowledge.Definitions
{
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

using System;
using Immersive.Framework.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    public enum PEGAActorGroupKnowledgeEvidenceOrigin
    {
        SpawnPointPhysicalIngress = 0,
        DesignerAuthoredIdentity = 10,
        AuthorizedInitialLocationSource = 20,
        DesignerAuthoredInitialFact = 30
    }

    /// <summary>Definitions for the current NPC facts; these are adapters over the shared fact mechanism.</summary>
    internal static class PEGANpcKnowledgeFacts
    {
        public static readonly PEGAKnowledgeFactDefinition<byte, PEGANpcPerceivedAreaPresence> PerceivedAreaPresence =
            new PEGAKnowledgeFactDefinition<byte, PEGANpcPerceivedAreaPresence>(
                "Perceived area presence",
                new PEGALatestEvidenceKnowledgePolicy<PEGANpcPerceivedAreaPresence>(
                    areEqual: ArePerceivedAreaPresenceEqual,
                    validateValue: value => value.IsValid ? string.Empty : "Perceived presence evidence is invalid."),
                address => address == 0);

        public static readonly PEGAKnowledgeFactDefinition<ActorId, ActorProfileId> ObservedCharacterIdentity =
            new PEGAKnowledgeFactDefinition<ActorId, ActorProfileId>(
                "Observed character identity",
                new PEGALatestEvidenceKnowledgePolicy<ActorProfileId>(
                    areEqual: (left, right) => left.Equals(right),
                    validateValue: value => value.IsValid ? string.Empty : "ActorProfileId is invalid."),
                address => address.IsValid);

        public static readonly PEGAKnowledgeFactDefinition<ActorId, bool> CharacterVisualContact =
            new PEGAKnowledgeFactDefinition<ActorId, bool>(
                "Character visual contact",
                new PEGALatestEvidenceKnowledgePolicy<bool>(),
                address => address.IsValid);

        public static readonly PEGAKnowledgeFactDefinition<ActorId, Vector3> LastObservedCharacterPosition =
            new PEGAKnowledgeFactDefinition<ActorId, Vector3>(
                "Last observed character position",
                new PEGALatestEvidenceKnowledgePolicy<Vector3>(
                    areEqual: AreExactlyEqual,
                    validateValue: value => IsFinite(value)
                        ? string.Empty
                        : "Position contains a non-finite coordinate."),
                address => address.IsValid);

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
            (change.IsFor(ObservedCharacterIdentity.For(subjectActorId)) ||
             change.IsFor(CharacterVisualContact.For(subjectActorId)) ||
             change.IsFor(LastObservedCharacterPosition.For(subjectActorId)));

        public static bool IsIdentityOrPositionChangeFor(
            PEGAKnowledgeFactChangedEvent change,
            ActorId subjectActorId) =>
            change != null && subjectActorId.IsValid &&
            (change.IsFor(ObservedCharacterIdentity.For(subjectActorId)) ||
             change.IsFor(LastObservedCharacterPosition.For(subjectActorId)));

        public static bool IsIdentityOrContactChangeFor(
            PEGAKnowledgeFactChangedEvent change,
            ActorId subjectActorId) =>
            change != null && subjectActorId.IsValid &&
            (change.IsFor(ObservedCharacterIdentity.For(subjectActorId)) ||
             change.IsFor(CharacterVisualContact.For(subjectActorId)));

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

    /// <summary>Current Group fact definitions are owned by Group-specific producers, not by the shared store.</summary>
    internal static class PEGAActorGroupKnowledgeFacts
    {
        public static readonly PEGAKnowledgeFactDefinition<ScriptableObject, ScriptableObject> EntityIdentity =
            new PEGAKnowledgeFactDefinition<ScriptableObject, ScriptableObject>(
                "Entity identity",
                new PEGAStructuralKnowledgePolicy<ScriptableObject>(
                    value => value != null ? string.Empty : "Entity identity is required."),
                address => address != null);

        public static readonly PEGAKnowledgeFactDefinition<ScriptableObject, Vector3> EntityInitialLocation =
            new PEGAKnowledgeFactDefinition<ScriptableObject, Vector3>(
                "Entity initial location",
                new PEGAStructuralKnowledgePolicy<Vector3>(
                    value => IsFinite(value) ? string.Empty : "Initial location is not finite."),
                address => address != null);

        public static readonly PEGAKnowledgeFactDefinition<string, PEGANpcPlanningTarget> SpawnPointEscapeTarget =
            new PEGAKnowledgeFactDefinition<string, PEGANpcPlanningTarget>(
                "Spawn Point escape target",
                new PEGAStructuralKnowledgePolicy<PEGANpcPlanningTarget>(
                    value => value.IsValid ? string.Empty : "Escape target is invalid."),
                address => !string.IsNullOrWhiteSpace(address));

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

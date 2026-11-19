using System;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using UnityEngine;

namespace PEGA.Attacks.Runtime
{
    public readonly struct PegaActorAttackIntent
    {
        private PegaActorAttackIntent(bool isRequested, ActorOccurrenceId? requestedTargetOccurrenceId)
        {
            IsRequested = isRequested;
            RequestedTargetOccurrenceId = requestedTargetOccurrenceId;
        }

        public bool IsRequested { get; }
        public ActorOccurrenceId? RequestedTargetOccurrenceId { get; }
        public static PegaActorAttackIntent None => default;
        public static PegaActorAttackIntent Request => new PegaActorAttackIntent(true, null);

        public static PegaActorAttackIntent RequestFor(ActorOccurrenceId targetOccurrenceId) =>
            new PegaActorAttackIntent(true, targetOccurrenceId);
    }

    public interface IPegaActorAttackIntentSource
    {
        bool TryGetIntent(out PegaActorAttackIntent intent);
    }

    public enum PegaActorAttackResultKind
    {
        Miss = 0,
        Hit = 10,
        Unavailable = 20
    }

    public readonly struct PegaActorAttackContact
    {
        internal PegaActorAttackContact(
            ActorOccurrence occurrence,
            PegaActorImpactResolution impactResolution)
        {
            Occurrence = occurrence;
            ActorId = occurrence.ActorId;
            OccurrenceId = occurrence.Id;
            ActorRoot = occurrence.ActorRoot;
            ImpactResolution = impactResolution;
        }

        public ActorOccurrence Occurrence { get; }
        public ActorId ActorId { get; }
        public ActorOccurrenceId OccurrenceId { get; }
        public GameObject ActorRoot { get; }
        public PegaActorImpactResolution ImpactResolution { get; }
    }

    public enum PegaActorImpactOutcome
    {
        Unavailable = 0,
        RemainedActive = 10,
        Incapacitated = 20,
        AlreadyIncapacitated = 30,
        Protected = 40
    }

    public readonly struct PegaActorImpactResolution
    {
        internal PegaActorImpactResolution(
            PegaActorImpactOutcome outcome,
            float attackCapacity,
            float resistance,
            int droppedItemCount,
            int? carriedItemCountBeforeImpact,
            bool knockbackApplied,
            string diagnostic,
            double attackProtectionEndsAtTimeSeconds = 0d)
        {
            Outcome = outcome;
            AttackCapacity = attackCapacity;
            Resistance = resistance;
            DroppedItemCount = droppedItemCount;
            CarriedItemCountBeforeImpact = carriedItemCountBeforeImpact;
            KnockbackApplied = knockbackApplied;
            Diagnostic = diagnostic ?? string.Empty;
            AttackProtectionEndsAtTimeSeconds = attackProtectionEndsAtTimeSeconds;
        }

        public PegaActorImpactOutcome Outcome { get; }
        public float AttackCapacity { get; }
        public float Resistance { get; }
        public int DroppedItemCount { get; }
        public int? CarriedItemCountBeforeImpact { get; }
        public bool KnockbackApplied { get; }
        public string Diagnostic { get; }
        public double AttackProtectionEndsAtTimeSeconds { get; }
        public bool IsIncapacitated =>
            Outcome == PegaActorImpactOutcome.Incapacitated ||
            Outcome == PegaActorImpactOutcome.AlreadyIncapacitated;
        public bool BecameIncapacitated => Outcome == PegaActorImpactOutcome.Incapacitated;
    }

    public sealed class PegaActorImpactResolvedEvent : IEvent
    {
        internal PegaActorImpactResolvedEvent(
            ActorId aggressorActorId,
            ActorOccurrenceId aggressorOccurrenceId,
            ActorOccurrence targetOccurrence,
            PegaActorImpactResolution resolution)
        {
            AggressorActorId = aggressorActorId;
            AggressorOccurrenceId = aggressorOccurrenceId;
            TargetOccurrence = targetOccurrence;
            Resolution = resolution;
        }

        public ActorId AggressorActorId { get; }
        public ActorOccurrenceId AggressorOccurrenceId { get; }
        public ActorOccurrence TargetOccurrence { get; }
        public GameObject TargetActorRoot => TargetOccurrence.ActorRoot;
        public ActorId TargetActorId => TargetOccurrence.ActorId;
        public ActorOccurrenceId TargetOccurrenceId => TargetOccurrence.Id;
        public PegaActorImpactResolution Resolution { get; }
    }

    public readonly struct PegaActorAttackResolution
    {
        private readonly IReadOnlyList<PegaActorAttackContact> _contacts;

        internal PegaActorAttackResolution(
            long attemptId,
            PegaActorAttackResultKind result,
            Vector3 origin,
            Vector3 direction,
            IReadOnlyList<PegaActorAttackContact> contacts,
            string diagnostic,
            double resolvedAtTimeSeconds)
        {
            AttemptId = attemptId;
            Result = result;
            Origin = origin;
            Direction = direction;
            _contacts = contacts ?? Array.Empty<PegaActorAttackContact>();
            Diagnostic = diagnostic ?? string.Empty;
            ResolvedAtTimeSeconds = resolvedAtTimeSeconds;
        }

        public long AttemptId { get; }
        public PegaActorAttackResultKind Result { get; }
        public bool IsHit => Result == PegaActorAttackResultKind.Hit;
        public bool IsAvailable => Result != PegaActorAttackResultKind.Unavailable;
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public IReadOnlyList<PegaActorAttackContact> Contacts =>
            _contacts ?? Array.Empty<PegaActorAttackContact>();
        public string Diagnostic { get; }
        public double ResolvedAtTimeSeconds { get; }
    }

    public sealed class PegaActorAttackAttemptedEvent : IEvent
    {
        internal PegaActorAttackAttemptedEvent(
            long attemptId,
            GameObject actorRoot,
            Vector3 origin,
            Vector3 direction,
            ActorOccurrenceId? requestedTargetOccurrenceId,
            double attemptedAtTimeSeconds)
        {
            AttemptId = attemptId;
            ActorRoot = actorRoot;
            Origin = origin;
            Direction = direction;
            RequestedTargetOccurrenceId = requestedTargetOccurrenceId;
            AttemptedAtTimeSeconds = attemptedAtTimeSeconds;
        }

        public long AttemptId { get; }
        public GameObject ActorRoot { get; }
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public ActorOccurrenceId? RequestedTargetOccurrenceId { get; }
        public double AttemptedAtTimeSeconds { get; }
    }

    public sealed class PegaActorAttackResolvedEvent : IEvent
    {
        internal PegaActorAttackResolvedEvent(PegaActorAttackResolution resolution) =>
            Resolution = resolution;

        public PegaActorAttackResolution Resolution { get; }
    }
}

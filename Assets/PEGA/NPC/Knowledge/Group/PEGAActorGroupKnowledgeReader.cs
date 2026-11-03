using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using ImmersiveGames.Goap;
using UnityEngine;

namespace PEGA.NPC
{
    public enum PEGAActorGroupKnowledgeAccessStatus
    {
        Authorized,
        NotMember,
        GroupUnavailable,
        AuthorityUnavailable,
        InvalidActor,
        AmbiguousAuthority
    }

    /// <summary>Separates authorization from fact status so Unknown can mean absent or denied without exposing data.</summary>
    public readonly struct PEGAActorGroupKnowledgeReadResult<TValue>
    {
        internal PEGAActorGroupKnowledgeReadResult(
            PEGAActorGroupKnowledgeAccessStatus accessStatus,
            PEGAKnowledgeFact<TValue> fact)
        {
            AccessStatus = accessStatus;
            Fact = fact;
        }

        public PEGAActorGroupKnowledgeAccessStatus AccessStatus { get; }

        public PEGAKnowledgeFact<TValue> Fact { get; }

        public bool IsAuthorized => AccessStatus == PEGAActorGroupKnowledgeAccessStatus.Authorized;
    }

    /// <summary>Instantâneo somente para leitura dos fatos pertencentes a um Group autorizado.</summary>
    public readonly struct PEGAActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>
    {
        internal PEGAActorGroupKnowledgeFactSetSnapshot(
            GroupId groupId,
            IReadOnlyList<PEGAKnowledgeFactSnapshot<TAddress, TValue>> facts)
        {
            GroupId = groupId;
            Facts = facts ?? Array.Empty<PEGAKnowledgeFactSnapshot<TAddress, TValue>>();
        }

        public GroupId GroupId { get; }

        public IReadOnlyList<PEGAKnowledgeFactSnapshot<TAddress, TValue>> Facts { get; }
    }

    /// <summary>Instantâneo dos fatos de Groups vivos nos quais este Actor possui membership atual.</summary>
    public readonly struct PEGAActorGroupKnowledgeFactSetReadResult<TAddress, TValue>
    {
        internal PEGAActorGroupKnowledgeFactSetReadResult(
            PEGAActorGroupKnowledgeAccessStatus accessStatus,
            IReadOnlyList<PEGAActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>> groups,
            string diagnostic)
        {
            AccessStatus = accessStatus;
            Groups = groups ?? Array.Empty<PEGAActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>>();
            Diagnostic = diagnostic ?? string.Empty;
        }

        public PEGAActorGroupKnowledgeAccessStatus AccessStatus { get; }

        public IReadOnlyList<PEGAActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>> Groups { get; }

        public string Diagnostic { get; }

        public bool IsAuthorized => AccessStatus == PEGAActorGroupKnowledgeAccessStatus.Authorized;
    }

    /// <summary>Reads Group Knowledge through explicit owner references and current Framework membership.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorDeclaration))]
    [AddComponentMenu("PEGA/Knowledge/Actor Group Knowledge Reader")]
    public sealed class PEGAActorGroupKnowledgeReader : MonoBehaviour, IGoapKnowledgeCapability
    {
        [SerializeField]
        [Tooltip("PEGA Group Knowledge owners that can be consulted by this Actor. Each owner checks live Framework membership before returning a fact.")]
        private PEGAActorGroupKnowledgeState[] authorityStates = Array.Empty<PEGAActorGroupKnowledgeState>();

        /// <summary>Consults one Group fact; denied and unavailable results contain an empty Unknown fact.</summary>
        public PEGAActorGroupKnowledgeReadResult<TValue> ReadGroupFact<TAddress, TValue>(
            GroupId groupId,
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds)
        {
            ValidateQuery(key, queryTimeSeconds);
            ActorDeclaration actor = GetComponent<ActorDeclaration>();
            if (!TryReadActorId(actor, out ActorId actorId))
            {
                return Empty<TValue>(PEGAActorGroupKnowledgeAccessStatus.InvalidActor);
            }

            if (authorityStates == null || authorityStates.Length == 0)
            {
                return Empty<TValue>(PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable);
            }

            var visited = new HashSet<PEGAActorGroupKnowledgeState>();
            int activeAuthorities = 0;
            int availableOwners = 0;
            int ownershipClaims = 0;
            PEGAActorGroupKnowledgeReadResult<TValue> selected = default;
            for (int index = 0; index < authorityStates.Length; index++)
            {
                PEGAActorGroupKnowledgeState authority = authorityStates[index];
                if (authority == null || !authority.isActiveAndEnabled || !visited.Add(authority))
                {
                    continue;
                }

                activeAuthorities++;
                if (authority.IsAvailable)
                {
                    availableOwners++;
                }

                PEGAActorGroupKnowledgeReadResult<TValue> result = authority.ReadGroupFact(
                    actorId,
                    groupId,
                    key,
                    queryTimeSeconds);
                if (result.AccessStatus == PEGAActorGroupKnowledgeAccessStatus.GroupUnavailable)
                {
                    continue;
                }

                if (result.AccessStatus == PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable &&
                    !authority.HasRegisteredGroup(groupId))
                {
                    continue;
                }

                ownershipClaims++;
                selected = result;
                if (ownershipClaims > 1)
                {
                    return Empty<TValue>(PEGAActorGroupKnowledgeAccessStatus.AmbiguousAuthority);
                }
            }

            if (ownershipClaims == 1)
            {
                return selected;
            }

            return Empty<TValue>(activeAuthorities == 0
                ? PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable
                : availableOwners == 0
                    ? PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable
                    : PEGAActorGroupKnowledgeAccessStatus.GroupUnavailable);
        }

        /// <summary>Submits a fact to the unique owning Group authority, which checks this Actor's current membership.</summary>
        public PEGAActorGroupKnowledgeAccessStatus SubmitGroupFact<TAddress, TValue>(
            GroupId groupId,
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult writeResult)
        {
            writeResult = PEGAKnowledgeWriteResult.RejectedInvalid;
            if (!groupId.IsValid || !key.IsValid || !evidence.IsValid)
            {
                return PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
            }

            ActorDeclaration actor = GetComponent<ActorDeclaration>();
            if (!TryReadActorId(actor, out ActorId actorId))
            {
                return PEGAActorGroupKnowledgeAccessStatus.InvalidActor;
            }

            if (authorityStates == null || authorityStates.Length == 0)
            {
                return PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
            }

            PEGAActorGroupKnowledgeState selectedOwner = null;
            var visited = new HashSet<PEGAActorGroupKnowledgeState>();
            for (int index = 0; index < authorityStates.Length; index++)
            {
                PEGAActorGroupKnowledgeState authority = authorityStates[index];
                if (authority == null || !authority.isActiveAndEnabled || !visited.Add(authority) ||
                    !authority.HasRegisteredGroup(groupId))
                {
                    continue;
                }

                if (selectedOwner != null)
                {
                    return PEGAActorGroupKnowledgeAccessStatus.AmbiguousAuthority;
                }

                selectedOwner = authority;
            }

            if (selectedOwner == null)
            {
                return PEGAActorGroupKnowledgeAccessStatus.GroupUnavailable;
            }

            return selectedOwner.ObserveFactForActor(
                actorId,
                groupId,
                key,
                value,
                evidence,
                out writeResult);
        }

        /// <summary>
        /// Enumera fatos tipados somente de Groups retornados pela consulta de membership efetiva do Framework.
        /// Cada resultado preserva o GroupId proprietário; authorities conflitantes falham como ambíguas.
        /// </summary>
        public PEGAActorGroupKnowledgeFactSetReadResult<TAddress, TValue> ReadGroupFacts<TAddress, TValue>(
            PEGAKnowledgeFactDefinition<TAddress, TValue> definition,
            double queryTimeSeconds)
        {
            ValidateDefinitionQuery(definition, queryTimeSeconds);
            ActorDeclaration actor = GetComponent<ActorDeclaration>();
            if (!TryReadActorId(actor, out ActorId actorId))
            {
                return EmptyFactSets<TAddress, TValue>(PEGAActorGroupKnowledgeAccessStatus.InvalidActor);
            }

            if (authorityStates == null || authorityStates.Length == 0)
            {
                return EmptyFactSets<TAddress, TValue>(PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable);
            }

            var visitedAuthorities = new HashSet<PEGAActorGroupKnowledgeState>();
            var seenGroups = new HashSet<GroupId>();
            var groups = new List<PEGAActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>>();
            int activeAuthorities = 0;
            for (int index = 0; index < authorityStates.Length; index++)
            {
                PEGAActorGroupKnowledgeState authority = authorityStates[index];
                if (authority == null || !authority.isActiveAndEnabled || !visitedAuthorities.Add(authority))
                {
                    continue;
                }

                activeAuthorities++;
                if (!authority.TryReadGroupFactsForActor(
                        actorId,
                        definition,
                        queryTimeSeconds,
                        out IReadOnlyList<PEGAActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>> authorityGroups,
                        out PEGAActorGroupKnowledgeAccessStatus accessStatus,
                        out string diagnostic))
                {
                    return EmptyFactSets<TAddress, TValue>(
                        accessStatus,
                        string.IsNullOrWhiteSpace(diagnostic)
                            ? "A Group Knowledge authority could not complete the live membership read."
                            : diagnostic);
                }

                for (int groupIndex = 0; groupIndex < authorityGroups.Count; groupIndex++)
                {
                    PEGAActorGroupKnowledgeFactSetSnapshot<TAddress, TValue> group = authorityGroups[groupIndex];
                    if (!seenGroups.Add(group.GroupId))
                    {
                        return EmptyFactSets<TAddress, TValue>(
                            PEGAActorGroupKnowledgeAccessStatus.AmbiguousAuthority,
                            $"More than one Group Knowledge authority claims live GroupId '{group.GroupId.StableText}'.");
                    }

                    groups.Add(group);
                }
            }

            if (activeAuthorities == 0)
            {
                return EmptyFactSets<TAddress, TValue>(PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable);
            }

            return new PEGAActorGroupKnowledgeFactSetReadResult<TAddress, TValue>(
                PEGAActorGroupKnowledgeAccessStatus.Authorized,
                Array.AsReadOnly(groups.ToArray()),
                string.Empty);
        }

        private static PEGAActorGroupKnowledgeReadResult<TValue> Empty<TValue>(
            PEGAActorGroupKnowledgeAccessStatus accessStatus) =>
            new PEGAActorGroupKnowledgeReadResult<TValue>(
                accessStatus,
                PEGAKnowledgeFact<TValue>.Unknown(PEGAKnowledgeScope.Group));

        private static PEGAActorGroupKnowledgeFactSetReadResult<TAddress, TValue> EmptyFactSets<TAddress, TValue>(
            PEGAActorGroupKnowledgeAccessStatus accessStatus,
            string diagnostic = "") =>
            new PEGAActorGroupKnowledgeFactSetReadResult<TAddress, TValue>(
                accessStatus,
                Array.Empty<PEGAActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>>(),
                diagnostic);

        private static bool TryReadActorId(ActorDeclaration actor, out ActorId actorId)
        {
            actorId = default;
            if (actor == null)
            {
                return false;
            }

            try
            {
                actorId = actor.ActorId;
                return actorId.IsValid;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void ValidateQuery<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds)
        {
            if (!key.IsValid)
            {
                throw new ArgumentException("A valid typed fact key is required.", nameof(key));
            }

            if (double.IsNaN(queryTimeSeconds) || double.IsInfinity(queryTimeSeconds) || queryTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(queryTimeSeconds));
            }
        }

        private static void ValidateDefinitionQuery<TAddress, TValue>(
            PEGAKnowledgeFactDefinition<TAddress, TValue> definition,
            double queryTimeSeconds)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (double.IsNaN(queryTimeSeconds) || double.IsInfinity(queryTimeSeconds) || queryTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(queryTimeSeconds));
            }
        }
    }
}

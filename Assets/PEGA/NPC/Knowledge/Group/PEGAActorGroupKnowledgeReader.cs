using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
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

    /// <summary>Reads Group Knowledge through explicit owner references and current Framework membership.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorDeclaration))]
    [AddComponentMenu("PEGA/Knowledge/Actor Group Knowledge Reader")]
    public sealed class PEGAActorGroupKnowledgeReader : MonoBehaviour
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

        private static PEGAActorGroupKnowledgeReadResult<TValue> Empty<TValue>(
            PEGAActorGroupKnowledgeAccessStatus accessStatus) =>
            new PEGAActorGroupKnowledgeReadResult<TValue>(
                accessStatus,
                PEGAKnowledgeFact<TValue>.Unknown(PEGAKnowledgeScope.Group));

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
    }
}

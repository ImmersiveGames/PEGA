using System;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.GOAP;
using PEGA.NPC.Actions.Runtime;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using UnityEngine;
namespace PEGA.NPC.Knowledge.Group
{
    public enum PegaActorGroupKnowledgeAccessStatus
    {
        Authorized,
        NotMember,
        GroupUnavailable,
        AuthorityUnavailable,
        InvalidActor,
        AmbiguousAuthority
    }

    /// <summary>Separates authorization from fact status so Unknown can mean absent or denied without exposing data.</summary>
    public readonly struct PegaActorGroupKnowledgeReadResult<TValue>
    {
        internal PegaActorGroupKnowledgeReadResult(
            PegaActorGroupKnowledgeAccessStatus accessStatus,
            PegaKnowledgeFact<TValue> fact)
        {
            AccessStatus = accessStatus;
            Fact = fact;
        }

        public PegaActorGroupKnowledgeAccessStatus AccessStatus { get; }

        public PegaKnowledgeFact<TValue> Fact { get; }

        public bool IsAuthorized => AccessStatus == PegaActorGroupKnowledgeAccessStatus.Authorized;
    }

    /// <summary>Instantâneo somente para leitura dos fatos pertencentes a um Group autorizado.</summary>
    public readonly struct PegaActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>
    {
        internal PegaActorGroupKnowledgeFactSetSnapshot(
            GroupId groupId,
            IReadOnlyList<PegaKnowledgeFactSnapshot<TAddress, TValue>> facts)
        {
            GroupId = groupId;
            Facts = facts ?? Array.Empty<PegaKnowledgeFactSnapshot<TAddress, TValue>>();
        }

        public GroupId GroupId { get; }

        public IReadOnlyList<PegaKnowledgeFactSnapshot<TAddress, TValue>> Facts { get; }
    }

    /// <summary>Instantâneo dos fatos de Groups vivos nos quais este Actor possui membership atual.</summary>
    public readonly struct PegaActorGroupKnowledgeFactSetReadResult<TAddress, TValue>
    {
        internal PegaActorGroupKnowledgeFactSetReadResult(
            PegaActorGroupKnowledgeAccessStatus accessStatus,
            IReadOnlyList<PegaActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>> groups,
            string diagnostic)
        {
            AccessStatus = accessStatus;
            Groups = groups ?? Array.Empty<PegaActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>>();
            Diagnostic = diagnostic ?? string.Empty;
        }

        public PegaActorGroupKnowledgeAccessStatus AccessStatus { get; }

        public IReadOnlyList<PegaActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>> Groups { get; }

        public string Diagnostic { get; }

        public bool IsAuthorized => AccessStatus == PegaActorGroupKnowledgeAccessStatus.Authorized;
    }

    /// <summary>Reads Group Knowledge through explicit owner references and current Framework membership.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorDeclaration))]
    [AddComponentMenu("PEGA/Knowledge/Actor Group Knowledge Reader")]
    public sealed class PegaActorGroupKnowledgeReader : MonoBehaviour,
        IGoapKnowledgeCapability,
        IPegaNpcPlanningInputChangeSource
    {
        private const string LogCategory = "PEGA.Gameplay.Knowledge";

        [SerializeField]
        [Tooltip("PEGA Group Knowledge owners that can be consulted by this Actor. Each owner checks live Framework membership before returning a fact.")]
        private PegaActorGroupKnowledgeState[] authorityStates = Array.Empty<PegaActorGroupKnowledgeState>();

        [NonSerialized]
        private EventBus<PegaNpcPlanningInputsChangedEvent> _planningInputChangeEvents;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        public IEventBinding SubscribeToPlanningInputChanges(
            Action<PegaNpcPlanningInputsChangedEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsurePlanningInputChangeEventsInitialized();
            return _planningInputChangeEvents.Subscribe(change =>
            {
                try
                {
                    handler(change);
                }
                catch (Exception exception)
                {
                    LogPlanningInputChangeSubscriberFailure(handler, exception);
                }
            });
        }

        public bool UnsubscribeFromPlanningInputChanges(IEventBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            return _planningInputChangeEvents != null &&
                _planningInputChangeEvents.Unsubscribe(binding);
        }

        internal void NotifyPlanningInputsChanged()
        {
            EnsurePlanningInputChangeEventsInitialized();
            _planningInputChangeEvents.Publish(new PegaNpcPlanningInputsChangedEvent());
        }

        /// <summary>Consults one Group fact; denied and unavailable results contain an empty Unknown fact.</summary>
        public PegaActorGroupKnowledgeReadResult<TValue> ReadGroupFact<TAddress, TValue>(
            GroupId groupId,
            PegaKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds)
        {
            ValidateQuery(key, queryTimeSeconds);
            ActorDeclaration actor = GetComponent<ActorDeclaration>();
            if (!TryReadActorId(actor, out ActorId actorId))
            {
                return Empty<TValue>(PegaActorGroupKnowledgeAccessStatus.InvalidActor);
            }

            if (authorityStates == null || authorityStates.Length == 0)
            {
                return Empty<TValue>(PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable);
            }

            var visited = new HashSet<PegaActorGroupKnowledgeState>();
            int activeAuthorities = 0;
            int availableOwners = 0;
            int ownershipClaims = 0;
            PegaActorGroupKnowledgeReadResult<TValue> selected = default;
            for (int index = 0; index < authorityStates.Length; index++)
            {
                PegaActorGroupKnowledgeState authority = authorityStates[index];
                if (authority == null || !authority.isActiveAndEnabled || !visited.Add(authority))
                {
                    continue;
                }

                activeAuthorities++;
                if (authority.IsAvailable)
                {
                    availableOwners++;
                }

                PegaActorGroupKnowledgeReadResult<TValue> result = authority.ReadGroupFact(
                    actorId,
                    groupId,
                    key,
                    queryTimeSeconds);
                if (result.AccessStatus == PegaActorGroupKnowledgeAccessStatus.GroupUnavailable)
                {
                    continue;
                }

                if (result.AccessStatus == PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable &&
                    !authority.HasRegisteredGroup(groupId))
                {
                    continue;
                }

                ownershipClaims++;
                selected = result;
                if (ownershipClaims > 1)
                {
                    return Empty<TValue>(PegaActorGroupKnowledgeAccessStatus.AmbiguousAuthority);
                }
            }

            if (ownershipClaims == 1)
            {
                return selected;
            }

            return Empty<TValue>(activeAuthorities == 0
                ? PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable
                : availableOwners == 0
                    ? PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable
                    : PegaActorGroupKnowledgeAccessStatus.GroupUnavailable);
        }

        /// <summary>Submits a fact to the unique owning Group authority, which checks this Actor's current membership.</summary>
        public PegaActorGroupKnowledgeAccessStatus SubmitGroupFact<TAddress, TValue>(
            GroupId groupId,
            PegaKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PegaKnowledgeEvidence evidence,
            out PegaKnowledgeWriteResult writeResult)
        {
            writeResult = PegaKnowledgeWriteResult.RejectedInvalid;
            if (!groupId.IsValid || !key.IsValid || !evidence.IsValid)
            {
                return PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
            }

            ActorDeclaration actor = GetComponent<ActorDeclaration>();
            if (!TryReadActorId(actor, out ActorId actorId))
            {
                return PegaActorGroupKnowledgeAccessStatus.InvalidActor;
            }

            if (authorityStates == null || authorityStates.Length == 0)
            {
                return PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
            }

            PegaActorGroupKnowledgeState selectedOwner = null;
            var visited = new HashSet<PegaActorGroupKnowledgeState>();
            for (int index = 0; index < authorityStates.Length; index++)
            {
                PegaActorGroupKnowledgeState authority = authorityStates[index];
                if (authority == null || !authority.isActiveAndEnabled || !visited.Add(authority) ||
                    !authority.HasRegisteredGroup(groupId))
                {
                    continue;
                }

                if (selectedOwner != null)
                {
                    return PegaActorGroupKnowledgeAccessStatus.AmbiguousAuthority;
                }

                selectedOwner = authority;
            }

            if (selectedOwner == null)
            {
                return PegaActorGroupKnowledgeAccessStatus.GroupUnavailable;
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
        public PegaActorGroupKnowledgeFactSetReadResult<TAddress, TValue> ReadGroupFacts<TAddress, TValue>(
            PegaKnowledgeFactDefinition<TAddress, TValue> definition,
            double queryTimeSeconds)
        {
            ValidateDefinitionQuery(definition, queryTimeSeconds);
            ActorDeclaration actor = GetComponent<ActorDeclaration>();
            if (!TryReadActorId(actor, out ActorId actorId))
            {
                return EmptyFactSets<TAddress, TValue>(PegaActorGroupKnowledgeAccessStatus.InvalidActor);
            }

            if (authorityStates == null || authorityStates.Length == 0)
            {
                return EmptyFactSets<TAddress, TValue>(PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable);
            }

            var visitedAuthorities = new HashSet<PegaActorGroupKnowledgeState>();
            var seenGroups = new HashSet<GroupId>();
            var groups = new List<PegaActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>>();
            int activeAuthorities = 0;
            for (int index = 0; index < authorityStates.Length; index++)
            {
                PegaActorGroupKnowledgeState authority = authorityStates[index];
                if (authority == null || !authority.isActiveAndEnabled || !visitedAuthorities.Add(authority))
                {
                    continue;
                }

                activeAuthorities++;
                if (!authority.TryReadGroupFactsForActor(
                        actorId,
                        definition,
                        queryTimeSeconds,
                        out IReadOnlyList<PegaActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>> authorityGroups,
                        out PegaActorGroupKnowledgeAccessStatus accessStatus,
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
                    PegaActorGroupKnowledgeFactSetSnapshot<TAddress, TValue> group = authorityGroups[groupIndex];
                    if (!seenGroups.Add(group.GroupId))
                    {
                        return EmptyFactSets<TAddress, TValue>(
                            PegaActorGroupKnowledgeAccessStatus.AmbiguousAuthority,
                            $"More than one Group Knowledge authority claims live GroupId '{group.GroupId.StableText}'.");
                    }

                    groups.Add(group);
                }
            }

            if (activeAuthorities == 0)
            {
                return EmptyFactSets<TAddress, TValue>(PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable);
            }

            return new PegaActorGroupKnowledgeFactSetReadResult<TAddress, TValue>(
                PegaActorGroupKnowledgeAccessStatus.Authorized,
                Array.AsReadOnly(groups.ToArray()),
                string.Empty);
        }

        private static PegaActorGroupKnowledgeReadResult<TValue> Empty<TValue>(
            PegaActorGroupKnowledgeAccessStatus accessStatus) =>
            new PegaActorGroupKnowledgeReadResult<TValue>(
                accessStatus,
                PegaKnowledgeFact<TValue>.Unknown(PegaKnowledgeScope.Group));

        private static PegaActorGroupKnowledgeFactSetReadResult<TAddress, TValue> EmptyFactSets<TAddress, TValue>(
            PegaActorGroupKnowledgeAccessStatus accessStatus,
            string diagnostic = "") =>
            new PegaActorGroupKnowledgeFactSetReadResult<TAddress, TValue>(
                accessStatus,
                Array.Empty<PegaActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>>(),
                diagnostic);

        private void OnDestroy()
        {
            _planningInputChangeEvents?.Clear();
            _planningInputChangeEvents = null;
        }

        private void EnsurePlanningInputChangeEventsInitialized()
        {
            _planningInputChangeEvents ??= new EventBus<PegaNpcPlanningInputsChangedEvent>();
        }

        private void LogPlanningInputChangeSubscriberFailure(
            Action<PegaNpcPlanningInputsChangedEvent> handler,
            Exception exception)
        {
            if (!_loggerInitialized)
            {
                _log = PegaLogging.CreateScopedLogger<PegaActorGroupKnowledgeReader>(
                    LogCategory,
                    gameObject.name);
                _loggerInitialized = true;
            }

            _log.Error(
                "Group Knowledge planning-input subscriber failed; the confirmed Framework membership remains committed.",
                exception,
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("subscriberMethod", handler.Method.Name));
        }

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
            PegaKnowledgeFactKey<TAddress, TValue> key,
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
            PegaKnowledgeFactDefinition<TAddress, TValue> definition,
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

using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.Authoring;
using Immersive.Framework.RouteLifecycle;
using Immersive.Framework.RuntimeContent;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>Optional Group Knowledge authority owned beside one Actor Group composition.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/Knowledge/Actor Group Knowledge State")]
    public sealed class PEGAActorGroupKnowledgeState : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.Knowledge";

        private readonly Dictionary<GroupId, PEGAKnowledgeFactStore> _factStores =
            new Dictionary<GroupId, PEGAKnowledgeFactStore>();

        [NonSerialized] private bool _hasOwner;
        [NonSerialized] private RuntimeContentScope _ownerScope;
        [NonSerialized] private UnityEngine.Object _ownerIdentity;
        [NonSerialized] private Component _ownerContribution;
        [NonSerialized] private ActorGroupAccessBinding _groupAccess;
        [NonSerialized] private ScopedLogger _log;
        [NonSerialized] private bool _loggerInitialized;

        public bool IsAvailable =>
            isActiveAndEnabled &&
            _hasOwner &&
            _ownerIdentity != null &&
            _ownerContribution != null &&
            _groupAccess != null &&
            _groupAccess.IsAccessAvailable;

        internal bool TryBeginOwner(
            RuntimeContentScope ownerScope,
            UnityEngine.Object ownerIdentity,
            Component ownerContribution,
            ActorGroupAccessBinding groupAccess,
            out string diagnostic)
        {
            if (!isActiveAndEnabled || !IsValidOwnerShape(ownerScope, ownerIdentity, ownerContribution) ||
                ownerContribution.gameObject != gameObject ||
                groupAccess == null || groupAccess.gameObject != gameObject ||
                !groupAccess.IsAccessAvailable)
            {
                diagnostic = "Optional Group Knowledge requires the active, exact content owner and its bound Actor Group access.";
                return false;
            }

            if (_hasOwner)
            {
                bool sameOwner = _ownerScope == ownerScope &&
                    ReferenceEquals(_ownerIdentity, ownerIdentity) &&
                    ReferenceEquals(_ownerContribution, ownerContribution) &&
                    ReferenceEquals(_groupAccess, groupAccess);
                diagnostic = sameOwner
                    ? string.Empty
                    : "Optional Group Knowledge is already active for a different content owner.";
                return sameOwner;
            }

            _ownerScope = ownerScope;
            _ownerIdentity = ownerIdentity;
            _ownerContribution = ownerContribution;
            _groupAccess = groupAccess;
            _hasOwner = true;
            EnsureLoggerInitialized();
            _log.Info(
                "Optional Actor Group Knowledge became available for its content owner.",
                LogFields.Field("ownerScope", ownerScope),
                LogFields.Field("contentRoot", gameObject.name));
            diagnostic = string.Empty;
            return true;
        }

        internal bool TryRegisterCreatedGroup(
            ActorGroupSnapshot group,
            ActorGroupAccessBinding groupAccess,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!IsAvailable || !ReferenceEquals(_groupAccess, groupAccess))
            {
                diagnostic = "Optional Group Knowledge is unavailable for this Actor Group owner.";
                return false;
            }

            if (!group.IsValid ||
                !_groupAccess.TryGetGroup(group.GroupId, out ActorGroupSnapshot liveGroup, out diagnostic) ||
                !liveGroup.IsValid || liveGroup.GroupId != group.GroupId)
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "The Framework did not confirm the newly created Actor Group as live."
                    : diagnostic;
                return false;
            }

            if (_factStores.ContainsKey(group.GroupId))
            {
                diagnostic = $"Group Knowledge already has a store for live GroupId '{group.GroupId.StableText}'.";
                return false;
            }

            _factStores.Add(group.GroupId, new PEGAKnowledgeFactStore(PEGAKnowledgeScope.Group));
            EnsureLoggerInitialized();
            _log.Info(
                "Created an independent Group Knowledge store for a Framework Actor Group.",
                LogFields.Field("ownerScope", _ownerScope),
                LogFields.Field("groupId", group.GroupId.StableText));
            diagnostic = string.Empty;
            return true;
        }

        internal void EndOwner(
            RuntimeContentScope ownerScope,
            UnityEngine.Object ownerIdentity,
            Component ownerContribution)
        {
            if (!_hasOwner || _ownerScope != ownerScope ||
                !ReferenceEquals(_ownerIdentity, ownerIdentity) ||
                !ReferenceEquals(_ownerContribution, ownerContribution))
            {
                return;
            }

            EnsureLoggerInitialized();
            _log.Info(
                "Cleared Group Knowledge stores when their exact content owner exited.",
                LogFields.Field("ownerScope", _ownerScope),
                LogFields.Field("contentRoot", gameObject.name));
            ClearOwner();
        }

        /// <summary>Records a typed fact in a live Group owned by this exact content owner.</summary>
        public PEGAActorGroupKnowledgeAccessStatus ObserveFact<TAddress, TValue>(
            GroupId groupId,
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult writeResult)
        {
            writeResult = PEGAKnowledgeWriteResult.RejectedInvalid;
            PEGAActorGroupKnowledgeAccessStatus access = TryGetWritableStore(groupId, out PEGAKnowledgeFactStore factStore);
            if (access != PEGAActorGroupKnowledgeAccessStatus.Authorized)
            {
                return access;
            }

            if (!key.IsValid || !evidence.IsValid)
            {
                return PEGAActorGroupKnowledgeAccessStatus.Authorized;
            }

            writeResult = factStore.Observe(key, value, evidence);
            LogWrite(groupId, key.Name, writeResult, evidence);
            return PEGAActorGroupKnowledgeAccessStatus.Authorized;
        }

        /// <summary>Invalidates a typed fact in a live Group owned by this exact content owner.</summary>
        public PEGAActorGroupKnowledgeAccessStatus InvalidateFact<TAddress, TValue>(
            GroupId groupId,
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult writeResult)
        {
            writeResult = PEGAKnowledgeWriteResult.RejectedInvalid;
            PEGAActorGroupKnowledgeAccessStatus access = TryGetWritableStore(groupId, out PEGAKnowledgeFactStore factStore);
            if (access != PEGAActorGroupKnowledgeAccessStatus.Authorized)
            {
                return access;
            }

            if (!key.IsValid || !evidence.IsValid)
            {
                return PEGAActorGroupKnowledgeAccessStatus.Authorized;
            }

            writeResult = factStore.Invalidate(key, evidence);
            LogWrite(groupId, key.Name, writeResult, evidence);
            return PEGAActorGroupKnowledgeAccessStatus.Authorized;
        }

        internal PEGAActorGroupKnowledgeReadResult<TValue> ReadGroupFact<TAddress, TValue>(
            ActorId actorId,
            GroupId groupId,
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds)
        {
            ValidateQuery(key, queryTimeSeconds);
            PEGAActorGroupKnowledgeAccessStatus access = TryGetAuthorizedStore(
                actorId,
                groupId,
                out PEGAKnowledgeFactStore factStore);
            if (access != PEGAActorGroupKnowledgeAccessStatus.Authorized)
            {
                return new PEGAActorGroupKnowledgeReadResult<TValue>(
                    access,
                    PEGAKnowledgeFact<TValue>.Unknown(PEGAKnowledgeScope.Group));
            }

            return new PEGAActorGroupKnowledgeReadResult<TValue>(
                PEGAActorGroupKnowledgeAccessStatus.Authorized,
                factStore.Get(key, queryTimeSeconds));
        }

        internal bool HasRegisteredGroup(GroupId groupId) => _factStores.ContainsKey(groupId);

        private PEGAActorGroupKnowledgeAccessStatus TryGetWritableStore(
            GroupId groupId,
            out PEGAKnowledgeFactStore factStore)
        {
            PEGAActorGroupKnowledgeAccessStatus ownership = TryGetOwnedStore(groupId, out factStore);
            return ownership;
        }

        private PEGAActorGroupKnowledgeAccessStatus TryGetAuthorizedStore(
            ActorId actorId,
            GroupId groupId,
            out PEGAKnowledgeFactStore factStore)
        {
            if (!actorId.IsValid)
            {
                factStore = null;
                return PEGAActorGroupKnowledgeAccessStatus.InvalidActor;
            }

            PEGAActorGroupKnowledgeAccessStatus ownership = TryGetOwnedStore(groupId, out factStore);
            if (ownership != PEGAActorGroupKnowledgeAccessStatus.Authorized)
            {
                return ownership;
            }

            if (!_groupAccess.TryIsMember(groupId, actorId, out bool isMember, out _))
            {
                factStore = null;
                return PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
            }

            if (!isMember)
            {
                factStore = null;
                return PEGAActorGroupKnowledgeAccessStatus.NotMember;
            }

            return PEGAActorGroupKnowledgeAccessStatus.Authorized;
        }

        private PEGAActorGroupKnowledgeAccessStatus TryGetOwnedStore(
            GroupId groupId,
            out PEGAKnowledgeFactStore factStore)
        {
            factStore = null;
            if (!IsAvailable)
            {
                return PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
            }

            if (!groupId.IsValid || !_factStores.TryGetValue(groupId, out PEGAKnowledgeFactStore ownedStore))
            {
                return PEGAActorGroupKnowledgeAccessStatus.GroupUnavailable;
            }

            if (!_groupAccess.TryGetGroup(groupId, out ActorGroupSnapshot liveGroup, out _) ||
                !liveGroup.IsValid || liveGroup.GroupId != groupId)
            {
                ownedStore.Clear();
                _factStores.Remove(groupId);
                return PEGAActorGroupKnowledgeAccessStatus.GroupUnavailable;
            }

            factStore = ownedStore;
            return PEGAActorGroupKnowledgeAccessStatus.Authorized;
        }

        private void OnDestroy()
        {
            ClearOwner();
        }

        private void ClearOwner()
        {
            foreach (KeyValuePair<GroupId, PEGAKnowledgeFactStore> entry in _factStores)
            {
                entry.Value.Clear();
            }

            _factStores.Clear();
            _hasOwner = false;
            _ownerScope = RuntimeContentScope.Unknown;
            _ownerIdentity = null;
            _ownerContribution = null;
            _groupAccess = null;
        }

        private void LogWrite(
            GroupId groupId,
            string factName,
            PEGAKnowledgeWriteResult result,
            PEGAKnowledgeEvidence evidence)
        {
            EnsureLoggerInitialized();
            _log.Info(
                "Group Knowledge fact write completed.",
                LogFields.Field("groupId", groupId.StableText),
                LogFields.Field("fact", factName),
                LogFields.Field("scope", PEGAKnowledgeScope.Group),
                LogFields.Field("writeResult", result),
                LogFields.Field("origin", evidence.Origin),
                LogFields.Field("observationTimeSeconds", evidence.ObservationTimeSeconds));
        }

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PEGALogging.CreateScopedLogger<PEGAActorGroupKnowledgeState>(LogCategory, gameObject.name);
            _loggerInitialized = true;
        }

        private static bool IsValidOwnerShape(
            RuntimeContentScope ownerScope,
            UnityEngine.Object ownerIdentity,
            Component ownerContribution) =>
            ownerIdentity != null && ownerContribution != null &&
            (ownerScope == RuntimeContentScope.Activity &&
                ownerIdentity is ActivityAsset && ownerContribution is ActivityContentContribution ||
             ownerScope == RuntimeContentScope.Route &&
                ownerIdentity is RouteAsset && ownerContribution is RouteContentContribution);

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

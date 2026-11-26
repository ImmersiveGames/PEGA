using System;
using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.Actors;
using Immersive.Framework.Authoring;
using Immersive.Framework.RouteLifecycle;
using Immersive.Framework.RuntimeContent;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.NPC.Knowledge.Authoring;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
namespace PEGA.NPC.Knowledge.Group
{
    /// <summary>Optional Group Knowledge authority owned beside one Actor Group composition.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/Knowledge/Actor Group Knowledge State")]
    public sealed class PegaActorGroupKnowledgeState : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.Knowledge";

        private readonly Dictionary<GroupId, PegaKnowledgeFactStore> _factStores =
            new Dictionary<GroupId, PegaKnowledgeFactStore>();

        [SerializeField, Tooltip("Conhecimentos iniciais coletivos. Cada linha aponta para um Profile também declarado para criação pelo owner; os fatos só são aplicados depois que o Framework confirma o GroupId vivo.")]
        private List<PegaKnowledgeGroupInitialAuthoring> initialKnowledge =
            new List<PegaKnowledgeGroupInitialAuthoring>();

        [NonSerialized] private bool _hasOwner;
        [NonSerialized] private RuntimeContentScope _ownerScope;
        [NonSerialized] private UnityEngine.Object _ownerIdentity;
        [NonSerialized] private Component _ownerContribution;
        [NonSerialized] private ActorGroupAccessBinding _groupAccess;
        [NonSerialized] private PegaKnowledgeGroupInitialAuthoring[] _initialKnowledgeSnapshot;
        [NonSerialized] private ActorOccurrenceReferenceBinding _actorBinding;
        [NonSerialized] private double _initialKnowledgeObservationTimeSeconds;
        [NonSerialized] private ScopedLogger _log;
        [NonSerialized] private bool _loggerInitialized;

        public bool IsAvailable =>
            isActiveAndEnabled &&
            _hasOwner &&
            _ownerIdentity != null &&
            _ownerContribution != null &&
            _groupAccess != null &&
            _groupAccess.IsAccessAvailable;

        internal bool HasAuthoredInitialKnowledge
        {
            get
            {
                if (initialKnowledge == null)
                {
                    return false;
                }

                for (int index = 0; index < initialKnowledge.Count; index++)
                {
                    if (initialKnowledge[index] != null && initialKnowledge[index].HasFacts)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal bool HasAuthoredInitialKnowledgeFor(GroupProfile profile)
        {
            if (profile == null || initialKnowledge == null)
            {
                return false;
            }

            for (int index = 0; index < initialKnowledge.Count; index++)
            {
                PegaKnowledgeGroupInitialAuthoring entry = initialKnowledge[index];
                if (entry != null && entry.HasFacts && ReferenceEquals(entry.GroupProfile, profile))
                {
                    return true;
                }
            }

            return false;
        }

        internal bool TryValidateInitialKnowledgeGroups(
            IReadOnlyList<GroupProfile> declaredGroups,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (initialKnowledge == null || initialKnowledge.Count == 0)
            {
                return true;
            }

            var declaredById = new Dictionary<GroupId, GroupProfile>();
            if (declaredGroups != null)
            {
                for (int index = 0; index < declaredGroups.Count; index++)
                {
                    GroupProfile declaredProfile = declaredGroups[index];
                    if (declaredProfile != null &&
                        declaredProfile.TryGetGroupId(out GroupId declaredGroupId, out _))
                    {
                        declaredById[declaredGroupId] = declaredProfile;
                    }
                }
            }

            var configuredGroupIds = new HashSet<GroupId>();
            for (int index = 0; index < initialKnowledge.Count; index++)
            {
                PegaKnowledgeGroupInitialAuthoring entry = initialKnowledge[index];
                if (entry == null)
                {
                    diagnostic = $"Group initial-knowledge row {index} is missing.";
                    return false;
                }

                if (!entry.HasFacts)
                {
                    continue;
                }

                GroupProfile profile = entry.GroupProfile;
                if (profile == null || !profile.TryGetGroupId(out GroupId groupId, out diagnostic))
                {
                    diagnostic = string.IsNullOrEmpty(diagnostic)
                        ? $"Group initial-knowledge row {index} requires a Group Profile selector."
                        : diagnostic;
                    return false;
                }

                if (!declaredById.TryGetValue(groupId, out GroupProfile declaredProfile) ||
                    !ReferenceEquals(declaredProfile, profile))
                {
                    diagnostic = $"Group initial-knowledge row {index} selects GroupId '{groupId.StableText}', but its exact Profile is not declared for creation by this owner.";
                    return false;
                }

                if (!configuredGroupIds.Add(groupId))
                {
                    diagnostic = $"GroupId '{groupId.StableText}' has more than one initial-knowledge row.";
                    return false;
                }
            }

            return true;
        }

        internal bool TryBeginOwner(
            RuntimeContentScope ownerScope,
            UnityEngine.Object ownerIdentity,
            Component ownerContribution,
            ActorGroupAccessBinding groupAccess,
            IReadOnlyList<GroupProfile> declaredGroups,
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

            bool initialKnowledgeValid =
                TryValidateInitialKnowledgeGroups(declaredGroups, out string initialKnowledgeDiagnostic);

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
            _initialKnowledgeSnapshot = initialKnowledgeValid
                ? CaptureInitialKnowledgeSnapshot()
                : Array.Empty<PegaKnowledgeGroupInitialAuthoring>();
            _initialKnowledgeObservationTimeSeconds = Time.timeAsDouble;
            _actorBinding = GetComponent<ActorOccurrenceReferenceBinding>();
            _hasOwner = true;
            EnsureLoggerInitialized();
            _log.Info(
                "Optional Actor Group Knowledge became available for its content owner.",
                LogFields.Field("ownerScope", ownerScope),
                LogFields.Field("contentRoot", gameObject.name));
            diagnostic = initialKnowledgeValid ? string.Empty : initialKnowledgeDiagnostic;
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
                !liveGroup.IsValid || liveGroup.GroupId != group.GroupId ||
                !ReferenceEquals(liveGroup.Profile, group.Profile))
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

            _factStores.Add(group.GroupId, new PegaKnowledgeFactStore(PegaKnowledgeScope.Group));
            EnsureLoggerInitialized();
            _log.Info(
                "Created an independent Group Knowledge store for a Framework Actor Group.",
                LogFields.Field("ownerScope", _ownerScope),
                LogFields.Field("groupId", group.GroupId.StableText));
            ApplyInitialKnowledge(group);
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
        public PegaActorGroupKnowledgeAccessStatus ObserveFact<TAddress, TValue>(
            GroupId groupId,
            PegaKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PegaKnowledgeEvidence evidence,
            out PegaKnowledgeWriteResult writeResult)
        {
            writeResult = PegaKnowledgeWriteResult.RejectedInvalid;
            PegaActorGroupKnowledgeAccessStatus access = TryGetWritableStore(groupId, out PegaKnowledgeFactStore factStore);
            if (access != PegaActorGroupKnowledgeAccessStatus.Authorized)
            {
                return access;
            }

            if (!key.IsValid || !evidence.IsValid)
            {
                return PegaActorGroupKnowledgeAccessStatus.Authorized;
            }

            writeResult = factStore.Observe(key, value, evidence);
            LogWrite(groupId, key, writeResult, evidence);
            return PegaActorGroupKnowledgeAccessStatus.Authorized;
        }

        /// <summary>Records a typed Group fact only after the Framework confirms the Actor's current membership.</summary>
        public PegaActorGroupKnowledgeAccessStatus ObserveFactForActor<TAddress, TValue>(
            ActorId actorId,
            GroupId groupId,
            PegaKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PegaKnowledgeEvidence evidence,
            out PegaKnowledgeWriteResult writeResult)
        {
            writeResult = PegaKnowledgeWriteResult.RejectedInvalid;
            PegaActorGroupKnowledgeAccessStatus access = TryGetAuthorizedStore(
                actorId,
                groupId,
                out PegaKnowledgeFactStore factStore);
            if (access != PegaActorGroupKnowledgeAccessStatus.Authorized)
            {
                return access;
            }

            if (!key.IsValid || !evidence.IsValid)
            {
                return PegaActorGroupKnowledgeAccessStatus.Authorized;
            }

            writeResult = factStore.Observe(key, value, evidence);
            LogWrite(groupId, key, writeResult, evidence);
            return PegaActorGroupKnowledgeAccessStatus.Authorized;
        }

        /// <summary>Invalidates a typed fact in a live Group owned by this exact content owner.</summary>
        public PegaActorGroupKnowledgeAccessStatus InvalidateFact<TAddress, TValue>(
            GroupId groupId,
            PegaKnowledgeFactKey<TAddress, TValue> key,
            PegaKnowledgeEvidence evidence,
            out PegaKnowledgeWriteResult writeResult)
        {
            writeResult = PegaKnowledgeWriteResult.RejectedInvalid;
            PegaActorGroupKnowledgeAccessStatus access = TryGetWritableStore(groupId, out PegaKnowledgeFactStore factStore);
            if (access != PegaActorGroupKnowledgeAccessStatus.Authorized)
            {
                return access;
            }

            if (!key.IsValid || !evidence.IsValid)
            {
                return PegaActorGroupKnowledgeAccessStatus.Authorized;
            }

            writeResult = factStore.Invalidate(key, evidence);
            LogWrite(groupId, key, writeResult, evidence);
            return PegaActorGroupKnowledgeAccessStatus.Authorized;
        }

        internal PegaActorGroupKnowledgeReadResult<TValue> ReadGroupFact<TAddress, TValue>(
            ActorId actorId,
            GroupId groupId,
            PegaKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds)
        {
            ValidateQuery(key, queryTimeSeconds);
            PegaActorGroupKnowledgeAccessStatus access = TryGetAuthorizedStore(
                actorId,
                groupId,
                out PegaKnowledgeFactStore factStore);
            if (access != PegaActorGroupKnowledgeAccessStatus.Authorized)
            {
                return new PegaActorGroupKnowledgeReadResult<TValue>(
                    access,
                    PegaKnowledgeFact<TValue>.Unknown(PegaKnowledgeScope.Group));
            }

            return new PegaActorGroupKnowledgeReadResult<TValue>(
                PegaActorGroupKnowledgeAccessStatus.Authorized,
                factStore.Get(key, queryTimeSeconds));
        }

        internal bool TryReadGroupFactsForActor<TAddress, TValue>(
            ActorId actorId,
            PegaKnowledgeFactDefinition<TAddress, TValue> definition,
            double queryTimeSeconds,
            out IReadOnlyList<PegaActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>> groups,
            out PegaActorGroupKnowledgeAccessStatus accessStatus,
            out string diagnostic)
        {
            groups = Array.Empty<PegaActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>>();
            diagnostic = string.Empty;
            if (definition == null || double.IsNaN(queryTimeSeconds) ||
                double.IsInfinity(queryTimeSeconds) || queryTimeSeconds < 0d)
            {
                accessStatus = PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
                diagnostic = "Group fact enumeration requires a canonical definition and finite query time.";
                return false;
            }

            if (!actorId.IsValid)
            {
                accessStatus = PegaActorGroupKnowledgeAccessStatus.InvalidActor;
                diagnostic = "Group fact enumeration requires a valid logical ActorId.";
                return false;
            }

            if (!IsAvailable || _groupAccess == null ||
                !_groupAccess.TryGetGroupsForActor(
                    actorId,
                    int.MaxValue,
                    out IReadOnlyList<GroupId> liveGroupIds,
                    out diagnostic))
            {
                accessStatus = PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The exact Group owner or Framework membership query is unavailable."
                    : diagnostic;
                return false;
            }

            var factSets = new List<PegaActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>>();
            for (int index = 0; index < liveGroupIds.Count; index++)
            {
                GroupId groupId = liveGroupIds[index];
                if (!_factStores.ContainsKey(groupId))
                {
                    continue;
                }

                PegaActorGroupKnowledgeAccessStatus currentAccess = TryGetAuthorizedStore(
                    actorId,
                    groupId,
                    out PegaKnowledgeFactStore factStore);
                if (currentAccess != PegaActorGroupKnowledgeAccessStatus.Authorized)
                {
                    accessStatus = currentAccess;
                    diagnostic = $"Framework membership for Actor '{actorId.StableText}' in Group '{groupId.StableText}' changed or became unavailable during fact enumeration.";
                    return false;
                }

                factSets.Add(new PegaActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>(
                    groupId,
                    factStore.GetFacts(definition, queryTimeSeconds)));
            }

            groups = Array.AsReadOnly(factSets.ToArray());
            accessStatus = PegaActorGroupKnowledgeAccessStatus.Authorized;
            diagnostic = string.Empty;
            return true;
        }

        internal bool HasRegisteredGroup(GroupId groupId) => _factStores.ContainsKey(groupId);

        private PegaActorGroupKnowledgeAccessStatus TryGetWritableStore(
            GroupId groupId,
            out PegaKnowledgeFactStore factStore)
        {
            PegaActorGroupKnowledgeAccessStatus ownership = TryGetOwnedStore(groupId, out factStore);
            return ownership;
        }

        private PegaActorGroupKnowledgeAccessStatus TryGetAuthorizedStore(
            ActorId actorId,
            GroupId groupId,
            out PegaKnowledgeFactStore factStore)
        {
            if (!actorId.IsValid)
            {
                factStore = null;
                return PegaActorGroupKnowledgeAccessStatus.InvalidActor;
            }

            PegaActorGroupKnowledgeAccessStatus ownership = TryGetOwnedStore(groupId, out factStore);
            if (ownership != PegaActorGroupKnowledgeAccessStatus.Authorized)
            {
                return ownership;
            }

            if (!_groupAccess.TryIsMember(groupId, actorId, out bool isMember, out _))
            {
                factStore = null;
                return PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
            }

            if (!isMember)
            {
                factStore = null;
                return PegaActorGroupKnowledgeAccessStatus.NotMember;
            }

            return PegaActorGroupKnowledgeAccessStatus.Authorized;
        }

        private PegaActorGroupKnowledgeAccessStatus TryGetOwnedStore(
            GroupId groupId,
            out PegaKnowledgeFactStore factStore)
        {
            factStore = null;
            if (!IsAvailable)
            {
                return PegaActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
            }

            if (!groupId.IsValid || !_factStores.TryGetValue(groupId, out PegaKnowledgeFactStore ownedStore))
            {
                return PegaActorGroupKnowledgeAccessStatus.GroupUnavailable;
            }

            if (!_groupAccess.TryGetGroup(groupId, out ActorGroupSnapshot liveGroup, out _) ||
                !liveGroup.IsValid || liveGroup.GroupId != groupId)
            {
                ownedStore.Clear();
                _factStores.Remove(groupId);
                return PegaActorGroupKnowledgeAccessStatus.GroupUnavailable;
            }

            factStore = ownedStore;
            return PegaActorGroupKnowledgeAccessStatus.Authorized;
        }

        private void OnDestroy()
        {
            ClearOwner();
        }

        private void ClearOwner()
        {
            foreach (KeyValuePair<GroupId, PegaKnowledgeFactStore> entry in _factStores)
            {
                entry.Value.Clear();
            }

            _factStores.Clear();
            _hasOwner = false;
            _ownerScope = RuntimeContentScope.Unknown;
            _ownerIdentity = null;
            _ownerContribution = null;
            _groupAccess = null;
            _initialKnowledgeSnapshot = null;
            _actorBinding = null;
            _initialKnowledgeObservationTimeSeconds = 0d;
        }

        private void ApplyInitialKnowledge(ActorGroupSnapshot group)
        {
            if (_initialKnowledgeSnapshot == null || _initialKnowledgeSnapshot.Length == 0)
            {
                return;
            }

            for (int groupIndex = 0; groupIndex < _initialKnowledgeSnapshot.Length; groupIndex++)
            {
                PegaKnowledgeGroupInitialAuthoring groupEntry = _initialKnowledgeSnapshot[groupIndex];
                if (groupEntry == null || !ReferenceEquals(groupEntry.GroupProfile, group.Profile))
                {
                    continue;
                }

                IReadOnlyList<PegaKnowledgeFactAuthoringEntry> facts = groupEntry.Facts;
                for (int factIndex = 0; factIndex < facts.Count; factIndex++)
                {
                    PegaKnowledgeFactAuthoringEntry entry = facts[factIndex];
                    var writer = new GroupBoundInitialFactWriter(this, group.GroupId);
                    PegaKnowledgeFactInitialAuthoringStatus status = entry.TryApply(
                        _actorBinding,
                        writer,
                        PegaKnowledgeEvidence.From(
                            PegaNpcEvidenceOrigin.DesignerAuthoredInitial,
                            _initialKnowledgeObservationTimeSeconds,
                            this,
                            entry.DefinitionId),
                        out PegaKnowledgeWriteResult result,
                        out string diagnostic);

                    if (status == PegaKnowledgeFactInitialAuthoringStatus.Pending)
                    {
                        diagnostic = "The selected Actor identity is not resolved for this Group owner activation; use a resolved ActorDeclaration or a currently available binding.";
                    }

                    if ((status != PegaKnowledgeFactInitialAuthoringStatus.Applied ||
                         result == PegaKnowledgeWriteResult.RejectedInvalid) &&
                        string.IsNullOrWhiteSpace(diagnostic))
                    {
                        diagnostic = writer.WasInvoked &&
                                     writer.LastAccessStatus != PegaActorGroupKnowledgeAccessStatus.Authorized
                            ? $"The Group authority rejected the write with access status '{writer.LastAccessStatus}'."
                            : $"The canonical fact writer rejected the value with result '{result}'; check the definition policy and typed value.";
                    }

                    if (status != PegaKnowledgeFactInitialAuthoringStatus.Applied ||
                        result == PegaKnowledgeWriteResult.RejectedInvalid)
                    {
                        EnsureLoggerInitialized();
                        _log.Error(
                            "Designer-authored initial Group Knowledge was rejected.",
                            LogFields.Field("groupId", group.GroupId.StableText),
                            LogFields.Field("entryIndex", factIndex),
                            LogFields.Field("factDefinitionId", entry.DefinitionId),
                            LogFields.Field("writeResult", result),
                            LogFields.Field("groupWriteAccess", writer.WasInvoked
                                ? writer.LastAccessStatus.ToString()
                                : "not invoked"),
                            LogFields.Field("reason", diagnostic));
                    }
                }
            }
        }

        private PegaKnowledgeGroupInitialAuthoring[] CaptureInitialKnowledgeSnapshot()
        {
            if (initialKnowledge == null || initialKnowledge.Count == 0)
            {
                return Array.Empty<PegaKnowledgeGroupInitialAuthoring>();
            }

            var snapshot = new PegaKnowledgeGroupInitialAuthoring[initialKnowledge.Count];
            for (int index = 0; index < initialKnowledge.Count; index++)
            {
                snapshot[index] = initialKnowledge[index]?.Snapshot();
            }

            return snapshot;
        }

        private void LogWrite<TAddress, TValue>(
            GroupId groupId,
            PegaKnowledgeFactKey<TAddress, TValue> key,
            PegaKnowledgeWriteResult result,
            PegaKnowledgeEvidence evidence)
        {
            EnsureLoggerInitialized();
            _log.Info(
                "Group Knowledge fact write completed.",
                LogFields.Field("groupId", groupId.StableText),
                LogFields.Field("fact", key.Name),
                LogFields.Field("factDefinitionId", key.Definition.Id),
                LogFields.Field("factAddress", key.AddressValue),
                LogFields.Field("scope", PegaKnowledgeScope.Group),
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

            _log = PegaLogging.CreateScopedLogger<PegaActorGroupKnowledgeState>(LogCategory, gameObject.name);
            _loggerInitialized = true;
        }

        private sealed class GroupBoundInitialFactWriter : IPegaKnowledgeFactWriter
        {
            private readonly PegaActorGroupKnowledgeState _owner;
            private readonly GroupId _groupId;

            internal GroupBoundInitialFactWriter(PegaActorGroupKnowledgeState owner, GroupId groupId)
            {
                _owner = owner;
                _groupId = groupId;
            }

            internal PegaActorGroupKnowledgeAccessStatus LastAccessStatus { get; private set; } =
                PegaActorGroupKnowledgeAccessStatus.Authorized;

            internal bool WasInvoked { get; private set; }

            public PegaKnowledgeWriteResult ObserveFact<TAddress, TValue>(
                PegaKnowledgeFactKey<TAddress, TValue> key,
                TValue value,
                PegaKnowledgeEvidence evidence)
            {
                WasInvoked = true;
                LastAccessStatus = _owner.ObserveFact(
                    _groupId,
                    key,
                    value,
                    evidence,
                    out PegaKnowledgeWriteResult writeResult);
                return LastAccessStatus == PegaActorGroupKnowledgeAccessStatus.Authorized
                    ? writeResult
                    : PegaKnowledgeWriteResult.RejectedInvalid;
            }
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
    }
}

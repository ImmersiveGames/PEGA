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

        [SerializeField, Tooltip("Conhecimentos iniciais coletivos. Cada linha aponta para um Profile também declarado para criação pelo owner; os fatos só são aplicados depois que o Framework confirma o GroupId vivo.")]
        private List<PEGAKnowledgeGroupInitialAuthoring> initialKnowledge =
            new List<PEGAKnowledgeGroupInitialAuthoring>();

        [NonSerialized] private bool _hasOwner;
        [NonSerialized] private RuntimeContentScope _ownerScope;
        [NonSerialized] private UnityEngine.Object _ownerIdentity;
        [NonSerialized] private Component _ownerContribution;
        [NonSerialized] private ActorGroupAccessBinding _groupAccess;
        [NonSerialized] private PEGAKnowledgeGroupInitialAuthoring[] _initialKnowledgeSnapshot;
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
                PEGAKnowledgeGroupInitialAuthoring entry = initialKnowledge[index];
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
                PEGAKnowledgeGroupInitialAuthoring entry = initialKnowledge[index];
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
                : Array.Empty<PEGAKnowledgeGroupInitialAuthoring>();
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

            _factStores.Add(group.GroupId, new PEGAKnowledgeFactStore(PEGAKnowledgeScope.Group));
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
            LogWrite(groupId, key, writeResult, evidence);
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
            LogWrite(groupId, key, writeResult, evidence);
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

        internal bool TryReadGroupFactsForActor<TAddress, TValue>(
            ActorId actorId,
            PEGAKnowledgeFactDefinition<TAddress, TValue> definition,
            double queryTimeSeconds,
            out IReadOnlyList<PEGAActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>> groups,
            out PEGAActorGroupKnowledgeAccessStatus accessStatus,
            out string diagnostic)
        {
            groups = Array.Empty<PEGAActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>>();
            diagnostic = string.Empty;
            if (definition == null || double.IsNaN(queryTimeSeconds) ||
                double.IsInfinity(queryTimeSeconds) || queryTimeSeconds < 0d)
            {
                accessStatus = PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
                diagnostic = "Group fact enumeration requires a canonical definition and finite query time.";
                return false;
            }

            if (!actorId.IsValid)
            {
                accessStatus = PEGAActorGroupKnowledgeAccessStatus.InvalidActor;
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
                accessStatus = PEGAActorGroupKnowledgeAccessStatus.AuthorityUnavailable;
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The exact Group owner or Framework membership query is unavailable."
                    : diagnostic;
                return false;
            }

            var factSets = new List<PEGAActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>>();
            for (int index = 0; index < liveGroupIds.Count; index++)
            {
                GroupId groupId = liveGroupIds[index];
                if (!_factStores.ContainsKey(groupId))
                {
                    continue;
                }

                PEGAActorGroupKnowledgeAccessStatus currentAccess = TryGetAuthorizedStore(
                    actorId,
                    groupId,
                    out PEGAKnowledgeFactStore factStore);
                if (currentAccess != PEGAActorGroupKnowledgeAccessStatus.Authorized)
                {
                    accessStatus = currentAccess;
                    diagnostic = $"Framework membership for Actor '{actorId.StableText}' in Group '{groupId.StableText}' changed or became unavailable during fact enumeration.";
                    return false;
                }

                factSets.Add(new PEGAActorGroupKnowledgeFactSetSnapshot<TAddress, TValue>(
                    groupId,
                    factStore.GetFacts(definition, queryTimeSeconds)));
            }

            groups = Array.AsReadOnly(factSets.ToArray());
            accessStatus = PEGAActorGroupKnowledgeAccessStatus.Authorized;
            diagnostic = string.Empty;
            return true;
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
                PEGAKnowledgeGroupInitialAuthoring groupEntry = _initialKnowledgeSnapshot[groupIndex];
                if (groupEntry == null || !ReferenceEquals(groupEntry.GroupProfile, group.Profile))
                {
                    continue;
                }

                IReadOnlyList<PEGAKnowledgeFactAuthoringEntry> facts = groupEntry.Facts;
                for (int factIndex = 0; factIndex < facts.Count; factIndex++)
                {
                    PEGAKnowledgeFactAuthoringEntry entry = facts[factIndex];
                    var writer = new GroupBoundInitialFactWriter(this, group.GroupId);
                    PEGAKnowledgeFactInitialAuthoringStatus status = entry.TryApply(
                        _actorBinding,
                        writer,
                        PEGAKnowledgeEvidence.From(
                            PEGANpcEvidenceOrigin.DesignerAuthoredInitial,
                            _initialKnowledgeObservationTimeSeconds,
                            this,
                            entry.DefinitionId),
                        out PEGAKnowledgeWriteResult result,
                        out string diagnostic);

                    if (status == PEGAKnowledgeFactInitialAuthoringStatus.Pending)
                    {
                        diagnostic = "The selected Actor identity is not resolved for this Group owner activation; use a resolved ActorDeclaration or a currently available binding.";
                    }

                    if ((status != PEGAKnowledgeFactInitialAuthoringStatus.Applied ||
                         result == PEGAKnowledgeWriteResult.RejectedInvalid) &&
                        string.IsNullOrWhiteSpace(diagnostic))
                    {
                        diagnostic = writer.WasInvoked &&
                                     writer.LastAccessStatus != PEGAActorGroupKnowledgeAccessStatus.Authorized
                            ? $"The Group authority rejected the write with access status '{writer.LastAccessStatus}'."
                            : $"The canonical fact writer rejected the value with result '{result}'; check the definition policy and typed value.";
                    }

                    if (status != PEGAKnowledgeFactInitialAuthoringStatus.Applied ||
                        result == PEGAKnowledgeWriteResult.RejectedInvalid)
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

        private PEGAKnowledgeGroupInitialAuthoring[] CaptureInitialKnowledgeSnapshot()
        {
            if (initialKnowledge == null || initialKnowledge.Count == 0)
            {
                return Array.Empty<PEGAKnowledgeGroupInitialAuthoring>();
            }

            var snapshot = new PEGAKnowledgeGroupInitialAuthoring[initialKnowledge.Count];
            for (int index = 0; index < initialKnowledge.Count; index++)
            {
                snapshot[index] = initialKnowledge[index]?.Snapshot();
            }

            return snapshot;
        }

        private void LogWrite<TAddress, TValue>(
            GroupId groupId,
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGAKnowledgeWriteResult result,
            PEGAKnowledgeEvidence evidence)
        {
            EnsureLoggerInitialized();
            _log.Info(
                "Group Knowledge fact write completed.",
                LogFields.Field("groupId", groupId.StableText),
                LogFields.Field("fact", key.Name),
                LogFields.Field("factDefinitionId", key.Definition.Id),
                LogFields.Field("factAddress", key.AddressValue),
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

        private sealed class GroupBoundInitialFactWriter : IPEGAKnowledgeFactWriter
        {
            private readonly PEGAActorGroupKnowledgeState _owner;
            private readonly GroupId _groupId;

            internal GroupBoundInitialFactWriter(PEGAActorGroupKnowledgeState owner, GroupId groupId)
            {
                _owner = owner;
                _groupId = groupId;
            }

            internal PEGAActorGroupKnowledgeAccessStatus LastAccessStatus { get; private set; } =
                PEGAActorGroupKnowledgeAccessStatus.Authorized;

            internal bool WasInvoked { get; private set; }

            public PEGAKnowledgeWriteResult ObserveFact<TAddress, TValue>(
                PEGAKnowledgeFactKey<TAddress, TValue> key,
                TValue value,
                PEGAKnowledgeEvidence evidence)
            {
                WasInvoked = true;
                LastAccessStatus = _owner.ObserveFact(
                    _groupId,
                    key,
                    value,
                    evidence,
                    out PEGAKnowledgeWriteResult writeResult);
                return LastAccessStatus == PEGAActorGroupKnowledgeAccessStatus.Authorized
                    ? writeResult
                    : PEGAKnowledgeWriteResult.RejectedInvalid;
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

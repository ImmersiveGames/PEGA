using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Immersive.Framework.Actors;
using Immersive.Foundation.Events;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    public enum PEGAActorGroupPriorKnowledgeMode
    {
        IdentityOnly = 10,
        IdentityAndInitialLocation = 20
    }

    [Serializable]
    public sealed class PEGAActorGroupPriorKnowledgeDefinition
    {
        [SerializeField]
        [Tooltip("Asset que define a identidade persistente da entidade conhecida pelo Group.")]
        private ScriptableObject entityIdentity;

        [SerializeField]
        [Tooltip("Autoriza somente conhecer a identidade ou também registrar uma posição inicial obtida de uma ocorrência válida.")]
        private PEGAActorGroupPriorKnowledgeMode mode = PEGAActorGroupPriorKnowledgeMode.IdentityOnly;

        public ScriptableObject EntityIdentity => entityIdentity;

        public PEGAActorGroupPriorKnowledgeMode Mode => mode;
    }

    /// <summary>Autoridade PEGA dos fatos de um Group; sua disponibilidade acompanha o owner que criou o Group.</summary>
    [AddComponentMenu("PEGA/Actors/Actor Group Knowledge State")]
    public sealed class PEGAActorGroupKnowledgeState : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.ActorGroupKnowledge";
        private const string EscapeTargetIdPrefix = "pega.spawn-point.escape:";

        [Header("Group Access")]
        [SerializeField]
        [Tooltip("Group Profile que identifica os fatos desta autoridade.")]
        private GroupProfile groupProfile;

        [SerializeField]
        [Tooltip("Fatos iniciais conhecidos por este Group. A localização só é autorizada no modo correspondente.")]
        private PEGAActorGroupPriorKnowledgeDefinition[] priorEntityKnowledge =
            Array.Empty<PEGAActorGroupPriorKnowledgeDefinition>();

        [NonSerialized]
        private ActorGroupAccessBinding actorGroupAccessBinding;

        [NonSerialized]
        private PEGAActorGroupKnowledgeContext _context;

        [NonSerialized]
        private string _ownerScope;

        [NonSerialized]
        private PEGAKnowledgeFactStore _factStore;

        [NonSerialized]
        private HashSet<ActorId> _authorizedAccessLoggedActors;

        [NonSerialized]
        private HashSet<KnowledgeConsumptionKey> _consumptionLogs;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        [NonSerialized]
        private bool _ownerActive;

        public IReadOnlyList<PEGAActorGroupPriorKnowledgeDefinition> PriorEntityKnowledge =>
            priorEntityKnowledge ?? Array.Empty<PEGAActorGroupPriorKnowledgeDefinition>();

        public GroupProfile GroupProfile => groupProfile;

        internal bool IsOwnerActive => _ownerActive;

        internal T FindAuthorizedKnowledgeSource<T>() where T : PEGAActorGroupKnowledgeSource
        {
            PEGAActorGroupKnowledgeSource[] sources = GetComponentsInChildren<PEGAActorGroupKnowledgeSource>(true);
            for (int index = 0; index < sources.Length; index++)
            {
                if (sources[index] is T source && source.IsConfiguredFor(this))
                {
                    return source;
                }
            }

            return null;
        }

        private bool IsAuthorizedKnowledgeSource(PEGAActorGroupKnowledgeSource source)
        {
            PEGAActorGroupKnowledgeSource[] sources = GetComponentsInChildren<PEGAActorGroupKnowledgeSource>(true);
            for (int index = 0; index < sources.Length; index++)
            {
                if (ReferenceEquals(sources[index], source) &&
                    source.IsConfiguredFor(this))
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshAuthorizedInitialKnowledgeSources()
        {
            if (!_ownerActive)
            {
                return;
            }

            PEGAActorGroupKnowledgeSource[] sources = GetComponentsInChildren<PEGAActorGroupKnowledgeSource>(true);
            for (int index = 0; index < sources.Length; index++)
            {
                PEGAActorGroupKnowledgeSource source = sources[index];
                if (source != null && source.IsKnowledgeSourceActiveForOwner &&
                    source.IsConfiguredFor(this))
                {
                    source.RefreshAuthorizedInitialKnowledge();
                }
            }
        }

        private void Awake()
        {
            EnsureStateInitialized();
            EnsureLoggerInitialized();
        }

        internal bool TryActivate(
            ActorGroupAccessBinding groupAccess,
            PEGAActorGroupKnowledgeContext knowledgeContext,
            string ownerScope,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_ownerActive || groupAccess == null || !groupAccess.IsAccessAvailable ||
                knowledgeContext == null || groupProfile == null ||
                !groupProfile.TryGetGroupId(out GroupId groupId, out diagnostic) ||
                !groupAccess.TryGetGroup(groupId, out ActorGroupSnapshot group, out diagnostic) ||
                !group.HasProfile || group.Profile != groupProfile)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "Group knowledge requires its owner-created live Group and an explicit knowledge context."
                    : diagnostic;
                return false;
            }

            actorGroupAccessBinding = groupAccess;
            _context = knowledgeContext;
            _ownerScope = ownerScope;
            _ownerActive = true;
            EnsureStateInitialized();
            EnsureLoggerInitialized();
            _authorizedAccessLoggedActors.Clear();
            _consumptionLogs.Clear();
            InitializePriorEntityKnowledge();
            if (!_context.TryRegister(groupId, this, out diagnostic))
            {
                Deactivate();
                return false;
            }

            PEGAActorGroupKnowledgeSource[] sources = GetComponentsInChildren<PEGAActorGroupKnowledgeSource>(true);
            for (int index = 0; index < sources.Length; index++)
            {
                if (sources[index] != null && sources[index].IsConfiguredFor(this))
                {
                    sources[index].Activate();
                }
            }

            RefreshAuthorizedInitialKnowledgeSources();
            return true;
        }

        internal void Deactivate()
        {
            PEGAActorGroupKnowledgeSource[] sources = GetComponentsInChildren<PEGAActorGroupKnowledgeSource>(true);
            for (int index = 0; index < sources.Length; index++)
            {
                if (sources[index] != null && sources[index].IsConfiguredFor(this))
                {
                    sources[index].Deactivate();
                }
            }

            if (groupProfile != null && groupProfile.TryGetGroupId(out GroupId groupId, out _))
            {
                _context?.Unregister(groupId, this);
            }

            _ownerActive = false;
            _factStore?.Clear();
            _authorizedAccessLoggedActors?.Clear();
            _consumptionLogs?.Clear();
            actorGroupAccessBinding = null;
            _context = null;
            _ownerScope = null;
        }

        private void OnDestroy()
        {
            Deactivate();
        }

        /// <summary>Consulta se o ActorId pertence ao único Group autorizado por esta instância.</summary>
        public bool TryIsMember(GameObject actorRoot, out bool isMember, out string diagnostic)
        {
            isMember = false;
            if (!_ownerActive)
            {
                diagnostic = "Group knowledge is unavailable outside its owner lifecycle.";
                return false;
            }

            if (!TryResolveActorId(actorRoot, out ActorId actorId, out diagnostic))
            {
                return false;
            }

            if (!TryGetAuthorizedGroupId(out GroupId groupId, out diagnostic))
            {
                return false;
            }

            if (!actorGroupAccessBinding.TryGetGroup(groupId, out _, out diagnostic))
            {
                return false;
            }

            if (!actorGroupAccessBinding.TryIsMember(groupId, actorId, out isMember, out diagnostic))
            {
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        /// <summary>Consulta um fato tipado somente para membro confirmado do Group vivo.</summary>
        public bool TryGetFact<TAddress, TValue>(
            GameObject actorRoot,
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            out PEGAKnowledgeFact<TValue> fact,
            out string diagnostic)
        {
            fact = PEGAKnowledgeFact<TValue>.Unknown(PEGAKnowledgeScope.Group);
            if (!TryValidateKnowledgeAccess(actorRoot, out diagnostic))
            {
                return false;
            }

            if (!TryIsMember(actorRoot, out bool isMember, out diagnostic) || !isMember)
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The Actor is not a confirmed member of the live Actor Group.";
                }

                return false;
            }

            if (!key.IsValid)
            {
                diagnostic = "A valid fact definition and address are required for Group knowledge queries.";
                return false;
            }

            EnsureStateInitialized();
            fact = _factStore.Get(key, Time.timeAsDouble);
            LogAuthorizedGroupAccess(actorRoot);
            diagnostic = string.Empty;
            return true;
        }

        /// <summary>Registra um fato por uma fonte configurada, com membership obrigatória para evidência de Actor.</summary>
        internal bool TryRecordFact<TAddress, TValue>(
            IPEGAActorGroupKnowledgeFactSource source,
            GameObject memberRoot,
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult result,
            out string diagnostic)
        {
            result = PEGAKnowledgeWriteResult.RejectedInvalid;
            if (!TryAuthorizeFactWriter(source, memberRoot, out diagnostic))
            {
                return false;
            }

            EnsureStateInitialized();
            result = _factStore.Observe(key, value, evidence);
            if (result == PEGAKnowledgeWriteResult.RejectedInvalid)
            {
                diagnostic = "The fact key, evidence or value was rejected by its data policy.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        /// <summary>Invalida um fato por uma fonte configurada, com membership obrigatória para evidência de Actor.</summary>
        internal bool TryInvalidateFact<TAddress, TValue>(
            IPEGAActorGroupKnowledgeFactSource source,
            GameObject memberRoot,
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult result,
            out string diagnostic)
        {
            result = PEGAKnowledgeWriteResult.RejectedInvalid;
            if (!TryAuthorizeFactWriter(source, memberRoot, out diagnostic))
            {
                return false;
            }

            EnsureStateInitialized();
            result = _factStore.Invalidate(key, evidence);
            if (result == PEGAKnowledgeWriteResult.RejectedInvalid)
            {
                diagnostic = "The fact key or evidence was rejected by its data policy.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        /// <summary>Retorna Targets compartilhados somente ao ActorId que pertence ao Group vivo autorizado.</summary>
        public bool TryGetKnownEscapeTargets(
            GameObject actorRoot,
            out IReadOnlyList<PEGANpcPlanningTarget> targets,
            out string diagnostic)
        {
            targets = null;
            if (!TryValidateKnowledgeAccess(actorRoot, out diagnostic))
            {
                return false;
            }

            if (!TryIsMember(actorRoot, out bool isMember, out diagnostic) || !isMember)
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The Actor is not a member of the authorized Actor Group.";
                }

                return false;
            }

            targets = GetKnownEscapeTargetsCore();
            LogAuthorizedGroupAccess(actorRoot);
            return true;
        }

        /// <summary>Consulta fatos prévios separados de identidade e localização inicial.</summary>
        public bool TryGetKnownEntityFacts(
            GameObject actorRoot,
            ScriptableObject entityIdentity,
            out PEGAKnowledgeFact<ScriptableObject> identityFact,
            out PEGAKnowledgeFact<Vector3> initialLocationFact,
            out IPEGAActorGroupLocationSubjectEvidence locationSubjectEvidence,
            out string diagnostic)
        {
            identityFact = PEGAKnowledgeFact<ScriptableObject>.Unknown(PEGAKnowledgeScope.Group);
            initialLocationFact = PEGAKnowledgeFact<Vector3>.Unknown(PEGAKnowledgeScope.Group);
            locationSubjectEvidence = null;
            diagnostic = string.Empty;
            if (entityIdentity == null)
            {
                diagnostic = "A persistent entity identity asset is required.";
                return false;
            }

            if (!TryValidateKnowledgeAccess(actorRoot, out diagnostic))
            {
                return false;
            }

            if (!TryIsMember(actorRoot, out bool isMember, out diagnostic) || !isMember)
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The Actor is not a confirmed member of the live Actor Group.";
                }

                return false;
            }

            EnsureStateInitialized();
            identityFact = _factStore.Get(
                PEGAActorGroupKnowledgeFacts.EntityIdentity.For(entityIdentity),
                Time.timeAsDouble);
            initialLocationFact = _factStore.Get(
                PEGAActorGroupKnowledgeFacts.EntityInitialLocation.For(entityIdentity),
                Time.timeAsDouble);
            for (int index = 0; index < initialLocationFact.Evidence.Count; index++)
            {
                if (initialLocationFact.Evidence[index].Payload is IPEGAActorGroupLocationSubjectEvidence evidence)
                {
                    locationSubjectEvidence = evidence;
                    break;
                }
            }

            LogAuthorizedGroupAccess(actorRoot);
            diagnostic = string.Empty;
            return true;
        }

        internal bool HasKnownInitialLocation(ScriptableObject entityIdentity) =>
            _ownerActive && entityIdentity != null && _factStore != null &&
            _factStore.Get(
                PEGAActorGroupKnowledgeFacts.EntityInitialLocation.For(entityIdentity),
                Time.timeAsDouble).HasValue;

        internal bool TryRecordAuthorizedInitialLocation(
            PEGAActorGroupKnowledgeSource source,
            ScriptableObject entityIdentity,
            IPEGAActorGroupLocationSubjectEvidence locationSubjectEvidence,
            Vector3 initialLocation,
            double evidenceTimeSeconds,
            string sourceEvidence,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_ownerActive || source == null || !IsAuthorizedKnowledgeSource(source) ||
                !source.IsKnowledgeSourceActiveForOwner || entityIdentity == null ||
                locationSubjectEvidence == null ||
                !IsFinite(initialLocation) ||
                !IsNonNegativeFinite(evidenceTimeSeconds))
            {
                diagnostic = "Initial location requires an active configured source, a valid identity and finite evidence.";
                return false;
            }

            if (!TryGetAuthorizedGroupId(out GroupId groupId, out diagnostic) ||
                !actorGroupAccessBinding.TryGetGroup(groupId, out _, out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "Initial location remains unknown until the authorized live Group exists."
                    : diagnostic;
                return false;
            }

            ActorProfileId profileId = entityIdentity is ActorProfile actorProfile &&
                                       actorProfile.TryGetActorProfileId(out ActorProfileId resolvedProfileId, out _)
                ? resolvedProfileId
                : default;
            if (!HasInitialLocationAuthorization(entityIdentity))
            {
                diagnostic = "This entity is not configured for authorized initial location knowledge.";
                return false;
            }

            if (HasKnownInitialLocation(entityIdentity))
            {
                return true;
            }

            PEGAKnowledgeFactKey<ScriptableObject, Vector3> key =
                PEGAActorGroupKnowledgeFacts.EntityInitialLocation.For(entityIdentity);
            PEGAKnowledgeEvidence evidence = PEGAKnowledgeEvidence.From(
                PEGAActorGroupKnowledgeEvidenceOrigin.AuthorizedInitialLocationSource,
                evidenceTimeSeconds,
                source,
                locationSubjectEvidence);
            if (!TryRecordFact(
                    source,
                    null,
                    key,
                    initialLocation,
                    evidence,
                    out PEGAKnowledgeWriteResult writeResult,
                    out diagnostic) ||
                writeResult == PEGAKnowledgeWriteResult.RejectedInvalid)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The initial location fact was rejected by its data policy."
                    : diagnostic;
                return false;
            }

            EnsureLoggerInitialized();
            string identityText = profileId.IsValid
                ? profileId.StableText
                : string.Concat(entityIdentity.GetType().FullName, ":", entityIdentity.name);
            _log.Info(
                "Actor Group recorded an authorized initial entity location fact.",
                LogFields.Field("groupId", groupId.StableText),
                LogFields.Field("entityIdentity", identityText),
                LogFields.Field("source", source.GetType().FullName),
                LogFields.Field("sourceEvidence", sourceEvidence ?? string.Empty),
                LogFields.Field("factOrigin", PEGAActorGroupKnowledgeEvidenceOrigin.AuthorizedInitialLocationSource),
                LogFields.Field("validityScope", _ownerScope),
                LogFields.Field("positionX", initialLocation.x),
                LogFields.Field("positionY", initialLocation.y),
                LogFields.Field("positionZ", initialLocation.z),
                LogFields.Field("evidenceTimeSeconds", evidenceTimeSeconds));
            return true;
        }

        internal bool NotifyFrameworkGroupMembershipConfirmed(GameObject actorRoot, GroupId groupId)
        {
            if (!_ownerActive || !TryGetAuthorizedGroupId(out GroupId authorizedGroupId, out _) ||
                !authorizedGroupId.Equals(groupId) ||
                !TryResolveActorId(actorRoot, out ActorId actorId, out _) ||
                !actorGroupAccessBinding.TryIsMember(groupId, actorId, out bool isMember, out _) ||
                !isMember)
            {
                return false;
            }

            LogAuthorizedGroupAccess(actorRoot);
            RefreshAuthorizedInitialKnowledgeSources();
            return true;
        }

        internal void LogFactConsumed(
            GameObject memberRoot,
            IPEGAKnowledgeFactKey factKey,
            string consumer)
        {
            EnsureStateInitialized();
            if (memberRoot == null || factKey == null || !factKey.IsValid || string.IsNullOrWhiteSpace(consumer) ||
                !TryResolveActorId(memberRoot, out ActorId actorId, out _))
            {
                return;
            }

            var key = new KnowledgeConsumptionKey(actorId, factKey, consumer);
            if (!_consumptionLogs.Add(key))
            {
                return;
            }

            EnsureLoggerInitialized();
            _log.Info(
                "Authorized Actor Group member consumed a shared knowledge fact.",
                LogFields.Field("groupId", GetAuthorizedGroupIdText()),
                LogFields.Field("actorId", actorId.StableText),
                LogFields.Field("fact", factKey.Name),
                LogFields.Field("factAddress", factKey.Address),
                LogFields.Field("consumer", consumer),
                LogFields.Field("knowledgeScope", PEGAKnowledgeScope.Group));
        }

        /// <summary>Consulta um Target conhecido somente por um integrante verificado do Actor Group.</summary>
        public bool TryGetKnownEscapeTarget(
            GameObject actorRoot,
            string spawnPointId,
            out PEGANpcPlanningTarget target,
            out string diagnostic)
        {
            target = default;
            if (string.IsNullOrWhiteSpace(spawnPointId))
            {
                diagnostic = string.Empty;
                return false;
            }

            if (!TryValidateKnowledgeAccess(actorRoot, out diagnostic))
            {
                return false;
            }

            if (!TryIsMember(actorRoot, out bool isMember, out diagnostic) || !isMember)
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The Actor is not a member of the authorized Actor Group.";
                }

                return false;
            }

            EnsureStateInitialized();
            PEGAKnowledgeFact<PEGANpcPlanningTarget> fact = _factStore.Get(
                PEGAActorGroupKnowledgeFacts.SpawnPointEscapeTarget.For(spawnPointId.Trim()),
                Time.timeAsDouble);
            if (!fact.TryGetValue(out target))
            {
                diagnostic = string.Empty;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        internal bool TryRecordSpawnPointUse(
            PEGANpcSpawnPoint spawnPoint,
            string spawnPointId,
            Vector3 knownPosition,
            double evidenceTimeSeconds,
            GameObject memberRoot,
            out bool wasAdded,
            out string diagnostic)
        {
            wasAdded = false;
            diagnostic = string.Empty;
            EnsureStateInitialized();
            if (spawnPoint == null || memberRoot == null ||
                string.IsNullOrWhiteSpace(spawnPointId) ||
                !IsFinite(knownPosition) || !IsNonNegativeFinite(evidenceTimeSeconds))
            {
                diagnostic = "Spawn Point evidence requires a source, member, stable id, finite position and valid time.";
                return false;
            }

            if (spawnPoint.ActorGroupKnowledgeState != this)
            {
                diagnostic = "The Spawn Point must reference this Actor Group knowledge state.";
                return false;
            }

            if (!TryValidateKnowledgeAccess(memberRoot, out diagnostic) ||
                !TryIsMember(memberRoot, out bool isMember, out diagnostic) ||
                !isMember)
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The entering ActorId is not a member of the authorized Actor Group.";
                }

                return false;
            }

            if (!TryGetAuthorizedGroupId(out GroupId groupId, out diagnostic))
            {
                return false;
            }

            string normalizedId = spawnPointId.Trim();
            PEGAKnowledgeFactKey<string, PEGANpcPlanningTarget> targetKey =
                PEGAActorGroupKnowledgeFacts.SpawnPointEscapeTarget.For(normalizedId);
            PEGAKnowledgeFact<PEGANpcPlanningTarget> existingFact =
                _factStore.Get(targetKey, evidenceTimeSeconds);
            if (existingFact.TryGetValue(out _))
            {
                bool sameSource = false;
                for (int index = 0; index < existingFact.Evidence.Count; index++)
                {
                    if (ReferenceEquals(existingFact.Evidence[index].Source, spawnPoint))
                    {
                        sameSource = true;
                        break;
                    }
                }

                if (!sameSource)
                {
                    diagnostic = "Another Spawn Point already uses this stable id in the Actor Group.";
                    return false;
                }

                return true;
            }

            var targetId = PEGANpcPlanningTargetId.ForStableDataId(
                string.Concat(EscapeTargetIdPrefix, groupId.StableText, ":", normalizedId));
            PEGANpcPlanningTarget target = PEGANpcPlanningTarget.FromKnownEscapeDestination(
                targetId,
                knownPosition,
                evidenceTimeSeconds);
            if (!target.IsValid)
            {
                diagnostic = "The Spawn Point did not produce a valid known escape Target.";
                return false;
            }

            PEGAKnowledgeEvidence evidence = PEGAKnowledgeEvidence.From(
                PEGAActorGroupKnowledgeEvidenceOrigin.SpawnPointPhysicalIngress,
                evidenceTimeSeconds,
                spawnPoint,
                memberRoot);
            if (!TryRecordFact(
                    spawnPoint,
                    memberRoot,
                    targetKey,
                    target,
                    evidence,
                    out PEGAKnowledgeWriteResult writeResult,
                    out diagnostic))
            {
                return false;
            }

            wasAdded = writeResult == PEGAKnowledgeWriteResult.Recorded;
            if (!wasAdded)
            {
                return true;
            }

            EnsureLoggerInitialized();
            _log.Debug(
                "Actor Group learned a potential escape from physical Spawn Point use.",
                LogFields.Field("actorGroupKnowledgeState", gameObject.name),
                LogFields.Field("spawnPointId", normalizedId),
                LogFields.Field("targetKind", target.Kind),
                LogFields.Field("targetProvenance", target.Provenance),
                LogFields.Field("targetIdentity", target.TargetId.IdentityKind),
                LogFields.Field("knownPositionX", knownPosition.x),
                LogFields.Field("knownPositionY", knownPosition.y),
                LogFields.Field("knownPositionZ", knownPosition.z),
                LogFields.Field("evidenceTimeSeconds", evidenceTimeSeconds),
                LogFields.Field("memberRoot", memberRoot.name));
            _log.Info(
                "Actor Group learned a new escape route.",
                LogFields.Field("actorRoot", memberRoot.name));

            return true;
        }

        private IReadOnlyList<PEGANpcPlanningTarget> GetKnownEscapeTargetsCore()
        {
            EnsureStateInitialized();
            IReadOnlyList<PEGAKnowledgeFactSnapshot<string, PEGANpcPlanningTarget>> facts =
                _factStore.GetFacts(PEGAActorGroupKnowledgeFacts.SpawnPointEscapeTarget, Time.timeAsDouble);
            var knownFacts = new List<PEGAKnowledgeFactSnapshot<string, PEGANpcPlanningTarget>>();
            for (int index = 0; index < facts.Count; index++)
            {
                if (facts[index].Fact.HasValue)
                {
                    knownFacts.Add(facts[index]);
                }
            }

            knownFacts.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.Key.AddressValue,
                right.Key.AddressValue));

            var targets = new List<PEGANpcPlanningTarget>(knownFacts.Count);
            for (int index = 0; index < knownFacts.Count; index++)
            {
                if (knownFacts[index].Fact.TryGetValue(out PEGANpcPlanningTarget target))
                {
                    targets.Add(target);
                }
            }

            return new ReadOnlyCollection<PEGANpcPlanningTarget>(targets);
        }

        private bool TryResolveActorId(GameObject actorRoot, out ActorId actorId, out string diagnostic)
        {
            actorId = default;
            if (actorRoot == null)
            {
                diagnostic = "A Group member Actor root is required.";
                return false;
            }

            if (actorGroupAccessBinding == null)
            {
                diagnostic = "An owner-bound Actor Group access binding is required.";
                return false;
            }

            ActorDeclaration declaration = actorRoot.GetComponent<ActorDeclaration>();
            if (declaration == null)
            {
                diagnostic = "A Group member Actor must have an ActorDeclaration on its root.";
                return false;
            }

            try
            {
                actorId = declaration.ActorId;
            }
            catch (Exception exception)
            {
                diagnostic = "The Group member ActorId is invalid. " + exception.Message;
                actorId = default;
                return false;
            }

            if (!actorId.IsValid)
            {
                diagnostic = "A valid ActorId is required for Group membership.";
                actorId = default;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryValidateKnowledgeAccess(GameObject actorRoot, out string diagnostic)
        {
            if (actorRoot == null)
            {
                diagnostic = "A Group knowledge Actor root is required.";
                return false;
            }

            if (!TryResolveActorId(actorRoot, out _, out diagnostic))
            {
                return false;
            }

            return true;
        }

        private bool TryAuthorizeFactWriter(
            IPEGAActorGroupKnowledgeFactSource source,
            GameObject memberRoot,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_ownerActive || source == null || source.KnowledgeState != this ||
                !(source is Component sourceComponent) || sourceComponent == null ||
                !source.IsActive || !source.IsConfiguredFor(this))
            {
                diagnostic = "Fact writes require an active source configured for this Group knowledge authority.";
                return false;
            }

            if (!TryGetAuthorizedGroupId(out GroupId groupId, out diagnostic) ||
                !actorGroupAccessBinding.TryGetGroup(groupId, out ActorGroupSnapshot group, out diagnostic) ||
                !group.HasProfile || group.Profile != groupProfile)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "Fact writes require the owner-created live Group represented by this authority."
                    : diagnostic;
                return false;
            }

            if (memberRoot == null)
            {
                if (!source.IsActiveForOwner)
                {
                    diagnostic = "A source without an Actor must be active in this Group owner's lifecycle.";
                    return false;
                }

                return true;
            }

            if (!TryIsMember(memberRoot, out bool isMember, out diagnostic) || !isMember)
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "Actor evidence requires current official membership in this Group.";
                }

                return false;
            }

            return true;
        }

        internal void LogRejectedSpawnPointUse(
            string spawnPointId,
            GameObject memberRoot,
            string reason)
        {
            EnsureLoggerInitialized();
            _log.Warning(
                "Spawn Point use was not accepted as Actor Group knowledge.",
                LogFields.Field("actorGroupKnowledgeState", gameObject.name),
                LogFields.Field("spawnPointId", spawnPointId ?? string.Empty),
                LogFields.Field("memberRoot", memberRoot != null ? memberRoot.name : "unavailable"),
                LogFields.Field("reason", reason ?? string.Empty));
        }

        private void PublishChange(PEGAKnowledgeFactChangedEvent change)
        {
            if (TryGetAuthorizedGroupId(out GroupId groupId, out _))
            {
                _context?.NotifyFactChanged(groupId, change);
            }
        }

        private void EnsureStateInitialized()
        {
            if (_factStore == null)
            {
                _factStore = new PEGAKnowledgeFactStore(PEGAKnowledgeScope.Group);
                _factStore.Changed += PublishChange;
            }

            _authorizedAccessLoggedActors ??= new HashSet<ActorId>();
            _consumptionLogs ??= new HashSet<KnowledgeConsumptionKey>();
        }

        private void InitializePriorEntityKnowledge()
        {
            _factStore.Clear();
            var seenIdentities = new HashSet<ScriptableObject>();
            if (!TryGetAuthorizedGroupId(out GroupId groupId, out string groupDiagnostic))
            {
                EnsureLoggerInitialized();
                _log.Error(
                    "Actor Group prior knowledge could not be initialized.",
                    LogFields.Field("reason", groupDiagnostic));
                return;
            }

            PEGAActorGroupPriorKnowledgeDefinition[] definitions = priorEntityKnowledge ??
                Array.Empty<PEGAActorGroupPriorKnowledgeDefinition>();
            for (int index = 0; index < definitions.Length; index++)
            {
                PEGAActorGroupPriorKnowledgeDefinition definition = definitions[index];
                if (definition == null || definition.EntityIdentity == null ||
                    (definition.Mode != PEGAActorGroupPriorKnowledgeMode.IdentityOnly &&
                     definition.Mode != PEGAActorGroupPriorKnowledgeMode.IdentityAndInitialLocation))
                {
                    _log.Error(
                        "Actor Group prior knowledge authoring contains an invalid entity or mode.",
                        LogFields.Field("groupId", groupId.StableText),
                        LogFields.Field("definitionIndex", index));
                    continue;
                }

                if (!seenIdentities.Add(definition.EntityIdentity))
                {
                    _log.Error(
                        "Actor Group prior knowledge repeats the same entity identity asset.",
                        LogFields.Field("groupId", groupId.StableText),
                        LogFields.Field("entityIdentityAsset", definition.EntityIdentity.name));
                    continue;
                }

                ActorProfileId actorProfileId = default;
                if (definition.EntityIdentity is ActorProfile actorProfile &&
                    !actorProfile.TryGetActorProfileId(out actorProfileId, out string profileDiagnostic))
                {
                    _log.Error(
                        "Actor Group prior knowledge references an Actor Profile with an invalid identity.",
                        LogFields.Field("groupId", groupId.StableText),
                        LogFields.Field("entityIdentityAsset", actorProfile.name),
                        LogFields.Field("reason", profileDiagnostic));
                    continue;
                }

                double authoredAt = Time.timeAsDouble;
                PEGAKnowledgeEvidence evidence = PEGAKnowledgeEvidence.From(
                    PEGAActorGroupKnowledgeEvidenceOrigin.DesignerAuthoredIdentity,
                    authoredAt,
                    this,
                    definition);
                PEGAKnowledgeFactKey<ScriptableObject, ScriptableObject> identityKey =
                    PEGAActorGroupKnowledgeFacts.EntityIdentity.For(definition.EntityIdentity);
                PEGAKnowledgeWriteResult identityResult =
                    _factStore.Observe(identityKey, definition.EntityIdentity, evidence);
                if (identityResult == PEGAKnowledgeWriteResult.RejectedInvalid)
                {
                    _log.Error(
                        "Actor Group prior identity was rejected by its fact policy.",
                        LogFields.Field("groupId", groupId.StableText),
                        LogFields.Field("entityIdentityAsset", definition.EntityIdentity.name));
                    continue;
                }

                _log.Info(
                    "Actor Group prior entity identity was authored for the active owner scope.",
                    LogFields.Field("groupId", groupId.StableText),
                    LogFields.Field("entityIdentity", actorProfileId.IsValid
                        ? actorProfileId.StableText
                        : string.Concat(definition.EntityIdentity.GetType().FullName, ":", definition.EntityIdentity.name)),
                    LogFields.Field("fact", identityKey.Name),
                    LogFields.Field("factOrigin", PEGAActorGroupKnowledgeEvidenceOrigin.DesignerAuthoredIdentity),
                    LogFields.Field("locationAuthorization", definition.Mode),
                    LogFields.Field("validityScope", _ownerScope),
                    LogFields.Field("authoredAtSeconds", authoredAt));
            }

            if (HasInitialLocationAuthorization() && !HasConfiguredKnowledgeSource())
            {
                _log.Error(
                    "Actor Group prior knowledge authorizes initial locations but has no configured source.",
                    LogFields.Field("groupId", groupId.StableText),
                    LogFields.Field("locationAuthorization", PEGAActorGroupPriorKnowledgeMode.IdentityAndInitialLocation));
            }

            PEGAActorGroupKnowledgeSource[] sources = GetComponentsInChildren<PEGAActorGroupKnowledgeSource>(true);
            for (int index = 0; index < sources.Length; index++)
            {
                if (sources[index] == null || !sources[index].IsConfiguredFor(this))
                {
                    _log.Error(
                        "Actor Group knowledge source is missing or points to a different knowledge authority.",
                        LogFields.Field("groupId", groupId.StableText),
                        LogFields.Field("sourceIndex", index));
                }
            }
        }

        private bool HasInitialLocationAuthorization()
        {
            PEGAActorGroupPriorKnowledgeDefinition[] definitions = priorEntityKnowledge ??
                Array.Empty<PEGAActorGroupPriorKnowledgeDefinition>();
            for (int index = 0; index < definitions.Length; index++)
            {
                if (definitions[index] != null &&
                    definitions[index].Mode == PEGAActorGroupPriorKnowledgeMode.IdentityAndInitialLocation)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasInitialLocationAuthorization(ScriptableObject entityIdentity)
        {
            if (entityIdentity == null)
            {
                return false;
            }

            PEGAActorGroupPriorKnowledgeDefinition[] definitions = priorEntityKnowledge ??
                Array.Empty<PEGAActorGroupPriorKnowledgeDefinition>();
            for (int index = 0; index < definitions.Length; index++)
            {
                if (definitions[index] != null &&
                    ReferenceEquals(definitions[index].EntityIdentity, entityIdentity) &&
                    definitions[index].Mode == PEGAActorGroupPriorKnowledgeMode.IdentityAndInitialLocation)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasConfiguredKnowledgeSource()
        {
            PEGAActorGroupKnowledgeSource[] sources = GetComponentsInChildren<PEGAActorGroupKnowledgeSource>(true);
            for (int index = 0; index < sources.Length; index++)
            {
                if (sources[index] != null && sources[index].IsConfiguredFor(this))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetAuthorizedGroupId(out GroupId groupId, out string diagnostic)
        {
            groupId = default;
            if (actorGroupAccessBinding == null)
            {
                diagnostic = "An owner-bound Actor Group access binding is required.";
                return false;
            }

            if (groupProfile == null)
            {
                diagnostic = "Each Actor Group knowledge authority requires a Group Profile.";
                return false;
            }

            if (!groupProfile.TryGetGroupId(out groupId, out diagnostic))
            {
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private void LogAuthorizedGroupAccess(GameObject actorRoot)
        {
            EnsureStateInitialized();
            if (actorRoot == null || !TryResolveActorId(actorRoot, out ActorId actorId, out _) ||
                !_authorizedAccessLoggedActors.Add(actorId) ||
                !TryGetAuthorizedGroupId(out GroupId groupId, out _))
            {
                return;
            }

            EnsureLoggerInitialized();
            _log.Info(
                "Framework confirmed this Actor as a member authorized to access Group knowledge.",
                LogFields.Field("groupId", groupId.StableText),
                LogFields.Field("actorId", actorId.StableText),
                LogFields.Field("knowledgeScope", PEGAKnowledgeScope.Group));
        }

        private string GetAuthorizedGroupIdText() =>
            TryGetAuthorizedGroupId(out GroupId groupId, out _) ? groupId.StableText : string.Empty;

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PEGALogging.CreateScopedLogger<PEGAActorGroupKnowledgeState>(LogCategory, gameObject.name);
            _loggerInitialized = true;
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(double value) =>
            value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);

        private readonly struct KnowledgeConsumptionKey : IEquatable<KnowledgeConsumptionKey>
        {
            private readonly ActorId _actorId;
            private readonly IPEGAKnowledgeFactKey _factKey;
            private readonly string _consumer;

            public KnowledgeConsumptionKey(
                ActorId actorId,
                IPEGAKnowledgeFactKey factKey,
                string consumer)
            {
                _actorId = actorId;
                _factKey = factKey;
                _consumer = consumer;
            }

            public bool Equals(KnowledgeConsumptionKey other) =>
                _actorId.Equals(other._actorId) && object.Equals(_factKey, other._factKey) &&
                string.Equals(_consumer, other._consumer, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is KnowledgeConsumptionKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _actorId.GetHashCode();
                    hash = hash * 397 ^ (_factKey != null ? _factKey.GetHashCode() : 0);
                    return hash * 397 ^ StringComparer.Ordinal.GetHashCode(_consumer ?? string.Empty);
                }
            }
        }
    }
}

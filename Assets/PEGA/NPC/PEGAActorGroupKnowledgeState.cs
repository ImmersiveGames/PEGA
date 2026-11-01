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

    public enum PEGAActorGroupKnowledgeFactKind
    {
        SpawnPointEscapeTarget = 10,
        EntityIdentity = 20,
        EntityInitialLocation = 30
    }

    public enum PEGAActorGroupKnowledgeEvidenceOrigin
    {
        SpawnPointPhysicalIngress = 0,
        DesignerAuthoredIdentity = 10,
        AuthorizedInitialLocationSource = 20
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

    /// <summary>Publica mudanças aceitas pela autoridade coletiva de um Actor Group.</summary>
    public sealed class PEGAActorGroupKnowledgeFactChangedEvent : IEvent
    {
        internal PEGAActorGroupKnowledgeFactChangedEvent(
            string spawnPointId,
            PEGANpcPlanningTarget target,
            PEGAActorGroupKnowledgeEvidenceOrigin evidenceOrigin,
            double evidenceTimeSeconds)
        {
            FactKind = PEGAActorGroupKnowledgeFactKind.SpawnPointEscapeTarget;
            SpawnPointId = spawnPointId;
            Target = target;
            EvidenceOrigin = evidenceOrigin;
            EvidenceTimeSeconds = evidenceTimeSeconds;
            EntityIdentity = null;
        }

        internal PEGAActorGroupKnowledgeFactChangedEvent(
            PEGAActorGroupKnowledgeFactKind factKind,
            ScriptableObject entityIdentity,
            PEGAActorGroupKnowledgeEvidenceOrigin evidenceOrigin,
            double evidenceTimeSeconds)
        {
            FactKind = factKind;
            SpawnPointId = string.Empty;
            Target = default;
            EvidenceOrigin = evidenceOrigin;
            EvidenceTimeSeconds = evidenceTimeSeconds;
            EntityIdentity = entityIdentity;
        }

        public PEGAActorGroupKnowledgeFactKind FactKind { get; }

        public string SpawnPointId { get; }

        public PEGANpcPlanningTarget Target { get; }

        public PEGAActorGroupKnowledgeEvidenceOrigin EvidenceOrigin { get; }

        public double EvidenceTimeSeconds { get; }

        public ScriptableObject EntityIdentity { get; }
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
        private Dictionary<string, KnownSpawnPointFact> _knownEscapePoints;

        [NonSerialized]
        private Dictionary<ScriptableObject, PriorEntityKnowledgeFact> _priorEntityFacts;

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
            _knownEscapePoints?.Clear();
            _priorEntityFacts?.Clear();
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
            out PEGANpcKnowledgeFact<ScriptableObject> identityFact,
            out PEGANpcKnowledgeFact<Vector3> initialLocationFact,
            out IPEGAActorGroupLocationSubjectEvidence locationSubjectEvidence,
            out string diagnostic)
        {
            identityFact = PEGANpcKnowledgeFact<ScriptableObject>.UnknownForScope(
                PEGANpcKnowledgeScope.Group);
            initialLocationFact = PEGANpcKnowledgeFact<Vector3>.UnknownForScope(
                PEGANpcKnowledgeScope.Group);
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
            if (!_priorEntityFacts.TryGetValue(entityIdentity, out PriorEntityKnowledgeFact fact))
            {
                diagnostic = string.Empty;
                return true;
            }

            identityFact = fact.IdentityFact;
            initialLocationFact = fact.InitialLocationFact;
            locationSubjectEvidence = fact.InitialLocationSubjectEvidence;
            LogAuthorizedGroupAccess(actorRoot);
            diagnostic = string.Empty;
            return true;
        }

        internal bool HasKnownInitialLocation(ScriptableObject entityIdentity) =>
            _ownerActive && entityIdentity != null &&
            _priorEntityFacts != null &&
            _priorEntityFacts.TryGetValue(entityIdentity, out PriorEntityKnowledgeFact fact) &&
            fact.InitialLocationFact.HasValue;

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
            if (!_priorEntityFacts.TryGetValue(entityIdentity, out PriorEntityKnowledgeFact fact) ||
                fact.Mode != PEGAActorGroupPriorKnowledgeMode.IdentityAndInitialLocation)
            {
                diagnostic = "This entity is not configured for authorized initial location knowledge.";
                return false;
            }

            if (fact.InitialLocationFact.HasValue)
            {
                return true;
            }

            fact.InitialLocationFact = new PEGANpcKnowledgeFact<Vector3>(
                PEGANpcKnowledgeStatus.Known,
                true,
                evidenceTimeSeconds,
                PEGANpcEvidenceOrigin.AuthorizedInitialLocation,
                PEGANpcKnowledgeInvalidationCause.None,
                initialLocation,
                PEGANpcKnowledgeScope.Group);
            fact.InitialLocationSubjectEvidence = locationSubjectEvidence;
            _priorEntityFacts[entityIdentity] = fact;

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
            PublishChange(new PEGAActorGroupKnowledgeFactChangedEvent(
                PEGAActorGroupKnowledgeFactKind.EntityInitialLocation,
                entityIdentity,
                PEGAActorGroupKnowledgeEvidenceOrigin.AuthorizedInitialLocationSource,
                evidenceTimeSeconds));
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

        internal void LogEntityFactConsumed(
            GameObject memberRoot,
            ScriptableObject entityIdentity,
            PEGAActorGroupKnowledgeFactKind factKind,
            string consumer)
        {
            EnsureStateInitialized();
            if (memberRoot == null || entityIdentity == null || string.IsNullOrWhiteSpace(consumer) ||
                !TryResolveActorId(memberRoot, out ActorId actorId, out _))
            {
                return;
            }

            var key = new KnowledgeConsumptionKey(actorId, entityIdentity, factKind, consumer);
            if (!_consumptionLogs.Add(key))
            {
                return;
            }

            string identityText = entityIdentity is ActorProfile profile &&
                                  profile.TryGetActorProfileId(out ActorProfileId profileId, out _)
                ? profileId.StableText
                : string.Concat(entityIdentity.GetType().FullName, ":", entityIdentity.name);
            EnsureLoggerInitialized();
            _log.Info(
                "Authorized Actor Group member consumed a shared knowledge fact.",
                LogFields.Field("groupId", GetAuthorizedGroupIdText()),
                LogFields.Field("actorId", actorId.StableText),
                LogFields.Field("entityIdentity", identityText),
                LogFields.Field("factKind", factKind),
                LogFields.Field("consumer", consumer),
                LogFields.Field("knowledgeScope", PEGANpcKnowledgeScope.Group));
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
            if (!_knownEscapePoints.TryGetValue(spawnPointId.Trim(), out KnownSpawnPointFact fact))
            {
                diagnostic = string.Empty;
                return false;
            }

            target = fact.Target;
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

            if (!TryValidateKnowledgeAccess(memberRoot, out diagnostic))
            {
                return false;
            }

            if (!TryIsMember(memberRoot, out bool isMember, out diagnostic))
            {
                return false;
            }

            if (!isMember)
            {
                diagnostic = "The entering ActorId is not a member of the authorized Actor Group.";
                return false;
            }

            if (!TryGetAuthorizedGroupId(out GroupId groupId, out diagnostic))
            {
                return false;
            }

            string normalizedId = spawnPointId.Trim();
            if (_knownEscapePoints.TryGetValue(normalizedId, out KnownSpawnPointFact existing))
            {
                if (!ReferenceEquals(existing.Source, spawnPoint))
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

            _knownEscapePoints.Add(
                normalizedId,
                new KnownSpawnPointFact(spawnPoint, target));
            wasAdded = true;

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

            PublishChange(new PEGAActorGroupKnowledgeFactChangedEvent(
                normalizedId,
                target,
                PEGAActorGroupKnowledgeEvidenceOrigin.SpawnPointPhysicalIngress,
                evidenceTimeSeconds));
            return true;
        }

        private IReadOnlyList<PEGANpcPlanningTarget> GetKnownEscapeTargetsCore()
        {
            EnsureStateInitialized();
            var spawnPointIds = new List<string>(_knownEscapePoints.Keys);
            spawnPointIds.Sort(StringComparer.Ordinal);

            var targets = new List<PEGANpcPlanningTarget>(spawnPointIds.Count);
            for (int i = 0; i < spawnPointIds.Count; i++)
            {
                targets.Add(_knownEscapePoints[spawnPointIds[i]].Target);
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

        private void PublishChange(PEGAActorGroupKnowledgeFactChangedEvent change)
        {
            if (TryGetAuthorizedGroupId(out GroupId groupId, out _))
            {
                _context?.NotifyFactChanged(groupId, change);
            }
        }

        private void EnsureStateInitialized()
        {
            _knownEscapePoints ??= new Dictionary<string, KnownSpawnPointFact>(StringComparer.Ordinal);
            _priorEntityFacts ??= new Dictionary<ScriptableObject, PriorEntityKnowledgeFact>();
            _authorizedAccessLoggedActors ??= new HashSet<ActorId>();
            _consumptionLogs ??= new HashSet<KnowledgeConsumptionKey>();
        }

        private void InitializePriorEntityKnowledge()
        {
            _priorEntityFacts.Clear();
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

                if (_priorEntityFacts.ContainsKey(definition.EntityIdentity))
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
                var fact = new PriorEntityKnowledgeFact(
                    definition.Mode,
                    new PEGANpcKnowledgeFact<ScriptableObject>(
                        PEGANpcKnowledgeStatus.Known,
                        true,
                        authoredAt,
                        PEGANpcEvidenceOrigin.GroupPriorIdentity,
                        PEGANpcKnowledgeInvalidationCause.None,
                        definition.EntityIdentity,
                        PEGANpcKnowledgeScope.Group),
                    PEGANpcKnowledgeFact<Vector3>.UnknownForScope(PEGANpcKnowledgeScope.Group));
                _priorEntityFacts.Add(definition.EntityIdentity, fact);
                _log.Info(
                    "Actor Group prior entity identity was authored for the active owner scope.",
                    LogFields.Field("groupId", groupId.StableText),
                    LogFields.Field("entityIdentity", actorProfileId.IsValid
                        ? actorProfileId.StableText
                        : string.Concat(definition.EntityIdentity.GetType().FullName, ":", definition.EntityIdentity.name)),
                    LogFields.Field("factKind", PEGAActorGroupKnowledgeFactKind.EntityIdentity),
                    LogFields.Field("factOrigin", PEGAActorGroupKnowledgeEvidenceOrigin.DesignerAuthoredIdentity),
                    LogFields.Field("locationAuthorization", definition.Mode),
                    LogFields.Field("validityScope", _ownerScope),
                    LogFields.Field("authoredAtSeconds", authoredAt));
                PublishChange(new PEGAActorGroupKnowledgeFactChangedEvent(
                    PEGAActorGroupKnowledgeFactKind.EntityIdentity,
                    definition.EntityIdentity,
                    PEGAActorGroupKnowledgeEvidenceOrigin.DesignerAuthoredIdentity,
                    authoredAt));
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
                LogFields.Field("knowledgeScope", PEGANpcKnowledgeScope.Group));
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

        private readonly struct KnownSpawnPointFact
        {
            public KnownSpawnPointFact(
                PEGANpcSpawnPoint source,
                PEGANpcPlanningTarget target)
            {
                Source = source;
                Target = target;
            }

            public PEGANpcSpawnPoint Source { get; }

            public PEGANpcPlanningTarget Target { get; }
        }

        private struct PriorEntityKnowledgeFact
        {
            public PriorEntityKnowledgeFact(
                PEGAActorGroupPriorKnowledgeMode mode,
                PEGANpcKnowledgeFact<ScriptableObject> identityFact,
                PEGANpcKnowledgeFact<Vector3> initialLocationFact)
            {
                Mode = mode;
                IdentityFact = identityFact;
                InitialLocationFact = initialLocationFact;
                InitialLocationSubjectEvidence = null;
            }

            public PEGAActorGroupPriorKnowledgeMode Mode;

            public PEGANpcKnowledgeFact<ScriptableObject> IdentityFact;

            public PEGANpcKnowledgeFact<Vector3> InitialLocationFact;

            public IPEGAActorGroupLocationSubjectEvidence InitialLocationSubjectEvidence;

        }

        private readonly struct KnowledgeConsumptionKey : IEquatable<KnowledgeConsumptionKey>
        {
            private readonly ActorId _actorId;
            private readonly ScriptableObject _entityIdentity;
            private readonly PEGAActorGroupKnowledgeFactKind _factKind;
            private readonly string _consumer;

            public KnowledgeConsumptionKey(
                ActorId actorId,
                ScriptableObject entityIdentity,
                PEGAActorGroupKnowledgeFactKind factKind,
                string consumer)
            {
                _actorId = actorId;
                _entityIdentity = entityIdentity;
                _factKind = factKind;
                _consumer = consumer;
            }

            public bool Equals(KnowledgeConsumptionKey other) =>
                _actorId.Equals(other._actorId) && ReferenceEquals(_entityIdentity, other._entityIdentity) &&
                _factKind == other._factKind && string.Equals(_consumer, other._consumer, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is KnowledgeConsumptionKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _actorId.GetHashCode();
                    hash = hash * 397 ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_entityIdentity);
                    hash = hash * 397 ^ (int)_factKind;
                    return hash * 397 ^ StringComparer.Ordinal.GetHashCode(_consumer ?? string.Empty);
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Foundation.Events;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>Autoridade PEGA dos fatos de um Group; sua disponibilidade acompanha o owner que criou o Group.</summary>
    [AddComponentMenu("PEGA/Actors/Actor Group Knowledge State")]
    public sealed class PEGAActorGroupKnowledgeState : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.ActorGroupKnowledge";

        [Header("Group Access")]
        [SerializeField]
        [Tooltip("Group Profile que identifica os fatos desta autoridade.")]
        private GroupProfile groupProfile;

        [SerializeField]
        [Header("Initial Knowledge")]
        [Tooltip("Fatos tipados declarados para o Group. Cada definição aplica sua política e metadados ao ser armazenada.")]
        private List<PEGAActorGroupInitialKnowledgeFact> initialKnowledgeFacts = new();

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

        public IReadOnlyList<PEGAActorGroupInitialKnowledgeFact> InitialKnowledgeFacts
        {
            get
            {
                return initialKnowledgeFacts != null
                    ? (IReadOnlyList<PEGAActorGroupInitialKnowledgeFact>)initialKnowledgeFacts
                    : Array.Empty<PEGAActorGroupInitialKnowledgeFact>();
            }
        }

        public GroupProfile GroupProfile => groupProfile;

        internal bool IsOwnerActive => _ownerActive;

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
            InitializeInitialKnowledge();
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

        private void InitializeInitialKnowledge()
        {
            _factStore.Clear();
            if (!TryGetAuthorizedGroupId(out GroupId groupId, out string groupDiagnostic))
            {
                EnsureLoggerInitialized();
                _log.Error(
                    "Actor Group initial knowledge could not be initialized.",
                    LogFields.Field("reason", groupDiagnostic));
                return;
            }

            InitializeInitialKnowledgeFacts(groupId);
        }

        private void InitializeInitialKnowledgeFacts(GroupId groupId)
        {
            if (initialKnowledgeFacts == null)
            {
                return;
            }

            for (int index = 0; index < initialKnowledgeFacts.Count; index++)
            {
                PEGAActorGroupInitialKnowledgeFact definition = initialKnowledgeFacts[index];
                if (definition == null)
                {
                    _log.Error(
                        "Actor Group initial fact authoring contains an empty definition.",
                        LogFields.Field("groupId", groupId.StableText),
                        LogFields.Field("definitionIndex", index));
                    continue;
                }

                double authoredAt = Time.timeAsDouble;
                PEGAKnowledgeEvidence evidence = PEGAKnowledgeEvidence.From(
                    PEGAActorGroupKnowledgeEvidenceOrigin.DesignerAuthoredInitialFact,
                    authoredAt,
                    this,
                    definition);
                if (!definition.TryObserve(_factStore, evidence, out PEGAKnowledgeWriteResult result,
                        out string diagnostic) || result == PEGAKnowledgeWriteResult.RejectedInvalid)
                {
                    _log.Error(
                        "Actor Group initial fact was rejected by its fact definition or policy.",
                        LogFields.Field("groupId", groupId.StableText),
                        LogFields.Field("definitionIndex", index),
                        LogFields.Field("fact", definition.FactName),
                        LogFields.Field("writeResult", result),
                        LogFields.Field("reason", diagnostic));
                    continue;
                }

                _log.Info(
                    "Actor Group initial fact was authored for the active owner scope.",
                    LogFields.Field("groupId", groupId.StableText),
                    LogFields.Field("fact", definition.FactName),
                    LogFields.Field("factOrigin", PEGAActorGroupKnowledgeEvidenceOrigin.DesignerAuthoredInitialFact),
                    LogFields.Field("validityScope", _ownerScope),
                    LogFields.Field("authoredAtSeconds", authoredAt),
                    LogFields.Field("writeResult", result));
            }
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

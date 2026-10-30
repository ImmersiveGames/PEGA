using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Immersive.Framework.Actors;
using Immersive.Foundation.Events;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

namespace PEGA.NPC
{
    public enum PEGANpcGangKnowledgeEvidenceOrigin
    {
        SpawnPointPhysicalIngress = 0
    }

    /// <summary>Uma evidência de uso de Spawn Point, compartilhada pelos membros de uma Gangue.</summary>
    public sealed class PEGANpcGangKnowledgeFactChangedEvent : IEvent
    {
        internal PEGANpcGangKnowledgeFactChangedEvent(
            string spawnPointId,
            PEGANpcPlanningTarget target,
            PEGANpcGangKnowledgeEvidenceOrigin evidenceOrigin,
            double evidenceTimeSeconds)
        {
            SpawnPointId = spawnPointId;
            Target = target;
            EvidenceOrigin = evidenceOrigin;
            EvidenceTimeSeconds = evidenceTimeSeconds;
        }

        public string SpawnPointId { get; }

        public PEGANpcPlanningTarget Target { get; }

        public PEGANpcGangKnowledgeEvidenceOrigin EvidenceOrigin { get; }

        public double EvidenceTimeSeconds { get; }
    }

    /// <summary>Armazena conhecimento de Spawn Points no escopo de uma única Gangue.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Gang Knowledge State")]
    public sealed class PEGANpcGangKnowledgeState : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.GangKnowledge";
        private const string EscapeTargetIdPrefix = "pega.spawn-point.escape:";

        [Header("Group Access")]
        [SerializeField]
        [Tooltip("Group Profiles cujos integrantes podem consultar e atualizar este conhecimento coletivo.")]
        [FormerlySerializedAs("authorizedGroupDefinitions")]
        private GroupProfile[] authorizedGroupProfiles = Array.Empty<GroupProfile>();

        [SerializeField]
        [Tooltip("Acesso a Actor Groups fornecido pela contribuição da Activity que contém esta Gangue.")]
        private ActorGroupAccessBinding actorGroupAccessBinding;

        [NonSerialized]
        private Dictionary<string, KnownSpawnPointFact> _knownEscapePoints;

        [NonSerialized]
        private EventBus<PEGANpcGangKnowledgeFactChangedEvent> _changeEvents;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        private void Awake()
        {
            EnsureStateInitialized();
            EnsureLoggerInitialized();
        }

        private void OnDestroy()
        {
            _changeEvents?.Clear();
            _changeEvents = null;
        }

        /// <summary>Materializa os Grupos autorizados após o owner da Activity confirmar o acesso oficial.</summary>
        internal bool TryEnsureAuthorizedGroupsLive(
            out int groupCount,
            out int createdGroupCount,
            out string diagnostic)
        {
            groupCount = 0;
            createdGroupCount = 0;
            if (actorGroupAccessBinding == null)
            {
                diagnostic = "An Activity-owned Actor Group access binding is required for the Gang knowledge state.";
                return false;
            }

            if (!TryGetAuthorizedGroupIds(out List<GroupId> groupIds, out diagnostic))
            {
                return false;
            }

            for (int index = 0; index < groupIds.Count; index++)
            {
                if (actorGroupAccessBinding.TryGetGroup(groupIds[index], out _, out _))
                {
                    groupCount++;
                    continue;
                }

                if (!actorGroupAccessBinding.TryCreateGroup(
                        authorizedGroupProfiles[index],
                        out _,
                        out diagnostic))
                {
                    return false;
                }

                groupCount++;
                createdGroupCount++;
                EnsureLoggerInitialized();
                _log.Debug(
                    "PEGA materialized an authorized Actor Group for collective knowledge.",
                    LogFields.Field("gangKnowledgeState", gameObject.name),
                    LogFields.Field("groupId", groupIds[index].StableText),
                    LogFields.Field("groupCreatedWithoutMemberIngress", true));
            }

            diagnostic = string.Empty;
            return true;
        }

        /// <summary>Registra os Actors já materializados nesta cena usando a configuração de cada componente de Membership.</summary>
        internal bool TryRegisterSceneMembers(
            Scene scene,
            out int registeredMemberCount,
            out string diagnostic)
        {
            registeredMemberCount = 0;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                diagnostic = "A loaded Activity scene is required to register its authored Gang members.";
                return false;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                PEGANpcGangMembership[] memberships =
                    roots[rootIndex].GetComponentsInChildren<PEGANpcGangMembership>(true);
                for (int membershipIndex = 0; membershipIndex < memberships.Length; membershipIndex++)
                {
                    PEGANpcGangMembership membership = memberships[membershipIndex];
                    if (membership == null)
                    {
                        continue;
                    }

                    if (!TryMatchesAuthorizedGroupConfiguration(
                            membership,
                            out bool isAuthorized,
                            out diagnostic))
                    {
                        diagnostic = string.Concat(
                            "Could not inspect Gang membership on Actor '",
                            membership.gameObject.name,
                            "': ",
                            diagnostic);
                        return false;
                    }

                    if (!isAuthorized)
                    {
                        continue;
                    }

                    if (!TryJoinMember(membership.gameObject, out _, out diagnostic))
                    {
                        diagnostic = string.Concat(
                            "Could not register authored Gang member '",
                            membership.gameObject.name,
                            "': ",
                            diagnostic);
                        return false;
                    }

                    registeredMemberCount++;
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        /// <summary>Associa o ActorId ao Grupo oficial e ao conhecimento coletivo PEGA.</summary>
        public bool TryJoinMember(
            GameObject actorRoot,
            out PEGANpcGangMembership membership,
            out string diagnostic)
        {
            membership = null;
            diagnostic = string.Empty;
            if (actorRoot == null)
            {
                diagnostic = "A gang member Actor root is required.";
                return false;
            }

            if (actorRoot.GetComponent<PEGANpcKnowledgeState>() == null)
            {
                diagnostic = "A gang member must have its own PEGANpcKnowledgeState on the Actor root.";
                return false;
            }

            membership = actorRoot.GetComponent<PEGANpcGangMembership>();
            if (membership == null)
            {
                diagnostic = "A gang member Actor must compose PEGANpcGangMembership before joining a Gang knowledge state.";
                return false;
            }

            if (!TryResolveActorId(actorRoot, out ActorId actorId, out diagnostic))
            {
                membership = null;
                return false;
            }

            if (!TryGetAuthorizedGroupIds(out List<GroupId> authorizedGroupIds, out diagnostic) ||
                !TryGetMembershipGroupIds(membership, out List<GroupId> memberGroupIds, out diagnostic))
            {
                membership = null;
                return false;
            }

            bool isAuthorizedMember = false;
            for (int memberIndex = 0; memberIndex < memberGroupIds.Count; memberIndex++)
            {
                if (authorizedGroupIds.Contains(memberGroupIds[memberIndex]))
                {
                    isAuthorizedMember = true;
                    break;
                }
            }

            if (!isAuthorizedMember)
            {
                membership = null;
                diagnostic = "The Actor's configured Group Profiles do not include a Group authorized by this knowledge state.";
                return false;
            }

            if (membership.GangKnowledgeState != null && membership.GangKnowledgeState != this)
            {
                membership = null;
                diagnostic = "This Actor already uses another PEGA Gang knowledge channel.";
                return false;
            }

            for (int groupIndex = 0; groupIndex < memberGroupIds.Count; groupIndex++)
            {
                GroupId groupId = memberGroupIds[groupIndex];
                if (!actorGroupAccessBinding.TryGetGroup(groupId, out _, out diagnostic))
                {
                    membership = null;
                    diagnostic = string.Concat(
                        "The configured Group must be live before this Actor can join it. groupId='",
                        groupId.StableText,
                        "'. ",
                        diagnostic);
                    return false;
                }
            }

            for (int groupIndex = 0; groupIndex < memberGroupIds.Count; groupIndex++)
            {
                GroupId groupId = memberGroupIds[groupIndex];
                if (!actorGroupAccessBinding.TryJoinGroup(groupId, actorId, out diagnostic))
                {
                    membership = null;
                    return false;
                }

                if (!actorGroupAccessBinding.TryIsMember(
                        groupId,
                        actorId,
                        out bool isGroupMember,
                        out diagnostic) ||
                    !isGroupMember)
                {
                    membership = null;
                    if (string.IsNullOrWhiteSpace(diagnostic))
                    {
                        diagnostic = "The Immersive Framework did not confirm the Actor's Group membership after joining.";
                    }

                    return false;
                }
            }

            if (!TryIsMember(actorRoot, out bool isMember, out diagnostic) || !isMember)
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The Immersive Framework did not confirm the Actor's Gang membership after joining.";
                }

                membership = null;
                return false;
            }

            if (!membership.TryBindKnowledgeState(this, out diagnostic))
            {
                membership = null;
                return false;
            }

            membership.RefreshGangKnowledgeSubscription();

            EnsureLoggerInitialized();
            _log.Debug(
                "NPC joined the Gang Actor Group and its PEGA knowledge channel.",
                LogFields.Field("gangKnowledgeState", gameObject.name),
                LogFields.Field("configuredGroupCount", memberGroupIds.Count),
                LogFields.Field("actorId", actorId.StableText),
                LogFields.Field("actorRoot", actorRoot.name),
                LogFields.Field("frameworkMembershipConfirmed", true),
                LogFields.Field("knownEscapePointCount", GetKnownEscapePointCount()));
            return true;
        }

        /// <summary>Consulta se o ActorId está no Grupo vivo definido para esta Gangue.</summary>
        public bool TryIsMember(GameObject actorRoot, out bool isMember, out string diagnostic)
        {
            isMember = false;
            if (!TryResolveActorId(actorRoot, out ActorId actorId, out diagnostic))
            {
                return false;
            }

            if (authorizedGroupProfiles == null || authorizedGroupProfiles.Length == 0)
            {
                diagnostic = "At least one authorized Group Profile is required for the Gang knowledge state.";
                return false;
            }

            for (int index = 0; index < authorizedGroupProfiles.Length; index++)
            {
                GroupProfile profile = authorizedGroupProfiles[index];
                if (profile == null ||
                    !profile.TryGetGroupId(out GroupId groupId, out diagnostic))
                {
                    if (profile == null)
                    {
                        diagnostic = "The authorized Group Profiles list contains a missing asset.";
                    }

                    return false;
                }

                if (!actorGroupAccessBinding.TryIsMember(
                        groupId,
                        actorId,
                        out bool isGroupMember,
                        out diagnostic))
                {
                    return false;
                }

                if (isGroupMember)
                {
                    isMember = true;
                    diagnostic = string.Empty;
                    return true;
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        /// <summary>Retorna conhecimento compartilhado somente ao ActorId que pertence ao Grupo desta Gangue.</summary>
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
                    diagnostic = "The Actor is not a member of this Gang Actor Group.";
                }

                return false;
            }

            targets = GetKnownEscapeTargetsCore();
            return true;
        }

        /// <summary>Consulta um Target conhecido somente por um integrante verificado da Gangue.</summary>
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
                    diagnostic = "The Actor is not a member of this Gang Actor Group.";
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

        /// <summary>Assina mudanças do conhecimento após confirmar a membership do ActorId.</summary>
        internal bool TrySubscribeToChanges(
            GameObject actorRoot,
            Action<PEGANpcGangKnowledgeFactChangedEvent> handler,
            out IEventBinding binding,
            out string diagnostic)
        {
            binding = null;
            if (handler == null)
            {
                diagnostic = "A Gang knowledge change handler is required.";
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
                    diagnostic = "The Actor is not a member of this Gang Actor Group.";
                }

                return false;
            }

            EnsureStateInitialized();
            _changeEvents ??= new EventBus<PEGANpcGangKnowledgeFactChangedEvent>();
            binding = _changeEvents.Subscribe(change =>
            {
                try
                {
                    handler(change);
                }
                catch (Exception exception)
                {
                    LogSubscriberFailure(handler, change, exception);
                }
            });
            diagnostic = string.Empty;
            return true;
        }

        public bool UnsubscribeFromChanges(IEventBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            return _changeEvents != null && _changeEvents.Unsubscribe(binding);
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

            if (spawnPoint.GangKnowledgeState != this)
            {
                diagnostic = "The Spawn Point must reference this Gang knowledge state.";
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
                diagnostic = "The entering ActorId is not a member of this Gang Actor Group.";
                return false;
            }

            string normalizedId = spawnPointId.Trim();
            if (_knownEscapePoints.TryGetValue(normalizedId, out KnownSpawnPointFact existing))
            {
                if (!ReferenceEquals(existing.Source, spawnPoint))
                {
                    diagnostic = "Another Spawn Point already uses this stable id in the Gang.";
                    return false;
                }

                return true;
            }

            var targetId = PEGANpcPlanningTargetId.ForStableDataId(
                string.Concat(EscapeTargetIdPrefix, normalizedId));
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
                "Gang learned a potential escape from physical Spawn Point use.",
                LogFields.Field("gangKnowledgeState", gameObject.name),
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
                "Gang learned a new escape route.",
                LogFields.Field("actorRoot", memberRoot.name));

            PublishChange(new PEGANpcGangKnowledgeFactChangedEvent(
                normalizedId,
                target,
                PEGANpcGangKnowledgeEvidenceOrigin.SpawnPointPhysicalIngress,
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

        private bool TryGetAuthorizedGroupIds(out List<GroupId> groupIds, out string diagnostic)
        {
            groupIds = new List<GroupId>();
            if (authorizedGroupProfiles == null || authorizedGroupProfiles.Length == 0)
            {
                diagnostic = "At least one authorized Group Profile is required for the Gang knowledge state.";
                return false;
            }

            var uniqueGroupIds = new HashSet<GroupId>();
            for (int index = 0; index < authorizedGroupProfiles.Length; index++)
            {
                GroupProfile profile = authorizedGroupProfiles[index];
                if (profile == null)
                {
                    diagnostic = "The authorized Group Profiles list contains a missing asset.";
                    groupIds.Clear();
                    return false;
                }

                if (!profile.TryGetGroupId(out GroupId groupId, out diagnostic))
                {
                    groupIds.Clear();
                    return false;
                }

                if (!uniqueGroupIds.Add(groupId))
                {
                    diagnostic = string.Concat(
                        "The authorized Group Profiles list repeats GroupId '",
                        groupId.StableText,
                        "'.");
                    groupIds.Clear();
                    return false;
                }

                groupIds.Add(groupId);
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool TryGetMembershipGroupIds(
            PEGANpcGangMembership membership,
            out List<GroupId> groupIds,
            out string diagnostic)
        {
            groupIds = new List<GroupId>();
            if (membership == null)
            {
                diagnostic = "A composed PEGANpcGangMembership is required for this Actor.";
                return false;
            }

            IReadOnlyList<GroupProfile> profiles = membership.GroupProfiles;
            if (profiles == null || profiles.Count == 0)
            {
                diagnostic = "The Actor's Gang Membership must declare at least one Group Profile.";
                return false;
            }

            var uniqueGroupIds = new HashSet<GroupId>();
            for (int index = 0; index < profiles.Count; index++)
            {
                GroupProfile profile = profiles[index];
                if (profile == null)
                {
                    diagnostic = "The Actor's Gang Membership contains a missing Group Profile asset.";
                    groupIds.Clear();
                    return false;
                }

                if (!profile.TryGetGroupId(out GroupId groupId, out diagnostic))
                {
                    groupIds.Clear();
                    return false;
                }

                if (!uniqueGroupIds.Add(groupId))
                {
                    diagnostic = string.Concat(
                        "The Actor's Gang Membership repeats GroupId '",
                        groupId.StableText,
                        "'.");
                    groupIds.Clear();
                    return false;
                }

                groupIds.Add(groupId);
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryMatchesAuthorizedGroupConfiguration(
            PEGANpcGangMembership membership,
            out bool matches,
            out string diagnostic)
        {
            matches = false;
            if (!TryGetAuthorizedGroupIds(out List<GroupId> authorizedGroupIds, out diagnostic) ||
                !TryGetMembershipGroupIds(membership, out List<GroupId> memberGroupIds, out diagnostic))
            {
                return false;
            }

            for (int index = 0; index < memberGroupIds.Count; index++)
            {
                if (authorizedGroupIds.Contains(memberGroupIds[index]))
                {
                    matches = true;
                    break;
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryResolveActorId(GameObject actorRoot, out ActorId actorId, out string diagnostic)
        {
            actorId = default;
            if (actorRoot == null)
            {
                diagnostic = "A Gang Actor root is required.";
                return false;
            }

            if (actorGroupAccessBinding == null)
            {
                diagnostic = "An Activity-owned Actor Group access binding is required for the Gang.";
                return false;
            }

            ActorDeclaration declaration = actorRoot.GetComponent<ActorDeclaration>();
            if (declaration == null)
            {
                diagnostic = "A Gang member Actor must have an ActorDeclaration on its root.";
                return false;
            }

            try
            {
                actorId = declaration.ActorId;
            }
            catch (Exception exception)
            {
                diagnostic = "The Gang member ActorId is invalid. " + exception.Message;
                actorId = default;
                return false;
            }

            if (!actorId.IsValid)
            {
                diagnostic = "A valid ActorId is required for Gang membership.";
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
                diagnostic = "A Gang knowledge Actor root is required.";
                return false;
            }

            PEGANpcGangMembership membership = actorRoot.GetComponent<PEGANpcGangMembership>();
            if (membership == null || membership.GangKnowledgeState != this)
            {
                diagnostic = "The Actor must be bound to this PEGA Gang knowledge channel.";
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
                "Gang Spawn Point use was not accepted as knowledge.",
                LogFields.Field("gangKnowledgeState", gameObject.name),
                LogFields.Field("spawnPointId", spawnPointId ?? string.Empty),
                LogFields.Field("memberRoot", memberRoot != null ? memberRoot.name : "unavailable"),
                LogFields.Field("reason", reason ?? string.Empty));
        }

        private void PublishChange(PEGANpcGangKnowledgeFactChangedEvent change)
        {
            if (_changeEvents == null)
            {
                return;
            }

            _changeEvents.Publish(change);
        }

        private void LogSubscriberFailure(
            Action<PEGANpcGangKnowledgeFactChangedEvent> handler,
            PEGANpcGangKnowledgeFactChangedEvent change,
            Exception exception)
        {
            EnsureLoggerInitialized();
            _log.Error(
                "Gang knowledge subscriber failed; the accepted fact and remaining subscribers are preserved.",
                LogFields.Field("gangKnowledgeState", gameObject.name),
                LogFields.Field("spawnPointId", change.SpawnPointId),
                LogFields.Field("subscriberMethod", handler.Method.Name),
                LogFields.Field("exceptionType", exception.GetType().FullName),
                LogFields.Field("exceptionMessage", exception.Message));
        }

        private int GetKnownEscapePointCount()
        {
            EnsureStateInitialized();
            return _knownEscapePoints.Count;
        }

        private void EnsureStateInitialized()
        {
            _knownEscapePoints ??= new Dictionary<string, KnownSpawnPointFact>(StringComparer.Ordinal);
        }

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PEGALogging.CreateScopedLogger<PEGANpcGangKnowledgeState>(LogCategory, gameObject.name);
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
    }
}

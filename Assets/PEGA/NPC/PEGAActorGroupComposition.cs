using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.RouteLifecycle;
using Immersive.Framework.RuntimeContent;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    internal interface IPEGAActorGroupMember
    {
        GameObject ActorRoot { get; }

        IReadOnlyList<GroupProfile> GroupProfiles { get; }
    }

    internal interface IPEGAActorGroupMembershipListener
    {
        void OnFrameworkGroupMembershipConfirmed(GroupId groupId);
    }

    /// <summary>Cria os Groups declarados pelo owner e associa seus Actors pelos IDs lógicos.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorGroupAccessBinding))]
    [AddComponentMenu("PEGA/Actors/Actor Group Composition")]
    public sealed class PEGAActorGroupComposition : MonoBehaviour,
        IActivityContentLifecycleReceiver,
        IRouteContentLifecycleReceiver
    {
        private const string LogCategory = "PEGA.Gameplay.ActorGroups";

        [SerializeField]
        [Tooltip("Groups criados explicitamente por este owner. Um Group pode não ter membros.")]
        private GroupProfile[] groupsToCreate = Array.Empty<GroupProfile>();

        [SerializeField]
        [Tooltip("Contexto PEGA de conhecimento compartilhado pelos owners desta Session. Opcional quando não há conhecimento de Group.")]
        private PEGAActorGroupKnowledgeContext knowledgeContext;

        private readonly List<PEGAActorGroupKnowledgeState> _activeKnowledgeStates = new();
        private readonly List<PEGAActorGroupMembership> _boundKnowledgeMembers = new();

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        void IActivityContentLifecycleReceiver.OnActivityContentEntered(ActivityContentLifecycleContext context)
        {
            if (context.ContentRoot != gameObject)
            {
                return;
            }

            EnterOwner(
                RuntimeContentScope.Activity,
                context.ActivityName,
                context.Activity != null && context.Activity.HasValidActivityId);
        }

        void IActivityContentLifecycleReceiver.OnActivityContentExited(ActivityContentLifecycleContext context)
        {
            if (context.ContentRoot == gameObject)
            {
                ReleaseKnowledge();
            }
        }

        void IRouteContentLifecycleReceiver.OnRouteContentEntered(RouteContentLifecycleContext context)
        {
            if (context.ContentRoot != gameObject)
            {
                return;
            }

            EnterOwner(
                RuntimeContentScope.Route,
                context.RouteName,
                context.Route != null && context.Route.HasValidRouteId);
        }

        void IRouteContentLifecycleReceiver.OnRouteContentExited(RouteContentLifecycleContext context)
        {
            if (context.ContentRoot == gameObject)
            {
                ReleaseKnowledge();
            }
        }

        private void OnDestroy() => ReleaseKnowledge();

        private void EnterOwner(RuntimeContentScope ownerScope, string ownerName, bool hasValidOwner)
        {
            EnsureLoggerInitialized();
            if (!hasValidOwner)
            {
                _log.Error(
                    "Actor Group composition requires a valid Framework owner.",
                    LogFields.Field("ownerScope", ownerScope),
                    LogFields.Field("contentRoot", gameObject.name));
                return;
            }

            if (!TryCompose(ownerScope, ownerName, out string diagnostic))
            {
                _log.Error(
                    "Actor Group composition failed.",
                    LogFields.Field("ownerScope", ownerScope),
                    LogFields.Field("owner", ownerName),
                    LogFields.Field("contentRoot", gameObject.name),
                    LogFields.Field("reason", diagnostic));
            }
        }

        private bool TryCompose(
            RuntimeContentScope ownerScope,
            string ownerName,
            out string diagnostic)
        {
            ActorGroupAccessBinding groupAccess = GetComponent<ActorGroupAccessBinding>();
            if (groupAccess == null || !groupAccess.IsAccessAvailable)
            {
                diagnostic = "The owner-bound Actor Group access binding is unavailable.";
                return false;
            }

            if (!TryResolveDeclarations(
                    groupAccess,
                    out List<GroupProfile> declaredGroups,
                    out List<ResolvedMembership> memberships,
                    out List<IPEGAActorGroupMember> configuredActors,
                    out diagnostic))
            {
                return false;
            }

            var localKnowledgeStates = new List<PEGAActorGroupKnowledgeState>();
            var knowledgeByGroup = new HashSet<GroupId>();
            PEGAActorGroupKnowledgeState[] knowledgeCandidates =
                GetComponentsInChildren<PEGAActorGroupKnowledgeState>(true);
            for (int index = 0; index < knowledgeCandidates.Length; index++)
            {
                PEGAActorGroupKnowledgeState state = knowledgeCandidates[index];
                if (!IsWithinThisContribution(state.transform))
                {
                    continue;
                }

                GroupProfile profile = state.GroupProfile;
                if (knowledgeContext == null || profile == null ||
                    !profile.TryGetGroupId(out GroupId groupId, out diagnostic) ||
                    !knowledgeByGroup.Add(groupId) || !declaredGroups.Contains(profile))
                {
                    diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                        ? "Each Group knowledge state requires one distinct Group created by this owner and an explicit shared context."
                        : diagnostic;
                    return false;
                }

                localKnowledgeStates.Add(state);
            }

            for (int index = 0; index < declaredGroups.Count; index++)
            {
                GroupProfile profile = declaredGroups[index];
                if (!groupAccess.TryCreateGroup(profile, out ActorGroupSnapshot group, out diagnostic))
                {
                    diagnostic = $"Could not create authored Actor Group '{profile.GroupIdText}'. {diagnostic}";
                    return false;
                }

                if (!group.IsValid || group.Profile != profile)
                {
                    diagnostic = $"Framework returned an unexpected Group for '{profile.GroupIdText}'.";
                    return false;
                }

                _log.Info(
                    "Framework created an owner-bound Actor Group.",
                    LogFields.Field("ownerScope", ownerScope),
                    LogFields.Field("owner", ownerName),
                    LogFields.Field("groupId", group.GroupId.StableText));
            }

            for (int index = 0; index < localKnowledgeStates.Count; index++)
            {
                PEGAActorGroupKnowledgeState state = localKnowledgeStates[index];
                if (!state.TryActivate(groupAccess, knowledgeContext, ownerScope.ToString(), out diagnostic))
                {
                    ReleaseKnowledge();
                    return false;
                }

                _activeKnowledgeStates.Add(state);
            }

            for (int index = 0; index < configuredActors.Count; index++)
            {
                if (configuredActors[index] is PEGAActorGroupMembership member && knowledgeContext != null)
                {
                    TryBindMemberKnowledge(member);
                }
            }

            for (int index = 0; index < memberships.Count; index++)
            {
                ResolvedMembership membership = memberships[index];
                if (!groupAccess.TryGetGroup(
                        membership.GroupId,
                        out ActorGroupSnapshot group,
                        out diagnostic))
                {
                    diagnostic = $"Actor Group '{membership.GroupId.StableText}' is unavailable for membership. {diagnostic}";
                    ReleaseKnowledge();
                    return false;
                }

                if (group.HasProfile && group.Profile != membership.Profile)
                {
                    diagnostic = $"Actor '{membership.ActorId.StableText}' references a different Group Profile for '{membership.GroupId.StableText}'.";
                    ReleaseKnowledge();
                    return false;
                }

                if (!groupAccess.TryJoinGroup(membership.GroupId, membership.ActorId, out diagnostic))
                {
                    diagnostic = $"Could not associate Actor '{membership.ActorId.StableText}' with Group '{membership.GroupId.StableText}'. {diagnostic}";
                    ReleaseKnowledge();
                    return false;
                }

                if (!groupAccess.TryIsMember(
                        membership.GroupId,
                        membership.ActorId,
                        out bool isMember,
                        out diagnostic) || !isMember)
                {
                    diagnostic = $"Framework did not confirm membership of Actor '{membership.ActorId.StableText}' in Group '{membership.GroupId.StableText}'. {diagnostic}";
                    ReleaseKnowledge();
                    return false;
                }

                _log.Info(
                    "Framework confirmed Actor Group membership.",
                    LogFields.Field("ownerScope", ownerScope),
                    LogFields.Field("owner", ownerName),
                    LogFields.Field("groupId", membership.GroupId.StableText),
                    LogFields.Field("actorId", membership.ActorId.StableText),
                    LogFields.Field("actorRoot", membership.Configuration.ActorRoot.name));

                if (membership.Configuration is IPEGAActorGroupMembershipListener listener)
                {
                    listener.OnFrameworkGroupMembershipConfirmed(membership.GroupId);
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryResolveDeclarations(
            ActorGroupAccessBinding groupAccess,
            out List<GroupProfile> declaredGroups,
            out List<ResolvedMembership> memberships,
            out List<IPEGAActorGroupMember> configuredActors,
            out string diagnostic)
        {
            declaredGroups = new List<GroupProfile>();
            memberships = new List<ResolvedMembership>();
            configuredActors = new List<IPEGAActorGroupMember>();
            var declaredById = new Dictionary<GroupId, GroupProfile>();
            if (groupsToCreate == null)
            {
                diagnostic = "The owner requires an explicit Group creation list, which may be empty.";
                return false;
            }

            GroupProfile[] authoredGroups = groupsToCreate;
            for (int index = 0; index < authoredGroups.Length; index++)
            {
                GroupProfile profile = authoredGroups[index];
                if (profile == null)
                {
                    diagnostic = "The owner has a missing Group Profile in its creation list.";
                    return false;
                }

                if (!profile.TryGetGroupId(out GroupId groupId, out diagnostic))
                {
                    return false;
                }

                if (!declaredById.TryAdd(groupId, profile))
                {
                    diagnostic = $"The owner declares GroupId '{groupId.StableText}' more than once.";
                    return false;
                }

                declaredGroups.Add(profile);
            }

            foreach (KeyValuePair<GroupId, GroupProfile> entry in declaredById)
            {
                if (groupAccess.TryGetGroup(entry.Key, out _, out _))
                {
                    diagnostic = $"Owner-declared GroupId '{entry.Key.StableText}' is already live.";
                    return false;
                }
            }

            var seenActors = new HashSet<ActorId>();
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                if (behaviours[behaviourIndex] is not IPEGAActorGroupMember configuration)
                {
                    continue;
                }

                if (!IsWithinThisContribution(behaviours[behaviourIndex].transform))
                {
                    continue;
                }

                GameObject actorRoot = configuration.ActorRoot;
                if (actorRoot == null || !IsWithinThisContribution(actorRoot.transform))
                {
                    diagnostic = "A Group member must belong to this content contribution, not a nested owner or composition.";
                    return false;
                }

                ActorDeclaration declaration = actorRoot.GetComponent<ActorDeclaration>();
                if (declaration == null)
                {
                    diagnostic = "Each Actor Group member requires an ActorDeclaration on its root.";
                    return false;
                }

                ActorId actorId;
                try
                {
                    actorId = declaration.ActorId;
                }
                catch (ArgumentException exception)
                {
                    diagnostic = $"Actor '{actorRoot.name}' has an invalid ActorId. {exception.Message}";
                    return false;
                }

                if (!seenActors.Add(actorId))
                {
                    diagnostic = $"ActorId '{actorId.StableText}' is declared more than once under this Group composition.";
                    return false;
                }

                configuredActors.Add(configuration);

                var seenGroups = new HashSet<GroupId>();
                IReadOnlyList<GroupProfile> profiles = configuration.GroupProfiles;
                if (profiles == null)
                {
                    diagnostic = $"Actor '{actorRoot.name}' has no Group Profile list.";
                    return false;
                }

                for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
                {
                    GroupProfile profile = profiles[profileIndex];
                    if (profile == null)
                    {
                        diagnostic = $"Actor '{actorRoot.name}' has a missing Group Profile.";
                        return false;
                    }

                    if (!profile.TryGetGroupId(out GroupId groupId, out diagnostic))
                    {
                        return false;
                    }

                    if (!seenGroups.Add(groupId))
                    {
                        diagnostic = $"Actor '{actorRoot.name}' repeats GroupId '{groupId.StableText}'.";
                        return false;
                    }

                    if (declaredById.TryGetValue(groupId, out GroupProfile declaredProfile))
                    {
                        if (declaredProfile != profile)
                        {
                            diagnostic = $"Actor '{actorRoot.name}' references a different Profile for owner-declared GroupId '{groupId.StableText}'.";
                            return false;
                        }
                    }
                    else
                    {
                        if (!groupAccess.TryGetGroup(groupId, out ActorGroupSnapshot liveGroup, out diagnostic))
                        {
                            diagnostic = $"Actor '{actorRoot.name}' references a Group that is not live under another owner. {diagnostic}";
                            return false;
                        }

                        if (liveGroup.HasProfile && liveGroup.Profile != profile)
                        {
                            diagnostic = $"Actor '{actorRoot.name}' references a different Profile for live GroupId '{groupId.StableText}'.";
                            return false;
                        }
                    }

                    memberships.Add(new ResolvedMembership(configuration, actorId, groupId, profile));
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool IsWithinThisContribution(Transform actorTransform)
        {
            for (Transform current = actorTransform; current != null; current = current.parent)
            {
                if (current == transform)
                {
                    return true;
                }

                if (current.GetComponent<ActivityContentContribution>() != null ||
                    current.GetComponent<RouteContentContribution>() != null ||
                    current.GetComponent<PEGAActorGroupComposition>() != null)
                {
                    return false;
                }
            }

            return false;
        }

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PEGALogging.CreateScopedLogger<PEGAActorGroupComposition>(LogCategory, gameObject.name);
            _loggerInitialized = true;
        }

        private void ReleaseKnowledge()
        {
            for (int index = 0; index < _boundKnowledgeMembers.Count; index++)
            {
                if (_boundKnowledgeMembers[index] != null)
                {
                    _boundKnowledgeMembers[index].UnbindKnowledgeContext(knowledgeContext);
                }
            }

            _boundKnowledgeMembers.Clear();
            for (int index = 0; index < _activeKnowledgeStates.Count; index++)
            {
                if (_activeKnowledgeStates[index] != null)
                {
                    _activeKnowledgeStates[index].Deactivate();
                }
            }

            _activeKnowledgeStates.Clear();
        }

        internal bool TryBindMemberKnowledge(PEGAActorGroupMembership member)
        {
            if (member == null || knowledgeContext == null ||
                !IsWithinThisContribution(member.transform))
            {
                return false;
            }

            ActorGroupAccessBinding groupAccess = GetComponent<ActorGroupAccessBinding>();
            if (groupAccess == null || !groupAccess.IsAccessAvailable)
            {
                return false;
            }

            if (!_boundKnowledgeMembers.Contains(member))
            {
                member.BindKnowledgeContext(knowledgeContext, groupAccess);
                _boundKnowledgeMembers.Add(member);
            }

            return true;
        }

        private readonly struct ResolvedMembership
        {
            internal ResolvedMembership(
                IPEGAActorGroupMember configuration,
                ActorId actorId,
                GroupId groupId,
                GroupProfile profile)
            {
                Configuration = configuration;
                ActorId = actorId;
                GroupId = groupId;
                Profile = profile;
            }

            internal IPEGAActorGroupMember Configuration { get; }

            internal ActorId ActorId { get; }

            internal GroupId GroupId { get; }

            internal GroupProfile Profile { get; }
        }
    }
}


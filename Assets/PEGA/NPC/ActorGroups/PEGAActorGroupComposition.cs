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
                context.Activity != null && context.Activity.HasValidActivityId,
                context.Activity,
                context.Contribution);
        }

        void IActivityContentLifecycleReceiver.OnActivityContentExited(ActivityContentLifecycleContext context)
        {
            if (context.ContentRoot == gameObject)
            {
                GetComponent<PEGAActorGroupKnowledgeState>()?.EndOwner(
                    RuntimeContentScope.Activity,
                    context.Activity,
                    context.Contribution);
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
                context.Route != null && context.Route.HasValidRouteId,
                context.Route,
                context.Binding);
        }

        void IRouteContentLifecycleReceiver.OnRouteContentExited(RouteContentLifecycleContext context)
        {
            if (context.ContentRoot == gameObject)
            {
                GetComponent<PEGAActorGroupKnowledgeState>()?.EndOwner(
                    RuntimeContentScope.Route,
                    context.Route,
                    context.Binding);
            }
        }

        private void EnterOwner(
            RuntimeContentScope ownerScope,
            string ownerName,
            bool hasValidOwner,
            UnityEngine.Object ownerIdentity,
            Component ownerContribution)
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

            if (!TryCompose(ownerScope, ownerName, ownerIdentity, ownerContribution, out string diagnostic))
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
            UnityEngine.Object ownerIdentity,
            Component ownerContribution,
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
                    out diagnostic))
            {
                return false;
            }

            PEGAActorGroupKnowledgeState groupKnowledge = GetComponent<PEGAActorGroupKnowledgeState>();
            bool groupKnowledgeAvailable = false;
            if (groupKnowledge != null)
            {
                if (!groupKnowledge.TryValidateInitialKnowledgeGroups(
                        declaredGroups,
                        out string initialKnowledgeDiagnostic))
                {
                    _log.Error(
                        "Group initial Knowledge authoring was rejected; Framework Group creation and membership will continue, and an enabled Group Knowledge authority will still bind independently.",
                        LogFields.Field("ownerScope", ownerScope),
                        LogFields.Field("owner", ownerName),
                        LogFields.Field("reason", initialKnowledgeDiagnostic));
                }

                if (!groupKnowledge.isActiveAndEnabled)
                {
                    if (groupKnowledge.HasAuthoredInitialKnowledge)
                    {
                        _log.Error(
                            "Designer-authored Group Knowledge was rejected because its optional authority is disabled; Framework Group creation and membership will continue.",
                            LogFields.Field("ownerScope", ownerScope),
                            LogFields.Field("owner", ownerName));
                    }
                    else
                    {
                        _log.Warning(
                            "Optional Group Knowledge is disabled; Actor Group creation and membership will continue without it.",
                            LogFields.Field("ownerScope", ownerScope),
                            LogFields.Field("owner", ownerName));
                    }
                }
                else if (groupKnowledge.TryBeginOwner(
                             ownerScope,
                             ownerIdentity,
                             ownerContribution,
                             groupAccess,
                             declaredGroups,
                             out string beginKnowledgeDiagnostic))
                {
                    groupKnowledgeAvailable = true;
                }
                else
                {
                    if (groupKnowledge.HasAuthoredInitialKnowledge)
                    {
                        _log.Error(
                            "Designer-authored Group Knowledge was rejected because its authority could not bind to this owner; Framework Group creation and membership will continue.",
                            LogFields.Field("ownerScope", ownerScope),
                            LogFields.Field("owner", ownerName),
                            LogFields.Field("reason", beginKnowledgeDiagnostic));
                    }
                    else
                    {
                        _log.Warning(
                            "Optional Group Knowledge could not bind to this Actor Group owner; Actor Group creation and membership will continue.",
                            LogFields.Field("ownerScope", ownerScope),
                            LogFields.Field("owner", ownerName),
                            LogFields.Field("reason", beginKnowledgeDiagnostic));
                    }
                }
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

                if (groupKnowledgeAvailable &&
                    !groupKnowledge.TryRegisterCreatedGroup(group, groupAccess, out string registrationDiagnostic))
                {
                    if (groupKnowledge.HasAuthoredInitialKnowledgeFor(profile))
                    {
                        _log.Error(
                            "Designer-authored Group Knowledge was rejected because no store was registered for this live Framework Group; composition will continue.",
                            LogFields.Field("groupId", group.GroupId.StableText),
                            LogFields.Field("reason", registrationDiagnostic));
                    }
                    else
                    {
                        _log.Warning(
                            "Optional Group Knowledge did not create a store for a Framework Actor Group; composition will continue.",
                            LogFields.Field("groupId", group.GroupId.StableText),
                            LogFields.Field("reason", registrationDiagnostic));
                    }
                }

                _log.Info(
                    "Framework created an owner-bound Actor Group.",
                    LogFields.Field("ownerScope", ownerScope),
                    LogFields.Field("owner", ownerName),
                    LogFields.Field("groupId", group.GroupId.StableText));
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
                    return false;
                }

                if (group.HasProfile && group.Profile != membership.Profile)
                {
                    diagnostic = $"Actor '{membership.ActorId.StableText}' references a different Group Profile for '{membership.GroupId.StableText}'.";
                    return false;
                }

                if (!groupAccess.TryJoinGroup(membership.GroupId, membership.ActorId, out diagnostic))
                {
                    diagnostic = $"Could not associate Actor '{membership.ActorId.StableText}' with Group '{membership.GroupId.StableText}'. {diagnostic}";
                    return false;
                }

                if (!groupAccess.TryIsMember(
                        membership.GroupId,
                        membership.ActorId,
                        out bool isMember,
                        out diagnostic) || !isMember)
                {
                    diagnostic = $"Framework did not confirm membership of Actor '{membership.ActorId.StableText}' in Group '{membership.GroupId.StableText}'. {diagnostic}";
                    return false;
                }

                _log.Info(
                    "Framework confirmed Actor Group membership.",
                    LogFields.Field("ownerScope", ownerScope),
                    LogFields.Field("owner", ownerName),
                    LogFields.Field("groupId", membership.GroupId.StableText),
                    LogFields.Field("actorId", membership.ActorId.StableText),
                    LogFields.Field("actorRoot", membership.Configuration.ActorRoot.name));
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryResolveDeclarations(
            ActorGroupAccessBinding groupAccess,
            out List<GroupProfile> declaredGroups,
            out List<ResolvedMembership> memberships,
            out string diagnostic)
        {
            declaredGroups = new List<GroupProfile>();
            memberships = new List<ResolvedMembership>();
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


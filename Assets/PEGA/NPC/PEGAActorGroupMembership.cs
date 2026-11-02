using System.Collections.Generic;
using Immersive.Framework.Actors;
using UnityEngine;
using UnityEngine.Serialization;

namespace PEGA.NPC
{
    /// <summary>Declara os Actor Groups do Actor e expõe o acesso autorizado ao conhecimento de cada Group.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorDeclaration))]
    [AddComponentMenu("PEGA/Actors/Actor Group Membership")]
    public sealed class PEGAActorGroupMembership : MonoBehaviour,
        IPEGAActorGroupMember,
        IPEGAActorGroupMembershipListener
    {
        [Header("Actor Groups")]
        [SerializeField]
        [FormerlySerializedAs("groupDefinitions")]
        [Tooltip("Group Profiles aos quais este Actor pertence. O Framework confirma o vínculo runtime pelo ActorId.")]
        private GroupProfile[] groupProfiles = System.Array.Empty<GroupProfile>();

        [System.NonSerialized]
        private PEGAActorGroupKnowledgeContext _knowledgeContext;

        [System.NonSerialized]
        private ActorGroupAccessBinding _groupAccess;

        internal IReadOnlyList<GroupProfile> GroupProfiles =>
            groupProfiles ?? System.Array.Empty<GroupProfile>();

        GameObject IPEGAActorGroupMember.ActorRoot => gameObject;

        IReadOnlyList<GroupProfile> IPEGAActorGroupMember.GroupProfiles => GroupProfiles;

        private void OnEnable()
        {
            PEGAActorGroupComposition composition = GetComponentInParent<PEGAActorGroupComposition>();
            composition?.TryBindMemberKnowledge(this);
        }

        internal void BindKnowledgeContext(
            PEGAActorGroupKnowledgeContext knowledgeContext,
            ActorGroupAccessBinding groupAccess)
        {
            if (knowledgeContext == null || groupAccess == null)
            {
                throw new System.ArgumentNullException(knowledgeContext == null
                    ? nameof(knowledgeContext) : nameof(groupAccess));
            }

            _knowledgeContext = knowledgeContext;
            _groupAccess = groupAccess;
        }

        internal void UnbindKnowledgeContext(PEGAActorGroupKnowledgeContext knowledgeContext)
        {
            if (_knowledgeContext != knowledgeContext)
            {
                return;
            }

            _knowledgeContext = null;
            _groupAccess = null;
        }

        public bool TryGetAuthorizedKnowledgeStates(
            out IReadOnlyList<PEGAActorGroupKnowledgeState> states,
            out string diagnostic)
        {
            states = System.Array.Empty<PEGAActorGroupKnowledgeState>();
            if (_knowledgeContext == null || _groupAccess == null || !_groupAccess.IsAccessAvailable)
            {
                diagnostic = "This Actor has no active PEGA Group knowledge context.";
                return false;
            }

            ActorDeclaration declaration = GetComponent<ActorDeclaration>();
            if (declaration == null)
            {
                diagnostic = "An ActorDeclaration is required for Group knowledge access.";
                return false;
            }

            ActorId actorId;
            try
            {
                actorId = declaration.ActorId;
            }
            catch (System.ArgumentException exception)
            {
                diagnostic = $"This Actor has an invalid ActorId. {exception.Message}";
                return false;
            }

            if (!actorId.IsValid)
            {
                diagnostic = "A valid ActorId is required for Group knowledge access.";
                return false;
            }

            if (!_groupAccess.TryGetGroupsForActor(actorId, int.MaxValue,
                    out IReadOnlyList<GroupId> groupIds, out diagnostic))
            {
                return false;
            }

            var authorized = new List<PEGAActorGroupKnowledgeState>(groupIds.Count);
            for (int index = 0; index < groupIds.Count; index++)
            {
                GroupId groupId = groupIds[index];
                if (_knowledgeContext.TryGetAuthority(groupId, out PEGAActorGroupKnowledgeState state) &&
                    state.TryIsMember(gameObject, out bool isMember, out _) && isMember)
                {
                    authorized.Add(state);
                }
            }

            states = authorized;
            diagnostic = string.Empty;
            return true;
        }

        void IPEGAActorGroupMembershipListener.OnFrameworkGroupMembershipConfirmed(GroupId groupId)
        {
            if (!groupId.IsValid || _knowledgeContext == null ||
                !_knowledgeContext.TryGetAuthority(groupId, out PEGAActorGroupKnowledgeState state))
            {
                return;
            }

            state.NotifyFrameworkGroupMembershipConfirmed(gameObject, groupId);
        }
    }
}

using System.Collections.Generic;
using Immersive.Framework.Actors;
using UnityEngine;
using UnityEngine.Serialization;

namespace PEGA.NPC
{
    /// <summary>Declara os Actor Groups aos quais este Actor pertence.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorDeclaration))]
    [AddComponentMenu("PEGA/Actors/Actor Group Membership")]
    public sealed class PEGAActorGroupMembership : MonoBehaviour, IPEGAActorGroupMember
    {
        [Header("Actor Groups")]
        [SerializeField]
        [FormerlySerializedAs("groupDefinitions")]
        [Tooltip("Group Profiles aos quais este Actor pertence. O Framework confirma o vínculo runtime pelo ActorId.")]
        private GroupProfile[] groupProfiles = System.Array.Empty<GroupProfile>();

        internal IReadOnlyList<GroupProfile> GroupProfiles =>
            groupProfiles ?? System.Array.Empty<GroupProfile>();

        GameObject IPEGAActorGroupMember.ActorRoot => gameObject;

        IReadOnlyList<GroupProfile> IPEGAActorGroupMember.GroupProfiles => GroupProfiles;
    }
}

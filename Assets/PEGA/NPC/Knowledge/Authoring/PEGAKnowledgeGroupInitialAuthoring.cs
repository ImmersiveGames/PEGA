using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using UnityEngine;
namespace PEGA.NPC.Knowledge.Authoring
{
    /// <summary>Initial facts selected for one Group Profile that the owner must create through the Framework.</summary>
    [Serializable]
    public sealed class PegaKnowledgeGroupInitialAuthoring
    {
        [SerializeField, Tooltip("Profile selecionado para o Group que este owner declara criar. O Profile fornece o GroupId; não cria nem representa sozinho um Group vivo.")]
        private GroupProfile groupProfile;

        [SerializeField, Tooltip("Fatos iniciais do Group. Cada linha usa o mesmo catálogo e editor Identity → Property → Value dos demais escopos.")]
        private PegaKnowledgeFactAuthoringEntry[] facts = Array.Empty<PegaKnowledgeFactAuthoringEntry>();

        internal GroupProfile GroupProfile => groupProfile;

        internal IReadOnlyList<PegaKnowledgeFactAuthoringEntry> Facts =>
            facts ?? Array.Empty<PegaKnowledgeFactAuthoringEntry>();

        internal bool HasFacts => facts != null && facts.Length > 0;

        internal PegaKnowledgeGroupInitialAuthoring Snapshot()
        {
            PegaKnowledgeGroupInitialAuthoring snapshot =
                (PegaKnowledgeGroupInitialAuthoring)MemberwiseClone();
            snapshot.facts = facts != null
                ? (PegaKnowledgeFactAuthoringEntry[])facts.Clone()
                : Array.Empty<PegaKnowledgeFactAuthoringEntry>();
            return snapshot;
        }
    }
}

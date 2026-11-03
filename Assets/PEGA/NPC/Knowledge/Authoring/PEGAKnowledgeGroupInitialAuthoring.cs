using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>Initial facts selected for one Group Profile that the owner must create through the Framework.</summary>
    [Serializable]
    public sealed class PEGAKnowledgeGroupInitialAuthoring
    {
        [SerializeField, Tooltip("Profile selecionado para o Group que este owner declara criar. O Profile fornece o GroupId; não cria nem representa sozinho um Group vivo.")]
        private GroupProfile groupProfile;

        [SerializeField, Tooltip("Fatos iniciais do Group. Cada linha usa o mesmo catálogo e editor Identity → Property → Value dos demais escopos.")]
        private PEGAKnowledgeFactAuthoringEntry[] facts = Array.Empty<PEGAKnowledgeFactAuthoringEntry>();

        internal GroupProfile GroupProfile => groupProfile;

        internal IReadOnlyList<PEGAKnowledgeFactAuthoringEntry> Facts =>
            facts ?? Array.Empty<PEGAKnowledgeFactAuthoringEntry>();

        internal bool HasFacts => facts != null && facts.Length > 0;

        internal PEGAKnowledgeGroupInitialAuthoring Snapshot()
        {
            PEGAKnowledgeGroupInitialAuthoring snapshot =
                (PEGAKnowledgeGroupInitialAuthoring)MemberwiseClone();
            snapshot.facts = facts != null
                ? (PEGAKnowledgeFactAuthoringEntry[])facts.Clone()
                : Array.Empty<PEGAKnowledgeFactAuthoringEntry>();
            return snapshot;
        }
    }
}

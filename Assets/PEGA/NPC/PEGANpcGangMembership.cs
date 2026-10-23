using System.Collections.Generic;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>Vínculo explícito entre um NPC e o conhecimento compartilhado de sua Gangue.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PEGANpcKnowledgeState))]
    [AddComponentMenu("PEGA/NPC/Gang Membership")]
    public sealed class PEGANpcGangMembership : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Estado compartilhado da Gangue deste NPC.")]
        private PEGANpcGangKnowledgeState gangKnowledgeState;

        public PEGANpcGangKnowledgeState GangKnowledgeState => gangKnowledgeState;

        public bool TryGetKnownEscapeTargets(
            out IReadOnlyList<PEGANpcPlanningTarget> targets,
            out string diagnostic)
        {
            if (gangKnowledgeState == null)
            {
                targets = null;
                diagnostic = "This NPC has no valid Gang knowledge membership.";
                return false;
            }

            targets = gangKnowledgeState.GetKnownEscapeTargets();
            diagnostic = string.Empty;
            return true;
        }

        internal bool TryBind(PEGANpcGangKnowledgeState state, out string diagnostic)
        {
            if (state == null)
            {
                diagnostic = "A Gang knowledge state is required.";
                return false;
            }

            if (gangKnowledgeState != null && gangKnowledgeState != state)
            {
                diagnostic = "This NPC is already associated with another Gang knowledge state.";
                return false;
            }

            gangKnowledgeState = state;
            diagnostic = string.Empty;
            return true;
        }
    }
}

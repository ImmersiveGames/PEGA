using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using ImmersiveGames.Goap;
using UnityEngine;
using UnityEngine.Serialization;

namespace PEGA.NPC
{
    /// <summary>Declara os Grupos do Actor e conecta o conhecimento coletivo da Gangue ao GOAP do PEGA.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PEGANpcKnowledgeState))]
    [AddComponentMenu("PEGA/NPC/Gang Membership")]
    public sealed class PEGANpcGangMembership : MonoBehaviour,
        IPEGANpcPlanningInputChangeSource,
        IGoapKnowledgeCapability
    {
        [Header("Actor Groups")]
        [SerializeField]
        [FormerlySerializedAs("groupDefinitions")]
        [Tooltip("Group Profiles aos quais este Actor pertence. O Framework confirma o vínculo runtime pelo ActorId.")]
        private GroupProfile[] groupProfiles = System.Array.Empty<GroupProfile>();

        [SerializeField]
        [Tooltip("Canal de conhecimento coletivo da Gangue usado pelo PEGA.")]
        private PEGANpcGangKnowledgeState gangKnowledgeState;

        [System.NonSerialized]
        private EventBus<PEGANpcPlanningInputsChangedEvent> _planningInputEvents;

        [System.NonSerialized]
        private IEventBinding _gangKnowledgeBinding;

        public PEGANpcGangKnowledgeState GangKnowledgeState => gangKnowledgeState;

        internal IReadOnlyList<GroupProfile> GroupProfiles =>
            groupProfiles ?? System.Array.Empty<GroupProfile>();

        private void OnEnable()
        {
            SubscribeToGangKnowledge();
        }

        private void OnDisable()
        {
            UnsubscribeFromGangKnowledge();
        }

        private void OnDestroy()
        {
            UnsubscribeFromGangKnowledge();
            _planningInputEvents?.Clear();
            _planningInputEvents = null;
        }

        public IEventBinding SubscribeToPlanningInputChanges(
            System.Action<PEGANpcPlanningInputsChangedEvent> handler)
        {
            if (handler == null)
            {
                throw new System.ArgumentNullException(nameof(handler));
            }

            _planningInputEvents ??= new EventBus<PEGANpcPlanningInputsChangedEvent>();
            return _planningInputEvents.Subscribe(handler);
        }

        public bool UnsubscribeFromPlanningInputChanges(IEventBinding binding)
        {
            if (binding == null)
            {
                throw new System.ArgumentNullException(nameof(binding));
            }

            return _planningInputEvents != null && _planningInputEvents.Unsubscribe(binding);
        }

        public bool TryGetKnownEscapeTargets(
            out IReadOnlyList<PEGANpcPlanningTarget> targets,
            out string diagnostic)
        {
            if (gangKnowledgeState == null)
            {
                targets = null;
                diagnostic = "This NPC has no valid Gang knowledge access.";
                return false;
            }

            return gangKnowledgeState.TryGetKnownEscapeTargets(gameObject, out targets, out diagnostic);
        }

        internal bool TryBindKnowledgeState(PEGANpcGangKnowledgeState state, out string diagnostic)
        {
            if (state == null)
            {
                diagnostic = "A PEGA Gang knowledge state is required.";
                return false;
            }

            if (gangKnowledgeState != null && gangKnowledgeState != state)
            {
                diagnostic = "This Actor already uses another PEGA Gang knowledge channel.";
                return false;
            }

            gangKnowledgeState = state;
            if (isActiveAndEnabled)
            {
                SubscribeToGangKnowledge();
            }

            diagnostic = string.Empty;
            return true;
        }

        internal void RefreshGangKnowledgeSubscription()
        {
            SubscribeToGangKnowledge();
        }

        private void SubscribeToGangKnowledge()
        {
            if (gangKnowledgeState != null && _gangKnowledgeBinding == null)
            {
                if (gangKnowledgeState.TrySubscribeToChanges(
                        gameObject,
                        OnGangKnowledgeChanged,
                        out IEventBinding binding,
                        out _))
                {
                    _gangKnowledgeBinding = binding;
                }
            }
        }

        private void UnsubscribeFromGangKnowledge()
        {
            if (gangKnowledgeState != null && _gangKnowledgeBinding != null)
            {
                gangKnowledgeState.UnsubscribeFromChanges(_gangKnowledgeBinding);
                _gangKnowledgeBinding = null;
            }
        }

        private void OnGangKnowledgeChanged(PEGANpcGangKnowledgeFactChangedEvent change)
        {
            if (change == null ||
                change.EvidenceOrigin != PEGANpcGangKnowledgeEvidenceOrigin.SpawnPointPhysicalIngress)
            {
                return;
            }

            if (gangKnowledgeState == null ||
                !gangKnowledgeState.TryIsMember(gameObject, out bool isMember, out _) ||
                !isMember)
            {
                return;
            }

            _planningInputEvents ??= new EventBus<PEGANpcPlanningInputsChangedEvent>();
            _planningInputEvents.Publish(new PEGANpcPlanningInputsChangedEvent());
        }
    }
}

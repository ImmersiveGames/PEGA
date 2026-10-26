using System.Collections.Generic;
using Immersive.Foundation.Events;
using PEGA.GOAP;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>Vínculo explícito entre um NPC e o conhecimento compartilhado de sua Gangue.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PEGANpcKnowledgeState))]
    [AddComponentMenu("PEGA/NPC/Gang Membership")]
    public sealed class PEGANpcGangMembership : MonoBehaviour,
        IPEGANpcPlanningInputChangeSource,
        IPEGAGoapKnowledgeCapability
    {
        [SerializeField]
        [Tooltip("Estado compartilhado da Gangue deste NPC.")]
        private PEGANpcGangKnowledgeState gangKnowledgeState;

        [System.NonSerialized]
        private EventBus<PEGANpcPlanningInputsChangedEvent> _planningInputEvents;

        [System.NonSerialized]
        private IEventBinding _gangKnowledgeBinding;

        public PEGANpcGangKnowledgeState GangKnowledgeState => gangKnowledgeState;

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
            if (isActiveAndEnabled)
            {
                SubscribeToGangKnowledge();
            }

            diagnostic = string.Empty;
            return true;
        }

        private void SubscribeToGangKnowledge()
        {
            if (gangKnowledgeState != null && _gangKnowledgeBinding == null)
            {
                _gangKnowledgeBinding = gangKnowledgeState.SubscribeToChanges(OnGangKnowledgeChanged);
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

            _planningInputEvents ??= new EventBus<PEGANpcPlanningInputsChangedEvent>();
            _planningInputEvents.Publish(new PEGANpcPlanningInputsChangedEvent());
        }
    }
}

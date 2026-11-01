using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>Typed identity of the physical manifestation whose position a source recorded.</summary>
    public interface IPEGAActorGroupLocationSubjectEvidence
    {
    }

    /// <summary>Optional source owned by one live PEGA Group knowledge authority.</summary>
    public abstract class PEGAActorGroupKnowledgeSource : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Autoridade de conhecimento do Group que aceita fatos desta fonte.")]
        private PEGAActorGroupKnowledgeState actorGroupKnowledgeState;

        private bool _ownerActive;

        protected PEGAActorGroupKnowledgeState KnowledgeState => actorGroupKnowledgeState;

        protected bool IsKnowledgeSourceActive => _ownerActive;

        internal bool IsKnowledgeSourceActiveForOwner => _ownerActive;

        internal bool IsConfiguredFor(PEGAActorGroupKnowledgeState knowledgeState) =>
            ReferenceEquals(actorGroupKnowledgeState, knowledgeState);

        internal void Activate()
        {
            if (_ownerActive)
            {
                return;
            }

            _ownerActive = true;
            OnKnowledgeSourceEntered();
        }

        internal void Deactivate()
        {
            if (!_ownerActive)
            {
                return;
            }

            OnKnowledgeSourceExited();
            _ownerActive = false;
        }

        private void OnDestroy() => Deactivate();

        protected virtual void OnKnowledgeSourceEntered()
        {
        }

        protected virtual void OnKnowledgeSourceExited()
        {
        }

        internal abstract void RefreshAuthorizedInitialKnowledge();

        protected bool TryRecordInitialLocation(
            ScriptableObject entityIdentity,
            IPEGAActorGroupLocationSubjectEvidence locationSubjectEvidence,
            Vector3 initialLocation,
            double evidenceTimeSeconds,
            string sourceEvidence,
            out string diagnostic)
        {
            if (!_ownerActive || actorGroupKnowledgeState == null)
            {
                diagnostic = "The knowledge source is unavailable outside its Group owner lifecycle.";
                return false;
            }

            return actorGroupKnowledgeState.TryRecordAuthorizedInitialLocation(
                this,
                entityIdentity,
                locationSubjectEvidence,
                initialLocation,
                evidenceTimeSeconds,
                sourceEvidence,
                out diagnostic);
        }
    }
}

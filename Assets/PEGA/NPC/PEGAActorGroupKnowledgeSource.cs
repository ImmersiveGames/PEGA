using UnityEngine;

namespace PEGA.NPC
{
    internal interface IPEGAActorGroupKnowledgeFactSource
    {
        PEGAActorGroupKnowledgeState KnowledgeState { get; }

        bool IsActive { get; }

        bool IsActiveForOwner { get; }

        bool IsConfiguredFor(PEGAActorGroupKnowledgeState knowledgeState);
    }

    /// <summary>Optional source owned by one live PEGA Group knowledge authority.</summary>
    public abstract class PEGAActorGroupKnowledgeSource : MonoBehaviour, IPEGAActorGroupKnowledgeFactSource
    {
        [SerializeField]
        [Tooltip("Autoridade de conhecimento do Group que aceita fatos desta fonte.")]
        private PEGAActorGroupKnowledgeState actorGroupKnowledgeState;

        private bool _ownerActive;

        protected PEGAActorGroupKnowledgeState KnowledgeState => actorGroupKnowledgeState;

        PEGAActorGroupKnowledgeState IPEGAActorGroupKnowledgeFactSource.KnowledgeState => actorGroupKnowledgeState;

        bool IPEGAActorGroupKnowledgeFactSource.IsActive => isActiveAndEnabled;

        bool IPEGAActorGroupKnowledgeFactSource.IsActiveForOwner => _ownerActive;

        protected bool IsKnowledgeSourceActive => _ownerActive;

        internal bool IsConfiguredFor(PEGAActorGroupKnowledgeState knowledgeState) =>
            ReferenceEquals(actorGroupKnowledgeState, knowledgeState);

        bool IPEGAActorGroupKnowledgeFactSource.IsConfiguredFor(
            PEGAActorGroupKnowledgeState knowledgeState) => IsConfiguredFor(knowledgeState);

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

        protected bool TryRecordFact<TAddress, TValue>(
            GameObject memberRoot,
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult result,
            out string diagnostic)
        {
            result = PEGAKnowledgeWriteResult.RejectedInvalid;
            if (!_ownerActive || actorGroupKnowledgeState == null)
            {
                diagnostic = "The knowledge source is unavailable outside its Group owner lifecycle.";
                return false;
            }

            return actorGroupKnowledgeState.TryRecordFact(
                this,
                memberRoot,
                key,
                value,
                evidence,
                out result,
                out diagnostic);
        }

        protected bool TryInvalidateFact<TAddress, TValue>(
            GameObject memberRoot,
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult result,
            out string diagnostic)
        {
            result = PEGAKnowledgeWriteResult.RejectedInvalid;
            if (!_ownerActive || actorGroupKnowledgeState == null)
            {
                diagnostic = "The knowledge source is unavailable outside its Group owner lifecycle.";
                return false;
            }

            return actorGroupKnowledgeState.TryInvalidateFact(
                this,
                memberRoot,
                key,
                evidence,
                out result,
                out diagnostic);
        }
    }
}

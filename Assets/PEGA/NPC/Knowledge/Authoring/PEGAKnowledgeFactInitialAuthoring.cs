using Immersive.Framework.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>Writer route implemented by a Knowledge authority or authority-bound writer.</summary>
    public interface IPEGAKnowledgeFactWriter
    {
        PEGAKnowledgeWriteResult ObserveFact<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PEGAKnowledgeEvidence evidence);
    }

    /// <summary>Optional typed authoring capability kept separate from definition metadata.</summary>
    public interface IPEGAKnowledgeFactInitialAuthoringDefinition : IPEGAKnowledgeFactDefinition
    {
        bool TryWriteInitialFact(
            object address,
            object value,
            IPEGAKnowledgeFactWriter writer,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult result,
            out string diagnostic);
    }

    internal enum PEGAKnowledgeFactInitialAuthoringStatus
    {
        Applied,
        Pending,
        Rejected
    }

    /// <summary>One serialized Identity → Property → Value row shared by Knowledge scopes.</summary>
    [System.Serializable]
    public struct PEGAKnowledgeFactAuthoringEntry
    {
        [SerializeField, HideInInspector]
        private string definitionId;

        [SerializeField, HideInInspector]
        private PEGAKnowledgeFactAuthoringPayload address;

        [SerializeField, HideInInspector]
        private PEGAKnowledgeFactAuthoringPayload value;

        public string DefinitionId => definitionId;

        internal PEGAKnowledgeFactInitialAuthoringStatus TryApply(
            ActorOccurrenceReferenceBinding actorBinding,
            IPEGAKnowledgeFactWriter writer,
            PEGAKnowledgeEvidence evidence,
            out PEGAKnowledgeWriteResult result,
            out string diagnostic)
        {
            result = PEGAKnowledgeWriteResult.RejectedInvalid;
            if (!PEGAKnowledgeFactDefinitionCatalog.TryGet(definitionId, out IPEGAKnowledgeFactDefinition definition))
            {
                diagnostic = $"Fact definition '{definitionId}' is not in the canonical catalog.";
                return PEGAKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            if (!definition.AllowsDesignerAuthoredInitialFact)
            {
                diagnostic = $"Fact definition '{definition.Id}' does not allow designer-authored initial facts.";
                return PEGAKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            if (!PEGAKnowledgeFactAuthoringPayload.Supports(definition.AddressType) ||
                !PEGAKnowledgeFactAuthoringPayload.Supports(definition.ValueType))
            {
                diagnostic = $"Fact definition '{definition.Id}' uses a type without an Inspector payload.";
                return PEGAKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            if (!address.TryGetValue(definition.AddressType, actorBinding, out object resolvedAddress,
                    out bool addressPending, out diagnostic))
            {
                return addressPending
                    ? PEGAKnowledgeFactInitialAuthoringStatus.Pending
                    : PEGAKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            if (!value.TryGetValue(definition.ValueType, actorBinding, out object resolvedValue,
                    out bool valuePending, out diagnostic))
            {
                return valuePending
                    ? PEGAKnowledgeFactInitialAuthoringStatus.Pending
                    : PEGAKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            if (!(definition is IPEGAKnowledgeFactInitialAuthoringDefinition authoringDefinition))
            {
                diagnostic = $"Fact definition '{definition.Id}' has no typed initial-authoring adapter.";
                return PEGAKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            PEGAKnowledgeEvidence definitionEvidence = new PEGAKnowledgeEvidence(
                evidence.Origin,
                evidence.ObservationTimeSeconds,
                evidence.Source,
                definition.Id);
            if (!authoringDefinition.TryWriteInitialFact(
                    resolvedAddress,
                    resolvedValue,
                    writer,
                    definitionEvidence,
                    out result,
                    out diagnostic))
            {
                return PEGAKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            return PEGAKnowledgeFactInitialAuthoringStatus.Applied;
        }
    }
}

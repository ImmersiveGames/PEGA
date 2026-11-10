using Immersive.Framework.Actors;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using UnityEngine;
namespace PEGA.NPC.Knowledge.Authoring
{
    /// <summary>Writer route implemented by a Knowledge authority or authority-bound writer.</summary>
    public interface IPegaKnowledgeFactWriter
    {
        PegaKnowledgeWriteResult ObserveFact<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PegaKnowledgeEvidence evidence);
    }

    /// <summary>Optional typed authoring capability kept separate from definition metadata.</summary>
    public interface IPegaKnowledgeFactInitialAuthoringDefinition : IPegaKnowledgeFactDefinition
    {
        bool TryWriteInitialFact(
            object address,
            object value,
            IPegaKnowledgeFactWriter writer,
            PegaKnowledgeEvidence evidence,
            out PegaKnowledgeWriteResult result,
            out string diagnostic);
    }

    internal enum PegaKnowledgeFactInitialAuthoringStatus
    {
        Applied,
        Pending,
        Rejected
    }

    /// <summary>One serialized Identity → Property → Value row shared by Knowledge scopes.</summary>
    [System.Serializable]
    public struct PegaKnowledgeFactAuthoringEntry
    {
        [SerializeField, HideInInspector]
        private string definitionId;

        [SerializeField, HideInInspector]
        private PegaKnowledgeFactAuthoringPayload address;

        [SerializeField, HideInInspector]
        private PegaKnowledgeFactAuthoringPayload value;

        public string DefinitionId => definitionId;

        internal PegaKnowledgeFactInitialAuthoringStatus TryApply(
            ActorOccurrenceReferenceBinding actorBinding,
            IPegaKnowledgeFactWriter writer,
            PegaKnowledgeEvidence evidence,
            out PegaKnowledgeWriteResult result,
            out string diagnostic)
        {
            result = PegaKnowledgeWriteResult.RejectedInvalid;
            if (!PegaKnowledgeFactDefinitionCatalog.TryGet(definitionId, out IPegaKnowledgeFactDefinition definition))
            {
                diagnostic = $"Fact definition '{definitionId}' is not in the canonical catalog.";
                return PegaKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            if (!definition.AllowsDesignerAuthoredInitialFact)
            {
                diagnostic = $"Fact definition '{definition.Id}' does not allow designer-authored initial facts.";
                return PegaKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            if (!PegaKnowledgeFactAuthoringPayload.Supports(definition.AddressType) ||
                !PegaKnowledgeFactAuthoringPayload.Supports(definition.ValueType))
            {
                diagnostic = $"Fact definition '{definition.Id}' uses a type without an Inspector payload.";
                return PegaKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            if (!address.TryGetValue(definition.AddressType, actorBinding, null, out object resolvedAddress,
                    out bool addressPending, out diagnostic))
            {
                return addressPending
                    ? PegaKnowledgeFactInitialAuthoringStatus.Pending
                    : PegaKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            if (!value.TryGetValue(definition.ValueType, actorBinding, resolvedAddress, out object resolvedValue,
                    out bool valuePending, out diagnostic))
            {
                return valuePending
                    ? PegaKnowledgeFactInitialAuthoringStatus.Pending
                    : PegaKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            if (!(definition is IPegaKnowledgeFactInitialAuthoringDefinition authoringDefinition))
            {
                diagnostic = $"Fact definition '{definition.Id}' has no typed initial-authoring adapter.";
                return PegaKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            PegaKnowledgeEvidence definitionEvidence = new PegaKnowledgeEvidence(
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
                return PegaKnowledgeFactInitialAuthoringStatus.Rejected;
            }

            return PegaKnowledgeFactInitialAuthoringStatus.Applied;
        }
    }
}

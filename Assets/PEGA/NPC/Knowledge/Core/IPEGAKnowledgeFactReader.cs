using PEGA.GOAP;
using PEGA.NPC.Knowledge.Definitions;
namespace PEGA.NPC.Knowledge.Core
{
    /// <summary>Read-only access to typed facts owned by one explicit PEGA Knowledge scope authority.</summary>
    public interface IPegaKnowledgeFactReader : IGoapKnowledgeCapability
    {
        PegaKnowledgeScope Scope { get; }

        bool IsAvailable { get; }

        PegaKnowledgeFact<TValue> ReadFact<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds);
    }
}

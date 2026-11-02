using ImmersiveGames.Goap;

namespace PEGA.NPC
{
    /// <summary>Read-only access to typed facts owned by one explicit PEGA Knowledge scope authority.</summary>
    public interface IPEGAKnowledgeFactReader : IGoapKnowledgeCapability
    {
        PEGAKnowledgeScope Scope { get; }

        bool IsAvailable { get; }

        PEGAKnowledgeFact<TValue> ReadFact<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds);
    }
}

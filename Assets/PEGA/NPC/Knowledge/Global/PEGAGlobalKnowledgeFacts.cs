using UnityEngine.Scripting;

namespace PEGA.NPC
{
    /// <summary>Canonical definitions for facts whose authority is Global.</summary>
    [Preserve]
    public static class PEGAGlobalKnowledgeFacts
    {
        [Preserve]
        public static readonly PEGAKnowledgeFactDefinition<byte, bool> MainObjectiveIdentified =
            new PEGAKnowledgeFactDefinition<byte, bool>(
                "pega.global.main-objective-identified",
                "Main objective identified",
                new PEGALatestEvidenceKnowledgePolicy<bool>(),
                address => address == 0,
                allowsDesignerAuthoredInitialFact: false);
    }
}

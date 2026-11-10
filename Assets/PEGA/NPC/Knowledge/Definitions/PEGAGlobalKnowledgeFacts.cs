using PEGA.NPC.Knowledge.Core;
using UnityEngine.Scripting;
namespace PEGA.NPC.Knowledge.Definitions
{
    /// <summary>Canonical definitions for facts whose authority is Global.</summary>
    [Preserve]
    public static class PegaGlobalKnowledgeFacts
    {
        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<byte, bool> MainObjectiveIdentified =
            new PegaKnowledgeFactDefinition<byte, bool>(
                "pega.global.main-objective-identified",
                "Main objective identified",
                new PegaLatestEvidenceKnowledgePolicy<bool>(),
                address => address == 0,
                allowsDesignerAuthoredInitialFact: false);
    }
}

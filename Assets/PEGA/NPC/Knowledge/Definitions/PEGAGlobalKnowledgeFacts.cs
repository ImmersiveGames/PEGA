using PEGA.NPC.Knowledge.Core;
using UnityEngine.Scripting;
namespace PEGA.NPC.Knowledge.Definitions
{
    /// <summary>Canonical definitions for facts whose authority is Global.</summary>
    [Preserve]
    public static class PegaGlobalKnowledgeFacts
    {
        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<byte, PegaRelicDiscovery> RelicDiscovery =
            new PegaKnowledgeFactDefinition<byte, PegaRelicDiscovery>(
                "pega.global.relic-discovery",
                "First relic discovery",
                new RelicDiscoveryPolicy(),
                address => address == 0,
                allowsDesignerAuthoredInitialFact: false);

        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<byte, bool> MainObjectiveIdentified =
            new PegaKnowledgeFactDefinition<byte, bool>(
                "pega.global.main-objective-identified",
                "Main objective identified",
                new PegaLatestEvidenceKnowledgePolicy<bool>(),
                address => address == 0,
                allowsDesignerAuthoredInitialFact: false);

        private sealed class RelicDiscoveryPolicy : IPegaKnowledgeFactPolicy<PegaRelicDiscovery>
        {
            public PegaKnowledgeFactUpdate<PegaRelicDiscovery> Observe(
                PegaKnowledgeFact<PegaRelicDiscovery> current, PegaRelicDiscovery value,
                PegaKnowledgeEvidence evidence, PegaKnowledgeScope scope)
            {
                if (scope != PegaKnowledgeScope.Global || !value.IsValid ||
                    value.TimeSeconds != evidence.ObservationTimeSeconds)
                {
                    return new PegaKnowledgeFactUpdate<PegaRelicDiscovery>(
                        PegaKnowledgeWriteResult.RejectedInvalid, current, false);
                }

                if (current.HasValue)
                {
                    return new PegaKnowledgeFactUpdate<PegaRelicDiscovery>(
                        PegaKnowledgeWriteResult.NoChange, current, false);
                }

                var first = PegaKnowledgeFact<PegaRelicDiscovery>.Create(
                    PegaKnowledgeStatus.Known, true, value.TimeSeconds, new[] { evidence },
                    PegaKnowledgeInvalidationCause.None, value, value, true, scope);
                return new PegaKnowledgeFactUpdate<PegaRelicDiscovery>(PegaKnowledgeWriteResult.Recorded, first, true);
            }

            public PegaKnowledgeFactUpdate<PegaRelicDiscovery> Invalidate(
                PegaKnowledgeFact<PegaRelicDiscovery> current, PegaKnowledgeEvidence evidence, PegaKnowledgeScope scope) =>
                new PegaKnowledgeFactUpdate<PegaRelicDiscovery>(PegaKnowledgeWriteResult.RejectedInvalid, current, false);

            public PegaKnowledgeFact<PegaRelicDiscovery> EvaluateValidity(
                PegaKnowledgeFact<PegaRelicDiscovery> current, double queryTimeSeconds, PegaKnowledgeScope scope) => current;
        }
    }
}

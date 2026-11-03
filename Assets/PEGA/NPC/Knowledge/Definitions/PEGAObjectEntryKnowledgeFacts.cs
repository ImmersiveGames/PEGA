using Immersive.Framework.ObjectEntry;
using UnityEngine;
using UnityEngine.Scripting;

namespace PEGA.NPC
{
    /// <summary>Canonical properties addressed by the Framework's logical ObjectEntry identity.</summary>
    [Preserve]
    public static class PEGAObjectEntryKnowledgeFacts
    {
        [Preserve]
        public static readonly PEGAKnowledgeFactDefinition<ObjectEntryId, Vector3> Position =
            new PEGAKnowledgeFactDefinition<ObjectEntryId, Vector3>(
                "pega.object-entry.position",
                "Object Entry position",
                new PEGALatestEvidenceKnowledgePolicy<Vector3>(
                    areEqual: AreExactlyEqual,
                    validateValue: value => IsFinite(value)
                        ? string.Empty
                        : "Position contains a non-finite coordinate."),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: true);

        [Preserve]
        public static readonly PEGAKnowledgeFactDefinition<ObjectEntryId, bool> InvestigationRequired =
            new PEGAKnowledgeFactDefinition<ObjectEntryId, bool>(
                "pega.poi.investigation-required",
                "POI investigation required",
                new PEGALatestEvidenceKnowledgePolicy<bool>(),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: true);

        [Preserve]
        public static readonly PEGAKnowledgeFactDefinition<ObjectEntryId, bool> InvestigationCompleted =
            new PEGAKnowledgeFactDefinition<ObjectEntryId, bool>(
                "pega.poi.investigation-completed",
                "POI investigation completed",
                new PEGALatestEvidenceKnowledgePolicy<bool>(),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: true);

        [Preserve]
        public static readonly PEGAKnowledgeFactDefinition<ObjectEntryId, bool> ActivityExit =
            new PEGAKnowledgeFactDefinition<ObjectEntryId, bool>(
                "pega.poi.activity-exit",
                "Activity exit POI",
                new PEGALatestEvidenceKnowledgePolicy<bool>(),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: true);

        private static bool AreExactlyEqual(Vector3 left, Vector3 right) =>
            left.x == right.x && left.y == right.y && left.z == right.z;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

using Immersive.Framework.ObjectEntry;
using PEGA.Interactions.Runtime;
using PEGA.NPC.Knowledge.Core;
using UnityEngine;
using UnityEngine.Scripting;
namespace PEGA.NPC.Knowledge.Definitions
{
    /// <summary>Canonical properties addressed by the Framework's logical ObjectEntry identity.</summary>
    [Preserve]
    public static class PegaObjectEntryKnowledgeFacts
    {
        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<ObjectEntryId, Vector3> Position =
            new PegaKnowledgeFactDefinition<ObjectEntryId, Vector3>(
                "pega.object-entry.position",
                "Object Entry position",
                new PegaLatestEvidenceKnowledgePolicy<Vector3>(
                    areEqual: AreExactlyEqual,
                    validateValue: value => IsFinite(value)
                        ? string.Empty
                        : "Position contains a non-finite coordinate."),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: true);

        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<ObjectEntryId, bool> PickUpAvailable =
            new PegaKnowledgeFactDefinition<ObjectEntryId, bool>(
                "pega.object-entry.pick-up-available",
                "Object Entry pick up available",
                new PegaLatestEvidenceKnowledgePolicy<bool>(),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: true);

        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<ObjectEntryId, PegaPickUpObjectEntryConfigurationSnapshot> PickUpPlanningParameters =
            new PegaKnowledgeFactDefinition<ObjectEntryId, PegaPickUpObjectEntryConfigurationSnapshot>(
                "pega.object-entry.pick-up-planning-parameters",
                "Object Entry Pick Up planning parameters",
                new PegaLatestEvidenceKnowledgePolicy<PegaPickUpObjectEntryConfigurationSnapshot>(
                    areEqual: (left, right) => left.Equals(right),
                    validateValue: value => value.IsValid
                        ? string.Empty
                        : "Pick Up planning parameters require a valid ObjectEntryId and finite, non-negative range and duration."),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: true);

        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<ObjectEntryId, bool> InvestigationRequired =
            new PegaKnowledgeFactDefinition<ObjectEntryId, bool>(
                "pega.poi.investigation-required",
                "POI investigation required",
                new PegaLatestEvidenceKnowledgePolicy<bool>(),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: true);

        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<ObjectEntryId, bool> InvestigationCompleted =
            new PegaKnowledgeFactDefinition<ObjectEntryId, bool>(
                "pega.poi.investigation-completed",
                "POI investigation completed",
                new PegaLatestEvidenceKnowledgePolicy<bool>(),
                address => address.IsValid,
                allowsDesignerAuthoredInitialFact: true);

        [Preserve]
        public static readonly PegaKnowledgeFactDefinition<ObjectEntryId, bool> ActivityExit =
            new PegaKnowledgeFactDefinition<ObjectEntryId, bool>(
                "pega.poi.activity-exit",
                "Activity exit POI",
                new PegaLatestEvidenceKnowledgePolicy<bool>(),
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

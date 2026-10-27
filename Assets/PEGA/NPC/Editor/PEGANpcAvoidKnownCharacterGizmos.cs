using Immersive.Framework.Actors;
using UnityEditor;
using UnityEngine;

namespace PEGA.NPC.Editor
{
    /// <summary>Development-only visualization for the configured safe-separation Action.</summary>
    internal static class PEGANpcAvoidKnownCharacterGizmos
    {
        [DrawGizmo(GizmoType.Selected)]
        private static void Draw(PEGANpcAutonomy autonomy, GizmoType gizmoType)
        {
            if (autonomy == null ||
                !TryFindDefinition(autonomy, out PEGANpcAvoidKnownCharacterAction action) ||
                !TryGetKnownCharacterPosition(autonomy, out Vector3 knownPosition))
            {
                return;
            }

            Color previousColor = Handles.color;
            try
            {
                Handles.color = new Color(1f, 0.65f, 0.1f, 0.9f);
                Handles.DrawWireDisc(knownPosition, Vector3.up, action.SafeSeparationDistanceMeters);
            }
            finally
            {
                Handles.color = previousColor;
            }
        }

        private static bool TryFindDefinition(
            PEGANpcAutonomy autonomy,
            out PEGANpcAvoidKnownCharacterAction action)
        {
            for (int index = 0; index < autonomy.ActionDefinitionCount; index++)
            {
                if (autonomy.GetActionDefinitionAt(index) is PEGANpcAvoidKnownCharacterAction definition)
                {
                    action = definition;
                    return true;
                }
            }

            action = null;
            return false;
        }

        private static bool TryGetKnownCharacterPosition(PEGANpcAutonomy autonomy, out Vector3 position)
        {
            position = default;
            PEGANpcKnowledgeState knowledge = autonomy.GetComponent<PEGANpcKnowledgeState>();
            ActorOccurrenceReferenceBinding reference = autonomy.GetComponent<ActorOccurrenceReferenceBinding>();
            if (knowledge == null || reference == null ||
                !reference.TryGetCurrent(out ActorOccurrenceResolution resolution) ||
                !resolution.Occurrence.ActorId.IsValid)
            {
                return false;
            }

            ActorId targetActorId = resolution.Occurrence.ActorId;
            PEGANpcKnowledgeFact<ActorProfileId> identity = knowledge.GetObservedCharacterIdentity(targetActorId);
            PEGANpcKnowledgeFact<Vector3> lastPosition = knowledge.GetLastObservedCharacterPosition(targetActorId);
            return identity.TryGetValue(out _) &&
                   (identity.Origins & PEGANpcEvidenceOrigin.Perception) != 0 &&
                   lastPosition.TryGetValue(out position) &&
                   lastPosition.HasObservationTime;
        }
    }
}

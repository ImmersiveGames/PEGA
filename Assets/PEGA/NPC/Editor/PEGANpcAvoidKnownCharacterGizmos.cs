using Immersive.Framework.Actors;
using PEGA.NPC.Actions.Runtime;
using PEGA.NPC.Autonomy;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Individual;
using UnityEditor;
using UnityEngine;

namespace PEGA.NPC.Editor
{
    /// <summary>Development-only visualization for the configured safe-separation Action.</summary>
    internal static class PegaNpcAvoidKnownCharacterGizmos
    {
        [DrawGizmo(GizmoType.Selected)]
        private static void Draw(PegaNpcAutonomy autonomy, GizmoType gizmoType)
        {
            if (autonomy == null ||
                !TryFindDefinition(autonomy, out PegaNpcAvoidKnownCharacterAction action) ||
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
            PegaNpcAutonomy autonomy,
            out PegaNpcAvoidKnownCharacterAction action)
        {
            for (int index = 0; index < autonomy.ActionDefinitionCount; index++)
            {
                if (autonomy.GetActionDefinitionAt(index) is PegaNpcAvoidKnownCharacterAction definition)
                {
                    action = definition;
                    return true;
                }
            }

            action = null;
            return false;
        }

        private static bool TryGetKnownCharacterPosition(PegaNpcAutonomy autonomy, out Vector3 position)
        {
            position = default;
            PegaNpcKnowledgeState knowledge = autonomy.GetComponent<PegaNpcKnowledgeState>();
            ActorOccurrenceReferenceBinding reference = autonomy.GetComponent<ActorOccurrenceReferenceBinding>();
            if (knowledge == null || reference == null ||
                !reference.TryGetCurrent(out ActorOccurrenceResolution resolution) ||
                !resolution.Occurrence.ActorId.IsValid)
            {
                return false;
            }

            ActorId targetActorId = resolution.Occurrence.ActorId;
            PegaKnowledgeFact<Vector3> lastPosition = knowledge.GetLastObservedCharacterPosition(targetActorId);
            return lastPosition.TryGetValue(out position) &&
                   lastPosition.HasObservationTime &&
                   lastPosition.HasOrigin(PegaNpcEvidenceOrigin.Perception);
        }
    }
}

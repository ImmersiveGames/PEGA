using UnityEditor;
using Immersive.Framework.Actors;
using UnityEngine;

namespace PEGA.NPC.Editor
{
    /// <summary>Development-only visualization for the bounded local investigation area.</summary>
    internal static class PEGANpcInvestigateLastKnownCharacterPositionGizmos
    {
        private static readonly Color BoundColor = new Color(0.35f, 0.85f, 1f, 0.85f);
        private static readonly Color ActiveTargetColor = new Color(0.3f, 1f, 0.45f, 1f);
        private static readonly Color RepositionThresholdColor = new Color(1f, 0.72f, 0.25f, 0.8f);

        [DrawGizmo(GizmoType.Selected)]
        private static void Draw(
            PEGANpcAutonomy autonomy,
            GizmoType gizmoType)
        {
            PEGANpcInvestigateLastKnownCharacterPositionAction action = FindDefinition(autonomy);
            if (autonomy == null || action == null)
            {
                return;
            }

            float radius = action.LocalRadiusMeters;
            float verticalBand = action.MaximumVerticalDifferenceMeters;
            if (!IsPositiveFinite(radius) || !IsNonNegativeFinite(verticalBand))
            {
                return;
            }

            Vector3 anchor = autonomy.transform.position;
            bool hasLastKnownAnchor = TryGetLastKnownAnchor(autonomy, out anchor);
            Color previousColor = Handles.color;
            try
            {
                Handles.color = BoundColor;
                DrawVerticalBounds(anchor, radius, verticalBand);
                Handles.Label(
                    anchor + Vector3.up * (verticalBand + 0.1f),
                    hasLastKnownAnchor
                        ? "Área local em torno da última posição conhecida"
                        : "Prévia da área local (âncora em runtime)");

                if (IsNonNegativeFinite(action.ConfiguredArrivalRadiusMeters) &&
                    IsPositiveFinite(action.MinimumPhysicalRepositionMeters))
                {
                    Handles.color = RepositionThresholdColor;
                    Handles.DrawWireDisc(
                        autonomy.transform.position,
                        Vector3.up,
                        action.ConfiguredArrivalRadiusMeters + action.MinimumPhysicalRepositionMeters);
                    Handles.Label(
                        autonomy.transform.position + Vector3.up * 0.15f,
                        "Distância mínima do destino local");
                }

                DrawActiveTarget(autonomy);
            }
            finally
            {
                Handles.color = previousColor;
            }
        }

        private static bool TryGetLastKnownAnchor(
            PEGANpcAutonomy autonomy,
            out Vector3 anchor)
        {
            anchor = default;
            if (!Application.isPlaying)
            {
                return false;
            }

            PEGANpcPerceptionSource perception = autonomy.GetComponent<PEGANpcPerceptionSource>();
            PEGANpcKnowledgeState knowledge = autonomy.GetComponent<PEGANpcKnowledgeState>();
            ActorOccurrenceReferenceBinding reference = autonomy.GetComponent<ActorOccurrenceReferenceBinding>();
            PEGANpcPerceptionSource.PEGANpcPerceptionGizmoState sample =
                perception != null ? perception.LatestGizmoState : default;
            if (!sample.HasSample || !sample.HasTarget || sample.HasVisualContact ||
                knowledge == null || reference == null ||
                !reference.TryGetCurrent(out ActorOccurrenceResolution resolution) ||
                !resolution.Occurrence.ActorId.IsValid)
            {
                return false;
            }

            ActorId targetActorId = resolution.Occurrence.ActorId;
            PEGANpcKnowledgeFact<Immersive.Framework.Actors.ActorProfileId> identity =
                knowledge.GetObservedCharacterIdentity(targetActorId);
            PEGANpcKnowledgeFact<Vector3> position = knowledge.GetLastObservedCharacterPosition(targetActorId);
            if (!identity.TryGetValue(out _) ||
                (identity.Origins & PEGANpcEvidenceOrigin.Perception) == 0 ||
                !position.TryGetValue(out anchor) || !position.HasObservationTime)
            {
                anchor = default;
                return false;
            }

            return true;
        }

        private static void DrawVerticalBounds(Vector3 anchor, float radius, float verticalBand)
        {
            Vector3 lower = anchor - Vector3.up * verticalBand;
            Vector3 upper = anchor + Vector3.up * verticalBand;
            Handles.DrawWireDisc(lower, Vector3.up, radius);
            if (verticalBand > 0f)
            {
                Handles.DrawWireDisc(upper, Vector3.up, radius);
            }

            Handles.DrawLine(lower + Vector3.forward * radius, upper + Vector3.forward * radius);
            Handles.DrawLine(lower - Vector3.forward * radius, upper - Vector3.forward * radius);
            Handles.DrawLine(lower + Vector3.right * radius, upper + Vector3.right * radius);
            Handles.DrawLine(lower - Vector3.right * radius, upper - Vector3.right * radius);
        }

        private static void DrawActiveTarget(PEGANpcAutonomy autonomy)
        {
            if (!autonomy.TryGetActionExecution(
                    PEGANpcPlanningActionId.InvestigateLastKnownCharacterPosition,
                    out PEGANpcActionExecutionSnapshot execution))
            {
                return;
            }

            if (execution.Outcome != PEGANpcActionOutcome.Running || !execution.DeclaredEffect.IsTargetResolved)
            {
                return;
            }

            Handles.color = ActiveTargetColor;
            Handles.DrawWireDisc(execution.TargetPosition, Vector3.up, 0.18f);
            Handles.Label(execution.TargetPosition, "Destino local selecionado pelo GOAP");
        }

        private static PEGANpcInvestigateLastKnownCharacterPositionAction FindDefinition(
            PEGANpcAutonomy autonomy)
        {
            if (autonomy == null)
            {
                return null;
            }

            for (int index = 0; index < autonomy.ActionDefinitionCount; index++)
            {
                if (autonomy.GetActionDefinitionAt(index) is PEGANpcInvestigateLastKnownCharacterPositionAction action)
                {
                    return action;
                }
            }

            return null;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

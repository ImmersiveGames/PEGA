using UnityEditor;
using UnityEngine;

namespace PEGA.NPC.Editor
{
    /// <summary>Prévia de desenvolvimento da busca radial configurada e do Target em execução.</summary>
    internal static class PEGANpcInvestigatePerceivedAreaPresenceGizmos
    {
        private static readonly Color SearchBoundColor = new Color(0.95f, 0.72f, 0.25f, 0.85f);
        private static readonly Color SampleColor = new Color(1f, 0.88f, 0.35f, 0.95f);
        private static readonly Color ActiveTargetColor = new Color(0.3f, 1f, 0.45f, 1f);

        [DrawGizmo(GizmoType.Selected)]
        private static void Draw(
            PEGANpcAutonomy autonomy,
            GizmoType gizmoType)
        {
            PEGANpcInvestigatePerceivedAreaPresenceAction action = FindDefinition(autonomy);
            if (autonomy == null || action == null)
            {
                return;
            }

            if (!IsPositiveFinite(action.SearchRadiusMeters) ||
                !IsNonNegativeFinite(action.MaximumVerticalDifferenceMeters))
            {
                return;
            }

            Vector3 anchor = autonomy.transform.position;
            bool hasPresenceAnchor = TryGetPresenceAnchor(autonomy, out anchor);
            Color previousColor = Handles.color;
            try
            {
                Handles.color = SearchBoundColor;
                DrawVerticalBounds(anchor, action.SearchRadiusMeters, action.MaximumVerticalDifferenceMeters);
                Handles.Label(
                    anchor + Vector3.up * (action.MaximumVerticalDifferenceMeters + 0.1f),
                    hasPresenceAnchor
                        ? "Busca da presença radial conhecida"
                        : "Prévia da busca radial (âncora em runtime)");

                Handles.color = SampleColor;
                Vector3[] directions = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
                for (int index = 0; index < directions.Length; index++)
                {
                    Vector3 samplePosition = anchor + directions[index] * action.SearchRadiusMeters;
                    Handles.DrawWireDisc(samplePosition, Vector3.up, 0.16f);
                }

                if (IsNonNegativeFinite(action.ConfiguredArrivalRadiusMeters) &&
                    IsNonNegativeFinite(action.MinimumPhysicalRepositionMeters))
                {
                    Handles.color = ActiveTargetColor;
                    Handles.DrawWireDisc(
                        autonomy.transform.position,
                        Vector3.up,
                        action.ConfiguredArrivalRadiusMeters + action.MinimumPhysicalRepositionMeters);
                    Handles.Label(
                        autonomy.transform.position + Vector3.up * 0.15f,
                        "Deslocamento físico mínimo");
                }

                DrawActiveTarget(autonomy);
            }
            finally
            {
                Handles.color = previousColor;
            }
        }

        private static bool TryGetPresenceAnchor(
            PEGANpcAutonomy autonomy,
            out Vector3 anchor)
        {
            anchor = autonomy.transform.position;
            if (!Application.isPlaying)
            {
                return false;
            }

            PEGANpcKnowledgeState knowledgeState = autonomy.GetComponent<PEGANpcKnowledgeState>();
            if (knowledgeState == null ||
                !knowledgeState.GetPerceivedAreaPresence().TryGetValue(
                    out PEGANpcPerceivedAreaPresence presence) ||
                !presence.IsPresent)
            {
                return false;
            }

            anchor = presence.ObserverPosition;
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
                    PEGANpcPlanningActionId.InvestigatePerceivedAreaPresence,
                    out PEGANpcActionExecutionSnapshot execution))
            {
                return;
            }

            if (execution.Outcome != PEGANpcActionOutcome.Running ||
                !execution.DeclaredEffect.IsTargetResolved)
            {
                return;
            }

            Handles.color = ActiveTargetColor;
            Handles.DrawWireDisc(execution.TargetPosition, Vector3.up, 0.18f);
            Handles.Label(execution.TargetPosition, "Ponto de busca selecionado pelo GOAP");
        }

        private static PEGANpcInvestigatePerceivedAreaPresenceAction FindDefinition(
            PEGANpcAutonomy autonomy)
        {
            if (autonomy == null)
            {
                return null;
            }

            for (int index = 0; index < autonomy.ActionDefinitionCount; index++)
            {
                if (autonomy.GetActionDefinitionAt(index) is PEGANpcInvestigatePerceivedAreaPresenceAction action)
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

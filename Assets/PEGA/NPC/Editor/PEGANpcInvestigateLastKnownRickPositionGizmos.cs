using UnityEditor;
using UnityEngine;

namespace PEGA.NPC.Editor
{
    /// <summary>Development-only visualization for the bounded local investigation area.</summary>
    internal static class PEGANpcInvestigateLastKnownRickPositionGizmos
    {
        private static readonly Color BoundColor = new Color(0.35f, 0.85f, 1f, 0.85f);
        private static readonly Color ActiveTargetColor = new Color(0.3f, 1f, 0.45f, 1f);
        private static readonly Color RepositionThresholdColor = new Color(1f, 0.72f, 0.25f, 0.8f);

        [DrawGizmo(GizmoType.Selected)]
        private static void Draw(
            PEGANpcInvestigateLastKnownRickPositionAction action,
            GizmoType gizmoType)
        {
            if (action == null)
            {
                return;
            }

            SerializedObject serializedAction = new SerializedObject(action);
            serializedAction.Update();
            SerializedProperty radiusProperty = serializedAction.FindProperty("localRadiusMeters");
            SerializedProperty verticalProperty = serializedAction.FindProperty("maximumVerticalDifferenceMeters");
            SerializedProperty arrivalProperty = serializedAction.FindProperty("arrivalRadiusMeters");
            SerializedProperty repositionProperty = serializedAction.FindProperty("minimumPhysicalRepositionMeters");
            if (radiusProperty == null || verticalProperty == null)
            {
                return;
            }

            float radius = radiusProperty.floatValue;
            float verticalBand = verticalProperty.floatValue;
            if (!IsPositiveFinite(radius) || !IsNonNegativeFinite(verticalBand))
            {
                return;
            }

            Vector3 anchor = action.transform.position;
            bool hasLastKnownAnchor = TryGetLastKnownAnchor(action, out anchor);
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

                if (arrivalProperty != null && repositionProperty != null &&
                    IsNonNegativeFinite(arrivalProperty.floatValue) &&
                    IsPositiveFinite(repositionProperty.floatValue))
                {
                    Handles.color = RepositionThresholdColor;
                    Handles.DrawWireDisc(
                        action.transform.position,
                        Vector3.up,
                        arrivalProperty.floatValue + repositionProperty.floatValue);
                    Handles.Label(
                        action.transform.position + Vector3.up * 0.15f,
                        "Distância mínima do destino local");
                }

                DrawActiveTarget(action);
            }
            finally
            {
                Handles.color = previousColor;
            }
        }

        private static bool TryGetLastKnownAnchor(
            PEGANpcInvestigateLastKnownRickPositionAction action,
            out Vector3 anchor)
        {
            anchor = default;
            if (!Application.isPlaying)
            {
                return false;
            }

            PEGANpcPerceptionSource perception = action.GetComponent<PEGANpcPerceptionSource>();
            PEGANpcApproachKnownRickAction approach = action.GetComponent<PEGANpcApproachKnownRickAction>();
            PEGANpcPerceptionSource.PEGANpcPerceptionGizmoState sample =
                perception != null ? perception.LatestGizmoState : default;
            if (!sample.HasSample || !sample.HasRickTarget || sample.HasVisualContact || approach == null ||
                !approach.TryGetKnownRickPosition(out anchor, out _))
            {
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

        private static void DrawActiveTarget(PEGANpcInvestigateLastKnownRickPositionAction action)
        {
            PEGANpcActionExecutionSnapshot execution = action.CurrentExecution;
            if (execution.Outcome != PEGANpcActionOutcome.Running || !execution.DeclaredEffect.IsTargetResolved)
            {
                return;
            }

            Handles.color = ActiveTargetColor;
            Handles.DrawWireDisc(execution.TargetPosition, Vector3.up, 0.18f);
            Handles.Label(execution.TargetPosition, "Destino local selecionado pelo GOAP");
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

using UnityEditor;
using UnityEngine;

namespace PEGA.NPC.Editor
{
    /// <summary>DevelopmentOnly Scene View visualization of configured and sampled perception.</summary>
    internal static class PEGANpcPerceptionSourceGizmos
    {
        private const float MinimumDirectionMagnitude = 0.0001f;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void Draw(PEGANpcPerceptionSource source, GizmoType gizmoType)
        {
            if (source == null || !source.DrawPerceptionGizmos)
            {
                return;
            }

            bool isSelected = (gizmoType & GizmoType.Selected) != 0;
            if (!isSelected && !source.DrawGizmosWhenUnselected)
            {
                return;
            }

            Color previousColor = Handles.color;
            try
            {
                DrawConfiguredGeometry(source);
                if (Application.isPlaying)
                {
                    DrawLatestObservation(source.LatestGizmoState);
                }
            }
            finally
            {
                Handles.color = previousColor;
            }
        }

        private static void DrawConfiguredGeometry(PEGANpcPerceptionSource source)
        {
            Vector3 origin = source.transform.position + Vector3.up * source.ObserverOriginHeightMeters;
            Vector3 forward = source.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= MinimumDirectionMagnitude * MinimumDirectionMagnitude)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            Handles.color = new Color(0.2f, 0.75f, 1f, 0.9f);
            Handles.DrawWireDisc(origin, Vector3.up, source.PerceivedAreaRadiusMeters);

            float halfAngle = source.VisionFieldOfViewDegrees * 0.5f;
            Vector3 left = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward;
            Vector3 right = Quaternion.AngleAxis(halfAngle, Vector3.up) * forward;
            Handles.color = new Color(1f, 0.8f, 0.2f, 0.95f);
            Handles.DrawLine(origin, origin + left * source.VisionRangeMeters);
            Handles.DrawLine(origin, origin + right * source.VisionRangeMeters);
            Handles.DrawWireArc(
                origin,
                Vector3.up,
                left,
                source.VisionFieldOfViewDegrees,
                source.VisionRangeMeters);
        }

        private static void DrawLatestObservation(PEGANpcPerceptionSource.PEGANpcPerceptionGizmoState state)
        {
            if (!state.HasSample || !state.HasRickTarget)
            {
                return;
            }

            Handles.color = state.IsOccluded
                ? new Color(1f, 0.25f, 0.2f, 1f)
                : state.HasVisualContact
                    ? new Color(0.2f, 1f, 0.35f, 1f)
                    : state.IsAreaPresencePerceived
                        ? new Color(1f, 0.75f, 0.2f, 1f)
                        : new Color(0.8f, 0.8f, 0.8f, 0.9f);

            Handles.DrawLine(state.Origin, state.TargetAimPoint);
            Handles.DrawWireDisc(state.TargetAimPoint, Vector3.up, 0.18f);
            Handles.Label(
                state.TargetAimPoint,
                state.IsOccluded
                    ? "Rick: ocluso"
                    : state.HasVisualContact
                        ? "Rick: contato visual"
                        : state.IsAreaPresencePerceived
                            ? "Rick: presença radial"
                            : "Rick: fora da percepção");
        }
    }
}

using ImmersiveGames.Perception;
using UnityEditor;
using UnityEngine;

namespace PEGA.Diagnostics.Editor
{
    /// <summary>Desenha a geometria espacial exata publicada pelo sistema de Perception.</summary>
    internal static class PegaPerceptionSpatialVolumeGizmos
    {
        internal static void Draw(
            PerceptionSpatialDiscoveryVolume volume,
            Color color,
            string label)
        {
            Color previousGizmosColor = Gizmos.color;
            Color previousHandlesColor = Handles.color;
            Matrix4x4 previousGizmosMatrix = Gizmos.matrix;
            try
            {
                Gizmos.color = color;
                Handles.color = color;
                switch (volume.Shape)
                {
                    case PerceptionSpatialVolumeShape.Sphere:
                        Gizmos.DrawWireSphere(volume.Center, volume.RadiusMeters);
                        break;

                    case PerceptionSpatialVolumeShape.Box:
                        Gizmos.matrix = Matrix4x4.TRS(
                            volume.Center,
                            volume.Rotation,
                            Vector3.one);
                        Gizmos.DrawWireCube(Vector3.zero, volume.BoxSizeMeters);
                        break;

                    case PerceptionSpatialVolumeShape.Capsule:
                        DrawWireCapsule(volume);
                        break;
                }

                if (!string.IsNullOrWhiteSpace(label))
                {
                    Handles.Label(
                        volume.Center + Vector3.up * VolumeLabelOffset(volume),
                        label);
                }
            }
            finally
            {
                Gizmos.color = previousGizmosColor;
                Handles.color = previousHandlesColor;
                Gizmos.matrix = previousGizmosMatrix;
            }
        }

        private static void DrawWireCapsule(PerceptionSpatialDiscoveryVolume volume)
        {
            Vector3 axis = volume.Rotation * Vector3.up;
            float halfSegment = Mathf.Max(
                0f,
                volume.CapsuleHeightMeters * 0.5f - volume.RadiusMeters);
            Vector3 lower = volume.Center - axis * halfSegment;
            Vector3 upper = volume.Center + axis * halfSegment;
            Gizmos.DrawWireSphere(lower, volume.RadiusMeters);
            Gizmos.DrawWireSphere(upper, volume.RadiusMeters);

            Vector3 right = volume.Rotation * Vector3.right * volume.RadiusMeters;
            Vector3 forward = volume.Rotation * Vector3.forward * volume.RadiusMeters;
            Gizmos.DrawLine(lower + right, upper + right);
            Gizmos.DrawLine(lower - right, upper - right);
            Gizmos.DrawLine(lower + forward, upper + forward);
            Gizmos.DrawLine(lower - forward, upper - forward);
        }

        private static float VolumeLabelOffset(PerceptionSpatialDiscoveryVolume volume)
        {
            switch (volume.Shape)
            {
                case PerceptionSpatialVolumeShape.Sphere:
                    return volume.RadiusMeters;

                case PerceptionSpatialVolumeShape.Box:
                    return volume.BoxSizeMeters.y * 0.5f;

                case PerceptionSpatialVolumeShape.Capsule:
                    return volume.CapsuleHeightMeters * 0.5f;

                default:
                    return 0.1f;
            }
        }
    }
}

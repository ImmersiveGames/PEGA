using PEGA.Player;
using UnityEditor;
using UnityEngine;

namespace PEGA.Player.Editor
{
    internal static class PEGAPlayerObjectEntryDiscoveryGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void Draw(PEGAPlayerObjectEntryDiscoverySource source, GizmoType gizmoType)
        {
            if (source == null || !source.DrawDiscoveryGizmos)
            {
                return;
            }

            bool isSelected = (gizmoType & GizmoType.Selected) != 0;
            if (!isSelected && !source.DrawGizmosWhenUnselected)
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Color previousHandlesColor = Handles.color;
            try
            {
                Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.85f);
                Gizmos.DrawWireSphere(source.DiscoveryCenter, source.DiscoveryRadiusMeters);

                if (!Application.isPlaying)
                {
                    return;
                }

                Handles.color = new Color(0.2f, 1f, 0.35f, 1f);
                Handles.Label(
                    source.DiscoveryCenter + Vector3.up * 1.2f,
                    $"Object Entry candidates: {source.Candidates.Count}");

                Gizmos.color = new Color(0.2f, 1f, 0.35f, 1f);
                for (int index = 0; index < source.Candidates.Count; index++)
                {
                    PEGAPlayerObjectEntryCandidate candidate = source.Candidates[index];
                    GameObject occurrence = candidate.Occurrence;
                    if (occurrence == null)
                    {
                        continue;
                    }

                    Gizmos.DrawWireCube(occurrence.transform.position, Vector3.one * 0.3f);
                    Handles.Label(
                        occurrence.transform.position + Vector3.up * 0.5f,
                        $"{candidate.ObjectEntryId.StableText}\n{occurrence.name}");
                }
            }
            finally
            {
                Gizmos.color = previousColor;
                Handles.color = previousHandlesColor;
            }
        }
    }
}

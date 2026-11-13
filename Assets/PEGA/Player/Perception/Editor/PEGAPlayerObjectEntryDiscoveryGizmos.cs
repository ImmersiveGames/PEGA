using ImmersiveGames.Perception;
using PEGA.Diagnostics;
using PEGA.Diagnostics.Editor;
using PEGA.Player.Perception;
using PEGA.Selection.Player;
using UnityEditor;
using UnityEngine;

namespace PEGA.Player.Editor
{
    internal static class PegaPlayerObjectEntryDiscoveryGizmos
    {
        private static readonly Color DiscoveryColor = new Color(0.2f, 0.45f, 1f, 0.9f);
        private static readonly Color CandidateColor = new Color(0.2f, 1f, 0.35f, 1f);
        private static readonly Color SelectionColor = new Color(1f, 0.62f, 0.12f, 0.95f);

        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy)]
        private static void Draw(PegaPlayerObjectEntryDiscoverySource source, GizmoType gizmoType)
        {
            if (source == null)
            {
                return;
            }

            bool isInSelectionHierarchy = (gizmoType & GizmoType.InSelectionHierarchy) != 0;
            if (!isInSelectionHierarchy && !source.DrawGizmosWhenUnselected)
            {
                return;
            }

            PegaPlayerObjectEntrySelectionSource selection =
                source.GetComponent<PegaPlayerObjectEntrySelectionSource>();
            bool showDiscovery = source.DiscoveryVisualizationLevel != PegaDiagnosticVisualizationLevel.Off;
            bool showSelection = selection != null &&
                selection.SelectionVisualizationLevel != PegaDiagnosticVisualizationLevel.Off;
            if (!showDiscovery && !showSelection)
            {
                return;
            }

            Color previousGizmosColor = Gizmos.color;
            Color previousHandlesColor = Handles.color;
            try
            {
                bool discoveryIsDetailed = source.DiscoveryVisualizationLevel ==
                    PegaDiagnosticVisualizationLevel.Detailed;
                bool selectionIsDetailed = selection != null &&
                    selection.SelectionVisualizationLevel == PegaDiagnosticVisualizationLevel.Detailed;
                bool hasDiscoveryVolume = false;
                string discoveryDiagnostic = string.Empty;
                PerceptionSpatialDiscoveryVolume discoveryVolume = default;
                if (showDiscovery)
                {
                    hasDiscoveryVolume = source.TryGetDiscoveryVolume(
                        out discoveryVolume,
                        out discoveryDiagnostic);
                    DrawDiscovery(source, discoveryVolume, hasDiscoveryVolume, discoveryDiagnostic, discoveryIsDetailed);
                }

                PegaPlayerObjectEntryCandidate? currentSelection =
                    Application.isPlaying && selection != null
                        ? selection.CurrentSelection
                        : null;
                if (showDiscovery && Application.isPlaying)
                {
                    DrawDiscoveredCandidates(source, discoveryIsDetailed);
                }

                if (showSelection)
                {
                    DrawSelection(source, selection, currentSelection, selectionIsDetailed);
                }
            }
            finally
            {
                Gizmos.color = previousGizmosColor;
                Handles.color = previousHandlesColor;
            }
        }

        private static void DrawDiscovery(
            PegaPlayerObjectEntryDiscoverySource source,
            PerceptionSpatialDiscoveryVolume volume,
            bool hasVolume,
            string diagnostic,
            bool isDetailed)
        {
            Vector3 labelOrigin = hasVolume ? volume.Center : source.DiscoveryCenter;
            if (hasVolume)
            {
                PegaPerceptionSpatialVolumeGizmos.Draw(
                    volume,
                    DiscoveryColor,
                    isDetailed
                        ? FormatDiscoveryVolume(volume)
                        : FormatDiscoveryVolumeSummary(volume));
            }
            else
            {
                Handles.color = DiscoveryColor;
                string label = isDetailed && !string.IsNullOrWhiteSpace(diagnostic)
                    ? $"Descoberta indisponível: {diagnostic}"
                    : "Descoberta indisponível";
                Handles.Label(labelOrigin + Vector3.up * 1.2f, label);
            }

            if (!Application.isPlaying)
            {
                return;
            }

            Handles.color = CandidateColor;
            string summary = isDetailed
                ? $"Descobertos: {source.Candidates.Count} ({source.AcquisitionStatus})"
                : $"Descobertos: {source.Candidates.Count}";
            if (isDetailed && !string.IsNullOrWhiteSpace(source.Diagnostic))
            {
                summary += $"\n{source.Diagnostic}";
            }

            Handles.Label(labelOrigin + Vector3.up * 1.5f, summary);
        }

        private static void DrawDiscoveredCandidates(
            PegaPlayerObjectEntryDiscoverySource source,
            bool isDetailed)
        {
            for (int index = 0; index < source.Candidates.Count; index++)
            {
                PegaPlayerObjectEntryCandidate candidate = source.Candidates[index];
                GameObject occurrence = candidate.Occurrence;
                if (occurrence == null)
                {
                    continue;
                }

                Gizmos.color = CandidateColor;
                Gizmos.DrawWireCube(occurrence.transform.position, Vector3.one * 0.3f);
                if (!isDetailed)
                {
                    continue;
                }

                Handles.color = CandidateColor;
                Handles.Label(
                    occurrence.transform.position + Vector3.up * 0.5f,
                    $"DISCOVERED\n{candidate.ObjectEntryId.StableText}\n{occurrence.name}");
            }
        }

        private static void DrawSelection(
            PegaPlayerObjectEntryDiscoverySource source,
            PegaPlayerObjectEntrySelectionSource selection,
            PegaPlayerObjectEntryCandidate? currentSelection,
            bool isDetailed)
        {
            if (!selection.TryGetPhysicalSelectionFrame(
                    out Vector3 selectionCenter,
                    out Vector3 visibilityOrigin))
            {
                Handles.color = SelectionColor;
                Handles.Label(
                    source.DiscoveryCenter + Vector3.up * 2f,
                    "Seleção: corpo físico indisponível");
                return;
            }

            if (selection.SelectionRangeMeters > 0f)
            {
                Gizmos.color = SelectionColor;
                Gizmos.DrawWireSphere(selectionCenter, selection.SelectionRangeMeters);
                DrawPlayerSelectionVolume(selection, selectionCenter);
            }

            Gizmos.color = SelectionColor;
            Gizmos.DrawWireSphere(visibilityOrigin, 0.08f);

            if (!currentSelection.HasValue || currentSelection.Value.Occurrence == null)
            {
                Handles.color = SelectionColor;
                string noSelectionLabel = isDetailed
                    ? $"Seleção: nenhuma\nAlcance: {selection.SelectionRangeMeters:0.##} m / volume: ±{selection.SelectionHorizontalHalfAngleDegrees:0.#}° × ±{selection.SelectionVerticalHalfAngleDegrees:0.#}°"
                    : $"Seleção: {selection.SelectionRangeMeters:0.##} m · foco: —";
                Handles.Label(selectionCenter + Vector3.up * 2f, noSelectionLabel);
                return;
            }

            PegaPlayerObjectEntryCandidate selected = currentSelection.Value;
            GameObject occurrence = selected.Occurrence;
            Gizmos.color = SelectionColor;
            Gizmos.DrawWireCube(occurrence.transform.position, Vector3.one * 0.42f);
            Handles.color = SelectionColor;
            string selectedLabel = isDetailed
                ? $"SELECIONADO\n{selected.ObjectEntryId.StableText}\n{occurrence.name}\nAlcance: {selection.SelectionRangeMeters:0.##} m / volume: ±{selection.SelectionHorizontalHalfAngleDegrees:0.#}° × ±{selection.SelectionVerticalHalfAngleDegrees:0.#}° (+{selection.StickyVolumeMarginDegrees:0.#}° para manter)"
                : $"Foco: {occurrence.name}";
            Handles.Label(occurrence.transform.position + Vector3.up * 0.65f, selectedLabel);
        }

        private static void DrawPlayerSelectionVolume(
            PegaPlayerObjectEntrySelectionSource selection,
            Vector3 center)
        {
            float horizontalAngle = selection.SelectionHorizontalHalfAngleDegrees;
            float verticalAngle = selection.SelectionVerticalHalfAngleDegrees;
            float range = selection.SelectionRangeMeters;
            Vector3 forward = selection.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0f)
            {
                return;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Gizmos.color = SelectionColor;
            DrawAngularVolume(center, forward, right, range, horizontalAngle, verticalAngle);

            if (selection.StickyVolumeMarginDegrees > 0f)
            {
                Gizmos.color = new Color(SelectionColor.r, SelectionColor.g, SelectionColor.b, 0.35f);
                DrawAngularVolume(
                    center,
                    forward,
                    right,
                    range,
                    horizontalAngle + selection.StickyVolumeMarginDegrees,
                    verticalAngle + selection.StickyVolumeMarginDegrees);
            }
        }

        private static void DrawAngularVolume(
            Vector3 center,
            Vector3 forward,
            Vector3 right,
            float range,
            float horizontalAngle,
            float verticalAngle)
        {
            for (int horizontalSign = -1; horizontalSign <= 1; horizontalSign += 2)
            {
                float azimuth = horizontalAngle * horizontalSign;
                DrawVolumeRay(center, forward, right, range, azimuth, 0f);
                DrawVolumeArc(center, forward, right, range, azimuth, -verticalAngle, verticalAngle, vertical: true);
            }

            for (int verticalSign = -1; verticalSign <= 1; verticalSign += 2)
            {
                float elevation = verticalAngle * verticalSign;
                DrawVolumeRay(center, forward, right, range, 0f, elevation);
                DrawVolumeArc(center, forward, right, range, elevation, -horizontalAngle, horizontalAngle, vertical: false);
            }

            Gizmos.DrawLine(center, center + forward * range);
        }

        private static void DrawVolumeRay(
            Vector3 center,
            Vector3 forward,
            Vector3 right,
            float range,
            float azimuthDegrees,
            float elevationDegrees)
        {
            Gizmos.DrawLine(
                center,
                center + GetVolumeDirection(forward, right, azimuthDegrees, elevationDegrees) * range);
        }

        private static void DrawVolumeArc(
            Vector3 center,
            Vector3 forward,
            Vector3 right,
            float range,
            float fixedAngleDegrees,
            float startAngleDegrees,
            float endAngleDegrees,
            bool vertical)
        {
            const int segmentCount = 16;
            Vector3 previous = default;
            for (int segment = 0; segment <= segmentCount; segment++)
            {
                float interpolation = segment / (float)segmentCount;
                float changingAngle = Mathf.Lerp(startAngleDegrees, endAngleDegrees, interpolation);
                Vector3 direction = vertical
                    ? GetVolumeDirection(forward, right, fixedAngleDegrees, changingAngle)
                    : GetVolumeDirection(forward, right, changingAngle, fixedAngleDegrees);
                Vector3 point = center + direction * range;
                if (segment > 0)
                {
                    Gizmos.DrawLine(previous, point);
                }

                previous = point;
            }
        }

        private static Vector3 GetVolumeDirection(
            Vector3 forward,
            Vector3 right,
            float azimuthDegrees,
            float elevationDegrees)
        {
            float azimuth = azimuthDegrees * Mathf.Deg2Rad;
            float elevation = elevationDegrees * Mathf.Deg2Rad;
            Vector3 horizontal = forward * Mathf.Cos(azimuth) + right * Mathf.Sin(azimuth);
            return horizontal * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation);
        }

        private static string FormatDiscoveryVolume(PerceptionSpatialDiscoveryVolume volume)
        {
            switch (volume.Shape)
            {
                case PerceptionSpatialVolumeShape.Sphere:
                    return $"Descoberta: esfera · r {volume.RadiusMeters:0.##} m";

                case PerceptionSpatialVolumeShape.Box:
                    return $"Descoberta: caixa · {volume.BoxSizeMeters.x:0.##} × {volume.BoxSizeMeters.y:0.##} × {volume.BoxSizeMeters.z:0.##} m";

                case PerceptionSpatialVolumeShape.Capsule:
                    return $"Descoberta: cápsula · r {volume.RadiusMeters:0.##} m / h {volume.CapsuleHeightMeters:0.##} m";

                default:
                    return "Descoberta Object Entry";
            }
        }

        private static string FormatDiscoveryVolumeSummary(PerceptionSpatialDiscoveryVolume volume)
        {
            switch (volume.Shape)
            {
                case PerceptionSpatialVolumeShape.Sphere:
                    return $"Descoberta · r {volume.RadiusMeters:0.#} m";

                case PerceptionSpatialVolumeShape.Box:
                    return $"Descoberta · {volume.BoxSizeMeters.x:0.#}×{volume.BoxSizeMeters.y:0.#}×{volume.BoxSizeMeters.z:0.#} m";

                case PerceptionSpatialVolumeShape.Capsule:
                    return $"Descoberta · r {volume.RadiusMeters:0.#} m / h {volume.CapsuleHeightMeters:0.#} m";

                default:
                    return "Descoberta Object Entry";
            }
        }
    }
}

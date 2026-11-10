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
            Vector3 selectionCenter = source.DiscoveryCenter;
            if (selection.SelectionRangeMeters > 0f)
            {
                Gizmos.color = SelectionColor;
                Gizmos.DrawWireSphere(selectionCenter, selection.SelectionRangeMeters);
            }

            if (!currentSelection.HasValue || currentSelection.Value.Occurrence == null)
            {
                Handles.color = SelectionColor;
                string noSelectionLabel = isDetailed
                    ? $"Seleção: nenhuma\nAlcance: {selection.SelectionRangeMeters:0.##} m / mira: {selection.AimAssistViewportRadius:0.##} viewport"
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
                ? $"SELECIONADO\n{selected.ObjectEntryId.StableText}\n{occurrence.name}\nAlcance: {selection.SelectionRangeMeters:0.##} m / mira: {selection.AimAssistViewportRadius:0.##} viewport"
                : $"Foco: {occurrence.name}";
            Handles.Label(occurrence.transform.position + Vector3.up * 0.65f, selectedLabel);
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

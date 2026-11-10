using System.Collections.Generic;
using ImmersiveGames.Perception;
using PEGA.Diagnostics;
using PEGA.Diagnostics.Editor;
using PEGA.NPC.Perception;
using UnityEditor;
using UnityEngine;

namespace PEGA.NPC.Editor
{
    /// <summary>Visualização DevelopmentOnly das geometrias e do último lote sensorial do NPC.</summary>
    internal static class PegaNpcPerceptionSourceGizmos
    {
        private const float MinimumDirectionMagnitude = 0.0001f;
        private const float CandidateMarkerSizeMeters = 0.24f;

        private static readonly Color AcquisitionColor = new Color(0.2f, 0.75f, 1f, 0.85f);
        private static readonly Color RadialColor = new Color(0.25f, 0.8f, 1f, 0.95f);
        private static readonly Color VisualColor = new Color(1f, 0.75f, 0.18f, 0.95f);
        private static readonly Color DetectedColor = new Color(0.2f, 1f, 0.35f, 1f);
        private static readonly Color OccludedColor = new Color(1f, 0.25f, 0.2f, 1f);
        private static readonly Color NegativeColor = new Color(0.95f, 0.55f, 0.15f, 1f);
        private static readonly Color UnknownColor = new Color(0.8f, 0.8f, 0.8f, 0.95f);

        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy)]
        private static void Draw(PegaNpcPerceptionSource source, GizmoType gizmoType)
        {
            if (source == null ||
                source.PerceptionVisualizationLevel == PegaDiagnosticVisualizationLevel.Off)
            {
                return;
            }

            bool isInSelectionHierarchy = (gizmoType & GizmoType.InSelectionHierarchy) != 0;
            if (!isInSelectionHierarchy && !source.DrawGizmosWhenUnselected)
            {
                return;
            }

            if (Application.isPlaying && !source.isActiveAndEnabled)
            {
                return;
            }

            Color previousHandlesColor = Handles.color;
            Color previousGizmosColor = Gizmos.color;
            Matrix4x4 previousGizmosMatrix = Gizmos.matrix;
            try
            {
                bool isDetailed = source.PerceptionVisualizationLevel ==
                    PegaDiagnosticVisualizationLevel.Detailed;
                DrawConfiguredEvaluationGeometry(source, isDetailed);
                if (Application.isPlaying)
                {
                    if (isDetailed)
                    {
                        DrawLatestSample(source, source.LatestGizmoState);
                    }
                    else
                    {
                        DrawLatestSampleSummary(source, source.LatestGizmoState);
                    }
                }
                else if (isDetailed)
                {
                    DrawConfiguredAcquisitionVolumes(source);
                }
            }
            finally
            {
                Handles.color = previousHandlesColor;
                Gizmos.color = previousGizmosColor;
                Gizmos.matrix = previousGizmosMatrix;
            }
        }

        private static void DrawConfiguredEvaluationGeometry(
            PegaNpcPerceptionSource source,
            bool isDetailed)
        {
            Vector3 radialOrigin = source.PerceivedAreaOrigin;
            Vector3 visionOrigin = source.VisionOrigin;
            if (source.HasPerceivedAreaSensor)
            {
                Handles.color = RadialColor;
                Handles.DrawWireDisc(radialOrigin, Vector3.up, source.PerceivedAreaRadiusMeters);
                Handles.Label(
                    radialOrigin + Vector3.up * 0.1f,
                    isDetailed
                        ? $"Avaliação radial ({source.PerceivedAreaRadiusMeters:0.##} m)"
                        : $"Radial {source.PerceivedAreaRadiusMeters:0.##} m");
            }

            if (!source.HasVisionSensor)
            {
                return;
            }

            Vector3 forward = source.VisionForward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= MinimumDirectionMagnitude * MinimumDirectionMagnitude)
            {
                Handles.color = UnknownColor;
                Handles.Label(
                    visionOrigin,
                    isDetailed ? "Campo visual indisponível: sem direção horizontal" : "Visão indisponível");
                return;
            }
            forward.Normalize();

            float halfAngle = source.VisionFieldOfViewDegrees * 0.5f;
            Vector3 left = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward;
            Vector3 right = Quaternion.AngleAxis(halfAngle, Vector3.up) * forward;
            Handles.color = VisualColor;
            Handles.DrawLine(visionOrigin, visionOrigin + left * source.VisionRangeMeters);
            Handles.DrawLine(visionOrigin, visionOrigin + right * source.VisionRangeMeters);
            Handles.DrawWireArc(
                visionOrigin,
                Vector3.up,
                left,
                source.VisionFieldOfViewDegrees,
                source.VisionRangeMeters);
            Handles.Label(
                visionOrigin + forward * (source.VisionRangeMeters * 0.65f),
                isDetailed
                    ? $"Campo visual ({source.VisionRangeMeters:0.##} m / {source.VisionFieldOfViewDegrees:0.#}°)"
                    : $"Visão {source.VisionRangeMeters:0.##} m / {source.VisionFieldOfViewDegrees:0.#}°");
        }

        private static void DrawConfiguredAcquisitionVolumes(PegaNpcPerceptionSource source)
        {
            bool hasRadialVolume = source.TryGetPerceivedAreaDiscoveryVolume(out PerceptionSpatialDiscoveryVolume radialVolume);
            bool hasVisualVolume = source.TryGetVisionDiscoveryVolume(out PerceptionSpatialDiscoveryVolume visualVolume);
            if (hasRadialVolume && hasVisualVolume && HasSameGeometry(radialVolume, visualVolume))
            {
                DrawVolume(
                    radialVolume,
                    AcquisitionColor,
                    "Aquisição física configurada (compartilhada: radial + visual)");
                return;
            }

            if (hasRadialVolume)
            {
                DrawVolume(radialVolume, RadialColor, "Aquisição física configurada (radial)");
            }

            if (hasVisualVolume)
            {
                DrawVolume(visualVolume, VisualColor, "Aquisição física configurada (visual)");
            }
        }

        private static void DrawLatestSample(
            PegaNpcPerceptionSource source,
            PegaNpcPerceptionSource.PegaNpcPerceptionGizmoState state)
        {
            if (!state.HasSample)
            {
                Handles.color = UnknownColor;
                Handles.Label(source.transform.position, "Perception: aguardando lote de amostragem");
                return;
            }

            DrawAcquisitionVolumes(state);
            DrawSampleStatus(state);
            var labelAssigned = new bool[state.Candidates.Count];
            for (int index = 0; index < state.Candidates.Count; index++)
            {
                DrawCandidate(state.Candidates[index]);
            }

            for (int index = 0; index < state.Candidates.Count; index++)
            {
                PegaNpcPerceptionSource.PegaNpcPerceptionCandidateGizmoState candidate = state.Candidates[index];
                if (!candidate.HasPosition || labelAssigned[index])
                {
                    continue;
                }

                List<int> group = ClaimCoincidentCandidateGroup(state.Candidates, index, labelAssigned);
                Handles.color = UnknownColor;
                Handles.Label(
                    candidate.Position + Vector3.up * (CandidateMarkerSizeMeters * 0.75f),
                    BuildCoincidentCandidateLabel(state.Candidates, group));
            }
        }

        private static void DrawLatestSampleSummary(
            PegaNpcPerceptionSource source,
            PegaNpcPerceptionSource.PegaNpcPerceptionGizmoState state)
        {
            if (!state.HasSample)
            {
                Handles.color = UnknownColor;
                Handles.Label(source.transform.position, "Percepção: aguardando");
                return;
            }

            DrawSampleStatusSummary(state);
            DrawCandidateSummary(state.Candidates);
        }

        private static void DrawSampleStatusSummary(
            PegaNpcPerceptionSource.PegaNpcPerceptionGizmoState state)
        {
            string radial = state.RadialSensorSampled
                ? state.IsAreaPresencePerceived ? "R+" : "R−"
                : "R·";
            string visual = state.VisualSensorSampled
                ? state.HasVisualContact ? "V+" : "V−"
                : "V·";
            Handles.color = UnknownColor;
            Handles.Label(
                state.Origin + Vector3.up * 0.24f,
                $"{radial} · {visual} · Aq {FormatAcquisitionStatusShort(state.AcquisitionStatus)} · C {state.Candidates.Count}");
        }

        private static void DrawCandidateSummary(
            System.Collections.Generic.IReadOnlyList<PegaNpcPerceptionSource.PegaNpcPerceptionCandidateGizmoState> candidates)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                PegaNpcPerceptionSource.PegaNpcPerceptionCandidateGizmoState candidate = candidates[index];
                DrawCandidateMarker(candidate);
            }
        }

        private static string FormatAcquisitionStatusShort(PerceptionAcquisitionStatus status)
        {
            switch (status)
            {
                case PerceptionAcquisitionStatus.Complete: return "ok";
                case PerceptionAcquisitionStatus.Partial: return "parcial";
                case PerceptionAcquisitionStatus.Unavailable: return "indisp.";
                default: return "?";
            }
        }

        private static List<int> ClaimCoincidentCandidateGroup(
            System.Collections.Generic.IReadOnlyList<PegaNpcPerceptionSource.PegaNpcPerceptionCandidateGizmoState> candidates,
            int currentIndex,
            bool[] labelAssigned)
        {
            var group = new List<int> { currentIndex };
            labelAssigned[currentIndex] = true;
            const float coincidentPositionToleranceMeters = 0.02f;
            float toleranceSquared = coincidentPositionToleranceMeters * coincidentPositionToleranceMeters;
            for (int groupIndex = 0; groupIndex < group.Count; groupIndex++)
            {
                Vector3 groupPosition = candidates[group[groupIndex]].Position;
                for (int index = 0; index < candidates.Count; index++)
                {
                    if (labelAssigned[index] || !candidates[index].HasPosition ||
                        (candidates[index].Position - groupPosition).sqrMagnitude > toleranceSquared)
                    {
                        continue;
                    }

                    labelAssigned[index] = true;
                    group.Add(index);
                }
            }

            return group;
        }

        private static string BuildCoincidentCandidateLabel(
            System.Collections.Generic.IReadOnlyList<PegaNpcPerceptionSource.PegaNpcPerceptionCandidateGizmoState> candidates,
            System.Collections.Generic.IReadOnlyList<int> group)
        {
            var labels = new System.Collections.Generic.List<string>();
            for (int index = 0; index < group.Count; index++)
            {
                labels.Add(FormatCandidateLabel(candidates[group[index]]));
            }

            return string.Join("\n\n", labels);
        }

        private static void DrawAcquisitionVolumes(PegaNpcPerceptionSource.PegaNpcPerceptionGizmoState state)
        {
            for (int index = 0; index < state.AcquisitionVolumes.Count; index++)
            {
                PegaNpcPerceptionSource.PegaNpcPerceptionAcquisitionGizmoState volume =
                    state.AcquisitionVolumes[index];
                Color color = volume.Status == PerceptionAcquisitionStatus.Complete
                    ? AcquisitionColor
                    : volume.Status == PerceptionAcquisitionStatus.Partial
                        ? NegativeColor
                        : OccludedColor;
                DrawVolume(
                    volume.Volume,
                    color,
                    $"Aquisição física ({volume.SensorLabels}; {FormatAcquisitionStatus(volume.Status)})");
            }

            if (state.AcquisitionVolumes.Count == 0)
            {
                Handles.color = state.AcquisitionStatus == PerceptionAcquisitionStatus.Complete
                    ? AcquisitionColor
                    : state.AcquisitionStatus == PerceptionAcquisitionStatus.Partial
                        ? NegativeColor
                        : OccludedColor;
                string diagnostic = string.IsNullOrWhiteSpace(state.AcquisitionDiagnostic)
                    ? string.Empty
                    : $" — {state.AcquisitionDiagnostic}";
                Handles.Label(
                    state.Origin,
                    $"Aquisição de candidatos: {FormatAcquisitionStatus(state.AcquisitionStatus)}{diagnostic}");
            }
        }

        private static void DrawSampleStatus(PegaNpcPerceptionSource.PegaNpcPerceptionGizmoState state)
        {
            Handles.color = UnknownColor;
            string radial = state.RadialSensorSampled ? "amostrado" : "não amostrado";
            string visual = state.VisualSensorSampled ? "amostrado" : "não amostrado";
            string acquisition = FormatAcquisitionStatus(state.AcquisitionStatus);
            string diagnostic = string.IsNullOrWhiteSpace(state.AcquisitionDiagnostic)
                ? string.Empty
                : $" · {state.AcquisitionDiagnostic}";
            Handles.Label(
                state.Origin + Vector3.up * 0.24f,
                $"Sensores: radial {radial} · visual {visual} · aquisição {acquisition} · candidatos {state.Candidates.Count}{diagnostic}");
        }

        private static void DrawCandidate(
            PegaNpcPerceptionSource.PegaNpcPerceptionCandidateGizmoState candidate)
        {
            if (!candidate.HasPosition)
            {
                return;
            }

            Vector3 position = candidate.Position;
            bool radialDetected = candidate.Radial.HasObservation && candidate.Radial.Observation.IsDetected;
            bool visualDetected = candidate.Visual.HasObservation && candidate.Visual.Observation.IsDetected;
            bool visualOccluded = candidate.Visual.HasObservation && candidate.Visual.Observation.IsOccluded;
            Color markerColor = GetCandidateMarkerColor(
                radialDetected,
                visualDetected,
                visualOccluded);

            if (candidate.Radial.HasObservation)
            {
                Handles.color = candidate.Radial.Observation.IsOccluded
                    ? OccludedColor
                    : candidate.Radial.Observation.IsDetected
                        ? RadialColor
                        : NegativeColor;
                Handles.DrawLine(candidate.Radial.Observation.ObserverOrigin, position);
            }

            if (candidate.Visual.HasObservation)
            {
                Handles.color = candidate.Visual.Observation.IsOccluded
                    ? OccludedColor
                    : candidate.Visual.Observation.IsDetected
                        ? DetectedColor
                        : NegativeColor;
                Handles.DrawLine(candidate.Visual.Observation.ObserverOrigin, position);
            }

            bool isPhysical = (candidate.Origins & PerceptionCandidateOrigin.Physical) != 0;
            bool isExternal = (candidate.Origins & PerceptionCandidateOrigin.External) != 0;
            Handles.color = markerColor;
            if (isPhysical)
            {
                Handles.DrawWireCube(position, Vector3.one * CandidateMarkerSizeMeters);
            }
            if (isExternal)
            {
                Handles.DrawWireDisc(position, Vector3.up, CandidateMarkerSizeMeters * 0.7f);
            }

        }

        private static void DrawCandidateMarker(
            PegaNpcPerceptionSource.PegaNpcPerceptionCandidateGizmoState candidate)
        {
            if (!candidate.HasPosition)
            {
                return;
            }

            bool radialDetected = candidate.Radial.HasObservation && candidate.Radial.Observation.IsDetected;
            bool visualDetected = candidate.Visual.HasObservation && candidate.Visual.Observation.IsDetected;
            bool visualOccluded = candidate.Visual.HasObservation && candidate.Visual.Observation.IsOccluded;
            Color color = GetCandidateMarkerColor(radialDetected, visualDetected, visualOccluded);
            bool isPhysical = (candidate.Origins & PerceptionCandidateOrigin.Physical) != 0;
            bool isExternal = (candidate.Origins & PerceptionCandidateOrigin.External) != 0;
            Handles.color = color;
            if (isPhysical)
            {
                Handles.DrawWireCube(
                    candidate.Position,
                    Vector3.one * CandidateMarkerSizeMeters);
            }

            if (isExternal)
            {
                Handles.DrawWireDisc(
                    candidate.Position,
                    Vector3.up,
                    CandidateMarkerSizeMeters * 0.7f);
            }
        }

        private static Color GetCandidateMarkerColor(
            bool radialDetected,
            bool visualDetected,
            bool visualOccluded) => visualOccluded
                ? OccludedColor
                : visualDetected
                    ? DetectedColor
                    : radialDetected
                        ? RadialColor
                        : UnknownColor;

        private static string FormatCandidateLabel(
            PegaNpcPerceptionSource.PegaNpcPerceptionCandidateGizmoState candidate)
        {
            bool isPhysical = (candidate.Origins & PerceptionCandidateOrigin.Physical) != 0;
            bool isExternal = (candidate.Origins & PerceptionCandidateOrigin.External) != 0;
            string origin = isPhysical && isExternal
                ? "físico + externo"
                : isPhysical
                    ? "físico"
                    : "externo";
            string identity = candidate.IdentityRecognized
                ? $"Identidade reconhecida: {candidate.IdentitySummary}"
                : "Identidade: anônima";
            if (!candidate.IdentityRecognized && !string.IsNullOrWhiteSpace(candidate.IdentityDiagnostic))
            {
                identity += $" ({candidate.IdentityDiagnostic})";
            }

            string label =
                $"Candidato {candidate.CandidateIndex} ({origin})\n" +
                $"Radial: {FormatSensorResult(candidate.Radial, true)}\n" +
                $"Visual: {FormatSensorResult(candidate.Visual, false)}\n" +
                identity;
            return label;
        }

        private static string FormatSensorResult(
            PegaNpcPerceptionSource.PegaNpcPerceptionSensorGizmoResult result,
            bool isRadial)
        {
            if (!result.WasSampled)
            {
                return "não amostrado";
            }

            if (!result.HasResult)
            {
                return "sem resultado";
            }

            if (!result.HasOutcome)
            {
                return string.IsNullOrWhiteSpace(result.Diagnostic)
                    ? "indisponível"
                    : $"indisponível: {result.Diagnostic}";
            }

            if (result.Status == PerceptionObservationStatus.Insufficient)
            {
                return "evidência insuficiente";
            }
            if (result.Status == PerceptionObservationStatus.Unsupported)
            {
                return "não suportado";
            }

            if (!result.HasObservation)
            {
                return result.Status == PerceptionObservationStatus.Positive
                    ? "positivo (sem evidência geométrica)"
                    : result.Status == PerceptionObservationStatus.Negative
                        ? "negativo (sem evidência geométrica)"
                        : "sem observação";
            }

            if (result.Observation.IsOccluded)
            {
                return "ocluído";
            }
            if (!result.Observation.IsWithinSensorBounds)
            {
                return isRadial ? "fora do raio radial" : "fora do cone/alcance";
            }

            return result.Status == PerceptionObservationStatus.Positive
                ? isRadial ? "presença detectada" : "visível"
                : "negativo";
        }

        private static void DrawVolume(
            PerceptionSpatialDiscoveryVolume volume,
            Color color,
            string label)
        {
            PegaPerceptionSpatialVolumeGizmos.Draw(volume, color, label);
        }

        private static bool HasSameGeometry(
            PerceptionSpatialDiscoveryVolume left,
            PerceptionSpatialDiscoveryVolume right)
        {
            if (left.Shape != right.Shape || !left.Center.Equals(right.Center))
            {
                return false;
            }

            switch (left.Shape)
            {
                case PerceptionSpatialVolumeShape.Sphere:
                    return left.RadiusMeters.Equals(right.RadiusMeters);
                case PerceptionSpatialVolumeShape.Box:
                    return left.Rotation.Equals(right.Rotation) &&
                           left.BoxSizeMeters.Equals(right.BoxSizeMeters);
                case PerceptionSpatialVolumeShape.Capsule:
                    return left.Rotation.Equals(right.Rotation) &&
                           left.RadiusMeters.Equals(right.RadiusMeters) &&
                           left.CapsuleHeightMeters.Equals(right.CapsuleHeightMeters);
                default:
                    return false;
            }
        }

        private static string FormatAcquisitionStatus(PerceptionAcquisitionStatus status)
        {
            switch (status)
            {
                case PerceptionAcquisitionStatus.Complete: return "completa";
                case PerceptionAcquisitionStatus.Partial: return "parcial";
                case PerceptionAcquisitionStatus.Unavailable: return "indisponível";
                default: return "não especificada";
            }
        }
    }
}

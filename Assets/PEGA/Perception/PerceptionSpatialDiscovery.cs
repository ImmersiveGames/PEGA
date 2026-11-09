using System;
using UnityEngine;

namespace ImmersiveGames.Perception
{
    public enum PerceptionSpatialVolumeShape
    {
        Sphere = 0,
        Box = 1,
        Capsule = 2
    }

    /// <summary>Configuração opcional de aquisição física armazenada junto ao asset do sensor.</summary>
    [Serializable]
    public sealed class PerceptionSpatialDiscoverySettings
    {
        [SerializeField]
        [Tooltip("Habilita este sensor como contribuidor opcional da aquisição espacial compartilhada.")]
        private bool enabled;

        [SerializeField]
        [Tooltip("Volume 3D usado para descobrir colliders candidatos.")]
        private PerceptionSpatialVolumeShape volumeShape;

        [SerializeField]
        [Tooltip("Deslocamento local do centro do volume em metros.")]
        private Vector3 centerOffsetMeters;

        [SerializeField]
        [Tooltip("Rotação local do volume em graus; usada por Box e Capsule.")]
        private Vector3 localRotationEulerDegrees;

        [SerializeField, Min(0.01f)]
        [Tooltip("Raio em metros; usado por Sphere e Capsule.")]
        private float radiusMeters = 1f;

        [SerializeField]
        [Tooltip("Dimensões X, Y e Z da Box em metros.")]
        private Vector3 boxSizeMeters = Vector3.one;

        [SerializeField, Min(0.01f)]
        [Tooltip("Altura total da Capsule em metros; deve ser ao menos o dobro do raio.")]
        private float capsuleHeightMeters = 2f;

        [SerializeField]
        [Tooltip("Layers incluídas na aquisição de candidatos. Independente das layers de oclusão.")]
        private LayerMask candidateLayers;

        [SerializeField]
        [Tooltip("Define se colliders Trigger podem ser candidatos. UseGlobal respeita Physics.queriesHitTriggers.")]
        private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal;

        public bool Enabled => enabled;

        internal bool TryCreateVolume(
            Transform observer,
            out PerceptionSpatialDiscoveryVolume volume,
            out string diagnostic)
        {
            Vector3 centerOrigin = observer != null ? observer.position : default;
            return TryCreateVolume(observer, centerOrigin, out volume, out diagnostic);
        }

        internal bool TryCreateVolume(
            Transform observer,
            Vector3 centerOrigin,
            out PerceptionSpatialDiscoveryVolume volume,
            out string diagnostic)
        {
            volume = default;
            if (!enabled)
            {
                diagnostic = "Spatial discovery is not enabled for this sensor.";
                return false;
            }

            if (observer == null)
            {
                diagnostic = "Spatial discovery requires an observer Transform.";
                return false;
            }

            if (candidateLayers.value == 0)
            {
                diagnostic = "At least one candidate layer must be configured for spatial discovery.";
                return false;
            }

            if (!Enum.IsDefined(typeof(PerceptionSpatialVolumeShape), volumeShape))
            {
                diagnostic = "Spatial discovery volume shape is invalid.";
                return false;
            }

            if (!Enum.IsDefined(typeof(QueryTriggerInteraction), triggerInteraction))
            {
                diagnostic = "Spatial discovery trigger policy is invalid.";
                return false;
            }

            if (!IsFinite(centerOffsetMeters))
            {
                diagnostic = "Spatial discovery center offset must be finite.";
                return false;
            }

            if (!IsFinite(centerOrigin))
            {
                diagnostic = "Spatial discovery center origin must be finite.";
                return false;
            }

            Vector3 center = centerOrigin + observer.rotation * centerOffsetMeters;
            if (!IsFinite(center))
            {
                diagnostic = "Spatial discovery center must be finite.";
                return false;
            }

            Quaternion rotation = Quaternion.identity;
            if (volumeShape != PerceptionSpatialVolumeShape.Sphere)
            {
                if (!IsFinite(localRotationEulerDegrees))
                {
                    diagnostic = "Spatial discovery rotation must be finite.";
                    return false;
                }

                rotation = observer.rotation * Quaternion.Euler(localRotationEulerDegrees);
            }

            switch (volumeShape)
            {
                case PerceptionSpatialVolumeShape.Sphere:
                    if (!IsPositiveFinite(radiusMeters))
                    {
                        diagnostic = "Spatial discovery sphere radius must be finite and positive.";
                        return false;
                    }
                    break;

                case PerceptionSpatialVolumeShape.Box:
                    if (!IsPositiveFinite(boxSizeMeters))
                    {
                        diagnostic = "Spatial discovery box dimensions must be finite and positive.";
                        return false;
                    }
                    break;

                case PerceptionSpatialVolumeShape.Capsule:
                    if (!IsPositiveFinite(radiusMeters) ||
                        !IsPositiveFinite(capsuleHeightMeters) ||
                        capsuleHeightMeters < radiusMeters * 2f)
                    {
                        diagnostic = "Spatial discovery capsule height must be finite and at least twice its positive radius.";
                        return false;
                    }
                    break;
            }

            volume = new PerceptionSpatialDiscoveryVolume(
                volumeShape,
                center,
                rotation,
                boxSizeMeters,
                radiusMeters,
                capsuleHeightMeters,
                candidateLayers.value,
                triggerInteraction);
            diagnostic = string.Empty;
            return true;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsPositiveFinite(Vector3 value) =>
            IsPositiveFinite(value.x) && IsPositiveFinite(value.y) && IsPositiveFinite(value.z);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Geometria mundial efetivamente usada por uma aquisição espacial.</summary>
    public readonly struct PerceptionSpatialDiscoveryVolume
    {
        internal PerceptionSpatialDiscoveryVolume(
            PerceptionSpatialVolumeShape shape,
            Vector3 center,
            Quaternion rotation,
            Vector3 boxSizeMeters,
            float radiusMeters,
            float capsuleHeightMeters,
            int candidateLayerMask,
            QueryTriggerInteraction triggerInteraction)
        {
            Shape = shape;
            Center = center;
            Rotation = rotation;
            BoxSizeMeters = boxSizeMeters;
            RadiusMeters = radiusMeters;
            CapsuleHeightMeters = capsuleHeightMeters;
            CandidateLayerMask = candidateLayerMask;
            TriggerInteraction = triggerInteraction;
        }

        public PerceptionSpatialVolumeShape Shape { get; }

        public Vector3 Center { get; }

        public Quaternion Rotation { get; }

        public Vector3 BoxSizeMeters { get; }

        public float RadiusMeters { get; }

        public float CapsuleHeightMeters { get; }

        public int CandidateLayerMask { get; }

        public QueryTriggerInteraction TriggerInteraction { get; }

        internal bool HasSameGeometry(PerceptionSpatialDiscoveryVolume other)
        {
            if (Shape != other.Shape || !Center.Equals(other.Center))
            {
                return false;
            }

            switch (Shape)
            {
                case PerceptionSpatialVolumeShape.Sphere:
                    return RadiusMeters.Equals(other.RadiusMeters);
                case PerceptionSpatialVolumeShape.Box:
                    return Rotation.Equals(other.Rotation) && BoxSizeMeters.Equals(other.BoxSizeMeters);
                case PerceptionSpatialVolumeShape.Capsule:
                    return Rotation.Equals(other.Rotation) &&
                           RadiusMeters.Equals(other.RadiusMeters) &&
                           CapsuleHeightMeters.Equals(other.CapsuleHeightMeters);
                default:
                    return false;
            }
        }
    }
}

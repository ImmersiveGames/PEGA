using UnityEngine;

namespace ImmersiveGames.Perception
{
    [CreateAssetMenu(
        fileName = "VisionConeSensor",
        menuName = "Immersive/Perception/Vision Cone Sensor")]
    public sealed class VisionConeSensor : PerceptionSensorDefinition
    {
        [SerializeField, Min(0.1f)]
        [Tooltip("Alcance horizontal máximo deste Cone de Visão (m).")]
        private float rangeMeters = 1f;

        [SerializeField, Range(1f, 180f)]
        [Tooltip("Ângulo total à frente do observador em que este sensor detecta (°).")]
        private float fieldOfViewDegrees = 90f;

        public float RangeMeters => rangeMeters;

        public Vector3 GetForward(Transform observer) => observer.forward;

        public float FieldOfViewDegrees => fieldOfViewDegrees;

        protected override bool TryEvaluateSensorBounds(
            Transform observer,
            Vector3 observerOrigin,
            Vector3 targetAimPoint,
            float planarDistanceMeters,
            out bool isWithinSensorBounds,
            out string diagnostic)
        {
            Vector3 planarForward = GetForward(observer);
            planarForward.y = 0f;
            bool hasForward = planarForward.sqrMagnitude >
                MinimumPlanarMagnitudeMeters * MinimumPlanarMagnitudeMeters;
            if (hasForward)
            {
                planarForward.Normalize();
            }

            Vector3 planarToTarget = targetAimPoint - observerOrigin;
            planarToTarget.y = 0f;
            isWithinSensorBounds = hasForward &&
                planarDistanceMeters <= rangeMeters &&
                planarDistanceMeters > MinimumPlanarMagnitudeMeters &&
                Vector3.Angle(planarForward, planarToTarget) <= fieldOfViewDegrees * 0.5f;

            diagnostic = string.Empty;
            return true;
        }

        protected override bool TryValidateSensorConfiguration(out string diagnostic)
        {
            if (!IsPositiveFinite(rangeMeters))
            {
                diagnostic = "Vision range must be finite and positive.";
                return false;
            }

            if (float.IsNaN(fieldOfViewDegrees) ||
                float.IsInfinity(fieldOfViewDegrees) ||
                fieldOfViewDegrees <= 0f ||
                fieldOfViewDegrees > 180f)
            {
                diagnostic = "Vision field of view must be finite and in the interval (0, 180] degrees.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }
    }
}

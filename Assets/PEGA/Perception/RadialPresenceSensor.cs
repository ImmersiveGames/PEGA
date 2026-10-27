using UnityEngine;

namespace ImmersiveGames.Perception
{
    [CreateAssetMenu(
        fileName = "RadialPresenceSensor",
        menuName = "Immersive/Perception/Radial Presence Sensor")]
    public sealed class RadialPresenceSensor : PerceptionSensorDefinition
    {
        [SerializeField, Min(0.1f)]
        [Tooltip("Raio horizontal da região em que este sensor detecta presença (m).")]
        private float radiusMeters = 1f;

        public float RadiusMeters => radiusMeters;

        protected override bool TryEvaluateSensorBounds(
            Transform observer,
            Vector3 observerOrigin,
            Vector3 targetAimPoint,
            float planarDistanceMeters,
            out bool isWithinSensorBounds,
            out string diagnostic)
        {
            isWithinSensorBounds = planarDistanceMeters <= radiusMeters;
            diagnostic = string.Empty;
            return true;
        }

        protected override bool TryValidateSensorConfiguration(out string diagnostic)
        {
            if (!IsPositiveFinite(radiusMeters))
            {
                diagnostic = "Radial presence radius must be finite and positive.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }
    }
}

using UnityEngine;

namespace ImmersiveGames.Perception
{
    /// <summary>Configuração compartilhável e lógica sem estado de um sensor de percepção.</summary>
    /// <remarks>O sensor não guarda estado do Actor; a amostragem recebe contexto e retorna um resultado.</remarks>
    public abstract class PerceptionSensorDefinition : ScriptableObject
    {
        protected const float MinimumPlanarMagnitudeMeters = 0.0001f;

        [SerializeField]
        [Tooltip("Identificador único deste sensor dentro do Perception Sensor Set consumidor.")]
        private string sensorId;

        [SerializeField, Min(0f)]
        [Tooltip("Altura da origem deste sensor acima do Transform observador (m).")]
        private float observerOriginHeightMeters;

        [SerializeField]
        [Tooltip("Layers cujos colliders bloqueiam este sensor. Configure explicitamente para o projeto.")]
        private LayerMask occlusionLayers;

        public string SensorId => sensorId;

        public float ObserverOriginHeightMeters => observerOriginHeightMeters;

        public Vector3 GetObserverOrigin(Transform observer) =>
            observer.position + Vector3.up * observerOriginHeightMeters;

        public bool TryValidateConfiguration(out string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(sensorId))
            {
                diagnostic = "Sensor identifier must not be empty.";
                return false;
            }

            if (!IsNonNegativeFinite(observerOriginHeightMeters))
            {
                diagnostic = "Observer origin height must be finite and non-negative.";
                return false;
            }

            if (occlusionLayers.value == 0)
            {
                diagnostic = "At least one occlusion layer must be configured.";
                return false;
            }

            return TryValidateSensorConfiguration(out diagnostic);
        }

        internal bool TryObserve(
            Transform observer,
            Vector3 targetAimPoint,
            double observationTimeSeconds,
            out PerceptionSensorObservation observation,
            out string diagnostic)
        {
            observation = default;
            if (!TryValidateConfiguration(out diagnostic))
            {
                return false;
            }

            if (observer == null ||
                !IsFinite(targetAimPoint) ||
                double.IsNaN(observationTimeSeconds) ||
                double.IsInfinity(observationTimeSeconds))
            {
                diagnostic = "Observer, target position and observation time must be valid and finite.";
                return false;
            }

            Vector3 observerOrigin = GetObserverOrigin(observer);
            if (!IsFinite(observerOrigin))
            {
                diagnostic = "Observer origin must be finite.";
                return false;
            }

            Vector3 planarToTarget = targetAimPoint - observerOrigin;
            planarToTarget.y = 0f;
            float planarDistanceMeters = planarToTarget.magnitude;
            if (!IsNonNegativeFinite(planarDistanceMeters))
            {
                diagnostic = "Planar target distance must be finite and non-negative.";
                return false;
            }

            if (!TryEvaluateSensorBounds(
                    observer,
                    observerOrigin,
                    targetAimPoint,
                    planarDistanceMeters,
                    out bool isWithinSensorBounds,
                    out diagnostic))
            {
                return false;
            }

            bool isOccluded = false;
            if (isWithinSensorBounds)
            {
                Vector3 toTarget = targetAimPoint - observerOrigin;
                float distance = toTarget.magnitude;
                if (!IsNonNegativeFinite(distance))
                {
                    diagnostic = "Target ray distance must be finite and non-negative.";
                    return false;
                }

                if (distance > MinimumPlanarMagnitudeMeters)
                {
                    isOccluded = Physics.Raycast(
                        observerOrigin,
                        toTarget / distance,
                        distance,
                        occlusionLayers,
                        QueryTriggerInteraction.Collide);
                }
            }

            observation = new PerceptionSensorObservation(
                observationTimeSeconds,
                observerOrigin,
                targetAimPoint,
                planarDistanceMeters,
                isWithinSensorBounds,
                isOccluded);
            diagnostic = string.Empty;
            return true;
        }

        protected abstract bool TryValidateSensorConfiguration(out string diagnostic);

        protected abstract bool TryEvaluateSensorBounds(
            Transform observer,
            Vector3 observerOrigin,
            Vector3 targetAimPoint,
            float planarDistanceMeters,
            out bool isWithinSensorBounds,
            out string diagnostic);

        protected static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        protected static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

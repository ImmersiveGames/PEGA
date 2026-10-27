using System;
using UnityEngine;

namespace ImmersiveGames.Perception
{
    /// <summary>Observação independente de identidade produzida por uma definição de sensor.</summary>
    public readonly struct PerceptionSensorObservation
    {
        public PerceptionSensorObservation(
            double observationTimeSeconds,
            Vector3 observerOrigin,
            Vector3 targetAimPoint,
            float planarDistanceMeters,
            bool isWithinSensorBounds,
            bool isOccluded)
        {
            if (double.IsNaN(observationTimeSeconds) || double.IsInfinity(observationTimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(observationTimeSeconds));
            }

            if (!IsFinite(observerOrigin))
            {
                throw new ArgumentOutOfRangeException(nameof(observerOrigin));
            }

            if (!IsFinite(targetAimPoint))
            {
                throw new ArgumentOutOfRangeException(nameof(targetAimPoint));
            }

            if (planarDistanceMeters < 0f || float.IsNaN(planarDistanceMeters) || float.IsInfinity(planarDistanceMeters))
            {
                throw new ArgumentOutOfRangeException(nameof(planarDistanceMeters));
            }

            ObservationTimeSeconds = observationTimeSeconds;
            ObserverOrigin = observerOrigin;
            TargetAimPoint = targetAimPoint;
            PlanarDistanceMeters = planarDistanceMeters;
            IsWithinSensorBounds = isWithinSensorBounds;
            IsOccluded = isOccluded;
        }

        public double ObservationTimeSeconds { get; }

        public Vector3 ObserverOrigin { get; }

        public Vector3 TargetAimPoint { get; }

        public float PlanarDistanceMeters { get; }

        public bool IsWithinSensorBounds { get; }

        public bool IsOccluded { get; }

        public bool IsDetected => IsWithinSensorBounds && !IsOccluded;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Resultado independente de um sensor configurado em uma amostragem.</summary>
    public readonly struct PerceptionSensorResult
    {
        internal PerceptionSensorResult(
            PerceptionSensorDefinition sensorDefinition,
            bool hasObservation,
            PerceptionSensorObservation observation,
            string diagnostic)
        {
            SensorDefinition = sensorDefinition;
            SensorId = sensorDefinition != null ? sensorDefinition.SensorId : string.Empty;
            HasObservation = hasObservation;
            Observation = observation;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public PerceptionSensorDefinition SensorDefinition { get; }

        public string SensorId { get; }

        public bool HasObservation { get; }

        public PerceptionSensorObservation Observation { get; }

        public string Diagnostic { get; }
    }
}

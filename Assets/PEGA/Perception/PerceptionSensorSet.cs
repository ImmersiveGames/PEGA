using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveGames.Perception
{
    /// <summary>Instância runtime de um conjunto de sensores configurado pelo consumidor.</summary>
    /// <remarks>
    /// Esta classe não é um componente Unity. Cada consumidor cria sua própria instância com um observador e uma
    /// lista de definições; os assets permanecem compartilháveis e sem estado runtime. Os resultados pertencem a
    /// esta instância e são reutilizados na próxima amostragem.
    /// </remarks>
    public sealed class PerceptionSensorSet
    {
        private readonly Transform _observer;
        private readonly IReadOnlyList<PerceptionSensorDefinition> _sensors;

        private List<PerceptionSensorResult> _latestResults;

        public PerceptionSensorSet(
            Transform observer,
            IReadOnlyList<PerceptionSensorDefinition> sensors)
        {
            _observer = observer;
            _sensors = sensors;
        }

        public bool TryValidateComposition(out string diagnostic)
        {
            if (_observer == null)
            {
                diagnostic = "Perception observer must be available.";
                return false;
            }

            if (_sensors == null)
            {
                diagnostic = "Sensor list is missing; use an empty list when no sensors are configured.";
                return false;
            }

            for (int index = 0; index < _sensors.Count; index++)
            {
                PerceptionSensorDefinition sensor = _sensors[index];
                if (sensor == null)
                {
                    diagnostic = $"Sensor list entry {index} has no sensor configuration asset.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(sensor.SensorId))
                {
                    diagnostic = $"Sensor list entry {index} has no identifier.";
                    return false;
                }

                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    if (string.Equals(
                            _sensors[previousIndex].SensorId,
                            sensor.SensorId,
                            StringComparison.Ordinal))
                    {
                        diagnostic = $"Sensor identifier '{sensor.SensorId}' is repeated in the configured list.";
                        return false;
                    }
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        public bool TryGetSensor<TSensor>(
            string sensorId,
            out TSensor sensor,
            out string diagnostic)
            where TSensor : PerceptionSensorDefinition
        {
            sensor = null;
            if (!TryValidateComposition(out diagnostic))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(sensorId))
            {
                diagnostic = "Requested sensor identifier must not be empty.";
                return false;
            }

            for (int index = 0; index < _sensors.Count; index++)
            {
                PerceptionSensorDefinition candidate = _sensors[index];
                if (!string.Equals(candidate.SensorId, sensorId, StringComparison.Ordinal))
                {
                    continue;
                }

                sensor = candidate as TSensor;
                if (sensor == null)
                {
                    diagnostic = $"Sensor '{sensorId}' is not a {typeof(TSensor).Name}.";
                    return false;
                }

                diagnostic = string.Empty;
                return true;
            }

            diagnostic = $"No configured sensor has identifier '{sensorId}'.";
            return false;
        }

        public bool TryObserveAll(
            Vector3 targetAimPoint,
            double observationTimeSeconds,
            out IReadOnlyList<PerceptionSensorResult> results,
            out string diagnostic)
        {
            results = Array.Empty<PerceptionSensorResult>();
            if (!TryValidateComposition(out diagnostic))
            {
                ClearLatestResults();
                return false;
            }

            List<PerceptionSensorResult> latestResults = EnsureLatestResults();
            latestResults.Clear();
            for (int index = 0; index < _sensors.Count; index++)
            {
                PerceptionSensorDefinition sensor = _sensors[index];
                bool hasObservation = sensor.TryObserve(
                    _observer,
                    targetAimPoint,
                    observationTimeSeconds,
                    out PerceptionSensorObservation observation,
                    out string sensorDiagnostic);
                latestResults.Add(new PerceptionSensorResult(
                    sensor,
                    hasObservation,
                    observation,
                    sensorDiagnostic));
            }

            results = latestResults;
            diagnostic = string.Empty;
            return true;
        }

        private List<PerceptionSensorResult> EnsureLatestResults() =>
            _latestResults ?? (_latestResults = new List<PerceptionSensorResult>());

        private void ClearLatestResults()
        {
            _latestResults?.Clear();
        }
    }
}

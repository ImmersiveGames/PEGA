using UnityEngine;

namespace ImmersiveGames.Perception
{
    /// <summary>Configuração compartilhável e lógica sem estado de um sensor de percepção.</summary>
    /// <remarks>O sensor não guarda estado do Actor; a amostragem recebe contexto e retorna um resultado.</remarks>
    public abstract class PerceptionSensorDefinition : ScriptableObject
    {
        protected const float MinimumPlanarMagnitudeMeters = 0.0001f;

        [SerializeField]
        [Tooltip("Nome de referência para diagnóstico. A identidade de runtime pertence ao componente de instância.")]
        private string sensorId;

        [SerializeField, Min(0.01f)]
        [Tooltip("Intervalo mínimo entre amostragens desta instância de sensor (s).")]
        private float samplingIntervalSeconds = 0.25f;

        [SerializeField, Min(0f)]
        [Tooltip("Altura da origem deste sensor acima do Transform observador (m).")]
        private float observerOriginHeightMeters;

        [SerializeField]
        [Tooltip("Layers cujos colliders bloqueiam este sensor. Configure explicitamente para o projeto.")]
        private LayerMask occlusionLayers;

        [SerializeField]
        [Tooltip("Aquisição física opcional deste sensor. Desativada por padrão para preservar assets existentes.")]
        private PerceptionSpatialDiscoverySettings spatialDiscovery = new PerceptionSpatialDiscoverySettings();

        public string SensorId => sensorId;

        public float SamplingIntervalSeconds => samplingIntervalSeconds;

        public float ObserverOriginHeightMeters => observerOriginHeightMeters;

        internal bool HasSpatialDiscovery => spatialDiscovery != null && spatialDiscovery.Enabled;

        public Vector3 GetObserverOrigin(Transform observer) =>
            observer.position + Vector3.up * observerOriginHeightMeters;

        /// <summary>Resolve o volume espacial configurado em torno da posição do observador.</summary>
        public bool TryGetSpatialDiscoveryVolume(
            Transform observer,
            out PerceptionSpatialDiscoveryVolume volume,
            out string diagnostic)
        {
            volume = default;
            if (!HasSpatialDiscovery)
            {
                diagnostic = "Spatial discovery is not enabled for this sensor.";
                return false;
            }

            return spatialDiscovery.TryCreateVolume(observer, out volume, out diagnostic);
        }

        /// <summary>Resolve o volume em torno de uma origem mundial atual fornecida pelo consumidor.</summary>
        public bool TryGetSpatialDiscoveryVolume(
            Transform observer,
            Vector3 centerOrigin,
            out PerceptionSpatialDiscoveryVolume volume,
            out string diagnostic)
        {
            volume = default;
            if (!HasSpatialDiscovery)
            {
                diagnostic = "Spatial discovery is not enabled for this sensor.";
                return false;
            }

            return spatialDiscovery.TryCreateVolume(observer, centerOrigin, out volume, out diagnostic);
        }

        public bool TryValidateConfiguration(out string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(sensorId))
            {
                diagnostic = "Sensor diagnostic name must not be empty.";
                return false;
            }

            if (!IsPositiveFinite(samplingIntervalSeconds))
            {
                diagnostic = "Sensor sampling interval must be finite and positive.";
                return false;
            }

            if (!IsNonNegativeFinite(observerOriginHeightMeters))
            {
                diagnostic = "Observer origin height must be finite and non-negative.";
                return false;
            }

            if (RequiresOcclusionLayerMask && occlusionLayers.value == 0)
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
            return TryObserve(
                observer,
                targetAimPoint,
                null,
                observationTimeSeconds,
                out observation,
                out diagnostic);
        }

        private bool TryObserve(
            Transform observer,
            Vector3 targetAimPoint,
            Collider candidateCollider,
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
                    Vector3 rayDirection = toTarget / distance;
                    int candidateLayerBit = candidateCollider != null
                        ? 1 << candidateCollider.gameObject.layer
                        : 0;
                    if (candidateCollider != null && (occlusionLayers.value & candidateLayerBit) != 0)
                    {
                        // O collider alvo pode compartilhar uma layer de oclusão; ignore somente
                        // os colliders do próprio alvo e preserve todos os demais bloqueadores.
                        RaycastHit[] hits = Physics.RaycastAll(
                            observerOrigin,
                            rayDirection,
                            distance,
                            occlusionLayers,
                            QueryTriggerInteraction.Collide);
                        for (int index = 0; index < hits.Length; index++)
                        {
                            Collider hitCollider = hits[index].collider;
                            if (hitCollider == null || IsCandidateCollider(hitCollider, candidateCollider))
                            {
                                continue;
                            }

                            isOccluded = true;
                            break;
                        }
                    }
                    else
                    {
                        isOccluded = Physics.Raycast(
                            observerOrigin,
                            rayDirection,
                            distance,
                            occlusionLayers,
                            QueryTriggerInteraction.Collide);
                    }
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

        /// <summary>
        /// Avaliação comum de um candidato. Sensores que entendem outras evidências
        /// podem sobrescrever este método sem criar uma observação planar legada.
        /// Retorne true somente com um estado de avaliação inicializado; false indica
        /// falha operacional ou de configuração e exige diagnóstico.
        /// </summary>
        protected internal virtual bool TryObserveCandidate(
            Transform observer,
            PerceptionCandidate candidate,
            double observationTimeSeconds,
            out PerceptionSensorEvaluation evaluation,
            out string diagnostic)
        {
            evaluation = default;
            if (observer == null || candidate == null ||
                double.IsNaN(observationTimeSeconds) || double.IsInfinity(observationTimeSeconds))
            {
                diagnostic = "Observer, candidate and observation time must be valid.";
                return false;
            }

            if (!candidate.TryGetUniquePosition(out Vector3 targetAimPoint, out _))
            {
                if (!TryValidateConfiguration(out diagnostic))
                {
                    return false;
                }

                evaluation = new PerceptionSensorEvaluation(PerceptionObservationStatus.Insufficient);
                diagnostic = "Candidate does not contain exactly one position evidence item.";
                return true;
            }

            if (!TryObserve(
                    observer,
                    targetAimPoint,
                    GetUniquePhysicalCollider(candidate),
                    observationTimeSeconds,
                    out PerceptionSensorObservation observation,
                    out diagnostic))
            {
                return false;
            }

            evaluation = new PerceptionSensorEvaluation(
                observation.IsDetected
                    ? PerceptionObservationStatus.Positive
                    : PerceptionObservationStatus.Negative,
                observation);
            diagnostic = string.Empty;
            return true;
        }

        private static Collider GetUniquePhysicalCollider(PerceptionCandidate candidate)
        {
            Collider selected = null;
            for (int index = 0; index < candidate.Evidence.Count; index++)
            {
                PerceptionCandidateEvidence evidence = candidate.Evidence[index];
                if (evidence.Kind != PerceptionCandidateEvidenceKind.PhysicalCollider ||
                    evidence.PhysicalCollider == null)
                {
                    continue;
                }

                if (selected == null)
                {
                    selected = evidence.PhysicalCollider;
                }
                else if (selected != evidence.PhysicalCollider)
                {
                    return null;
                }
            }

            return selected;
        }

        private static bool IsCandidateCollider(Collider hitCollider, Collider candidateCollider)
        {
            return hitCollider == candidateCollider;
        }

        /// <summary>
        /// Sensores que não usam a avaliação pontual legada podem omitir a validação
        /// planar e implementar somente a validação de sua própria configuração.
        /// </summary>
        protected virtual bool RequiresOcclusionLayerMask => true;

        protected virtual bool TryValidateSensorConfiguration(out string diagnostic)
        {
            diagnostic = string.Empty;
            return true;
        }

        protected virtual bool TryEvaluateSensorBounds(
            Transform observer,
            Vector3 observerOrigin,
            Vector3 targetAimPoint,
            float planarDistanceMeters,
            out bool isWithinSensorBounds,
            out string diagnostic)
        {
            isWithinSensorBounds = false;
            diagnostic = "Sensor does not implement positional evaluation.";
            return false;
        }

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

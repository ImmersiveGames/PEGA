using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveGames.Perception
{
    /// <summary>
    /// Política de admissibilidade para candidatos físicos, aplicada após a aquisição
    /// e a exclusão da ocorrência observadora, antes da avaliação pelos sensores.
    /// O predicado não reconhece nem atribui identidade ao collider.
    /// </summary>
    public delegate bool PerceptionPhysicalCandidateEligibility(Collider candidate);

    [Flags]
    public enum PerceptionCandidateOrigin
    {
        None = 0,
        Physical = 1,
        External = 2
    }

    public enum PerceptionCandidateEvidenceKind
    {
        None = 0,
        PhysicalCollider = 1,
        Position = 2,
        ExternalReference = 3
    }

    /// <summary>Completude das fontes consultadas na aquisição de candidatos.</summary>
    public enum PerceptionAcquisitionStatus
    {
        Unspecified = 0,
        Complete = 1,
        Partial = 2,
        Unavailable = 3
    }

    /// <summary>Conclusão sensorial para um candidato, sem interpretação de gameplay.</summary>
    public enum PerceptionObservationStatus
    {
        Unspecified = 0,
        Positive = 1,
        Negative = 2,
        Insufficient = 3,
        Unsupported = 4
    }

    /// <summary>Uma evidência de candidato com origem e instante próprios.</summary>
    public readonly struct PerceptionCandidateEvidence
    {
        private PerceptionCandidateEvidence(
            PerceptionCandidateEvidenceKind kind,
            PerceptionCandidateOrigin origin,
            Collider physicalCollider,
            Vector3 position,
            object externalReference,
            double observedAtSeconds,
            object sourceReference,
            double? validUntilSeconds)
        {
            Kind = kind;
            Origin = origin;
            PhysicalCollider = physicalCollider;
            Position = position;
            ExternalReference = externalReference;
            ObservedAtSeconds = observedAtSeconds;
            SourceReference = sourceReference;
            ValidUntilSeconds = validUntilSeconds;
        }

        public PerceptionCandidateEvidenceKind Kind { get; }

        public PerceptionCandidateOrigin Origin { get; }

        public Collider PhysicalCollider { get; }

        public Vector3 Position { get; }

        /// <summary>
        /// Referência fornecida pelo consumidor. Perception a transporta sem interpretar,
        /// comparar ou tratá-la como identidade reconhecida.
        /// </summary>
        public object ExternalReference { get; }

        /// <summary>Instante em segundos na base temporal do consumidor.</summary>
        public double ObservedAtSeconds { get; }

        /// <summary>Token opaco da fonte desta evidência; não é uma identidade de domínio.</summary>
        public object SourceReference { get; }

        /// <summary>
        /// Nulo quando a evidência é apenas pontual. Os segundos usam a base de tempo
        /// do consumidor e não são convertidos ou comparados por Perception.
        /// </summary>
        public double? ValidUntilSeconds { get; }

        public bool IsValid => Kind != PerceptionCandidateEvidenceKind.None;

        public static PerceptionCandidateEvidence ForPhysicalCollider(
            Collider collider,
            PerceptionCandidateOrigin origin,
            double observedAtSeconds,
            object sourceReference = null,
            double? validUntilSeconds = null)
        {
            if (collider == null)
            {
                throw new ArgumentNullException(nameof(collider));
            }

            if (origin != PerceptionCandidateOrigin.Physical &&
                origin != PerceptionCandidateOrigin.External)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(origin),
                    "Collider evidence must have one explicit origin.");
            }

            ValidateTime(observedAtSeconds, nameof(observedAtSeconds));
            ValidateOptionalTime(validUntilSeconds, nameof(validUntilSeconds));
            return new PerceptionCandidateEvidence(
                PerceptionCandidateEvidenceKind.PhysicalCollider,
                origin,
                collider,
                default,
                null,
                observedAtSeconds,
                sourceReference,
                validUntilSeconds);
        }

        public static PerceptionCandidateEvidence ForPosition(
            Vector3 position,
            PerceptionCandidateOrigin origin,
            double observedAtSeconds,
            object sourceReference = null,
            double? validUntilSeconds = null)
        {
            if (origin != PerceptionCandidateOrigin.Physical &&
                origin != PerceptionCandidateOrigin.External)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(origin),
                    "Position evidence must have one explicit origin.");
            }

            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            ValidateTime(observedAtSeconds, nameof(observedAtSeconds));
            ValidateOptionalTime(validUntilSeconds, nameof(validUntilSeconds));
            return new PerceptionCandidateEvidence(
                PerceptionCandidateEvidenceKind.Position,
                origin,
                null,
                position,
                null,
                observedAtSeconds,
                sourceReference,
                validUntilSeconds);
        }

        public static PerceptionCandidateEvidence ForExternalReference(
            object externalReference,
            double observedAtSeconds,
            object sourceReference = null,
            double? validUntilSeconds = null)
        {
            if (externalReference == null)
            {
                throw new ArgumentNullException(nameof(externalReference));
            }

            ValidateTime(observedAtSeconds, nameof(observedAtSeconds));
            ValidateOptionalTime(validUntilSeconds, nameof(validUntilSeconds));
            return new PerceptionCandidateEvidence(
                PerceptionCandidateEvidenceKind.ExternalReference,
                PerceptionCandidateOrigin.External,
                null,
                default,
                externalReference,
                observedAtSeconds,
                sourceReference,
                validUntilSeconds);
        }

        private static void ValidateTime(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateOptionalTime(double? value, string parameterName)
        {
            if (value.HasValue)
            {
                ValidateTime(value.Value, parameterName);
            }
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Um candidato é um conjunto explícito de evidências correlacionadas. O token
    /// opcional só é transportado; esta classe não o compara nem lhe atribui identidade.
    /// </summary>
    public sealed class PerceptionCandidate
    {
        private readonly IReadOnlyList<PerceptionCandidateEvidence> _evidence;

        public PerceptionCandidate(
            IReadOnlyList<PerceptionCandidateEvidence> evidence,
            object correlationToken = null)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            if (evidence.Count == 0)
            {
                throw new ArgumentException("A candidate must contain at least one evidence item.", nameof(evidence));
            }

            PerceptionCandidateEvidence[] copy = new PerceptionCandidateEvidence[evidence.Count];
            PerceptionCandidateOrigin origins = PerceptionCandidateOrigin.None;
            for (int index = 0; index < evidence.Count; index++)
            {
                PerceptionCandidateEvidence item = evidence[index];
                if (!item.IsValid)
                {
                    throw new ArgumentException(
                        $"Candidate evidence entry {index} is uninitialized.",
                        nameof(evidence));
                }

                copy[index] = item;
                origins |= item.Origin;
            }

            _evidence = Array.AsReadOnly(copy);
            CorrelationToken = correlationToken;
            Origins = origins;
        }

        public IReadOnlyList<PerceptionCandidateEvidence> Evidence => _evidence;

        public object CorrelationToken { get; }

        public bool HasCorrelationToken => CorrelationToken != null;

        public PerceptionCandidateOrigin Origins { get; }

        /// <summary>
        /// Retorna posição apenas quando há exatamente uma evidência de posição;
        /// ausências e múltiplas posições permanecem explícitas para o sensor.
        /// </summary>
        public bool TryGetUniquePosition(out Vector3 position, out double observedAtSeconds)
        {
            position = default;
            observedAtSeconds = default;
            bool found = false;
            for (int index = 0; index < _evidence.Count; index++)
            {
                PerceptionCandidateEvidence item = _evidence[index];
                if (item.Kind != PerceptionCandidateEvidenceKind.Position)
                {
                    continue;
                }

                if (found)
                {
                    position = default;
                    observedAtSeconds = default;
                    return false;
                }

                found = true;
                position = item.Position;
                observedAtSeconds = item.ObservedAtSeconds;
            }

            return found;
        }
    }

    /// <summary>Resultado de uma fonte consultada para adquirir candidatos.</summary>
    public readonly struct PerceptionCandidateSourceResult
    {
        public PerceptionCandidateSourceResult(
            PerceptionCandidateOrigin origin,
            PerceptionAcquisitionStatus status,
            double completedAtSeconds,
            object sourceReference = null,
            object coverageReference = null,
            string diagnostic = null,
            double? validUntilSeconds = null,
            int excludedObserverColliderCount = 0)
        {
            if (origin == PerceptionCandidateOrigin.None ||
                (origin & ~(PerceptionCandidateOrigin.Physical | PerceptionCandidateOrigin.External)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(origin));
            }

            if (status == PerceptionAcquisitionStatus.Unspecified ||
                !Enum.IsDefined(typeof(PerceptionAcquisitionStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (double.IsNaN(completedAtSeconds) || double.IsInfinity(completedAtSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(completedAtSeconds));
            }

            if (validUntilSeconds.HasValue &&
                (double.IsNaN(validUntilSeconds.Value) || double.IsInfinity(validUntilSeconds.Value)))
            {
                throw new ArgumentOutOfRangeException(nameof(validUntilSeconds));
            }

            if (excludedObserverColliderCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(excludedObserverColliderCount));
            }

            Origin = origin;
            Status = status;
            CompletedAtSeconds = completedAtSeconds;
            SourceReference = sourceReference;
            CoverageReference = coverageReference;
            Diagnostic = diagnostic ?? string.Empty;
            ValidUntilSeconds = validUntilSeconds;
            ExcludedObserverColliderCount = excludedObserverColliderCount;
        }

        public PerceptionCandidateOrigin Origin { get; }

        public PerceptionAcquisitionStatus Status { get; }

        /// <summary>Instante de conclusão na mesma base temporal das evidências.</summary>
        public double CompletedAtSeconds { get; }

        /// <summary>Identifica a fonte sem conferir autoridade de identidade ao valor.</summary>
        public object SourceReference { get; }

        /// <summary>
        /// Descritor opaco do escopo consultado. Pode ser nulo quando a cobertura
        /// está implícita na configuração da fonte. Complete vale somente para esse
        /// escopo, nunca para o mundo inteiro por inferência.
        /// </summary>
        public object CoverageReference { get; }

        public string Diagnostic { get; }

        /// <summary>Nulo quando a cobertura só é válida no instante da amostragem.</summary>
        public double? ValidUntilSeconds { get; }

        /// <summary>Número de colliders da ocorrência observadora removidos desta consulta física.</summary>
        public int ExcludedObserverColliderCount { get; }
    }

    /// <summary>
    /// Lote de aquisição. O estado agregado é derivado dos resultados das fontes;
    /// nenhum lote vazio implica ausência global fora das coberturas declaradas.
    /// </summary>
    public sealed class PerceptionCandidateAcquisition
    {
        private readonly IReadOnlyList<PerceptionCandidateSourceResult> _sources;
        private readonly IReadOnlyList<PerceptionCandidate> _candidates;

        public PerceptionCandidateAcquisition(
            IReadOnlyList<PerceptionCandidateSourceResult> sources,
            IReadOnlyList<PerceptionCandidate> candidates)
        {
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            PerceptionCandidateSourceResult[] sourceCopy = new PerceptionCandidateSourceResult[sources.Count];
            for (int index = 0; index < sources.Count; index++)
            {
                PerceptionCandidateSourceResult source = sources[index];
                if (source.Status == PerceptionAcquisitionStatus.Unspecified)
                {
                    throw new ArgumentException(
                        $"Acquisition source entry {index} is uninitialized.",
                        nameof(sources));
                }

                sourceCopy[index] = source;
            }

            PerceptionCandidateOrigin availableOrigins = PerceptionCandidateOrigin.None;
            for (int index = 0; index < sourceCopy.Length; index++)
            {
                PerceptionCandidateSourceResult source = sourceCopy[index];
                if (source.Status != PerceptionAcquisitionStatus.Unavailable)
                {
                    availableOrigins |= source.Origin;
                }
            }

            PerceptionCandidate[] candidateCopy = new PerceptionCandidate[candidates.Count];
            for (int index = 0; index < candidates.Count; index++)
            {
                PerceptionCandidate candidate = candidates[index];
                if (candidate == null)
                {
                    throw new ArgumentException(
                        $"Candidate entry {index} is null.",
                        nameof(candidates));
                }

                if ((candidate.Origins & availableOrigins) != candidate.Origins)
                {
                    throw new ArgumentException(
                        $"Candidate entry {index} includes evidence from an unavailable or unreported origin.",
                        nameof(candidates));
                }

                candidateCopy[index] = candidate;
            }

            Status = AggregateStatus(sourceCopy);
            if (Status == PerceptionAcquisitionStatus.Unavailable && candidateCopy.Length > 0)
            {
                throw new ArgumentException(
                    "Unavailable acquisition cannot contain candidates.",
                    nameof(candidates));
            }

            _sources = Array.AsReadOnly(sourceCopy);
            _candidates = Array.AsReadOnly(candidateCopy);
        }

        public IReadOnlyList<PerceptionCandidateSourceResult> Sources => _sources;

        public IReadOnlyList<PerceptionCandidate> Candidates => _candidates;

        public PerceptionAcquisitionStatus Status { get; }

        private static PerceptionAcquisitionStatus AggregateStatus(
            IReadOnlyList<PerceptionCandidateSourceResult> sources)
        {
            if (sources.Count == 0)
            {
                return PerceptionAcquisitionStatus.Unavailable;
            }

            bool allComplete = true;
            bool allUnavailable = true;
            for (int index = 0; index < sources.Count; index++)
            {
                PerceptionAcquisitionStatus status = sources[index].Status;
                allComplete &= status == PerceptionAcquisitionStatus.Complete;
                allUnavailable &= status == PerceptionAcquisitionStatus.Unavailable;
            }

            if (allComplete)
            {
                return PerceptionAcquisitionStatus.Complete;
            }

            return allUnavailable
                ? PerceptionAcquisitionStatus.Unavailable
                : PerceptionAcquisitionStatus.Partial;
        }
    }

    /// <summary>Evidência sensorial específica, transportada sem interpretação pelo agregador.</summary>
    public interface IPerceptionObservationEvidence
    {
    }

    /// <summary>Conclusão produzida por uma avaliação sensorial individual.</summary>
    public readonly struct PerceptionSensorEvaluation
    {
        public PerceptionSensorEvaluation(
            PerceptionObservationStatus status,
            IPerceptionObservationEvidence evidence = null,
            double? validUntilSeconds = null)
        {
            if (status == PerceptionObservationStatus.Unspecified ||
                !Enum.IsDefined(typeof(PerceptionObservationStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (validUntilSeconds.HasValue &&
                (double.IsNaN(validUntilSeconds.Value) || double.IsInfinity(validUntilSeconds.Value)))
            {
                throw new ArgumentOutOfRangeException(nameof(validUntilSeconds));
            }

            Status = status;
            Evidence = evidence;
            ValidUntilSeconds = validUntilSeconds;
        }

        public PerceptionObservationStatus Status { get; }

        public IPerceptionObservationEvidence Evidence { get; }

        /// <summary>Nulo quando a observação não afirma validade além da amostragem.</summary>
        public double? ValidUntilSeconds { get; }
    }

    /// <summary>Resultado de uma instância sensorial para um candidato de um lote específico.</summary>
    public readonly struct PerceptionCandidateSensorResult
    {
        internal PerceptionCandidateSensorResult(
            PerceptionSensorInstance sensorInstance,
            int candidateIndex,
            PerceptionCandidate candidate,
            double observationTimeSeconds,
            bool hasOutcome,
            PerceptionSensorEvaluation evaluation,
            string diagnostic)
        {
            SensorInstance = sensorInstance;
            SensorDefinition = sensorInstance != null ? sensorInstance.Definition : null;
            SensorId = SensorDefinition != null ? SensorDefinition.SensorId : string.Empty;
            CandidateIndex = candidateIndex;
            CorrelationToken = candidate != null ? candidate.CorrelationToken : null;
            CandidateOrigins = candidate != null
                ? candidate.Origins
                : PerceptionCandidateOrigin.None;
            ObservationTimeSeconds = observationTimeSeconds;
            HasOutcome = hasOutcome;
            Status = hasOutcome ? evaluation.Status : PerceptionObservationStatus.Unspecified;
            Evidence = hasOutcome ? evaluation.Evidence : null;
            ValidUntilSeconds = hasOutcome ? evaluation.ValidUntilSeconds : null;
            Diagnostic = diagnostic ?? string.Empty;
        }

        /// <summary>Identidade desta instância no Actor; nunca é derivada do asset reutilizável.</summary>
        public PerceptionSensorInstance SensorInstance { get; }

        public PerceptionSensorDefinition SensorDefinition { get; }

        public string SensorId { get; }

        /// <summary>Índice local ao lote; não é uma identidade persistente.</summary>
        public int CandidateIndex { get; }

        /// <summary>Token de correlação do consumidor, transportado sem comparação.</summary>
        public object CorrelationToken { get; }

        public PerceptionCandidateOrigin CandidateOrigins { get; }

        /// <summary>Instante da avaliação na base temporal do consumidor.</summary>
        public double ObservationTimeSeconds { get; }

        /// <summary>False indica falha operacional ou de configuração, não resultado negativo.</summary>
        public bool HasOutcome { get; }

        public PerceptionObservationStatus Status { get; }

        public IPerceptionObservationEvidence Evidence { get; }

        public double? ValidUntilSeconds { get; }

        public string Diagnostic { get; }
    }

    /// <summary>Observação independente de identidade produzida por uma definição de sensor.</summary>
    public readonly struct PerceptionSensorObservation : IPerceptionObservationEvidence
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
            PerceptionSensorInstance sensorInstance,
            bool hasObservation,
            PerceptionSensorObservation observation,
            string diagnostic)
        {
            SensorInstance = sensorInstance;
            SensorDefinition = sensorInstance != null ? sensorInstance.Definition : null;
            SensorId = SensorDefinition != null ? SensorDefinition.SensorId : string.Empty;
            HasObservation = hasObservation;
            Observation = observation;
            Diagnostic = diagnostic ?? string.Empty;
        }

        /// <summary>Identidade desta instância no Actor; assets compartilhados não identificam instâncias.</summary>
        public PerceptionSensorInstance SensorInstance { get; }

        public PerceptionSensorDefinition SensorDefinition { get; }

        public string SensorId { get; }

        public bool HasObservation { get; }

        public PerceptionSensorObservation Observation { get; }

        public string Diagnostic { get; }
    }
}

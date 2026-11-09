using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using UnityEngine;

namespace ImmersiveGames.Perception
{
    /// <summary>Única autoridade de execução dos sensores compostos neste Actor.</summary>
    /// <remarks>Os componentes e assets definem a composição; cadências, buffers e resultados pertencem a esta instância.</remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/Perception/Sensor Authority")]
    public sealed class PerceptionSensorSet : MonoBehaviour
    {
        private const int InitialSpatialQueryBufferSize = 32;
        private const int MaximumSpatialQueryBufferSize = 8192;

        private readonly List<PerceptionSensorInstance> _sensorInstances =
            new List<PerceptionSensorInstance>();

        private readonly List<PerceptionSensorInstance> _composedSensorInstances =
            new List<PerceptionSensorInstance>();

        private readonly List<PerceptionSensorInstance> _dueSensorInstances =
            new List<PerceptionSensorInstance>();

        private readonly Dictionary<PerceptionSensorInstance, SamplingState> _samplingStates =
            new Dictionary<PerceptionSensorInstance, SamplingState>();

        private MonoBehaviour _samplingDriver;

        private readonly HashSet<PerceptionSensorInstance> _retainedSensorInstances =
            new HashSet<PerceptionSensorInstance>();

        private readonly List<PerceptionSensorInstance> _staleSensorInstances =
            new List<PerceptionSensorInstance>();

        private List<PerceptionCandidateSensorResult> _latestCandidateResults;
        private Collider[] _spatialQueryBuffer;

        private struct SamplingState
        {
            internal SamplingState(
                PerceptionSensorDefinition definition,
                float samplingIntervalSeconds,
                double nextSampleTimeSeconds,
                bool isSchedulable)
            {
                Definition = definition;
                SamplingIntervalSeconds = samplingIntervalSeconds;
                NextSampleTimeSeconds = nextSampleTimeSeconds;
                IsSchedulable = isSchedulable;
            }

            internal PerceptionSensorDefinition Definition { get; }

            internal float SamplingIntervalSeconds { get; }

            internal double NextSampleTimeSeconds { get; }

            internal bool IsSchedulable { get; }
        }

        private sealed class SpatialSensorContribution
        {
            internal SpatialSensorContribution(
                PerceptionSensorInstance sensorInstance,
                PerceptionSpatialDiscoveryVolume volume)
            {
                SensorInstance = sensorInstance;
                Volume = volume;
            }

            internal PerceptionSensorInstance SensorInstance { get; }

            internal PerceptionSpatialDiscoveryVolume Volume { get; }
        }

        private sealed class SpatialQueryGroup
        {
            internal SpatialQueryGroup(PerceptionSpatialDiscoveryVolume volume)
            {
                Volume = volume;
                Contributions = new List<SpatialSensorContribution>();
            }

            internal PerceptionSpatialDiscoveryVolume Volume { get; }

            internal List<SpatialSensorContribution> Contributions { get; }

            internal int CandidateLayerMask { get; set; }
        }

        private sealed class DiscoveredCollider
        {
            internal DiscoveredCollider(Collider collider, Vector3 position)
            {
                Collider = collider;
                Position = position;
                Sensors = new List<PerceptionSensorInstance>();
            }

            internal Collider Collider { get; }

            internal Vector3 Position { get; }

            internal List<PerceptionSensorInstance> Sensors { get; }
        }

        private Transform Observer => transform;

        /// <summary>Registra o único adapter que entrega o lote de candidatos deste Actor.</summary>
        public bool TryRegisterSamplingDriver(MonoBehaviour driver, out string diagnostic)
        {
            if (!TryValidateComposition(out diagnostic))
            {
                return false;
            }

            if (driver == null || !driver.isActiveAndEnabled || driver.gameObject != gameObject)
            {
                diagnostic = "Sampling driver must be an active component on the same Actor root as this authority.";
                return false;
            }

            if (_samplingDriver != null && !ReferenceEquals(_samplingDriver, driver))
            {
                diagnostic = $"Actor already has sampling driver '{_samplingDriver.GetType().Name}'. Only one driver may advance an Actor sample batch.";
                return false;
            }

            _samplingDriver = driver;
            diagnostic = string.Empty;
            return true;
        }

        public void UnregisterSamplingDriver(MonoBehaviour driver)
        {
            if (ReferenceEquals(_samplingDriver, driver))
            {
                _samplingDriver = null;
            }
        }

        public bool TryValidateComposition(out string diagnostic)
        {
            if (!enabled || !gameObject.activeInHierarchy)
            {
                diagnostic = "Perception Sensor Set is disabled.";
                return false;
            }

            if (GetComponent<ActorDeclaration>() == null)
            {
                diagnostic = "Perception Sensor Set must be on the root GameObject that owns the Framework ActorDeclaration.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        public bool ContainsSensorInstance(PerceptionSensorInstance target)
        {
            return target != null && target.gameObject == gameObject;
        }

        /// <summary>Solicita uma amostra imediata de todas as instâncias ativas do Actor.</summary>
        public void RequestImmediateSample(MonoBehaviour samplingDriver)
        {
            if (enabled && gameObject.activeInHierarchy &&
                samplingDriver != null && samplingDriver.isActiveAndEnabled &&
                ReferenceEquals(_samplingDriver, samplingDriver))
            {
                _samplingStates.Clear();
            }
        }

        /// <summary>Solicita uma amostra imediata somente para a instância indicada.</summary>
        public void RequestImmediateSample(
            MonoBehaviour samplingDriver,
            PerceptionSensorInstance sensorInstance)
        {
            if (enabled && gameObject.activeInHierarchy &&
                samplingDriver != null && samplingDriver.isActiveAndEnabled &&
                ReferenceEquals(_samplingDriver, samplingDriver) &&
                sensorInstance != null && sensorInstance.enabled &&
                sensorInstance.gameObject.activeInHierarchy &&
                ContainsSensorInstance(sensorInstance))
            {
                _samplingStates.Remove(sensorInstance);
            }
        }

        public bool HasDueSensorInstances(MonoBehaviour samplingDriver, double sampleTimeSeconds)
        {
            if (!isActiveAndEnabled ||
                samplingDriver == null ||
                !samplingDriver.isActiveAndEnabled ||
                !ReferenceEquals(_samplingDriver, samplingDriver) ||
                double.IsNaN(sampleTimeSeconds) || double.IsInfinity(sampleTimeSeconds))
            {
                return false;
            }

            RefreshSensorInstances();
            for (int index = 0; index < _sensorInstances.Count; index++)
            {
                PerceptionSensorInstance sensorInstance = _sensorInstances[index];
                PerceptionSensorDefinition definition = sensorInstance.Definition;
                float samplingIntervalSeconds = definition != null
                    ? definition.SamplingIntervalSeconds
                    : 0f;
                if (!_samplingStates.TryGetValue(sensorInstance, out SamplingState state) ||
                    !HasSameSamplingConfiguration(state, definition, samplingIntervalSeconds) ||
                    (state.IsSchedulable && sampleTimeSeconds >= state.NextSampleTimeSeconds))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Agenda, adquire e avalia somente as instâncias devidas. A amostra forçada
        /// ignora a cadência uma vez e não altera o próximo vencimento periódico.
        /// </summary>
        public bool TrySample(
            MonoBehaviour samplingDriver,
            ActorOccurrenceResolution? observerOccurrence,
            PerceptionCandidateAcquisition externallySuppliedCandidates,
            double sampleTimeSeconds,
            PerceptionSensorInstance spatialCenterOverrideSensorInstance,
            Vector3? spatialCenterOriginOverride,
            bool force,
            out bool sampled,
            out IReadOnlyList<PerceptionSensorInstance> sampledSensorInstances,
            out PerceptionCandidateAcquisition acquisition,
            out IReadOnlyList<PerceptionCandidateSensorResult> results,
            out string diagnostic)
        {
            return TrySample(
                samplingDriver,
                observerOccurrence,
                externallySuppliedCandidates,
                sampleTimeSeconds,
                spatialCenterOverrideSensorInstance,
                spatialCenterOriginOverride,
                force,
                null,
                out sampled,
                out sampledSensorInstances,
                out acquisition,
                out results,
                out diagnostic);
        }

        /// <summary>
        /// Agenda, adquire e avalia somente as instâncias devidas, aplicando opcionalmente
        /// uma política genérica de admissibilidade aos colliders físicos antes da avaliação.
        /// </summary>
        public bool TrySample(
            MonoBehaviour samplingDriver,
            ActorOccurrenceResolution? observerOccurrence,
            PerceptionCandidateAcquisition externallySuppliedCandidates,
            double sampleTimeSeconds,
            PerceptionSensorInstance spatialCenterOverrideSensorInstance,
            Vector3? spatialCenterOriginOverride,
            bool force,
            PerceptionPhysicalCandidateEligibility physicalCandidateEligibility,
            out bool sampled,
            out IReadOnlyList<PerceptionSensorInstance> sampledSensorInstances,
            out PerceptionCandidateAcquisition acquisition,
            out IReadOnlyList<PerceptionCandidateSensorResult> results,
            out string diagnostic)
        {
            sampled = false;
            sampledSensorInstances = Array.Empty<PerceptionSensorInstance>();
            acquisition = null;
            results = Array.Empty<PerceptionCandidateSensorResult>();
            ClearLatestCandidateResults();

            if (double.IsNaN(sampleTimeSeconds) || double.IsInfinity(sampleTimeSeconds))
            {
                diagnostic = "Perception sample time must be finite.";
                return false;
            }

            if (!isActiveAndEnabled)
            {
                if (enabled && gameObject.activeInHierarchy &&
                    samplingDriver != null && samplingDriver.isActiveAndEnabled &&
                    ReferenceEquals(_samplingDriver, samplingDriver))
                {
                    // O Unity pode chamar outro OnEnable antes do callback deste Set.
                    // Adia a chamada sem consumir a agenda nem reportar autoridade desabilitada.
                    diagnostic = string.Empty;
                    return true;
                }

                diagnostic = !enabled || !gameObject.activeInHierarchy
                    ? "Perception Sensor Set is disabled."
                    : "Only the registered Actor sampling driver may advance a perception batch.";
                return false;
            }

            if (samplingDriver == null ||
                !samplingDriver.isActiveAndEnabled ||
                !ReferenceEquals(_samplingDriver, samplingDriver))
            {
                diagnostic = "Only the registered Actor sampling driver may advance a perception batch.";
                return false;
            }

            if (spatialCenterOriginOverride.HasValue && !IsFinite(spatialCenterOriginOverride.Value))
            {
                diagnostic = "Spatial discovery center origin must be finite.";
                return false;
            }

            if (spatialCenterOriginOverride.HasValue != (spatialCenterOverrideSensorInstance != null) ||
                (spatialCenterOverrideSensorInstance != null &&
                 (!spatialCenterOverrideSensorInstance.isActiveAndEnabled ||
                  !ContainsSensorInstance(spatialCenterOverrideSensorInstance))))
            {
                diagnostic = "A spatial center override must identify one active sensor instance in this Actor.";
                return false;
            }

            if (!TryValidateComposition(out diagnostic))
            {
                return false;
            }

            RefreshSensorInstances();
            SelectDueSensorInstances(sampleTimeSeconds, force);
            if (_dueSensorInstances.Count == 0)
            {
                diagnostic = string.Empty;
                return true;
            }

            sampledSensorInstances = _dueSensorInstances;
            if (!force)
            {
                AdvanceSamplingSchedule(sampleTimeSeconds);
            }

            if (!TryAcquireCandidatesCore(
                    observerOccurrence,
                    physicalCandidateEligibility,
                    externallySuppliedCandidates,
                    sampleTimeSeconds,
                    spatialCenterOverrideSensorInstance,
                    spatialCenterOriginOverride,
                    _dueSensorInstances,
                    out acquisition,
                    out diagnostic))
            {
                return false;
            }

            if (!TryObserveCandidatesCore(
                    acquisition,
                    sampleTimeSeconds,
                    _dueSensorInstances,
                    out results,
                    out diagnostic))
            {
                acquisition = null;
                return false;
            }

            sampled = true;
            diagnostic = string.Empty;
            return true;
        }

        private bool TryObserveCandidatesCore(
            PerceptionCandidateAcquisition acquisition,
            double observationTimeSeconds,
            IReadOnlyList<PerceptionSensorInstance> sensorInstances,
            out IReadOnlyList<PerceptionCandidateSensorResult> results,
            out string diagnostic)
        {
            results = Array.Empty<PerceptionCandidateSensorResult>();
            if (acquisition == null)
            {
                diagnostic = "Perception candidate acquisition is missing.";
                ClearLatestCandidateResults();
                return false;
            }

            List<PerceptionCandidateSensorResult> latestResults = EnsureLatestCandidateResults();
            latestResults.Clear();
            for (int candidateIndex = 0; candidateIndex < acquisition.Candidates.Count; candidateIndex++)
            {
                PerceptionCandidate candidate = acquisition.Candidates[candidateIndex];
                for (int sensorIndex = 0; sensorIndex < sensorInstances.Count; sensorIndex++)
                {
                    PerceptionSensorInstance sensorInstance = sensorInstances[sensorIndex];
                    PerceptionSensorDefinition sensor = sensorInstance != null
                        ? sensorInstance.Definition
                        : null;
                    if (sensor == null)
                    {
                        latestResults.Add(new PerceptionCandidateSensorResult(
                            sensorInstance,
                            candidateIndex,
                            candidate,
                            observationTimeSeconds,
                            false,
                            default,
                            "Sensor instance has no definition asset."));
                        continue;
                    }

                    if (!IsPositiveFinite(sensor.SamplingIntervalSeconds))
                    {
                        latestResults.Add(new PerceptionCandidateSensorResult(
                            sensorInstance,
                            candidateIndex,
                            candidate,
                            observationTimeSeconds,
                            false,
                            default,
                            "Sensor sampling interval must be finite and positive."));
                        continue;
                    }

                    bool hasOutcome = sensor.TryObserveCandidate(
                        Observer,
                        candidate,
                        observationTimeSeconds,
                        out PerceptionSensorEvaluation evaluation,
                        out string sensorDiagnostic);
                    if (hasOutcome && evaluation.Status == PerceptionObservationStatus.Unspecified)
                    {
                        hasOutcome = false;
                        sensorDiagnostic = "Sensor returned an uninitialized observation outcome.";
                    }

                    latestResults.Add(new PerceptionCandidateSensorResult(
                        sensorInstance,
                        candidateIndex,
                        candidate,
                        observationTimeSeconds,
                        hasOutcome,
                        evaluation,
                        sensorDiagnostic));
                }
            }

            results = latestResults;
            diagnostic = string.Empty;
            return true;
        }

        private bool TryAcquireCandidatesCore(
            ActorOccurrenceResolution? observerOccurrence,
            PerceptionPhysicalCandidateEligibility physicalCandidateEligibility,
            PerceptionCandidateAcquisition externallySuppliedCandidates,
            double acquisitionTimeSeconds,
            PerceptionSensorInstance spatialCenterOverrideSensorInstance,
            Vector3? spatialCenterOriginOverride,
            IReadOnlyList<PerceptionSensorInstance> sensorInstances,
            out PerceptionCandidateAcquisition acquisition,
            out string diagnostic)
        {
            acquisition = null;
            var sourceResults = new List<PerceptionCandidateSourceResult>();
            var candidates = new List<PerceptionCandidate>();
            if (externallySuppliedCandidates != null)
            {
                for (int index = 0; index < externallySuppliedCandidates.Sources.Count; index++)
                {
                    sourceResults.Add(externallySuppliedCandidates.Sources[index]);
                }

                for (int index = 0; index < externallySuppliedCandidates.Candidates.Count; index++)
                {
                    candidates.Add(externallySuppliedCandidates.Candidates[index]);
                }
            }

            var queryGroups = new List<SpatialQueryGroup>();
            for (int sensorIndex = 0; sensorIndex < sensorInstances.Count; sensorIndex++)
            {
                PerceptionSensorInstance sensorInstance = sensorInstances[sensorIndex];
                PerceptionSensorDefinition sensor = sensorInstance != null
                    ? sensorInstance.Definition
                    : null;
                if (sensor == null || !sensor.HasSpatialDiscovery)
                {
                    continue;
                }

                if (!sensorInstance.TryValidateConfiguration(out string sensorDiagnostic))
                {
                    sourceResults.Add(new PerceptionCandidateSourceResult(
                        PerceptionCandidateOrigin.Physical,
                        PerceptionAcquisitionStatus.Unavailable,
                        acquisitionTimeSeconds,
                        sensorInstance,
                        sensor,
                        sensorDiagnostic));
                    continue;
                }

                PerceptionSpatialDiscoveryVolume volume;
                string volumeDiagnostic;
                bool hasCenterOverride =
                    spatialCenterOriginOverride.HasValue &&
                    ReferenceEquals(sensorInstance, spatialCenterOverrideSensorInstance);
                bool hasVolume = hasCenterOverride
                    ? sensor.TryGetSpatialDiscoveryVolume(
                        Observer,
                        spatialCenterOriginOverride.Value,
                        out volume,
                        out volumeDiagnostic)
                    : sensor.TryGetSpatialDiscoveryVolume(
                        Observer,
                        out volume,
                        out volumeDiagnostic);
                if (!hasVolume)
                {
                    sourceResults.Add(new PerceptionCandidateSourceResult(
                        PerceptionCandidateOrigin.Physical,
                        PerceptionAcquisitionStatus.Unavailable,
                        acquisitionTimeSeconds,
                        sensorInstance,
                        sensor,
                        volumeDiagnostic));
                    continue;
                }

                SpatialQueryGroup group = FindQueryGroup(queryGroups, volume);
                if (group == null)
                {
                    group = new SpatialQueryGroup(volume);
                    queryGroups.Add(group);
                }

                group.Contributions.Add(new SpatialSensorContribution(sensorInstance, volume));
                group.CandidateLayerMask |= volume.CandidateLayerMask;
            }

            if (queryGroups.Count > 0)
            {
                if (!TryGetObserverOccurrenceRoot(
                        observerOccurrence,
                        out GameObject observerActorRoot,
                        out string occurrenceDiagnostic))
                {
                    AddUnavailableSources(queryGroups, sourceResults, acquisitionTimeSeconds, occurrenceDiagnostic);
                }
                else
                {
                    // Unity Object equality correlates separate managed wrappers for the same native Collider.
                    // Different Collider components remain distinct even when they share a GameObject.
                    var discovered = new Dictionary<Collider, DiscoveredCollider>();
                    for (int groupIndex = 0; groupIndex < queryGroups.Count; groupIndex++)
                    {
                        SpatialQueryGroup group = queryGroups[groupIndex];
                        PerceptionAcquisitionStatus status;
                        string groupDiagnostic;
                        bool querySucceeded;
                        int hitCount;
                        var excludedObserverColliderCounts =
                            new Dictionary<PerceptionSensorInstance, int>();
                        int ineligiblePhysicalCandidateCount = 0;

                        try
                        {
                            querySucceeded = TryRunSpatialQuery(
                                group.Volume,
                                group.CandidateLayerMask,
                                out hitCount,
                                out bool complete,
                                out groupDiagnostic);
                            status = querySucceeded
                                ? complete
                                    ? PerceptionAcquisitionStatus.Complete
                                    : PerceptionAcquisitionStatus.Partial
                                : PerceptionAcquisitionStatus.Unavailable;
                        }
                        catch (Exception exception)
                        {
                            querySucceeded = false;
                            hitCount = 0;
                            status = PerceptionAcquisitionStatus.Unavailable;
                            groupDiagnostic = "Spatial physics query failed. " + exception.Message;
                        }

                        if (querySucceeded)
                        {
                            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                            {
                                Collider collider = _spatialQueryBuffer[hitIndex];
                                if (collider == null)
                                {
                                    status = PerceptionAcquisitionStatus.Partial;
                                    groupDiagnostic = AppendDiagnostic(
                                        groupDiagnostic,
                                        "Spatial query returned a collider that is no longer available.");
                                    continue;
                                }

                                if (!TryGetContributingSensors(group, collider, out List<PerceptionSensorInstance> contributingSensors))
                                {
                                    continue;
                                }

                                if (BelongsToOccurrence(collider, observerActorRoot.transform))
                                {
                                    for (int sensorIndex = 0; sensorIndex < contributingSensors.Count; sensorIndex++)
                                    {
                                        PerceptionSensorInstance sensorInstance = contributingSensors[sensorIndex];
                                        excludedObserverColliderCounts.TryGetValue(sensorInstance, out int count);
                                        excludedObserverColliderCounts[sensorInstance] = count + 1;
                                    }

                                    continue;
                                }

                                if (physicalCandidateEligibility != null)
                                {
                                    bool isEligible;
                                    try
                                    {
                                        isEligible = physicalCandidateEligibility(collider);
                                    }
                                    catch (Exception exception)
                                    {
                                        status = PerceptionAcquisitionStatus.Partial;
                                        groupDiagnostic = AppendDiagnostic(
                                            groupDiagnostic,
                                            "Physical candidate eligibility failed. " + exception.Message);
                                        ineligiblePhysicalCandidateCount++;
                                        continue;
                                    }

                                    if (!isEligible)
                                    {
                                        ineligiblePhysicalCandidateCount++;
                                        continue;
                                    }
                                }

                                Vector3 candidatePosition = collider.bounds.center;
                                if (!IsFinite(candidatePosition))
                                {
                                    status = PerceptionAcquisitionStatus.Partial;
                                    groupDiagnostic = AppendDiagnostic(
                                        groupDiagnostic,
                                        "A discovered collider has no finite bounds center and was omitted.");
                                    continue;
                                }

                                if (!discovered.TryGetValue(collider, out DiscoveredCollider candidate))
                                {
                                    candidate = new DiscoveredCollider(collider, candidatePosition);
                                    discovered.Add(collider, candidate);
                                }

                                for (int sensorIndex = 0; sensorIndex < contributingSensors.Count; sensorIndex++)
                                {
                                    PerceptionSensorInstance sensorInstance = contributingSensors[sensorIndex];
                                    if (!candidate.Sensors.Contains(sensorInstance))
                                    {
                                        candidate.Sensors.Add(sensorInstance);
                                    }
                                }
                            }
                        }

                        if (ineligiblePhysicalCandidateCount > 0)
                        {
                            groupDiagnostic = AppendDiagnostic(
                                groupDiagnostic,
                                $"{ineligiblePhysicalCandidateCount} physical colliders were omitted by the candidate eligibility policy.");
                        }

                        AddSourcesForGroup(
                            group,
                            sourceResults,
                            status,
                            acquisitionTimeSeconds,
                            groupDiagnostic,
                            excludedObserverColliderCounts);
                    }

                    foreach (KeyValuePair<Collider, DiscoveredCollider> pair in discovered)
                    {
                        DiscoveredCollider discoveredCollider = pair.Value;
                        if (discoveredCollider.Collider == null)
                        {
                            continue;
                        }

                        var evidence = new List<PerceptionCandidateEvidence>(
                            discoveredCollider.Sensors.Count + 1);
                        for (int sensorIndex = 0; sensorIndex < discoveredCollider.Sensors.Count; sensorIndex++)
                        {
                            evidence.Add(PerceptionCandidateEvidence.ForPhysicalCollider(
                                discoveredCollider.Collider,
                                PerceptionCandidateOrigin.Physical,
                                acquisitionTimeSeconds,
                                discoveredCollider.Sensors[sensorIndex]));
                        }

                        evidence.Add(PerceptionCandidateEvidence.ForPosition(
                            discoveredCollider.Position,
                            PerceptionCandidateOrigin.Physical,
                            acquisitionTimeSeconds,
                            discoveredCollider.Collider));
                        candidates.Add(new PerceptionCandidate(evidence, discoveredCollider.Collider));
                    }
                }
            }

            acquisition = new PerceptionCandidateAcquisition(sourceResults, candidates);
            diagnostic = string.Empty;
            return true;
        }

        private static SpatialQueryGroup FindQueryGroup(
            IReadOnlyList<SpatialQueryGroup> groups,
            PerceptionSpatialDiscoveryVolume volume)
        {
            for (int index = 0; index < groups.Count; index++)
            {
                if (groups[index].Volume.HasSameGeometry(volume))
                {
                    return groups[index];
                }
            }

            return null;
        }

        private bool TryGetObserverOccurrenceRoot(
            ActorOccurrenceResolution? resolution,
            out GameObject actorRoot,
            out string diagnostic)
        {
            actorRoot = null;
            if (!resolution.HasValue || !resolution.Value.Succeeded)
            {
                diagnostic = resolution.HasValue && !string.IsNullOrWhiteSpace(resolution.Value.Diagnostic)
                    ? resolution.Value.Diagnostic
                    : "Current Framework Actor occurrence resolution is unavailable.";
                return false;
            }

            ActorOccurrence occurrence = resolution.Value.Occurrence;
            actorRoot = occurrence.ActorRoot;
            if (!occurrence.IsValid || actorRoot == null)
            {
                actorRoot = null;
                diagnostic = "Framework Actor occurrence does not expose a valid physical root.";
                return false;
            }

            if (actorRoot != gameObject || GetComponent<ActorDeclaration>() == null)
            {
                actorRoot = null;
                diagnostic = "Perception spatial authority must share the Framework Actor occurrence root and its ActorDeclaration.";
                return false;
            }

            if (Observer == null ||
                Observer != actorRoot.transform)
            {
                actorRoot = null;
                diagnostic = "Perception observer is outside the resolved Framework Actor occurrence root.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static void AddUnavailableSources(
            IReadOnlyList<SpatialQueryGroup> groups,
            ICollection<PerceptionCandidateSourceResult> sourceResults,
            double acquisitionTimeSeconds,
            string diagnostic)
        {
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                AddSourcesForGroup(
                    groups[groupIndex],
                    sourceResults,
                    PerceptionAcquisitionStatus.Unavailable,
                    acquisitionTimeSeconds,
                    diagnostic);
            }
        }

        private static void AddSourcesForGroup(
            SpatialQueryGroup group,
            ICollection<PerceptionCandidateSourceResult> sourceResults,
            PerceptionAcquisitionStatus status,
            double acquisitionTimeSeconds,
            string diagnostic,
            IReadOnlyDictionary<PerceptionSensorInstance, int> excludedObserverColliderCounts = null)
        {
            for (int index = 0; index < group.Contributions.Count; index++)
            {
                SpatialSensorContribution contribution = group.Contributions[index];
                int excludedObserverColliderCount = 0;
                if (excludedObserverColliderCounts != null)
                {
                    excludedObserverColliderCounts.TryGetValue(
                        contribution.SensorInstance,
                        out excludedObserverColliderCount);
                }

                sourceResults.Add(new PerceptionCandidateSourceResult(
                    PerceptionCandidateOrigin.Physical,
                    status,
                    acquisitionTimeSeconds,
                    contribution.SensorInstance,
                    contribution.Volume,
                    diagnostic,
                    excludedObserverColliderCount: excludedObserverColliderCount));
            }
        }

        private static bool TryGetContributingSensors(
            SpatialQueryGroup group,
            Collider collider,
            out List<PerceptionSensorInstance> contributingSensors)
        {
            contributingSensors = null;
            int layerBit = 1 << collider.gameObject.layer;
            bool isTrigger = collider.isTrigger;
            for (int index = 0; index < group.Contributions.Count; index++)
            {
                SpatialSensorContribution contribution = group.Contributions[index];
                if ((contribution.Volume.CandidateLayerMask & layerBit) == 0 ||
                    !MatchesTriggerPolicy(isTrigger, contribution.Volume.TriggerInteraction))
                {
                    continue;
                }

                if (contributingSensors == null)
                {
                    contributingSensors = new List<PerceptionSensorInstance>();
                }

                contributingSensors.Add(contribution.SensorInstance);
            }

            return contributingSensors != null;
        }

        private static bool MatchesTriggerPolicy(
            bool isTrigger,
            QueryTriggerInteraction triggerInteraction)
        {
            if (!isTrigger || triggerInteraction == QueryTriggerInteraction.Collide)
            {
                return true;
            }

            if (triggerInteraction == QueryTriggerInteraction.Ignore)
            {
                return false;
            }

            return Physics.queriesHitTriggers;
        }

        private static bool BelongsToOccurrence(Collider collider, Transform actorRoot)
        {
            Transform colliderTransform = collider.transform;
            return colliderTransform == actorRoot || colliderTransform.IsChildOf(actorRoot);
        }

        private bool TryRunSpatialQuery(
            PerceptionSpatialDiscoveryVolume volume,
            int candidateLayerMask,
            out int hitCount,
            out bool complete,
            out string diagnostic)
        {
            if (_spatialQueryBuffer == null)
            {
                _spatialQueryBuffer = new Collider[InitialSpatialQueryBufferSize];
            }

            while (true)
            {
                hitCount = volume.Shape switch
                {
                    PerceptionSpatialVolumeShape.Sphere => Physics.OverlapSphereNonAlloc(
                        volume.Center,
                        volume.RadiusMeters,
                        _spatialQueryBuffer,
                        candidateLayerMask,
                        QueryTriggerInteraction.Collide),
                    PerceptionSpatialVolumeShape.Box => Physics.OverlapBoxNonAlloc(
                        volume.Center,
                        volume.BoxSizeMeters * 0.5f,
                        _spatialQueryBuffer,
                        volume.Rotation,
                        candidateLayerMask,
                        QueryTriggerInteraction.Collide),
                    PerceptionSpatialVolumeShape.Capsule => Physics.OverlapCapsuleNonAlloc(
                        GetCapsulePoint(volume, -1f),
                        GetCapsulePoint(volume, 1f),
                        volume.RadiusMeters,
                        _spatialQueryBuffer,
                        candidateLayerMask,
                        QueryTriggerInteraction.Collide),
                    _ => throw new InvalidOperationException("Unsupported spatial discovery volume shape.")
                };

                if (hitCount < _spatialQueryBuffer.Length)
                {
                    complete = true;
                    diagnostic = string.Empty;
                    return true;
                }

                if (_spatialQueryBuffer.Length >= MaximumSpatialQueryBufferSize)
                {
                    complete = false;
                    diagnostic = $"Spatial discovery saturated its {MaximumSpatialQueryBufferSize}-collider result buffer; returned candidates are partial.";
                    return true;
                }

                int nextCapacity = Math.Min(
                    _spatialQueryBuffer.Length * 2,
                    MaximumSpatialQueryBufferSize);
                Array.Resize(ref _spatialQueryBuffer, nextCapacity);
            }
        }

        private static Vector3 GetCapsulePoint(
            PerceptionSpatialDiscoveryVolume volume,
            float direction)
        {
            float centerOffsetMeters =
                (volume.CapsuleHeightMeters * 0.5f - volume.RadiusMeters) * direction;
            return volume.Center + volume.Rotation * (Vector3.up * centerOffsetMeters);
        }

        private void RefreshSensorInstances()
        {
            _composedSensorInstances.Clear();
            GetComponents(_composedSensorInstances);
            _sensorInstances.Clear();
            _retainedSensorInstances.Clear();
            for (int index = 0; index < _composedSensorInstances.Count; index++)
            {
                PerceptionSensorInstance sensorInstance = _composedSensorInstances[index];
                if (sensorInstance == null)
                {
                    continue;
                }

                if (sensorInstance.isActiveAndEnabled)
                {
                    _sensorInstances.Add(sensorInstance);
                }

                if (sensorInstance.enabled && sensorInstance.gameObject.activeInHierarchy)
                {
                    // Preserva a cadência enquanto o OnEnable da instância está pendente.
                    _retainedSensorInstances.Add(sensorInstance);
                }
            }

            _staleSensorInstances.Clear();
            foreach (KeyValuePair<PerceptionSensorInstance, SamplingState> entry in _samplingStates)
            {
                if (entry.Key == null || !_retainedSensorInstances.Contains(entry.Key))
                {
                    _staleSensorInstances.Add(entry.Key);
                }
            }

            for (int index = 0; index < _staleSensorInstances.Count; index++)
            {
                _samplingStates.Remove(_staleSensorInstances[index]);
            }
        }

        private void SelectDueSensorInstances(double sampleTimeSeconds, bool force)
        {
            _dueSensorInstances.Clear();
            for (int index = 0; index < _sensorInstances.Count; index++)
            {
                PerceptionSensorInstance sensorInstance = _sensorInstances[index];
                PerceptionSensorDefinition definition = sensorInstance != null
                    ? sensorInstance.Definition
                    : null;
                float samplingIntervalSeconds = definition != null
                    ? definition.SamplingIntervalSeconds
                    : 0f;
                bool hasState = _samplingStates.TryGetValue(sensorInstance, out SamplingState state);
                if (force ||
                    !hasState ||
                    !HasSameSamplingConfiguration(state, definition, samplingIntervalSeconds) ||
                    (state.IsSchedulable && sampleTimeSeconds >= state.NextSampleTimeSeconds))
                {
                    _dueSensorInstances.Add(sensorInstance);
                }
            }
        }

        private void AdvanceSamplingSchedule(double sampleTimeSeconds)
        {
            for (int index = 0; index < _dueSensorInstances.Count; index++)
            {
                PerceptionSensorInstance sensorInstance = _dueSensorInstances[index];
                PerceptionSensorDefinition definition = sensorInstance != null
                    ? sensorInstance.Definition
                    : null;
                float samplingIntervalSeconds = definition != null
                    ? definition.SamplingIntervalSeconds
                    : 0f;
                bool isSchedulable = definition != null && IsPositiveFinite(samplingIntervalSeconds);
                double nextSampleTimeSeconds = isSchedulable
                    ? sampleTimeSeconds + samplingIntervalSeconds
                    : double.PositiveInfinity;
                isSchedulable &= !double.IsNaN(nextSampleTimeSeconds) &&
                    !double.IsInfinity(nextSampleTimeSeconds);
                _samplingStates[sensorInstance] = new SamplingState(
                    definition,
                    samplingIntervalSeconds,
                    nextSampleTimeSeconds,
                    isSchedulable);
            }
        }

        private static bool HasSameSamplingConfiguration(
            SamplingState state,
            PerceptionSensorDefinition definition,
            float samplingIntervalSeconds) =>
            ReferenceEquals(state.Definition, definition) &&
            state.SamplingIntervalSeconds.Equals(samplingIntervalSeconds);

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static string AppendDiagnostic(string current, string addition) =>
            string.IsNullOrWhiteSpace(current)
                ? addition
                : current + " " + addition;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private List<PerceptionCandidateSensorResult> EnsureLatestCandidateResults() =>
            _latestCandidateResults ?? (_latestCandidateResults = new List<PerceptionCandidateSensorResult>());

        private void ClearLatestCandidateResults()
        {
            _latestCandidateResults?.Clear();
        }
    }
}

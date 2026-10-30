using System;
using System.Collections.Generic;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.World.PointsOfInterest
{
    public enum PEGAPointOfInterestResolutionStatus
    {
        InvalidIdentity = 0,
        WorldContextInactive = 10,
        Unavailable = 20,
        Resolved = 30
    }

    internal enum PEGAPointOfInterestRegistrationStatus
    {
        Invalid = 0,
        Registered = 10,
        // A concorrente fica aguardando, mas não é a manifestação resolvida do POI.
        Waiting = 20
    }

    /// <summary>Escopo explícito que resolve identidades POI para manifestações físicas atuais.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/World/Point of Interest Context")]
    public sealed class PEGAPointOfInterestContext : MonoBehaviour
    {
        private const string LogCategory = "PEGA.World.PointsOfInterest";

        [NonSerialized]
        private Dictionary<PEGAPointOfInterestIdentity, PEGAPointOfInterest> _currentManifestations =
            new Dictionary<PEGAPointOfInterestIdentity, PEGAPointOfInterest>();

        [NonSerialized]
        private Dictionary<PEGAPointOfInterestIdentity, List<PEGAPointOfInterest>> _waitingManifestations =
            new Dictionary<PEGAPointOfInterestIdentity, List<PEGAPointOfInterest>>();

        [NonSerialized]
        private ScopedLogger _log;

        /// <summary>Resolve a única manifestação ativa dessa identidade neste contexto de mundo.</summary>
        public PEGAPointOfInterestResolutionStatus ResolveCurrent(
            PEGAPointOfInterestIdentity identity,
            out PEGAPointOfInterest manifestation)
        {
            manifestation = null;
            if (identity == null)
            {
                return PEGAPointOfInterestResolutionStatus.InvalidIdentity;
            }

            if (!isActiveAndEnabled)
            {
                return PEGAPointOfInterestResolutionStatus.WorldContextInactive;
            }

            EnsureRegistryInitialized();
            manifestation = FindCurrentOrPromote(identity);
            if (manifestation == null)
            {
                return PEGAPointOfInterestResolutionStatus.Unavailable;
            }

            return PEGAPointOfInterestResolutionStatus.Resolved;
        }

        internal PEGAPointOfInterestRegistrationStatus Register(
            PEGAPointOfInterest manifestation,
            PEGAPointOfInterestIdentity identity,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!isActiveAndEnabled)
            {
                diagnostic = "The POI World Context must be active and enabled before a manifestation can register.";
                return PEGAPointOfInterestRegistrationStatus.Invalid;
            }

            if (manifestation == null || !manifestation.isActiveAndEnabled || identity == null ||
                manifestation.Identity != identity || manifestation.WorldContext != this)
            {
                diagnostic = "POI registration requires an active manifestation, a valid identity and this World Context.";
                return PEGAPointOfInterestRegistrationStatus.Invalid;
            }

            EnsureRegistryInitialized();
            PEGAPointOfInterest current = FindCurrentOrPromote(identity);
            if (current == manifestation)
            {
                return PEGAPointOfInterestRegistrationStatus.Registered;
            }

            if (current != null)
            {
                diagnostic = "Another active manifestation already occupies this POI identity in the World Context.";
                LogDuplicateIdentity(identity, current, manifestation);
                AddWaitingManifestation(identity, manifestation);
                return PEGAPointOfInterestRegistrationStatus.Waiting;
            }

            _currentManifestations.Add(identity, manifestation);
            return PEGAPointOfInterestRegistrationStatus.Registered;
        }

        internal void Unregister(
            PEGAPointOfInterest manifestation,
            PEGAPointOfInterestIdentity identity)
        {
            if (manifestation == null || identity == null || _currentManifestations == null)
            {
                return;
            }

            bool releasedCurrent =
                _currentManifestations.TryGetValue(identity, out PEGAPointOfInterest current) &&
                current == manifestation;

            if (releasedCurrent)
            {
                _currentManifestations.Remove(identity);
            }

            RemoveWaitingManifestation(identity, manifestation);
            if (releasedCurrent)
            {
                PromoteNext(identity);
            }
        }

        private void OnDestroy()
        {
            _currentManifestations?.Clear();
            _currentManifestations = null;
            _waitingManifestations?.Clear();
            _waitingManifestations = null;
        }

        private void EnsureRegistryInitialized()
        {
            _currentManifestations ??= new Dictionary<PEGAPointOfInterestIdentity, PEGAPointOfInterest>();
            _waitingManifestations ??=
                new Dictionary<PEGAPointOfInterestIdentity, List<PEGAPointOfInterest>>();
        }

        private PEGAPointOfInterest FindCurrentOrPromote(PEGAPointOfInterestIdentity identity)
        {
            if (_currentManifestations.TryGetValue(identity, out PEGAPointOfInterest current))
            {
                if (IsValidActiveManifestation(current, identity))
                {
                    return current;
                }

                _currentManifestations.Remove(identity);
            }

            return PromoteNext(identity);
        }

        private PEGAPointOfInterest PromoteNext(PEGAPointOfInterestIdentity identity)
        {
            if (!_waitingManifestations.TryGetValue(identity, out List<PEGAPointOfInterest> waiting))
            {
                return null;
            }

            while (waiting.Count > 0)
            {
                PEGAPointOfInterest candidate = waiting[0];
                waiting.RemoveAt(0);
                if (!IsValidActiveManifestation(candidate, identity))
                    continue;

                _currentManifestations[identity] = candidate;
                if (waiting.Count == 0)
                    _waitingManifestations.Remove(identity);
                return candidate;
            }

            _waitingManifestations.Remove(identity);
            return null;
        }

        private void AddWaitingManifestation(
            PEGAPointOfInterestIdentity identity,
            PEGAPointOfInterest manifestation)
        {
            if (!_waitingManifestations.TryGetValue(identity, out List<PEGAPointOfInterest> waiting))
            {
                waiting = new List<PEGAPointOfInterest>();
                _waitingManifestations.Add(identity, waiting);
            }

            for (int index = 0; index < waiting.Count; index++)
            {
                if (waiting[index] == manifestation)
                    return;
            }

            waiting.Add(manifestation);
        }

        private void RemoveWaitingManifestation(
            PEGAPointOfInterestIdentity identity,
            PEGAPointOfInterest manifestation)
        {
            if (!_waitingManifestations.TryGetValue(identity, out List<PEGAPointOfInterest> waiting))
            {
                return;
            }

            waiting.Remove(manifestation);
            if (waiting.Count == 0)
            {
                _waitingManifestations.Remove(identity);
            }
        }

        private bool IsValidActiveManifestation(
            PEGAPointOfInterest manifestation,
            PEGAPointOfInterestIdentity identity)
        {
            return manifestation != null && manifestation.isActiveAndEnabled &&
                   manifestation.Identity == identity && manifestation.WorldContext == this;
        }

        private void LogDuplicateIdentity(
            PEGAPointOfInterestIdentity identity,
            PEGAPointOfInterest first,
            PEGAPointOfInterest duplicate)
        {
            _log ??= PEGALogging.CreateScopedLogger<PEGAPointOfInterestContext>(LogCategory, name);
            _log.Error(
                "A second active POI manifestation attempted to occupy an identity already registered in this World Context; it is waiting for that identity to be released.",
                LogFields.Field("poiIdentity", identity.name),
                LogFields.Field("worldContext", name),
                LogFields.Field("firstManifestation", first != null ? first.gameObject.name : string.Empty),
                LogFields.Field("duplicateManifestation", duplicate != null ? duplicate.gameObject.name : string.Empty));
        }
    }
}

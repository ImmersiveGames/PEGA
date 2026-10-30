using System;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.World.PointsOfInterest
{
    /// <summary>Manifestação física atual de uma identidade POI em um contexto de mundo.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/World/Point of Interest")]
    public sealed class PEGAPointOfInterest : MonoBehaviour
    {
        private const string LogCategory = "PEGA.World.PointsOfInterest";

        [Header("Point of Interest")]
        [SerializeField]
        [Tooltip("Asset de identidade deste objeto individual. Crie um asset por objeto; não reutilize como perfil.")]
        private PEGAPointOfInterestIdentity identity;

        [SerializeField]
        [Tooltip("Contexto do mundo que resolve a manifestação física atual desta identidade.")]
        private PEGAPointOfInterestContext worldContext;

        [NonSerialized]
        private PEGAPointOfInterestIdentity _registeredIdentity;

        [NonSerialized]
        private PEGAPointOfInterestContext _registeredContext;

        [NonSerialized]
        private bool _isTrackedInContext;

        [NonSerialized]
        private ScopedLogger _log;

        public PEGAPointOfInterestIdentity Identity => identity;

        public PEGAPointOfInterestContext WorldContext => worldContext;

        /// <summary>Transform usado como alvo espacial; acompanha a movimentação desta manifestação.</summary>
        public Transform TargetTransform => transform;

        /// <summary>Configura uma manifestação recriada antes que ela seja ativada.</summary>
        public bool TryConfigure(
            PEGAPointOfInterestIdentity nextIdentity,
            PEGAPointOfInterestContext nextWorldContext,
            out string diagnostic)
        {
            if (isActiveAndEnabled || _isTrackedInContext)
            {
                diagnostic = "A POI manifestation must be inactive before its identity or World Context can be configured.";
                return false;
            }

            if (nextIdentity == null)
            {
                diagnostic = "A persistent POI identity asset is required.";
                return false;
            }

            if (nextWorldContext == null)
            {
                diagnostic = "An explicit POI World Context is required.";
                return false;
            }

            identity = nextIdentity;
            worldContext = nextWorldContext;
            diagnostic = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            if (identity == null)
            {
                FailConfiguration("A Point of Interest requires a persistent identity asset.");
                return;
            }

            if (worldContext == null)
            {
                FailConfiguration("A Point of Interest requires an explicit World Context.");
                return;
            }

            PEGAPointOfInterestRegistrationStatus status =
                worldContext.Register(this, identity, out string diagnostic);
            if (status == PEGAPointOfInterestRegistrationStatus.Invalid)
            {
                FailConfiguration(diagnostic);
                return;
            }

            _registeredIdentity = identity;
            _registeredContext = worldContext;
            _isTrackedInContext = true;
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        private void FailConfiguration(string diagnostic)
        {
            _log ??= PEGALogging.CreateScopedLogger<PEGAPointOfInterest>(LogCategory, gameObject.name);
            _log.Error(
                "Point of Interest could not register its physical manifestation.",
                LogFields.Field("gameObject", gameObject.name),
                LogFields.Field("poiIdentity", identity != null ? identity.name : string.Empty),
                LogFields.Field("worldContext", worldContext != null ? worldContext.name : string.Empty),
                LogFields.Field("diagnostic", diagnostic));
            enabled = false;
        }

        private void Unregister()
        {
            if (!_isTrackedInContext)
            {
                return;
            }

            if (_registeredContext != null)
            {
                _registeredContext.Unregister(this, _registeredIdentity);
            }

            _registeredIdentity = null;
            _registeredContext = null;
            _isTrackedInContext = false;
        }
    }
}

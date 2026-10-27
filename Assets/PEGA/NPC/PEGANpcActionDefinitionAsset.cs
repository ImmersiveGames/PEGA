using UnityEngine;
using UnityEngine.AI;
using Immersive.Framework.Actors;

namespace PEGA.NPC
{
    /// <summary>
    /// Shared, authored Action definition. PEGANpcAutonomy creates one hidden runtime clone per Actor;
    /// mutable execution state therefore never lives in the shared project asset.
    /// </summary>
    public abstract class PEGANpcActionDefinitionAsset : ScriptableObject
    {
        private PEGANpcActionRuntimeContext _runtimeContext;
        private bool _runtimeInitialized;
        private bool _runtimeEnabled;

        protected PEGANpcActionRuntimeContext RuntimeContext => _runtimeContext;
        protected GameObject ActorRoot => _runtimeContext != null ? _runtimeContext.ActorRoot : null;
        protected Transform ActorTransform => _runtimeContext != null ? _runtimeContext.ActorTransform : null;
        protected bool IsRuntimeActive =>
            _runtimeEnabled &&
            _runtimeContext != null &&
            _runtimeContext.Autonomy != null &&
            _runtimeContext.Autonomy.isActiveAndEnabled &&
            _runtimeContext.ActorRoot != null &&
            _runtimeContext.ActorRoot.activeInHierarchy;

        internal bool InitializeRuntime(PEGANpcActionRuntimeContext context, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_runtimeInitialized || context == null || context.ActorRoot == null)
            {
                diagnostic = "Action runtime requires a fresh instance and a valid Actor context.";
                return false;
            }

            _runtimeContext = context;
            if (!OnRuntimeInitialize(out diagnostic))
            {
                _runtimeContext = null;
                return false;
            }

            _runtimeInitialized = true;
            diagnostic = string.Empty;
            return true;
        }

        internal void EnableRuntime()
        {
            if (!_runtimeInitialized || _runtimeEnabled)
            {
                return;
            }

            _runtimeEnabled = true;
            OnRuntimeEnabled();
        }

        internal void TickRuntime()
        {
            if (_runtimeInitialized && IsRuntimeActive)
            {
                OnRuntimeTick();
            }
        }

        internal void DisableRuntime()
        {
            if (!_runtimeInitialized || !_runtimeEnabled)
            {
                return;
            }

            OnRuntimeDisabled();
            _runtimeEnabled = false;
        }

        internal void DisposeRuntime()
        {
            if (!_runtimeInitialized)
            {
                return;
            }

            DisableRuntime();
            OnRuntimeDestroyed();
            _runtimeContext = null;
            _runtimeInitialized = false;
        }

        protected virtual bool OnRuntimeInitialize(out string diagnostic)
        {
            diagnostic = string.Empty;
            return true;
        }

        protected virtual void OnRuntimeEnabled()
        {
        }

        protected virtual void OnRuntimeTick()
        {
        }

        protected virtual void OnRuntimeDisabled()
        {
        }

        protected virtual void OnRuntimeDestroyed()
        {
        }
    }

    /// <summary>Actor-local services explicitly supplied to each Action runtime clone.</summary>
    public sealed class PEGANpcActionRuntimeContext
    {
        internal PEGANpcActionRuntimeContext(
            PEGANpcAutonomy autonomy,
            PEGANpcKnowledgeState knowledgeState,
            PEGANpcNavMeshMovementIntentSource navigationSource,
            ActorOccurrenceReferenceBinding actorReference,
            NavMeshAgent navigationAgent,
            PEGANpcGangMembership gangMembership,
            IPEGANpcLastKnownTargetContinuationProvider lastKnownTargetContinuationProvider)
        {
            Autonomy = autonomy;
            ActorRoot = autonomy != null ? autonomy.gameObject : null;
            ActorTransform = autonomy != null ? autonomy.transform : null;
            KnowledgeState = knowledgeState;
            NavigationSource = navigationSource;
            ActorReference = actorReference;
            NavigationAgent = navigationAgent;
            GangMembership = gangMembership;
            LastKnownTargetContinuationProvider = lastKnownTargetContinuationProvider;
        }

        public PEGANpcAutonomy Autonomy { get; }
        public GameObject ActorRoot { get; }
        public Transform ActorTransform { get; }
        public PEGANpcKnowledgeState KnowledgeState { get; }
        public PEGANpcNavMeshMovementIntentSource NavigationSource { get; }
        public ActorOccurrenceReferenceBinding ActorReference { get; }
        public NavMeshAgent NavigationAgent { get; }
        public PEGANpcGangMembership GangMembership { get; }
        public IPEGANpcLastKnownTargetContinuationProvider LastKnownTargetContinuationProvider { get; }
    }
}

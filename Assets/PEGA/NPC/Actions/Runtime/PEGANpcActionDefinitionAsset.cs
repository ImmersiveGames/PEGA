using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.Movement.NPC;
using PEGA.NPC.Autonomy;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
using UnityEngine.AI;
namespace PEGA.NPC.Actions.Runtime
{
    /// <summary>
    /// Shared, authored Action definition. PEGANpcAutonomy creates one hidden runtime clone per Actor;
    /// mutable execution state therefore never lives in the shared project asset.
    /// </summary>
    public abstract class PegaNpcActionDefinitionAsset : ScriptableObject
    {
        private PegaNpcActionRuntimeContext _runtimeContext;
        private bool _runtimeInitialized;
        private bool _runtimeEnabled;

        protected PegaNpcActionRuntimeContext RuntimeContext => _runtimeContext;
        protected GameObject ActorRoot => _runtimeContext != null ? _runtimeContext.ActorRoot : null;
        protected Transform ActorTransform => _runtimeContext != null ? _runtimeContext.ActorTransform : null;
        protected bool IsRuntimeActive =>
            _runtimeEnabled &&
            _runtimeContext != null &&
            _runtimeContext.Autonomy != null &&
            _runtimeContext.Autonomy.isActiveAndEnabled &&
            _runtimeContext.ActorRoot != null &&
            _runtimeContext.ActorRoot.activeInHierarchy;

        internal bool InitializeRuntime(PegaNpcActionRuntimeContext context, out string diagnostic)
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
    public sealed class PegaNpcActionRuntimeContext
    {
        internal PegaNpcActionRuntimeContext(
            PegaNpcAutonomy autonomy,
            PegaNpcKnowledgeState knowledgeState,
            PegaNpcKnowledgeReadView knowledgeReadView,
            PegaNpcNavMeshMovementIntentSource navigationSource,
            ActorOccurrenceReferenceBinding actorReference,
            NavMeshAgent navigationAgent,
            IPegaNpcLastKnownTargetContinuationProvider lastKnownTargetContinuationProvider,
            ObjectEntryOccurrenceQueryBinding objectEntryOccurrenceQueryBinding)
        {
            Autonomy = autonomy;
            ActorRoot = autonomy != null ? autonomy.gameObject : null;
            ActorTransform = autonomy != null ? autonomy.transform : null;
            KnowledgeState = knowledgeState;
            KnowledgeReadView = knowledgeReadView ??
                throw new System.ArgumentNullException(nameof(knowledgeReadView));
            NavigationSource = navigationSource;
            ActorReference = actorReference;
            NavigationAgent = navigationAgent;
            LastKnownTargetContinuationProvider = lastKnownTargetContinuationProvider;
            ObjectEntryOccurrenceQueryBinding = objectEntryOccurrenceQueryBinding;
        }

        public PegaNpcAutonomy Autonomy { get; }
        public GameObject ActorRoot { get; }
        public Transform ActorTransform { get; }
        public PegaNpcKnowledgeState KnowledgeState { get; }
        public PegaNpcKnowledgeReadView KnowledgeReadView { get; }
        public PegaNpcNavMeshMovementIntentSource NavigationSource { get; }
        public ActorOccurrenceReferenceBinding ActorReference { get; }
        public NavMeshAgent NavigationAgent { get; }
        public IPegaNpcLastKnownTargetContinuationProvider LastKnownTargetContinuationProvider { get; }
        public ObjectEntryOccurrenceQueryBinding ObjectEntryOccurrenceQueryBinding { get; }
    }
}

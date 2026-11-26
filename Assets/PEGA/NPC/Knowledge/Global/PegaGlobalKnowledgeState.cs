using System;
using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.Actors;
using Immersive.Framework.RouteLifecycle;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.NPC.Knowledge.Authoring;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
namespace PEGA.NPC.Knowledge.Global
{
    /// <summary>
    /// Global Knowledge owned by the Route or Activity contribution on this content root.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/Knowledge/Global Knowledge State")]
    public sealed class PegaGlobalKnowledgeState : MonoBehaviour,
        IPegaKnowledgeFactReader,
        IPegaKnowledgeFactWriter,
        IActivityContentLifecycleReceiver,
        IRouteContentLifecycleReceiver
    {
        private const string LogCategory = "PEGA.Gameplay.Knowledge";

        [SerializeField, Tooltip("Fatos iniciais publicados na entrada da contribuição de Route ou Activity deste objeto.")]
        private List<PegaKnowledgeFactAuthoringEntry> initialKnowledge =
            new List<PegaKnowledgeFactAuthoringEntry>();

        [NonSerialized]
        private PegaKnowledgeFactStore _factStore;

        [NonSerialized]
        private Component _activeContribution;

        [NonSerialized]
        private UnityEngine.Object _activeOwner;

        [NonSerialized]
        private PegaKnowledgeFactAuthoringEntry[] _initialKnowledgeSnapshot;

        [NonSerialized]
        private ActorOccurrenceReferenceBinding _actorBinding;

        [NonSerialized]
        private double _initialKnowledgeObservationTimeSeconds;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        public PegaKnowledgeScope Scope => PegaKnowledgeScope.Global;

        public bool IsAvailable => _factStore != null && _activeOwner != null && _activeContribution != null;

        public bool IsOwnedByRoute(RouteContentContribution contribution) =>
            IsAvailable && contribution != null && _activeContribution == contribution &&
            ReferenceEquals(_activeOwner, contribution.Route);

        void IActivityContentLifecycleReceiver.OnActivityContentEntered(ActivityContentLifecycleContext context)
        {
            if (!IsOwnContribution(context.ContentRoot, context.Contribution))
            {
                return;
            }

            EnsureLoggerInitialized();
            if (context.Activity == null || !context.Activity.HasValidActivityId ||
                context.Contribution.Activity != context.Activity)
            {
                _log.Error(
                    "Global Knowledge requires its exact, valid Activity content owner.",
                    LogFields.Field("contentRoot", gameObject.name));
                return;
            }

            EnterOwner(context.Activity, context.Contribution, context.ActivityName);
        }

        void IRouteContentLifecycleReceiver.OnRouteContentEntered(RouteContentLifecycleContext context)
        {
            if (!IsOwnContribution(context.ContentRoot, context.Binding))
            {
                return;
            }

            EnsureLoggerInitialized();
            if (context.Route == null || !context.Route.HasValidRouteId || context.Binding.Route != context.Route)
            {
                _log.Error("Global Knowledge requires its exact, valid Route content owner.",
                    LogFields.Field("contentRoot", gameObject.name));
                return;
            }

            EnterOwner(context.Route, context.Binding, context.RouteName);
        }

        private void EnterOwner(UnityEngine.Object owner, Component contribution, string ownerName)
        {
            if (GetComponent<ActivityContentContribution>() != null && GetComponent<RouteContentContribution>() != null)
            {
                _log.Error("Global Knowledge requires a single Route or Activity contribution on its content root.",
                    LogFields.Field("contentRoot", gameObject.name));
                return;
            }

            if (IsAvailable && ReferenceEquals(_activeOwner, owner) && _activeContribution == contribution)
            {
                return;
            }

            ClearStore();
            _factStore = new PegaKnowledgeFactStore(PegaKnowledgeScope.Global);
            _activeContribution = contribution;
            _activeOwner = owner;
            _initialKnowledgeSnapshot = initialKnowledge != null
                ? initialKnowledge.ToArray()
                : Array.Empty<PegaKnowledgeFactAuthoringEntry>();
            _actorBinding = GetComponent<ActorOccurrenceReferenceBinding>();
            _initialKnowledgeObservationTimeSeconds = Time.timeAsDouble;
            _log.Info(
                "Global Knowledge became available.",
                LogFields.Field("owner", ownerName),
                LogFields.Field("contentRoot", gameObject.name));
            ApplyInitialKnowledge();
        }

        void IActivityContentLifecycleReceiver.OnActivityContentExited(ActivityContentLifecycleContext context)
        {
            ExitOwner(context.ContentRoot, context.Contribution, context.Activity, context.ActivityName);
        }

        void IRouteContentLifecycleReceiver.OnRouteContentExited(RouteContentLifecycleContext context)
        {
            ExitOwner(context.ContentRoot, context.Binding, context.Route, context.RouteName);
        }

        private void ExitOwner(GameObject root, Component contribution, UnityEngine.Object owner, string ownerName)
        {
            if (root != gameObject || !IsAvailable || _activeContribution != contribution ||
                !ReferenceEquals(_activeOwner, owner))
            {
                return;
            }

            EnsureLoggerInitialized();
            _log.Info(
                "Global Knowledge was cleared.",
                LogFields.Field("owner", ownerName),
                LogFields.Field("contentRoot", gameObject.name));
            ClearStore();
        }

        public PegaKnowledgeFact<TValue> ReadFact<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds)
        {
            ValidateQuery(key, queryTimeSeconds);
            return IsAvailable
                ? _factStore.Get(key, queryTimeSeconds)
                : PegaKnowledgeFact<TValue>.Unknown(PegaKnowledgeScope.Global);
        }

        public PegaKnowledgeWriteResult ObserveFact<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PegaKnowledgeEvidence evidence)
        {
            PegaKnowledgeFactStore store = RequireActiveStore();
            if (!key.IsValid || !evidence.IsValid)
            {
                return PegaKnowledgeWriteResult.RejectedInvalid;
            }

            PegaKnowledgeFact<TValue> previous = store.Get(key, evidence.ObservationTimeSeconds);
            PegaKnowledgeWriteResult result = store.Observe(key, value, evidence);
            PegaKnowledgeFact<TValue> current = store.Get(key, evidence.ObservationTimeSeconds);
            LogWriteResult(key, result, evidence, previous, current);
            return result;
        }

        public PegaKnowledgeWriteResult InvalidateFact<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            PegaKnowledgeEvidence evidence)
        {
            PegaKnowledgeFactStore store = RequireActiveStore();
            if (!key.IsValid || !evidence.IsValid)
            {
                return PegaKnowledgeWriteResult.RejectedInvalid;
            }

            PegaKnowledgeFact<TValue> previous = store.Get(key, evidence.ObservationTimeSeconds);
            PegaKnowledgeWriteResult result = store.Invalidate(key, evidence);
            PegaKnowledgeFact<TValue> current = store.Get(key, evidence.ObservationTimeSeconds);
            LogWriteResult(key, result, evidence, previous, current);
            return result;
        }

        private void OnDestroy()
        {
            ClearStore();
        }

        private bool IsOwnContribution<T>(GameObject root, T contribution) where T : Component =>
            root == gameObject && contribution != null && GetComponent<T>() == contribution;

        private PegaKnowledgeFactStore RequireActiveStore()
        {
            if (!IsAvailable)
            {
                throw new InvalidOperationException(
                    "Global Knowledge can be written only while its content contribution is active.");
            }

            return _factStore;
        }

        private static void ValidateQuery<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds)
        {
            if (!key.IsValid)
            {
                throw new ArgumentException("A valid typed fact key is required.", nameof(key));
            }

            if (double.IsNaN(queryTimeSeconds) || double.IsInfinity(queryTimeSeconds) || queryTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(queryTimeSeconds));
            }
        }

        private void LogWriteResult<TAddress, TValue>(
            PegaKnowledgeFactKey<TAddress, TValue> key,
            PegaKnowledgeWriteResult result,
            PegaKnowledgeEvidence evidence,
            PegaKnowledgeFact<TValue> previous,
            PegaKnowledgeFact<TValue> current)
        {
            EnsureLoggerInitialized();
            LogField[] fields =
            {
                LogFields.Field("fact", key.Name),
                LogFields.Field("factDefinitionId", key.Definition.Id),
                LogFields.Field("factAddress", key.AddressValue),
                LogFields.Field("scope", Scope),
                LogFields.Field("writeResult", result),
                LogFields.Field("origin", evidence.Origin),
                LogFields.Field("observationTimeSeconds", evidence.ObservationTimeSeconds),
                LogFields.Field("previousStatus", previous.Status),
                LogFields.Field("currentStatus", current.Status),
                LogFields.Field("hasValueOrStatusChanged", HasValueOrStatusChanged(previous, current))
            };

            if (result == PegaKnowledgeWriteResult.RejectedInvalid ||
                result == PegaKnowledgeWriteResult.InvalidatedConflict)
            {
                _log.Warning("Global Knowledge fact write completed with a diagnostic result.", fields);
                return;
            }

            _log.Info("Global Knowledge fact write completed.", fields);
        }

        private static bool HasValueOrStatusChanged<TValue>(
            PegaKnowledgeFact<TValue> previous,
            PegaKnowledgeFact<TValue> current)
        {
            bool previousHasValue = previous.TryGetValue(out TValue previousValue);
            bool currentHasValue = current.TryGetValue(out TValue currentValue);
            return previous.Status != current.Status ||
                   previousHasValue != currentHasValue ||
                   previousHasValue && !Equals(previousValue, currentValue);
        }

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PegaLogging.CreateScopedLogger<PegaGlobalKnowledgeState>(LogCategory, gameObject.name);
            _loggerInitialized = true;
        }

        private void ClearStore()
        {
            _factStore?.Clear();
            _factStore = null;
            _activeOwner = null;
            _activeContribution = null;
            _initialKnowledgeSnapshot = null;
            _actorBinding = null;
            _initialKnowledgeObservationTimeSeconds = 0d;
        }

        private void ApplyInitialKnowledge()
        {
            if (_initialKnowledgeSnapshot == null || _initialKnowledgeSnapshot.Length == 0)
            {
                return;
            }

            for (int index = 0; index < _initialKnowledgeSnapshot.Length; index++)
            {
                PegaKnowledgeFactAuthoringEntry entry = _initialKnowledgeSnapshot[index];
                PegaKnowledgeFactInitialAuthoringStatus status = entry.TryApply(
                    _actorBinding,
                    this,
                    PegaKnowledgeEvidence.From(
                        PegaNpcEvidenceOrigin.DesignerAuthoredInitial,
                        _initialKnowledgeObservationTimeSeconds,
                        this,
                        entry.DefinitionId),
                    out PegaKnowledgeWriteResult result,
                    out string diagnostic);

                if (status == PegaKnowledgeFactInitialAuthoringStatus.Pending)
                {
                    diagnostic = "The selected Actor identity is not resolved during owner entry; use a resolved ActorDeclaration or an available binding.";
                }

                if ((status != PegaKnowledgeFactInitialAuthoringStatus.Applied ||
                     result == PegaKnowledgeWriteResult.RejectedInvalid) &&
                    string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = $"The Global fact writer rejected the value with result '{result}'; check the definition policy and typed value.";
                }

                if (status != PegaKnowledgeFactInitialAuthoringStatus.Applied ||
                    result == PegaKnowledgeWriteResult.RejectedInvalid)
                {
                    EnsureLoggerInitialized();
                    _log.Error(
                        "Designer-authored initial Global Knowledge was rejected.",
                        LogFields.Field("owner", _activeOwner.name),
                        LogFields.Field("entryIndex", index),
                        LogFields.Field("factDefinitionId", entry.DefinitionId),
                        LogFields.Field("writeResult", result),
                        LogFields.Field("reason", diagnostic));
                }
            }
        }
    }
}

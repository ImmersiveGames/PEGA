using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ActivityFlow;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// Global Knowledge authority owned by one Activity content contribution. It exposes typed writes and a
    /// read-only GOAP capability; its store exists only while that owner is active.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/Knowledge/Global Knowledge State")]
    public sealed class PEGAGlobalKnowledgeState : MonoBehaviour,
        IPEGAKnowledgeFactReader,
        IPEGAKnowledgeFactWriter,
        IActivityContentLifecycleReceiver
    {
        private const string LogCategory = "PEGA.Gameplay.Knowledge";

        [SerializeField, Tooltip("Fatos iniciais da Activity. Cada linha usa o catálogo canônico e é publicada somente na authority Global deste owner.")]
        private List<PEGAKnowledgeFactAuthoringEntry> initialKnowledge =
            new List<PEGAKnowledgeFactAuthoringEntry>();

        [NonSerialized]
        private PEGAKnowledgeFactStore _factStore;

        [NonSerialized]
        private ActivityContentContribution _contribution;

        [NonSerialized]
        private Immersive.Framework.Authoring.ActivityAsset _activeActivity;

        [NonSerialized]
        private PEGAKnowledgeFactAuthoringEntry[] _initialKnowledgeSnapshot;

        [NonSerialized]
        private ActorOccurrenceReferenceBinding _actorBinding;

        [NonSerialized]
        private double _initialKnowledgeObservationTimeSeconds;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        public PEGAKnowledgeScope Scope => PEGAKnowledgeScope.Global;

        public bool IsAvailable => _factStore != null && _activeActivity != null;

        void IActivityContentLifecycleReceiver.OnActivityContentEntered(ActivityContentLifecycleContext context)
        {
            if (!IsOwnedContribution(context))
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

            if (IsAvailable && ReferenceEquals(_activeActivity, context.Activity))
            {
                return;
            }

            ClearStore();
            _factStore = new PEGAKnowledgeFactStore(PEGAKnowledgeScope.Global);
            _activeActivity = context.Activity;
            _initialKnowledgeSnapshot = initialKnowledge != null
                ? initialKnowledge.ToArray()
                : Array.Empty<PEGAKnowledgeFactAuthoringEntry>();
            _actorBinding = GetComponent<ActorOccurrenceReferenceBinding>();
            _initialKnowledgeObservationTimeSeconds = Time.timeAsDouble;
            _log.Info(
                "Activity-owned Global Knowledge became available.",
                LogFields.Field("activity", context.Activity.ActivityName),
                LogFields.Field("contentRoot", gameObject.name));
            ApplyInitialKnowledge();
        }

        void IActivityContentLifecycleReceiver.OnActivityContentExited(ActivityContentLifecycleContext context)
        {
            if (!IsOwnedContribution(context) || !IsAvailable ||
                !ReferenceEquals(_activeActivity, context.Activity))
            {
                return;
            }

            EnsureLoggerInitialized();
            _log.Info(
                "Activity-owned Global Knowledge was cleared.",
                LogFields.Field("activity", context.Activity.ActivityName),
                LogFields.Field("contentRoot", gameObject.name));
            ClearStore();
        }

        public PEGAKnowledgeFact<TValue> ReadFact<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            double queryTimeSeconds)
        {
            ValidateQuery(key, queryTimeSeconds);
            return IsAvailable
                ? _factStore.Get(key, queryTimeSeconds)
                : PEGAKnowledgeFact<TValue>.Unknown(PEGAKnowledgeScope.Global);
        }

        public PEGAKnowledgeWriteResult ObserveFact<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            TValue value,
            PEGAKnowledgeEvidence evidence)
        {
            PEGAKnowledgeFactStore store = RequireActiveStore();
            if (!key.IsValid || !evidence.IsValid)
            {
                return PEGAKnowledgeWriteResult.RejectedInvalid;
            }

            PEGAKnowledgeFact<TValue> previous = store.Get(key, evidence.ObservationTimeSeconds);
            PEGAKnowledgeWriteResult result = store.Observe(key, value, evidence);
            PEGAKnowledgeFact<TValue> current = store.Get(key, evidence.ObservationTimeSeconds);
            LogWriteResult(key, result, evidence, previous, current);
            return result;
        }

        public PEGAKnowledgeWriteResult InvalidateFact<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGAKnowledgeEvidence evidence)
        {
            PEGAKnowledgeFactStore store = RequireActiveStore();
            if (!key.IsValid || !evidence.IsValid)
            {
                return PEGAKnowledgeWriteResult.RejectedInvalid;
            }

            PEGAKnowledgeFact<TValue> previous = store.Get(key, evidence.ObservationTimeSeconds);
            PEGAKnowledgeWriteResult result = store.Invalidate(key, evidence);
            PEGAKnowledgeFact<TValue> current = store.Get(key, evidence.ObservationTimeSeconds);
            LogWriteResult(key, result, evidence, previous, current);
            return result;
        }

        private void OnDestroy()
        {
            ClearStore();
        }

        private bool IsOwnedContribution(ActivityContentLifecycleContext context)
        {
            if (context.ContentRoot != gameObject || context.Contribution == null ||
                context.Contribution.gameObject != gameObject)
            {
                return false;
            }

            if (_contribution == null)
            {
                _contribution = GetComponent<ActivityContentContribution>();
            }

            return _contribution != null && context.Contribution == _contribution;
        }

        private PEGAKnowledgeFactStore RequireActiveStore()
        {
            if (!IsAvailable)
            {
                throw new InvalidOperationException(
                    "Global Knowledge can be written only while its Activity content contribution is active.");
            }

            return _factStore;
        }

        private static void ValidateQuery<TAddress, TValue>(
            PEGAKnowledgeFactKey<TAddress, TValue> key,
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
            PEGAKnowledgeFactKey<TAddress, TValue> key,
            PEGAKnowledgeWriteResult result,
            PEGAKnowledgeEvidence evidence,
            PEGAKnowledgeFact<TValue> previous,
            PEGAKnowledgeFact<TValue> current)
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

            if (result == PEGAKnowledgeWriteResult.RejectedInvalid ||
                result == PEGAKnowledgeWriteResult.InvalidatedConflict)
            {
                _log.Warning("Global Knowledge fact write completed with a diagnostic result.", fields);
                return;
            }

            _log.Info("Global Knowledge fact write completed.", fields);
        }

        private static bool HasValueOrStatusChanged<TValue>(
            PEGAKnowledgeFact<TValue> previous,
            PEGAKnowledgeFact<TValue> current)
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

            _log = PEGALogging.CreateScopedLogger<PEGAGlobalKnowledgeState>(LogCategory, gameObject.name);
            _loggerInitialized = true;
        }

        private void ClearStore()
        {
            _factStore?.Clear();
            _factStore = null;
            _activeActivity = null;
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
                PEGAKnowledgeFactAuthoringEntry entry = _initialKnowledgeSnapshot[index];
                PEGAKnowledgeFactInitialAuthoringStatus status = entry.TryApply(
                    _actorBinding,
                    this,
                    PEGAKnowledgeEvidence.From(
                        PEGANpcEvidenceOrigin.DesignerAuthoredInitial,
                        _initialKnowledgeObservationTimeSeconds,
                        this,
                        entry.DefinitionId),
                    out PEGAKnowledgeWriteResult result,
                    out string diagnostic);

                if (status == PEGAKnowledgeFactInitialAuthoringStatus.Pending)
                {
                    diagnostic = "The selected Actor identity is not resolved during this Activity owner entry; use a resolved ActorDeclaration or an available binding.";
                }

                if ((status != PEGAKnowledgeFactInitialAuthoringStatus.Applied ||
                     result == PEGAKnowledgeWriteResult.RejectedInvalid) &&
                    string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = $"The Global fact writer rejected the value with result '{result}'; check the definition policy and typed value.";
                }

                if (status != PEGAKnowledgeFactInitialAuthoringStatus.Applied ||
                    result == PEGAKnowledgeWriteResult.RejectedInvalid)
                {
                    EnsureLoggerInitialized();
                    _log.Error(
                        "Designer-authored initial Global Knowledge was rejected.",
                        LogFields.Field("activity", _activeActivity != null ? _activeActivity.ActivityName : "unavailable"),
                        LogFields.Field("entryIndex", index),
                        LogFields.Field("factDefinitionId", entry.DefinitionId),
                        LogFields.Field("writeResult", result),
                        LogFields.Field("reason", diagnostic));
                }
            }
        }
    }
}

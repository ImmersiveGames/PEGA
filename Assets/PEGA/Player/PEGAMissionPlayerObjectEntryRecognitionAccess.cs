using Immersive.Framework.Actors;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.ObjectEntry;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.Player.Perception;
using UnityEngine;

namespace PEGA.Player
{
    /// <summary>Entrega a consulta Activity-scoped ao source do Player atual e a retira no release.</summary>
    [DisallowMultipleComponent]
    public sealed class PegaMissionPlayerObjectEntryRecognitionAccess : ActivityContentBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.PlayerObjectDiscovery";

        [SerializeField]
        [Tooltip("Âncora de reconhecimento pertencente ao conteúdo da Activity ativa.")]
        private ObjectEntryOccurrenceQueryBinding recognitionQuery;

        [SerializeField]
        [Tooltip("Referência oficial ao Slot do Player que receberá acesso à consulta.")]
        private ActorOccurrenceReferenceBinding playerActorReference;

        private PegaPlayerObjectEntryDiscoverySource _boundSource;
        private ObjectEntryOccurrenceQueryBinding _boundQuery;
        private ScopedLogger _log;
        private string _lastReportedDiagnostic;
        private string _activeActivityName = string.Empty;
        private string _boundActivityName = string.Empty;
        private string _boundPlayerRootName = string.Empty;
        private string _boundPlayerRootEntityId = string.Empty;
        private ActorOccurrenceId _boundPlayerActorOccurrenceId;
        private string _boundQueryAnchorName = string.Empty;
        private string _boundQueryAnchorEntityId = string.Empty;
        private bool _hasBoundAccess;
        private bool _isApplicationQuitting;

        protected override void OnActivityContentEntered(ActivityContentLifecycleContext context)
        {
            _isApplicationQuitting = false;
            _lastReportedDiagnostic = string.Empty;
            _activeActivityName = context.ActivityName;
            StartWatchingPlayer("activity-content-entered");
        }

        private void OnEnable()
        {
            _isApplicationQuitting = false;
            if (IsActivityContentActive)
            {
                StartWatchingPlayer("activity-content-enabled");
            }
        }

        private void StartWatchingPlayer(string reason)
        {
            if (_isApplicationQuitting)
            {
                StopWatchingPlayer();
                DetachFromPlayer("application-quit");
                return;
            }

            if (playerActorReference == null || recognitionQuery == null)
            {
                DetachFromPlayer("access-configuration-invalid");
                ReportConfigurationError(
                    "A ponte da Activity requer a referência do Player e a âncora de reconhecimento.");
                return;
            }

            playerActorReference.Changed -= OnPlayerActorReferenceChanged;
            playerActorReference.Changed += OnPlayerActorReferenceChanged;
            RefreshPlayerBinding(reason);
        }

        private void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
            StopWatchingPlayer();
            DetachFromPlayer("application-quit");
        }

        protected override void OnActivityContentExited(ActivityContentLifecycleContext context)
        {
            StopWatchingPlayer();
            DetachFromPlayer("activity-content-exited");
            _activeActivityName = string.Empty;
        }

        private void OnDisable()
        {
            StopWatchingPlayer();
            DetachFromPlayer("access-component-disabled");
        }

        private void OnPlayerActorReferenceChanged(ActorOccurrenceReferenceChange change)
        {
            RefreshPlayerBinding($"player-reference-{change.Kind}");
        }

        private void RefreshPlayerBinding(string reason)
        {
            if (_isApplicationQuitting)
            {
                DetachFromPlayer("application-quit");
                return;
            }

            if (!IsActivityContentActive ||
                playerActorReference == null ||
                recognitionQuery == null)
            {
                DetachFromPlayer(!IsActivityContentActive
                    ? "activity-content-inactive"
                    : "access-configuration-invalid");
                if (IsActivityContentActive)
                {
                    ReportConfigurationError(
                        "A ponte da Activity requer a referência do Player e a âncora de reconhecimento.");
                }
                return;
            }

            if (!playerActorReference.TryGetCurrent(out ActorOccurrenceResolution player) ||
                !player.Succeeded)
            {
                DetachFromPlayer("player-actor-reference-unavailable");
                return;
            }

            GameObject playerRoot = player.Occurrence.ActorRoot;
            PegaPlayerObjectEntryDiscoverySource source = playerRoot != null
                ? playerRoot.GetComponent<PegaPlayerObjectEntryDiscoverySource>()
                : null;
            if (source == null)
            {
                DetachFromPlayer("player-discovery-source-missing");
                ReportConfigurationError(
                    "O Actor físico atual do Slot não contém PEGAPlayerObjectEntryDiscoverySource no root.");
                return;
            }

            if (_hasBoundAccess &&
                object.ReferenceEquals(_boundSource, source) &&
                object.ReferenceEquals(_boundQuery, recognitionQuery) &&
                _boundPlayerActorOccurrenceId.Equals(player.Occurrence.Id))
            {
                _lastReportedDiagnostic = string.Empty;
                return;
            }

            if (_hasBoundAccess)
            {
                DetachFromPlayer("player-actor-or-query-replaced");
            }

            if (!source.TryBindRecognitionQuery(
                    recognitionQuery,
                    playerActorReference,
                    out string diagnostic))
            {
                DetachFromPlayer("query-binding-rejected");
                ReportConfigurationError(diagnostic);
                return;
            }

            _boundSource = source;
            _boundQuery = recognitionQuery;
            _boundActivityName = _activeActivityName;
            _boundPlayerRootName = playerRoot.name;
            _boundPlayerRootEntityId = playerRoot.GetEntityId().ToString();
            _boundPlayerActorOccurrenceId = player.Occurrence.Id;
            _boundQueryAnchorName = recognitionQuery.gameObject.name;
            _boundQueryAnchorEntityId = recognitionQuery.GetEntityId().ToString();
            _hasBoundAccess = true;
            _lastReportedDiagnostic = string.Empty;

            GetLogger().Info(
                "Player recognition query access became available.",
                LogFields.Field("activity", _boundActivityName),
                LogFields.Field("playerRoot", _boundPlayerRootName),
                LogFields.Field("playerOccurrenceId", _boundPlayerRootEntityId),
                LogFields.Field("frameworkActorOccurrenceId", _boundPlayerActorOccurrenceId.ToString()),
                LogFields.Field("queryAnchor", _boundQueryAnchorName),
                LogFields.Field("queryAnchorEntityId", _boundQueryAnchorEntityId),
                LogFields.Field("reason", reason));
        }

        private void StopWatchingPlayer()
        {
            if (playerActorReference != null)
            {
                playerActorReference.Changed -= OnPlayerActorReferenceChanged;
            }
        }

        private void DetachFromPlayer(string reason)
        {
            PegaPlayerObjectEntryDiscoverySource source = _boundSource;
            ObjectEntryOccurrenceQueryBinding query = _boundQuery;
            _boundSource = null;
            _boundQuery = null;

            if (source != null)
            {
                source.UnbindRecognitionQuery(query);
            }

            if (!_hasBoundAccess)
            {
                _boundPlayerActorOccurrenceId = default;
                return;
            }

            GetLogger().Info(
                "Player recognition query access was revoked.",
                LogFields.Field("activity", _boundActivityName),
                LogFields.Field("playerRoot", _boundPlayerRootName),
                LogFields.Field("playerOccurrenceId", _boundPlayerRootEntityId),
                LogFields.Field("frameworkActorOccurrenceId", _boundPlayerActorOccurrenceId.ToString()),
                LogFields.Field("queryAnchor", _boundQueryAnchorName),
                LogFields.Field("queryAnchorEntityId", _boundQueryAnchorEntityId),
                LogFields.Field("reason", reason));

            _hasBoundAccess = false;
            _boundActivityName = string.Empty;
            _boundPlayerRootName = string.Empty;
            _boundPlayerRootEntityId = string.Empty;
            _boundPlayerActorOccurrenceId = default;
            _boundQueryAnchorName = string.Empty;
            _boundQueryAnchorEntityId = string.Empty;
        }

        private void ReportConfigurationError(string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(diagnostic) ||
                string.Equals(_lastReportedDiagnostic, diagnostic, System.StringComparison.Ordinal))
            {
                return;
            }

            _lastReportedDiagnostic = diagnostic;
            GetLogger().Error(
                "Acesso do Player à consulta de Object Entry não pôde ser conectado.",
                LogFields.Field("activityContent", gameObject.name),
                LogFields.Field("diagnostic", diagnostic));
        }

        private ScopedLogger GetLogger()
        {
            _log ??= PegaLogging.CreateScopedLogger<PegaMissionPlayerObjectEntryRecognitionAccess>(
                LogCategory,
                gameObject.name);
            return _log;
        }
    }
}

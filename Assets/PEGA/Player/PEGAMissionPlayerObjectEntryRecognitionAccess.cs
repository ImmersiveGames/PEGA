using Immersive.Framework.Actors;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.ObjectEntry;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Attacks.Runtime;
using PEGA.Diagnostics;
using PEGA.Player.Perception;
using UnityEngine;

namespace PEGA.Player
{
    /// <summary>Entrega consultas Activity-scoped independentes ao Actor Player atual e as retira no release.</summary>
    [DisallowMultipleComponent]
    public sealed class PegaMissionPlayerObjectEntryRecognitionAccess : ActivityContentBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.PlayerObjectDiscovery";

        [SerializeField]
        [Tooltip("Âncora de reconhecimento pertencente ao conteúdo da Activity ativa.")]
        private ObjectEntryOccurrenceQueryBinding recognitionQuery;

        [SerializeField]
        [Tooltip("Consulta física de Actors pertencente ao conteúdo da Activity ativa, independente da descoberta de Object Entries.")]
        private ActorOccurrenceQueryBinding actorOccurrenceRecognitionQuery;

        [SerializeField]
        [Tooltip("Referência oficial ao Slot do Player que receberá acesso à consulta.")]
        private ActorOccurrenceReferenceBinding playerActorReference;

        private PegaPlayerObjectEntryDiscoverySource _boundSource;
        private ObjectEntryOccurrenceQueryBinding _boundQuery;
        private PegaActorAttackMechanic _boundAttackMechanic;
        private ActorOccurrenceQueryBinding _boundActorQuery;
        private ScopedLogger _log;
        private string _lastReportedDiagnostic;
        private string _lastAttackReportedDiagnostic;
        private string _activeActivityName = string.Empty;
        private string _boundActivityName = string.Empty;
        private string _boundPlayerRootName = string.Empty;
        private string _boundPlayerRootEntityId = string.Empty;
        private ActorOccurrenceId _boundPlayerActorOccurrenceId;
        private ActorOccurrenceId _boundAttackActorOccurrenceId;
        private string _boundQueryAnchorName = string.Empty;
        private string _boundQueryAnchorEntityId = string.Empty;
        private string _boundAttackActivityName = string.Empty;
        private string _boundAttackPlayerRootName = string.Empty;
        private string _boundActorQueryAnchorName = string.Empty;
        private bool _hasBoundAccess;
        private bool _hasBoundAttackAccess;
        private bool _isApplicationQuitting;

        protected override void OnActivityContentEntered(ActivityContentLifecycleContext context)
        {
            _isApplicationQuitting = false;
            _lastReportedDiagnostic = string.Empty;
            _lastAttackReportedDiagnostic = string.Empty;
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

            if (playerActorReference == null)
            {
                DetachFromPlayer("access-configuration-invalid");
                ReportConfigurationError(
                    "A ponte da Activity requer a referência oficial ao Slot do Player.");
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

            if (!IsActivityContentActive || playerActorReference == null)
            {
                DetachFromPlayer(!IsActivityContentActive
                    ? "activity-content-inactive"
                    : "access-configuration-invalid");
                if (IsActivityContentActive)
                {
                    ReportConfigurationError(
                        "A ponte da Activity requer a referência oficial ao Slot do Player.");
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
            if (playerRoot == null)
            {
                DetachFromPlayer("player-actor-root-unavailable");
                return;
            }

            RefreshObjectEntryQueryBinding(playerRoot, player.Occurrence.Id, reason);
            RefreshActorQueryBinding(playerRoot, player.Occurrence.Id, reason);
        }

        private void RefreshObjectEntryQueryBinding(
            GameObject playerRoot,
            ActorOccurrenceId playerOccurrenceId,
            string reason)
        {
            if (recognitionQuery == null)
            {
                DetachObjectEntryAccess("object-entry-query-unavailable");
                ReportConfigurationError(
                    "O conteúdo ativo requer a âncora de reconhecimento de Object Entry para manter descoberta e Pick Up.");
                return;
            }

            PegaPlayerObjectEntryDiscoverySource source = playerRoot != null
                ? playerRoot.GetComponent<PegaPlayerObjectEntryDiscoverySource>()
                : null;
            if (source == null)
            {
                DetachObjectEntryAccess("player-discovery-source-missing");
                ReportConfigurationError(
                    "O Actor físico atual do Slot não contém PEGAPlayerObjectEntryDiscoverySource no root.");
                return;
            }

            if (_hasBoundAccess &&
                object.ReferenceEquals(_boundSource, source) &&
                object.ReferenceEquals(_boundQuery, recognitionQuery) &&
                _boundPlayerActorOccurrenceId.Equals(playerOccurrenceId))
            {
                _lastReportedDiagnostic = string.Empty;
                return;
            }

            if (_hasBoundAccess)
            {
                DetachObjectEntryAccess("player-actor-or-query-replaced");
            }

            if (!source.TryBindRecognitionQuery(
                    recognitionQuery,
                    playerActorReference,
                    out string diagnostic))
            {
                DetachObjectEntryAccess("query-binding-rejected");
                ReportConfigurationError(diagnostic);
                return;
            }

            _boundSource = source;
            _boundQuery = recognitionQuery;
            _boundActivityName = _activeActivityName;
            _boundPlayerRootName = playerRoot.name;
            _boundPlayerRootEntityId = playerRoot.GetEntityId().ToString();
            _boundPlayerActorOccurrenceId = playerOccurrenceId;
            _boundQueryAnchorName = recognitionQuery.gameObject.name;
            _boundQueryAnchorEntityId = recognitionQuery.GetEntityId().ToString();
            _hasBoundAccess = true;
            _lastReportedDiagnostic = string.Empty;

            GetLogger().Info(
                "Player Object Entry recognition query access became available.",
                LogFields.Field("activity", _boundActivityName),
                LogFields.Field("playerRoot", _boundPlayerRootName),
                LogFields.Field("playerOccurrenceId", _boundPlayerRootEntityId),
                LogFields.Field("frameworkActorOccurrenceId", _boundPlayerActorOccurrenceId.ToString()),
                LogFields.Field("queryAnchor", _boundQueryAnchorName),
                LogFields.Field("queryAnchorEntityId", _boundQueryAnchorEntityId),
                LogFields.Field("reason", reason));
        }

        private void RefreshActorQueryBinding(
            GameObject playerRoot,
            ActorOccurrenceId playerOccurrenceId,
            string reason)
        {
            if (actorOccurrenceRecognitionQuery == null)
            {
                DetachActorAttackAccess("actor-query-unavailable");
                ReportAttackConfigurationError(
                    "O conteúdo ativo requer a consulta Activity de ocorrências físicas de Actor para Atacar.");
                return;
            }

            PegaActorAttackMechanic attackMechanic = playerRoot.GetComponent<PegaActorAttackMechanic>();
            if (attackMechanic == null || attackMechanic.gameObject != playerRoot)
            {
                DetachActorAttackAccess("player-attack-mechanic-missing");
                ReportAttackConfigurationError(
                    "O Actor físico atual do Slot não contém PegaActorAttackMechanic no root.");
                return;
            }

            if (_hasBoundAttackAccess &&
                object.ReferenceEquals(_boundAttackMechanic, attackMechanic) &&
                object.ReferenceEquals(_boundActorQuery, actorOccurrenceRecognitionQuery) &&
                _boundAttackActorOccurrenceId.Equals(playerOccurrenceId))
            {
                _lastAttackReportedDiagnostic = string.Empty;
                return;
            }

            if (_hasBoundAttackAccess)
            {
                DetachActorAttackAccess("player-actor-or-query-replaced");
            }

            if (!attackMechanic.TryBindActivityRecognitionQuery(actorOccurrenceRecognitionQuery, out string diagnostic))
            {
                DetachActorAttackAccess("actor-query-binding-rejected");
                ReportAttackConfigurationError(diagnostic);
                return;
            }

            _boundAttackMechanic = attackMechanic;
            _boundActorQuery = actorOccurrenceRecognitionQuery;
            _boundAttackActivityName = _activeActivityName;
            _boundAttackPlayerRootName = playerRoot.name;
            _boundAttackActorOccurrenceId = playerOccurrenceId;
            _boundActorQueryAnchorName = actorOccurrenceRecognitionQuery.gameObject.name;
            _hasBoundAttackAccess = true;
            _lastAttackReportedDiagnostic = string.Empty;

            GetLogger().Info(
                "Player Actor recognition query access became available for Attack.",
                LogFields.Field("activity", _boundAttackActivityName),
                LogFields.Field("playerRoot", _boundAttackPlayerRootName),
                LogFields.Field("frameworkActorOccurrenceId", _boundAttackActorOccurrenceId.ToString()),
                LogFields.Field("actorQueryAnchor", _boundActorQueryAnchorName),
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
            DetachObjectEntryAccess(reason);
            DetachActorAttackAccess(reason);
        }

        private void DetachObjectEntryAccess(string reason)
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

        private void DetachActorAttackAccess(string reason)
        {
            PegaActorAttackMechanic attackMechanic = _boundAttackMechanic;
            ActorOccurrenceQueryBinding actorQuery = _boundActorQuery;
            _boundAttackMechanic = null;
            _boundActorQuery = null;

            if (attackMechanic != null)
            {
                attackMechanic.UnbindActivityRecognitionQuery(actorQuery);
            }

            if (!_hasBoundAttackAccess)
            {
                _boundAttackActorOccurrenceId = default;
                return;
            }

            GetLogger().Info(
                "Player Actor recognition query access for Attack was revoked.",
                LogFields.Field("activity", _boundAttackActivityName),
                LogFields.Field("playerRoot", _boundAttackPlayerRootName),
                LogFields.Field("frameworkActorOccurrenceId", _boundAttackActorOccurrenceId.ToString()),
                LogFields.Field("actorQueryAnchor", _boundActorQueryAnchorName),
                LogFields.Field("reason", reason));

            _hasBoundAttackAccess = false;
            _boundAttackActivityName = string.Empty;
            _boundAttackPlayerRootName = string.Empty;
            _boundAttackActorOccurrenceId = default;
            _boundActorQueryAnchorName = string.Empty;
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
                "Acesso do Player às consultas de reconhecimento da Activity não pôde ser conectado.",
                LogFields.Field("activityContent", gameObject.name),
                LogFields.Field("diagnostic", diagnostic));
        }

        private void ReportAttackConfigurationError(string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(diagnostic) ||
                string.Equals(_lastAttackReportedDiagnostic, diagnostic, System.StringComparison.Ordinal))
            {
                return;
            }

            _lastAttackReportedDiagnostic = diagnostic;
            GetLogger().Error(
                "Acesso do Player à consulta de Actor para Atacar não pôde ser conectado.",
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

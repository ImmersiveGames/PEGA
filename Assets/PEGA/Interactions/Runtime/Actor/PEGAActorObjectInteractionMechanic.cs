using System.Collections.Generic;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.Interactions.Runtime.ObjectEntry;
using PEGA.Player.Perception;
using PEGA.Selection.Player;
using UnityEngine;
namespace PEGA.Interactions.Runtime.Actor
{
    /// <summary>Consome a intenção genérica do Player e encaminha solicitações autorizadas do Actor ao Agregador.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/Interaction/Actor/Object Interaction Mechanic")]
    public sealed class PegaActorObjectInteractionMechanic : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.ObjectInteraction";

        [Header("Player Intent")]
        [SerializeField]
        [Tooltip("Fonte de intenção genérica de interação vinculada ao mesmo Actor. Deixe vazia para Actors sem Input de Player.")]
        private MonoBehaviour playerIntentSource;

        [SerializeField]
        [Tooltip("Fonte da seleção atual do Player neste Actor.")]
        private PegaPlayerObjectEntrySelectionSource playerSelectionSource;

        [SerializeField]
        [Tooltip("Autoridade de descoberta e ocorrência usada para validar o alvo selecionado pelo Player.")]
        private PegaPlayerObjectEntryDiscoverySource playerDiscoverySource;

        private IPegaObjectInteractionIntentSource _playerIntentSource;
        private ScopedLogger _log;
        private bool _isConfigured;
        private bool _hasPlayerIntentPath;

        private void Awake()
        {
            _log = PegaLogging.CreateScopedLogger<PegaActorObjectInteractionMechanic>(
                LogCategory,
                gameObject.name);

            _hasPlayerIntentPath = playerIntentSource != null ||
                playerSelectionSource != null || playerDiscoverySource != null;
            if (!_hasPlayerIntentPath)
            {
                _isConfigured = true;
                return;
            }

            _playerIntentSource = playerIntentSource as IPegaObjectInteractionIntentSource;
            if (_playerIntentSource == null || playerIntentSource.gameObject != gameObject ||
                playerSelectionSource == null || playerSelectionSource.gameObject != gameObject ||
                playerDiscoverySource == null || playerDiscoverySource.gameObject != gameObject)
            {
                _log.Error(
                    "Player interaction requires an intent source, selection source and discovery authority on the same Actor root.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("intentSourceAssigned", playerIntentSource != null),
                    LogFields.Field("selectionSourceAssigned", playerSelectionSource != null),
                    LogFields.Field("discoverySourceAssigned", playerDiscoverySource != null));
                enabled = false;
                return;
            }

            _isConfigured = true;
            _log.Debug(
                "Actor object interaction mechanic initialized.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("playerIntentPath", _hasPlayerIntentPath));
        }

        private void Update()
        {
            if (!_isConfigured || !_hasPlayerIntentPath || !isActiveAndEnabled ||
                !_playerIntentSource.TryGetIntent(out PegaObjectInteractionIntent intent) ||
                !intent.IsRequested)
            {
                return;
            }

            ProcessPlayerIntent();
        }

        /// <summary>Encaminha a Action concreta já escolhida pelo GOAP através da mesma autoridade de execução.</summary>
        public bool TryStartRequestedAction(
            PegaObjectInteractionAction action,
            PegaObjectInteractionRequest request,
            out long executionId,
            out string diagnostic)
        {
            executionId = 0;
            diagnostic = string.Empty;
            if (!_isConfigured || !isActiveAndEnabled || action == null ||
                !object.ReferenceEquals(request.ActorRoot, gameObject))
            {
                diagnostic = "The Actor interaction mechanic requires its own active Actor root and an explicit composed action.";
                return false;
            }

            PegaObjectEntryInteractionAggregator aggregator = request.TargetRoot != null
                ? request.TargetRoot.GetComponent<PegaObjectEntryInteractionAggregator>()
                : null;
            if (aggregator == null)
            {
                diagnostic = "The current Object Entry has no active interaction aggregator.";
                return false;
            }

            bool started = aggregator.TryStart(action, request, out executionId, out diagnostic);
            _log.Info(
                started
                    ? "Actor object interaction request started."
                    : "Actor object interaction request was rejected.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("participant", request.Participant),
                LogFields.Field("objectEntryId", request.ObjectEntryId.StableText),
                LogFields.Field("objectRoot", request.TargetRoot.name),
                LogFields.Field("action", action.GetType().Name),
                LogFields.Field("executionId", executionId),
                LogFields.Field("diagnostic", diagnostic));
            return started;
        }

        private void ProcessPlayerIntent()
        {
            PegaPlayerObjectEntryCandidate? currentSelection = playerSelectionSource.CurrentSelection;
            if (!currentSelection.HasValue)
            {
                LogPlayerRequestRejected("The Player has no current Object Entry selection.", null);
                return;
            }

            PegaPlayerObjectEntryCandidate candidate = currentSelection.Value;
            if (candidate.Occurrence == null ||
                !playerDiscoverySource.IsCurrentCandidate(candidate))
            {
                LogPlayerRequestRejected(
                    "The selected Object Entry is no longer a current recognized occurrence.",
                    candidate);
                return;
            }

            if (!PegaObjectInteractionRequest.TryCreate(
                    PegaObjectInteractionParticipant.Player,
                    gameObject,
                    candidate.Occurrence,
                    playerDiscoverySource,
                    out PegaObjectInteractionRequest request,
                    out string diagnostic))
            {
                LogPlayerRequestRejected(diagnostic, candidate);
                return;
            }

            PegaObjectEntryInteractionAggregator aggregator = candidate.Occurrence
                .GetComponent<PegaObjectEntryInteractionAggregator>();
            if (aggregator == null ||
                !aggregator.TryGetAvailableActions(
                    request,
                    out IReadOnlyList<PegaObjectInteractionAction> actions,
                    out diagnostic))
            {
                LogPlayerRequestRejected(
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "The selected Object Entry has no interaction aggregator."
                        : diagnostic,
                    candidate);
                return;
            }

            if (actions.Count != 1)
            {
                LogPlayerRequestRejected(
                    actions.Count == 0
                        ? "The selected Object Entry offers no currently available operation to the Player."
                        : "The selected Object Entry offers multiple operations; contextual choice is not configured.",
                    candidate,
                    actions.Count);
                return;
            }

            PegaObjectInteractionAction action = actions[0];
            _log.Info(
                "Player interaction intent resolved against the current selection.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("objectEntryId", candidate.ObjectEntryId.StableText),
                LogFields.Field("objectRoot", candidate.Occurrence.name),
                LogFields.Field("action", action.GetType().Name));

            if (!TryStartRequestedAction(action, request, out _, out diagnostic))
            {
                LogPlayerRequestRejected(diagnostic, candidate, 1);
            }
        }

        private void LogPlayerRequestRejected(
            string diagnostic,
            PegaPlayerObjectEntryCandidate? candidate,
            int? availableActionCount = null)
        {
            PegaPlayerObjectEntryCandidate selected = candidate.GetValueOrDefault();
            _log.Info(
                "Player interaction intent did not start an operation.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("selectedObjectEntryId", candidate.HasValue
                    ? selected.ObjectEntryId.StableText
                    : "none"),
                LogFields.Field("selectedObjectRoot", candidate.HasValue && selected.Occurrence != null
                    ? selected.Occurrence.name
                    : "none"),
                LogFields.Field("availableActionCount", availableActionCount.HasValue
                    ? availableActionCount.Value.ToString()
                    : "unknown"),
                LogFields.Field("diagnostic", diagnostic));
        }
    }
}

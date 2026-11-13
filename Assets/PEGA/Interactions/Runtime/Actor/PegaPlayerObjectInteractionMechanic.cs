using System.Collections.Generic;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.Interactions.Runtime.ObjectEntry;
using PEGA.Player.Perception;
using PEGA.Selection.Player;
using UnityEngine;
namespace PEGA.Interactions.Runtime.Actor
{
    /// <summary>Resolve a intenção genérica do Player através de sua seleção e autoridade de descoberta.</summary>
    [AddComponentMenu("PEGA/Interaction/Actor/Player Object Interaction Mechanic")]
    public sealed class PegaPlayerObjectInteractionMechanic : PegaActorObjectInteractionMechanic
    {
        [Header("Player Intent")]
        [SerializeField]
        [Tooltip("Fonte de intenção genérica de interação vinculada ao mesmo Actor Player.")]
        private MonoBehaviour playerIntentSource;

        [SerializeField]
        [Tooltip("Fonte da seleção atual do Player neste Actor.")]
        private PegaPlayerObjectEntrySelectionSource playerSelectionSource;

        [SerializeField]
        [Tooltip("Autoridade de descoberta e ocorrência usada para validar o alvo selecionado pelo Player.")]
        private PegaPlayerObjectEntryDiscoverySource playerDiscoverySource;

        private IPegaObjectInteractionIntentSource _playerIntentSource;
        private bool _isConfigured;
        private int _pendingInteractionIntentFrame = -1;

        private void Awake()
        {
            InitializeSharedLogger();

            _playerIntentSource = playerIntentSource as IPegaObjectInteractionIntentSource;
            if (_playerIntentSource == null || playerIntentSource.gameObject != gameObject ||
                playerSelectionSource == null || playerSelectionSource.gameObject != gameObject ||
                playerDiscoverySource == null || playerDiscoverySource.gameObject != gameObject)
            {
                InteractionLogger.Error(
                    "Player interaction requires an intent source, selection source and discovery authority on the same Actor root.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("intentSourceAssigned", playerIntentSource != null),
                    LogFields.Field("selectionSourceAssigned", playerSelectionSource != null),
                    LogFields.Field("discoverySourceAssigned", playerDiscoverySource != null));
                enabled = false;
                return;
            }

            _isConfigured = true;
            InteractionLogger.Debug(
                "Actor object interaction mechanic initialized.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("playerIntentPath", true));
        }

        private void OnEnable()
        {
            if (_isConfigured && playerSelectionSource != null)
            {
                playerSelectionSource.ContextualEvaluationCompleted += OnContextualEvaluationCompleted;
            }
        }

        private void Update()
        {
            if (_pendingInteractionIntentFrame >= 0 &&
                _pendingInteractionIntentFrame != Time.frameCount)
            {
                _pendingInteractionIntentFrame = -1;
                LogPlayerRequestRejected(
                    "The Player physical selection was not evaluated in the current frame before this interaction request expired.",
                    playerSelectionSource != null
                        ? playerSelectionSource.CurrentSelection
                        : null);
            }

            if (!_isConfigured || !isActiveAndEnabled ||
                !_playerIntentSource.TryGetIntent(out PegaObjectInteractionIntent intent) ||
                !intent.IsRequested)
            {
                return;
            }

            _pendingInteractionIntentFrame = Time.frameCount;
        }

        private void OnContextualEvaluationCompleted()
        {
            if (!_isConfigured || !isActiveAndEnabled ||
                _pendingInteractionIntentFrame != Time.frameCount)
            {
                return;
            }

            _pendingInteractionIntentFrame = -1;
            ProcessPlayerIntent();
        }

        private void OnDisable()
        {
            if (playerSelectionSource != null)
            {
                playerSelectionSource.ContextualEvaluationCompleted -= OnContextualEvaluationCompleted;
            }

            _pendingInteractionIntentFrame = -1;
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
            InteractionLogger.Info(
                "Player interaction intent resolved against the current selection.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("objectEntryId", candidate.ObjectEntryId.StableText),
                LogFields.Field("objectRoot", candidate.Occurrence.name),
                LogFields.Field("action", action.GetType().Name));

            if (!TryStartSharedExecution(action, request, out _, out diagnostic))
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
            InteractionLogger.Info(
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

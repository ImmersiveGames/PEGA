using System;
using System.Threading;
using System.Threading.Tasks;
using Immersive.Framework.Actors;
using Immersive.Framework.PlayerParticipation;
using Immersive.Framework.PlayerSlots;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.Actors
{
    /// <summary>
    /// Prova manual de composição visual para ocorrências Player e NonPlayer.
    /// Destination: TemporaryDebug.
    /// Purpose: validar a substituição visual por Composition Parts.
    /// Removal: após conclusão e aceite da prova de composição do card #102.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PEGAActorVisualCompositionProof : MonoBehaviour
    {
        private enum ProofTarget
        {
            Player = 0,
            StaticNonPlayer = 1
        }

        private const string VisualKey = "visual";
        private const string Source = "PEGA.ActorVisualCompositionProof";
        private const string BaselineSource = "PEGA.ActorVisualCompositionProof.ActivityBaseline";
        private const int PlayerPriority = 20;
        private const int NonPlayerBaselinePriority = -10;
        private const int NonPlayerPriority = 20;

        [SerializeField, Tooltip("Selecione a ocorrência explícita que receberá os comandos de contexto.")]
        private ProofTarget proofTarget = ProofTarget.Player;
        [SerializeField] private PlayerSlotProfile playerSlotProfile;
        [SerializeField] private PlayerActorCompositionAccess playerCompositionAccess;
        [SerializeField] private PlayerSessionObserver playerSessionObserver;
        [SerializeField] private ActorOccurrenceReferenceBinding playerActorReference;
        [SerializeField] private ActorOccurrencePublication nonPlayerPublication;
        [SerializeField] private GameObject petraPatrolVisual;
        [SerializeField] private GameObject petraPatrolAlternateVisual;

        private ScopedLogger _logger;
        private PlayerActorCompositionContribution _playerContribution;
        private ActorOccurrenceCompositionContribution _nonPlayerBaselineContribution;
        private ActorOccurrenceCompositionContribution _nonPlayerContribution;
        private bool _operationInProgress;

        [ContextMenu("Apply Petra A")]
        private async void ApplyPetraA()
        {
            await ExecuteManualAsync(petraPatrolVisual, "Petra A", false);
        }

        [ContextMenu("Apply Petra B")]
        private async void ApplyPetraB()
        {
            await ExecuteManualAsync(petraPatrolAlternateVisual, "Petra B", false);
        }

        [ContextMenu("Restore Original")]
        private async void RestoreOriginal()
        {
            await ExecuteManualAsync(null, "Original", true);
        }

        private async Task ExecuteManualAsync(GameObject visualPrefab, string visualName, bool restore)
        {
            ScopedLogger logger;
            try
            {
                logger = GetLogger();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return;
            }

            if (!Application.isPlaying)
            {
                logger.Warning(
                    "PEGA Actor Composition proof commands are available only in Play Mode.",
                    LogFields.Field("target", proofTarget));
                return;
            }

            if (_operationInProgress)
            {
                logger.Warning(
                    "PEGA Actor Composition proof ignored a command while another operation is pending.",
                    LogFields.Field("target", proofTarget),
                    LogFields.Field("operation", visualName));
                return;
            }

            if (!restore && visualPrefab == null)
            {
                logger.Error(
                    "PEGA Actor Composition proof visual prefab is not assigned.",
                    LogFields.Field("target", proofTarget),
                    LogFields.Field("visual", visualName));
                return;
            }

            _operationInProgress = true;
            try
            {
                switch (proofTarget)
                {
                    case ProofTarget.Player:
                        await RunPlayerOperationAsync(visualPrefab, visualName, restore);
                        break;
                    case ProofTarget.StaticNonPlayer:
                        await RunStaticNonPlayerOperationAsync(visualPrefab, visualName, restore);
                        break;
                    default:
                        logger.Error(
                            "PEGA Actor Composition proof has an unsupported target.",
                            LogFields.Field("target", proofTarget));
                        break;
                }
            }
            catch (Exception exception)
            {
                logger.Error(
                    "PEGA Actor Composition proof operation failed before a result was returned.",
                    exception,
                    LogFields.Field("target", proofTarget),
                    LogFields.Field("operation", restore ? "Restore Original" : "Apply " + visualName));
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        private async Task RunPlayerOperationAsync(GameObject visualPrefab, string visualName, bool restore)
        {
            if (!TryBuildPlayerRequest(out PlayerActorCompositionContributionRequest request, out string issue))
            {
                GetLogger().Warning(
                    "PEGA Actor Composition proof could not resolve the current P1 occurrence.",
                    LogFields.Field("target", proofTarget),
                    LogFields.Field("diagnostic", issue));
                return;
            }

            if (!await EnsurePlayerContributionAsync(request))
                return;

            PlayerActorCompositionContribution contribution = _playerContribution;
            if (restore)
            {
                PlayerActorCompositionOperationResult restored = await contribution.RemovePartAsync(
                    VisualKey,
                    Source,
                    "Restore the Player profile visual",
                    CancellationToken.None);
                ReportPlayerResult("Restore Original", restored);

                if (restored != null && IsCommitted(restored.Status))
                {
                    PlayerActorCompositionOperationResult closed = await contribution.CloseAsync(
                        Source,
                        "Finish the manual Player visual contribution",
                        CancellationToken.None);
                    ReportPlayerResult("Close Player contribution", closed);
                    if (contribution.IsClosed)
                        _playerContribution = null;
                }
                else if (restored?.Status == PlayerActorCompositionOperationStatus.Stale)
                {
                    _playerContribution = null;
                }

                return;
            }

            PlayerActorCompositionOperationResult result = await contribution.SetPartAsync(
                new PlayerActorCompositionPart(VisualKey, visualPrefab),
                Source,
                $"Apply {visualName} to the current Player Actor",
                CancellationToken.None);
            ReportPlayerResult($"Apply {visualName}", result);
            if (result?.Status == PlayerActorCompositionOperationStatus.Stale)
                _playerContribution = null;
        }

        private async Task<bool> EnsurePlayerContributionAsync(
            PlayerActorCompositionContributionRequest request)
        {
            if (_playerContribution?.IsClosed == true)
                _playerContribution = null;

            if (_playerContribution != null && !Matches(request, _playerContribution))
            {
                PlayerActorCompositionContribution previous = _playerContribution;
                _playerContribution = null;
                PlayerActorCompositionOperationResult closed = await previous.CloseAsync(
                    Source,
                    "Release the contribution for the previous Player occurrence",
                    CancellationToken.None);
                ReportPlayerResult("Close previous Player contribution", closed);
            }

            if (_playerContribution != null)
                return true;

            PlayerActorCompositionContribution contribution = null;
            string issue = "Player composition access is not assigned.";
            if (playerCompositionAccess == null ||
                !playerCompositionAccess.TryOpenContribution(
                    request,
                    out contribution,
                    out issue))
            {
                GetLogger().Warning(
                    "PEGA Actor Composition proof could not open a contribution for the current Player occurrence.",
                    LogFields.Field("playerSlot", request.PlayerSlotId.StableText),
                    LogFields.Field("actorOccurrence", request.ActorOccurrenceId.Value),
                    LogFields.Field("diagnostic", issue));
                return false;
            }

            _playerContribution = contribution;
            GetLogger().Info(
                "PEGA Actor Composition proof opened a Player contribution.",
                LogFields.Field("playerSlot", contribution.PlayerSlotId.StableText),
                LogFields.Field("playerOccurrence", contribution.PlayerOccurrenceId.Value),
                LogFields.Field("actor", contribution.ActorId.StableText),
                LogFields.Field("actorOccurrence", contribution.ActorOccurrenceId.Value));
            return true;
        }

        private bool TryBuildPlayerRequest(
            out PlayerActorCompositionContributionRequest request,
            out string issue)
        {
            request = null;
            issue = string.Empty;
            if (playerSlotProfile == null ||
                playerCompositionAccess == null ||
                playerSessionObserver == null ||
                playerActorReference == null)
            {
                issue = "Player target requires its Slot Profile, scoped composition access, Session observer and occurrence reference.";
                return false;
            }

            if (!playerSlotProfile.TryGetPlayerSlotId(out PlayerSlotId configuredSlot, out issue))
                return false;
            if (!playerSessionObserver.TryGetObservation(
                    out PlayerSessionScopedObservationSnapshot observation))
            {
                issue = playerSessionObserver.Diagnostic;
                return false;
            }

            PlayerSessionScopedSlotObservation selectedSlot = default;
            bool foundSlot = false;
            for (int index = 0; index < observation.Slots.Count; index++)
            {
                PlayerSessionScopedSlotObservation candidate = observation.Slots[index];
                if (candidate.Slot.PlayerSlotId != configuredSlot)
                    continue;

                selectedSlot = candidate;
                foundSlot = true;
                break;
            }

            if (!foundSlot)
            {
                issue = $"Player Session observation has no configured Slot '{configuredSlot}'.";
                return false;
            }
            if (!selectedSlot.IsJoined || !selectedSlot.Slot.PlayerOccurrenceId.IsValid)
            {
                issue = $"Player Slot '{configuredSlot}' is not joined.";
                return false;
            }
            if (!selectedSlot.HasCurrentActorEvidence ||
                !selectedSlot.CurrentActor.HasCurrentActor ||
                !selectedSlot.CurrentActor.ActorEvidence.IsValid)
            {
                issue = $"Player Slot '{configuredSlot}' has no current Actor correlation evidence.";
                return false;
            }
            if (!playerActorReference.TryGetCurrent(out ActorOccurrenceResolution currentActor) ||
                !currentActor.Succeeded)
            {
                issue = currentActor.Diagnostic;
                return false;
            }

            ActorOccurrence occurrence = currentActor.Occurrence;
            if (occurrence.PlayerSlotId != configuredSlot ||
                selectedSlot.CurrentActor.ActorEvidence.PlayerSlotId != configuredSlot ||
                selectedSlot.CurrentActor.ActorEvidence.ActorId != occurrence.ActorId)
            {
                issue = $"Player observation and exact Actor occurrence reference do not match Slot '{configuredSlot}'.";
                return false;
            }

            request = new PlayerActorCompositionContributionRequest(
                selectedSlot.Slot.PlayerSlotId,
                selectedSlot.Slot.PlayerOccurrenceId,
                occurrence.ActorId,
                occurrence.Id,
                PlayerPriority,
                Source,
                "Manually change the current Player Actor visual");
            return true;
        }

        private async Task RunStaticNonPlayerOperationAsync(GameObject visualPrefab, string visualName, bool restore)
        {
            if (!TryGetStaticNonPlayerMount(
                    out ActorOccurrencePublication publication,
                    out Transform mount,
                    out string issue))
            {
                GetLogger().Warning(
                    "PEGA Actor Composition proof could not resolve the static NonPlayer occurrence.",
                    LogFields.Field("target", proofTarget),
                    LogFields.Field("diagnostic", issue));
                return;
            }

            if (!restore && !await EnsureNonPlayerBaselineAsync(publication, mount))
                return;
            if (!await EnsureNonPlayerContributionAsync(publication))
                return;

            ActorOccurrenceCompositionContribution contribution = _nonPlayerContribution;
            if (restore)
            {
                ActorOccurrenceCompositionOperationResult restored = await contribution.RemovePartAsync(
                    VisualKey,
                    Source,
                    "Restore the Activity-authored NonPlayer visual",
                    CancellationToken.None);
                ReportNonPlayerResult("Restore Original", restored);

                if (restored != null && IsCommitted(restored.Status))
                {
                    ActorOccurrenceCompositionOperationResult closed = await contribution.CloseAsync(
                        Source,
                        "Finish the manual NonPlayer visual contribution",
                        CancellationToken.None);
                    ReportNonPlayerResult("Close NonPlayer contribution", closed);
                    if (contribution.IsClosed)
                        _nonPlayerContribution = null;
                }
                else if (restored?.Status == ActorOccurrenceCompositionOperationStatus.Stale)
                {
                    _nonPlayerContribution = null;
                }

                return;
            }

            ActorOccurrenceCompositionOperationResult result = await contribution.SetPartAsync(
                new ActorOccurrenceCompositionPart(VisualKey, visualPrefab),
                Source,
                $"Apply {visualName} to the static NonPlayer Actor",
                CancellationToken.None);
            ReportNonPlayerResult($"Apply {visualName}", result);
            if (result?.Status == ActorOccurrenceCompositionOperationStatus.Stale)
                _nonPlayerContribution = null;
        }

        private bool TryGetStaticNonPlayerMount(
            out ActorOccurrencePublication publication,
            out Transform mount,
            out string issue)
        {
            publication = nonPlayerPublication;
            mount = null;
            if (publication == null)
            {
                issue = "Static NonPlayer target requires an explicit ActorOccurrencePublication reference.";
                return false;
            }

            mount = publication.CompositionMount;
            if (mount == null)
            {
                issue = "Static NonPlayer ActorOccurrencePublication has no composition mount.";
                return false;
            }

            issue = string.Empty;
            return true;
        }

        private async Task<bool> EnsureNonPlayerBaselineAsync(
            ActorOccurrencePublication publication,
            Transform mount)
        {
            if (_nonPlayerBaselineContribution?.IsClosed == true)
                _nonPlayerBaselineContribution = null;
            if (_nonPlayerBaselineContribution != null)
                return true;

            if (mount.childCount != 1)
            {
                GetLogger().Warning(
                    "PEGA Actor Composition proof requires exactly one Activity-authored visual in the static NonPlayer mount.",
                    LogFields.Field("actorRoot", publication.gameObject.name),
                    LogFields.Field("mount", mount.name),
                    LogFields.Field("childCount", mount.childCount));
                return false;
            }

            GameObject authoredVisual = mount.GetChild(0).gameObject;
            if (!publication.TryOpenCompositionContribution(
                    NonPlayerBaselinePriority,
                    BaselineSource,
                    "Keep the Activity-authored visual as the NonPlayer baseline",
                    out ActorOccurrenceCompositionContribution baseline,
                    out string issue))
            {
                GetLogger().Warning(
                    "PEGA Actor Composition proof could not open the static NonPlayer baseline contribution.",
                    LogFields.Field("actorRoot", publication.gameObject.name),
                    LogFields.Field("diagnostic", issue));
                return false;
            }

            ActorOccurrenceCompositionOperationResult adopted = await baseline.SetPartAsync(
                ActorOccurrenceCompositionPart.AdoptExisting(VisualKey, authoredVisual),
                BaselineSource,
                "Adopt the existing Activity-authored visual into the Composer baseline",
                CancellationToken.None);
            ReportNonPlayerResult("Register Activity visual baseline", adopted);
            if (adopted == null || !IsCommitted(adopted.Status))
            {
                ActorOccurrenceCompositionOperationResult closed = await baseline.CloseAsync(
                    BaselineSource,
                    "Close the rejected static NonPlayer baseline contribution",
                    CancellationToken.None);
                ReportNonPlayerResult("Close rejected baseline contribution", closed);
                return false;
            }

            _nonPlayerBaselineContribution = baseline;
            return true;
        }

        private async Task<bool> EnsureNonPlayerContributionAsync(ActorOccurrencePublication publication)
        {
            if (_nonPlayerContribution?.IsClosed == true)
                _nonPlayerContribution = null;
            if (_nonPlayerContribution != null)
                return true;

            if (!publication.TryOpenCompositionContribution(
                    NonPlayerPriority,
                    Source,
                    "Manually change the static NonPlayer Actor visual",
                    out ActorOccurrenceCompositionContribution contribution,
                    out string issue))
            {
                GetLogger().Warning(
                    "PEGA Actor Composition proof could not open a contribution for the static NonPlayer occurrence.",
                    LogFields.Field("actorRoot", publication.gameObject.name),
                    LogFields.Field("diagnostic", issue));
                return false;
            }

            _nonPlayerContribution = contribution;
            GetLogger().Info(
                "PEGA Actor Composition proof opened a static NonPlayer contribution.",
                LogFields.Field("actor", contribution.ActorId.StableText),
                LogFields.Field("actorOccurrence", contribution.ActorOccurrenceId.Value),
                LogFields.Field("owner", contribution.Owner));
            return true;
        }

        private ScopedLogger GetLogger()
        {
            if (_logger == null)
            {
                _logger = PEGALogging.CreateScopedLogger<PEGAActorVisualCompositionProof>(
                    "PEGA.Testing.ActorComposition",
                    gameObject.name);
            }

            return _logger;
        }

        private static bool Matches(
            PlayerActorCompositionContributionRequest request,
            PlayerActorCompositionContribution contribution) =>
            contribution.PlayerSlotId == request.PlayerSlotId &&
            contribution.PlayerOccurrenceId == request.PlayerOccurrenceId &&
            contribution.ActorId == request.ActorId &&
            contribution.ActorOccurrenceId == request.ActorOccurrenceId;

        private static bool IsCommitted(PlayerActorCompositionOperationStatus status) =>
            status is PlayerActorCompositionOperationStatus.Committed or
                PlayerActorCompositionOperationStatus.NoChange;

        private static bool IsCommitted(ActorOccurrenceCompositionOperationStatus status) =>
            status is ActorOccurrenceCompositionOperationStatus.Committed or
                ActorOccurrenceCompositionOperationStatus.NoChange;

        private void ReportPlayerResult(string operation, PlayerActorCompositionOperationResult result)
        {
            if (result == null)
            {
                GetLogger().Error(
                    "PEGA Actor Composition proof received no Player operation result.",
                    LogFields.Field("operation", operation));
                return;
            }

            LogField[] fields = LogFields.Of(
                LogFields.Field("operation", operation),
                LogFields.Field("status", result.Status),
                LogFields.Field("playerSlot", result.PlayerSlotId.StableText),
                LogFields.Field("playerOccurrence", result.PlayerOccurrenceId.Value),
                LogFields.Field("actor", result.ActorId.StableText),
                LogFields.Field("actorOccurrence", result.ActorOccurrenceId.Value),
                LogFields.Field("compositionRevision", result.CompositionRevision),
                LogFields.Field("issue", result.Issue));
            if (IsCommitted(result.Status))
                GetLogger().Info("PEGA Actor Composition proof Player operation completed.", fields);
            else
                GetLogger().Warning("PEGA Actor Composition proof Player operation did not commit.", fields);
        }

        private void ReportNonPlayerResult(string operation, ActorOccurrenceCompositionOperationResult result)
        {
            if (result == null)
            {
                GetLogger().Error(
                    "PEGA Actor Composition proof received no NonPlayer operation result.",
                    LogFields.Field("operation", operation));
                return;
            }

            LogField[] fields = LogFields.Of(
                LogFields.Field("operation", operation),
                LogFields.Field("status", result.Status),
                LogFields.Field("actor", result.ActorId.StableText),
                LogFields.Field("actorOccurrence", result.ActorOccurrenceId.Value),
                LogFields.Field("owner", result.Owner),
                LogFields.Field("compositionRevision", result.CompositionRevision),
                LogFields.Field("issue", result.Issue));
            if (IsCommitted(result.Status))
                GetLogger().Info("PEGA Actor Composition proof NonPlayer operation completed.", fields);
            else
                GetLogger().Warning("PEGA Actor Composition proof NonPlayer operation did not commit.", fields);
        }
    }
}

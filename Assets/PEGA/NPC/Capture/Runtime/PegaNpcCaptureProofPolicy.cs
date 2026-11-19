using System.Collections;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Actors.Runtime;
using PEGA.Attacks.Runtime;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC.Capture.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorDeclaration))]
    [RequireComponent(typeof(ActorOccurrenceQueryBinding))]
    [RequireComponent(typeof(PegaActorParticipationState))]
    [RequireComponent(typeof(PegaActorImpactReceiverMechanic))]
    [RequireComponent(typeof(PegaActorIncapacitationMechanic))]
    [AddComponentMenu("PEGA/NPC/Capture Proof Policy")]
    public sealed class PegaNpcCaptureProofPolicy : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.Attack.Events";

        [SerializeField]
        [Tooltip("No proof V03, captures this NPC after a confirmed attack incapacitates it while its inventory was known to be empty.")]
        private bool captureWhenIncapacitatedWithoutItems;

        private PegaActorParticipationState _participationState;
        private ActorDeclaration _actorDeclaration;
        private ActorOccurrenceQueryBinding _actorOccurrenceQuery;
        private PegaActorImpactReceiverMechanic _impactReceiver;
        private PegaActorIncapacitationMechanic _incapacitationMechanic;
        private IEventBinding _impactBinding;
        private PegaActorImpactResolvedEvent _pendingCaptureImpact;
        private Coroutine _pendingCaptureCoroutine;
        private ScopedLogger _log;

        private void Awake()
        {
            _log = PegaLogging.CreateScopedLogger<PegaNpcCaptureProofPolicy>(
                LogCategory,
                gameObject.name);
            _participationState = GetComponent<PegaActorParticipationState>();
            _actorDeclaration = GetComponent<ActorDeclaration>();
            _actorOccurrenceQuery = GetComponent<ActorOccurrenceQueryBinding>();
            _impactReceiver = GetComponent<PegaActorImpactReceiverMechanic>();
            _incapacitationMechanic = GetComponent<PegaActorIncapacitationMechanic>();
            if (_actorDeclaration == null || _actorDeclaration.ActorKind != ActorKind.NonPlayer)
            {
                _log.Error(
                    "The automatic capture proof policy is restricted to NonPlayer Actors.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("actorDeclarationAvailable", _actorDeclaration != null),
                    LogFields.Field("actorKind", _actorDeclaration != null
                        ? _actorDeclaration.ActorKind.ToString()
                        : "unavailable"));
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_participationState == null || _participationState.IsCaptured)
            {
                UnsubscribeFromImpacts();
                return;
            }

            if (_impactReceiver != null && _impactBinding == null)
            {
                _impactBinding = _impactReceiver.SubscribeToImpacts(OnImpactResolved);
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromImpacts();
            if (_pendingCaptureCoroutine != null)
            {
                StopCoroutine(_pendingCaptureCoroutine);
                _pendingCaptureCoroutine = null;
                _pendingCaptureImpact = null;
            }
        }

        private void OnDestroy() => UnsubscribeFromImpacts();

        private void OnImpactResolved(PegaActorImpactResolvedEvent impactEvent)
        {
            if (!captureWhenIncapacitatedWithoutItems ||
                impactEvent == null ||
                impactEvent.TargetActorRoot != gameObject ||
                !impactEvent.TargetOccurrenceId.IsValid ||
                impactEvent.Resolution.Outcome != PegaActorImpactOutcome.Incapacitated ||
                !impactEvent.Resolution.CarriedItemCountBeforeImpact.HasValue ||
                impactEvent.Resolution.CarriedItemCountBeforeImpact.Value != 0 ||
                _participationState == null ||
                _pendingCaptureImpact != null)
            {
                return;
            }

            _pendingCaptureImpact = impactEvent;
            _pendingCaptureCoroutine = StartCoroutine(CaptureAfterAttackResolution());
        }

        private IEnumerator CaptureAfterAttackResolution()
        {
            yield return null;

            PegaActorImpactResolvedEvent impactEvent = _pendingCaptureImpact;
            _pendingCaptureImpact = null;
            _pendingCaptureCoroutine = null;
            string occurrenceDiagnostic = string.Empty;
            if (impactEvent == null || !TryResolveExactTargetOccurrence(impactEvent, out occurrenceDiagnostic))
            {
                _log.Warning(
                    "NPC capture proof was not applied because the exact impacted Activity occurrence is no longer current.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", impactEvent != null ? impactEvent.TargetActorId.StableText : "unavailable"),
                    LogFields.Field("targetActorOccurrenceId", impactEvent != null ? impactEvent.TargetOccurrenceId.ToString() : "unavailable"),
                    LogFields.Field("diagnostic", impactEvent == null ? "The pending impact was cleared before the next simulation frame." : occurrenceDiagnostic));
                yield break;
            }

            if (_incapacitationMechanic == null || !_incapacitationMechanic.IsIncapacitated)
            {
                _log.Debug(
                    "NPC capture proof was not applied because the target had already recovered before the next simulation frame.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("targetActorId", impactEvent.TargetActorId.StableText),
                    LogFields.Field("targetActorOccurrenceId", impactEvent.TargetOccurrenceId.ToString()));
                yield break;
            }

            if (_participationState == null || !_participationState.TryMarkCaptured())
            {
                yield break;
            }

            _log.Info(
                "NPC was captured by the V03 proof policy after an empty-inventory incapacitating impact.",
                LogFields.Field("aggressorActorId", impactEvent.AggressorActorId.StableText),
                LogFields.Field("aggressorActorOccurrenceId", impactEvent.AggressorOccurrenceId.ToString()),
                LogFields.Field("targetActorId", impactEvent.TargetActorId.StableText),
                LogFields.Field("targetActorOccurrenceId", impactEvent.TargetOccurrenceId.ToString()),
                LogFields.Field("attackCapacity", impactEvent.Resolution.AttackCapacity),
                LogFields.Field("resistance", impactEvent.Resolution.Resistance),
                LogFields.Field("carriedItemCountBeforeImpact", impactEvent.Resolution.CarriedItemCountBeforeImpact),
                LogFields.Field("droppedItemCount", impactEvent.Resolution.DroppedItemCount),
                LogFields.Field("participationEnded", true),
                LogFields.Field("actorRemainsPhysicallyPresent", true));

            UnsubscribeFromImpacts();
        }

        private bool TryResolveExactTargetOccurrence(
            PegaActorImpactResolvedEvent impactEvent,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!gameObject.activeInHierarchy ||
                _actorDeclaration == null ||
                _actorOccurrenceQuery == null ||
                !impactEvent.TargetOccurrence.IsValid ||
                impactEvent.TargetOccurrence.ActorRoot != gameObject ||
                impactEvent.TargetOccurrence.ActorId != impactEvent.TargetActorId)
            {
                diagnostic = "The proof policy, target identity, root or exact impact occurrence is no longer available.";
                return false;
            }

            ActorOccurrenceResolution current = _actorOccurrenceQuery.Resolve(
                ActorOccurrenceReference.ForOccurrence(impactEvent.TargetOccurrence));
            ActorId declaredActorId;
            try
            {
                declaredActorId = _actorDeclaration.ActorId;
            }
            catch (System.ArgumentException exception)
            {
                diagnostic = exception.Message;
                return false;
            }

            if (!current.Succeeded ||
                current.Occurrence.Id != impactEvent.TargetOccurrenceId ||
                current.Occurrence.ActorRoot != gameObject ||
                current.Occurrence.ActorId != declaredActorId)
            {
                diagnostic = string.IsNullOrWhiteSpace(current.Diagnostic)
                    ? $"Exact occurrence query returned {current.Status} or a different Actor occurrence."
                    : current.Diagnostic;
                return false;
            }

            return true;
        }

        private void UnsubscribeFromImpacts()
        {
            if (_impactReceiver != null && _impactBinding != null)
            {
                _impactReceiver.UnsubscribeFromImpacts(_impactBinding);
                _impactBinding = null;
            }
        }
    }
}

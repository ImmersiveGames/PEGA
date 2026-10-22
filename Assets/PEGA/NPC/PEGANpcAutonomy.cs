using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Actors;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// Starts the first valid Normal-context GOAP plan for this NPC after Perception confirms Rick.
    /// Plan selection is event-driven; this component does not own Action execution after start.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Autonomy")]
    [RequireComponent(typeof(ActorOccurrenceReferenceBinding))]
    [RequireComponent(typeof(PEGANpcKnowledgeState))]
    [RequireComponent(typeof(PEGANpcDesireState))]
    [RequireComponent(typeof(PEGANpcApproachKnownRickAction))]
    public sealed class PEGANpcAutonomy : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.Autonomy";

        [SerializeField]
        [Tooltip("Profile de Desejos atribuído a este NPC. O profile atual de N01 é provisório para a demonstração na Mission.")]
        private PEGANpcDesireProfile desireProfile;

        [System.NonSerialized]
        private ActorOccurrenceReferenceBinding _actorReference;

        [System.NonSerialized]
        private PEGANpcKnowledgeState _knowledgeState;

        [System.NonSerialized]
        private PEGANpcDesireState _desireState;

        [System.NonSerialized]
        private PEGANpcApproachKnownRickAction _approachAction;

        [System.NonSerialized]
        private PEGANpcDesireContextDefinition _normalContext;

        [System.NonSerialized]
        private IEventBinding _knowledgeChangeBinding;
        [System.NonSerialized]
        private ScopedLogger _log;

        [System.NonSerialized]
        private bool _bootstrapPending;

        [System.NonSerialized]
        private bool _selectionQueued;

        [System.NonSerialized]
        private bool _initialSelectionConsumed;
        private void OnEnable()
        {
            EnsureLoggerInitialized();
            if (!TryInitialize(out string diagnostic))
            {
                _log.Error(
                    "NPC autonomy could not initialize its Normal-context GOAP inputs.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("diagnostic", diagnostic));
                enabled = false;
                return;
            }

            _knowledgeChangeBinding ??= _knowledgeState.SubscribeToKnowledgeChanges(OnKnowledgeChanged);
            _bootstrapPending = !_initialSelectionConsumed;
        }

        private void OnDisable()
        {
            if (_knowledgeState != null && _knowledgeChangeBinding != null)
            {
                _knowledgeState.UnsubscribeFromKnowledgeChanges(_knowledgeChangeBinding);
                _knowledgeChangeBinding = null;
            }

            _bootstrapPending = false;
            _selectionQueued = false;
        }

        private void OnDestroy()
        {
            if (_knowledgeState != null && _knowledgeChangeBinding != null)
            {
                _knowledgeState.UnsubscribeFromKnowledgeChanges(_knowledgeChangeBinding);
                _knowledgeChangeBinding = null;
            }        }

        private void LateUpdate()
        {
            if (!_bootstrapPending && !_selectionQueued)
            {
                return;
            }

            bool wasQueuedByPerception = _selectionQueued;
            _bootstrapPending = false;
            _selectionQueued = false;
            if (_initialSelectionConsumed || !TryGetCurrentPerceptionBackedRick(out ActorId rickActorId))
            {
                return;
            }

            SelectAndStartInitialPlan(rickActorId, wasQueuedByPerception);
        }

        private bool TryInitialize(out string diagnostic)
        {
            diagnostic = string.Empty;
            _actorReference = GetComponent<ActorOccurrenceReferenceBinding>();
            _knowledgeState = GetComponent<PEGANpcKnowledgeState>();
            _desireState = GetComponent<PEGANpcDesireState>();
            _approachAction = GetComponent<PEGANpcApproachKnownRickAction>();

            if (_actorReference == null || _knowledgeState == null || _desireState == null || _approachAction == null)
            {
                diagnostic = "Actor reference, Knowledge State, Desire State and Approach Known Rick Action must exist on this NPC root.";
                return false;
            }

            if (desireProfile == null || !desireProfile.TryGetContext(
                    PEGANpcDesireContext.Normal,
                    out _normalContext,
                    out diagnostic))
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "A Desire Profile with an explicit Normal context is required."
                    : diagnostic;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private void OnKnowledgeChanged(PEGANpcKnowledgeFactChangedEvent change)
        {
            if (_initialSelectionConsumed ||
                change == null ||
                change.FactKind != PEGANpcKnowledgeFactKind.RickVisualContact ||
                !change.SubjectActorId.HasValue ||
                !change.HasValueOrStatusChanged ||
                change.CurrentStatus != PEGANpcKnowledgeStatus.Known ||
                change.EvidenceOrigin != PEGANpcEvidenceOrigin.Perception ||
                !TryGetCurrentPerceptionBackedRick(out ActorId rickActorId) ||
                change.SubjectActorId.Value != rickActorId)
            {
                return;
            }

            PEGANpcKnowledgeFact<bool> contact = _knowledgeState.GetRickVisualContact(rickActorId);
            if (!contact.TryGetValue(out bool hasVisualContact) ||
                !hasVisualContact ||
                !contact.HasObservationTime ||
                contact.ObservationTimeSeconds != change.ObservationTimeSeconds ||
                (contact.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                return;
            }

            _selectionQueued = true;
        }

        private bool TryGetCurrentPerceptionBackedRick(out ActorId rickActorId)
        {
            rickActorId = default;
            if (_actorReference == null || !_actorReference.TryGetCurrent(out ActorOccurrenceResolution resolution))
            {
                return false;
            }

            rickActorId = resolution.Occurrence.ActorId;
            if (!rickActorId.IsValid)
            {
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                _knowledgeState.GetObservedCharacterIdentity(rickActorId);
            PEGANpcKnowledgeFact<bool> contact = _knowledgeState.GetRickVisualContact(rickActorId);
            PEGANpcKnowledgeFact<Vector3> position = _knowledgeState.GetLastObservedRickPosition(rickActorId);
            return identity.TryGetValue(out _) &&
                   (identity.Origins & PEGANpcEvidenceOrigin.Perception) != 0 &&
                   contact.TryGetValue(out bool hasVisualContact) &&
                   hasVisualContact &&
                   (contact.Origins & PEGANpcEvidenceOrigin.Perception) != 0 &&
                   position.TryGetValue(out _) &&
                   position.HasObservationTime &&
                   (position.Origins & PEGANpcEvidenceOrigin.Perception) != 0;
        }

        private void SelectAndStartInitialPlan(ActorId rickActorId, bool wasQueuedByPerception)
        {
            if (!_approachAction.TryCreatePlanningCandidate(
                    out PEGANpcPlanningTarget target,
                    out PEGANpcPlanningActionCandidate candidate,
                    out string candidateDiagnostic))
            {
                _log.Warning(
                    "NPC received valid Rick perception, but no live Approach Known Rick candidate was available.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", rickActorId.StableText),
                    LogFields.Field("trigger", wasQueuedByPerception ? "PerceptionEvent" : "InitialKnowledgeSnapshot"),
                    LogFields.Field("diagnostic", candidateDiagnostic));
                return;
            }

            PEGANpcPlanningInput input = new PEGANpcPlanningInput(
                PEGANpcPlanningValidationMode.LiveKnowledge,
                PEGANpcDesireContext.Normal,
                new[] { target },
                new[] { candidate });
            PEGANpcPlanSelectionResult selection = PEGANpcPlanSelector.Select(
                input,
                _normalContext,
                _desireState.GetSnapshot());

            if (!selection.IsValid ||
                !selection.HasSelectedPlan ||
                selection.SelectedPlan.ActionId != PEGANpcPlanningActionId.ApproachKnownRick ||
                selection.SelectedPlan.TargetProvenance != PEGANpcPlanningTargetProvenance.KnowledgeState ||
                selection.SelectedPlan.TargetId != PEGANpcPlanningTargetId.ForActor(rickActorId))
            {
                _log.Warning(
                    "NPC perception did not produce a selectable live GOAP plan.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", rickActorId.StableText),
                    LogFields.Field("validationMode", selection.ValidationMode),
                    LogFields.Field("candidatePlanCount", selection.CandidatePlans.Count),
                    LogFields.Field("diagnostic", selection.Diagnostic));
                return;
            }

            PEGANpcPlanSnapshot plan = selection.SelectedPlan;
            _initialSelectionConsumed = true;
            _log.Info(
                "NPC autonomously selected an initial GOAP plan from live Perception-backed knowledge.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("trigger", wasQueuedByPerception ? "PerceptionEvent" : "InitialKnowledgeSnapshot"),
                LogFields.Field("context", selection.Context),
                LogFields.Field("desireId", plan.DesireId),
                LogFields.Field("actionId", plan.ActionId),
                LogFields.Field("targetActorId", plan.TargetId),
                LogFields.Field("targetProvenance", plan.TargetProvenance),
                LogFields.Field("targetPosition", plan.TargetPosition),
                LogFields.Field("positionEvidenceTimeSeconds", plan.EvidenceTimeSeconds),
                LogFields.Field("routeLengthMeters", plan.RouteLengthMeters),
                LogFields.Field("estimatedSeconds", plan.EstimatedSeconds),
                LogFields.Field("effectiveDesireWeight", plan.EffectiveDesireWeight),
                LogFields.Field("costPreference", plan.CostPreference),
                LogFields.Field("score", plan.Score));

            if (!_approachAction.TryStart(out string startDiagnostic))
            {
                _log.Error(
                    "NPC selected a valid GOAP plan, but its Action could not start.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("actionId", plan.ActionId),
                    LogFields.Field("diagnostic", startDiagnostic));
                return;
            }

            _log.Info(
                "NPC started the selected GOAP Action through its existing physical navigation path.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("actionId", plan.ActionId),
                LogFields.Field("actionOutcome", _approachAction.Outcome),
                LogFields.Field("targetPosition", plan.TargetPosition),
                LogFields.Field("speedRegime", candidate.SpeedRegime));
        }

        private void EnsureLoggerInitialized()
        {
            _log ??= PEGALogging.CreateScopedLogger<PEGANpcAutonomy>(LogCategory, gameObject.name);
        }
    }
}

using System;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.Movement.Actor;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
namespace PEGA.NPC.Actions.Runtime
{
    [CreateAssetMenu(fileName = "ApproachKnownCharacter", menuName = "PEGA/NPC/Movement/Approach Known Character")]
    public sealed class PegaNpcApproachKnownCharacterAction : PegaNpcActionProviderAsset,
        IPegaNpcLastKnownTargetContinuationProvider,
        IPegaNpcKnowledgeChangeRelevanceProvider
    {
        private const string IntentLogCategory = "PEGA.Gameplay.NPC.TargetContinuation";

        protected override PegaNpcActionEffectKind ActionEffectKind =>
            PegaNpcActionEffectKind.ApproachKnownCharacterPosition;

        public override PegaNpcActionContract Contract => new PegaNpcActionContract(
            PegaNpcPlanningActionId.ApproachKnownCharacter,
            ActionEffectKind,
            PegaNpcPlanningTargetKind.CharacterPosition,
            true,
            "NPC began approaching a known character.");

        [System.NonSerialized]
        private PegaNpcKnowledgeState _knowledgeState;

        [System.NonSerialized]
        private PegaNpcKnowledgeReadView _knowledgeReadView;

        [System.NonSerialized]
        private bool _runningPerceptionContinuation;

        [System.NonSerialized]
        private ActorOccurrenceReferenceBinding _targetReference;

        [System.NonSerialized]
        private IEventBinding _knowledgeChangeBinding;

        [System.NonSerialized]
        private EventBus<PegaNpcLastKnownTargetContinuationIntentRequestedEvent> _continuationIntentEvents;

        [System.NonSerialized]
        private ScopedLogger _intentLog;

        public IEventBinding SubscribeToLastKnownTargetContinuationIntents(
            Action<PegaNpcLastKnownTargetContinuationIntentRequestedEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureContinuationIntentEventsInitialized();
            return _continuationIntentEvents.Subscribe(intentEvent =>
            {
                try
                {
                    handler(intentEvent);
                }
                catch (Exception exception)
                {
                    LogIntentSubscriberFailure(handler, intentEvent, exception);
                }
            });
        }

        public bool UnsubscribeFromLastKnownTargetContinuationIntents(IEventBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            return _continuationIntentEvents != null && _continuationIntentEvents.Unsubscribe(binding);
        }

        protected override bool OnRuntimeInitialize(out string diagnostic)
        {
            if (!base.OnRuntimeInitialize(out diagnostic))
            {
                return false;
            }

            _knowledgeState = RuntimeContext.KnowledgeState;
            _knowledgeReadView = RuntimeContext.KnowledgeReadView;
            _targetReference = RuntimeContext.ActorReference;
            if (_knowledgeState == null || _knowledgeState.gameObject != ActorRoot ||
                _knowledgeReadView == null ||
                _targetReference == null || _targetReference.gameObject != ActorRoot)
            {
                diagnostic = "Approach Known Character requires the Actor's Knowledge State and occurrence reference.";
                return false;
            }

            EnsureIntentLoggerInitialized();
            diagnostic = string.Empty;
            return true;
        }

        protected override void OnRuntimeEnabled()
        {
            _knowledgeChangeBinding ??= _knowledgeState.SubscribeToKnowledgeChanges(OnKnowledgeChanged);
            base.OnRuntimeEnabled();
        }

        protected override void OnRuntimeDisabled()
        {
            if (_knowledgeState != null && _knowledgeChangeBinding != null)
            {
                _knowledgeState.UnsubscribeFromKnowledgeChanges(_knowledgeChangeBinding);
                _knowledgeChangeBinding = null;
            }

            base.OnRuntimeDisabled();
        }

        protected override void OnRuntimeDestroyed()
        {
            if (_knowledgeState != null && _knowledgeChangeBinding != null)
            {
                _knowledgeState.UnsubscribeFromKnowledgeChanges(_knowledgeChangeBinding);
                _knowledgeChangeBinding = null;
            }

            _continuationIntentEvents?.Clear();
            _continuationIntentEvents = null;
            base.OnRuntimeDestroyed();
        }

        public bool TryGetKnownTargetPosition(out Vector3 position, out string diagnostic)
        {
            return TryGetKnownTargetEvidence(out _, out position, out _, out diagnostic);
        }

        public bool IsRelevantKnowledgeChange(PegaKnowledgeFactChangedEvent change)
        {
            if (change == null || !change.HasValueOrStatusChanged ||
                !TryResolveTargetSubject(out ActorId subjectActorId, out _) ||
                !PegaNpcKnowledgeFacts.IsCharacterFactChangeFor(change, subjectActorId))
            {
                return false;
            }

            return true;
        }

        public bool IsCurrentContinuationIntent(
            PegaNpcLastKnownTargetContinuationIntent intent,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!intent.IsValid || !TryResolveTargetSubject(out ActorId currentSubjectActorId, out diagnostic) ||
                currentSubjectActorId != intent.SubjectActorId)
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "The intent does not identify this Action's currently configured target occurrence."
                    : diagnostic;
                return false;
            }

            PegaKnowledgeFact<bool> playerFact = _knowledgeState.GetFact(
                PegaNpcKnowledgeFacts.IsPlayer.For(intent.SubjectActorId));
            if (!PegaNpcKnowledgeFacts.HasPerceptionBackedPlayerFactAtOrBefore(
                    playerFact,
                    intent.ContactLossObservationTimeSeconds))
            {
                diagnostic = "The continuation target has no Perception-backed Player fact at or before contact loss.";
                return false;
            }

            PegaKnowledgeFact<bool> contact =
                _knowledgeState.GetCharacterVisualContact(intent.SubjectActorId);
            PegaKnowledgeFact<Vector3> lastPosition =
                _knowledgeState.GetLastObservedCharacterPosition(intent.SubjectActorId);
            if (!contact.TryGetValue(out bool hasVisualContact) ||
                hasVisualContact ||
                !contact.HasObservationTime ||
                Math.Abs(contact.ObservationTimeSeconds - intent.ContactLossObservationTimeSeconds) > 0.000001d ||
                !contact.HasOrigin(PegaNpcEvidenceOrigin.Perception))
            {
                diagnostic = "The current Knowledge State no longer matches the intent's Perception-backed visual-contact loss.";
                return false;
            }

            if (!lastPosition.TryGetValue(out Vector3 knownPosition) ||
                !knownPosition.Equals(intent.LastKnownPosition) ||
                !lastPosition.HasObservationTime ||
                Math.Abs(lastPosition.ObservationTimeSeconds - intent.LastKnownObservationTimeSeconds) > 0.000001d ||
                lastPosition.ObservationTimeSeconds > intent.ContactLossObservationTimeSeconds + 0.000001d ||
                !lastPosition.HasOrigin(PegaNpcEvidenceOrigin.Perception))
            {
                diagnostic = "The intent's last position is not the current Perception-backed position preserved by this NPC.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        public bool IsContinuationSupersededByReacquisition(
            PegaNpcLastKnownTargetContinuationIntent intent)
        {
            if (!intent.IsValid ||
                !TryResolveTargetSubject(out ActorId currentSubjectActorId, out _) ||
                currentSubjectActorId != intent.SubjectActorId)
            {
                return false;
            }

            PegaKnowledgeFact<bool> contact =
                _knowledgeState.GetCharacterVisualContact(intent.SubjectActorId);
            return contact.TryGetValue(out bool hasVisualContact) &&
                hasVisualContact &&
                contact.HasObservationTime &&
                contact.ObservationTimeSeconds > intent.ContactLossObservationTimeSeconds + 0.000001d &&
                contact.HasOrigin(PegaNpcEvidenceOrigin.Perception);
        }

        private void OnKnowledgeChanged(PegaKnowledgeFactChangedEvent change)
        {
            if (!PegaNpcKnowledgeFacts.TryGetCharacterSubject(change, out ActorId subjectActorId) ||
                !PegaNpcKnowledgeFacts.IsPerceptionContactChange(change, subjectActorId))
            {
                return;
            }

            PegaKnowledgeFact<bool> contact =
                _knowledgeState.GetCharacterVisualContact(subjectActorId);
            if (!contact.TryGetValue(out bool hasVisualContact) || hasVisualContact)
            {
                return;
            }

            if (!TryCreateLastKnownTargetContinuationIntent(
                    change,
                    out PegaNpcLastKnownTargetContinuationIntent intent,
                    out string diagnostic))
            {
                EnsureIntentLoggerInitialized();
                _intentLog.Warning(
                    "Lost-contact pursuit intent was not published.",
                    LogFields.Field("actorRoot", ActorRoot.name),
                    LogFields.Field("subjectActorId", subjectActorId),
                    LogFields.Field("observationTimeSeconds", change.Evidence.ObservationTimeSeconds),
                    LogFields.Field("diagnostic", diagnostic));
                return;
            }

            EnsureContinuationIntentEventsInitialized();
            _continuationIntentEvents.Publish(
                new PegaNpcLastKnownTargetContinuationIntentRequestedEvent(intent));

            EnsureIntentLoggerInitialized();
            _intentLog.Debug(
                "NPC requested continuation of target pursuit at the last Perception-backed position.",
                LogFields.Field("actorRoot", ActorRoot.name),
                LogFields.Field("subjectActorId", intent.SubjectActorId),
                LogFields.Field("actionId", intent.ActionId),
                LogFields.Field("targetPosition", intent.LastKnownPosition),
                LogFields.Field("positionEvidenceTimeSeconds", intent.LastKnownObservationTimeSeconds),
                LogFields.Field("contactLossTimeSeconds", intent.ContactLossObservationTimeSeconds));
        }

        /// <summary>
        /// Produces the continuation intent for a confirmed visual-contact loss.
        /// The destination comes only from this NPC's Knowledge State; this method never reads target's current transform.
        /// Call it while handling the corresponding N01 knowledge-change event.
        /// </summary>
        private bool TryCreateLastKnownTargetContinuationIntent(
            PegaKnowledgeFactChangedEvent contactChange,
            out PegaNpcLastKnownTargetContinuationIntent intent,
            out string diagnostic)
        {
            intent = default;
            diagnostic = string.Empty;
            if (contactChange == null ||
                !PegaNpcKnowledgeFacts.TryGetCharacterSubject(contactChange, out ActorId subjectActorId) ||
                !contactChange.IsFor(PegaNpcKnowledgeFacts.CharacterVisualContact.For(subjectActorId)) ||
                !contactChange.HasValueOrStatusChanged ||
                contactChange.PreviousStatus != PegaKnowledgeStatus.Known ||
                contactChange.CurrentStatus != PegaKnowledgeStatus.Known ||
                !contactChange.Evidence.Origin.Is(PegaNpcEvidenceOrigin.Perception))
            {
                diagnostic = "A changed Known-to-Known target visual-contact event from Perception is required.";
                return false;
            }

            if (_knowledgeState == null || _knowledgeState.gameObject != ActorRoot)
            {
                diagnostic = "The Action requires this NPC's PEGANpcKnowledgeState on the same root.";
                return false;
            }

            if (!subjectActorId.IsValid)
            {
                diagnostic = "The contact-loss event has no valid target subject ActorId.";
                return false;
            }

            PegaKnowledgeFact<bool> contact = _knowledgeState.GetCharacterVisualContact(subjectActorId);
            if (!contact.TryGetValue(out bool hasVisualContact) ||
                hasVisualContact ||
                !contact.HasObservationTime ||
                contact.ObservationTimeSeconds != contactChange.Evidence.ObservationTimeSeconds ||
                !contact.HasOrigin(PegaNpcEvidenceOrigin.Perception))
            {
                diagnostic = "The current Knowledge State does not confirm this Perception event as a current visual-contact loss.";
                return false;
            }

            PegaKnowledgeFact<bool> playerFact = _knowledgeState.GetFact(
                PegaNpcKnowledgeFacts.IsPlayer.For(subjectActorId));
            if (!PegaNpcKnowledgeFacts.HasPerceptionBackedPlayerFactAtOrBefore(
                    playerFact,
                    contact.ObservationTimeSeconds))
            {
                diagnostic = "The target was not known to be a Player through Perception before contact loss.";
                return false;
            }

            PegaKnowledgeFact<Vector3> lastPosition =
                _knowledgeState.GetLastObservedCharacterPosition(subjectActorId);
            if (!lastPosition.TryGetValue(out Vector3 position) ||
                !lastPosition.HasObservationTime ||
                !lastPosition.HasOrigin(PegaNpcEvidenceOrigin.Perception) ||
                lastPosition.ObservationTimeSeconds > contact.ObservationTimeSeconds)
            {
                diagnostic = "The NPC has no Perception-backed last target position at or before contact loss.";
                return false;
            }

            intent = new PegaNpcLastKnownTargetContinuationIntent(
                subjectActorId,
                position,
                lastPosition.ObservationTimeSeconds,
                contact.ObservationTimeSeconds);
            diagnostic = string.Empty;
            return true;
        }

        public bool TryCreatePlanningCandidate(
            out PegaNpcPlanningTarget target,
            out PegaNpcPlanningActionCandidate candidate,
            out string diagnostic)
        {
            target = default;
            candidate = default;
            if (!TryGetKnownTargetEvidence(
                    out ActorId subjectActorId,
                    out Vector3 knownPosition,
                    out double evidenceTimeSeconds,
                    out diagnostic))
            {
                return false;
            }

            if (!TryDeferNavigationRoute(
                    PegaActorMovementSpeedRegime.Pursuit,
                    out PegaNpcNavigationRouteEstimate routeEstimate,
                    out diagnostic))
            {
                return false;
            }

            target = PegaNpcPlanningTarget.FromKnownActorPosition(
                subjectActorId,
                knownPosition,
                evidenceTimeSeconds);
            candidate = new PegaNpcPlanningActionCandidate(
                Contract,
                this,
                target.TargetId,
                PegaActorMovementSpeedRegime.Pursuit,
                routeEstimate,
                IntrinsicDurationSeconds);
            diagnostic = string.Empty;
            return true;
        }

        public override bool TryCreatePlanningCandidates(
            PegaNpcActionPlanningContext context,
            out IReadOnlyList<PegaNpcPlanningTarget> targets,
            out IReadOnlyList<PegaNpcPlanningActionCandidate> candidates,
            out string diagnostic)
        {
            List<PegaNpcPlanningTarget> targetList = new List<PegaNpcPlanningTarget>();
            List<PegaNpcPlanningActionCandidate> candidateList = new List<PegaNpcPlanningActionCandidate>();
            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());
            ActorId currentTargetActorId = context.TargetActorId;
            if (context.PlanContinuation.HasValue)
            {
                PegaNpcPlanContinuation continuation = context.PlanContinuation.Value;
                if (!currentTargetActorId.IsValid ||
                    continuation.LastKnownApproachAttempted || continuation.LastKnownPositionReached ||
                    currentTargetActorId != continuation.SubjectActorId ||
                    PlanarDistance(ActorTransform.position, continuation.LastKnownPosition) <= ArrivalRadiusMeters)
                {
                    diagnostic = string.Empty;
                    return true;
                }

                if (!TryCreatePlanningCandidate(out PegaNpcPlanningTarget candidateTarget,
                        out PegaNpcPlanningActionCandidate candidate, out diagnostic))
                {
                    return false;
                }

                if (candidateTarget.TargetId != PegaNpcPlanningTargetId.ForActor(continuation.SubjectActorId) ||
                    !candidateTarget.KnownPosition.Equals(continuation.LastKnownPosition) ||
                    Math.Abs(candidateTarget.EvidenceTimeSeconds - continuation.PositionEvidenceTimeSeconds) > 0.000001d)
                {
                    diagnostic = "The continuation Approach candidate is not bound to the preserved last Perception position.";
                    return false;
                }

                targetList.Add(candidateTarget);
                candidateList.Add(candidate);
                targets = Array.AsReadOnly(targetList.ToArray());
                candidates = Array.AsReadOnly(candidateList.ToArray());
                return true;
            }

            if (!currentTargetActorId.IsValid)
            {
                diagnostic = string.Empty;
                return true;
            }

            if (!context.HasPerceptionBackedTargetContact)
            {
                diagnostic = string.Empty;
                return true;
            }

            if (!TryCreatePlanningCandidateFromKnowledgeView(
                    context.KnowledgeReadView,
                    out PegaNpcPlanningTarget target,
                    out PegaNpcPlanningActionCandidate normalCandidate,
                    out diagnostic))
            {
                return false;
            }

            if (target.TargetId != PegaNpcPlanningTargetId.ForActor(currentTargetActorId))
            {
                diagnostic = "Approach candidate does not match the current target observation.";
                return false;
            }

            targets = Array.AsReadOnly(new[] { target });
            candidates = Array.AsReadOnly(new[] { normalCandidate });
            return true;
        }

        public override bool CanOfferCandidate(
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcPlanContinuation? continuation) =>
            CandidateMatchesContract(candidate) &&
            (!continuation.HasValue ||
             (!continuation.Value.LastKnownApproachAttempted &&
              !continuation.Value.LastKnownPositionReached &&
              candidate.TargetId == PegaNpcPlanningTargetId.ForActor(continuation.Value.SubjectActorId)));

        public override bool IsCurrentPlanValid(
            PegaNpcPlanSnapshot plan,
            PegaNpcPlanContinuation? continuation,
            out Vector3 currentKnownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            currentKnownPosition = default;
            evidenceTimeSeconds = 0d;
            diagnostic = string.Empty;
            if (continuation.HasValue)
            {
                PegaNpcPlanContinuation current = continuation.Value;
                if (plan.Purpose != PegaNpcPlanPurpose.PlanContinuationStep ||
                    plan.TargetId != PegaNpcPlanningTargetId.ForActor(current.SubjectActorId) ||
                    !IsCurrentContinuationIntent(current.ToIntent(), out diagnostic) ||
                    !TryGetKnownTargetPosition(out currentKnownPosition, out diagnostic) ||
                    !IsTargetPositionWithinTolerance(currentKnownPosition, plan.TargetPosition))
                {
                    diagnostic = string.IsNullOrEmpty(diagnostic)
                        ? "The Approach plan no longer matches its live pursuit continuation or known target."
                        : diagnostic;
                    return false;
                }

                evidenceTimeSeconds = current.PositionEvidenceTimeSeconds;
                diagnostic = string.Empty;
                return true;
            }

            if (plan.Purpose != PegaNpcPlanPurpose.DesireGoal ||
                plan.TargetId.IdentityKind != PegaNpcPlanningTargetIdentityKind.Actor ||
                !TryGetPlanningTargetEvidence(out ActorId currentSubject, out currentKnownPosition,
                    out evidenceTimeSeconds, out diagnostic) ||
                plan.TargetId != PegaNpcPlanningTargetId.ForActor(currentSubject) ||
                plan.TargetId != PegaNpcPlanningTargetId.ForActor(GetCurrentTargetActorId()) ||
                !IsTargetPositionWithinTolerance(currentKnownPosition, plan.TargetPosition))
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                        ? "The Approach plan no longer matches the current known target."
                    : diagnostic;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        public override bool TryStart(
            PegaNpcPlanSnapshot plan,
            PegaNpcPlanContinuation? continuation,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            bool continuationMatches = continuation.HasValue
                ? plan.Purpose == PegaNpcPlanPurpose.PlanContinuationStep &&
                  plan.TargetId == PegaNpcPlanningTargetId.ForActor(continuation.Value.SubjectActorId) &&
                  IsCurrentContinuationIntent(continuation.Value.ToIntent(), out diagnostic)
                : plan.Purpose == PegaNpcPlanPurpose.DesireGoal;
            Vector3 position;
            bool hasPosition = continuation.HasValue
                ? TryGetKnownTargetPosition(out position, out diagnostic)
                : TryGetPlanningTargetEvidence(out _, out position, out _, out diagnostic);
            if (plan.ActionId != Contract.ActionId || !continuationMatches ||
                !hasPosition ||
                !IsTargetPositionWithinTolerance(position, plan.TargetPosition))
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "The selected Approach plan is stale for this Action's known target."
                    : diagnostic;
                RejectStart(diagnostic);
                return false;
            }

            _runningPerceptionContinuation = continuation.HasValue;
            return TryStartNavigation(
                plan.TargetId,
                position,
                PegaActorMovementSpeedRegime.Pursuit,
                out diagnostic);
        }

        private ActorId GetCurrentTargetActorId() =>
            TryResolveTargetSubject(out ActorId actorId, out _) ? actorId : default;

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            Vector3 delta = left - right;
            delta.y = 0f;
            return delta.magnitude;
        }

        private bool TryGetKnownTargetEvidence(
            out ActorId subjectActorId,
            out Vector3 position,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            subjectActorId = default;
            position = default;
            evidenceTimeSeconds = 0d;
            if (!TryResolveTargetSubject(out subjectActorId, out diagnostic))
            {
                return false;
            }

            PegaKnowledgeFact<bool> playerFact = _knowledgeState.GetFact(
                PegaNpcKnowledgeFacts.IsPlayer.For(subjectActorId));
            if (!PegaNpcKnowledgeFacts.HasPerceptionBackedPlayerFact(playerFact))
            {
                diagnostic = "This NPC has no Perception-backed fact that the target is a Player.";
                return false;
            }

            PegaKnowledgeFact<Vector3> lastObservedPosition =
                _knowledgeState.GetLastObservedCharacterPosition(subjectActorId);
            if (!lastObservedPosition.TryGetValue(out position) ||
                !lastObservedPosition.HasObservationTime ||
                !lastObservedPosition.HasOrigin(PegaNpcEvidenceOrigin.Perception))
            {
                diagnostic = "This NPC has no valid last observed target position with evidence time.";
                return false;
            }

            evidenceTimeSeconds = lastObservedPosition.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        private bool TryGetPlanningTargetEvidence(
            out ActorId subjectActorId,
            out Vector3 position,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            return TryGetPlanningTargetEvidence(
                _knowledgeReadView,
                out subjectActorId,
                out position,
                out evidenceTimeSeconds,
                out diagnostic);
        }

        private bool TryGetPlanningTargetEvidence(
            PegaNpcKnowledgeReadView knowledgeView,
            out ActorId subjectActorId,
            out Vector3 position,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            subjectActorId = default;
            position = default;
            evidenceTimeSeconds = 0d;
            diagnostic = string.Empty;
            if (knowledgeView == null || !TryResolveTargetSubject(out subjectActorId, out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "A composed PEGA knowledge read view is required."
                    : diagnostic;
                return false;
            }

            PegaKnowledgeFact<Vector3> knownPosition =
                knowledgeView.GetKnownCharacterPosition(subjectActorId);
            PegaKnowledgeFact<bool> playerFact = knowledgeView.GetIsPlayer(subjectActorId);
            if (!PegaNpcKnowledgeFacts.HasPerceptionBackedPlayerFact(playerFact) ||
                !knownPosition.TryGetValue(out position) || !knownPosition.HasObservationTime ||
                !knownPosition.HasOrigin(PegaNpcEvidenceOrigin.Perception) ||
                !IsFinitePosition(position))
            {
                diagnostic = "The target's Player status or location is unknown in this NPC's Perception-backed Individual knowledge.";
                return false;
            }

            evidenceTimeSeconds = knownPosition.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        private static bool IsFinitePosition(Vector3 value) =>
            IsFiniteComponent(value.x) && IsFiniteComponent(value.y) &&
            IsFiniteComponent(value.z);

        private static bool IsFiniteComponent(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private bool TryCreatePlanningCandidateFromKnowledgeView(
            PegaNpcKnowledgeReadView knowledgeView,
            out PegaNpcPlanningTarget target,
            out PegaNpcPlanningActionCandidate candidate,
            out string diagnostic)
        {
            target = default;
            candidate = default;
            if (!TryGetPlanningTargetEvidence(
                    knowledgeView,
                    out ActorId subjectActorId,
                    out Vector3 knownPosition,
                    out double evidenceTimeSeconds,
                    out diagnostic))
            {
                return false;
            }

            if (!TryDeferNavigationRoute(
                    PegaActorMovementSpeedRegime.Pursuit,
                    out PegaNpcNavigationRouteEstimate routeEstimate,
                    out diagnostic))
            {
                return false;
            }

            target = PegaNpcPlanningTarget.FromKnownActorPosition(
                subjectActorId,
                knownPosition,
                evidenceTimeSeconds);
            candidate = new PegaNpcPlanningActionCandidate(
                Contract,
                this,
                target.TargetId,
                PegaActorMovementSpeedRegime.Pursuit,
                routeEstimate,
                IntrinsicDurationSeconds);
            diagnostic = string.Empty;
            return true;
        }

        public bool TryStart(out string diagnostic)
        {
            if (!TryGetPlanningTargetEvidence(out _, out Vector3 lastKnownPosition, out _, out diagnostic))
            {
                RejectStart(diagnostic);
                return false;
            }

            return TryStartNavigation(
                PegaNpcPlanningTargetId.ForActor(GetCurrentTargetActorId()),
                lastKnownPosition,
                PegaActorMovementSpeedRegime.Pursuit,
                out diagnostic);
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic) =>
            _runningPerceptionContinuation
                ? TryGetKnownTargetPosition(out _, out diagnostic)
                : TryGetPlanningTargetEvidence(out _, out _, out _, out diagnostic);

        private bool TryResolveTargetSubject(out ActorId subjectActorId, out string diagnostic)
        {
            subjectActorId = default;
            diagnostic = string.Empty;
            if (_knowledgeState == null || _knowledgeState.gameObject != ActorRoot)
            {
                diagnostic = "The Action requires this NPC's PEGANpcKnowledgeState on the same root.";
                return false;
            }

            if (_targetReference == null || _targetReference.gameObject != ActorRoot)
            {
                diagnostic = "The Action requires an explicit target Actor occurrence reference on the same root.";
                return false;
            }

            if (!_targetReference.TryGetCurrent(out ActorOccurrenceResolution resolution))
            {
                diagnostic = $"The configured target Actor occurrence is not currently resolved: {resolution.Status}. {resolution.Diagnostic}";
                return false;
            }

            subjectActorId = resolution.Occurrence.ActorId;
            if (!subjectActorId.IsValid)
            {
                diagnostic = "The current target Actor occurrence has no valid ActorId.";
                subjectActorId = default;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private void EnsureContinuationIntentEventsInitialized()
        {
            _continuationIntentEvents ??= new EventBus<PegaNpcLastKnownTargetContinuationIntentRequestedEvent>();
        }

        private void EnsureIntentLoggerInitialized()
        {
            _intentLog ??= PegaLogging.CreateScopedLogger<PegaNpcApproachKnownCharacterAction>(
                IntentLogCategory,
                ActorRoot.name);
        }

        private void LogIntentSubscriberFailure(
            Action<PegaNpcLastKnownTargetContinuationIntentRequestedEvent> handler,
            PegaNpcLastKnownTargetContinuationIntentRequestedEvent intentEvent,
            Exception exception)
        {
            EnsureIntentLoggerInitialized();
            _intentLog.Error(
                "Lost-contact pursuit intent subscriber failed; other subscribers remain isolated.",
                exception,
                LogFields.Field("actorRoot", ActorRoot.name),
                LogFields.Field("subscriber", handler.Method.DeclaringType?.FullName),
                LogFields.Field("subjectActorId", intentEvent.Intent.SubjectActorId),
                LogFields.Field("targetPosition", intentEvent.Intent.LastKnownPosition));
        }
    }
}

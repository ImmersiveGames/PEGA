using System;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    [CreateAssetMenu(fileName = "ApproachKnownCharacter", menuName = "PEGA/NPC/Movement/Approach Known Character")]
    public sealed class PEGANpcApproachKnownCharacterAction : PEGANpcNavigationAction,
        IPEGANpcLastKnownTargetContinuationProvider,
        IPEGANpcKnowledgeChangeRelevanceProvider
    {
        private const string IntentLogCategory = "PEGA.Gameplay.NPC.TargetContinuation";

        protected override PEGANpcActionEffectKind ActionEffectKind =>
            PEGANpcActionEffectKind.ApproachKnownCharacterPosition;

        public override PEGANpcActionContract Contract => new PEGANpcActionContract(
            PEGANpcPlanningActionId.ApproachKnownCharacter,
            ActionEffectKind,
            PEGANpcPlanningTargetKind.CharacterPosition,
            true,
            "NPC began approaching a known character.");

        [System.NonSerialized]
        private PEGANpcKnowledgeState knowledgeState;

        [System.NonSerialized]
        private ActorOccurrenceReferenceBinding targetReference;

        [System.NonSerialized]
        private IEventBinding _knowledgeChangeBinding;

        [System.NonSerialized]
        private EventBus<PEGANpcLastKnownTargetContinuationIntentRequestedEvent> _continuationIntentEvents;

        [System.NonSerialized]
        private ScopedLogger _intentLog;

        public IEventBinding SubscribeToLastKnownTargetContinuationIntents(
            Action<PEGANpcLastKnownTargetContinuationIntentRequestedEvent> handler)
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

            knowledgeState = RuntimeContext.KnowledgeState;
            targetReference = RuntimeContext.ActorReference;
            if (knowledgeState == null || knowledgeState.gameObject != ActorRoot ||
                targetReference == null || targetReference.gameObject != ActorRoot)
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
            _knowledgeChangeBinding ??= knowledgeState.SubscribeToKnowledgeChanges(OnKnowledgeChanged);
            base.OnRuntimeEnabled();
        }

        protected override void OnRuntimeDisabled()
        {
            if (knowledgeState != null && _knowledgeChangeBinding != null)
            {
                knowledgeState.UnsubscribeFromKnowledgeChanges(_knowledgeChangeBinding);
                _knowledgeChangeBinding = null;
            }

            base.OnRuntimeDisabled();
        }

        protected override void OnRuntimeDestroyed()
        {
            if (knowledgeState != null && _knowledgeChangeBinding != null)
            {
                knowledgeState.UnsubscribeFromKnowledgeChanges(_knowledgeChangeBinding);
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

        public bool IsRelevantKnowledgeChange(PEGANpcKnowledgeFactChangedEvent change)
        {
            if (change == null || !change.HasValueOrStatusChanged ||
                !change.SubjectActorId.HasValue ||
                !TryResolveTargetSubject(out ActorId subjectActorId, out _) ||
                change.SubjectActorId.Value != subjectActorId)
            {
                return false;
            }

            return change.FactKind == PEGANpcKnowledgeFactKind.ObservedCharacterIdentity ||
                change.FactKind == PEGANpcKnowledgeFactKind.CharacterVisualContact ||
                change.FactKind == PEGANpcKnowledgeFactKind.LastObservedCharacterPosition;
        }

        public bool IsCurrentContinuationIntent(
            PEGANpcLastKnownTargetContinuationIntent intent,
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

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                knowledgeState.GetObservedCharacterIdentity(intent.SubjectActorId);
            PEGANpcKnowledgeFact<bool> contact =
                knowledgeState.GetCharacterVisualContact(intent.SubjectActorId);
            PEGANpcKnowledgeFact<Vector3> lastPosition =
                knowledgeState.GetLastObservedCharacterPosition(intent.SubjectActorId);
            if (!identity.TryGetValue(out _) ||
                !identity.HasObservationTime ||
                (identity.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                diagnostic = "The current Knowledge State does not contain Perception-backed target identity.";
                return false;
            }

            if (identity.ObservationTimeSeconds > intent.ContactLossObservationTimeSeconds + 0.000001d)
            {
                diagnostic = "Newer Perception observations superseded the last-known target continuation.";
                return false;
            }

            if (!contact.TryGetValue(out bool hasVisualContact) ||
                hasVisualContact ||
                !contact.HasObservationTime ||
                Math.Abs(contact.ObservationTimeSeconds - intent.ContactLossObservationTimeSeconds) > 0.000001d ||
                (contact.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                diagnostic = "The current Knowledge State no longer matches the intent's Perception-backed visual-contact loss.";
                return false;
            }

            if (!lastPosition.TryGetValue(out Vector3 knownPosition) ||
                !knownPosition.Equals(intent.LastKnownPosition) ||
                !lastPosition.HasObservationTime ||
                Math.Abs(lastPosition.ObservationTimeSeconds - intent.LastKnownObservationTimeSeconds) > 0.000001d ||
                lastPosition.ObservationTimeSeconds > intent.ContactLossObservationTimeSeconds + 0.000001d ||
                (lastPosition.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                diagnostic = "The intent's last position is not the current Perception-backed position preserved by this NPC.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        public bool IsContinuationSupersededByNewRecognition(
            PEGANpcLastKnownTargetContinuationIntent intent)
        {
            if (!intent.IsValid ||
                !TryResolveTargetSubject(out ActorId currentSubjectActorId, out _) ||
                currentSubjectActorId != intent.SubjectActorId)
            {
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                knowledgeState.GetObservedCharacterIdentity(intent.SubjectActorId);
            return identity.TryGetValue(out _) &&
                identity.HasObservationTime &&
                identity.ObservationTimeSeconds > intent.ContactLossObservationTimeSeconds + 0.000001d &&
                (identity.Origins & PEGANpcEvidenceOrigin.Perception) != 0;
        }

        private void OnKnowledgeChanged(PEGANpcKnowledgeFactChangedEvent change)
        {
            if (change == null ||
                change.FactKind != PEGANpcKnowledgeFactKind.CharacterVisualContact ||
                !change.SubjectActorId.HasValue ||
                change.EvidenceOrigin != PEGANpcEvidenceOrigin.Perception)
            {
                return;
            }

            PEGANpcKnowledgeFact<bool> contact =
                knowledgeState.GetCharacterVisualContact(change.SubjectActorId.Value);
            if (!contact.TryGetValue(out bool hasVisualContact) || hasVisualContact)
            {
                return;
            }

            if (!TryCreateLastKnownTargetContinuationIntent(
                    change,
                    out PEGANpcLastKnownTargetContinuationIntent intent,
                    out string diagnostic))
            {
                EnsureIntentLoggerInitialized();
                _intentLog.Warning(
                    "Lost-contact pursuit intent was not published.",
                    LogFields.Field("actorRoot", ActorRoot.name),
                    LogFields.Field("subjectActorId", change.SubjectActorId.Value),
                    LogFields.Field("observationTimeSeconds", change.ObservationTimeSeconds),
                    LogFields.Field("diagnostic", diagnostic));
                return;
            }

            EnsureContinuationIntentEventsInitialized();
            _continuationIntentEvents.Publish(
                new PEGANpcLastKnownTargetContinuationIntentRequestedEvent(intent));

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
            PEGANpcKnowledgeFactChangedEvent contactChange,
            out PEGANpcLastKnownTargetContinuationIntent intent,
            out string diagnostic)
        {
            intent = default;
            diagnostic = string.Empty;
            if (contactChange == null ||
                contactChange.FactKind != PEGANpcKnowledgeFactKind.CharacterVisualContact ||
                !contactChange.SubjectActorId.HasValue ||
                !contactChange.HasValueOrStatusChanged ||
                contactChange.PreviousStatus != PEGANpcKnowledgeStatus.Known ||
                contactChange.CurrentStatus != PEGANpcKnowledgeStatus.Known ||
                contactChange.EvidenceOrigin != PEGANpcEvidenceOrigin.Perception)
            {
                diagnostic = "A changed Known-to-Known target visual-contact event from Perception is required.";
                return false;
            }

            if (knowledgeState == null || knowledgeState.gameObject != ActorRoot)
            {
                diagnostic = "The Action requires this NPC's PEGANpcKnowledgeState on the same root.";
                return false;
            }

            ActorId subjectActorId = contactChange.SubjectActorId.Value;
            if (!subjectActorId.IsValid)
            {
                diagnostic = "The contact-loss event has no valid target subject ActorId.";
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                knowledgeState.GetObservedCharacterIdentity(subjectActorId);
            if (!identity.TryGetValue(out _) ||
                (identity.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                diagnostic = "The subject identity is not known from authorized Perception evidence.";
                return false;
            }

            PEGANpcKnowledgeFact<bool> contact = knowledgeState.GetCharacterVisualContact(subjectActorId);
            if (!contact.TryGetValue(out bool hasVisualContact) ||
                hasVisualContact ||
                !contact.HasObservationTime ||
                contact.ObservationTimeSeconds != contactChange.ObservationTimeSeconds ||
                (contact.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                diagnostic = "The current Knowledge State does not confirm this Perception event as a current visual-contact loss.";
                return false;
            }

            PEGANpcKnowledgeFact<Vector3> lastPosition =
                knowledgeState.GetLastObservedCharacterPosition(subjectActorId);
            if (!lastPosition.TryGetValue(out Vector3 position) ||
                !lastPosition.HasObservationTime ||
                (lastPosition.Origins & PEGANpcEvidenceOrigin.Perception) == 0 ||
                lastPosition.ObservationTimeSeconds > contact.ObservationTimeSeconds)
            {
                diagnostic = "The NPC has no Perception-backed last target position at or before contact loss.";
                return false;
            }

            intent = new PEGANpcLastKnownTargetContinuationIntent(
                subjectActorId,
                position,
                lastPosition.ObservationTimeSeconds,
                contact.ObservationTimeSeconds);
            diagnostic = string.Empty;
            return true;
        }

        public bool TryCreatePlanningCandidate(
            out PEGANpcPlanningTarget target,
            out PEGANpcPlanningActionCandidate candidate,
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
                    PEGA.Actors.PEGAActorMovementSpeedRegime.Pursuit,
                    out PEGANpcNavigationRouteEstimate routeEstimate,
                    out diagnostic))
            {
                return false;
            }

            target = PEGANpcPlanningTarget.FromKnownActorPosition(
                subjectActorId,
                knownPosition,
                evidenceTimeSeconds);
            candidate = new PEGANpcPlanningActionCandidate(
                Contract,
                this,
                target.TargetId,
                PEGA.Actors.PEGAActorMovementSpeedRegime.Pursuit,
                routeEstimate,
                IntrinsicDurationSeconds);
            diagnostic = string.Empty;
            return true;
        }

        public override bool TryCreatePlanningCandidates(
            PEGANpcActionPlanningContext context,
            out IReadOnlyList<PEGANpcPlanningTarget> targets,
            out IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
            out string diagnostic)
        {
            List<PEGANpcPlanningTarget> targetList = new List<PEGANpcPlanningTarget>();
            List<PEGANpcPlanningActionCandidate> candidateList = new List<PEGANpcPlanningActionCandidate>();
            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());
            ActorId currentTargetActorId = context.TargetActorId;
            if (context.PlanContinuation.HasValue)
            {
                PEGANpcPlanContinuation continuation = context.PlanContinuation.Value;
                if (!currentTargetActorId.IsValid ||
                    continuation.LastKnownApproachAttempted || continuation.LastKnownPositionReached ||
                    currentTargetActorId != continuation.SubjectActorId ||
                    PlanarDistance(ActorTransform.position, continuation.LastKnownPosition) <= ArrivalRadiusMeters)
                {
                    diagnostic = string.Empty;
                    return true;
                }

                if (!TryCreatePlanningCandidate(out PEGANpcPlanningTarget candidateTarget,
                        out PEGANpcPlanningActionCandidate candidate, out diagnostic))
                {
                    return false;
                }

                if (candidateTarget.TargetId != PEGANpcPlanningTargetId.ForActor(continuation.SubjectActorId) ||
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

            if (!currentTargetActorId.IsValid || !context.HasPerceptionBackedTargetContact)
            {
                diagnostic = string.Empty;
                return true;
            }

            if (!TryCreatePlanningCandidate(out PEGANpcPlanningTarget target,
                    out PEGANpcPlanningActionCandidate normalCandidate, out diagnostic))
            {
                return false;
            }

            if (target.TargetId != PEGANpcPlanningTargetId.ForActor(currentTargetActorId))
            {
                diagnostic = "Approach candidate does not match the current target observation.";
                return false;
            }

            targets = Array.AsReadOnly(new[] { target });
            candidates = Array.AsReadOnly(new[] { normalCandidate });
            return true;
        }

        public override bool CanOfferCandidate(
            PEGANpcPlanningActionCandidate candidate,
            PEGANpcPlanContinuation? continuation) =>
            CandidateMatchesContract(candidate) &&
            (!continuation.HasValue ||
             (!continuation.Value.LastKnownApproachAttempted &&
              !continuation.Value.LastKnownPositionReached &&
              candidate.TargetId == PEGANpcPlanningTargetId.ForActor(continuation.Value.SubjectActorId)));

        public override bool IsCurrentPlanValid(
            PEGANpcPlanSnapshot plan,
            PEGANpcPlanContinuation? continuation,
            out Vector3 currentKnownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            currentKnownPosition = default;
            evidenceTimeSeconds = 0d;
            diagnostic = string.Empty;
            if (continuation.HasValue)
            {
                PEGANpcPlanContinuation current = continuation.Value;
                if (plan.Purpose != PEGANpcPlanPurpose.PlanContinuationStep ||
                    plan.TargetId != PEGANpcPlanningTargetId.ForActor(current.SubjectActorId) ||
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

            if (plan.Purpose != PEGANpcPlanPurpose.DesireGoal ||
                plan.TargetId.IdentityKind != PEGANpcPlanningTargetIdentityKind.Actor ||
                !TryGetKnownTargetPosition(out currentKnownPosition, out diagnostic) ||
                plan.TargetId != PEGANpcPlanningTargetId.ForActor(GetCurrentTargetActorId()) ||
                !IsTargetPositionWithinTolerance(currentKnownPosition, plan.TargetPosition))
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                        ? "The Approach plan no longer matches the current known target."
                    : diagnostic;
                return false;
            }

            evidenceTimeSeconds = plan.EvidenceTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        public override bool TryStart(
            PEGANpcPlanSnapshot plan,
            PEGANpcPlanContinuation? continuation,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            bool continuationMatches = continuation.HasValue
                ? plan.Purpose == PEGANpcPlanPurpose.PlanContinuationStep &&
                  plan.TargetId == PEGANpcPlanningTargetId.ForActor(continuation.Value.SubjectActorId) &&
                  IsCurrentContinuationIntent(continuation.Value.ToIntent(), out diagnostic)
                : plan.Purpose == PEGANpcPlanPurpose.DesireGoal;
            if (plan.ActionId != Contract.ActionId || !continuationMatches ||
                !TryGetKnownTargetPosition(out Vector3 position, out diagnostic) ||
                !IsTargetPositionWithinTolerance(position, plan.TargetPosition))
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "The selected Approach plan is stale for this Action's known target."
                    : diagnostic;
                RejectStart(diagnostic);
                return false;
            }

            return TryStartNavigation(
                plan.TargetId,
                position,
                PEGA.Actors.PEGAActorMovementSpeedRegime.Pursuit,
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

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                knowledgeState.GetObservedCharacterIdentity(subjectActorId);
            if (!identity.TryGetValue(out _) ||
                (identity.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                if (string.IsNullOrEmpty(diagnostic))
                {
                    diagnostic = "The current target identity is not known from authorized Perception evidence.";
                }

                return false;
            }

            PEGANpcKnowledgeFact<Vector3> lastObservedPosition =
                knowledgeState.GetLastObservedCharacterPosition(subjectActorId);
            if (!lastObservedPosition.TryGetValue(out position) || !lastObservedPosition.HasObservationTime)
            {
                diagnostic = "This NPC has no valid last observed target position with evidence time.";
                return false;
            }

            evidenceTimeSeconds = lastObservedPosition.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        public bool TryStart(out string diagnostic)
        {
            if (!TryGetKnownTargetPosition(out Vector3 lastKnownPosition, out diagnostic))
            {
                RejectStart(diagnostic);
                return false;
            }

            return TryStartNavigation(
                PEGANpcPlanningTargetId.ForActor(GetCurrentTargetActorId()),
                lastKnownPosition,
                PEGA.Actors.PEGAActorMovementSpeedRegime.Pursuit,
                out diagnostic);
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic) =>
            TryGetKnownTargetPosition(out _, out diagnostic);

        private bool TryResolveTargetSubject(out ActorId subjectActorId, out string diagnostic)
        {
            subjectActorId = default;
            diagnostic = string.Empty;
            if (knowledgeState == null || knowledgeState.gameObject != ActorRoot)
            {
                diagnostic = "The Action requires this NPC's PEGANpcKnowledgeState on the same root.";
                return false;
            }

            if (targetReference == null || targetReference.gameObject != ActorRoot)
            {
                diagnostic = "The Action requires an explicit target Actor occurrence reference on the same root.";
                return false;
            }

            if (!targetReference.TryGetCurrent(out ActorOccurrenceResolution resolution))
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
            _continuationIntentEvents ??= new EventBus<PEGANpcLastKnownTargetContinuationIntentRequestedEvent>();
        }

        private void EnsureIntentLoggerInitialized()
        {
            _intentLog ??= PEGALogging.CreateScopedLogger<PEGANpcApproachKnownCharacterAction>(
                IntentLogCategory,
                ActorRoot.name);
        }

        private void LogIntentSubscriberFailure(
            Action<PEGANpcLastKnownTargetContinuationIntentRequestedEvent> handler,
            PEGANpcLastKnownTargetContinuationIntentRequestedEvent intentEvent,
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

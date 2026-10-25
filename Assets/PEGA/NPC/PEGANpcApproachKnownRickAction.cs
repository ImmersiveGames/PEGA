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
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Actions/Approach Known Rick")]
    public sealed class PEGANpcApproachKnownRickAction : PEGANpcNavigationAction,
        IPEGANpcLostContactPursuitProvider,
        IPEGANpcKnowledgeChangeRelevanceProvider
    {
        private const string IntentLogCategory = "PEGA.Gameplay.NPC.PursuitIntent";

        protected override PEGANpcActionEffectKind ActionEffectKind =>
            PEGANpcActionEffectKind.ApproachKnownRickPosition;

        protected override PEGANpcDesireGoalKind ActionGoalKind =>
            PEGANpcDesireGoalKind.ReachKnownCharacterPosition;

        public override PEGANpcActionContract Contract => new PEGANpcActionContract(
            PEGANpcPlanningActionId.ApproachKnownRick,
            ActionEffectKind,
            ActionGoalKind,
            PEGANpcPlanningTargetKind.CharacterPosition,
            true,
            "NPC began pursuing Rick.");

        [Header("Known Target")]
        [SerializeField]
        [Tooltip("Conhecimento mantido por este NPC.")]
        private PEGANpcKnowledgeState knowledgeState;

        [SerializeField]
        [Tooltip("Referência explícita ao Player cuja identidade deve estar reconhecida como Rick.")]
        private ActorOccurrenceReferenceBinding rickReference;

        [SerializeField]
        [Tooltip("Actor Profile que identifica Rick no Knowledge State.")]
        private ActorProfile rickActorProfile;

        [System.NonSerialized]
        private IEventBinding _knowledgeChangeBinding;

        [System.NonSerialized]
        private EventBus<PEGANpcLastKnownRickPursuitIntentRequestedEvent> _pursuitIntentEvents;

        [System.NonSerialized]
        private ScopedLogger _intentLog;

        public IEventBinding SubscribeToLostContactPursuitIntents(
            Action<PEGANpcLastKnownRickPursuitIntentRequestedEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsurePursuitIntentEventsInitialized();
            return _pursuitIntentEvents.Subscribe(intentEvent =>
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

        public bool UnsubscribeFromLostContactPursuitIntents(IEventBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            return _pursuitIntentEvents != null && _pursuitIntentEvents.Unsubscribe(binding);
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureIntentLoggerInitialized();
        }

        private void OnEnable()
        {
            if (knowledgeState == null || knowledgeState.gameObject != gameObject)
            {
                EnsureIntentLoggerInitialized();
                _intentLog.Warning(
                    "Lost-contact pursuit intent source requires this NPC's Knowledge State on the same root.",
                    LogFields.Field("actorRoot", gameObject.name));
                return;
            }

            _knowledgeChangeBinding ??= knowledgeState.SubscribeToKnowledgeChanges(OnKnowledgeChanged);
        }

        protected override void OnDisable()
        {
            if (knowledgeState != null && _knowledgeChangeBinding != null)
            {
                knowledgeState.UnsubscribeFromKnowledgeChanges(_knowledgeChangeBinding);
                _knowledgeChangeBinding = null;
            }

            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (knowledgeState != null && _knowledgeChangeBinding != null)
            {
                knowledgeState.UnsubscribeFromKnowledgeChanges(_knowledgeChangeBinding);
                _knowledgeChangeBinding = null;
            }

            _pursuitIntentEvents?.Clear();
            _pursuitIntentEvents = null;
            base.OnDestroy();
        }

        public bool TryGetKnownRickPosition(out Vector3 position, out string diagnostic)
        {
            return TryGetKnownRickEvidence(out _, out position, out _, out diagnostic);
        }

        public bool IsRelevantKnowledgeChange(PEGANpcKnowledgeFactChangedEvent change)
        {
            if (change == null || !change.HasValueOrStatusChanged ||
                !change.SubjectActorId.HasValue ||
                !TryResolveRickSubject(out ActorId subjectActorId, out _) ||
                change.SubjectActorId.Value != subjectActorId)
            {
                return false;
            }

            return change.FactKind == PEGANpcKnowledgeFactKind.ObservedCharacterIdentity ||
                change.FactKind == PEGANpcKnowledgeFactKind.RickVisualContact ||
                change.FactKind == PEGANpcKnowledgeFactKind.LastObservedRickPosition;
        }

        public bool IsCurrentLostContactPursuitIntent(
            PEGANpcLastKnownRickPursuitIntent intent,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!intent.IsValid || !TryResolveRickSubject(out ActorId currentSubjectActorId, out diagnostic) ||
                currentSubjectActorId != intent.SubjectActorId)
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "The intent does not identify this Action's current Player occurrence."
                    : diagnostic;
                return false;
            }

            if (rickActorProfile == null ||
                !rickActorProfile.TryGetActorProfileId(out ActorProfileId rickProfileId, out diagnostic) ||
                !rickProfileId.IsValid)
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "The Action has no valid configured Rick Actor Profile."
                    : diagnostic;
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                knowledgeState.GetObservedCharacterIdentity(intent.SubjectActorId);
            PEGANpcKnowledgeFact<bool> contact =
                knowledgeState.GetRickVisualContact(intent.SubjectActorId);
            PEGANpcKnowledgeFact<Vector3> lastPosition =
                knowledgeState.GetLastObservedRickPosition(intent.SubjectActorId);
            if (!identity.TryGetValue(out ActorProfileId observedProfileId) ||
                observedProfileId != rickProfileId ||
                !identity.HasObservationTime ||
                (identity.Origins & PEGANpcEvidenceOrigin.Perception) == 0)
            {
                diagnostic = "The current Knowledge State does not contain Perception-backed recognition of Rick.";
                return false;
            }

            if (identity.ObservationTimeSeconds > intent.ContactLossObservationTimeSeconds + 0.000001d)
            {
                diagnostic = "Newer Perception recognized Rick after the lost-contact event; its continuation is superseded.";
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

        public bool IsLostContactIntentSupersededByNewRecognition(
            PEGANpcLastKnownRickPursuitIntent intent)
        {
            if (!intent.IsValid ||
                !TryResolveRickSubject(out ActorId currentSubjectActorId, out _) ||
                currentSubjectActorId != intent.SubjectActorId ||
                rickActorProfile == null ||
                !rickActorProfile.TryGetActorProfileId(out ActorProfileId rickProfileId, out _) ||
                !rickProfileId.IsValid)
            {
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                knowledgeState.GetObservedCharacterIdentity(intent.SubjectActorId);
            return identity.TryGetValue(out ActorProfileId observedProfileId) &&
                observedProfileId == rickProfileId &&
                identity.HasObservationTime &&
                identity.ObservationTimeSeconds > intent.ContactLossObservationTimeSeconds + 0.000001d &&
                (identity.Origins & PEGANpcEvidenceOrigin.Perception) != 0;
        }

        private void OnKnowledgeChanged(PEGANpcKnowledgeFactChangedEvent change)
        {
            if (change == null ||
                change.FactKind != PEGANpcKnowledgeFactKind.RickVisualContact ||
                !change.SubjectActorId.HasValue ||
                change.EvidenceOrigin != PEGANpcEvidenceOrigin.Perception)
            {
                return;
            }

            PEGANpcKnowledgeFact<bool> contact =
                knowledgeState.GetRickVisualContact(change.SubjectActorId.Value);
            if (!contact.TryGetValue(out bool hasVisualContact) || hasVisualContact)
            {
                return;
            }

            if (!TryCreateLostContactPursuitIntent(
                    change,
                    out PEGANpcLastKnownRickPursuitIntent intent,
                    out string diagnostic))
            {
                EnsureIntentLoggerInitialized();
                _intentLog.Warning(
                    "Lost-contact pursuit intent was not published.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("subjectActorId", change.SubjectActorId.Value),
                    LogFields.Field("observationTimeSeconds", change.ObservationTimeSeconds),
                    LogFields.Field("diagnostic", diagnostic));
                return;
            }

            EnsurePursuitIntentEventsInitialized();
            _pursuitIntentEvents.Publish(
                new PEGANpcLastKnownRickPursuitIntentRequestedEvent(intent));

            EnsureIntentLoggerInitialized();
            _intentLog.Debug(
                "NPC requested continuation of Rick pursuit at the last Perception-backed position.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("subjectActorId", intent.SubjectActorId),
                LogFields.Field("actionId", intent.ActionId),
                LogFields.Field("targetPosition", intent.LastKnownPosition),
                LogFields.Field("positionEvidenceTimeSeconds", intent.LastKnownObservationTimeSeconds),
                LogFields.Field("contactLossTimeSeconds", intent.ContactLossObservationTimeSeconds));
        }

        /// <summary>
        /// Produces the continuation intent for a confirmed visual-contact loss.
        /// The destination comes only from this NPC's Knowledge State; this method never reads Rick's current transform.
        /// Call it while handling the corresponding N01 knowledge-change event.
        /// </summary>
        private bool TryCreateLostContactPursuitIntent(
            PEGANpcKnowledgeFactChangedEvent contactChange,
            out PEGANpcLastKnownRickPursuitIntent intent,
            out string diagnostic)
        {
            intent = default;
            diagnostic = string.Empty;
            if (contactChange == null ||
                contactChange.FactKind != PEGANpcKnowledgeFactKind.RickVisualContact ||
                !contactChange.SubjectActorId.HasValue ||
                !contactChange.HasValueOrStatusChanged ||
                contactChange.PreviousStatus != PEGANpcKnowledgeStatus.Known ||
                contactChange.CurrentStatus != PEGANpcKnowledgeStatus.Known ||
                contactChange.EvidenceOrigin != PEGANpcEvidenceOrigin.Perception)
            {
                diagnostic = "A changed Known-to-Known Rick visual-contact event from Perception is required.";
                return false;
            }

            if (knowledgeState == null || knowledgeState.gameObject != gameObject)
            {
                diagnostic = "The Action requires this NPC's PEGANpcKnowledgeState on the same root.";
                return false;
            }

            ActorId subjectActorId = contactChange.SubjectActorId.Value;
            if (!subjectActorId.IsValid)
            {
                diagnostic = "The contact-loss event has no valid Rick subject ActorId.";
                return false;
            }

            if (rickActorProfile == null ||
                !rickActorProfile.TryGetActorProfileId(out ActorProfileId rickProfileId, out diagnostic) ||
                !rickProfileId.IsValid)
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "The Action requires a valid Rick Actor Profile."
                    : diagnostic;
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                knowledgeState.GetObservedCharacterIdentity(subjectActorId);
            if (!identity.TryGetValue(out ActorProfileId observedProfileId) ||
                observedProfileId != rickProfileId)
            {
                diagnostic = "The subject is not legitimately known as the configured Rick Actor Profile.";
                return false;
            }

            PEGANpcKnowledgeFact<bool> contact = knowledgeState.GetRickVisualContact(subjectActorId);
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
                knowledgeState.GetLastObservedRickPosition(subjectActorId);
            if (!lastPosition.TryGetValue(out Vector3 position) ||
                !lastPosition.HasObservationTime ||
                (lastPosition.Origins & PEGANpcEvidenceOrigin.Perception) == 0 ||
                lastPosition.ObservationTimeSeconds > contact.ObservationTimeSeconds)
            {
                diagnostic = "The NPC has no Perception-backed last Rick position at or before contact loss.";
                return false;
            }

            intent = new PEGANpcLastKnownRickPursuitIntent(
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
            if (!TryGetKnownRickEvidence(
                    out ActorId subjectActorId,
                    out Vector3 knownPosition,
                    out double evidenceTimeSeconds,
                    out diagnostic))
            {
                return false;
            }

            if (!TryEstimateNavigationRoute(
                    knownPosition,
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
                true,
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
            if (context.PursuitContinuation.HasValue)
            {
                PEGANpcPursuitContinuation continuation = context.PursuitContinuation.Value;
                if (continuation.LastKnownApproachAttempted || continuation.LastKnownPositionReached ||
                    context.PlayerActorId != continuation.SubjectActorId ||
                    PlanarDistance(transform.position, continuation.LastKnownPosition) <= ArrivalRadiusMeters)
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

            if (!context.PlayerActorId.IsValid || !context.HasPerceptionBackedVisualContact)
            {
                diagnostic = string.Empty;
                return true;
            }

            if (!TryCreatePlanningCandidate(out PEGANpcPlanningTarget target,
                    out PEGANpcPlanningActionCandidate normalCandidate, out diagnostic))
            {
                return false;
            }

            if (target.TargetId != PEGANpcPlanningTargetId.ForActor(context.PlayerActorId))
            {
                diagnostic = "Approach candidate does not match the current Player Actor occurrence.";
                return false;
            }

            targets = Array.AsReadOnly(new[] { target });
            candidates = Array.AsReadOnly(new[] { normalCandidate });
            return true;
        }

        public override bool CanOfferCandidate(
            PEGANpcPlanningActionCandidate candidate,
            PEGANpcPursuitContinuation? continuation) =>
            CandidateMatchesContract(candidate) &&
            (!continuation.HasValue ||
             (!continuation.Value.LastKnownApproachAttempted &&
              !continuation.Value.LastKnownPositionReached &&
              candidate.TargetId == PEGANpcPlanningTargetId.ForActor(continuation.Value.SubjectActorId)));

        public override bool IsCurrentPlanValid(
            PEGANpcPlanSnapshot plan,
            PEGANpcPursuitContinuation? continuation,
            out Vector3 currentKnownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            currentKnownPosition = default;
            evidenceTimeSeconds = 0d;
            diagnostic = string.Empty;
            if (continuation.HasValue)
            {
                PEGANpcPursuitContinuation current = continuation.Value;
                if (plan.Purpose != PEGANpcPlanPurpose.PursuitContinuationStep ||
                    plan.TargetId != PEGANpcPlanningTargetId.ForActor(current.SubjectActorId) ||
                    !IsCurrentLostContactPursuitIntent(current.ToIntent(), out diagnostic) ||
                    !TryGetKnownRickPosition(out currentKnownPosition, out diagnostic) ||
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
                !TryGetKnownRickPosition(out currentKnownPosition, out diagnostic) ||
                plan.TargetId != PEGANpcPlanningTargetId.ForActor(GetCurrentRickActorId()) ||
                !IsTargetPositionWithinTolerance(currentKnownPosition, plan.TargetPosition))
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "The Approach plan no longer matches the current known Player target."
                    : diagnostic;
                return false;
            }

            evidenceTimeSeconds = plan.EvidenceTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        public override bool TryStart(
            PEGANpcPlanSnapshot plan,
            PEGANpcPursuitContinuation? continuation,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            bool continuationMatches = continuation.HasValue
                ? plan.Purpose == PEGANpcPlanPurpose.PursuitContinuationStep &&
                  plan.TargetId == PEGANpcPlanningTargetId.ForActor(continuation.Value.SubjectActorId) &&
                  IsCurrentLostContactPursuitIntent(continuation.Value.ToIntent(), out diagnostic)
                : plan.Purpose == PEGANpcPlanPurpose.DesireGoal;
            if (plan.ActionId != Contract.ActionId || !continuationMatches ||
                !TryGetKnownRickPosition(out Vector3 position, out diagnostic) ||
                !IsTargetPositionWithinTolerance(position, plan.TargetPosition))
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "The selected Approach plan is stale for this Action's known target."
                    : diagnostic;
                RejectStart(diagnostic);
                return false;
            }

            return TryStartNavigation(position, PEGA.Actors.PEGAActorMovementSpeedRegime.Pursuit, out diagnostic);
        }

        private ActorId GetCurrentRickActorId() =>
            TryResolveRickSubject(out ActorId actorId, out _) ? actorId : default;

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            Vector3 delta = left - right;
            delta.y = 0f;
            return delta.magnitude;
        }

        private bool TryGetKnownRickEvidence(
            out ActorId subjectActorId,
            out Vector3 position,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            subjectActorId = default;
            position = default;
            evidenceTimeSeconds = 0d;
            if (!TryResolveRickSubject(out subjectActorId, out diagnostic))
            {
                return false;
            }

            PEGANpcKnowledgeFact<ActorProfileId> identity =
                knowledgeState.GetObservedCharacterIdentity(subjectActorId);
            if (!identity.TryGetValue(out ActorProfileId observedProfileId) ||
                !rickActorProfile.TryGetActorProfileId(out ActorProfileId rickProfileId, out diagnostic) ||
                observedProfileId != rickProfileId)
            {
                if (string.IsNullOrEmpty(diagnostic))
                {
                    diagnostic = "The current Player subject is not known as the configured Rick Actor Profile.";
                }

                return false;
            }

            PEGANpcKnowledgeFact<Vector3> lastObservedPosition =
                knowledgeState.GetLastObservedRickPosition(subjectActorId);
            if (!lastObservedPosition.TryGetValue(out position) || !lastObservedPosition.HasObservationTime)
            {
                diagnostic = "This NPC has no valid last observed Rick position with evidence time.";
                return false;
            }

            evidenceTimeSeconds = lastObservedPosition.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        public bool TryStart(out string diagnostic)
        {
            if (!TryGetKnownRickPosition(out Vector3 lastKnownPosition, out diagnostic))
            {
                RejectStart(diagnostic);
                return false;
            }

            return TryStartNavigation(
                lastKnownPosition,
                PEGA.Actors.PEGAActorMovementSpeedRegime.Pursuit,
                out diagnostic);
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic) =>
            TryGetKnownRickPosition(out _, out diagnostic);

        private bool TryResolveRickSubject(out ActorId subjectActorId, out string diagnostic)
        {
            subjectActorId = default;
            diagnostic = string.Empty;
            if (knowledgeState == null || knowledgeState.gameObject != gameObject)
            {
                diagnostic = "The Action requires this NPC's PEGANpcKnowledgeState on the same root.";
                return false;
            }

            if (rickReference == null || rickReference.gameObject != gameObject)
            {
                diagnostic = "The Action requires an explicit Player Actor occurrence reference on the same root.";
                return false;
            }

            if (rickActorProfile == null ||
                !rickActorProfile.TryGetActorProfileId(out ActorProfileId profileId, out diagnostic) ||
                !profileId.IsValid)
            {
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "The Action requires a valid Rick Actor Profile."
                    : diagnostic;
                return false;
            }

            if (!rickReference.TryGetCurrent(out ActorOccurrenceResolution resolution))
            {
                diagnostic = $"The configured Rick Player occurrence is not currently resolved: {resolution.Status}. {resolution.Diagnostic}";
                return false;
            }

            subjectActorId = resolution.Occurrence.ActorId;
            if (!subjectActorId.IsValid)
            {
                diagnostic = "The current Rick Player occurrence has no valid ActorId.";
                subjectActorId = default;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private void EnsurePursuitIntentEventsInitialized()
        {
            _pursuitIntentEvents ??= new EventBus<PEGANpcLastKnownRickPursuitIntentRequestedEvent>();
        }

        private void EnsureIntentLoggerInitialized()
        {
            _intentLog ??= PEGALogging.CreateScopedLogger<PEGANpcApproachKnownRickAction>(
                IntentLogCategory,
                gameObject.name);
        }

        private void LogIntentSubscriberFailure(
            Action<PEGANpcLastKnownRickPursuitIntentRequestedEvent> handler,
            PEGANpcLastKnownRickPursuitIntentRequestedEvent intentEvent,
            Exception exception)
        {
            EnsureIntentLoggerInitialized();
            _intentLog.Error(
                "Lost-contact pursuit intent subscriber failed; other subscribers remain isolated.",
                exception,
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("subscriber", handler.Method.DeclaringType?.FullName),
                LogFields.Field("subjectActorId", intentEvent.Intent.SubjectActorId),
                LogFields.Field("targetPosition", intentEvent.Intent.LastKnownPosition));
        }
    }
}

using System;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.Attacks.Runtime;
using PEGA.GOAP;
using PEGA.Movement.Actor;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;

namespace PEGA.NPC.Actions.Runtime
{
    [CreateAssetMenu(
        fileName = "AttackKnownCharacter",
        menuName = "PEGA/NPC/Combat/Attack Known Character")]
    public sealed class PegaNpcAttackKnownCharacterAction : PegaNpcActionProviderAsset,
        IPegaNpcKnowledgeChangeRelevanceProvider
    {
        private const string AttackEventLogCategory = "PEGA.Gameplay.Attack.Events.GOAP";

        private PegaActorAttackMechanic _attackMechanic;
        private PegaNpcKnowledgeState _knowledgeState;
        private PegaNpcKnowledgeReadView _knowledgeReadView;
        private ActorOccurrenceReferenceBinding _targetReference;
        private PegaNpcAttackImpactEffect _executionEffect;
        private PegaActorAttackResolution _resolution;
        private ScopedLogger _attackEventLog;
        private string _lastCandidateObservationKey;
        private bool _hasAttemptedImpact;

        protected override PegaNpcActionEffectKind ActionEffectKind =>
            PegaNpcActionEffectKind.AttackKnownCharacterImpact;

        public override PegaNpcActionContract Contract => new PegaNpcActionContract(
            PegaNpcPlanningActionId.AttackKnownCharacter,
            ActionEffectKind,
            PegaNpcPlanningTargetKind.CharacterPosition,
            false,
            "NPC began a GOAP-selected attack attempt against its current known character target.");

        protected override bool OnRuntimeInitialize(out string diagnostic)
        {
            if (!base.OnRuntimeInitialize(out diagnostic))
            {
                return false;
            }

            _attackMechanic = ActorRoot.GetComponent<PegaActorAttackMechanic>();
            _knowledgeState = RuntimeContext.KnowledgeState;
            _knowledgeReadView = RuntimeContext.KnowledgeReadView;
            _targetReference = RuntimeContext.ActorReference;
            if (_attackMechanic == null || _attackMechanic.gameObject != ActorRoot ||
                _knowledgeState == null || _knowledgeState.gameObject != ActorRoot ||
                _knowledgeReadView == null ||
                _targetReference == null || _targetReference.gameObject != ActorRoot)
            {
                diagnostic = "Attack Known Character requires the Actor's attack mechanic, Knowledge State and official target occurrence reference on the same root.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        public override bool TryCreatePlanningCandidates(
            PegaNpcActionPlanningContext context,
            out IReadOnlyList<PegaNpcPlanningTarget> targets,
            out IReadOnlyList<PegaNpcPlanningActionCandidate> candidates,
            out string diagnostic)
        {
            targets = Array.Empty<PegaNpcPlanningTarget>();
            candidates = Array.Empty<PegaNpcPlanningActionCandidate>();
            diagnostic = string.Empty;
            if (context.PlanContinuation.HasValue)
            {
                LogCandidateStatus(
                    context,
                    "unavailable",
                    "An active plan continuation owns GOAP selection.",
                    null);
                return true;
            }

            if (!context.TargetActorId.IsValid)
            {
                LogCandidateStatus(
                    context,
                    "unavailable",
                    "The NPC has no current target ActorId.",
                    null);
                return true;
            }

            if (!context.HasPerceptionBackedTargetContact)
            {
                LogCandidateStatus(
                    context,
                    "unavailable",
                    "The current target has no perception-backed visual contact.",
                    null);
                return true;
            }

            if (!TryGetKnownTargetEvidence(
                    context.TargetActorId,
                    context.KnowledgeReadView,
                    out Vector3 knownPosition,
                    out double evidenceTimeSeconds,
                    out diagnostic))
            {
                LogCandidateStatus(
                    context,
                    "unavailable",
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "The target has no current perception-backed position evidence."
                        : diagnostic,
                    null);
                return string.IsNullOrWhiteSpace(diagnostic);
            }

            if (!TryGetCurrentTargetOccurrence(
                    context.TargetActorId,
                    out _,
                    out diagnostic))
            {
                LogCandidateStatus(
                    context,
                    "unavailable",
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "The official target occurrence is unavailable."
                        : diagnostic,
                    knownPosition);
                return string.IsNullOrWhiteSpace(diagnostic);
            }

            PegaNpcPlanningTarget target = PegaNpcPlanningTarget.FromKnownActorPosition(
                context.TargetActorId,
                knownPosition,
                evidenceTimeSeconds);
            PegaNpcPlanningActionCandidate candidate = new PegaNpcPlanningActionCandidate(
                Contract,
                this,
                target.TargetId,
                PegaActorMovementSpeedRegime.Pursuit,
                IntrinsicDurationSeconds,
                _attackMechanic.AttackReachMeters);
            targets = Array.AsReadOnly(new[] { target });
            candidates = Array.AsReadOnly(new[] { candidate });
            LogCandidateStatus(
                context,
                "offered",
                "The current target is perception-backed; GOAP can evaluate a direct attack or an attack after approach.",
                knownPosition);
            return true;
        }

        public override bool CanOfferCandidate(
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcPlanContinuation? continuation) =>
            !continuation.HasValue &&
            CandidateMatchesContract(candidate) &&
            candidate.TargetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.Actor &&
            candidate.TargetRadiusOverrideMeters.HasValue &&
            _attackMechanic != null &&
            Mathf.Abs(candidate.TargetRadiusOverrideMeters.Value - _attackMechanic.AttackReachMeters) <= 0.0001f;

        public override PegaNpcCausalStepEvaluationStatus EvaluateCausalStep(
            PegaNpcPlanningActionCandidate candidate,
            IGoapHypotheticalStateView stateBeforeStep,
            PegaNpcCausalPlanningKnowledge knowledge,
            out PegaNpcNavigationRouteEstimate routeEstimate,
            out float estimatedSeconds,
            out string diagnostic)
        {
            routeEstimate = PegaNpcNavigationRouteEstimate.NotRequired;
            estimatedSeconds = 0f;
            diagnostic = string.Empty;
            if (!object.ReferenceEquals(candidate.Action, this) ||
                !CandidateMatchesContract(candidate) ||
                candidate.TargetId.IdentityKind != PegaNpcPlanningTargetIdentityKind.Actor)
            {
                diagnostic = "The causal Attack step does not match this Action provider and known Actor target.";
                return PegaNpcCausalStepEvaluationStatus.Unavailable;
            }

            if (stateBeforeStep == null ||
                stateBeforeStep.Source != GoapStateSource.HypotheticalState ||
                knowledge == null ||
                !knowledge.TryGetKnownTarget(candidate.TargetId, out PegaNpcGoapKnownTarget knownTarget) ||
                knownTarget == null ||
                knownTarget.Target.TargetId != candidate.TargetId ||
                knownTarget.Target.Kind != candidate.RequiredTargetKind ||
                !knownTarget.Target.HasEvidenceTime ||
                _attackMechanic == null)
            {
                diagnostic = "The causal Attack step has no current hypothetical state or evidence-backed Actor target.";
                return PegaNpcCausalStepEvaluationStatus.Unknown;
            }

            PegaNpcPlanningTarget target = knownTarget.Target;
            if (!TryGetKnownTargetEvidence(
                    target.TargetId.KnownActorId,
                    _knowledgeReadView,
                    out Vector3 knownPosition,
                    out double evidenceTimeSeconds,
                    out diagnostic) ||
                Math.Abs(evidenceTimeSeconds - target.EvidenceTimeSeconds) > 0.000001d ||
                !knownPosition.Equals(target.KnownPosition) ||
                !TryGetCurrentTargetOccurrence(
                    target.TargetId.KnownActorId,
                    out _,
                    out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The causal Attack target no longer matches current perception evidence and the official Actor occurrence."
                    : diagnostic;
                return PegaNpcCausalStepEvaluationStatus.Unknown;
            }

            estimatedSeconds = IntrinsicDurationSeconds;
            if (float.IsNaN(estimatedSeconds) || float.IsInfinity(estimatedSeconds) || estimatedSeconds < 0f)
            {
                diagnostic = "The Attack Action duration is not finite and non-negative.";
                return PegaNpcCausalStepEvaluationStatus.Unknown;
            }

            if (stateBeforeStep.ProjectedEffects.Count == 0)
            {
                if (_attackMechanic.IsPositionInAttackRegion(knownPosition))
                {
                    return PegaNpcCausalStepEvaluationStatus.Available;
                }

                diagnostic = "The target is outside the current physical attack region and no matching approach has been projected.";
                return PegaNpcCausalStepEvaluationStatus.Unavailable;
            }

            PegaNpcTargetReachedEffect targetReached = new PegaNpcTargetReachedEffect(
                target.TargetId,
                target.Kind,
                target.EvidenceTimeSeconds);
            GoapTruthValue targetReachedInProjectedState = targetReached.EvaluateSatisfaction(stateBeforeStep);
            if (targetReachedInProjectedState == GoapTruthValue.True)
            {
                return PegaNpcCausalStepEvaluationStatus.Available;
            }

            diagnostic = targetReachedInProjectedState == GoapTruthValue.False
                ? "The projected state does not contain arrival at this exact known Actor target."
                : "The projected state cannot establish arrival at this exact known Actor target.";
            return targetReachedInProjectedState == GoapTruthValue.False
                ? PegaNpcCausalStepEvaluationStatus.Unavailable
                : PegaNpcCausalStepEvaluationStatus.Unknown;
        }

        public bool IsRelevantKnowledgeChange(PegaKnowledgeFactChangedEvent change)
        {
            if (change == null || !change.HasValueOrStatusChanged ||
                _targetReference == null ||
                !_targetReference.TryGetCurrent(out ActorOccurrenceResolution resolution) ||
                !resolution.Succeeded)
            {
                return false;
            }

            return change.IsFor(
                       PegaNpcKnowledgeFacts.CharacterIncapacitated.For(resolution.Occurrence.ActorId)) ||
                   change.IsFor(
                       PegaNpcKnowledgeFacts.CharacterAttackProtected.For(resolution.Occurrence.ActorId)) ||
                   change.IsFor(
                       PegaNpcKnowledgeFacts.CharacterVisualContact.For(resolution.Occurrence.ActorId)) ||
                   change.IsFor(
                       PegaNpcKnowledgeFacts.LastObservedCharacterPosition.For(resolution.Occurrence.ActorId));
        }

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
            if (continuation.HasValue ||
                plan.Purpose != PegaNpcPlanPurpose.DesireGoal ||
                plan.ActionId != Contract.ActionId ||
                plan.TargetKind != Contract.TargetKind ||
                plan.TargetId.IdentityKind != PegaNpcPlanningTargetIdentityKind.Actor ||
                !TryGetKnownTargetEvidence(
                    plan.TargetId.KnownActorId,
                    _knowledgeReadView,
                    out currentKnownPosition,
                    out evidenceTimeSeconds,
                    out diagnostic) ||
                Math.Abs(evidenceTimeSeconds - plan.EvidenceTimeSeconds) > 0.000001d ||
                !IsTargetPositionWithinTolerance(currentKnownPosition, plan.TargetPosition) ||
                !_attackMechanic.IsPositionInAttackRegion(currentKnownPosition) ||
                !TryGetCurrentTargetOccurrence(
                    plan.TargetId.KnownActorId,
                    out _,
                    out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Attack plan no longer matches the current perception-backed target and physical attack region."
                    : diagnostic;
                return false;
            }

            return true;
        }

        public override bool TryCreateCausalActionDefinition(
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcGoapKnownTarget target,
            out GoapActionDefinition actionDefinition,
            out string diagnostic)
        {
            actionDefinition = null;
            diagnostic = string.Empty;
            if (!object.ReferenceEquals(candidate.Action, this) ||
                !CandidateMatchesContract(candidate) ||
                target == null ||
                candidate.TargetId != target.Target.TargetId ||
                candidate.RequiredTargetKind != target.Target.Kind ||
                !TryGetKnownTargetEvidence(
                    target.Target.TargetId.KnownActorId,
                    _knowledgeReadView,
                    out Vector3 currentKnownPosition,
                    out double currentEvidenceTimeSeconds,
                    out diagnostic) ||
                !target.Target.HasEvidenceTime ||
                Math.Abs(currentEvidenceTimeSeconds - target.Target.EvidenceTimeSeconds) > 0.000001d ||
                !currentKnownPosition.Equals(target.Target.KnownPosition) ||
                !TryGetCurrentTargetOccurrence(
                    target.Target.TargetId.KnownActorId,
                    out ActorOccurrence occurrence,
                    out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Attack candidate is not a current application to this evidence-backed Actor Target."
                    : diagnostic;
                return false;
            }

            PegaNpcAttackImpactEffect impactEffect = new PegaNpcAttackImpactEffect(
                target.Target.TargetId,
                occurrence.Id,
                target.Target.KnownPosition,
                target.Target.EvidenceTimeSeconds);
            actionDefinition = new GoapActionDefinition(
                Array.Empty<IGoapCondition>(),
                new IGoapEffect[] { impactEffect });
            diagnostic = string.Empty;
            return true;
        }

        public override bool IsEffectAlreadySatisfiedAtPosition(
            PegaNpcPlanSnapshot plan,
            Vector3 actorPosition) => false;

        public override bool TryStart(
            PegaNpcPlanSnapshot plan,
            PegaNpcPlanContinuation? continuation,
            out string diagnostic)
        {
            _executionEffect = null;
            _resolution = default;
            _hasAttemptedImpact = false;
            if (!IsCurrentPlanValid(
                    plan,
                    continuation,
                    out Vector3 position,
                    out _,
                    out diagnostic))
            {
                RejectStart(diagnostic);
                return false;
            }

            _executionEffect = FindDeclaredImpactEffect(plan);
            if (_executionEffect == null)
            {
                diagnostic = "The selected causal Attack action has no declared impact effect for its exact target occurrence.";
                RejectStart(diagnostic);
                return false;
            }

            return TryStartStationaryAction(
                plan.TargetId,
                position,
                _attackMechanic.AttackReachMeters,
                out diagnostic);
        }

        public override bool TryGetObservedCausalEffects(
            PegaNpcPlanSnapshot plan,
            PegaNpcObservedActionEffect observedEffect,
            out IReadOnlyList<IGoapEffect> observedEffects,
            out string diagnostic)
        {
            observedEffects = Array.Empty<IGoapEffect>();
            if (plan.ActionId != Contract.ActionId ||
                !observedEffect.IsValid ||
                observedEffect.RequestId <= 0 ||
                observedEffect.Kind != Contract.EffectKind ||
                !observedEffect.TargetId.Equals(plan.TargetId) ||
                observedEffect.TargetKind != Contract.TargetKind ||
                observedEffect.ActualDistanceToTargetMeters > observedEffect.TargetRadiusMeters ||
                !_resolution.IsHit ||
                _executionEffect == null ||
                !TryGetExactTargetContact(out PegaActorAttackContact targetContact) ||
                targetContact.ImpactResolution.Outcome == PegaActorImpactOutcome.Protected ||
                !ContainsDeclaredEffect(plan, _executionEffect))
            {
                diagnostic = TryGetExactTargetContact(out PegaActorAttackContact protectedTargetContact) &&
                    protectedTargetContact.ImpactResolution.Outcome == PegaActorImpactOutcome.Protected
                    ? "The physical contact was confirmed, but post-recovery protection prevented the declared impact effect."
                    : "No confirmed physical hit matches the active GOAP target occurrence and declared impact effect.";
                return false;
            }

            observedEffects = Array.AsReadOnly<IGoapEffect>(new[] { _executionEffect });
            diagnostic = string.Empty;
            return true;
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_executionEffect == null || _attackMechanic == null ||
                !TryGetKnownTargetEvidence(
                    _executionEffect.TargetId.KnownActorId,
                    _knowledgeReadView,
                    out Vector3 knownPosition,
                    out _,
                    out diagnostic) ||
                !TryGetCurrentTargetOccurrence(
                    _executionEffect.TargetId.KnownActorId,
                    out ActorOccurrence currentOccurrence,
                    out diagnostic) ||
                currentOccurrence.Id != _executionEffect.TargetOccurrenceId ||
                !_attackMechanic.IsPositionInAttackRegion(knownPosition))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The current target occurrence is no longer known as active and inside the physical attack region."
                    : diagnostic;
                return false;
            }

            return true;
        }

        protected override void TickNonNavigationAction()
        {
            if (_hasAttemptedImpact)
            {
                return;
            }

            _hasAttemptedImpact = true;
            if (_executionEffect == null ||
                !_attackMechanic.TryPerformAttack(
                    PegaActorAttackIntent.RequestFor(_executionEffect.TargetOccurrenceId),
                    out _resolution))
            {
                CompleteAction(
                    PegaNpcActionOutcome.Invalid,
                    "The Actor attack mechanic did not execute the GOAP impact request.");
                return;
            }

            if (!_resolution.IsAvailable)
            {
                CompleteAction(
                    PegaNpcActionOutcome.Invalid,
                    _resolution.Diagnostic);
                return;
            }

            if (_resolution.IsHit)
            {
                ObserveTargetIncapacitationResult();
            }

            bool exactTargetWasHit = TryGetExactTargetContact(out PegaActorAttackContact targetContact);
            bool impactEffectApplied = exactTargetWasHit &&
                targetContact.ImpactResolution.Outcome != PegaActorImpactOutcome.Protected &&
                targetContact.ImpactResolution.Outcome != PegaActorImpactOutcome.Unavailable;
            string completionDiagnostic = exactTargetWasHit &&
                targetContact.ImpactResolution.Outcome == PegaActorImpactOutcome.Protected
                ? targetContact.ImpactResolution.Diagnostic
                : _resolution.Diagnostic;

            CompleteAction(
                impactEffectApplied
                    ? PegaNpcActionOutcome.Success
                    : PegaNpcActionOutcome.CompletedWithoutEffect,
                completionDiagnostic);
        }

        protected override bool IsActionSpecificEffectObserved(out string diagnostic)
        {
            bool matched = _resolution.IsHit &&
                _executionEffect != null &&
                TryGetExactTargetContact(out PegaActorAttackContact contact) &&
                contact.ImpactResolution.Outcome != PegaActorImpactOutcome.Protected &&
                contact.ImpactResolution.Outcome != PegaActorImpactOutcome.Unavailable;
            diagnostic = matched
                ? string.Empty
                : "The selected Actor occurrence did not receive an applicable impact inside the attack region.";
            return matched;
        }

        private bool TryGetKnownTargetEvidence(
            ActorId targetActorId,
            PegaNpcKnowledgeReadView knowledgeReadView,
            out Vector3 knownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            knownPosition = default;
            evidenceTimeSeconds = 0d;
            if (!targetActorId.IsValid || knowledgeReadView == null)
            {
                diagnostic = "The Action has no valid target ActorId or Knowledge State read view.";
                return false;
            }

            if (!TryGetCurrentTargetOccurrence(
                    targetActorId,
                    out ActorOccurrence currentOccurrence,
                    out diagnostic))
            {
                return false;
            }

            PegaKnowledgeFact<Vector3> positionFact = knowledgeReadView.GetKnownCharacterPosition(targetActorId);
            if (!positionFact.TryGetValue(out knownPosition) ||
                !positionFact.HasObservationTime ||
                !positionFact.HasOrigin(PegaNpcEvidenceOrigin.Perception))
            {
                diagnostic = "The Action has no current perception-backed position evidence for its target.";
                return false;
            }

            PegaKnowledgeFact<bool> contactFact = _knowledgeState.GetCharacterVisualContact(targetActorId);
            if (!contactFact.TryGetValue(out bool hasVisualContact) || !hasVisualContact)
            {
                diagnostic = "The target is no longer backed by current visual-contact knowledge.";
                return false;
            }

            PegaKnowledgeFact<PegaNpcCharacterIncapacitationObservation> incapacitationFact =
                knowledgeReadView.GetKnownCharacterIncapacitation(targetActorId);
            if (incapacitationFact.TryGetValue(
                    out PegaNpcCharacterIncapacitationObservation observation) &&
                observation.ActorOccurrenceId == currentOccurrence.Id &&
                observation.IsIncapacitated)
            {
                diagnostic = "The current perception-backed target is known to be incapacitated and cannot be attacked.";
                return false;
            }

            PegaKnowledgeFact<PegaNpcCharacterAttackProtectionObservation> attackProtectionFact =
                knowledgeReadView.GetKnownCharacterAttackProtection(targetActorId);
            if (attackProtectionFact.TryGetValue(
                    out PegaNpcCharacterAttackProtectionObservation protectionObservation) &&
                protectionObservation.ActorOccurrenceId == currentOccurrence.Id &&
                protectionObservation.IsProtected &&
                Time.timeAsDouble < protectionObservation.ProtectionEndsAtTimeSeconds)
            {
                diagnostic = "The current perception-backed target is inside its post-recovery attack protection window.";
                return false;
            }

            evidenceTimeSeconds = positionFact.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        private void ObserveTargetIncapacitationResult()
        {
            if (_executionEffect == null)
            {
                return;
            }

            ActorId expectedTargetId = _executionEffect.TargetId.KnownActorId;
            for (int index = 0; index < _resolution.Contacts.Count; index++)
            {
                PegaActorAttackContact contact = _resolution.Contacts[index];
                if (contact.ActorId != expectedTargetId ||
                    contact.OccurrenceId != _executionEffect.TargetOccurrenceId ||
                    contact.ImpactResolution.Outcome == PegaActorImpactOutcome.Unavailable)
                {
                    continue;
                }

                bool targetIncapacitated = contact.ImpactResolution.IsIncapacitated;
                _knowledgeState.ObserveCharacterIncapacitated(
                    contact.ActorId,
                    contact.OccurrenceId,
                    targetIncapacitated,
                    PegaNpcEvidenceOrigin.ActionResult,
                    _resolution.ResolvedAtTimeSeconds);
                bool targetAttackProtected = contact.ImpactResolution.Outcome == PegaActorImpactOutcome.Protected;
                _knowledgeState.ObserveCharacterAttackProtected(
                    contact.ActorId,
                    contact.OccurrenceId,
                    targetAttackProtected,
                    targetAttackProtected
                        ? contact.ImpactResolution.AttackProtectionEndsAtTimeSeconds
                        : 0d,
                    PegaNpcEvidenceOrigin.ActionResult,
                    _resolution.ResolvedAtTimeSeconds);
                _attackEventLog ??= PegaLogging.CreateScopedLogger<PegaNpcAttackKnownCharacterAction>(
                    AttackEventLogCategory,
                    ActorRoot != null ? ActorRoot.name : name);
                _attackEventLog.Info(
                    "NPC Attack GOAP Action recorded the exact target's recovery and incapacitation state from its confirmed impact.",
                    LogFields.Field("actorRoot", ActorRoot != null ? ActorRoot.name : string.Empty),
                    LogFields.Field("targetActorId", contact.ActorId.StableText),
                    LogFields.Field("targetActorOccurrenceId", contact.OccurrenceId.ToString()),
                    LogFields.Field("targetIncapacitated", targetIncapacitated),
                    LogFields.Field("targetAttackProtected", targetAttackProtected),
                    LogFields.Field("attackProtectionEndsAtTimeSeconds",
                        contact.ImpactResolution.AttackProtectionEndsAtTimeSeconds),
                    LogFields.Field("impactOutcome", contact.ImpactResolution.Outcome),
                    LogFields.Field("observationTimeSeconds", _resolution.ResolvedAtTimeSeconds));
                return;
            }
        }

        private bool TryGetCurrentTargetOccurrence(
            ActorId expectedActorId,
            out ActorOccurrence occurrence,
            out string diagnostic)
        {
            occurrence = default;
            if (_targetReference == null ||
                !_targetReference.TryGetCurrent(out ActorOccurrenceResolution resolution) ||
                !resolution.Succeeded ||
                resolution.Occurrence.ActorId != expectedActorId)
            {
                diagnostic = "The official target occurrence reference is unavailable or no longer matches the GOAP target ActorId.";
                return false;
            }

            occurrence = resolution.Occurrence;
            diagnostic = string.Empty;
            return true;
        }

        private PegaNpcAttackImpactEffect FindDeclaredImpactEffect(PegaNpcPlanSnapshot plan)
        {
            for (int index = 0; index < plan.CausalEffects.Count; index++)
            {
                if (plan.CausalEffects[index] is PegaNpcAttackImpactEffect impact &&
                    impact.TargetId.Equals(plan.TargetId))
                {
                    return impact;
                }
            }

            return null;
        }

        private void LogCandidateStatus(
            PegaNpcActionPlanningContext context,
            string status,
            string diagnostic,
            Vector3? knownPosition)
        {
            if (ActorRoot == null)
            {
                return;
            }

            _attackEventLog ??= PegaLogging.CreateScopedLogger<PegaNpcAttackKnownCharacterAction>(
                AttackEventLogCategory,
                ActorRoot.name);

            string targetActorId = context.TargetActorId.IsValid
                ? context.TargetActorId.StableText
                : "none";
            string observationKey = $"{status}|{targetActorId}|{diagnostic}";
            if (string.Equals(_lastCandidateObservationKey, observationKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastCandidateObservationKey = observationKey;
            _attackEventLog.Debug(
                "NPC GOAP evaluated Attack Known Character candidate availability.",
                LogFields.Field("actorRoot", ActorRoot.name),
                LogFields.Field("status", status),
                LogFields.Field("targetActorId", targetActorId),
                LogFields.Field("perceptionBackedTargetContact", context.HasPerceptionBackedTargetContact),
                LogFields.Field("planContinuationActive", context.PlanContinuation.HasValue),
                LogFields.Field("targetPosition", knownPosition.HasValue
                    ? knownPosition.Value.ToString("F2")
                    : "unavailable"),
                LogFields.Field("actorPosition", ActorRoot.transform.position.ToString("F2")),
                LogFields.Field("physicalForward", ActorRoot.transform.forward.ToString("F2")),
                LogFields.Field("attackReachMeters", _attackMechanic != null
                    ? _attackMechanic.AttackReachMeters
                    : -1f),
                LogFields.Field("diagnostic", diagnostic));
        }

        private static bool ContainsDeclaredEffect(PegaNpcPlanSnapshot plan, IGoapEffect effect)
        {
            for (int index = 0; index < plan.CausalEffects.Count; index++)
            {
                if (object.ReferenceEquals(plan.CausalEffects[index], effect))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetExactTargetContact(out PegaActorAttackContact targetContact)
        {
            targetContact = default;
            if (_executionEffect == null)
            {
                return false;
            }

            for (int index = 0; index < _resolution.Contacts.Count; index++)
            {
                PegaActorAttackContact contact = _resolution.Contacts[index];
                if (contact.ActorId == _executionEffect.TargetId.KnownActorId &&
                    contact.OccurrenceId == _executionEffect.TargetOccurrenceId)
                {
                    targetContact = contact;
                    return true;
                }
            }

            return false;
        }

    }
}

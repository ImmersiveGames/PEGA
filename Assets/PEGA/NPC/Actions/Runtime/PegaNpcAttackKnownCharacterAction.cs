using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using PEGA.Attacks.Runtime;
using PEGA.GOAP;
using PEGA.Movement.Actor;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;

namespace PEGA.NPC.Actions.Runtime
{
    [CreateAssetMenu(
        fileName = "AttackKnownCharacter",
        menuName = "PEGA/NPC/Combat/Attack Known Character")]
    public sealed class PegaNpcAttackKnownCharacterAction : PegaNpcActionProviderAsset
    {
        private PegaActorAttackMechanic _attackMechanic;
        private PegaNpcKnowledgeState _knowledgeState;
        private PegaNpcKnowledgeReadView _knowledgeReadView;
        private ActorOccurrenceReferenceBinding _targetReference;
        private PegaNpcAttackImpactEffect _executionEffect;
        private PegaActorAttackResolution _resolution;
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
            if (context.PlanContinuation.HasValue ||
                !context.TargetActorId.IsValid ||
                !context.HasPerceptionBackedTargetContact)
            {
                return true;
            }

            if (!TryGetKnownTargetEvidence(
                    context.TargetActorId,
                    context.KnowledgeReadView,
                    out Vector3 knownPosition,
                    out double evidenceTimeSeconds,
                    out diagnostic) ||
                !TryGetCurrentTargetOccurrence(
                    context.TargetActorId,
                    out _,
                    out diagnostic))
            {
                return string.IsNullOrWhiteSpace(diagnostic);
            }

            if (!_attackMechanic.IsPositionInAttackRegion(knownPosition))
            {
                return true;
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
                !_attackMechanic.IsPositionInAttackRegion(target.Target.KnownPosition) ||
                !TryGetCurrentTargetOccurrence(
                    target.Target.TargetId.KnownActorId,
                    out ActorOccurrence occurrence,
                    out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Attack candidate is not a current in-region application to this known Actor Target."
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
                !ContainsExactTarget(_resolution.Contacts, _executionEffect.TargetOccurrenceId) ||
                !ContainsDeclaredEffect(plan, _executionEffect))
            {
                diagnostic = "No confirmed physical hit matches the active GOAP target occurrence and declared impact effect.";
                return false;
            }

            observedEffects = Array.AsReadOnly<IGoapEffect>(new[] { _executionEffect });
            diagnostic = string.Empty;
            return true;
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic)
        {
            diagnostic = string.Empty;
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

            CompleteAction(
                _resolution.IsHit
                    ? PegaNpcActionOutcome.Success
                    : PegaNpcActionOutcome.CompletedWithoutEffect,
                _resolution.Diagnostic);
        }

        protected override bool IsActionSpecificEffectObserved(out string diagnostic)
        {
            bool matched = _resolution.IsHit &&
                _executionEffect != null &&
                ContainsExactTarget(_resolution.Contacts, _executionEffect.TargetOccurrenceId);
            diagnostic = matched
                ? string.Empty
                : "The selected Actor occurrence was not physically confirmed inside the attack region at impact.";
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

            evidenceTimeSeconds = positionFact.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
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

        private static bool ContainsExactTarget(
            IReadOnlyList<PegaActorAttackContact> contacts,
            ActorOccurrenceId occurrenceId)
        {
            for (int index = 0; index < contacts.Count; index++)
            {
                if (contacts[index].OccurrenceId.Equals(occurrenceId))
                {
                    return true;
                }
            }

            return false;
        }

    }
}

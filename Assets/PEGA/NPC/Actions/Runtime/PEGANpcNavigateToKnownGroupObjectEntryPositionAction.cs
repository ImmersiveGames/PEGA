using System;
using System.Collections.Generic;
using Immersive.Framework.ObjectEntry;
using PEGA.GOAP;
using PEGA.Movement.Actor;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Group;
using UnityEngine;
namespace PEGA.NPC.Actions.Runtime
{
    [CreateAssetMenu(
        fileName = "NavigateToKnownGroupObjectEntryPosition",
        menuName = "PEGA/NPC/Movement/Navigate To Known Group Object Entry Position")]
    public sealed class PegaNpcNavigateToKnownGroupObjectEntryPositionAction : PegaNpcActionProviderAsset
    {
        private PegaActorGroupKnowledgeReader _groupKnowledgeReader;
        private PegaNpcPlanSnapshot _executionPlan;
        private bool _hasExecutionPlan;

        protected override PegaNpcActionEffectKind ActionEffectKind =>
            PegaNpcActionEffectKind.ReachKnownGroupObjectEntryPosition;

        public override PegaNpcActionContract Contract => new PegaNpcActionContract(
            PegaNpcPlanningActionId.NavigateToKnownGroupObjectEntryPosition,
            ActionEffectKind,
            PegaNpcPlanningTargetKind.GroupObjectEntryPosition,
            true,
            "NPC began navigating to a Group-known Object Entry position.");

        protected override bool OnRuntimeInitialize(out string diagnostic)
        {
            if (!base.OnRuntimeInitialize(out diagnostic))
            {
                return false;
            }

            _groupKnowledgeReader = ActorRoot.GetComponent<PegaActorGroupKnowledgeReader>();
            if (_groupKnowledgeReader == null || _groupKnowledgeReader.gameObject != ActorRoot)
            {
                diagnostic = "The Action requires the Actor's composed Group Knowledge Reader on the same root.";
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
            var targetList = new List<PegaNpcPlanningTarget>();
            var candidateList = new List<PegaNpcPlanningActionCandidate>();
            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());
            diagnostic = string.Empty;

            if (context.PlanContinuation.HasValue)
            {
                return true;
            }

            if (!TryGetComposedReader(context.ComposedKnowledgeState, out PegaActorGroupKnowledgeReader reader, out diagnostic))
            {
                return false;
            }

            PegaActorGroupKnowledgeFactSetReadResult<ObjectEntryId, Vector3> read = reader.ReadGroupFacts(
                PegaObjectEntryKnowledgeFacts.Position,
                Time.timeAsDouble);
            if (!read.IsAuthorized)
            {
                diagnostic = string.IsNullOrWhiteSpace(read.Diagnostic)
                    ? $"Group Knowledge cannot provide current Object Entry positions ({read.AccessStatus})."
                    : read.Diagnostic;
                return false;
            }

            for (int groupIndex = 0; groupIndex < read.Groups.Count; groupIndex++)
            {
                PegaActorGroupKnowledgeFactSetSnapshot<ObjectEntryId, Vector3> group = read.Groups[groupIndex];
                if (!group.GroupId.IsValid)
                {
                    diagnostic = "Group Knowledge returned a fact set without a valid GroupId.";
                    return false;
                }

                for (int factIndex = 0; factIndex < group.Facts.Count; factIndex++)
                {
                    PegaKnowledgeFactSnapshot<ObjectEntryId, Vector3> snapshot = group.Facts[factIndex];
                    if (!snapshot.Key.IsValid || !snapshot.Key.AddressValue.IsValid ||
                        snapshot.Fact.Scope != PegaKnowledgeScope.Group ||
                        !snapshot.Fact.TryGetValue(out Vector3 knownPosition))
                    {
                        continue;
                    }

                    if (!snapshot.Fact.HasObservationTime)
                    {
                        diagnostic = $"Known Group position for ObjectEntryId '{snapshot.Key.AddressValue.StableText}' has no evidence time.";
                        return false;
                    }

                    PegaNpcPlanningTarget target = PegaNpcPlanningTarget.FromKnownGroupObjectEntryPosition(
                        snapshot.Key.AddressValue,
                        group.GroupId,
                        knownPosition,
                        snapshot.Fact.ObservationTimeSeconds);
                    if (!target.IsValid)
                    {
                        diagnostic = $"Group Knowledge returned an invalid position Target for ObjectEntryId '{snapshot.Key.AddressValue.StableText}'.";
                        return false;
                    }

                    targetList.Add(target);
                }
            }

            if (targetList.Count == 0)
            {
                targets = Array.AsReadOnly(targetList.ToArray());
                candidates = Array.AsReadOnly(candidateList.ToArray());
                return true;
            }

            if (!TryDeferNavigationRoute(
                    PegaActorMovementSpeedRegime.Movement,
                    out PegaNpcNavigationRouteEstimate deferredRoute,
                    out diagnostic))
            {
                return false;
            }

            for (int index = 0; index < targetList.Count; index++)
            {
                PegaNpcPlanningTarget target = targetList[index];
                candidateList.Add(new PegaNpcPlanningActionCandidate(
                    Contract,
                    this,
                    target.TargetId,
                    PegaActorMovementSpeedRegime.Movement,
                    deferredRoute,
                    IntrinsicDurationSeconds));
            }

            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());
            diagnostic = string.Empty;
            return true;
        }

        public override bool CanOfferCandidate(
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcPlanContinuation? continuation) =>
            !continuation.HasValue &&
            CandidateMatchesContract(candidate) &&
            candidate.TargetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition &&
            candidate.TargetId.ObjectEntryId.IsValid &&
            candidate.TargetId.KnowledgeGroupId.IsValid;

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
                plan.TargetProvenance != PegaNpcPlanningTargetProvenance.DerivedFromKnowledge ||
                plan.TargetId.IdentityKind != PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition ||
                !TryReadCurrentPositionEvidence(
                    plan.TargetId,
                    out currentKnownPosition,
                    out evidenceTimeSeconds,
                    out diagnostic) ||
                !currentKnownPosition.Equals(plan.TargetPosition) ||
                Math.Abs(evidenceTimeSeconds - plan.EvidenceTimeSeconds) > 0.000001d)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The plan no longer matches a live, membership-authorized Group Object Entry position fact."
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
            if (!IsCurrentPlanValid(
                    plan,
                    continuation,
                    out Vector3 knownPosition,
                    out _,
                    out diagnostic))
            {
                RejectStart(string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Group-known Object Entry position changed before navigation started."
                    : diagnostic);
                return false;
            }

            _executionPlan = plan;
            _hasExecutionPlan = true;
            if (!TryStartNavigation(
                    plan.TargetId,
                    knownPosition,
                    PegaActorMovementSpeedRegime.Movement,
                    out diagnostic))
            {
                _hasExecutionPlan = false;
                _executionPlan = default;
                return false;
            }

            return true;
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_hasExecutionPlan ||
                !TryReadCurrentPositionEvidence(
                    _executionPlan.TargetId,
                    out Vector3 currentPosition,
                    out double currentEvidenceTimeSeconds,
                    out diagnostic) ||
                !currentPosition.Equals(_executionPlan.TargetPosition) ||
                Math.Abs(currentEvidenceTimeSeconds - _executionPlan.EvidenceTimeSeconds) > 0.000001d)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "Group membership or the authoritative Object Entry position fact changed during navigation."
                    : diagnostic;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        protected override bool IsActionSpecificEffectObserved(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_hasExecutionPlan ||
                !TryReadCurrentPositionEvidence(
                    _executionPlan.TargetId,
                    out Vector3 currentPosition,
                    out double currentEvidenceTimeSeconds,
                    out diagnostic) ||
                !currentPosition.Equals(_executionPlan.TargetPosition) ||
                Math.Abs(currentEvidenceTimeSeconds - _executionPlan.EvidenceTimeSeconds) > 0.000001d)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Group-authorized position fact no longer matches the Action's destination."
                    : diagnostic;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryReadCurrentPositionEvidence(
            PegaNpcPlanningTargetId targetId,
            out Vector3 knownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            knownPosition = default;
            evidenceTimeSeconds = 0d;
            if (_groupKnowledgeReader == null ||
                targetId.IdentityKind != PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition ||
                !targetId.ObjectEntryId.IsValid || !targetId.KnowledgeGroupId.IsValid)
            {
                diagnostic = "The Action requires a typed ObjectEntryId and GroupId from its selected Target.";
                return false;
            }

            PegaActorGroupKnowledgeReadResult<Vector3> read = _groupKnowledgeReader.ReadGroupFact(
                targetId.KnowledgeGroupId,
                PegaObjectEntryKnowledgeFacts.Position.For(targetId.ObjectEntryId),
                Time.timeAsDouble);
            if (!read.IsAuthorized ||
                read.Fact.Scope != PegaKnowledgeScope.Group ||
                !read.Fact.TryGetValue(out knownPosition) ||
                !read.Fact.HasObservationTime)
            {
                diagnostic = $"The selected Group position fact is not currently readable through effective membership ({read.AccessStatus}).";
                return false;
            }

            evidenceTimeSeconds = read.Fact.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        private bool TryGetComposedReader(
            IGoapKnowledgeStateView composedKnowledgeState,
            out PegaActorGroupKnowledgeReader reader,
            out string diagnostic)
        {
            reader = null;
            if (composedKnowledgeState == null || composedKnowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                diagnostic = "The Action requires the NPC's explicit composed Knowledge State view.";
                return false;
            }

            for (int index = 0; index < composedKnowledgeState.Capabilities.Count; index++)
            {
                if (!(composedKnowledgeState.Capabilities[index] is PegaActorGroupKnowledgeReader candidate))
                {
                    continue;
                }

                if (reader != null && reader != candidate)
                {
                    diagnostic = "More than one Group Knowledge Reader is composed for this NPC; the Action will not choose an authority implicitly.";
                    return false;
                }

                reader = candidate;
            }

            if (reader == null || reader != _groupKnowledgeReader)
            {
                diagnostic = "The Action's Group Knowledge Reader is absent from the NPC's composed Knowledge State.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }
    }
}

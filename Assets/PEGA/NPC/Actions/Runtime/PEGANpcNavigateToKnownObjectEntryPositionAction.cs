using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.GOAP;
using PEGA.Movement.Actor;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Group;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
namespace PEGA.NPC.Actions.Runtime
{
    public enum PegaNpcObjectEntryPositionKnowledgeSource
    {
        Group = 0,
        Individual = 10
    }

    [CreateAssetMenu(
        fileName = "NavigateToKnownObjectEntryPosition",
        menuName = "PEGA/NPC/Movement/Navigate To Known Object Entry Position")]
    public sealed class PegaNpcNavigateToKnownObjectEntryPositionAction : PegaNpcActionProviderAsset
    {
        [SerializeField]
        [Tooltip("Knowledge authority that supplies the Object Entry position. Individual uses this NPC's own Perception-backed facts.")]
        private PegaNpcObjectEntryPositionKnowledgeSource knowledgeSource;

        private PegaActorGroupKnowledgeReader _groupKnowledgeReader;
        private PegaNpcKnowledgeState _individualKnowledgeState;
        private PegaNpcPlanSnapshot _executionPlan;
        private bool _hasExecutionPlan;

        protected override PegaNpcActionEffectKind ActionEffectKind =>
            UsesIndividualKnowledge
                ? PegaNpcActionEffectKind.ReachKnownIndividualObjectEntryPosition
                : PegaNpcActionEffectKind.ReachKnownGroupObjectEntryPosition;

        private bool UsesIndividualKnowledge =>
            knowledgeSource == PegaNpcObjectEntryPositionKnowledgeSource.Individual;

        private PegaNpcPlanningActionId ActionId => UsesIndividualKnowledge
            ? PegaNpcPlanningActionId.NavigateToKnownIndividualObjectEntryPosition
            : PegaNpcPlanningActionId.NavigateToKnownGroupObjectEntryPosition;

        private PegaNpcPlanningTargetKind TargetKind => UsesIndividualKnowledge
            ? PegaNpcPlanningTargetKind.IndividualObjectEntryPosition
            : PegaNpcPlanningTargetKind.GroupObjectEntryPosition;

        public override PegaNpcActionContract Contract => new PegaNpcActionContract(
            ActionId,
            ActionEffectKind,
            TargetKind,
            true,
            UsesIndividualKnowledge
                ? "NPC began navigating to an Object Entry position known through its Individual Knowledge."
                : "NPC began navigating to a Group-known Object Entry position.");

        protected override bool OnRuntimeInitialize(out string diagnostic)
        {
            if (!base.OnRuntimeInitialize(out diagnostic))
            {
                return false;
            }

            if (!Enum.IsDefined(typeof(PegaNpcObjectEntryPositionKnowledgeSource), knowledgeSource))
            {
                diagnostic = "The Action has an unsupported Object Entry position Knowledge source.";
                return false;
            }

            if (UsesIndividualKnowledge)
            {
                _individualKnowledgeState = RuntimeContext.KnowledgeState;
                if (_individualKnowledgeState == null || _individualKnowledgeState.gameObject != ActorRoot ||
                    _individualKnowledgeState.Scope != PegaKnowledgeScope.Individual)
                {
                    diagnostic = "The Action requires this Actor's composed Individual Knowledge State on the same root.";
                    return false;
                }
            }
            else
            {
                _groupKnowledgeReader = ActorRoot.GetComponent<PegaActorGroupKnowledgeReader>();
                if (_groupKnowledgeReader == null || _groupKnowledgeReader.gameObject != ActorRoot)
                {
                    diagnostic = "The Action requires the Actor's composed Group Knowledge Reader on the same root.";
                    return false;
                }
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

            if (UsesIndividualKnowledge)
            {
                if (context.KnowledgeState != _individualKnowledgeState)
                {
                    diagnostic = "Individual navigation requires this Actor's own composed Knowledge State.";
                    return false;
                }

                if (!TryGetComposedIndividualKnowledge(
                        context.ComposedKnowledgeState,
                        out PegaNpcKnowledgeState individualKnowledge,
                        out diagnostic))
                {
                    return false;
                }

                if (!TryGetActorId(out ActorId actorId, out diagnostic))
                {
                    return false;
                }

                IReadOnlyList<ObjectEntryId> objectEntryIds = individualKnowledge.GetKnownObjectEntryIds();
                for (int index = 0; index < objectEntryIds.Count; index++)
                {
                    ObjectEntryId objectEntryId = objectEntryIds[index];
                    PegaKnowledgeFact<Vector3> position = individualKnowledge.ReadFact(
                        PegaObjectEntryKnowledgeFacts.Position.For(objectEntryId),
                        Time.timeAsDouble);
                    if (position.Scope != PegaKnowledgeScope.Individual ||
                        !position.TryGetValue(out Vector3 knownPosition) ||
                        !position.HasObservationTime ||
                        !position.HasOrigin(PegaNpcEvidenceOrigin.Perception))
                    {
                        continue;
                    }

                    PegaNpcPlanningTarget target = PegaNpcPlanningTarget.FromKnownIndividualObjectEntryPosition(
                        objectEntryId,
                        actorId,
                        knownPosition,
                        position.ObservationTimeSeconds);
                    if (!target.IsValid)
                    {
                        diagnostic = $"Individual Knowledge returned an invalid position Target for ObjectEntryId '{objectEntryId.StableText}'.";
                        return false;
                    }

                    targetList.Add(target);
                }
            }
            else
            {
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
            candidate.TargetId.IdentityKind == (UsesIndividualKnowledge
                ? PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition
                : PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition) &&
            candidate.TargetId.ObjectEntryId.IsValid &&
            (UsesIndividualKnowledge
                ? candidate.TargetId.KnowledgeActorId.IsValid
                : candidate.TargetId.KnowledgeGroupId.IsValid);

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
                plan.ActionId != ActionId ||
                plan.TargetKind != TargetKind ||
                plan.TargetProvenance != PegaNpcPlanningTargetProvenance.DerivedFromKnowledge ||
                plan.TargetId.IdentityKind != (UsesIndividualKnowledge
                    ? PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition
                    : PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition) ||
                !TryReadCurrentPositionEvidence(
                    plan.TargetId,
                    out currentKnownPosition,
                    out evidenceTimeSeconds,
                    out diagnostic) ||
                !currentKnownPosition.Equals(plan.TargetPosition) ||
                !IsEvidenceTimeCompatible(evidenceTimeSeconds, plan.EvidenceTimeSeconds))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? UsesIndividualKnowledge
                        ? "The plan no longer matches this Actor's current Perception-backed Individual Object Entry position fact."
                        : "The plan no longer matches a live, membership-authorized Group Object Entry position fact."
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
                !IsEvidenceTimeCompatible(currentEvidenceTimeSeconds, _executionPlan.EvidenceTimeSeconds))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The authoritative Object Entry position fact changed during navigation."
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
                !IsEvidenceTimeCompatible(currentEvidenceTimeSeconds, _executionPlan.EvidenceTimeSeconds))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The currently authorized position fact no longer matches the Action's destination."
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
            diagnostic = string.Empty;
            PegaNpcPlanningTargetIdentityKind expectedIdentity = UsesIndividualKnowledge
                ? PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition
                : PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition;
            if (targetId.IdentityKind != expectedIdentity || !targetId.ObjectEntryId.IsValid)
            {
                diagnostic = "The Action requires a typed ObjectEntryId and the configured Knowledge authority from its selected Target.";
                return false;
            }

            if (UsesIndividualKnowledge)
            {
                if (_individualKnowledgeState == null ||
                    !TryGetActorId(out ActorId actorId, out diagnostic) ||
                    targetId.KnowledgeActorId != actorId)
                {
                    diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                        ? "The selected Individual position Target is not authorized by this Actor's identity."
                        : diagnostic;
                    return false;
                }

                PegaKnowledgeFact<Vector3> individualPosition = _individualKnowledgeState.ReadFact(
                    PegaObjectEntryKnowledgeFacts.Position.For(targetId.ObjectEntryId),
                    Time.timeAsDouble);
                if (individualPosition.Scope != PegaKnowledgeScope.Individual ||
                    !individualPosition.TryGetValue(out knownPosition) ||
                    !individualPosition.HasObservationTime ||
                    !individualPosition.HasOrigin(PegaNpcEvidenceOrigin.Perception))
                {
                    diagnostic = "The selected Individual position fact is no longer current Perception-backed Knowledge.";
                    return false;
                }

                evidenceTimeSeconds = individualPosition.ObservationTimeSeconds;
                diagnostic = string.Empty;
                return true;
            }

            if (_groupKnowledgeReader == null || !targetId.KnowledgeGroupId.IsValid)
            {
                diagnostic = "The Action requires its composed Group Knowledge Reader and a typed GroupId.";
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

        private bool TryGetComposedIndividualKnowledge(
            IGoapKnowledgeStateView composedKnowledgeState,
            out PegaNpcKnowledgeState knowledgeState,
            out string diagnostic)
        {
            knowledgeState = null;
            if (composedKnowledgeState == null || composedKnowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                diagnostic = "The Action requires the NPC's explicit composed Knowledge State view.";
                return false;
            }

            for (int index = 0; index < composedKnowledgeState.Capabilities.Count; index++)
            {
                if (!(composedKnowledgeState.Capabilities[index] is PegaNpcKnowledgeState candidate))
                {
                    continue;
                }

                if (knowledgeState != null && knowledgeState != candidate)
                {
                    diagnostic = "More than one Individual Knowledge State is composed for this NPC; the Action will not choose an authority implicitly.";
                    return false;
                }

                knowledgeState = candidate;
            }

            if (knowledgeState == null || knowledgeState != _individualKnowledgeState)
            {
                diagnostic = "The Action's Individual Knowledge State is absent from the NPC's composed Knowledge State.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryGetActorId(out ActorId actorId, out string diagnostic)
        {
            actorId = default;
            ActorDeclaration declaration = ActorRoot != null
                ? ActorRoot.GetComponent<ActorDeclaration>()
                : null;
            if (declaration == null)
            {
                diagnostic = "The Actor has no Framework ActorDeclaration.";
                return false;
            }

            try
            {
                actorId = declaration.ActorId;
                diagnostic = string.Empty;
                return actorId.IsValid;
            }
            catch (ArgumentException exception)
            {
                diagnostic = exception.Message;
                return false;
            }
        }

        private bool IsEvidenceTimeCompatible(double currentEvidenceTimeSeconds, double plannedEvidenceTimeSeconds) =>
            UsesIndividualKnowledge
                ? currentEvidenceTimeSeconds >= plannedEvidenceTimeSeconds - 0.000001d
                : Math.Abs(currentEvidenceTimeSeconds - plannedEvidenceTimeSeconds) <= 0.000001d;

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

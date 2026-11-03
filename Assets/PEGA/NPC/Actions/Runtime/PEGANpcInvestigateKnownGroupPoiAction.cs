using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using ImmersiveGames.Goap;
using PEGA.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    [CreateAssetMenu(
        fileName = "InvestigateKnownGroupPoi",
        menuName = "PEGA/NPC/Investigation/Investigate Known Group POI")]
    public sealed class PEGANpcInvestigateKnownGroupPoiAction : PEGANpcActionProviderAsset
    {
        [SerializeField]
        [Tooltip("Group Profile whose membership-authorized manifest identifies the POIs for this Action.")]
        private GroupProfile activityGroup;

        [SerializeField, Min(0.1f)]
        [Tooltip("Continuous time in seconds the Actor must remain at the POI while all investigation conditions remain valid.")]
        private float investigationDurationSeconds = 2f;

        private PEGAActorGroupKnowledgeReader _groupKnowledgeReader;
        private PEGANpcPlanSnapshot _executionPlan;
        private PEGAPoiInvestigationCompletedEffect _executionEffect;
        private PEGANpcPoiInvestigationEvidence _completionEvidence;
        private double _startedAtTimeSeconds;
        private double _completionEvidenceTimeSeconds;
        private bool _hasExecutionPlan;
        private bool _hasCompletionEvidence;

        protected override PEGANpcActionEffectKind ActionEffectKind =>
            PEGANpcActionEffectKind.CompleteGroupPoiInvestigation;

        public override PEGANpcActionContract Contract => new PEGANpcActionContract(
            PEGANpcPlanningActionId.InvestigateKnownGroupPoi,
            ActionEffectKind,
            PEGANpcPlanningTargetKind.GroupObjectEntryPosition,
            false,
            "NPC began investigating a pending POI from its authorized Group manifest.");

        public override float IntrinsicDurationSeconds => IsPositiveFinite(investigationDurationSeconds)
            ? investigationDurationSeconds
            : float.PositiveInfinity;

        protected override bool OnRuntimeInitialize(out string diagnostic)
        {
            if (!base.OnRuntimeInitialize(out diagnostic))
            {
                return false;
            }

            _groupKnowledgeReader = ActorRoot.GetComponent<PEGAActorGroupKnowledgeReader>();
            if (_groupKnowledgeReader == null || _groupKnowledgeReader.gameObject != ActorRoot ||
                !TryGetActivityGroupId(out _) || !IsPositiveFinite(investigationDurationSeconds))
            {
                diagnostic = "The Action requires the Actor's Group Knowledge Reader, a valid activity Group Profile and a finite positive dwell duration.";
                return false;
            }

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
            diagnostic = string.Empty;
            if (context.PlanContinuation.HasValue)
            {
                return true;
            }

            if (!IsPositiveFinite(investigationDurationSeconds) ||
                !TryGetActivityGroupId(out GroupId groupId) ||
                !TryGetComposedReader(context.ComposedKnowledgeState, out PEGAActorGroupKnowledgeReader reader, out diagnostic))
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The investigation Action has no valid Group Profile or dwell duration.";
                }

                return false;
            }

            PEGAActorGroupKnowledgeFactSetReadResult<ObjectEntryId, bool> manifest = reader.ReadGroupFacts(
                PEGAObjectEntryKnowledgeFacts.InvestigationRequired,
                Time.timeAsDouble);
            if (!manifest.IsAuthorized)
            {
                diagnostic = string.IsNullOrWhiteSpace(manifest.Diagnostic)
                    ? $"The Group investigation manifest is unavailable ({manifest.AccessStatus})."
                    : manifest.Diagnostic;
                return false;
            }

            for (int groupIndex = 0; groupIndex < manifest.Groups.Count; groupIndex++)
            {
                PEGAActorGroupKnowledgeFactSetSnapshot<ObjectEntryId, bool> group = manifest.Groups[groupIndex];
                if (group.GroupId != groupId)
                {
                    continue;
                }

                for (int factIndex = 0; factIndex < group.Facts.Count; factIndex++)
                {
                    PEGAKnowledgeFactSnapshot<ObjectEntryId, bool> requiredFact = group.Facts[factIndex];
                    if (!requiredFact.Key.IsValid || requiredFact.Fact.Scope != PEGAKnowledgeScope.Group ||
                        !requiredFact.Fact.TryGetValue(out bool isRequired) || !isRequired)
                    {
                        continue;
                    }

                    ObjectEntryId objectEntryId = requiredFact.Key.AddressValue;
                    if (!TryReadPendingTarget(reader, groupId, objectEntryId,
                            out Vector3 knownPosition, out double positionEvidenceTimeSeconds, out string targetDiagnostic))
                    {
                        continue;
                    }

                    PEGANpcPlanningTarget target = PEGANpcPlanningTarget.FromKnownGroupObjectEntryPosition(
                        objectEntryId,
                        groupId,
                        knownPosition,
                        positionEvidenceTimeSeconds);
                    if (!target.IsValid)
                    {
                        diagnostic = $"Group Knowledge returned an invalid investigation Target for ObjectEntryId '{objectEntryId.StableText}'.";
                        return false;
                    }

                    targetList.Add(target);
                    candidateList.Add(new PEGANpcPlanningActionCandidate(
                        Contract,
                        this,
                        target.TargetId,
                        PEGAActorMovementSpeedRegime.Movement,
                        IntrinsicDurationSeconds));
                }
            }

            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());
            return true;
        }

        public override bool CanOfferCandidate(
            PEGANpcPlanningActionCandidate candidate,
            PEGANpcPlanContinuation? continuation) =>
            !continuation.HasValue && CandidateMatchesContract(candidate) &&
            candidate.TargetId.IdentityKind == PEGANpcPlanningTargetIdentityKind.GroupObjectEntryPosition &&
            candidate.TargetId.ObjectEntryId.IsValid &&
            TryGetActivityGroupId(out GroupId groupId) &&
            candidate.TargetId.KnowledgeGroupId == groupId;

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
            if (continuation.HasValue || plan.Purpose != PEGANpcPlanPurpose.DesireGoal ||
                plan.ActionId != Contract.ActionId || plan.TargetKind != Contract.TargetKind ||
                plan.TargetProvenance != PEGANpcPlanningTargetProvenance.DerivedFromKnowledge ||
                !TryReadPendingTarget(plan.TargetId, out currentKnownPosition, out evidenceTimeSeconds, out diagnostic) ||
                !currentKnownPosition.Equals(plan.TargetPosition) ||
                Math.Abs(evidenceTimeSeconds - plan.EvidenceTimeSeconds) > 0.000001d)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The selected Action no longer matches a pending POI and its current Group-known position evidence."
                    : diagnostic;
                return false;
            }

            return true;
        }

        public override bool TryStart(
            PEGANpcPlanSnapshot plan,
            PEGANpcPlanContinuation? continuation,
            out string diagnostic)
        {
            _hasExecutionPlan = false;
            _hasCompletionEvidence = false;
            _executionEffect = null;
            if (!IsCurrentPlanValid(plan, continuation, out Vector3 position, out _, out diagnostic))
            {
                RejectStart(string.IsNullOrWhiteSpace(diagnostic)
                    ? "The selected POI is no longer pending in Group Knowledge."
                    : diagnostic);
                return false;
            }

            _executionEffect = FindDeclaredEffect(plan);
            if (_executionEffect == null || !TryStartStationaryAction(plan.TargetId, position, out diagnostic))
            {
                if (_executionEffect == null)
                {
                    diagnostic = "The selected causal Action has no declared POI investigation Effect.";
                    RejectStart(diagnostic);
                }

                return false;
            }

            _executionPlan = plan;
            _startedAtTimeSeconds = Time.timeAsDouble;
            _hasExecutionPlan = true;
            return true;
        }

        public override bool TryCreateCausalActionDefinition(
            PEGANpcPlanningActionCandidate candidate,
            PEGANpcGoapKnownTarget target,
            out GoapActionDefinition actionDefinition,
            out string diagnostic)
        {
            actionDefinition = null;
            diagnostic = string.Empty;
            if (!CanOfferCandidate(candidate, null) || target == null ||
                target.Target.TargetId != candidate.TargetId ||
                !TryReadPendingTarget(candidate.TargetId,
                    out Vector3 position, out double evidenceTimeSeconds, out diagnostic) ||
                !position.Equals(target.Target.KnownPosition) ||
                Math.Abs(evidenceTimeSeconds - target.Target.EvidenceTimeSeconds) > 0.000001d)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The causal investigation candidate is not a currently pending Group-known POI."
                    : diagnostic;
                return false;
            }

            actionDefinition = new GoapActionDefinition(
                new IGoapCondition[]
                {
                    new PEGAPoiInvestigationCanStartCondition(
                        candidate.TargetId.ObjectEntryId,
                        candidate.TargetId.KnowledgeGroupId,
                        position,
                        evidenceTimeSeconds)
                },
                new IGoapEffect[]
                {
                    new PEGAPoiInvestigationCompletedEffect(
                        candidate.TargetId.ObjectEntryId,
                        candidate.TargetId.KnowledgeGroupId)
                });
            diagnostic = string.Empty;
            return true;
        }

        public override PEGANpcCausalStepEvaluationStatus EvaluateCausalStep(
            PEGANpcPlanningActionCandidate candidate,
            IGoapHypotheticalStateView stateBeforeStep,
            PEGANpcCausalPlanningKnowledge knowledge,
            out PEGANpcNavigationRouteEstimate routeEstimate,
            out float estimatedSeconds,
            out string diagnostic)
        {
            routeEstimate = PEGANpcNavigationRouteEstimate.NotRequired;
            estimatedSeconds = 0f;
            diagnostic = string.Empty;
            if (stateBeforeStep == null || stateBeforeStep.Source != GoapStateSource.HypotheticalState ||
                knowledge == null || !CanOfferCandidate(candidate, null) ||
                !TryCreateCausalActionDefinition(candidate,
                    knowledge.TryGetKnownTarget(candidate.TargetId, out PEGANpcGoapKnownTarget target) ? target : null,
                    out GoapActionDefinition definition,
                    out diagnostic))
            {
                return PEGANpcCausalStepEvaluationStatus.Unavailable;
            }

            for (int index = 0; index < definition.SuccessPreconditions.Count; index++)
            {
                GoapTruthValue result = definition.SuccessPreconditions[index].Evaluate(stateBeforeStep);
                if (result != GoapTruthValue.True)
                {
                    diagnostic = "The hypothetical state does not currently prove arrival at this pending POI.";
                    return result == GoapTruthValue.Unknown
                        ? PEGANpcCausalStepEvaluationStatus.Unknown
                        : PEGANpcCausalStepEvaluationStatus.Unavailable;
                }
            }

            if (!IsPositiveFinite(investigationDurationSeconds))
            {
                diagnostic = "The configured investigation dwell duration is invalid.";
                return PEGANpcCausalStepEvaluationStatus.Unknown;
            }

            estimatedSeconds = investigationDurationSeconds;
            return PEGANpcCausalStepEvaluationStatus.Available;
        }

        public override bool TryGetObservedCausalEffects(
            PEGANpcPlanSnapshot plan,
            PEGANpcObservedActionEffect observedEffect,
            out IReadOnlyList<IGoapEffect> observedEffects,
            out string diagnostic)
        {
            observedEffects = Array.Empty<IGoapEffect>();
            diagnostic = string.Empty;
            PEGAPoiInvestigationCompletedEffect declared = FindDeclaredEffect(plan);
            if (!_hasCompletionEvidence || declared == null ||
                plan.ActionId != Contract.ActionId || observedEffect.Kind != Contract.EffectKind ||
                observedEffect.RequestId != CurrentExecutionId ||
                observedEffect.TargetId != plan.TargetId ||
                declared.ObjectEntryId != plan.TargetId.ObjectEntryId ||
                declared.GroupId != plan.TargetId.KnowledgeGroupId ||
                !IsActionSpecificEffectObserved(out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The investigation completion was not confirmed by its accepted Group fact and readback."
                    : diagnostic;
                return false;
            }

            observedEffects = Array.AsReadOnly<IGoapEffect>(new[] { declared });
            diagnostic = string.Empty;
            return true;
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_hasExecutionPlan ||
                !TryReadPendingTarget(_executionPlan.TargetId,
                    out Vector3 position, out double evidenceTimeSeconds, out diagnostic) ||
                !position.Equals(_executionPlan.TargetPosition) ||
                Math.Abs(evidenceTimeSeconds - _executionPlan.EvidenceTimeSeconds) > 0.000001d)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The POI stopped being pending or its authorized Group position changed during investigation."
                    : diagnostic;
                return false;
            }

            float distance = PlanarDistance(ActorTransform.position, position);
            if (distance > ArrivalRadiusMeters)
            {
                diagnostic = "The Actor left the configured POI investigation radius before the dwell completed.";
                return false;
            }

            return true;
        }

        protected override void TickNonNavigationAction()
        {
            if (Time.timeAsDouble - _startedAtTimeSeconds < investigationDurationSeconds)
            {
                return;
            }

            if (!ArePreconditionsStillValid(out string diagnostic) || !TryCommitGroupCompletion(out diagnostic))
            {
                CompleteAction(
                    PEGANpcActionOutcome.Invalid,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "The investigation could not publish an accepted, readable Group completion fact."
                        : diagnostic);
                return;
            }

            CompleteAction(
                PEGANpcActionOutcome.Success,
                "The Actor completed the configured dwell and read back its accepted Group investigation fact.");
        }

        protected override bool IsActionSpecificEffectObserved(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_hasCompletionEvidence || !_hasExecutionPlan ||
                !TryReadCurrentPosition(_executionPlan.TargetId,
                    out Vector3 position, out double positionEvidenceTimeSeconds, out diagnostic) ||
                !position.Equals(_executionPlan.TargetPosition) ||
                Math.Abs(positionEvidenceTimeSeconds - _executionPlan.EvidenceTimeSeconds) > 0.000001d ||
                PlanarDistance(ActorTransform.position, position) > ArrivalRadiusMeters ||
                !TryReadCommittedCompletion(_completionEvidence, _completionEvidenceTimeSeconds))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Actor is no longer present and authorized at the POI or the committed Group fact is no longer current."
                    : diagnostic;
                return false;
            }

            return true;
        }

        private bool TryCommitGroupCompletion(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_hasExecutionPlan || !TryGetActorId(out ActorId actorId, out diagnostic) ||
                !TryGetActivityGroupId(out GroupId groupId))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The current Actor identity or activity Group is unavailable for the completion write."
                    : diagnostic;
                return false;
            }

            PEGANpcPoiInvestigationEvidence evidenceOrigin = new PEGANpcPoiInvestigationEvidence(
                actorId,
                groupId,
                _executionPlan.TargetId.ObjectEntryId,
                CurrentExecutionId);
            if (!evidenceOrigin.IsValid)
            {
                diagnostic = "The completion write requires a valid Actor, Group, POI and Action execution identity.";
                return false;
            }

            double evidenceTimeSeconds = Time.timeAsDouble;
            PEGAKnowledgeEvidence evidence = new PEGAKnowledgeEvidence(
                PEGAKnowledgeEvidenceOrigin.From(evidenceOrigin),
                evidenceTimeSeconds,
                evidenceOrigin,
                "poi-investigation-completed");
            PEGAActorGroupKnowledgeAccessStatus access = _groupKnowledgeReader.SubmitGroupFact(
                groupId,
                PEGAObjectEntryKnowledgeFacts.InvestigationCompleted.For(evidenceOrigin.ObjectEntryId),
                true,
                evidence,
                out PEGAKnowledgeWriteResult writeResult);
            if (access != PEGAActorGroupKnowledgeAccessStatus.Authorized || !IsAcceptedWrite(writeResult) ||
                !TryReadCommittedCompletion(evidenceOrigin, evidenceTimeSeconds))
            {
                diagnostic = $"Group completion write was not accepted and read back for this execution (access={access}, write={writeResult}).";
                return false;
            }

            _completionEvidence = evidenceOrigin;
            _completionEvidenceTimeSeconds = evidenceTimeSeconds;
            _hasCompletionEvidence = true;
            return true;
        }

        private bool TryReadCommittedCompletion(
            PEGANpcPoiInvestigationEvidence expectedEvidence,
            double expectedEvidenceTimeSeconds)
        {
            if (_groupKnowledgeReader == null || !expectedEvidence.IsValid)
            {
                return false;
            }

            PEGAActorGroupKnowledgeReadResult<bool> result = _groupKnowledgeReader.ReadGroupFact(
                expectedEvidence.GroupId,
                PEGAObjectEntryKnowledgeFacts.InvestigationCompleted.For(expectedEvidence.ObjectEntryId),
                Time.timeAsDouble);
            PEGAActorGroupKnowledgeReadResult<bool> required = _groupKnowledgeReader.ReadGroupFact(
                expectedEvidence.GroupId,
                PEGAObjectEntryKnowledgeFacts.InvestigationRequired.For(expectedEvidence.ObjectEntryId),
                Time.timeAsDouble);
            if (!result.IsAuthorized || result.Fact.Scope != PEGAKnowledgeScope.Group ||
                !result.Fact.TryGetValue(out bool completed) || !completed ||
                !result.Fact.HasObservationTime ||
                Math.Abs(result.Fact.ObservationTimeSeconds - expectedEvidenceTimeSeconds) > 0.000001d ||
                !required.IsAuthorized || required.Fact.Scope != PEGAKnowledgeScope.Group ||
                !required.Fact.TryGetValue(out bool isRequired) || !isRequired)
            {
                return false;
            }

            for (int index = 0; index < result.Fact.Evidence.Count; index++)
            {
                PEGAKnowledgeEvidence evidence = result.Fact.Evidence[index];
                if (Math.Abs(evidence.ObservationTimeSeconds - expectedEvidenceTimeSeconds) <= 0.000001d &&
                    evidence.Source is PEGANpcPoiInvestigationEvidence observed &&
                    observed.Equals(expectedEvidence))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryReadPendingTarget(
            PEGANpcPlanningTargetId targetId,
            out Vector3 position,
            out double positionEvidenceTimeSeconds,
            out string diagnostic)
        {
            position = default;
            positionEvidenceTimeSeconds = 0d;
            if (!TryGetActivityGroupId(out GroupId groupId) ||
                targetId.IdentityKind != PEGANpcPlanningTargetIdentityKind.GroupObjectEntryPosition ||
                targetId.KnowledgeGroupId != groupId || !targetId.ObjectEntryId.IsValid)
            {
                diagnostic = "The Action requires the selected logical POI to belong to its configured activity Group.";
                return false;
            }

            return TryReadPendingTarget(
                _groupKnowledgeReader,
                groupId,
                targetId.ObjectEntryId,
                out position,
                out positionEvidenceTimeSeconds,
                out diagnostic);
        }

        private static bool TryReadPendingTarget(
            PEGAActorGroupKnowledgeReader reader,
            GroupId groupId,
            ObjectEntryId objectEntryId,
            out Vector3 position,
            out double positionEvidenceTimeSeconds,
            out string diagnostic)
        {
            position = default;
            positionEvidenceTimeSeconds = 0d;
            diagnostic = string.Empty;
            PEGAActorGroupKnowledgeReadResult<bool> required = reader.ReadGroupFact(
                groupId,
                PEGAObjectEntryKnowledgeFacts.InvestigationRequired.For(objectEntryId),
                Time.timeAsDouble);
            PEGAActorGroupKnowledgeReadResult<bool> completed = reader.ReadGroupFact(
                groupId,
                PEGAObjectEntryKnowledgeFacts.InvestigationCompleted.For(objectEntryId),
                Time.timeAsDouble);
            PEGAActorGroupKnowledgeReadResult<Vector3> positionRead = reader.ReadGroupFact(
                groupId,
                PEGAObjectEntryKnowledgeFacts.Position.For(objectEntryId),
                Time.timeAsDouble);
            if (!required.IsAuthorized || required.Fact.Scope != PEGAKnowledgeScope.Group ||
                !required.Fact.TryGetValue(out bool isRequired) || !isRequired ||
                !completed.IsAuthorized || completed.Fact.Scope != PEGAKnowledgeScope.Group ||
                !completed.Fact.TryGetValue(out bool isCompleted) || isCompleted ||
                !positionRead.IsAuthorized || positionRead.Fact.Scope != PEGAKnowledgeScope.Group ||
                !positionRead.Fact.TryGetValue(out position) || !positionRead.Fact.HasObservationTime)
            {
                diagnostic = "The POI must be known as required, explicitly pending and positioned in the same authorized Group Knowledge.";
                return false;
            }

            positionEvidenceTimeSeconds = positionRead.Fact.ObservationTimeSeconds;
            return true;
        }

        private bool TryReadCurrentPosition(
            PEGANpcPlanningTargetId targetId,
            out Vector3 position,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            position = default;
            evidenceTimeSeconds = 0d;
            if (targetId.IdentityKind != PEGANpcPlanningTargetIdentityKind.GroupObjectEntryPosition ||
                !targetId.KnowledgeGroupId.IsValid || !targetId.ObjectEntryId.IsValid)
            {
                diagnostic = "The completion Effect has no valid Group Object Entry identity.";
                return false;
            }

            PEGAActorGroupKnowledgeReadResult<Vector3> result = _groupKnowledgeReader.ReadGroupFact(
                targetId.KnowledgeGroupId,
                PEGAObjectEntryKnowledgeFacts.Position.For(targetId.ObjectEntryId),
                Time.timeAsDouble);
            if (!result.IsAuthorized || result.Fact.Scope != PEGAKnowledgeScope.Group ||
                !result.Fact.TryGetValue(out position) || !result.Fact.HasObservationTime)
            {
                diagnostic = "The Actor no longer has membership-authorized access to the POI position fact.";
                return false;
            }

            evidenceTimeSeconds = result.Fact.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        private bool TryGetComposedReader(
            IGoapKnowledgeStateView composedKnowledgeState,
            out PEGAActorGroupKnowledgeReader reader,
            out string diagnostic)
        {
            reader = null;
            if (composedKnowledgeState == null || composedKnowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                diagnostic = "The Action requires an explicit Knowledge State view.";
                return false;
            }

            for (int index = 0; index < composedKnowledgeState.Capabilities.Count; index++)
            {
                if (!(composedKnowledgeState.Capabilities[index] is PEGAActorGroupKnowledgeReader candidate))
                {
                    continue;
                }

                if (reader != null && reader != candidate)
                {
                    diagnostic = "More than one Group Knowledge Reader is composed for this NPC.";
                    return false;
                }

                reader = candidate;
            }

            bool matches = reader != null && reader == _groupKnowledgeReader;
            diagnostic = matches ? string.Empty : "The Action's Group Knowledge Reader is not in the composed Knowledge State.";
            return matches;
        }

        private bool TryGetActivityGroupId(out GroupId groupId)
        {
            groupId = default;
            return activityGroup != null &&
                   activityGroup.TryGetGroupId(out groupId, out _) &&
                   groupId.IsValid;
        }

        private static PEGAPoiInvestigationCompletedEffect FindDeclaredEffect(PEGANpcPlanSnapshot plan)
        {
            for (int index = 0; index < plan.CausalEffects.Count; index++)
            {
                if (plan.CausalEffects[index] is PEGAPoiInvestigationCompletedEffect effect)
                {
                    return effect;
                }
            }

            return null;
        }

        private bool TryGetActorId(out ActorId actorId, out string diagnostic)
        {
            actorId = default;
            diagnostic = string.Empty;
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
                return actorId.IsValid;
            }
            catch (ArgumentException exception)
            {
                diagnostic = exception.Message;
                return false;
            }
        }

        private static bool IsAcceptedWrite(PEGAKnowledgeWriteResult result) =>
            result == PEGAKnowledgeWriteResult.Recorded ||
            result == PEGAKnowledgeWriteResult.Replaced ||
            result == PEGAKnowledgeWriteResult.Merged ||
            result == PEGAKnowledgeWriteResult.NoChange;

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            Vector3 delta = left - right;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.GOAP;
using PEGA.Movement.Actor;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Goals.Conditions;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Group;
using UnityEngine;
namespace PEGA.NPC.Actions.Runtime
{
    [CreateAssetMenu(
        fileName = "InvestigateKnownGroupPoi",
        menuName = "PEGA/NPC/Investigation/Investigate Known Group POI")]
    public sealed class PegaNpcInvestigateKnownGroupPoiAction : PegaNpcActionProviderAsset
    {
        [SerializeField]
        [Tooltip("Group Profile whose membership-authorized manifest identifies the POIs for this Action.")]
        private GroupProfile activityGroup;

        [SerializeField, Min(0.1f)]
        [Tooltip("Continuous time in seconds the Actor must remain at the POI while all investigation conditions remain valid.")]
        private float investigationDurationSeconds = 2f;

        private PegaActorGroupKnowledgeReader _groupKnowledgeReader;
        private PegaNpcPlanSnapshot _executionPlan;
        private PegaPoiInvestigationCompletedEffect _executionEffect;
        private PegaNpcPoiInvestigationEvidence _completionEvidence;
        private double _startedAtTimeSeconds;
        private double _completionEvidenceTimeSeconds;
        private bool _hasExecutionPlan;
        private bool _hasCompletionEvidence;

        protected override PegaNpcActionEffectKind ActionEffectKind =>
            PegaNpcActionEffectKind.CompleteGroupPoiInvestigation;

        public override PegaNpcActionContract Contract => new PegaNpcActionContract(
            PegaNpcPlanningActionId.InvestigateKnownGroupPoi,
            ActionEffectKind,
            PegaNpcPlanningTargetKind.GroupObjectEntryPosition,
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

            _groupKnowledgeReader = ActorRoot.GetComponent<PegaActorGroupKnowledgeReader>();
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
            PegaNpcActionPlanningContext context,
            out IReadOnlyList<PegaNpcPlanningTarget> targets,
            out IReadOnlyList<PegaNpcPlanningActionCandidate> candidates,
            out string diagnostic)
        {
            List<PegaNpcPlanningTarget> targetList = new List<PegaNpcPlanningTarget>();
            List<PegaNpcPlanningActionCandidate> candidateList = new List<PegaNpcPlanningActionCandidate>();
            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());
            diagnostic = string.Empty;
            if (context.PlanContinuation.HasValue)
            {
                return true;
            }

            if (!IsPositiveFinite(investigationDurationSeconds) ||
                !TryGetActivityGroupId(out GroupId groupId) ||
                !TryGetComposedReader(context.ComposedKnowledgeState, out PegaActorGroupKnowledgeReader reader, out diagnostic))
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The investigation Action has no valid Group Profile or dwell duration.";
                }

                return false;
            }

            PegaActorGroupKnowledgeFactSetReadResult<ObjectEntryId, bool> manifest = reader.ReadGroupFacts(
                PegaObjectEntryKnowledgeFacts.InvestigationRequired,
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
                PegaActorGroupKnowledgeFactSetSnapshot<ObjectEntryId, bool> group = manifest.Groups[groupIndex];
                if (group.GroupId != groupId)
                {
                    continue;
                }

                for (int factIndex = 0; factIndex < group.Facts.Count; factIndex++)
                {
                    PegaKnowledgeFactSnapshot<ObjectEntryId, bool> requiredFact = group.Facts[factIndex];
                    if (!requiredFact.Key.IsValid || requiredFact.Fact.Scope != PegaKnowledgeScope.Group ||
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

                    PegaNpcPlanningTarget target = PegaNpcPlanningTarget.FromKnownGroupObjectEntryPosition(
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
                    candidateList.Add(new PegaNpcPlanningActionCandidate(
                        Contract,
                        this,
                        target.TargetId,
                        PegaActorMovementSpeedRegime.Movement,
                        IntrinsicDurationSeconds));
                }
            }

            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());
            return true;
        }

        public override bool CanOfferCandidate(
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcPlanContinuation? continuation) =>
            !continuation.HasValue && CandidateMatchesContract(candidate) &&
            candidate.TargetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition &&
            candidate.TargetId.ObjectEntryId.IsValid &&
            TryGetActivityGroupId(out GroupId groupId) &&
            candidate.TargetId.KnowledgeGroupId == groupId;

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
            if (continuation.HasValue || plan.Purpose != PegaNpcPlanPurpose.DesireGoal ||
                plan.ActionId != Contract.ActionId || plan.TargetKind != Contract.TargetKind ||
                plan.TargetProvenance != PegaNpcPlanningTargetProvenance.DerivedFromKnowledge ||
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
            PegaNpcPlanSnapshot plan,
            PegaNpcPlanContinuation? continuation,
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
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcGoapKnownTarget target,
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
                    new PegaPoiInvestigationCanStartCondition(
                        candidate.TargetId.ObjectEntryId,
                        candidate.TargetId.KnowledgeGroupId,
                        position,
                        evidenceTimeSeconds)
                },
                new IGoapEffect[]
                {
                    new PegaPoiInvestigationCompletedEffect(
                        candidate.TargetId.ObjectEntryId,
                        candidate.TargetId.KnowledgeGroupId)
                });
            diagnostic = string.Empty;
            return true;
        }

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
            if (stateBeforeStep == null || stateBeforeStep.Source != GoapStateSource.HypotheticalState ||
                knowledge == null || !CanOfferCandidate(candidate, null) ||
                !TryCreateCausalActionDefinition(candidate,
                    knowledge.TryGetKnownTarget(candidate.TargetId, out PegaNpcGoapKnownTarget target) ? target : null,
                    out GoapActionDefinition definition,
                    out diagnostic))
            {
                return PegaNpcCausalStepEvaluationStatus.Unavailable;
            }

            for (int index = 0; index < definition.SuccessPreconditions.Count; index++)
            {
                GoapTruthValue result = definition.SuccessPreconditions[index].Evaluate(stateBeforeStep);
                if (result != GoapTruthValue.True)
                {
                    diagnostic = "The hypothetical state does not currently prove arrival at this pending POI.";
                    return result == GoapTruthValue.Unknown
                        ? PegaNpcCausalStepEvaluationStatus.Unknown
                        : PegaNpcCausalStepEvaluationStatus.Unavailable;
                }
            }

            if (!IsPositiveFinite(investigationDurationSeconds))
            {
                diagnostic = "The configured investigation dwell duration is invalid.";
                return PegaNpcCausalStepEvaluationStatus.Unknown;
            }

            estimatedSeconds = investigationDurationSeconds;
            return PegaNpcCausalStepEvaluationStatus.Available;
        }

        public override bool TryGetObservedCausalEffects(
            PegaNpcPlanSnapshot plan,
            PegaNpcObservedActionEffect observedEffect,
            out IReadOnlyList<IGoapEffect> observedEffects,
            out string diagnostic)
        {
            observedEffects = Array.Empty<IGoapEffect>();
            diagnostic = string.Empty;
            PegaPoiInvestigationCompletedEffect declared = FindDeclaredEffect(plan);
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
                    PegaNpcActionOutcome.Invalid,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "The investigation could not publish an accepted, readable Group completion fact."
                        : diagnostic);
                return;
            }

            CompleteAction(
                PegaNpcActionOutcome.Success,
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

            PegaNpcPoiInvestigationEvidence evidenceOrigin = new PegaNpcPoiInvestigationEvidence(
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
            PegaKnowledgeEvidence evidence = new PegaKnowledgeEvidence(
                PegaKnowledgeEvidenceOrigin.From(evidenceOrigin),
                evidenceTimeSeconds,
                evidenceOrigin,
                "poi-investigation-completed");
            PegaActorGroupKnowledgeAccessStatus access = _groupKnowledgeReader.SubmitGroupFact(
                groupId,
                PegaObjectEntryKnowledgeFacts.InvestigationCompleted.For(evidenceOrigin.ObjectEntryId),
                true,
                evidence,
                out PegaKnowledgeWriteResult writeResult);
            if (access != PegaActorGroupKnowledgeAccessStatus.Authorized || !IsAcceptedWrite(writeResult) ||
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
            PegaNpcPoiInvestigationEvidence expectedEvidence,
            double expectedEvidenceTimeSeconds)
        {
            if (_groupKnowledgeReader == null || !expectedEvidence.IsValid)
            {
                return false;
            }

            PegaActorGroupKnowledgeReadResult<bool> result = _groupKnowledgeReader.ReadGroupFact(
                expectedEvidence.GroupId,
                PegaObjectEntryKnowledgeFacts.InvestigationCompleted.For(expectedEvidence.ObjectEntryId),
                Time.timeAsDouble);
            PegaActorGroupKnowledgeReadResult<bool> required = _groupKnowledgeReader.ReadGroupFact(
                expectedEvidence.GroupId,
                PegaObjectEntryKnowledgeFacts.InvestigationRequired.For(expectedEvidence.ObjectEntryId),
                Time.timeAsDouble);
            if (!result.IsAuthorized || result.Fact.Scope != PegaKnowledgeScope.Group ||
                !result.Fact.TryGetValue(out bool completed) || !completed ||
                !result.Fact.HasObservationTime ||
                Math.Abs(result.Fact.ObservationTimeSeconds - expectedEvidenceTimeSeconds) > 0.000001d ||
                !required.IsAuthorized || required.Fact.Scope != PegaKnowledgeScope.Group ||
                !required.Fact.TryGetValue(out bool isRequired) || !isRequired)
            {
                return false;
            }

            for (int index = 0; index < result.Fact.Evidence.Count; index++)
            {
                PegaKnowledgeEvidence evidence = result.Fact.Evidence[index];
                if (Math.Abs(evidence.ObservationTimeSeconds - expectedEvidenceTimeSeconds) <= 0.000001d &&
                    evidence.Source is PegaNpcPoiInvestigationEvidence observed &&
                    observed.Equals(expectedEvidence))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryReadPendingTarget(
            PegaNpcPlanningTargetId targetId,
            out Vector3 position,
            out double positionEvidenceTimeSeconds,
            out string diagnostic)
        {
            position = default;
            positionEvidenceTimeSeconds = 0d;
            if (!TryGetActivityGroupId(out GroupId groupId) ||
                targetId.IdentityKind != PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition ||
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
            PegaActorGroupKnowledgeReader reader,
            GroupId groupId,
            ObjectEntryId objectEntryId,
            out Vector3 position,
            out double positionEvidenceTimeSeconds,
            out string diagnostic)
        {
            position = default;
            positionEvidenceTimeSeconds = 0d;
            diagnostic = string.Empty;
            PegaActorGroupKnowledgeReadResult<bool> required = reader.ReadGroupFact(
                groupId,
                PegaObjectEntryKnowledgeFacts.InvestigationRequired.For(objectEntryId),
                Time.timeAsDouble);
            PegaActorGroupKnowledgeReadResult<bool> completed = reader.ReadGroupFact(
                groupId,
                PegaObjectEntryKnowledgeFacts.InvestigationCompleted.For(objectEntryId),
                Time.timeAsDouble);
            PegaActorGroupKnowledgeReadResult<Vector3> positionRead = reader.ReadGroupFact(
                groupId,
                PegaObjectEntryKnowledgeFacts.Position.For(objectEntryId),
                Time.timeAsDouble);
            if (!required.IsAuthorized || required.Fact.Scope != PegaKnowledgeScope.Group ||
                !required.Fact.TryGetValue(out bool isRequired) || !isRequired ||
                !completed.IsAuthorized || completed.Fact.Scope != PegaKnowledgeScope.Group ||
                !completed.Fact.TryGetValue(out bool isCompleted) || isCompleted ||
                !positionRead.IsAuthorized || positionRead.Fact.Scope != PegaKnowledgeScope.Group ||
                !positionRead.Fact.TryGetValue(out position) || !positionRead.Fact.HasObservationTime)
            {
                diagnostic = "The POI must be known as required, explicitly pending and positioned in the same authorized Group Knowledge.";
                return false;
            }

            positionEvidenceTimeSeconds = positionRead.Fact.ObservationTimeSeconds;
            return true;
        }

        private bool TryReadCurrentPosition(
            PegaNpcPlanningTargetId targetId,
            out Vector3 position,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            position = default;
            evidenceTimeSeconds = 0d;
            if (targetId.IdentityKind != PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition ||
                !targetId.KnowledgeGroupId.IsValid || !targetId.ObjectEntryId.IsValid)
            {
                diagnostic = "The completion Effect has no valid Group Object Entry identity.";
                return false;
            }

            PegaActorGroupKnowledgeReadResult<Vector3> result = _groupKnowledgeReader.ReadGroupFact(
                targetId.KnowledgeGroupId,
                PegaObjectEntryKnowledgeFacts.Position.For(targetId.ObjectEntryId),
                Time.timeAsDouble);
            if (!result.IsAuthorized || result.Fact.Scope != PegaKnowledgeScope.Group ||
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
            out PegaActorGroupKnowledgeReader reader,
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
                if (!(composedKnowledgeState.Capabilities[index] is PegaActorGroupKnowledgeReader candidate))
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

        private static PegaPoiInvestigationCompletedEffect FindDeclaredEffect(PegaNpcPlanSnapshot plan)
        {
            for (int index = 0; index < plan.CausalEffects.Count; index++)
            {
                if (plan.CausalEffects[index] is PegaPoiInvestigationCompletedEffect effect)
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

        private static bool IsAcceptedWrite(PegaKnowledgeWriteResult result) =>
            result == PegaKnowledgeWriteResult.Recorded ||
            result == PegaKnowledgeWriteResult.Replaced ||
            result == PegaKnowledgeWriteResult.Merged ||
            result == PegaKnowledgeWriteResult.NoChange;

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

using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using Immersive.Framework.RuntimeContent;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using PEGA.GOAP;
using PEGA.Interactions.Runtime;
using PEGA.Interactions.Runtime.Actor;
using PEGA.Interactions.Runtime.ObjectEntry;
using PEGA.Movement.Actor;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Group;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
namespace PEGA.NPC.Actions.Runtime
{
    public enum PegaNpcPickUpKnowledgeSource
    {
        Group = 0,
        Individual = 10
    }

    [CreateAssetMenu(
        fileName = "PickUpKnownObjectEntry",
        menuName = "PEGA/NPC/Actions/Pick Up Known Object Entry")]
    public sealed class PegaNpcPickUpKnownObjectEntryAction : PegaNpcActionProviderAsset,
        IPegaObjectInteractionOccurrenceAuthority,
        IPegaNpcKnowledgeChangeRelevanceProvider
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.Actions";

        private readonly struct RangeRejectedPickUpAttempt
        {
            public RangeRejectedPickUpAttempt(
                Vector3 actorPosition,
                Vector3 knownTargetPosition,
                float interactionRangeMeters)
            {
                ActorPosition = actorPosition;
                KnownTargetPosition = knownTargetPosition;
                InteractionRangeMeters = interactionRangeMeters;
            }

            public Vector3 ActorPosition { get; }
            public Vector3 KnownTargetPosition { get; }
            public float InteractionRangeMeters { get; }
        }

        [SerializeField]
        [Tooltip("Knowledge authority used to offer Pick Up candidates. Individual candidates require current Perception evidence from this Actor.")]
        private PegaNpcPickUpKnowledgeSource knowledgeSource;

        private PegaActorGroupKnowledgeReader _groupKnowledgeReader;
        private PegaNpcKnowledgeState _individualKnowledgeState;
        private PegaActorPhysicalInventory _inventory;
        private PegaNpcObjectInteractionMechanic _interactionMechanic;
        private ObjectEntryOccurrenceQueryBinding _occurrenceQuery;
        private PegaNpcPlanSnapshot _executionPlan;
        private PegaObjectEntryPickUpAction _executionAction;
        private long _objectInteractionExecutionId;
        private long _interactionActionExecutionId;
        private bool _hasExecutionPlan;
        private bool _hasPossessionEvidence;
        private bool _interactionLifecycleActive;
        private bool _interactionStartInProgress;
        private ScopedLogger _planningLog;
        [NonSerialized]
        private Dictionary<PegaNpcPlanningTargetId, RangeRejectedPickUpAttempt> _rangeRejectedAttempts;

        protected override PegaNpcActionEffectKind ActionEffectKind =>
            PegaNpcActionEffectKind.PossessObjectEntry;

        public override PegaNpcActionContract Contract => new PegaNpcActionContract(
            PegaNpcPlanningActionId.PickUpKnownObjectEntry,
            ActionEffectKind,
            UsesIndividualKnowledge
                ? PegaNpcPlanningTargetKind.IndividualObjectEntryPosition
                : PegaNpcPlanningTargetKind.GroupObjectEntryPosition,
            false,
            "NPC began the composed Pick Up action for a known, offered Object Entry.");

        public override bool TryGetRemainingExecutionCostSeconds(
            long requestId,
            out float remainingSeconds,
            out string diagnostic)
        {
            remainingSeconds = 0f;
            diagnostic = string.Empty;
            if (Outcome != PegaNpcActionOutcome.Running ||
                requestId <= 0 || requestId != CurrentExecutionId ||
                !_hasExecutionPlan || _executionAction == null || _objectInteractionExecutionId <= 0 ||
                !_executionAction.TryGetExecution(
                    _objectInteractionExecutionId,
                    out PegaObjectInteractionExecutionStatus interactionStatus,
                    out diagnostic) ||
                interactionStatus != PegaObjectInteractionExecutionStatus.Running ||
                !_executionAction.TryGetRemainingExecutionDurationSeconds(
                    _objectInteractionExecutionId,
                    _executionPlan.IntrinsicDurationSeconds,
                    out remainingSeconds,
                    out diagnostic))
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    diagnostic = "The active Pick Up request has no current shared interaction execution.";
                }

                return false;
            }

            if (float.IsNaN(remainingSeconds) || float.IsInfinity(remainingSeconds))
            {
                remainingSeconds = 0f;
                diagnostic = "The active Pick Up request produced a non-finite remaining duration.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        protected override bool OnRuntimeInitialize(out string diagnostic)
        {
            if (!base.OnRuntimeInitialize(out diagnostic))
            {
                return false;
            }

            _rangeRejectedAttempts = new Dictionary<PegaNpcPlanningTargetId, RangeRejectedPickUpAttempt>();

            _planningLog ??= PegaLogging.CreateScopedLogger<PegaNpcPickUpKnownObjectEntryAction>(
                LogCategory,
                ActorRoot.name);

            if (!Enum.IsDefined(typeof(PegaNpcPickUpKnowledgeSource), knowledgeSource))
            {
                diagnostic = "Pick Up Knowledge source must be Group or Individual.";
                return false;
            }

            _groupKnowledgeReader = ActorRoot.GetComponent<PegaActorGroupKnowledgeReader>();
            _individualKnowledgeState = ActorRoot.GetComponent<PegaNpcKnowledgeState>();
            _inventory = ActorRoot.GetComponent<PegaActorPhysicalInventory>();
            _interactionMechanic = ActorRoot.GetComponent<PegaNpcObjectInteractionMechanic>();
            _occurrenceQuery = RuntimeContext.ObjectEntryOccurrenceQueryBinding;
            bool requiresGroupKnowledge = UsesGroupKnowledge;
            bool requiresIndividualKnowledge = UsesIndividualKnowledge;
            if ((requiresGroupKnowledge &&
                 (_groupKnowledgeReader == null || _groupKnowledgeReader.gameObject != ActorRoot)) ||
                (requiresIndividualKnowledge &&
                 (_individualKnowledgeState == null || _individualKnowledgeState.gameObject != ActorRoot)) ||
                _inventory == null || _inventory.gameObject != ActorRoot ||
                _interactionMechanic == null || _interactionMechanic.gameObject != ActorRoot ||
                _occurrenceQuery == null)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Action requires its selected Knowledge authority, the Actor's inventory and interaction mechanic, and an Activity Object Entry query."
                    : diagnostic;
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
                LogCandidateGeneration(
                    0,
                    0,
                    0,
                    0,
                    _inventory != null && _inventory.HasFreeCapacity,
                    "The active plan continuation owns planning and does not offer Pick Up candidates.");
                return true;
            }

            if (_inventory == null || !_inventory.HasFreeCapacity)
            {
                LogCandidateGeneration(
                    0,
                    0,
                    0,
                    0,
                    false,
                    _inventory == null
                        ? "The NPC physical inventory is unavailable."
                        : "The NPC physical inventory has no free capacity.");
                return true;
            }

            if (!TryGetComposedAuthorities(
                    context,
                    out PegaActorGroupKnowledgeReader groupReader,
                    out PegaNpcKnowledgeState individualKnowledgeState,
                    out diagnostic))
            {
                return false;
            }

            int availableOfferCount = 0;
            int knownPositionCount = 0;
            int validParameterCount = 0;
            string firstSkippedOfferDiagnostic = string.Empty;
            if (UsesGroupKnowledge &&
                !TryAddGroupCandidates(
                    groupReader,
                    targetList,
                    candidateList,
                    ref availableOfferCount,
                    ref knownPositionCount,
                    ref validParameterCount,
                    ref firstSkippedOfferDiagnostic,
                    out diagnostic))
            {
                return false;
            }

            if (UsesIndividualKnowledge &&
                !TryAddIndividualCandidates(
                    individualKnowledgeState,
                    targetList,
                    candidateList,
                    ref availableOfferCount,
                    ref knownPositionCount,
                    ref validParameterCount,
                    ref firstSkippedOfferDiagnostic,
                    out diagnostic))
            {
                return false;
            }

            if (candidateList.Count == 0 && string.IsNullOrWhiteSpace(firstSkippedOfferDiagnostic))
            {
                firstSkippedOfferDiagnostic = availableOfferCount == 0
                    ? "The configured Knowledge authority supplied no currently true PickUpAvailable fact."
                    : knownPositionCount == 0
                        ? "No authorized current position was available for the offered Object Entries."
                        : validParameterCount == 0
                            ? "No valid authorized Pick Up planning parameters were available for the offered Object Entries."
                            : "No Pick Up candidate could be formed from the current Knowledge offers.";
            }

            LogCandidateGeneration(
                availableOfferCount,
                knownPositionCount,
                validParameterCount,
                candidateList.Count,
                true,
                firstSkippedOfferDiagnostic);
            targets = Array.AsReadOnly(targetList.ToArray());
            candidates = Array.AsReadOnly(candidateList.ToArray());
            return true;
        }

        private bool TryAddGroupCandidates(
            PegaActorGroupKnowledgeReader reader,
            List<PegaNpcPlanningTarget> targets,
            List<PegaNpcPlanningActionCandidate> candidates,
            ref int availableOfferCount,
            ref int knownPositionCount,
            ref int validParameterCount,
            ref string firstSkippedOfferDiagnostic,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            PegaActorGroupKnowledgeFactSetReadResult<ObjectEntryId, bool> offers = reader.ReadGroupFacts(
                PegaObjectEntryKnowledgeFacts.PickUpAvailable,
                Time.timeAsDouble);
            if (!offers.IsAuthorized)
            {
                diagnostic = string.IsNullOrWhiteSpace(offers.Diagnostic)
                    ? $"Group Knowledge cannot provide current Object Entry Pick Up offers ({offers.AccessStatus})."
                    : offers.Diagnostic;
                return false;
            }

            for (int groupIndex = 0; groupIndex < offers.Groups.Count; groupIndex++)
            {
                PegaActorGroupKnowledgeFactSetSnapshot<ObjectEntryId, bool> group = offers.Groups[groupIndex];
                if (!group.GroupId.IsValid)
                {
                    diagnostic = "Group Knowledge returned a Pick Up offer set without a valid GroupId.";
                    return false;
                }

                for (int factIndex = 0; factIndex < group.Facts.Count; factIndex++)
                {
                    PegaKnowledgeFactSnapshot<ObjectEntryId, bool> fact = group.Facts[factIndex];
                    if (!fact.Key.IsValid || !fact.Key.AddressValue.IsValid ||
                        fact.Fact.Scope != PegaKnowledgeScope.Group ||
                        !fact.Fact.TryGetValue(out bool isPickUpAvailable) || !isPickUpAvailable)
                    {
                        continue;
                    }

                    availableOfferCount++;
                    ObjectEntryId objectEntryId = fact.Key.AddressValue;
                    if (_inventory.Contains(objectEntryId))
                    {
                        SetFirstDiagnostic(
                            ref firstSkippedOfferDiagnostic,
                            $"Group Pick Up offer '{objectEntryId.StableText}' was excluded because this Actor's physical inventory already contains the Object Entry.");
                        continue;
                    }

                    if (IsKnownLocallyUnavailable(objectEntryId))
                    {
                        SetFirstDiagnostic(
                            ref firstSkippedOfferDiagnostic,
                            $"Group Pick Up offer '{objectEntryId.StableText}' was excluded by this Actor's current Individual availability evidence.");
                        continue;
                    }

                    if (!TryReadKnownTarget(
                            reader,
                            group.GroupId,
                            objectEntryId,
                            out Vector3 knownPosition,
                            out double positionEvidenceTimeSeconds,
                            out string positionDiagnostic))
                    {
                        SetFirstDiagnostic(ref firstSkippedOfferDiagnostic, positionDiagnostic);
                        continue;
                    }

                    knownPositionCount++;
                    if (!TryReadKnownPickUpPlanningParameters(
                            reader,
                            group.GroupId,
                            objectEntryId,
                            out PegaPickUpObjectEntryConfigurationSnapshot parameters,
                            out string parameterDiagnostic))
                    {
                        SetFirstDiagnostic(ref firstSkippedOfferDiagnostic, parameterDiagnostic);
                        continue;
                    }

                    validParameterCount++;
                    PegaNpcPlanningTarget target = PegaNpcPlanningTarget.FromKnownGroupObjectEntryPosition(
                        objectEntryId,
                        group.GroupId,
                        knownPosition,
                        positionEvidenceTimeSeconds);
                    if (!target.IsValid)
                    {
                        diagnostic = $"Group Knowledge returned an invalid Pick Up Target for ObjectEntryId '{objectEntryId.StableText}'.";
                        return false;
                    }

                    if (IsRangeRejectionUnchanged(
                            target.TargetId,
                            knownPosition,
                            parameters.InteractionRangeMeters))
                    {
                        SetFirstDiagnostic(
                            ref firstSkippedOfferDiagnostic,
                            $"Group Pick Up offer '{objectEntryId.StableText}' was not retried because its known target and Actor positions have not changed since the prior out-of-range attempt.");
                        continue;
                    }

                    targets.Add(target);
                    candidates.Add(CreateCandidate(target, parameters));
                }
            }

            return true;
        }

        private bool TryAddIndividualCandidates(
            PegaNpcKnowledgeState knowledgeState,
            List<PegaNpcPlanningTarget> targets,
            List<PegaNpcPlanningActionCandidate> candidates,
            ref int availableOfferCount,
            ref int knownPositionCount,
            ref int validParameterCount,
            ref string firstSkippedOfferDiagnostic,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!TryGetActorId(out ActorId actorId, out diagnostic))
            {
                return false;
            }

            IReadOnlyList<ObjectEntryId> objectEntryIds = knowledgeState.GetKnownObjectEntryIds();
            for (int index = 0; index < objectEntryIds.Count; index++)
            {
                ObjectEntryId objectEntryId = objectEntryIds[index];
                if (_inventory.Contains(objectEntryId))
                {
                    SetFirstDiagnostic(
                        ref firstSkippedOfferDiagnostic,
                        $"Individual Pick Up offer '{objectEntryId.StableText}' was excluded because this Actor's physical inventory already contains the Object Entry.");
                    continue;
                }

                if (IsKnownLocallyUnavailable(objectEntryId))
                {
                    SetFirstDiagnostic(
                        ref firstSkippedOfferDiagnostic,
                        $"Individual Pick Up offer '{objectEntryId.StableText}' was excluded by this Actor's current availability evidence.");
                    continue;
                }

                if (!TryReadKnownIndividualPickUpFacts(
                        knowledgeState,
                        objectEntryId,
                        out Vector3 knownPosition,
                        out PegaPickUpObjectEntryConfigurationSnapshot parameters,
                        out double evidenceTimeSeconds,
                        out string factDiagnostic))
                {
                    SetFirstDiagnostic(ref firstSkippedOfferDiagnostic, factDiagnostic);
                    continue;
                }

                availableOfferCount++;
                knownPositionCount++;
                validParameterCount++;
                PegaNpcPlanningTarget target = PegaNpcPlanningTarget.FromKnownIndividualObjectEntryPosition(
                    objectEntryId,
                    actorId,
                    knownPosition,
                    evidenceTimeSeconds);
                if (!target.IsValid)
                {
                    diagnostic = $"Individual Knowledge returned an invalid Pick Up Target for ObjectEntryId '{objectEntryId.StableText}'.";
                    return false;
                }

                if (IsRangeRejectionUnchanged(
                        target.TargetId,
                        knownPosition,
                        parameters.InteractionRangeMeters))
                {
                    SetFirstDiagnostic(
                        ref firstSkippedOfferDiagnostic,
                        $"Individual Pick Up offer '{objectEntryId.StableText}' was not retried because its known target and Actor positions have not changed since the prior out-of-range attempt.");
                    continue;
                }

                targets.Add(target);
                candidates.Add(CreateCandidate(target, parameters));
            }

            return true;
        }

        private PegaNpcPlanningActionCandidate CreateCandidate(
            PegaNpcPlanningTarget target,
            PegaPickUpObjectEntryConfigurationSnapshot parameters) =>
            new PegaNpcPlanningActionCandidate(
                Contract,
                this,
                target.TargetId,
                PegaActorMovementSpeedRegime.Movement,
                parameters.ExecutionDurationSeconds,
                parameters.InteractionRangeMeters);

        private static void SetFirstDiagnostic(ref string current, string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(diagnostic))
            {
                current = diagnostic;
            }
        }

        private void LogCandidateGeneration(
            int availableOfferCount,
            int knownPositionCount,
            int validParameterCount,
            int candidateCount,
            bool inventoryHasFreeCapacity,
            string diagnostic)
        {
            _planningLog?.Debug(
                "NPC Pick Up provider evaluated authorized Knowledge offers for GOAP candidates.",
                LogFields.Field("actorRoot", ActorRoot != null ? ActorRoot.name : string.Empty),
                LogFields.Field("actionId", Contract.ActionId),
                LogFields.Field("knowledgeSource", knowledgeSource),
                LogFields.Field("availableOfferCount", availableOfferCount),
                LogFields.Field("knownPositionCount", knownPositionCount),
                LogFields.Field("validParameterCount", validParameterCount),
                LogFields.Field("candidateCount", candidateCount),
                LogFields.Field("inventoryHasFreeCapacity", inventoryHasFreeCapacity),
                LogFields.Field("diagnostic", diagnostic ?? string.Empty));
        }

        public override bool CanOfferCandidate(
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcPlanContinuation? continuation)
        {
            if (continuation.HasValue || !CandidateMatchesContract(candidate) ||
                !IsTargetIdentityAuthorized(candidate.TargetId) ||
                _inventory == null || !_inventory.HasFreeCapacity ||
                _inventory.Contains(candidate.TargetId.ObjectEntryId) ||
                IsKnownLocallyUnavailable(candidate.TargetId.ObjectEntryId) ||
                !candidate.TargetRadiusOverrideMeters.HasValue ||
                !IsNonNegativeFinite(candidate.TargetRadiusOverrideMeters.Value) ||
                !IsNonNegativeFinite(candidate.IntrinsicDurationSeconds))
            {
                return false;
            }

            return true;
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
            if (continuation.HasValue || plan.Purpose != PegaNpcPlanPurpose.DesireGoal ||
                plan.ActionId != Contract.ActionId || plan.TargetKind != Contract.TargetKind ||
                plan.TargetProvenance != PegaNpcPlanningTargetProvenance.DerivedFromKnowledge ||
                !IsTargetIdentityAuthorized(plan.TargetId) ||
                !TryReadKnownTarget(
                    plan.TargetId,
                    out currentKnownPosition,
                    out evidenceTimeSeconds,
                    out diagnostic) ||
                !TryReadKnownPickUpPlanningParameters(
                    plan.TargetId,
                    out PegaPickUpObjectEntryConfigurationSnapshot parameters,
                    out diagnostic) ||
                !MatchesPlanParameters(parameters, plan) ||
                !currentKnownPosition.Equals(plan.TargetPosition) ||
                !IsEvidenceTimeCompatible(plan.TargetId, evidenceTimeSeconds, plan.EvidenceTimeSeconds) ||
                _inventory == null || !_inventory.HasFreeCapacity)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The selected Pick Up plan no longer matches its known offer, position or Actor inventory capacity."
                    : diagnostic;
                return false;
            }

            diagnostic = string.Empty;
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
                !TryReadKnownTarget(
                    candidate.TargetId,
                    out Vector3 position,
                    out double evidenceTimeSeconds,
                    out diagnostic) ||
                !TryReadKnownPickUpPlanningParameters(
                    candidate.TargetId,
                    out PegaPickUpObjectEntryConfigurationSnapshot parameters,
                    out diagnostic) ||
                !MatchesCandidateParameters(parameters, candidate) ||
                !position.Equals(target.Target.KnownPosition) ||
                !IsEvidenceTimeCompatible(candidate.TargetId, evidenceTimeSeconds, target.Target.EvidenceTimeSeconds) ||
                !TryGetActorId(out ActorId actorId, out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The causal Pick Up candidate is not supported by current, authorized Object Entry Knowledge."
                    : diagnostic;
                return false;
            }

            actionDefinition = new GoapActionDefinition(
                new IGoapCondition[]
                {
                    new PegaObjectEntryInteractionRangeCondition(
                        candidate.TargetId,
                        ActorTransform.position,
                        position,
                        candidate.TargetRadiusOverrideMeters.Value,
                        _planningLog)
                },
                new IGoapEffect[]
                {
                    new PegaObjectEntryPossessedEffect(actorId, candidate.TargetId.ObjectEntryId)
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
                !knowledge.TryGetKnownTarget(candidate.TargetId, out PegaNpcGoapKnownTarget target) ||
                !TryCreateCausalActionDefinition(candidate, target, out GoapActionDefinition definition, out diagnostic))
            {
                return PegaNpcCausalStepEvaluationStatus.Unavailable;
            }

            for (int index = 0; index < definition.SuccessPreconditions.Count; index++)
            {
                GoapTruthValue result = definition.SuccessPreconditions[index].Evaluate(stateBeforeStep);
                if (result != GoapTruthValue.True)
                {
                    diagnostic = "The hypothetical Actor position is outside the Pick Up interaction range.";
                    return result == GoapTruthValue.Unknown
                        ? PegaNpcCausalStepEvaluationStatus.Unknown
                        : PegaNpcCausalStepEvaluationStatus.Unavailable;
                }
            }

            estimatedSeconds = candidate.IntrinsicDurationSeconds;
            return IsNonNegativeFinite(estimatedSeconds)
                ? PegaNpcCausalStepEvaluationStatus.Available
                : PegaNpcCausalStepEvaluationStatus.Unknown;
        }

        public override bool TryStart(
            PegaNpcPlanSnapshot plan,
            PegaNpcPlanContinuation? continuation,
            out string diagnostic)
        {
            _hasExecutionPlan = false;
            _hasPossessionEvidence = false;
            _executionAction = null;
            _objectInteractionExecutionId = 0;
            _interactionActionExecutionId = 0;
            _interactionLifecycleActive = false;
            _interactionStartInProgress = false;
            if (!IsCurrentPlanValid(
                    plan,
                    continuation,
                    out Vector3 knownPosition,
                    out _,
                    out diagnostic))
            {
                bool expectedPreconditionInvalidation =
                    _inventory != null &&
                    (_inventory.Contains(plan.TargetId.ObjectEntryId) ||
                     !_inventory.HasFreeCapacity ||
                     IsKnownLocallyUnavailable(plan.TargetId.ObjectEntryId));
                RejectStart(string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Pick Up Target is no longer known as available."
                    : diagnostic,
                    expectedPreconditionInvalidation);
                return false;
            }

            _executionPlan = plan;
            _hasExecutionPlan = true;

            if (_occurrenceQuery == null)
            {
                diagnostic = "The selected known Object Entry has no Activity-owned occurrence query.";
                RejectStart(diagnostic);
                return false;
            }

            ObjectEntryOccurrenceResolution resolution = _occurrenceQuery.Resolve(
                StableObjectReference.ForEntry(plan.TargetId.ObjectEntryId));
            GameObject occurrence = resolution.Occurrence;
            ObjectEntryDeclaration declaration = occurrence != null
                ? occurrence.GetComponent<ObjectEntryDeclaration>()
                : null;
            PegaObjectEntryInteractionAggregator aggregator = occurrence != null
                ? occurrence.GetComponent<PegaObjectEntryInteractionAggregator>()
                : null;
            if (!resolution.Succeeded || declaration == null ||
                !declaration.TryGetObjectEntryId(out ObjectEntryId resolvedId) ||
                resolvedId != plan.TargetId.ObjectEntryId || aggregator == null ||
                !PegaObjectInteractionRequest.TryCreate(
                    PegaObjectInteractionParticipant.Npc,
                    ActorRoot,
                    occurrence,
                    this,
                    out PegaObjectInteractionRequest request,
                    out diagnostic) ||
                !aggregator.TryGetAvailableActions(
                    request,
                    out _,
                    out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The selected Object Entry no longer offers a Pick Up action to this Actor."
                    : diagnostic;
                RejectStart(diagnostic);
                return false;
            }

            PegaObjectEntryPickUpAction objectEntryPickUpAction = null;
            PegaObjectEntryPickUpAction[] composedPickUpActions =
                occurrence.GetComponents<PegaObjectEntryPickUpAction>();
            for (int index = 0; index < composedPickUpActions.Length; index++)
            {
                PegaObjectEntryPickUpAction candidateAction = composedPickUpActions[index];
                if (candidateAction == null ||
                    !aggregator.IsOfferedTo(
                        candidateAction,
                        PegaObjectInteractionParticipant.Npc) ||
                    !candidateAction.TryGetConfiguration(
                        out PegaPickUpObjectEntryConfigurationSnapshot currentParameters,
                        out _) ||
                    !MatchesPlanParameters(currentParameters, plan))
                {
                    continue;
                }

                if (objectEntryPickUpAction != null)
                {
                    diagnostic = "The Object Entry offers more than one matching Pick Up component; the Action will not choose implicitly.";
                    RejectStart(diagnostic);
                    return false;
                }

                objectEntryPickUpAction = candidateAction;
            }

            if (objectEntryPickUpAction == null)
            {
                diagnostic = "The selected Object Entry has no authored NPC Pick Up action matching its authorized Knowledge parameters.";
                RejectStart(diagnostic);
                return false;
            }

            if (!TryStartStationaryAction(
                    plan.TargetId,
                    knownPosition,
                    plan.TargetRadiusOverrideMeters.Value,
                    out diagnostic))
            {
                return false;
            }

            _interactionActionExecutionId = CurrentExecutionId;
            _interactionLifecycleActive = true;
            _interactionStartInProgress = true;
            if (!_interactionMechanic.TryStartRequestedAction(
                    objectEntryPickUpAction,
                    request,
                    out _objectInteractionExecutionId,
                    out PegaObjectEntryPickUpStartStatus startStatus,
                    out diagnostic))
            {
                _interactionStartInProgress = false;
                _interactionLifecycleActive = false;
                _hasExecutionPlan = false;
                bool expectedPreconditionInvalidation = IsExpectedStartStatus(startStatus);
                if (startStatus == PegaObjectEntryPickUpStartStatus.OutsideInteractionRange)
                {
                    RecordRangeRejectedAttempt(plan);
                }

                if (TryGetPickUpAvailabilityEvidenceKind(startStatus, out PegaObjectEntryPickUpEvidenceKind evidenceKind))
                {
                    RecordPickUpAvailabilityResult(plan.TargetId.ObjectEntryId, evidenceKind);
                }

                if (expectedPreconditionInvalidation)
                {
                    _planningLog?.Info(
                        "NPC Pick Up start assessment rejected the selected Object Entry by a current gameplay precondition.",
                        LogFields.Field("actorRoot", ActorRoot.name),
                        LogFields.Field("objectEntryId", plan.TargetId.ObjectEntryId.StableText),
                        LogFields.Field("startStatus", startStatus),
                        LogFields.Field("diagnostic", diagnostic));
                }
                else
                {
                    _planningLog?.Error(
                        "NPC Pick Up start assessment failed its interaction or contract validation.",
                        LogFields.Field("actorRoot", ActorRoot.name),
                        LogFields.Field("objectEntryId", plan.TargetId.ObjectEntryId.StableText),
                        LogFields.Field("startStatus", startStatus),
                        LogFields.Field("diagnostic", diagnostic));
                }
                CompleteAction(
                    PegaNpcActionOutcome.Invalid,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "The shared Pick Up action could not begin for the selected Object Entry."
                        : diagnostic,
                    expectedPreconditionInvalidation);
                return false;
            }

            _interactionStartInProgress = false;
            _executionAction = objectEntryPickUpAction;
            return true;
        }

        public override bool TryGetObservedCausalEffects(
            PegaNpcPlanSnapshot plan,
            PegaNpcObservedActionEffect observedEffect,
            out IReadOnlyList<IGoapEffect> observedEffects,
            out string diagnostic)
        {
            observedEffects = Array.Empty<IGoapEffect>();
            diagnostic = string.Empty;
            PegaObjectEntryPossessedEffect declared = FindDeclaredEffect(plan);
            if (!_hasPossessionEvidence || declared == null ||
                plan.ActionId != Contract.ActionId || observedEffect.Kind != Contract.EffectKind ||
                observedEffect.RequestId != CurrentExecutionId || observedEffect.TargetId != plan.TargetId ||
                declared.ObjectEntryId != plan.TargetId.ObjectEntryId ||
                !TryGetActorId(out ActorId actorId, out diagnostic) || declared.ActorId != actorId ||
                !IsActionSpecificEffectObserved(out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The shared Pick Up execution did not confirm physical inventory possession for its declared Actor and Object Entry."
                    : diagnostic;
                return false;
            }

            observedEffects = Array.AsReadOnly<IGoapEffect>(new IGoapEffect[] { declared });
            diagnostic = string.Empty;
            return true;
        }

        protected override bool ArePreconditionsStillValid(out string diagnostic)
        {
            diagnostic = string.Empty;
            bool actionConfigurationMatches = _executionAction == null ||
                (_executionAction.TryGetConfiguration(
                     out PegaPickUpObjectEntryConfigurationSnapshot actionParameters,
                     out _) &&
                 MatchesPlanParameters(actionParameters, _executionPlan));
            if (!_hasExecutionPlan ||
                !TryReadKnownTarget(
                    _executionPlan.TargetId,
                    out Vector3 position,
                    out double evidenceTimeSeconds,
                    out diagnostic) ||
                !TryReadKnownPickUpPlanningParameters(
                    _executionPlan.TargetId,
                    out PegaPickUpObjectEntryConfigurationSnapshot knownParameters,
                    out diagnostic) ||
                !MatchesPlanParameters(knownParameters, _executionPlan) ||
                !actionConfigurationMatches ||
                !position.Equals(_executionPlan.TargetPosition) ||
                !IsEvidenceTimeCompatible(
                    _executionPlan.TargetId,
                    evidenceTimeSeconds,
                    _executionPlan.EvidenceTimeSeconds) ||
                !_executionPlan.TargetRadiusOverrideMeters.HasValue ||
                Vector3.Distance(
                    ActorTransform.position,
                    position) > _executionPlan.TargetRadiusOverrideMeters.Value)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Actor left interaction range or the authorized Knowledge offer changed during Pick Up."
                    : diagnostic;
                return false;
            }

            return true;
        }

        protected override void OnExecutionTerminated(
            PegaNpcActionOutcome outcome,
            string diagnostic)
        {
            if (outcome != PegaNpcActionOutcome.Success)
            {
                if (_executionAction != null && _objectInteractionExecutionId > 0)
                {
                    _executionAction.TryCancelExecution(
                        _objectInteractionExecutionId,
                        string.IsNullOrWhiteSpace(diagnostic)
                            ? "The NPC GOAP execution ended before Pick Up committed."
                            : $"The NPC GOAP execution ended before Pick Up committed: {diagnostic}");
                }

                _hasExecutionPlan = false;
                _hasPossessionEvidence = false;
            }

            _interactionLifecycleActive = false;
            _interactionStartInProgress = false;
            _interactionActionExecutionId = 0;
        }

        bool IPegaObjectInteractionOccurrenceAuthority.TryValidateCurrentOccurrence(
            GameObject occurrenceRoot,
            ObjectEntryId objectEntryId,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_occurrenceQuery == null || RuntimeContext == null || RuntimeContext.Autonomy == null ||
                occurrenceRoot == null || !objectEntryId.IsValid ||
                !_hasExecutionPlan || _executionPlan.TargetId.ObjectEntryId != objectEntryId)
            {
                diagnostic = "The NPC has no current GOAP plan for this known Object Entry.";
                return false;
            }

            ObjectEntryOccurrenceResolution resolution = _occurrenceQuery.Resolve(
                StableObjectReference.ForEntry(objectEntryId));
            if (!resolution.Succeeded || resolution.Occurrence != occurrenceRoot)
            {
                diagnostic = string.IsNullOrWhiteSpace(resolution.Diagnostic)
                    ? "The Activity-owned occurrence query no longer resolves the NPC's exact selected Object Entry."
                    : resolution.Diagnostic;
                return false;
            }

            bool isStarting = !_interactionLifecycleActive || _interactionStartInProgress;
            if (!RuntimeContext.Autonomy.IsActiveActionExecution(
                    this,
                    _executionPlan,
                    isStarting ? 0 : _interactionActionExecutionId,
                    isStarting) ||
                !IsCurrentPlanValid(
                    _executionPlan,
                    null,
                    out _,
                    out _,
                    out diagnostic) ||
                !ArePreconditionsStillValid(out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The NPC's active GOAP plan or authorized Pick Up preconditions are no longer current."
                    : diagnostic;
                return false;
            }

            if (!_interactionLifecycleActive)
            {
                return true;
            }

            if (Outcome != PegaNpcActionOutcome.Running ||
                CurrentExecutionId != _interactionActionExecutionId)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The NPC's active GOAP action or Pick Up preconditions changed before commit."
                    : diagnostic;
                return false;
            }

            if (_interactionStartInProgress)
            {
                return true;
            }

            if (_executionAction == null || _objectInteractionExecutionId <= 0 ||
                !_executionAction.TryGetExecution(
                    _objectInteractionExecutionId,
                    out PegaObjectInteractionExecutionStatus status,
                    out diagnostic) || status != PegaObjectInteractionExecutionStatus.Running)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The NPC's exact shared Pick Up execution is no longer running."
                    : diagnostic;
                return false;
            }

            return true;
        }

        protected override void TickNonNavigationAction()
        {
            string diagnostic = string.Empty;
            if (_executionAction == null ||
                !_executionAction.TryGetExecution(
                    _objectInteractionExecutionId,
                    out PegaObjectInteractionExecutionStatus status,
                    out diagnostic))
            {
                CompleteAction(
                    PegaNpcActionOutcome.Invalid,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "The shared Pick Up action execution is unavailable."
                        : diagnostic);
                return;
            }

            if (status == PegaObjectInteractionExecutionStatus.Running)
            {
                return;
            }

            if (status != PegaObjectInteractionExecutionStatus.Succeeded)
            {
                CompleteAction(
                    PegaNpcActionOutcome.Invalid,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "The shared Pick Up action did not commit the item transfer."
                        : diagnostic);
                return;
            }

            _hasPossessionEvidence = true;
            if (!IsActionSpecificEffectObserved(out diagnostic))
            {
                _hasPossessionEvidence = false;
                CompleteAction(
                    PegaNpcActionOutcome.Invalid,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "The Pick Up interaction reported success without a confirmed inventory transfer."
                        : diagnostic);
                return;
            }

            RecordPickUpAvailabilityResult(
                _executionPlan.TargetId.ObjectEntryId,
                PegaObjectEntryPickUpEvidenceKind.NoLongerOnGround);
            CompleteAction(
                PegaNpcActionOutcome.Success,
                "The shared Pick Up action transferred the Object Entry into the NPC's physical inventory.");
        }

        protected override bool IsActionSpecificEffectObserved(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_hasPossessionEvidence || !_hasExecutionPlan || _inventory == null ||
                !_inventory.Contains(_executionPlan.TargetId.ObjectEntryId) ||
                _executionAction == null ||
                !_executionAction.TryGetExecution(
                    _objectInteractionExecutionId,
                    out PegaObjectInteractionExecutionStatus status,
                    out diagnostic) || status != PegaObjectInteractionExecutionStatus.Succeeded)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Actor's physical inventory does not contain the selected ObjectEntryId after Pick Up."
                    : diagnostic;
                return false;
            }

            ObjectEntryOccurrenceResolution resolution = _occurrenceQuery.Resolve(
                StableObjectReference.ForEntry(_executionPlan.TargetId.ObjectEntryId));
            PegaObjectEntryPhysicalItem item = resolution.Succeeded
                ? resolution.Occurrence.GetComponent<PegaObjectEntryPhysicalItem>()
                : null;
            if (item == null || item.IsAvailableOnGround || item.ObjectEntryId != _executionPlan.TargetId.ObjectEntryId)
            {
                diagnostic = "The same Object Entry identity is not present in the committed physical inventory transfer.";
                return false;
            }

            return true;
        }

        private bool TryReadKnownPickUpPlanningParameters(
            PegaActorGroupKnowledgeReader reader,
            GroupId groupId,
            ObjectEntryId objectEntryId,
            out PegaPickUpObjectEntryConfigurationSnapshot parameters,
            out string diagnostic)
        {
            parameters = default;
            if (reader == null || !groupId.IsValid || !objectEntryId.IsValid)
            {
                diagnostic = "Pick Up planning parameters require an authorized Group reader, GroupId and ObjectEntryId.";
                return false;
            }

            PegaActorGroupKnowledgeReadResult<PegaPickUpObjectEntryConfigurationSnapshot> read =
                reader.ReadGroupFact(
                    groupId,
                    PegaObjectEntryKnowledgeFacts.PickUpPlanningParameters.For(objectEntryId),
                    Time.timeAsDouble);
            if (!read.IsAuthorized || read.Fact.Scope != PegaKnowledgeScope.Group ||
                !read.Fact.TryGetValue(out parameters) || !read.Fact.HasObservationTime ||
                !parameters.IsValid || parameters.ObjectEntryId != objectEntryId)
            {
                parameters = default;
                diagnostic = $"The Group Pick Up parameters are unavailable or invalid (access='{read.AccessStatus}', factStatus='{read.Fact.Status}').";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool TryReadKnownPickUpPlanningParameters(
            PegaNpcPlanningTargetId targetId,
            out PegaPickUpObjectEntryConfigurationSnapshot parameters,
            out string diagnostic)
        {
            parameters = default;
            if (targetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition)
            {
                if (!UsesGroupKnowledge)
                {
                    diagnostic = "The Pick Up Action is not configured to use Group Knowledge.";
                    return false;
                }

                return TryReadKnownPickUpPlanningParameters(
                    _groupKnowledgeReader,
                    targetId.KnowledgeGroupId,
                    targetId.ObjectEntryId,
                    out parameters,
                    out diagnostic);
            }

            if (targetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition)
            {
                if (!UsesIndividualKnowledge)
                {
                    diagnostic = "The Pick Up Action is not configured to use Individual Knowledge.";
                    return false;
                }

                return TryReadKnownIndividualPickUpFacts(
                    _individualKnowledgeState,
                    targetId.ObjectEntryId,
                    out _,
                    out parameters,
                    out _,
                    out diagnostic);
            }

            diagnostic = "Pick Up planning parameters require a typed Object Entry target from an enabled Knowledge authority.";
            return false;
        }

        private static bool MatchesCandidateParameters(
            PegaPickUpObjectEntryConfigurationSnapshot parameters,
            PegaNpcPlanningActionCandidate candidate) =>
            parameters.IsValid &&
            parameters.ObjectEntryId == candidate.TargetId.ObjectEntryId &&
            candidate.TargetRadiusOverrideMeters.HasValue &&
            parameters.InteractionRangeMeters == candidate.TargetRadiusOverrideMeters.Value &&
            parameters.ExecutionDurationSeconds == candidate.IntrinsicDurationSeconds;

        private static bool MatchesPlanParameters(
            PegaPickUpObjectEntryConfigurationSnapshot parameters,
            PegaNpcPlanSnapshot plan) =>
            parameters.IsValid &&
            parameters.ObjectEntryId == plan.TargetId.ObjectEntryId &&
            plan.TargetRadiusOverrideMeters.HasValue &&
            parameters.InteractionRangeMeters == plan.TargetRadiusOverrideMeters.Value &&
            parameters.ExecutionDurationSeconds == plan.IntrinsicDurationSeconds;

        private bool TryReadKnownTarget(
            PegaNpcPlanningTargetId targetId,
            out Vector3 position,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            position = default;
            evidenceTimeSeconds = 0d;
            if (!IsTargetIdentityAuthorized(targetId))
            {
                diagnostic = "The Pick Up Action requires a typed Object Entry identity from an enabled and authoritative Knowledge scope.";
                return false;
            }

            if (targetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition)
            {
                return TryReadKnownTarget(
                    _groupKnowledgeReader,
                    targetId.KnowledgeGroupId,
                    targetId.ObjectEntryId,
                    out position,
                    out evidenceTimeSeconds,
                    out diagnostic);
            }

            return TryReadKnownIndividualPickUpFacts(
                _individualKnowledgeState,
                targetId.ObjectEntryId,
                out position,
                out _,
                out evidenceTimeSeconds,
                out diagnostic);
        }

        private bool TryReadKnownTarget(
            PegaActorGroupKnowledgeReader reader,
            GroupId groupId,
            ObjectEntryId objectEntryId,
            out Vector3 position,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            position = default;
            evidenceTimeSeconds = 0d;
            PegaActorGroupKnowledgeReadResult<bool> pickUpAvailable = reader.ReadGroupFact(
                groupId,
                PegaObjectEntryKnowledgeFacts.PickUpAvailable.For(objectEntryId),
                Time.timeAsDouble);
            PegaActorGroupKnowledgeReadResult<Vector3> positionFact = reader.ReadGroupFact(
                groupId,
                PegaObjectEntryKnowledgeFacts.Position.For(objectEntryId),
                Time.timeAsDouble);
            if (!pickUpAvailable.IsAuthorized || !pickUpAvailable.Fact.TryGetValue(out bool canPickUp) || !canPickUp ||
                pickUpAvailable.Fact.Scope != PegaKnowledgeScope.Group ||
                !positionFact.IsAuthorized || positionFact.Fact.Scope != PegaKnowledgeScope.Group ||
                !positionFact.Fact.TryGetValue(out position) || !positionFact.Fact.HasObservationTime)
            {
                diagnostic = "The Actor's current membership-authorized Group Knowledge does not prove a Pick Up offer and its position.";
                return false;
            }

            evidenceTimeSeconds = positionFact.Fact.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        private bool TryReadKnownIndividualPickUpFacts(
            PegaNpcKnowledgeState knowledgeState,
            ObjectEntryId objectEntryId,
            out Vector3 position,
            out PegaPickUpObjectEntryConfigurationSnapshot parameters,
            out double evidenceTimeSeconds,
            out string diagnostic)
        {
            position = default;
            parameters = default;
            evidenceTimeSeconds = 0d;
            diagnostic = string.Empty;
            if (knowledgeState == null || knowledgeState != _individualKnowledgeState ||
                knowledgeState.Scope != PegaKnowledgeScope.Individual || !objectEntryId.IsValid ||
                !TryGetActorId(out ActorId actorId, out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "Individual Pick Up facts require this Actor's own Knowledge State and ObjectEntryId."
                    : diagnostic;
                return false;
            }

            PegaKnowledgeFact<bool> pickUpAvailable = knowledgeState.ReadFact(
                PegaObjectEntryKnowledgeFacts.PickUpAvailable.For(objectEntryId),
                Time.timeAsDouble);
            PegaKnowledgeFact<Vector3> positionFact = knowledgeState.ReadFact(
                PegaObjectEntryKnowledgeFacts.Position.For(objectEntryId),
                Time.timeAsDouble);
            PegaKnowledgeFact<PegaPickUpObjectEntryConfigurationSnapshot> parametersFact = knowledgeState.ReadFact(
                PegaObjectEntryKnowledgeFacts.PickUpPlanningParameters.For(objectEntryId),
                Time.timeAsDouble);
            if (pickUpAvailable.Scope != PegaKnowledgeScope.Individual ||
                !pickUpAvailable.TryGetValue(out bool isPickUpAvailable) || !isPickUpAvailable ||
                !pickUpAvailable.HasOrigin(PegaNpcEvidenceOrigin.Perception) ||
                !pickUpAvailable.HasObservationTime ||
                positionFact.Scope != PegaKnowledgeScope.Individual ||
                !positionFact.TryGetValue(out position) ||
                !positionFact.HasOrigin(PegaNpcEvidenceOrigin.Perception) ||
                !positionFact.HasObservationTime ||
                parametersFact.Scope != PegaKnowledgeScope.Individual ||
                !parametersFact.TryGetValue(out parameters) ||
                !parametersFact.HasOrigin(PegaNpcEvidenceOrigin.Perception) ||
                !parametersFact.HasObservationTime ||
                !parameters.IsValid || parameters.ObjectEntryId != objectEntryId ||
                Math.Abs(pickUpAvailable.ObservationTimeSeconds - positionFact.ObservationTimeSeconds) > 0.000001d ||
                Math.Abs(parametersFact.ObservationTimeSeconds - positionFact.ObservationTimeSeconds) > 0.000001d)
            {
                position = default;
                parameters = default;
                diagnostic = $"Individual Pick Up Knowledge for '{objectEntryId.StableText}' is not a coherent, current Perception offer, position and configuration (authorityActorId='{actorId.StableText}').";
                return false;
            }

            evidenceTimeSeconds = positionFact.ObservationTimeSeconds;
            diagnostic = string.Empty;
            return true;
        }

        private bool TryGetComposedAuthorities(
            PegaNpcActionPlanningContext context,
            out PegaActorGroupKnowledgeReader groupReader,
            out PegaNpcKnowledgeState individualKnowledgeState,
            out string diagnostic)
        {
            groupReader = null;
            individualKnowledgeState = null;
            IGoapKnowledgeStateView composedKnowledgeState = context.ComposedKnowledgeState;
            if (composedKnowledgeState == null || composedKnowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                diagnostic = "Pick Up candidate generation requires this NPC's explicit composed Knowledge State view.";
                return false;
            }

            if (UsesGroupKnowledge)
            {
                for (int index = 0; index < composedKnowledgeState.Capabilities.Count; index++)
                {
                    if (!(composedKnowledgeState.Capabilities[index] is PegaActorGroupKnowledgeReader candidate))
                    {
                        continue;
                    }

                    if (groupReader != null && groupReader != candidate)
                    {
                        diagnostic = "More than one Group Knowledge Reader is composed for this NPC; Pick Up planning will not choose an authority implicitly.";
                        return false;
                    }

                    groupReader = candidate;
                }

                if (groupReader == null || groupReader != _groupKnowledgeReader)
                {
                    diagnostic = "The Pick Up Action's Group Knowledge Reader is absent from this NPC's composed Knowledge State.";
                    return false;
                }
            }

            if (UsesIndividualKnowledge)
            {
                if (context.KnowledgeState == null || context.KnowledgeState != _individualKnowledgeState ||
                    _individualKnowledgeState == null || _individualKnowledgeState.Scope != PegaKnowledgeScope.Individual)
                {
                    diagnostic = "Individual Pick Up planning requires this Actor's own composed Knowledge State.";
                    return false;
                }

                individualKnowledgeState = _individualKnowledgeState;
            }

            diagnostic = string.Empty;
            return true;
        }

        public bool IsRelevantKnowledgeChange(PegaKnowledgeFactChangedEvent change)
        {
            if (change == null || change.Scope != PegaKnowledgeScope.Individual ||
                change.FactKey == null)
            {
                return false;
            }

            object definition = change.FactKey.DefinitionIdentity;
            if (ReferenceEquals(definition, PegaObjectEntryKnowledgeFacts.PickUpAvailable))
            {
                return change.Evidence.Origin.Is(PegaNpcEvidenceOrigin.Perception) ||
                       change.Evidence.Origin.Is(PegaNpcEvidenceOrigin.ActionResult);
            }

            return UsesIndividualKnowledge &&
                   change.Evidence.Origin.Is(PegaNpcEvidenceOrigin.Perception) &&
                   (ReferenceEquals(definition, PegaObjectEntryKnowledgeFacts.Position) ||
                    ReferenceEquals(definition, PegaObjectEntryKnowledgeFacts.PickUpPlanningParameters));
        }

        private bool IsKnownLocallyUnavailable(ObjectEntryId objectEntryId)
        {
            if (_individualKnowledgeState == null || !_individualKnowledgeState.IsAvailable ||
                !objectEntryId.IsValid)
            {
                return false;
            }

            PegaKnowledgeFact<bool> availability = _individualKnowledgeState.ReadFact(
                PegaObjectEntryKnowledgeFacts.PickUpAvailable.For(objectEntryId),
                Time.timeAsDouble);
            if (availability.Scope != PegaKnowledgeScope.Individual || !availability.HasObservationTime)
            {
                return false;
            }

            if (availability.TryGetValue(out bool isAvailable))
            {
                return !isAvailable &&
                       (availability.HasOrigin(PegaNpcEvidenceOrigin.Perception) ||
                        availability.HasOrigin(PegaNpcEvidenceOrigin.ActionResult) ||
                        availability.HasOrigin(PegaNpcEvidenceOrigin.DesignerAuthoredInitial));
            }

            return availability.Status == PegaKnowledgeStatus.Invalidated &&
                   HasCurrentPickUpActionResultEvidence(availability, objectEntryId);
        }

        private static bool HasCurrentPickUpActionResultEvidence(
            PegaKnowledgeFact<bool> availability,
            ObjectEntryId objectEntryId)
        {
            for (int index = 0; index < availability.Evidence.Count; index++)
            {
                PegaKnowledgeEvidence evidence = availability.Evidence[index];
                if (evidence.ObservationTimeSeconds != availability.ObservationTimeSeconds ||
                    !evidence.Origin.Is(PegaNpcEvidenceOrigin.ActionResult) ||
                    !(evidence.Payload is PegaObjectEntryPickUpAvailabilityEvidence payload) ||
                    !payload.IsValid || payload.ObjectEntryId != objectEntryId)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool IsExpectedStartStatus(PegaObjectEntryPickUpStartStatus status) =>
            status == PegaObjectEntryPickUpStartStatus.TemporarilyReserved ||
            status == PegaObjectEntryPickUpStartStatus.NoLongerOnGround ||
            status == PegaObjectEntryPickUpStartStatus.AlreadyInInventory ||
            status == PegaObjectEntryPickUpStartStatus.InventoryFull ||
            status == PegaObjectEntryPickUpStartStatus.OutsideInteractionRange ||
            status == PegaObjectEntryPickUpStartStatus.ActionAlreadyRunning ||
            status == PegaObjectEntryPickUpStartStatus.ActionInactive;

        private bool IsRangeRejectionUnchanged(
            PegaNpcPlanningTargetId targetId,
            Vector3 knownTargetPosition,
            float interactionRangeMeters)
        {
            if (_rangeRejectedAttempts == null || !_rangeRejectedAttempts.TryGetValue(
                    targetId,
                    out RangeRejectedPickUpAttempt previousAttempt))
            {
                return false;
            }

            Vector3 currentActorPosition = ActorTransform.position;
            if (previousAttempt.ActorPosition.Equals(currentActorPosition) &&
                previousAttempt.KnownTargetPosition.Equals(knownTargetPosition) &&
                previousAttempt.InteractionRangeMeters == interactionRangeMeters)
            {
                return true;
            }

            _rangeRejectedAttempts.Remove(targetId);
            return false;
        }

        private void RecordRangeRejectedAttempt(PegaNpcPlanSnapshot plan)
        {
            _rangeRejectedAttempts ??= new Dictionary<PegaNpcPlanningTargetId, RangeRejectedPickUpAttempt>();
            _rangeRejectedAttempts[plan.TargetId] = new RangeRejectedPickUpAttempt(
                ActorTransform.position,
                plan.TargetPosition,
                plan.TargetRadiusOverrideMeters.Value);
        }

        private static bool TryGetPickUpAvailabilityEvidenceKind(
            PegaObjectEntryPickUpStartStatus status,
            out PegaObjectEntryPickUpEvidenceKind evidenceKind)
        {
            switch (status)
            {
                case PegaObjectEntryPickUpStartStatus.NoLongerOnGround:
                case PegaObjectEntryPickUpStartStatus.AlreadyInInventory:
                    evidenceKind = PegaObjectEntryPickUpEvidenceKind.NoLongerOnGround;
                    return true;
                case PegaObjectEntryPickUpStartStatus.TemporarilyReserved:
                case PegaObjectEntryPickUpStartStatus.ActionAlreadyRunning:
                case PegaObjectEntryPickUpStartStatus.ActionInactive:
                    evidenceKind = PegaObjectEntryPickUpEvidenceKind.TemporarilyUnavailable;
                    return true;
                default:
                    evidenceKind = default;
                    return false;
            }
        }

        private void RecordPickUpAvailabilityResult(
            ObjectEntryId objectEntryId,
            PegaObjectEntryPickUpEvidenceKind kind)
        {
            if (_individualKnowledgeState == null ||
                RuntimeContext == null ||
                RuntimeContext.KnowledgeState != _individualKnowledgeState ||
                !_individualKnowledgeState.IsAvailable ||
                !objectEntryId.IsValid)
            {
                _planningLog?.Error(
                    "NPC Pick Up could not record its local ActionResult availability evidence.",
                    LogFields.Field("actorRoot", ActorRoot != null ? ActorRoot.name : string.Empty),
                    LogFields.Field("objectEntryId", objectEntryId.StableText),
                    LogFields.Field("evidenceKind", kind),
                    LogFields.Field("diagnostic", "The Actor's composed Individual Knowledge authority is unavailable."));
                return;
            }

            PegaObjectEntryPickUpAvailabilityEvidence payload =
                new PegaObjectEntryPickUpAvailabilityEvidence(objectEntryId, kind);
            PegaKnowledgeEvidence evidence = PegaKnowledgeEvidence.From(
                PegaNpcEvidenceOrigin.ActionResult,
                Time.timeAsDouble,
                this,
                payload);
            PegaKnowledgeWriteResult result = _individualKnowledgeState.ObserveFact(
                PegaObjectEntryKnowledgeFacts.PickUpAvailable.For(objectEntryId),
                false,
                evidence);
            if (result == PegaKnowledgeWriteResult.RejectedInvalid)
            {
                _planningLog?.Error(
                    "NPC Pick Up availability ActionResult evidence was rejected by Individual Knowledge.",
                    LogFields.Field("actorRoot", ActorRoot.name),
                    LogFields.Field("objectEntryId", objectEntryId.StableText),
                    LogFields.Field("evidenceKind", kind),
                    LogFields.Field("knowledgeWriteResult", result));
                return;
            }

            _planningLog?.Info(
                "NPC Pick Up recorded Actor-local availability evidence from the interaction result.",
                LogFields.Field("actorRoot", ActorRoot.name),
                LogFields.Field("objectEntryId", objectEntryId.StableText),
                LogFields.Field("evidenceKind", kind),
                LogFields.Field("knowledgeWriteResult", result));
        }

        private bool IsTargetIdentityAuthorized(PegaNpcPlanningTargetId targetId)
        {
            if (!targetId.ObjectEntryId.IsValid)
            {
                return false;
            }

            if (targetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition)
            {
                return UsesGroupKnowledge && targetId.KnowledgeGroupId.IsValid;
            }

            return targetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition &&
                   UsesIndividualKnowledge && targetId.KnowledgeActorId.IsValid &&
                   TryGetActorId(out ActorId actorId, out _) && targetId.KnowledgeActorId == actorId;
        }

        private bool IsEvidenceTimeCompatible(
            PegaNpcPlanningTargetId targetId,
            double currentEvidenceTimeSeconds,
            double plannedEvidenceTimeSeconds)
        {
            if (targetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition)
            {
                return currentEvidenceTimeSeconds >= plannedEvidenceTimeSeconds - 0.000001d;
            }

            return Math.Abs(currentEvidenceTimeSeconds - plannedEvidenceTimeSeconds) <= 0.000001d;
        }

        private bool UsesGroupKnowledge =>
            knowledgeSource == PegaNpcPickUpKnowledgeSource.Group;

        private bool UsesIndividualKnowledge =>
            knowledgeSource == PegaNpcPickUpKnowledgeSource.Individual;

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

        private PegaObjectEntryPossessedEffect FindDeclaredEffect(PegaNpcPlanSnapshot plan)
        {
            for (int index = 0; index < plan.CausalEffects.Count; index++)
            {
                if (plan.CausalEffects[index] is PegaObjectEntryPossessedEffect effect &&
                    effect.ObjectEntryId == plan.TargetId.ObjectEntryId)
                {
                    return effect;
                }
            }

            return null;
        }

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

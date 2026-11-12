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
using UnityEngine;
namespace PEGA.NPC.Actions.Runtime
{
    [CreateAssetMenu(
        fileName = "PickUpKnownObjectEntry",
        menuName = "PEGA/NPC/Actions/Pick Up Known Object Entry")]
    public sealed class PegaNpcPickUpKnownObjectEntryAction : PegaNpcActionProviderAsset,
        IPegaObjectInteractionOccurrenceAuthority
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.Actions";

        private PegaActorGroupKnowledgeReader _groupKnowledgeReader;
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

        protected override PegaNpcActionEffectKind ActionEffectKind =>
            PegaNpcActionEffectKind.PossessObjectEntry;

        public override PegaNpcActionContract Contract => new PegaNpcActionContract(
            PegaNpcPlanningActionId.PickUpKnownObjectEntry,
            ActionEffectKind,
            PegaNpcPlanningTargetKind.GroupObjectEntryPosition,
            false,
            "NPC began the composed Pick Up action for a known, offered Object Entry.");

        protected override bool OnRuntimeInitialize(out string diagnostic)
        {
            if (!base.OnRuntimeInitialize(out diagnostic))
            {
                return false;
            }

            _planningLog ??= PegaLogging.CreateScopedLogger<PegaNpcPickUpKnownObjectEntryAction>(
                LogCategory,
                ActorRoot.name);

            _groupKnowledgeReader = ActorRoot.GetComponent<PegaActorGroupKnowledgeReader>();
            _inventory = ActorRoot.GetComponent<PegaActorPhysicalInventory>();
            _interactionMechanic = ActorRoot.GetComponent<PegaNpcObjectInteractionMechanic>();
            _occurrenceQuery = RuntimeContext.ObjectEntryOccurrenceQueryBinding;
            if (_groupKnowledgeReader == null || _groupKnowledgeReader.gameObject != ActorRoot ||
                _inventory == null || _inventory.gameObject != ActorRoot ||
                _interactionMechanic == null || _interactionMechanic.gameObject != ActorRoot ||
                _occurrenceQuery == null)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Action requires the Actor's composed Group Knowledge reader, inventory and interaction mechanic plus an Activity Object Entry query."
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

            if (!TryGetComposedReader(
                    context.ComposedKnowledgeState,
                    out PegaActorGroupKnowledgeReader reader,
                    out diagnostic))
            {
                return false;
            }

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

            int availableOfferCount = 0;
            int knownPositionCount = 0;
            int validParameterCount = 0;
            string firstSkippedOfferDiagnostic = string.Empty;
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
                    if (!TryReadKnownTarget(
                            reader,
                            group.GroupId,
                            objectEntryId,
                            out Vector3 knownPosition,
                            out double positionEvidenceTimeSeconds,
                            out string positionDiagnostic))
                    {
                        if (string.IsNullOrWhiteSpace(firstSkippedOfferDiagnostic))
                        {
                            firstSkippedOfferDiagnostic = positionDiagnostic;
                        }

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
                        if (string.IsNullOrWhiteSpace(firstSkippedOfferDiagnostic))
                        {
                            firstSkippedOfferDiagnostic = parameterDiagnostic;
                        }

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

                    targetList.Add(target);
                    candidateList.Add(new PegaNpcPlanningActionCandidate(
                        Contract,
                        this,
                        target.TargetId,
                        PegaActorMovementSpeedRegime.Movement,
                        parameters.ExecutionDurationSeconds,
                        parameters.InteractionRangeMeters));
                }
            }

            if (candidateList.Count == 0 && string.IsNullOrWhiteSpace(firstSkippedOfferDiagnostic))
            {
                firstSkippedOfferDiagnostic = availableOfferCount == 0
                    ? "Group Knowledge supplied no currently true PickUpAvailable fact."
                    : knownPositionCount == 0
                        ? "No authorized Group position was available for the offered Object Entries."
                        : validParameterCount == 0
                            ? "No valid authorized Pick Up planning parameters were available for the offered Object Entries."
                            : "No Pick Up candidate could be formed from the current Group offers.";
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

        private void LogCandidateGeneration(
            int availableOfferCount,
            int knownPositionCount,
            int validParameterCount,
            int candidateCount,
            bool inventoryHasFreeCapacity,
            string diagnostic)
        {
            _planningLog?.Debug(
                "NPC Pick Up provider evaluated its Group offers for GOAP candidates.",
                LogFields.Field("actorRoot", ActorRoot != null ? ActorRoot.name : string.Empty),
                LogFields.Field("actionId", Contract.ActionId),
                LogFields.Field("availableOfferCount", availableOfferCount),
                LogFields.Field("knownPositionCount", knownPositionCount),
                LogFields.Field("validParameterCount", validParameterCount),
                LogFields.Field("candidateCount", candidateCount),
                LogFields.Field("inventoryHasFreeCapacity", inventoryHasFreeCapacity),
                LogFields.Field("diagnostic", diagnostic ?? string.Empty));
        }

        public override bool CanOfferCandidate(
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcPlanContinuation? continuation) =>
            !continuation.HasValue && CandidateMatchesContract(candidate) &&
            candidate.TargetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition &&
            candidate.TargetId.ObjectEntryId.IsValid && candidate.TargetId.KnowledgeGroupId.IsValid &&
            _inventory != null && _inventory.HasFreeCapacity &&
            candidate.TargetRadiusOverrideMeters.HasValue &&
            IsNonNegativeFinite(candidate.TargetRadiusOverrideMeters.Value) &&
            IsNonNegativeFinite(candidate.IntrinsicDurationSeconds);

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
                plan.TargetId.IdentityKind != PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition ||
                !TryReadKnownTarget(
                    plan.TargetId,
                    out currentKnownPosition,
                    out evidenceTimeSeconds,
                    out diagnostic) ||
                !TryReadKnownPickUpPlanningParameters(
                    _groupKnowledgeReader,
                    plan.TargetId.KnowledgeGroupId,
                    plan.TargetId.ObjectEntryId,
                    out PegaPickUpObjectEntryConfigurationSnapshot parameters,
                    out diagnostic) ||
                !MatchesPlanParameters(parameters, plan) ||
                !currentKnownPosition.Equals(plan.TargetPosition) ||
                Math.Abs(evidenceTimeSeconds - plan.EvidenceTimeSeconds) > 0.000001d ||
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
                    _groupKnowledgeReader,
                    candidate.TargetId.KnowledgeGroupId,
                    candidate.TargetId.ObjectEntryId,
                    out PegaPickUpObjectEntryConfigurationSnapshot parameters,
                    out diagnostic) ||
                !MatchesCandidateParameters(parameters, candidate) ||
                !position.Equals(target.Target.KnownPosition) ||
                Math.Abs(evidenceTimeSeconds - target.Target.EvidenceTimeSeconds) > 0.000001d ||
                !TryGetActorId(out ActorId actorId, out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The causal Pick Up candidate is not a current, membership-authorized Object Entry offer."
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
                RejectStart(string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Pick Up Target is no longer known as available."
                    : diagnostic);
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
                    out IReadOnlyList<PegaObjectInteractionAction> availableActions,
                    out diagnostic))
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The selected Object Entry no longer offers a Pick Up action to this Actor."
                    : diagnostic;
                RejectStart(diagnostic);
                return false;
            }

            PegaObjectEntryPickUpAction objectEntryPickUpAction = null;
            for (int index = 0; index < availableActions.Count; index++)
            {
                if (!(availableActions[index] is PegaObjectEntryPickUpAction candidateAction) ||
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
                diagnostic = "The selected Object Entry has no currently eligible Pick Up action matching its authorized Group Knowledge parameters.";
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
                    out diagnostic))
            {
                _interactionStartInProgress = false;
                _interactionLifecycleActive = false;
                _hasExecutionPlan = false;
                CompleteAction(
                    PegaNpcActionOutcome.Invalid,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "The shared Pick Up action could not begin for the selected Object Entry."
                        : diagnostic);
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
                    _groupKnowledgeReader,
                    _executionPlan.TargetId.KnowledgeGroupId,
                    _executionPlan.TargetId.ObjectEntryId,
                    out PegaPickUpObjectEntryConfigurationSnapshot knownParameters,
                    out diagnostic) ||
                !MatchesPlanParameters(knownParameters, _executionPlan) ||
                !actionConfigurationMatches ||
                !position.Equals(_executionPlan.TargetPosition) ||
                Math.Abs(evidenceTimeSeconds - _executionPlan.EvidenceTimeSeconds) > 0.000001d ||
                !_executionPlan.TargetRadiusOverrideMeters.HasValue ||
                Vector3.Distance(
                    ActorTransform.position,
                    position) > _executionPlan.TargetRadiusOverrideMeters.Value)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "The Actor left the known interaction range or the authorized Group offer changed during Pick Up."
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
                    ? "The NPC's active GOAP plan or membership-authorized Pick Up preconditions are no longer current."
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
            if (_groupKnowledgeReader == null ||
                targetId.IdentityKind != PegaNpcPlanningTargetIdentityKind.GroupObjectEntryPosition ||
                !targetId.ObjectEntryId.IsValid || !targetId.KnowledgeGroupId.IsValid)
            {
                diagnostic = "The Pick Up Action requires a typed ObjectEntryId and GroupId from its selected known Target.";
                return false;
            }

            return TryReadKnownTarget(
                _groupKnowledgeReader,
                targetId.KnowledgeGroupId,
                targetId.ObjectEntryId,
                out position,
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

        private bool TryGetComposedReader(
            IGoapKnowledgeStateView composedKnowledgeState,
            out PegaActorGroupKnowledgeReader reader,
            out string diagnostic)
        {
            reader = null;
            if (composedKnowledgeState == null || composedKnowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                diagnostic = "Pick Up candidate generation requires this NPC's explicit composed Knowledge State view.";
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
                    diagnostic = "More than one Group Knowledge Reader is composed for this NPC; Pick Up planning will not choose an authority implicitly.";
                    return false;
                }

                reader = candidate;
            }

            if (reader == null || reader != _groupKnowledgeReader)
            {
                diagnostic = "The Pick Up Action's Group Knowledge Reader is absent from this NPC's composed Knowledge State.";
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

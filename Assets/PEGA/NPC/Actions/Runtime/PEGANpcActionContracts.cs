using System;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using PEGA.GOAP;
using PEGA.NPC.Autonomy.Planning;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
namespace PEGA.NPC.Actions.Runtime
{
    public enum PegaNpcActionOutcome
    {
        NotStarted = 0,
        Running = 10,
        Success = 20,
        Invalid = 30,
        Interrupted = 40,
        Cancelled = 50
    }

    /// <summary>Stable, open-ended identity for an Action offered to the local GOAP planner.</summary>
    public readonly struct PegaNpcPlanningActionId : IEquatable<PegaNpcPlanningActionId>, IComparable<PegaNpcPlanningActionId>
    {
        private readonly string _stableText;

        private PegaNpcPlanningActionId(string stableText) => _stableText = stableText;

        public static PegaNpcPlanningActionId ApproachKnownCharacter { get; } = new PegaNpcPlanningActionId("approach-known-target");
        public static PegaNpcPlanningActionId InvestigateLastKnownCharacterPosition { get; } = new PegaNpcPlanningActionId("investigate-last-known-target-position");
        public static PegaNpcPlanningActionId MaintainSafeSeparationFromKnownCharacter { get; } = new PegaNpcPlanningActionId("maintain-safe-separation-from-known-character");
        public static PegaNpcPlanningActionId InvestigatePerceivedAreaPresence { get; } = new PegaNpcPlanningActionId("investigate-perceived-area-presence");
        public static PegaNpcPlanningActionId NavigateToKnownGroupObjectEntryPosition { get; } = new PegaNpcPlanningActionId("navigate-to-known-group-object-entry-position");
        public static PegaNpcPlanningActionId InvestigateKnownGroupPoi { get; } = new PegaNpcPlanningActionId("investigate-known-group-poi");
        public static PegaNpcPlanningActionId PickUpKnownObjectEntry { get; } = new PegaNpcPlanningActionId("pick-up-known-object-entry");

        public static PegaNpcPlanningActionId FromStableText(string stableText) =>
            string.IsNullOrWhiteSpace(stableText) ? default : new PegaNpcPlanningActionId(stableText.Trim());

        public string StableText => _stableText ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(_stableText);
        public bool Equals(PegaNpcPlanningActionId other) => string.Equals(_stableText, other._stableText, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PegaNpcPlanningActionId other && Equals(other);
        public override int GetHashCode() => _stableText == null ? 0 : StringComparer.Ordinal.GetHashCode(_stableText);
        public int CompareTo(PegaNpcPlanningActionId other) => string.CompareOrdinal(_stableText, other._stableText);
        public override string ToString() => IsValid ? _stableText : "InvalidActionId";
        public static bool operator ==(PegaNpcPlanningActionId left, PegaNpcPlanningActionId right) => left.Equals(right);
        public static bool operator !=(PegaNpcPlanningActionId left, PegaNpcPlanningActionId right) => !left.Equals(right);
    }

    /// <summary>Stable, open-ended identity for a predicted and observed Action effect.</summary>
    public readonly struct PegaNpcActionEffectKind : IEquatable<PegaNpcActionEffectKind>
    {
        private readonly string _stableText;

        private PegaNpcActionEffectKind(string stableText) => _stableText = stableText;

        public static PegaNpcActionEffectKind ApproachKnownCharacterPosition { get; } = new PegaNpcActionEffectKind("approach-known-target-position");
        public static PegaNpcActionEffectKind ReachLocalObservationPosition { get; } = new PegaNpcActionEffectKind("reach-local-observation-position");
        public static PegaNpcActionEffectKind ReachSafeSeparationPosition { get; } = new PegaNpcActionEffectKind("reach-safe-separation-position");
        public static PegaNpcActionEffectKind ReachKnownGroupObjectEntryPosition { get; } = new PegaNpcActionEffectKind("reach-known-group-object-entry-position");
        public static PegaNpcActionEffectKind CompleteGroupPoiInvestigation { get; } = new PegaNpcActionEffectKind("complete-group-poi-investigation");
        public static PegaNpcActionEffectKind PossessObjectEntry { get; } = new PegaNpcActionEffectKind("possess-object-entry");

        public static PegaNpcActionEffectKind FromStableText(string stableText) =>
            string.IsNullOrWhiteSpace(stableText) ? default : new PegaNpcActionEffectKind(stableText.Trim());

        public string StableText => _stableText ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(_stableText);
        public bool Equals(PegaNpcActionEffectKind other) => string.Equals(_stableText, other._stableText, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PegaNpcActionEffectKind other && Equals(other);
        public override int GetHashCode() => _stableText == null ? 0 : StringComparer.Ordinal.GetHashCode(_stableText);
        public override string ToString() => IsValid ? _stableText : "InvalidEffectKind";
        public static bool operator ==(PegaNpcActionEffectKind left, PegaNpcActionEffectKind right) => left.Equals(right);
        public static bool operator !=(PegaNpcActionEffectKind left, PegaNpcActionEffectKind right) => !left.Equals(right);
    }

    public readonly struct PegaNpcActionEffectDeclaration
    {
        internal PegaNpcActionEffectDeclaration(
            PegaNpcActionEffectKind kind,
            PegaNpcPlanningTargetId targetId,
            PegaNpcPlanningTargetKind targetKind,
            bool isTargetResolved,
            Vector3 targetPosition,
            float targetRadiusMeters)
        {
            Kind = kind;
            TargetId = targetId;
            TargetKind = targetKind;
            IsTargetResolved = isTargetResolved;
            TargetPosition = targetPosition;
            TargetRadiusMeters = targetRadiusMeters;
        }

        public PegaNpcActionEffectKind Kind { get; }
        public PegaNpcPlanningTargetId TargetId { get; }
        public PegaNpcPlanningTargetKind TargetKind { get; }
        public bool IsTargetResolved { get; }
        public Vector3 TargetPosition { get; }
        public float TargetRadiusMeters { get; }
    }

    public readonly struct PegaNpcObservedActionEffect
    {
        internal PegaNpcObservedActionEffect(
            PegaNpcActionEffectKind kind,
            PegaNpcPlanningTargetId targetId,
            PegaNpcPlanningTargetKind targetKind,
            long requestId,
            Vector3 targetPosition,
            float targetRadiusMeters,
            float actualDistanceToTargetMeters,
            double observedAtSeconds)
        {
            Kind = kind;
            TargetId = targetId;
            TargetKind = targetKind;
            RequestId = requestId;
            TargetPosition = targetPosition;
            TargetRadiusMeters = targetRadiusMeters;
            ActualDistanceToTargetMeters = actualDistanceToTargetMeters;
            ObservedAtSeconds = observedAtSeconds;
            IsValid = true;
        }

        public PegaNpcActionEffectKind Kind { get; }
        public PegaNpcPlanningTargetId TargetId { get; }
        public PegaNpcPlanningTargetKind TargetKind { get; }
        public long RequestId { get; }
        public Vector3 TargetPosition { get; }
        public float TargetRadiusMeters { get; }
        public float ActualDistanceToTargetMeters { get; }
        public double ObservedAtSeconds { get; }
        public bool IsValid { get; }
    }

    /// <summary>Navigation-independent terminal data shared by every Action provider.</summary>
    public readonly struct PegaNpcActionExecutionSnapshot
    {
        internal PegaNpcActionExecutionSnapshot(
            PegaNpcActionOutcome outcome,
            long requestId,
            PegaNpcActionEffectDeclaration declaredEffect,
            bool effectObserved,
            string diagnostic)
        {
            Outcome = outcome;
            RequestId = requestId;
            DeclaredEffect = declaredEffect;
            EffectObserved = effectObserved;
            TargetPosition = declaredEffect.IsTargetResolved ? declaredEffect.TargetPosition : Vector3.zero;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public PegaNpcActionOutcome Outcome { get; }
        public long RequestId { get; }
        public PegaNpcActionEffectDeclaration DeclaredEffect { get; }
        public bool EffectObserved { get; }
        public Vector3 TargetPosition { get; }
        public string Diagnostic { get; }
    }

    /// <summary>Reports the terminal state of any Action provider, whether or not it uses navigation.</summary>
    public sealed class PegaNpcActionTerminalOutcomeEvent : IEvent
    {
        internal PegaNpcActionTerminalOutcomeEvent(
            PegaNpcActionExecutionSnapshot execution,
            double completedAtTimeSeconds)
        {
            Execution = execution;
            CompletedAtTimeSeconds = completedAtTimeSeconds;
        }

        public PegaNpcActionExecutionSnapshot Execution { get; }
        public double CompletedAtTimeSeconds { get; }
    }

    /// <summary>Stable, open-ended category describing the kind of planning Target consumed by an Action.</summary>
    public readonly struct PegaNpcPlanningTargetKind : IEquatable<PegaNpcPlanningTargetKind>
    {
        private readonly string _stableText;

        private PegaNpcPlanningTargetKind(string stableText) => _stableText = stableText;

        public static PegaNpcPlanningTargetKind CharacterPosition { get; } = new PegaNpcPlanningTargetKind("character-position");
        public static PegaNpcPlanningTargetKind LocalObservationPosition { get; } = new PegaNpcPlanningTargetKind("local-observation-position");
        public static PegaNpcPlanningTargetKind SafeSeparationPosition { get; } = new PegaNpcPlanningTargetKind("safe-separation-position");
        public static PegaNpcPlanningTargetKind GroupObjectEntryPosition { get; } = new PegaNpcPlanningTargetKind("group-object-entry-position");

        public static PegaNpcPlanningTargetKind FromStableText(string stableText) =>
            string.IsNullOrWhiteSpace(stableText) ? default : new PegaNpcPlanningTargetKind(stableText.Trim());

        public string StableText => _stableText ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(_stableText);
        public bool Equals(PegaNpcPlanningTargetKind other) => string.Equals(_stableText, other._stableText, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PegaNpcPlanningTargetKind other && Equals(other);
        public override int GetHashCode() => _stableText == null ? 0 : StringComparer.Ordinal.GetHashCode(_stableText);
        public override string ToString() => IsValid ? _stableText : "InvalidTargetKind";
        public static bool operator ==(PegaNpcPlanningTargetKind left, PegaNpcPlanningTargetKind right) => left.Equals(right);
        public static bool operator !=(PegaNpcPlanningTargetKind left, PegaNpcPlanningTargetKind right) => !left.Equals(right);
    }

    public readonly struct PegaNpcActionContract
    {
        public PegaNpcActionContract(
            PegaNpcPlanningActionId actionId,
            PegaNpcActionEffectKind effectKind,
            PegaNpcPlanningTargetKind targetKind,
            bool requiresNavigation,
            string startMessage)
        {
            ActionId = actionId;
            EffectKind = effectKind;
            TargetKind = targetKind;
            RequiresNavigation = requiresNavigation;
            StartMessage = startMessage ?? string.Empty;
        }

        public PegaNpcPlanningActionId ActionId { get; }
        public PegaNpcActionEffectKind EffectKind { get; }
        public PegaNpcPlanningTargetKind TargetKind { get; }
        public bool RequiresNavigation { get; }
        public string StartMessage { get; }

        public bool IsValid => ActionId.IsValid && EffectKind.IsValid && TargetKind.IsValid &&
            !string.IsNullOrWhiteSpace(StartMessage);
    }

    /// <summary>Inputs from this NPC's Knowledge State and current planning opportunity.</summary>
    public readonly struct PegaNpcActionPlanningContext
    {
        public PegaNpcActionPlanningContext(
            PegaNpcKnowledgeState knowledgeState,
            PegaNpcKnowledgeReadView knowledgeReadView,
            IGoapKnowledgeStateView composedKnowledgeState,
            ActorId targetActorId,
            bool hasPerceptionBackedTargetContact,
            PegaNpcPlanContinuation? planContinuation,
            IReadOnlyCollection<PegaNpcPlanningTargetId> attemptedInvestigationTargets)
        {
            KnowledgeState = knowledgeState;
            KnowledgeReadView = knowledgeReadView ??
                throw new ArgumentNullException(nameof(knowledgeReadView));
            ComposedKnowledgeState = composedKnowledgeState ??
                throw new ArgumentNullException(nameof(composedKnowledgeState));
            TargetActorId = targetActorId;
            HasPerceptionBackedTargetContact = hasPerceptionBackedTargetContact;
            PlanContinuation = planContinuation;
            AttemptedInvestigationTargets = attemptedInvestigationTargets ??
                Array.Empty<PegaNpcPlanningTargetId>();
        }

        public PegaNpcKnowledgeState KnowledgeState { get; }
        public PegaNpcKnowledgeReadView KnowledgeReadView { get; }
        public IGoapKnowledgeStateView ComposedKnowledgeState { get; }
        public ActorId TargetActorId { get; }
        public bool HasPerceptionBackedTargetContact { get; }
        public PegaNpcPlanContinuation? PlanContinuation { get; }
        public IReadOnlyCollection<PegaNpcPlanningTargetId> AttemptedInvestigationTargets { get; }
    }

    /// <summary>
    /// An Actor-local runtime capability created from a shared Action definition. Providers offer candidates and own
    /// Action-specific validation and execution; Autonomy ranks plans, dispatches them, and applies observed Effects.
    /// </summary>
    public interface IPegaNpcActionProvider : IPegaNpcCausalActionDefinitionProvider
    {
        PegaNpcActionContract Contract { get; }
        PegaNpcActionOutcome Outcome { get; }
        PegaNpcActionExecutionSnapshot CurrentExecution { get; }
        PegaNpcActionEffectDeclaration DeclaredEffect { get; }

        IEventBinding SubscribeToTerminalOutcomes(Action<PegaNpcActionTerminalOutcomeEvent> handler);
        bool UnsubscribeFromTerminalOutcomes(IEventBinding binding);
        bool TryCreatePlanningCandidates(
            PegaNpcActionPlanningContext context,
            out IReadOnlyList<PegaNpcPlanningTarget> targets,
            out IReadOnlyList<PegaNpcPlanningActionCandidate> candidates,
            out string diagnostic);
        bool CanOfferCandidate(PegaNpcPlanningActionCandidate candidate, PegaNpcPlanContinuation? continuation);
        bool IsCurrentPlanValid(
            PegaNpcPlanSnapshot plan,
            PegaNpcPlanContinuation? continuation,
            out Vector3 currentKnownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic);
        bool TryStart(PegaNpcPlanSnapshot plan, PegaNpcPlanContinuation? continuation, out string diagnostic);
        bool TryGetObservedEffect(out PegaNpcObservedActionEffect observedEffect);
        bool IsEffectAlreadySatisfiedAtPosition(PegaNpcPlanSnapshot plan, Vector3 actorPosition);
        bool IsTargetPositionWithinTolerance(Vector3 left, Vector3 right);
        bool CancelExecution(string diagnostic);
        bool InterruptExecution(string diagnostic);
    }

    /// <summary>
    /// Supplies causal success Preconditions and Effects for an Action/known-Target application,
    /// independently of any Desire or Goal condition.
    /// </summary>
    public interface IPegaNpcCausalActionDefinitionProvider
    {
        bool TryCreateCausalActionDefinition(
            PegaNpcPlanningActionCandidate candidate,
            PegaNpcGoapKnownTarget target,
            out GoapActionDefinition actionDefinition,
            out string diagnostic);

        bool TryGetObservedCausalEffects(
            PegaNpcPlanSnapshot plan,
            PegaNpcObservedActionEffect observedEffect,
            out IReadOnlyList<IGoapEffect> observedEffects,
            out string diagnostic);
    }

    public enum PegaNpcCausalStepEvaluationStatus
    {
        Available = 0,
        Unavailable = 10,
        Unknown = 20
    }

    /// <summary>
    /// Evaluates one Action/Target application against the hypothetical state immediately before that step.
    /// Unknown is explicit and cannot be used as an available Action or as a cost estimate.
    /// </summary>
    public interface IPegaNpcCausalActionStepEvaluator
    {
        PegaNpcCausalStepEvaluationStatus EvaluateCausalStep(
            PegaNpcPlanningActionCandidate candidate,
            IGoapHypotheticalStateView stateBeforeStep,
            PegaNpcCausalPlanningKnowledge knowledge,
            out PegaNpcNavigationRouteEstimate routeEstimate,
            out float estimatedSeconds,
            out string diagnostic);
    }

    /// <summary>Declara quais mudanças do Knowledge State podem alterar os candidatos deste provider.</summary>
    /// <remarks>Providers sem esta capability não declaram dependência de eventos do Knowledge State individual.</remarks>
    public interface IPegaNpcKnowledgeChangeRelevanceProvider
    {
        bool IsRelevantKnowledgeChange(PegaKnowledgeFactChangedEvent change);
    }

    /// <summary>Marker published when a composed local capability changes its available planning inputs.</summary>
    public sealed class PegaNpcPlanningInputsChangedEvent : IEvent
    {
        internal PegaNpcPlanningInputsChangedEvent()
        {
        }
    }

    /// <summary>Optional event source for planning facts that are not owned by individual Knowledge State.</summary>
    public interface IPegaNpcPlanningInputChangeSource
    {
        IEventBinding SubscribeToPlanningInputChanges(Action<PegaNpcPlanningInputsChangedEvent> handler);

        bool UnsubscribeFromPlanningInputChanges(IEventBinding binding);
    }

    /// <summary>Capability opcional que mantém Targets tentados de uma oportunidade finita desta Action.</summary>
    public interface IPegaNpcAttemptedTargetProvider
    {
        bool ShouldRecordAttemptedTarget(PegaNpcPlanningActionCandidate candidate);
    }

    /// <summary>Capability opcional para atualizar o destino de uma execução sem substituí-la.</summary>
    public interface IPegaNpcExecutionTargetUpdater
    {
        /// <remarks>Uma atualização bem-sucedida preserva a identidade e a correlação da execução atual.</remarks>
        bool TryUpdateExecutionTarget(
            PegaNpcPlanSnapshot currentPlan,
            PegaNpcPlanSnapshot updatedPlan,
            out string diagnostic);
    }

    /// <summary>Validates a confirmed Effect before it is reused as a causal checkpoint on a later GOAP evaluation.</summary>
    public interface IPegaNpcObservedEffectCheckpointValidator
    {
        bool CanPreserveObservedEffect(IGoapEffect effect);

        bool IsObservedEffectCheckpointValid(
            IGoapEffect effect,
            IGoapKnowledgeStateView knowledgeState,
            Vector3 actorPosition,
            out string diagnostic);
    }

    /// <summary>Optional target-observation capability composed on an NPC root.</summary>
    public interface IPegaNpcTargetObservationSource
    {
        bool TryGetCurrentTargetActorId(out ActorId actorId, out string diagnostic);

        IEventBinding SubscribeToPlanReevaluationRequests(
            Action<PegaNpcPlanReevaluationRequestedEvent> handler);

        bool UnsubscribeFromPlanReevaluationRequests(IEventBinding binding);

    }

    /// <summary>Optional capability for producing a causal continuation from known target evidence.</summary>
    public interface IPegaNpcLastKnownTargetContinuationProvider
    {
        IEventBinding SubscribeToLastKnownTargetContinuationIntents(
            Action<PegaNpcLastKnownTargetContinuationIntentRequestedEvent> handler);

        bool UnsubscribeFromLastKnownTargetContinuationIntents(IEventBinding binding);

        bool IsCurrentContinuationIntent(
            PegaNpcLastKnownTargetContinuationIntent intent,
            out string diagnostic);

        bool IsContinuationSupersededByReacquisition(
            PegaNpcLastKnownTargetContinuationIntent intent);

        bool TryGetKnownTargetPosition(out Vector3 position, out string diagnostic);
    }

}

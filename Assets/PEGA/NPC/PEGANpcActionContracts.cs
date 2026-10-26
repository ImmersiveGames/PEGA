using System;
using System.Collections.Generic;
using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using Immersive.Framework.PlayerParticipation;
using PEGA.GOAP;
using UnityEngine;

namespace PEGA.NPC
{
    public enum PEGANpcActionOutcome
    {
        NotStarted = 0,
        Running = 10,
        Success = 20,
        Invalid = 30,
        Interrupted = 40,
        Cancelled = 50
    }

    /// <summary>Stable, open-ended identity for an Action offered to the local GOAP planner.</summary>
    public readonly struct PEGANpcPlanningActionId : IEquatable<PEGANpcPlanningActionId>, IComparable<PEGANpcPlanningActionId>
    {
        private readonly string _stableText;

        private PEGANpcPlanningActionId(string stableText) => _stableText = stableText;

        public static PEGANpcPlanningActionId ApproachKnownCharacter { get; } = new PEGANpcPlanningActionId("approach-known-target");
        public static PEGANpcPlanningActionId TravelToEscapeTarget { get; } = new PEGANpcPlanningActionId("travel-to-escape-target");
        public static PEGANpcPlanningActionId InvestigateLastKnownCharacterPosition { get; } = new PEGANpcPlanningActionId("investigate-last-known-target-position");
        public static PEGANpcPlanningActionId MaintainSafeSeparationFromKnownCharacter { get; } = new PEGANpcPlanningActionId("maintain-safe-separation-from-known-character");
        public static PEGANpcPlanningActionId InvestigatePerceivedAreaPresence { get; } = new PEGANpcPlanningActionId("investigate-perceived-area-presence");

        public static PEGANpcPlanningActionId FromStableText(string stableText) =>
            string.IsNullOrWhiteSpace(stableText) ? default : new PEGANpcPlanningActionId(stableText.Trim());

        public string StableText => _stableText ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(_stableText);
        public bool Equals(PEGANpcPlanningActionId other) => string.Equals(_stableText, other._stableText, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PEGANpcPlanningActionId other && Equals(other);
        public override int GetHashCode() => _stableText == null ? 0 : StringComparer.Ordinal.GetHashCode(_stableText);
        public int CompareTo(PEGANpcPlanningActionId other) => string.CompareOrdinal(_stableText, other._stableText);
        public override string ToString() => IsValid ? _stableText : "InvalidActionId";
        public static bool operator ==(PEGANpcPlanningActionId left, PEGANpcPlanningActionId right) => left.Equals(right);
        public static bool operator !=(PEGANpcPlanningActionId left, PEGANpcPlanningActionId right) => !left.Equals(right);
    }

    /// <summary>Stable, open-ended identity for a predicted and observed Action effect.</summary>
    public readonly struct PEGANpcActionEffectKind : IEquatable<PEGANpcActionEffectKind>
    {
        private readonly string _stableText;

        private PEGANpcActionEffectKind(string stableText) => _stableText = stableText;

        public static PEGANpcActionEffectKind ApproachKnownCharacterPosition { get; } = new PEGANpcActionEffectKind("approach-known-target-position");
        public static PEGANpcActionEffectKind ReachSelectedEscapeDestination { get; } = new PEGANpcActionEffectKind("reach-selected-escape-destination");
        public static PEGANpcActionEffectKind ReachLocalObservationPosition { get; } = new PEGANpcActionEffectKind("reach-local-observation-position");
        public static PEGANpcActionEffectKind ReachSafeSeparationPosition { get; } = new PEGANpcActionEffectKind("reach-safe-separation-position");

        public static PEGANpcActionEffectKind FromStableText(string stableText) =>
            string.IsNullOrWhiteSpace(stableText) ? default : new PEGANpcActionEffectKind(stableText.Trim());

        public string StableText => _stableText ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(_stableText);
        public bool Equals(PEGANpcActionEffectKind other) => string.Equals(_stableText, other._stableText, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PEGANpcActionEffectKind other && Equals(other);
        public override int GetHashCode() => _stableText == null ? 0 : StringComparer.Ordinal.GetHashCode(_stableText);
        public override string ToString() => IsValid ? _stableText : "InvalidEffectKind";
        public static bool operator ==(PEGANpcActionEffectKind left, PEGANpcActionEffectKind right) => left.Equals(right);
        public static bool operator !=(PEGANpcActionEffectKind left, PEGANpcActionEffectKind right) => !left.Equals(right);
    }

    public readonly struct PEGANpcActionEffectDeclaration
    {
        internal PEGANpcActionEffectDeclaration(
            PEGANpcActionEffectKind kind,
            PEGANpcPlanningTargetId targetId,
            PEGANpcPlanningTargetKind targetKind,
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

        public PEGANpcActionEffectKind Kind { get; }
        public PEGANpcPlanningTargetId TargetId { get; }
        public PEGANpcPlanningTargetKind TargetKind { get; }
        public bool IsTargetResolved { get; }
        public Vector3 TargetPosition { get; }
        public float TargetRadiusMeters { get; }
    }

    public readonly struct PEGANpcObservedActionEffect
    {
        internal PEGANpcObservedActionEffect(
            PEGANpcActionEffectKind kind,
            PEGANpcPlanningTargetId targetId,
            PEGANpcPlanningTargetKind targetKind,
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

        public PEGANpcActionEffectKind Kind { get; }
        public PEGANpcPlanningTargetId TargetId { get; }
        public PEGANpcPlanningTargetKind TargetKind { get; }
        public long RequestId { get; }
        public Vector3 TargetPosition { get; }
        public float TargetRadiusMeters { get; }
        public float ActualDistanceToTargetMeters { get; }
        public double ObservedAtSeconds { get; }
        public bool IsValid { get; }
    }

    /// <summary>Navigation-independent terminal data shared by every Action provider.</summary>
    public readonly struct PEGANpcActionExecutionSnapshot
    {
        internal PEGANpcActionExecutionSnapshot(
            PEGANpcActionOutcome outcome,
            long requestId,
            PEGANpcActionEffectDeclaration declaredEffect,
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

        public PEGANpcActionOutcome Outcome { get; }
        public long RequestId { get; }
        public PEGANpcActionEffectDeclaration DeclaredEffect { get; }
        public bool EffectObserved { get; }
        public Vector3 TargetPosition { get; }
        public string Diagnostic { get; }
    }

    /// <summary>Reports the terminal state of any Action provider, whether or not it uses navigation.</summary>
    public sealed class PEGANpcActionTerminalOutcomeEvent : IEvent
    {
        internal PEGANpcActionTerminalOutcomeEvent(
            PEGANpcActionExecutionSnapshot execution,
            double completedAtTimeSeconds)
        {
            Execution = execution;
            CompletedAtTimeSeconds = completedAtTimeSeconds;
        }

        public PEGANpcActionExecutionSnapshot Execution { get; }
        public double CompletedAtTimeSeconds { get; }
    }

    /// <summary>Stable, open-ended category describing the kind of planning Target consumed by an Action.</summary>
    public readonly struct PEGANpcPlanningTargetKind : IEquatable<PEGANpcPlanningTargetKind>
    {
        private readonly string _stableText;

        private PEGANpcPlanningTargetKind(string stableText) => _stableText = stableText;

        public static PEGANpcPlanningTargetKind CharacterPosition { get; } = new PEGANpcPlanningTargetKind("character-position");
        public static PEGANpcPlanningTargetKind EscapeDestination { get; } = new PEGANpcPlanningTargetKind("escape-destination");
        public static PEGANpcPlanningTargetKind LocalObservationPosition { get; } = new PEGANpcPlanningTargetKind("local-observation-position");
        public static PEGANpcPlanningTargetKind SafeSeparationPosition { get; } = new PEGANpcPlanningTargetKind("safe-separation-position");

        public static PEGANpcPlanningTargetKind FromStableText(string stableText) =>
            string.IsNullOrWhiteSpace(stableText) ? default : new PEGANpcPlanningTargetKind(stableText.Trim());

        public string StableText => _stableText ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(_stableText);
        public bool Equals(PEGANpcPlanningTargetKind other) => string.Equals(_stableText, other._stableText, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PEGANpcPlanningTargetKind other && Equals(other);
        public override int GetHashCode() => _stableText == null ? 0 : StringComparer.Ordinal.GetHashCode(_stableText);
        public override string ToString() => IsValid ? _stableText : "InvalidTargetKind";
        public static bool operator ==(PEGANpcPlanningTargetKind left, PEGANpcPlanningTargetKind right) => left.Equals(right);
        public static bool operator !=(PEGANpcPlanningTargetKind left, PEGANpcPlanningTargetKind right) => !left.Equals(right);
    }

    public readonly struct PEGANpcActionContract
    {
        public PEGANpcActionContract(
            PEGANpcPlanningActionId actionId,
            PEGANpcActionEffectKind effectKind,
            PEGANpcPlanningTargetKind targetKind,
            bool requiresNavigation,
            string startMessage)
        {
            ActionId = actionId;
            EffectKind = effectKind;
            TargetKind = targetKind;
            RequiresNavigation = requiresNavigation;
            StartMessage = startMessage ?? string.Empty;
        }

        public PEGANpcPlanningActionId ActionId { get; }
        public PEGANpcActionEffectKind EffectKind { get; }
        public PEGANpcPlanningTargetKind TargetKind { get; }
        public bool RequiresNavigation { get; }
        public string StartMessage { get; }

        public bool IsValid => ActionId.IsValid && EffectKind.IsValid && TargetKind.IsValid &&
            !string.IsNullOrWhiteSpace(StartMessage);
    }

    /// <summary>Inputs from this NPC's Knowledge State and current planning opportunity.</summary>
    public readonly struct PEGANpcActionPlanningContext
    {
        public PEGANpcActionPlanningContext(
            PEGANpcKnowledgeState knowledgeState,
            ActorId targetActorId,
            bool hasPerceptionBackedTargetContact,
            PEGANpcPlanContinuation? planContinuation,
            IReadOnlyCollection<PEGANpcPlanningTargetId> attemptedInvestigationTargets)
        {
            KnowledgeState = knowledgeState;
            TargetActorId = targetActorId;
            HasPerceptionBackedTargetContact = hasPerceptionBackedTargetContact;
            PlanContinuation = planContinuation;
            AttemptedInvestigationTargets = attemptedInvestigationTargets ??
                Array.Empty<PEGANpcPlanningTargetId>();
        }

        public PEGANpcKnowledgeState KnowledgeState { get; }
        public ActorId TargetActorId { get; }
        public bool HasPerceptionBackedTargetContact { get; }
        public PEGANpcPlanContinuation? PlanContinuation { get; }
        public IReadOnlyCollection<PEGANpcPlanningTargetId> AttemptedInvestigationTargets { get; }
    }

    /// <summary>
    /// A capability attached to one Actor root. Providers offer candidates and own Action-specific validation and execution;
    /// the autonomy component only ranks plans, dispatches them, and applies observed Effects to Desire State.
    /// </summary>
    public interface IPEGANpcActionProvider : IPEGANpcCausalActionDefinitionProvider
    {
        PEGANpcActionContract Contract { get; }
        PEGANpcActionOutcome Outcome { get; }
        PEGANpcActionExecutionSnapshot CurrentExecution { get; }
        PEGANpcActionEffectDeclaration DeclaredEffect { get; }

        IEventBinding SubscribeToTerminalOutcomes(Action<PEGANpcActionTerminalOutcomeEvent> handler);
        bool UnsubscribeFromTerminalOutcomes(IEventBinding binding);
        bool TryCreatePlanningCandidates(
            PEGANpcActionPlanningContext context,
            out IReadOnlyList<PEGANpcPlanningTarget> targets,
            out IReadOnlyList<PEGANpcPlanningActionCandidate> candidates,
            out string diagnostic);
        bool CanOfferCandidate(PEGANpcPlanningActionCandidate candidate, PEGANpcPlanContinuation? continuation);
        bool IsCurrentPlanValid(
            PEGANpcPlanSnapshot plan,
            PEGANpcPlanContinuation? continuation,
            out Vector3 currentKnownPosition,
            out double evidenceTimeSeconds,
            out string diagnostic);
        bool TryStart(PEGANpcPlanSnapshot plan, PEGANpcPlanContinuation? continuation, out string diagnostic);
        bool TryGetObservedEffect(out PEGANpcObservedActionEffect observedEffect);
        bool IsEffectAlreadySatisfiedAtPosition(PEGANpcPlanSnapshot plan, Vector3 actorPosition);
        bool IsTargetPositionWithinTolerance(Vector3 left, Vector3 right);
        bool CancelExecution(string diagnostic);
        bool InterruptExecution(string diagnostic);
    }

    /// <summary>
    /// Supplies causal success Preconditions and Effects for an Action/known-Target application,
    /// independently of any Desire or Goal condition.
    /// </summary>
    public interface IPEGANpcCausalActionDefinitionProvider
    {
        bool TryCreateCausalActionDefinition(
            PEGANpcPlanningActionCandidate candidate,
            PEGANpcGoapKnownTarget target,
            out PEGAGoapActionDefinition actionDefinition,
            out string diagnostic);

        bool TryGetObservedCausalEffects(
            PEGANpcPlanSnapshot plan,
            PEGANpcObservedActionEffect observedEffect,
            out IReadOnlyList<IPEGAGoapEffect> observedEffects,
            out string diagnostic);
    }

    /// <summary>Declara quais mudanças do Knowledge State podem alterar os candidatos deste provider.</summary>
    /// <remarks>Providers sem esta capability não declaram dependência de eventos do Knowledge State individual.</remarks>
    public interface IPEGANpcKnowledgeChangeRelevanceProvider
    {
        bool IsRelevantKnowledgeChange(PEGANpcKnowledgeFactChangedEvent change);
    }

    /// <summary>Marker published when a composed local capability changes its available planning inputs.</summary>
    public sealed class PEGANpcPlanningInputsChangedEvent : IEvent
    {
        internal PEGANpcPlanningInputsChangedEvent()
        {
        }
    }

    /// <summary>Optional event source for planning facts that are not owned by individual Knowledge State.</summary>
    public interface IPEGANpcPlanningInputChangeSource
    {
        IEventBinding SubscribeToPlanningInputChanges(Action<PEGANpcPlanningInputsChangedEvent> handler);

        bool UnsubscribeFromPlanningInputChanges(IEventBinding binding);
    }

    /// <summary>Capability opcional que mantém Targets tentados de uma oportunidade finita desta Action.</summary>
    public interface IPEGANpcAttemptedTargetProvider
    {
        bool ShouldRecordAttemptedTarget(PEGANpcPlanningActionCandidate candidate);
    }

    /// <summary>Capability opcional para atualizar o destino de uma execução sem substituí-la.</summary>
    public interface IPEGANpcExecutionTargetUpdater
    {
        /// <remarks>Uma atualização bem-sucedida preserva a identidade e a correlação da execução atual.</remarks>
        bool TryUpdateExecutionTarget(
            PEGANpcPlanSnapshot currentPlan,
            PEGANpcPlanSnapshot updatedPlan,
            out string diagnostic);
    }

    /// <summary>Optional target-observation capability composed on an NPC root.</summary>
    public interface IPEGANpcTargetObservationSource
    {
        bool TryGetCurrentTargetActorId(out ActorId actorId, out string diagnostic);

        IEventBinding SubscribeToPlanReevaluationRequests(
            Action<PEGANpcPlanReevaluationRequestedEvent> handler);

        bool UnsubscribeFromPlanReevaluationRequests(IEventBinding binding);

        bool TryBindActivityObserverBeforeActivation(
            PlayerSessionObserver observer,
            out string diagnostic);
    }

    /// <summary>Optional capability for producing a causal continuation from known target evidence.</summary>
    public interface IPEGANpcLastKnownTargetContinuationProvider
    {
        IEventBinding SubscribeToLastKnownTargetContinuationIntents(
            Action<PEGANpcLastKnownTargetContinuationIntentRequestedEvent> handler);

        bool UnsubscribeFromLastKnownTargetContinuationIntents(IEventBinding binding);

        bool IsCurrentContinuationIntent(
            PEGANpcLastKnownTargetContinuationIntent intent,
            out string diagnostic);

        bool IsContinuationSupersededByNewRecognition(
            PEGANpcLastKnownTargetContinuationIntent intent);

        bool TryGetKnownTargetPosition(out Vector3 position, out string diagnostic);
    }

}

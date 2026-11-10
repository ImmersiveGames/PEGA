using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using PEGA.GOAP;
using PEGA.NPC.Actions.Runtime;
using PEGA.NPC.Knowledge.Core;
using PEGA.NPC.Knowledge.Definitions;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
namespace PEGA.NPC.Autonomy.Planning
{
    /// <summary>
    /// Read-only Individual Knowledge view supplied to PEGA GOAP consumers.
    /// It exposes stored facts and does not resolve Actors or physical manifestations.
    /// </summary>
    public sealed class PegaNpcKnowledgeReadView
    {
        private readonly PegaNpcKnowledgeState _individualKnowledge;

        internal PegaNpcKnowledgeReadView(PegaNpcKnowledgeState individualKnowledge)
        {
            _individualKnowledge = individualKnowledge ??
                throw new ArgumentNullException(nameof(individualKnowledge));
        }

        public PegaKnowledgeFact<Vector3> GetKnownCharacterPosition(ActorId subjectActorId) =>
            _individualKnowledge.GetLastObservedCharacterPosition(subjectActorId);

        public PegaKnowledgeFact<bool> GetIsPlayer(ActorId subjectActorId) =>
            _individualKnowledge.GetFact(
                PegaNpcKnowledgeFacts.IsPlayer.For(subjectActorId));
    }
    /// <summary>
    /// Read-only GOAP view over one NPC's existing Knowledge State. Facts retain their status and evidence
    /// metadata so conditions can distinguish unknown, invalidated, and known values.
    /// </summary>
    public sealed class PegaNpcGoapKnowledgeStateView : IGoapKnowledgeStateView
    {
        private readonly PegaNpcKnowledgeState _knowledgeState;
        private readonly IReadOnlyList<IGoapKnowledgeCapability> _capabilities;

        internal PegaNpcGoapKnowledgeStateView(
            PegaNpcKnowledgeState knowledgeState,
            IReadOnlyList<IGoapKnowledgeCapability> capabilities)
        {
            _knowledgeState = knowledgeState ?? throw new ArgumentNullException(nameof(knowledgeState));
            if (capabilities == null)
            {
                throw new ArgumentNullException(nameof(capabilities));
            }

            List<IGoapKnowledgeCapability> copy = new List<IGoapKnowledgeCapability>(capabilities.Count);
            bool includesKnowledgeState = false;
            for (int index = 0; index < capabilities.Count; index++)
            {
                IGoapKnowledgeCapability capability = capabilities[index] ??
                    throw new ArgumentException("Knowledge capabilities cannot contain null entries.", nameof(capabilities));
                if (object.ReferenceEquals(capability, knowledgeState))
                {
                    includesKnowledgeState = true;
                }

                copy.Add(capability);
            }

            if (!includesKnowledgeState)
            {
                copy.Add(knowledgeState);
            }

            _capabilities = Array.AsReadOnly(copy.ToArray());
        }

        public GoapStateSource Source => GoapStateSource.KnowledgeState;

        public IReadOnlyList<IGoapKnowledgeCapability> Capabilities => _capabilities;

        public PegaKnowledgeFact<PegaNpcPerceivedAreaPresence> GetPerceivedAreaPresence() =>
            _knowledgeState.GetPerceivedAreaPresence();

        public PegaKnowledgeFact<bool> GetCharacterVisualContact(ActorId subjectActorId) =>
            _knowledgeState.GetCharacterVisualContact(subjectActorId);

        public PegaKnowledgeFact<Vector3> GetLastObservedCharacterPosition(ActorId subjectActorId) =>
            _knowledgeState.GetLastObservedCharacterPosition(subjectActorId);
    }

    /// <summary>A GOAP Target handle backed by an immutable, already-known PEGA planning Target.</summary>
    public sealed class PegaNpcGoapKnownTarget : IGoapKnownTarget
    {
        internal PegaNpcGoapKnownTarget(PegaNpcPlanningTarget target)
        {
            Target = target;
        }

        public PegaNpcPlanningTarget Target { get; }
    }

    /// <summary>Generic predicted arrival at a known planning Target, independent of any Desire or Goal.</summary>
    public sealed class PegaNpcTargetReachedEffect : IGoapEffect
    {
        public PegaNpcTargetReachedEffect(
            PegaNpcPlanningTargetId targetId,
            PegaNpcPlanningTargetKind targetKind,
            double evidenceTimeSeconds)
            : this(targetId, targetKind, evidenceTimeSeconds, default, false)
        {
        }

        public PegaNpcTargetReachedEffect(
            PegaNpcPlanningTargetId targetId,
            PegaNpcPlanningTargetKind targetKind,
            double evidenceTimeSeconds,
            Vector3 targetPosition)
            : this(targetId, targetKind, evidenceTimeSeconds, targetPosition, true)
        {
        }

        private PegaNpcTargetReachedEffect(
            PegaNpcPlanningTargetId targetId,
            PegaNpcPlanningTargetKind targetKind,
            double evidenceTimeSeconds,
            Vector3 targetPosition,
            bool hasTargetPosition)
        {
            if (!targetId.IsValid || !targetKind.IsValid ||
                double.IsNaN(evidenceTimeSeconds) || double.IsInfinity(evidenceTimeSeconds) || evidenceTimeSeconds < 0d ||
                (hasTargetPosition && !IsFinite(targetPosition)))
            {
                throw new ArgumentException(
                    "A valid planning Target identity, kind and evidence time are required.",
                    nameof(targetId));
            }

            TargetId = targetId;
            TargetKind = targetKind;
            EvidenceTimeSeconds = evidenceTimeSeconds;
            TargetPosition = targetPosition;
            HasTargetPosition = hasTargetPosition;
        }

        public PegaNpcPlanningTargetId TargetId { get; }

        public PegaNpcPlanningTargetKind TargetKind { get; }

        public double EvidenceTimeSeconds { get; }

        public Vector3 TargetPosition { get; }

        public bool HasTargetPosition { get; }

        public Immersive.Framework.Actors.GroupId KnowledgeGroupId => TargetId.KnowledgeGroupId;

        public Immersive.Framework.ObjectEntry.ObjectEntryId ObjectEntryId => TargetId.ObjectEntryId;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Temporary progress condition owned by the established last-known-target continuation.</summary>
    internal sealed class PegaNpcLastKnownTargetProgressCondition : IGoapCondition
    {
        private readonly ActorId _subjectActorId;
        private readonly double _contactLossObservationTimeSeconds;
        private readonly double _positionEvidenceTimeSeconds;
        private readonly bool _lastKnownPositionReached;

        public PegaNpcLastKnownTargetProgressCondition(
            ActorId subjectActorId,
            double contactLossObservationTimeSeconds,
            double positionEvidenceTimeSeconds,
            bool lastKnownPositionReached)
        {
            _subjectActorId = subjectActorId;
            _contactLossObservationTimeSeconds = contactLossObservationTimeSeconds;
            _positionEvidenceTimeSeconds = positionEvidenceTimeSeconds;
            _lastKnownPositionReached = lastKnownPositionReached;
        }

        public GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!(state is IGoapEffectStateView effectState))
            {
                return GoapTruthValue.Unknown;
            }

            for (int index = 0; index < effectState.Effects.Count; index++)
            {
                if (!(effectState.Effects[index] is PegaNpcTargetReachedEffect effect) ||
                    Math.Abs(effect.EvidenceTimeSeconds - _positionEvidenceTimeSeconds) > 0.000001d)
                {
                    continue;
                }

                bool reachedLastKnownPosition =
                    effect.TargetKind == PegaNpcPlanningTargetKind.CharacterPosition &&
                    effect.TargetId == PegaNpcPlanningTargetId.ForActor(_subjectActorId);
                bool reachedInvestigationPosition =
                    effect.TargetKind == PegaNpcPlanningTargetKind.LocalObservationPosition &&
                    effect.TargetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.InvestigationSample &&
                    effect.TargetId.InvestigationSubjectActorId == _subjectActorId &&
                    Math.Abs(effect.TargetId.InvestigationLossTimeSeconds -
                             _contactLossObservationTimeSeconds) <= 0.000001d;
                if ((!_lastKnownPositionReached && reachedLastKnownPosition) ||
                    (_lastKnownPositionReached && reachedInvestigationPosition))
                {
                    return GoapTruthValue.True;
                }
            }

            return GoapTruthValue.Unknown;
        }
    }

    /// <summary>
    /// Causal-planning inputs adapted from a validated live NPC input. It references the NPC's existing
    /// Knowledge State and wraps its known Targets; it does not copy or discover world facts.
    /// </summary>
    public sealed class PegaNpcCausalPlanningKnowledge
    {
        private readonly IReadOnlyList<PegaNpcGoapKnownTarget> _knownTargets;

        internal PegaNpcCausalPlanningKnowledge(
            IGoapKnowledgeStateView knowledgeState,
            IReadOnlyList<PegaNpcPlanningTarget> knownTargets,
            IReadOnlyList<IGoapEffect> observedCheckpoints = null)
        {
            if (knowledgeState == null || knowledgeState.Source != GoapStateSource.KnowledgeState)
            {
                throw new ArgumentException(
                    "Causal planning requires the Actor's explicit Knowledge State view.",
                    nameof(knowledgeState));
            }

            if (knownTargets == null)
            {
                throw new ArgumentNullException(nameof(knownTargets));
            }

            KnowledgeState = knowledgeState;
            InitialState = new GoapHypotheticalState(
                KnowledgeState,
                observedCheckpoints ?? Array.Empty<IGoapEffect>());

            PegaNpcGoapKnownTarget[] targets = new PegaNpcGoapKnownTarget[knownTargets.Count];
            for (int index = 0; index < knownTargets.Count; index++)
            {
                PegaNpcPlanningTarget target = knownTargets[index];
                if (!target.IsValid || !target.HasEvidenceTime ||
                    (target.Provenance != PegaNpcPlanningTargetProvenance.KnowledgeState &&
                     target.Provenance != PegaNpcPlanningTargetProvenance.DerivedFromKnowledge))
                {
                    throw new ArgumentException(
                        $"Live causal Target at index {index} is invalid or lacks legitimate knowledge evidence.",
                        nameof(knownTargets));
                }

                targets[index] = new PegaNpcGoapKnownTarget(target);
            }

            _knownTargets = Array.AsReadOnly(targets);
        }

        public IGoapKnowledgeStateView KnowledgeState { get; }

        public GoapHypotheticalState InitialState { get; }

        public IReadOnlyList<PegaNpcGoapKnownTarget> KnownTargets => _knownTargets;

        public bool TryGetKnownTarget(
            PegaNpcPlanningTargetId targetId,
            out PegaNpcGoapKnownTarget knownTarget)
        {
            for (int index = 0; index < _knownTargets.Count; index++)
            {
                PegaNpcGoapKnownTarget candidate = _knownTargets[index];
                if (candidate.Target.TargetId == targetId)
                {
                    knownTarget = candidate;
                    return true;
                }
            }

            knownTarget = null;
            return false;
        }

        /// <summary>
        /// Returns only known Targets whose declared kind matches the requested Action Target kind.
        /// Action-specific conditions remain the responsibility of that Action's planning contract.
        /// </summary>
        public IReadOnlyList<PegaNpcGoapKnownTarget> GetCompatibleTargets(
            PegaNpcPlanningTargetKind requiredTargetKind)
        {
            if (!requiredTargetKind.IsValid)
            {
                throw new ArgumentException("A valid Target kind is required.", nameof(requiredTargetKind));
            }

            List<PegaNpcGoapKnownTarget> compatibleTargets = new List<PegaNpcGoapKnownTarget>();
            for (int index = 0; index < _knownTargets.Count; index++)
            {
                PegaNpcGoapKnownTarget target = _knownTargets[index];
                if (target.Target.Kind == requiredTargetKind)
                {
                    compatibleTargets.Add(target);
                }
            }

            return compatibleTargets.AsReadOnly();
        }
    }
}

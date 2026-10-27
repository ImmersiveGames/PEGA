using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using ImmersiveGames.Goap;
using PEGA.Actors;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// Read-only GOAP view over one NPC's existing Knowledge State. Facts retain their status and evidence
    /// metadata so conditions can distinguish unknown, invalidated, and known values.
    /// </summary>
    public sealed class PEGANpcGoapKnowledgeStateView : IGoapKnowledgeStateView
    {
        private readonly PEGANpcKnowledgeState _knowledgeState;
        private readonly IReadOnlyList<IGoapKnowledgeCapability> _capabilities;

        internal PEGANpcGoapKnowledgeStateView(
            PEGANpcKnowledgeState knowledgeState,
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

        public PEGANpcKnowledgeFact<PEGANpcPerceivedAreaPresence> GetPerceivedAreaPresence() =>
            _knowledgeState.GetPerceivedAreaPresence();

        public PEGANpcKnowledgeFact<ActorProfileId> GetObservedCharacterIdentity(ActorId subjectActorId) =>
            _knowledgeState.GetObservedCharacterIdentity(subjectActorId);

        public PEGANpcKnowledgeFact<bool> GetCharacterVisualContact(ActorId subjectActorId) =>
            _knowledgeState.GetCharacterVisualContact(subjectActorId);

        public PEGANpcKnowledgeFact<Vector3> GetLastObservedCharacterPosition(ActorId subjectActorId) =>
            _knowledgeState.GetLastObservedCharacterPosition(subjectActorId);
    }

    /// <summary>A GOAP Target handle backed by an immutable, already-known PEGA planning Target.</summary>
    public sealed class PEGANpcGoapKnownTarget : IGoapKnownTarget
    {
        internal PEGANpcGoapKnownTarget(PEGANpcPlanningTarget target)
        {
            Target = target;
        }

        public PEGANpcPlanningTarget Target { get; }
    }

    /// <summary>Generic predicted arrival at a known planning Target, independent of any Desire or Goal.</summary>
    public sealed class PEGANpcTargetReachedEffect : IGoapEffect
    {
        public PEGANpcTargetReachedEffect(
            PEGANpcPlanningTargetId targetId,
            PEGANpcPlanningTargetKind targetKind,
            double evidenceTimeSeconds)
        {
            if (!targetId.IsValid || !targetKind.IsValid ||
                double.IsNaN(evidenceTimeSeconds) || double.IsInfinity(evidenceTimeSeconds) || evidenceTimeSeconds < 0d)
            {
                throw new ArgumentException(
                    "A valid planning Target identity, kind and evidence time are required.",
                    nameof(targetId));
            }

            TargetId = targetId;
            TargetKind = targetKind;
            EvidenceTimeSeconds = evidenceTimeSeconds;
        }

        public PEGANpcPlanningTargetId TargetId { get; }

        public PEGANpcPlanningTargetKind TargetKind { get; }

        public double EvidenceTimeSeconds { get; }
    }

    /// <summary>Temporary progress condition owned by the established last-known-target continuation.</summary>
    internal sealed class PEGANpcLastKnownTargetProgressCondition : IGoapCondition
    {
        private readonly ActorId _subjectActorId;
        private readonly double _contactLossObservationTimeSeconds;
        private readonly double _positionEvidenceTimeSeconds;
        private readonly bool _lastKnownPositionReached;

        public PEGANpcLastKnownTargetProgressCondition(
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
                if (!(effectState.Effects[index] is PEGANpcTargetReachedEffect effect) ||
                    Math.Abs(effect.EvidenceTimeSeconds - _positionEvidenceTimeSeconds) > 0.000001d)
                {
                    continue;
                }

                bool reachedLastKnownPosition =
                    effect.TargetKind == PEGANpcPlanningTargetKind.CharacterPosition &&
                    effect.TargetId == PEGANpcPlanningTargetId.ForActor(_subjectActorId);
                bool reachedInvestigationPosition =
                    effect.TargetKind == PEGANpcPlanningTargetKind.LocalObservationPosition &&
                    effect.TargetId.IdentityKind == PEGANpcPlanningTargetIdentityKind.InvestigationSample &&
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
    public sealed class PEGANpcCausalPlanningKnowledge
    {
        private readonly IReadOnlyList<PEGANpcGoapKnownTarget> _knownTargets;

        internal PEGANpcCausalPlanningKnowledge(
            IGoapKnowledgeStateView knowledgeState,
            IReadOnlyList<PEGANpcPlanningTarget> knownTargets)
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
            InitialState = new GoapHypotheticalState(KnowledgeState);

            PEGANpcGoapKnownTarget[] targets = new PEGANpcGoapKnownTarget[knownTargets.Count];
            for (int index = 0; index < knownTargets.Count; index++)
            {
                PEGANpcPlanningTarget target = knownTargets[index];
                if (!target.IsValid || !target.HasEvidenceTime ||
                    (target.Provenance != PEGANpcPlanningTargetProvenance.KnowledgeState &&
                     target.Provenance != PEGANpcPlanningTargetProvenance.DerivedFromKnowledge))
                {
                    throw new ArgumentException(
                        $"Live causal Target at index {index} is invalid or lacks legitimate knowledge evidence.",
                        nameof(knownTargets));
                }

                targets[index] = new PEGANpcGoapKnownTarget(target);
            }

            _knownTargets = Array.AsReadOnly(targets);
        }

        public IGoapKnowledgeStateView KnowledgeState { get; }

        public GoapHypotheticalState InitialState { get; }

        public IReadOnlyList<PEGANpcGoapKnownTarget> KnownTargets => _knownTargets;

        public bool TryGetKnownTarget(
            PEGANpcPlanningTargetId targetId,
            out PEGANpcGoapKnownTarget knownTarget)
        {
            for (int index = 0; index < _knownTargets.Count; index++)
            {
                PEGANpcGoapKnownTarget candidate = _knownTargets[index];
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
        public IReadOnlyList<PEGANpcGoapKnownTarget> GetCompatibleTargets(
            PEGANpcPlanningTargetKind requiredTargetKind)
        {
            if (!requiredTargetKind.IsValid)
            {
                throw new ArgumentException("A valid Target kind is required.", nameof(requiredTargetKind));
            }

            List<PEGANpcGoapKnownTarget> compatibleTargets = new List<PEGANpcGoapKnownTarget>();
            for (int index = 0; index < _knownTargets.Count; index++)
            {
                PEGANpcGoapKnownTarget target = _knownTargets[index];
                if (target.Target.Kind == requiredTargetKind)
                {
                    compatibleTargets.Add(target);
                }
            }

            return compatibleTargets.AsReadOnly();
        }
    }
}

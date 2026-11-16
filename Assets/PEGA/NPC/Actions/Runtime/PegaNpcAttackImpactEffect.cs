using System;
using Immersive.Framework.Actors;
using PEGA.GOAP;
using PEGA.NPC.Autonomy.Planning;
using UnityEngine;

namespace PEGA.NPC.Actions.Runtime
{
    public sealed class PegaNpcAttackImpactEffect : IGoapEffect, IGoapEffectSatisfaction
    {
        internal PegaNpcAttackImpactEffect(
            PegaNpcPlanningTargetId targetId,
            ActorOccurrenceId targetOccurrenceId,
            Vector3 targetPosition,
            double evidenceTimeSeconds)
        {
            if (!targetId.IsValid ||
                targetId.IdentityKind != PegaNpcPlanningTargetIdentityKind.Actor ||
                !targetOccurrenceId.IsValid ||
                !IsFinite(targetPosition) ||
                !IsNonNegativeFinite(evidenceTimeSeconds))
            {
                throw new ArgumentException("A valid known Actor Target and its current position evidence are required.", nameof(targetId));
            }

            TargetId = targetId;
            TargetActorId = targetId.KnownActorId;
            TargetOccurrenceId = targetOccurrenceId;
            TargetPosition = targetPosition;
            EvidenceTimeSeconds = evidenceTimeSeconds;
        }

        public PegaNpcPlanningTargetId TargetId { get; }
        public ActorId TargetActorId { get; }
        public ActorOccurrenceId TargetOccurrenceId { get; }
        public Vector3 TargetPosition { get; }
        public double EvidenceTimeSeconds { get; }

        public GoapTruthValue EvaluateSatisfaction(IGoapStateView state)
        {
            if (state is IGoapHypotheticalStateView hypothetical)
            {
                if (Contains(hypothetical.ProjectedEffects) || Contains(hypothetical.ObservedEffects))
                {
                    return GoapTruthValue.True;
                }

                return GoapTruthValue.False;
            }

            if (state is IGoapObservedStateView observed)
            {
                return Contains(observed.ObservedEffects)
                    ? GoapTruthValue.True
                    : GoapTruthValue.False;
            }

            if (state is IGoapEffectStateView effectState)
            {
                return Contains(effectState.Effects)
                    ? GoapTruthValue.True
                    : GoapTruthValue.False;
            }

            return GoapTruthValue.Unknown;
        }

        private bool Contains(System.Collections.Generic.IReadOnlyList<IGoapEffect> effects)
        {
            for (int index = 0; index < effects.Count; index++)
            {
                if (effects[index] is PegaNpcAttackImpactEffect impact &&
                    impact.TargetActorId == TargetActorId &&
                    impact.TargetOccurrenceId.Equals(TargetOccurrenceId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    }
}

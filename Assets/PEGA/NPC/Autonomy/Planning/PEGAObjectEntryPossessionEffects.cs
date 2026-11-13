using System;
using System.Collections.Generic;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.Diagnostics;
using PEGA.GOAP;
using UnityEngine;
namespace PEGA.NPC.Autonomy.Planning
{
    public sealed class PegaObjectEntryPossessedEffect : IGoapEffect, IGoapEffectSatisfaction
    {
        public PegaObjectEntryPossessedEffect(ActorId actorId, ObjectEntryId objectEntryId)
        {
            if (!actorId.IsValid || !objectEntryId.IsValid)
            {
                throw new ArgumentException("Possession requires valid ActorId and ObjectEntryId identities.");
            }

            ActorId = actorId;
            ObjectEntryId = objectEntryId;
        }

        public ActorId ActorId { get; }
        public ObjectEntryId ObjectEntryId { get; }

        public GoapTruthValue EvaluateSatisfaction(IGoapStateView state)
        {
            if (state is IGoapHypotheticalStateView hypothetical)
            {
                return IsPresent(hypothetical.ObservedEffects) || IsPresent(hypothetical.ProjectedEffects)
                    ? GoapTruthValue.True
                    : GoapTruthValue.False;
            }

            if (state is IGoapEffectStateView effectState)
            {
                return IsPresent(effectState.Effects)
                    ? GoapTruthValue.True
                    : GoapTruthValue.False;
            }

            return GoapTruthValue.Unknown;
        }

        private bool IsPresent(IReadOnlyList<IGoapEffect> effects)
        {
            for (int index = 0; index < effects.Count; index++)
            {
                if (effects[index] is PegaObjectEntryPossessedEffect possessed &&
                    possessed.ActorId == ActorId &&
                    possessed.ObjectEntryId == ObjectEntryId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class PegaObjectEntryInteractionRangeCondition : IGoapCondition
    {
        private readonly Vector3 _actorPosition;
        private readonly Vector3 _targetPosition;
        private readonly float _interactionRangeMeters;
        private readonly PegaNpcPlanningTargetId _targetId;
        private readonly ScopedLogger _log;

        public PegaObjectEntryInteractionRangeCondition(
            PegaNpcPlanningTargetId targetId,
            Vector3 actorPosition,
            Vector3 targetPosition,
            float interactionRangeMeters,
            ScopedLogger log)
        {
            _targetId = targetId;
            _actorPosition = actorPosition;
            _targetPosition = targetPosition;
            _interactionRangeMeters = interactionRangeMeters;
            _log = log;
        }

        public GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (state == null || !(state is IGoapEffectStateView effectState) ||
                !IsFinite(_actorPosition) || !IsFinite(_targetPosition) ||
                !IsNonNegativeFinite(_interactionRangeMeters))
            {
                LogRangeEvaluation(
                    GoapTruthValue.Unknown,
                    _actorPosition,
                    false,
                    float.PositiveInfinity,
                    "The hypothetical state or Pick Up range inputs are unavailable or invalid.");
                return GoapTruthValue.Unknown;
            }

            Vector3 hypotheticalActorPosition = _actorPosition;
            bool hasProjectedArrival = false;
            if (state is IGoapHypotheticalStateView hypothetical &&
                _targetId.IdentityKind == PegaNpcPlanningTargetIdentityKind.IndividualObjectEntryPosition)
            {
                bool hasPositionForLatestProjectedArrival = false;
                for (int index = 0; index < hypothetical.ProjectedEffects.Count; index++)
                {
                    if (!(hypothetical.ProjectedEffects[index] is PegaNpcTargetReachedEffect reached))
                    {
                        continue;
                    }

                    hasProjectedArrival = true;
                    hasPositionForLatestProjectedArrival = reached.HasTargetPosition;
                    if (reached.HasTargetPosition)
                    {
                        hypotheticalActorPosition = reached.TargetPosition;
                    }
                }

                if (hasProjectedArrival && !hasPositionForLatestProjectedArrival)
                {
                    const string diagnostic = "The latest projected arrival has no authoritative target position for evaluating Pick Up range.";
                    LogRangeEvaluation(
                        GoapTruthValue.Unknown,
                        hypotheticalActorPosition,
                        true,
                        float.PositiveInfinity,
                        diagnostic);
                    return GoapTruthValue.Unknown;
                }
            }
            else
            {
                for (int index = 0; index < effectState.Effects.Count; index++)
                {
                    if (effectState.Effects[index] is PegaNpcTargetReachedEffect reached &&
                        reached.HasTargetPosition)
                    {
                        hypotheticalActorPosition = reached.TargetPosition;
                        hasProjectedArrival = true;
                    }
                }
            }

            float distanceMeters = Vector3.Distance(hypotheticalActorPosition, _targetPosition);
            GoapTruthValue result = distanceMeters <= _interactionRangeMeters
                ? GoapTruthValue.True
                : GoapTruthValue.False;
            LogRangeEvaluation(
                result,
                hypotheticalActorPosition,
                hasProjectedArrival,
                distanceMeters,
                string.Empty);
            return result;
        }

        private void LogRangeEvaluation(
            GoapTruthValue result,
            Vector3 hypotheticalActorPosition,
            bool hasProjectedArrival,
            float distanceMeters,
            string diagnostic)
        {
            _log?.Debug(
                "NPC Pick Up causal interaction range condition was evaluated.",
                LogFields.Field("targetId", _targetId),
                LogFields.Field("result", result),
                LogFields.Field("hasProjectedArrival", hasProjectedArrival),
                LogFields.Field("hypotheticalActorPosition", hypotheticalActorPosition),
                LogFields.Field("knownTargetPosition", _targetPosition),
                LogFields.Field("distance3dMeters", distanceMeters),
                LogFields.Field("interactionRangeMeters", _interactionRangeMeters),
                LogFields.Field("diagnostic", diagnostic));
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) => value >= 0f && IsFinite(value);
    }
}

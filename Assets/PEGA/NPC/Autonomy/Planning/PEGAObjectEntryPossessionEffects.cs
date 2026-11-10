using System;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.GOAP;
using UnityEngine;
namespace PEGA.NPC.Autonomy.Planning
{
    public sealed class PegaObjectEntryPossessedEffect : IGoapEffect
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
    }

    internal sealed class PegaObjectEntryInteractionRangeCondition : IGoapCondition
    {
        private readonly Vector3 _actorPosition;
        private readonly Vector3 _targetPosition;
        private readonly float _interactionRangeMeters;

        public PegaObjectEntryInteractionRangeCondition(
            Vector3 actorPosition,
            Vector3 targetPosition,
            float interactionRangeMeters)
        {
            _actorPosition = actorPosition;
            _targetPosition = targetPosition;
            _interactionRangeMeters = interactionRangeMeters;
        }

        public GoapTruthValue Evaluate(IGoapStateView state)
        {
            if (state == null || !(state is IGoapEffectStateView effectState) ||
                !IsFinite(_actorPosition) || !IsFinite(_targetPosition) ||
                !IsNonNegativeFinite(_interactionRangeMeters))
            {
                return GoapTruthValue.Unknown;
            }

            Vector3 hypotheticalActorPosition = _actorPosition;
            for (int index = 0; index < effectState.Effects.Count; index++)
            {
                if (effectState.Effects[index] is PegaNpcTargetReachedEffect reached &&
                    reached.HasTargetPosition)
                {
                    hypotheticalActorPosition = reached.TargetPosition;
                }
            }

            return Vector3.Distance(hypotheticalActorPosition, _targetPosition) <= _interactionRangeMeters
                ? GoapTruthValue.True
                : GoapTruthValue.False;
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsNonNegativeFinite(float value) => value >= 0f && IsFinite(value);
    }
}

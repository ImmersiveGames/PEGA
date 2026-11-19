using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using UnityEngine;

namespace PEGA.NPC.Knowledge.Definitions
{
    /// <summary>Primeira descoberta pública da relíquia durante a missão.</summary>
    public readonly struct PegaRelicDiscovery
    {
        public PegaRelicDiscovery(ActorId discovererActorId, ActorOccurrenceId discovererOccurrenceId,
            ObjectEntryId relicId, Vector3 position, double timeSeconds)
        {
            DiscovererActorId = discovererActorId;
            DiscovererOccurrenceId = discovererOccurrenceId;
            RelicId = relicId;
            Position = position;
            TimeSeconds = timeSeconds;
        }

        public ActorId DiscovererActorId { get; }
        public ActorOccurrenceId DiscovererOccurrenceId { get; }
        public ObjectEntryId RelicId { get; }
        public Vector3 Position { get; }
        public double TimeSeconds { get; }

        public bool IsValid => DiscovererActorId.IsValid && DiscovererOccurrenceId.IsValid && RelicId.IsValid &&
            IsFinite(Position.x) && IsFinite(Position.y) && IsFinite(Position.z) &&
            IsFinite(TimeSeconds) && TimeSeconds >= 0d;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

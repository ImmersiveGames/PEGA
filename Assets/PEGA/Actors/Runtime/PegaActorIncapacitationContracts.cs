using Immersive.Foundation.Events;
using Immersive.Framework.Actors;
using UnityEngine;

namespace PEGA.Actors.Runtime
{
    public sealed class PegaActorIncapacitationChangedEvent : IEvent
    {
        internal PegaActorIncapacitationChangedEvent(
            GameObject actorRoot,
            ActorId actorId,
            bool isIncapacitated,
            ActorId causedByActorId,
            double changedAtTimeSeconds)
        {
            ActorRoot = actorRoot;
            ActorId = actorId;
            IsIncapacitated = isIncapacitated;
            CausedByActorId = causedByActorId;
            ChangedAtTimeSeconds = changedAtTimeSeconds;
        }

        public GameObject ActorRoot { get; }
        public ActorId ActorId { get; }
        public bool IsIncapacitated { get; }
        public ActorId CausedByActorId { get; }
        public double ChangedAtTimeSeconds { get; }
    }
}

using System;
using System.Collections.Generic;
using Immersive.Framework.Actors;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>Explicit PEGA knowledge channel shared by content owners in one game Session.</summary>
    [CreateAssetMenu(fileName = "ActorGroupKnowledgeContext", menuName = "PEGA/Actors/Group Knowledge Context")]
    public sealed class PEGAActorGroupKnowledgeContext : ScriptableObject
    {
        [NonSerialized]
        private Dictionary<GroupId, PEGAActorGroupKnowledgeState> _authorities;

        [NonSerialized]
        private ScopedLogger _log;

        internal event Action<GroupId, PEGAActorGroupKnowledgeFactChangedEvent> Changed;

        internal bool TryRegister(GroupId groupId, PEGAActorGroupKnowledgeState authority, out string diagnostic)
        {
            if (!groupId.IsValid || authority == null)
            {
                diagnostic = "A valid GroupId and PEGA knowledge authority are required.";
                return false;
            }

            _authorities ??= new Dictionary<GroupId, PEGAActorGroupKnowledgeState>();
            if (_authorities.TryGetValue(groupId, out PEGAActorGroupKnowledgeState existing) &&
                existing != null && existing != authority)
            {
                diagnostic = $"GroupId '{groupId.StableText}' already has a PEGA knowledge authority in this context.";
                return false;
            }

            _authorities[groupId] = authority;
            diagnostic = string.Empty;
            PublishChanged(groupId, null);
            return true;
        }

        internal void Unregister(GroupId groupId, PEGAActorGroupKnowledgeState authority)
        {
            if (_authorities != null && _authorities.TryGetValue(groupId, out PEGAActorGroupKnowledgeState current) &&
                current == authority)
            {
                _authorities.Remove(groupId);
                PublishChanged(groupId, null);
            }
        }

        internal bool TryGetAuthority(GroupId groupId, out PEGAActorGroupKnowledgeState authority)
        {
            authority = null;
            return groupId.IsValid && _authorities != null &&
                _authorities.TryGetValue(groupId, out authority) && authority != null && authority.IsOwnerActive;
        }

        internal void NotifyFactChanged(GroupId groupId, PEGAActorGroupKnowledgeFactChangedEvent change)
        {
            if (TryGetAuthority(groupId, out _))
            {
                PublishChanged(groupId, change);
            }
        }

        private void PublishChanged(GroupId groupId, PEGAActorGroupKnowledgeFactChangedEvent change)
        {
            Action<GroupId, PEGAActorGroupKnowledgeFactChangedEvent> handlers = Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate subscriber in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<GroupId, PEGAActorGroupKnowledgeFactChangedEvent>)subscriber)(groupId, change);
                }
                catch (Exception exception)
                {
                    _log ??= PEGALogging.CreateScopedLogger<PEGAActorGroupKnowledgeContext>(
                        "PEGA.Gameplay.ActorGroupKnowledge", name);
                    _log.Error("A Group knowledge subscriber failed after the fact was accepted.",
                        LogFields.Field("groupId", groupId.StableText),
                        LogFields.Field("subscriberMethod", subscriber.Method.Name),
                        LogFields.Field("exceptionType", exception.GetType().FullName),
                        LogFields.Field("exceptionMessage", exception.Message));
                }
            }
        }

        private void OnDisable()
        {
            _authorities?.Clear();
            Changed = null;
            _log = null;
        }
    }
}

using System;
using System.Collections.Generic;
using PEGA.GOAP;
namespace PEGA.NPC.Knowledge.Core
{
    /// <summary>Composes independently owned PEGA Knowledge capabilities into a generic GOAP Knowledge view.</summary>
    public sealed class PegaKnowledgeGoapStateView : IGoapKnowledgeStateView
    {
        private readonly IReadOnlyList<IGoapKnowledgeCapability> _capabilities;

        public PegaKnowledgeGoapStateView(params IGoapKnowledgeCapability[] capabilities)
        {
            if (capabilities == null)
            {
                throw new ArgumentNullException(nameof(capabilities));
            }

            var copy = new List<IGoapKnowledgeCapability>(capabilities.Length);
            for (int index = 0; index < capabilities.Length; index++)
            {
                copy.Add(capabilities[index] ??
                    throw new ArgumentException("Knowledge capabilities cannot contain null entries.", nameof(capabilities)));
            }

            _capabilities = Array.AsReadOnly(copy.ToArray());
        }

        public GoapStateSource Source => GoapStateSource.KnowledgeState;

        public IReadOnlyList<IGoapKnowledgeCapability> Capabilities => _capabilities;
    }
}

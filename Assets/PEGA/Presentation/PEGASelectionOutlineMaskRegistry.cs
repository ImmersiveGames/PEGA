using System.Collections.Generic;
using UnityEngine;

namespace PEGA.Presentation
{
    /// <summary>Marca renderers atualmente selecionados para a passagem visual do URP.</summary>
    internal static class PegaSelectionOutlineMaskRegistry
    {
        internal const int SelectionRenderingLayerIndex = 8;
        internal const uint SelectionRenderingLayerMask = 1u << SelectionRenderingLayerIndex;

        private sealed class RendererLease
        {
            internal RendererLease(uint originalSelectionBit)
            {
                OriginalSelectionBit = originalSelectionBit;
                ReferenceCount = 1;
            }

            internal uint OriginalSelectionBit { get; }
            internal int ReferenceCount { get; set; }
        }

        private static readonly Dictionary<Renderer, RendererLease> RendererLeases =
            new Dictionary<Renderer, RendererLease>();

        internal static int ActiveRendererCount => RendererLeases.Count;

        internal static void Acquire(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (RendererLeases.TryGetValue(renderer, out RendererLease lease))
            {
                lease.ReferenceCount++;
                return;
            }

            uint originalSelectionBit = renderer.renderingLayerMask & SelectionRenderingLayerMask;
            RendererLeases.Add(renderer, new RendererLease(originalSelectionBit));
            renderer.renderingLayerMask |= SelectionRenderingLayerMask;
        }

        internal static void Release(Renderer renderer)
        {
            if (ReferenceEquals(renderer, null) ||
                !RendererLeases.TryGetValue(renderer, out RendererLease lease))
            {
                return;
            }

            lease.ReferenceCount--;
            if (lease.ReferenceCount > 0)
            {
                return;
            }

            if (renderer != null)
            {
                uint currentMask = renderer.renderingLayerMask;
                currentMask &= ~SelectionRenderingLayerMask;
                renderer.renderingLayerMask = currentMask | lease.OriginalSelectionBit;
            }

            RendererLeases.Remove(renderer);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            foreach (KeyValuePair<Renderer, RendererLease> pair in RendererLeases)
            {
                Renderer renderer = pair.Key;
                if (renderer == null)
                {
                    continue;
                }

                uint currentMask = renderer.renderingLayerMask;
                currentMask &= ~SelectionRenderingLayerMask;
                renderer.renderingLayerMask = currentMask | pair.Value.OriginalSelectionBit;
            }

            RendererLeases.Clear();
        }
    }
}

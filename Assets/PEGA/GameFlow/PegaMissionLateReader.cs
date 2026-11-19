using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class PegaMissionLateReader : MonoBehaviour
    {
        [SerializeField] private PegaMissionLifecycle mission;

        private void OnEnable()
        {
            var log = PegaLogging.CreateScopedLogger<PegaMissionLateReader>("PEGA.Gameplay.Mission", gameObject.name);
            if (mission == null || !mission.TryRead(out PegaMissionSnapshot snapshot))
            {
                log.Error("No current mission context is available to the late reader.");
                return;
            }

            if (!snapshot.Discovery.HasValue || !mission.TryReadGlobalDiscovery(out var shared) ||
                !snapshot.Discovery.Value.Equals(shared))
            {
                log.Error($"Late reader could not match the public discovery to mission history. {snapshot}");
                return;
            }
            log.Info($"First mission read after enabling the reader: {snapshot}; Global discovery matches mission history.");
        }
    }
}
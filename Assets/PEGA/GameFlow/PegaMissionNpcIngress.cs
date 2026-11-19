using System;
using Immersive.Framework.Authoring;
using Immersive.Framework.RouteLifecycle;
using Immersive.Logging.Loggers;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.GameFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RouteContentContribution))]
    [AddComponentMenu("PEGA/Game Flow/Mission NPC Ingress")]
    public sealed class PegaMissionNpcIngress : RouteContentBehaviour
    {
        [SerializeField] private PegaMissionLifecycle mission;
        [SerializeField] private GameObject npc;
        [SerializeField] private Transform entryPoint;

        private ScopedLogger _log;
        private bool _routeBound;
        private bool _admitted;

        protected override void OnRouteContentEntered(RouteContentLifecycleContext context)
        {
            if (context.Binding == null || context.Binding.gameObject != gameObject || _routeBound)
                return;

            if (mission == null || mission.gameObject != gameObject ||
                npc == null || npc.scene != gameObject.scene ||
                entryPoint == null || entryPoint.gameObject.scene != gameObject.scene ||
                !npc.transform.IsChildOf(context.Binding.transform) ||
                !entryPoint.IsChildOf(context.Binding.transform) ||
                npc.transform == entryPoint ||
                npc.activeSelf)
            {
                throw new InvalidOperationException(
                    "Mission NPC ingress requires one inactive Route-owned NPC occurrence, a Route-owned entry point and the mission lifecycle on the same Route contribution.");
            }

            _log = PegaLogging.CreateScopedLogger<PegaMissionNpcIngress>(
                "PEGA.Gameplay.Mission",
                npc.name);
            _routeBound = true;
            _admitted = false;
            mission.Observed += OnMissionObserved;
            AdmitIfAssaultIsOperational();
        }

        protected override void OnRouteContentExited(RouteContentLifecycleContext context)
        {
            ReleaseRouteBinding();
            if (npc != null)
                npc.SetActive(false);
        }

        private void OnDestroy() => ReleaseRouteBinding();

        private void OnMissionObserved(string milestone) => AdmitIfAssaultIsOperational();

        private void AdmitIfAssaultIsOperational()
        {
            if (!_routeBound || _admitted || mission == null || !mission.IsAssaultOperational)
                return;

            if (npc == null || npc.activeSelf)
            {
                throw new InvalidOperationException(
                    "Mission NPC ingress expected the authored NPC occurrence to remain inactive until Assault admission.");
            }

            npc.transform.SetPositionAndRotation(entryPoint.position, entryPoint.rotation);
            npc.SetActive(true);
            _log.Info($"NPC occurrence {npc.name} entered Assault at {Time.timeAsDouble:R}.");
            _admitted = true;
        }

        private void ReleaseRouteBinding()
        {
            if (!_routeBound)
                return;

            _routeBound = false;
            if (mission != null)
                mission.Observed -= OnMissionObserved;
            _admitted = false;
        }
    }
}
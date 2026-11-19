using System;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using Immersive.Framework.RuntimeContent;
using Immersive.Logging.Loggers;
using PEGA.Diagnostics;
using PEGA.Interactions.Runtime;
using PEGA.Interactions.Runtime.Actor;
using PEGA.Interactions.Runtime.ObjectEntry;
using PEGA.NPC.Knowledge.Definitions;
using UnityEngine;

namespace PEGA.GameFlow
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/Game Flow/Mission Discovery Proof")]
    public sealed class PegaMissionDiscoveryProof : MonoBehaviour, IPegaObjectInteractionOccurrenceAuthority
    {
        [SerializeField] private PegaMissionLifecycle mission;
        [SerializeField] private ActorOccurrenceQueryBinding actorQuery;
        [SerializeField] private Collider npcCollider;
        [SerializeField] private PegaActorPhysicalInventory inventory;
        [SerializeField] private ObjectEntryOccurrenceQueryBinding relicQuery;
        [SerializeField] private PegaObjectEntryPickUpAction pickUp;

        private PegaObjectEntryPhysicalItem _item;
        private PegaNpcObjectInteractionMechanic _interaction;
        private PegaRelicDiscovery? _confirmation;
        private ScopedLogger _log;

        private void Awake()
        {
            if (mission == null || actorQuery == null || npcCollider == null || inventory == null ||
                relicQuery == null || pickUp == null ||
                !pickUp.TryGetComponent(out _item) || !inventory.TryGetComponent(out _interaction))
            {
                enabled = false;
                throw new InvalidOperationException("Configure the Route discovery proof with its mission, occurrence queries, NPC and physical Pick Up.");
            }

            _log = PegaLogging.CreateScopedLogger<PegaMissionDiscoveryProof>("PEGA.Gameplay.Mission", name);
        }

        private void OnEnable() => mission.Observed += RecordEvidence;
        private void OnDisable()
        {
            if (mission != null)
                mission.Observed -= RecordEvidence;
        }

        [ContextMenu("Proof/1. Pick up the relic")]
        private void PickUpRelic()
        {
            if (!Application.isPlaying) return;
            if (!mission.IsAssaultReady)
            {
                _log.Warning("Pick Up requires the Assault Activity to be ready.");
                return;
            }

            if (!TryReadOccurrences(out _, out _, out string diagnostic) ||
                !PegaObjectInteractionRequest.TryCreate(PegaObjectInteractionParticipant.Npc,
                    inventory.gameObject, pickUp.gameObject, this, out var request, out diagnostic) ||
                !_interaction.TryStartRequestedAction(pickUp, request, out _, out _, out diagnostic))
                _log.Warning($"Controlled Pick Up rejected. {diagnostic}");
        }

        [ContextMenu("Proof/2. Confirm the discovery")]
        private void ConfirmDiscovery()
        {
            if (!Application.isPlaying) return;
            if (_confirmation.HasValue || !mission.IsAssaultReady)
            {
                _log.Warning("The first controlled confirmation requires ready Assault and a new discovery.");
                return;
            }

            if (!TryReadOccurrences(out var actor, out var relicId, out string diagnostic) ||
                !inventory.Contains(relicId) || inventory.Count != 1 || _item.IsAvailableOnGround)
            {
                _log.Warning($"Confirm after the NPC's Pick Up completes. {diagnostic}");
                return;
            }

            RecordEvidence("before-discovery");
            var confirmation = new PegaRelicDiscovery(actor.ActorId, actor.Id, relicId,
                _item.transform.position, Time.timeAsDouble);
            if (mission.ConfirmDiscovery(confirmation))
                _confirmation = confirmation;
            else
                _log.Warning("The mission rejected the controlled discovery.");
        }

        [ContextMenu("Proof/3. Redeliver the same confirmation")]
        private void RedeliverDiscovery()
        {
            if (!Application.isPlaying) return;
            if (!_confirmation.HasValue)
            {
                _log.Warning("There is no accepted controlled confirmation to redeliver.");
                return;
            }

            bool accepted = mission.ConfirmDiscovery(_confirmation.Value);
            _log.Info($"Redelivered the original confirmation. Accepted={accepted}");
        }

        public bool TryValidateCurrentOccurrence(GameObject occurrenceRoot, ObjectEntryId objectEntryId,
            out string diagnostic)
        {
            if (!isActiveAndEnabled || pickUp == null || occurrenceRoot != pickUp.gameObject)
            {
                diagnostic = "The interaction no longer addresses the authored relic occurrence.";
                return false;
            }

            var result = relicQuery.Resolve(StableObjectReference.ForEntry(objectEntryId));
            diagnostic = result.Succeeded && result.Occurrence != occurrenceRoot
                ? "The resolved Object Entry belongs to a different physical occurrence."
                : result.Diagnostic;
            return result.Succeeded && result.Occurrence == occurrenceRoot;
        }

        private bool TryReadOccurrences(out ActorOccurrence actor, out ObjectEntryId relicId, out string diagnostic)
        {
            actor = default;
            relicId = default;
            if (actorQuery == null || npcCollider == null || inventory == null ||
                relicQuery == null || pickUp == null || _item == null)
            {
                diagnostic = "A required Route proof component or physical occurrence is missing.";
                return false;
            }

            var recognition = actorQuery.Recognize(npcCollider);
            actor = recognition.Occurrence;
            if (!recognition.Succeeded || actor.ActorRoot != inventory.gameObject ||
                actor.Owner.Scope != RuntimeContentScope.Route)
            {
                diagnostic = $"The stationary NPC is not recognized under its Route. {recognition.Diagnostic}";
                return false;
            }

            if (!_item.TryGetObjectEntryId(out relicId))
            {
                diagnostic = "The relic has no valid Object Entry identity.";
                return false;
            }

            return TryValidateCurrentOccurrence(pickUp.gameObject, relicId, out diagnostic);
        }

        public string DescribePhysicalState()
        {
            if (!TryReadOccurrences(out var actor, out var relicId, out string diagnostic))
                return $"Unresolved physical state. {diagnostic}";

            return $"owner={actor.Owner.StableText} npc={actor.ActorId.StableText} occurrence={actor.Id} " +
                $"position={actor.ActorRoot.transform.position.ToString("F5")} relic={relicId.StableText} " +
                $"relicOccurrence={pickUp.gameObject.GetEntityId()} possessed={inventory.Contains(relicId)} " +
                $"count={inventory.Count} onGround={_item.IsAvailableOnGround}";
        }

        private void RecordEvidence(string milestone)
        {
            string current = mission.TryRead(out var snapshot) ? snapshot.ToString() : "Mission unavailable";
            _log.Info($"Controlled discovery proof [{milestone}]. {current} {DescribePhysicalState()}");
        }
    }
}

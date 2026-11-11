using System;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.Interactions.Runtime;
using PEGA.Interactions.Runtime.ObjectEntry;
using PEGA.NPC.Knowledge.Individual;
using UnityEngine;
using UnityEngine.Serialization;
namespace PEGA.NPC.Knowledge.Authoring
{
    public enum PegaKnowledgeActorIdentitySource
    {
        ActorDeclaration = 0,
        CurrentPlayerActorFromNpcBinding = 1
    }

    /// <summary>
    /// Typed Inspector slots shared by fact addresses and values. The selected canonical definition determines
    /// which slot is read; ActorId is always resolved through a Framework identity contract.
    /// </summary>
    [Serializable]
    public struct PegaKnowledgeFactAuthoringPayload
    {
        [SerializeField, HideInInspector] private PegaKnowledgeActorIdentitySource actorIdentitySource;
        [SerializeField, HideInInspector] private ActorDeclaration actorDeclaration;
        [SerializeField, HideInInspector] private ObjectEntryIdReference objectEntryId;
        [SerializeField, HideInInspector] private string stringValue;
        [SerializeField, HideInInspector] private int integerValue;
        [SerializeField, HideInInspector] private long longValue;
        [SerializeField, HideInInspector] private float floatValue;
        [SerializeField, HideInInspector] private double doubleValue;
        [SerializeField, HideInInspector] private bool boolValue;
        [SerializeField, HideInInspector] private Vector3 vector3Value;
        [SerializeField, HideInInspector] private PegaNpcPerceivedAreaPresence perceivedAreaPresenceValue;
        [SerializeField, HideInInspector]
        [FormerlySerializedAs("objectEntryRobActionValue")]
        [FormerlySerializedAs("robObjectEntryActionValue")]
        private PegaObjectEntryPickUpAction objectEntryPickUpActionValue;

        public static bool Supports(Type type) =>
            type == typeof(ActorId) ||
            type == typeof(ObjectEntryId) ||
            type == typeof(string) ||
            type == typeof(byte) ||
            type == typeof(int) ||
            type == typeof(long) ||
            type == typeof(float) ||
            type == typeof(double) ||
            type == typeof(bool) ||
            type == typeof(Vector3) ||
            type == typeof(PegaNpcPerceivedAreaPresence) ||
            type == typeof(PegaPickUpObjectEntryConfigurationSnapshot) ||
            IsSupportedEnum(type);

        private static bool IsSupportedEnum(Type type) =>
            type != null && type.IsEnum && Enum.GetUnderlyingType(type) == typeof(int);

        internal bool TryGetValue(
            Type type,
            ActorOccurrenceReferenceBinding actorBinding,
            object expectedAddress,
            out object resolved,
            out bool pending,
            out string diagnostic)
        {
            resolved = null;
            pending = false;
            diagnostic = string.Empty;
            if (!Supports(type))
            {
                diagnostic = $"Data type '{type?.FullName ?? "<null>"}' has no Unity authoring payload format.";
                return false;
            }

            if (type == typeof(ActorId))
            {
                return TryGetActorId(actorBinding, out resolved, out pending, out diagnostic);
            }

            if (type == typeof(PegaPickUpObjectEntryConfigurationSnapshot))
            {
                if (!(expectedAddress is ObjectEntryId expectedObjectEntryId) ||
                    !expectedObjectEntryId.IsValid)
                {
                    diagnostic = "Pick Up planning parameters require a valid ObjectEntryId address.";
                    return false;
                }

                if (objectEntryPickUpActionValue == null ||
                    !objectEntryPickUpActionValue.TryGetConfiguration(
                        out PegaPickUpObjectEntryConfigurationSnapshot parameters,
                        out diagnostic))
                {
                    diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                        ? "Select a valid Pick Up action component offered to NPC participants."
                        : diagnostic;
                    return false;
                }

                PegaObjectEntryInteractionAggregator aggregator =
                    objectEntryPickUpActionValue.GetComponent<PegaObjectEntryInteractionAggregator>();
                if (aggregator == null || !aggregator.IsOfferedTo(
                        objectEntryPickUpActionValue,
                        PegaObjectInteractionParticipant.Npc))
                {
                    diagnostic = "The selected Pick Up action must be offered to NPC participants by its Object Entry aggregator before its configuration can enter Group Knowledge.";
                    return false;
                }

                if (parameters.ObjectEntryId != expectedObjectEntryId)
                {
                    diagnostic = "The selected Pick Up action's ObjectEntryId does not match this fact address.";
                    return false;
                }

                resolved = parameters;
                return true;
            }

            try
            {
                if (type == typeof(ObjectEntryId))
                {
                    if (!objectEntryId.TryGetObjectEntryId(out ObjectEntryId id))
                    {
                        diagnostic = "Select a valid Framework Object Entry identity.";
                        return false;
                    }

                    resolved = id;
                }
                else if (type == typeof(string)) resolved = stringValue ?? string.Empty;
                else if (type == typeof(byte))
                {
                    if (integerValue < byte.MinValue || integerValue > byte.MaxValue)
                    {
                        diagnostic = "Byte values must be between 0 and 255.";
                        return false;
                    }

                    resolved = (byte)integerValue;
                }
                else if (type == typeof(int)) resolved = integerValue;
                else if (type == typeof(long)) resolved = longValue;
                else if (type == typeof(float)) resolved = floatValue;
                else if (type == typeof(double)) resolved = doubleValue;
                else if (type == typeof(bool)) resolved = boolValue;
                else if (type == typeof(Vector3)) resolved = vector3Value;
                else if (type == typeof(PegaNpcPerceivedAreaPresence)) resolved = perceivedAreaPresenceValue;
                else if (type.IsEnum) resolved = Enum.ToObject(type, integerValue);
                return true;
            }
            catch (ArgumentException exception)
            {
                diagnostic = exception.Message;
                return false;
            }
        }

        private bool TryGetActorId(
            ActorOccurrenceReferenceBinding actorBinding,
            out object resolved,
            out bool pending,
            out string diagnostic)
        {
            resolved = null;
            pending = false;
            if (actorIdentitySource == PegaKnowledgeActorIdentitySource.ActorDeclaration)
            {
                if (actorDeclaration == null)
                {
                    diagnostic = "Select a Framework ActorDeclaration for the ActorId address.";
                    return false;
                }

                try
                {
                    ActorId actorId = actorDeclaration.ActorId;
                    if (!actorId.IsValid)
                    {
                        diagnostic = "The selected ActorDeclaration does not provide a valid ActorId.";
                        return false;
                    }

                    resolved = actorId;
                    diagnostic = string.Empty;
                    return true;
                }
                catch (ArgumentException exception)
                {
                    diagnostic = exception.Message;
                    return false;
                }
            }

            if (actorIdentitySource != PegaKnowledgeActorIdentitySource.CurrentPlayerActorFromNpcBinding)
            {
                diagnostic = "Select a supported Framework Actor identity source.";
                return false;
            }

            if (actorBinding == null)
            {
                diagnostic = "This NPC has no ActorOccurrenceReferenceBinding to resolve its current Player Actor.";
                return false;
            }

            if (!actorBinding.TryGetCurrent(out ActorOccurrenceResolution resolution))
            {
                pending = resolution.Status == ActorOccurrenceResolutionStatus.Unavailable ||
                          resolution.Status == ActorOccurrenceResolutionStatus.Stale;
                diagnostic = string.IsNullOrWhiteSpace(resolution.Diagnostic)
                    ? "The NPC's current Player Actor identity is not available yet."
                    : resolution.Diagnostic;
                return false;
            }

            if (!resolution.Occurrence.HasPlayerSlot || !resolution.Occurrence.ActorId.IsValid)
            {
                diagnostic = "The NPC binding did not resolve a Player Actor with a valid ActorId.";
                return false;
            }

            resolved = resolution.Occurrence.ActorId;
            diagnostic = string.Empty;
            return true;
        }
    }
}

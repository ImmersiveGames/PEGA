using System;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>
    /// Unity-serializable payload slots shared by fact definitions. The selected definition chooses the exact
    /// typed slot; adding another definition with already-supported address/value types needs no authoring class.
    /// </summary>
    [Serializable]
    public struct PEGAKnowledgeFactAuthoringPayload
    {
        [SerializeField, HideInInspector] private ActorDeclaration actorDeclaration;
        [SerializeField, HideInInspector] private string actorIdText;
        [SerializeField, HideInInspector] private ActorProfile actorProfile;
        [SerializeField, HideInInspector] private string actorProfileIdText;
        [SerializeField, HideInInspector] private ObjectEntryIdReference objectEntryId;
        [SerializeField, HideInInspector] private string stringValue;
        [SerializeField, HideInInspector] private int integerValue;
        [SerializeField, HideInInspector] private long longValue;
        [SerializeField, HideInInspector] private float floatValue;
        [SerializeField, HideInInspector] private double doubleValue;
        [SerializeField, HideInInspector] private bool boolValue;
        [SerializeField, HideInInspector] private Vector3 vector3Value;
        [SerializeField, HideInInspector] private PEGANpcPerceivedAreaPresence perceivedAreaPresenceValue;

        public static bool Supports(Type type) =>
            type == typeof(ActorId) ||
            type == typeof(ActorProfileId) ||
            type == typeof(ObjectEntryId) ||
            type == typeof(string) ||
            type == typeof(byte) ||
            type == typeof(int) ||
            type == typeof(long) ||
            type == typeof(float) ||
            type == typeof(double) ||
            type == typeof(bool) ||
            type == typeof(Vector3) ||
            type == typeof(PEGANpcPerceivedAreaPresence) ||
            IsSupportedEnum(type);

        private static bool IsSupportedEnum(Type type) =>
            type != null && type.IsEnum && Enum.GetUnderlyingType(type) == typeof(int);

        internal bool TryGetValue(Type type, out object resolved, out string diagnostic)
        {
            resolved = null;
            diagnostic = string.Empty;
            if (!Supports(type))
            {
                diagnostic = $"Data type '{type?.FullName ?? "<null>"}' has no Unity authoring payload format.";
                return false;
            }

            try
            {
                if (type == typeof(ActorId))
                {
                    resolved = ActorId.From(actorIdText);
                }
                else if (type == typeof(ActorProfileId))
                {
                    resolved = ActorProfileId.From(actorProfileIdText);
                }
                else if (type == typeof(ObjectEntryId))
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
                else if (type == typeof(PEGANpcPerceivedAreaPresence)) resolved = perceivedAreaPresenceValue;
                else if (type.IsEnum) resolved = Enum.ToObject(type, integerValue);
                return true;
            }
            catch (ArgumentException exception)
            {
                diagnostic = exception.Message;
                return false;
            }
        }
    }
}

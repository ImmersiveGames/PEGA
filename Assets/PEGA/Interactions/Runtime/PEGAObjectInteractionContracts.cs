using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using UnityEngine;
namespace PEGA.Interactions.Runtime
{
    public enum PegaObjectInteractionParticipant
    {
        Player = 1,
        Npc = 2
    }

    [System.Flags]
    public enum PegaObjectInteractionParticipantMask
    {
        None = 0,
        Player = 1,
        Npc = 2,
        Both = Player | Npc
    }

    /// <summary>A generic command to interact, without selecting a concrete object operation.</summary>
    public readonly struct PegaObjectInteractionIntent
    {
        private PegaObjectInteractionIntent(bool isRequested)
        {
            IsRequested = isRequested;
        }

        public bool IsRequested { get; }

        public static PegaObjectInteractionIntent None => default;

        public static PegaObjectInteractionIntent Request => new PegaObjectInteractionIntent(true);
    }

    public interface IPegaObjectInteractionIntentSource
    {
        bool TryGetIntent(out PegaObjectInteractionIntent intent);
    }

    /// <summary>Immutable snapshot of one Rob action component's authored configuration.</summary>
    public readonly struct PegaRobObjectEntryConfigurationSnapshot : System.IEquatable<PegaRobObjectEntryConfigurationSnapshot>
    {
        public PegaRobObjectEntryConfigurationSnapshot(
            ObjectEntryId objectEntryId,
            float interactionRangeMeters,
            float executionDurationSeconds)
        {
            ObjectEntryId = objectEntryId;
            InteractionRangeMeters = interactionRangeMeters;
            ExecutionDurationSeconds = executionDurationSeconds;
        }

        public ObjectEntryId ObjectEntryId { get; }
        public float InteractionRangeMeters { get; }
        public float ExecutionDurationSeconds { get; }

        public bool IsValid => ObjectEntryId.IsValid &&
            IsNonNegativeFinite(InteractionRangeMeters) &&
            IsNonNegativeFinite(ExecutionDurationSeconds);

        public bool Equals(PegaRobObjectEntryConfigurationSnapshot other) =>
            ObjectEntryId == other.ObjectEntryId &&
            InteractionRangeMeters == other.InteractionRangeMeters &&
            ExecutionDurationSeconds == other.ExecutionDurationSeconds;

        public override bool Equals(object obj) =>
            obj is PegaRobObjectEntryConfigurationSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ObjectEntryId.GetHashCode();
                hash = (hash * 397) ^ InteractionRangeMeters.GetHashCode();
                return (hash * 397) ^ ExecutionDurationSeconds.GetHashCode();
            }
        }

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum PegaObjectInteractionExecutionStatus
    {
        NotStarted = 0,
        Running = 10,
        Succeeded = 20,
        Failed = 30,
        Cancelled = 40
    }

    public interface IPegaObjectInteractionOccurrenceAuthority
    {
        bool TryValidateCurrentOccurrence(
            GameObject occurrenceRoot,
            ObjectEntryId objectEntryId,
            out string diagnostic);
    }

    /// <summary>Solicitação autorizada de um Actor contra a raiz exata de um Object Entry resolvido pelo owner.</summary>
    public readonly struct PegaObjectInteractionRequest
    {
        private PegaObjectInteractionRequest(
            PegaObjectInteractionParticipant participant,
            GameObject actorRoot,
            ActorId actorId,
            GameObject targetRoot,
            ObjectEntryId objectEntryId,
            IPegaObjectInteractionOccurrenceAuthority occurrenceAuthority)
        {
            Participant = participant;
            ActorRoot = actorRoot;
            ActorId = actorId;
            TargetRoot = targetRoot;
            ObjectEntryId = objectEntryId;
            OccurrenceAuthority = occurrenceAuthority;
        }

        public PegaObjectInteractionParticipant Participant { get; }
        public GameObject ActorRoot { get; }
        public ActorId ActorId { get; }
        public GameObject TargetRoot { get; }
        public ObjectEntryId ObjectEntryId { get; }
        public IPegaObjectInteractionOccurrenceAuthority OccurrenceAuthority { get; }

        public bool IsValid =>
            (Participant == PegaObjectInteractionParticipant.Player ||
             Participant == PegaObjectInteractionParticipant.Npc) &&
            ActorRoot != null && ActorRoot.activeInHierarchy && ActorId.IsValid &&
            TargetRoot != null && TargetRoot.activeInHierarchy && ObjectEntryId.IsValid &&
            IsOccurrenceAuthorityAlive(OccurrenceAuthority);

        public bool TryValidateCurrentAuthority(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!IsValid)
            {
                diagnostic = "Interaction requires an active Actor, a typed Object Entry identity and a current occurrence authority.";
                return false;
            }

            ActorDeclaration actorDeclaration = ActorRoot.GetComponent<ActorDeclaration>();
            ObjectEntryDeclaration objectEntryDeclaration = TargetRoot.GetComponent<ObjectEntryDeclaration>();
            if (actorDeclaration == null || objectEntryDeclaration == null ||
                !objectEntryDeclaration.TryGetObjectEntryId(out ObjectEntryId currentObjectEntryId) ||
                currentObjectEntryId != ObjectEntryId)
            {
                diagnostic = "The request no longer resolves its exact Actor and Object Entry declarations.";
                return false;
            }

            try
            {
                if (actorDeclaration.ActorId != ActorId)
                {
                    diagnostic = "The Actor identity changed while the interaction was active.";
                    return false;
                }
            }
            catch (System.ArgumentException exception)
            {
                diagnostic = exception.Message;
                return false;
            }

            if (!OccurrenceAuthority.TryValidateCurrentOccurrence(
                    TargetRoot,
                    ObjectEntryId,
                    out diagnostic))
            {
                return false;
            }

            return true;
        }

        private static bool IsOccurrenceAuthorityAlive(
            IPegaObjectInteractionOccurrenceAuthority occurrenceAuthority) =>
            occurrenceAuthority != null &&
            (!(occurrenceAuthority is UnityEngine.Object unityAuthority) || unityAuthority != null);

        public static bool TryCreate(
            PegaObjectInteractionParticipant participant,
            GameObject actorRoot,
            GameObject targetRoot,
            IPegaObjectInteractionOccurrenceAuthority occurrenceAuthority,
            out PegaObjectInteractionRequest request,
            out string diagnostic)
        {
            request = default;
            if ((participant != PegaObjectInteractionParticipant.Player &&
                 participant != PegaObjectInteractionParticipant.Npc) ||
                actorRoot == null || targetRoot == null || occurrenceAuthority == null)
            {
                diagnostic = "Interaction requires an explicit participant, Actor root, Object Entry root and occurrence authority.";
                return false;
            }

            ActorDeclaration actorDeclaration = actorRoot.GetComponent<ActorDeclaration>();
            ObjectEntryDeclaration objectEntryDeclaration = targetRoot.GetComponent<ObjectEntryDeclaration>();
            if (actorDeclaration == null || objectEntryDeclaration == null ||
                !objectEntryDeclaration.TryGetObjectEntryId(out ObjectEntryId objectEntryId))
            {
                diagnostic = "Interaction requires exact Framework Actor and Object Entry declarations on the supplied roots.";
                return false;
            }

            ActorId actorId;
            try
            {
                actorId = actorDeclaration.ActorId;
            }
            catch (System.ArgumentException exception)
            {
                diagnostic = exception.Message;
                return false;
            }

            request = new PegaObjectInteractionRequest(
                participant,
                actorRoot,
                actorId,
                targetRoot,
                objectEntryId,
                occurrenceAuthority);
            if (!request.TryValidateCurrentAuthority(out diagnostic))
            {
                request = default;
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }
    }
}

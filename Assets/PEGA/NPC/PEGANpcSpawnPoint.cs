using System.Collections.Generic;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    /// <summary>Registra o uso físico de uma entrada como conhecimento compartilhado da Gangue.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    [AddComponentMenu("PEGA/NPC/Spawn Point")]
    public sealed class PEGANpcSpawnPoint : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.SpawnPoint";
        private const float MinimumInwardSpeedMetersPerSecond = 0.1f;

        [SerializeField]
        [Tooltip("Identificador estável e único deste Spawn Point dentro da Gangue. Oriente o +Z local para o interior da entrada.")]
        private string stableSpawnPointId;

        [SerializeField]
        [Tooltip("Estado compartilhado da Gangue que conhece este Spawn Point após o uso.")]
        private PEGANpcGangKnowledgeState gangKnowledgeState;

        [System.NonSerialized]
        private ScopedLogger _log;

        [System.NonSerialized]
        private bool _loggerInitialized;

        [System.NonSerialized]
        private BoxCollider _trigger;

        [System.NonSerialized]
        private Rigidbody _triggerBody;

        [System.NonSerialized]
        private bool _configurationValid;

        [System.NonSerialized]
        private bool _acceptedEvidence;

        [System.NonSerialized]
        private Dictionary<CharacterController, ActorTriggerContact> _activeActorContacts =
            new Dictionary<CharacterController, ActorTriggerContact>();

        public string StableSpawnPointId => stableSpawnPointId != null ? stableSpawnPointId.Trim() : string.Empty;

        public PEGANpcGangKnowledgeState GangKnowledgeState => gangKnowledgeState;

        private void Reset()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        public bool TryInitialize(
            string spawnPointId,
            PEGANpcGangKnowledgeState knowledgeState,
            out string diagnostic)
        {
            if (_acceptedEvidence)
            {
                diagnostic = "A Spawn Point cannot change identity or Gang after accepted use evidence.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(spawnPointId) || knowledgeState == null)
            {
                diagnostic = "A stable Spawn Point id and Gang knowledge state are required.";
                return false;
            }

            stableSpawnPointId = spawnPointId.Trim();
            gangKnowledgeState = knowledgeState;
            _configurationValid = ValidateConfiguration(out diagnostic);
            _trigger ??= GetComponent<BoxCollider>();
            _triggerBody ??= GetComponent<Rigidbody>();
            if (_trigger == null || !_trigger.isTrigger || _triggerBody == null || !_triggerBody.isKinematic)
            {
                _configurationValid = false;
                diagnostic = "A trigger BoxCollider and a kinematic Rigidbody are required.";
            }

            if (_configurationValid)
            {
                enabled = true;
            }

            return _configurationValid;
        }

        private void Awake()
        {
            EnsureLoggerInitialized();
            _configurationValid = ValidateConfiguration(out string diagnostic);
            _trigger = GetComponent<BoxCollider>();
            _triggerBody = GetComponent<Rigidbody>();
            if (_trigger == null || !_trigger.isTrigger || _triggerBody == null || !_triggerBody.isKinematic)
            {
                _configurationValid = false;
                _log.Error(
                    "Spawn Point requires a trigger BoxCollider and a kinematic Rigidbody.",
                    LogFields.Field("spawnPointId", stableSpawnPointId ?? string.Empty),
                    LogFields.Field("hasBoxCollider", _trigger != null),
                    LogFields.Field("isTrigger", _trigger != null && _trigger.isTrigger),
                    LogFields.Field("hasRigidbody", _triggerBody != null),
                    LogFields.Field("isKinematic", _triggerBody != null && _triggerBody.isKinematic));
            }

            if (!_configurationValid)
            {
                _log.Error(
                    "Spawn Point configuration is invalid and physical use will not be recorded.",
                    LogFields.Field("spawnPointId", stableSpawnPointId ?? string.Empty),
                    LogFields.Field("diagnostic", diagnostic));
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_configurationValid || other == null)
            {
                return;
            }

            CharacterController characterController = other.GetComponentInParent<CharacterController>();
            if (characterController == null)
            {
                return;
            }

            ActorTriggerContact contact = GetOrCreateActorContact(characterController);
            if (!contact.Colliders.Add(other) || contact.DirectionWasClassified)
            {
                return;
            }

            Vector3 inward = transform.forward;
            inward.y = 0f;
            Vector3 velocity = characterController.velocity;
            velocity.y = 0f;
            if (inward.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float inwardSpeed = Vector3.Dot(velocity, inward.normalized);
            if (Mathf.Abs(inwardSpeed) < MinimumInwardSpeedMetersPerSecond)
            {
                return;
            }

            contact.DirectionWasClassified = true;

            GameObject actorRoot = characterController.gameObject;
            if (inwardSpeed < MinimumInwardSpeedMetersPerSecond)
            {
                gangKnowledgeState.LogRejectedSpawnPointUse(
                    stableSpawnPointId,
                    actorRoot,
                    "the CharacterController entered without sufficient velocity toward the Spawn Point interior");
                return;
            }

            bool accepted = gangKnowledgeState.TryRecordSpawnPointUse(
                this,
                stableSpawnPointId,
                transform.position,
                Time.timeAsDouble,
                actorRoot,
                out bool wasAdded,
                out string diagnostic);
            if (!accepted)
            {
                gangKnowledgeState.LogRejectedSpawnPointUse(stableSpawnPointId, actorRoot, diagnostic);
                return;
            }

            if (wasAdded)
            {
                _acceptedEvidence = true;
                EnsureLoggerInitialized();
                _log.Debug(
                    "NPC physically entered a Gang Spawn Point; the entry became a known potential escape.",
                    LogFields.Field("spawnPointId", stableSpawnPointId),
                    LogFields.Field("actorRoot", actorRoot.name),
                    LogFields.Field("gangKnowledgeState", gangKnowledgeState.name),
                    LogFields.Field("entryPositionX", transform.position.x),
                    LogFields.Field("entryPositionY", transform.position.y),
                    LogFields.Field("entryPositionZ", transform.position.z),
                    LogFields.Field("evidenceTimeSeconds", Time.timeAsDouble));
            }
            else
            {
                EnsureLoggerInitialized();
                _log.Debug(
                    "NPC reused a known Gang Spawn Point; no duplicate escape Target was added.",
                    LogFields.Field("spawnPointId", stableSpawnPointId),
                    LogFields.Field("actorRoot", actorRoot.name),
                    LogFields.Field("gangKnowledgeState", gangKnowledgeState.name));
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null)
            {
                return;
            }

            CharacterController characterController = other.GetComponentInParent<CharacterController>();
            if (characterController == null ||
                !_activeActorContacts.TryGetValue(characterController, out ActorTriggerContact contact))
            {
                return;
            }

            contact.Colliders.Remove(other);
            if (contact.Colliders.Count == 0)
            {
                _activeActorContacts.Remove(characterController);
            }
        }

        private void OnDisable()
        {
            _activeActorContacts.Clear();
        }

        private ActorTriggerContact GetOrCreateActorContact(CharacterController characterController)
        {
            if (!_activeActorContacts.TryGetValue(characterController, out ActorTriggerContact contact))
            {
                contact = new ActorTriggerContact();
                _activeActorContacts.Add(characterController, contact);
            }

            return contact;
        }

        private bool ValidateConfiguration(out string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(stableSpawnPointId))
            {
                diagnostic = "A stable Spawn Point id is required.";
                return false;
            }

            if (gangKnowledgeState == null)
            {
                diagnostic = "A Gang knowledge state is required.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PEGALogging.CreateScopedLogger<PEGANpcSpawnPoint>(LogCategory, gameObject.name);
            _loggerInitialized = true;
        }

        private sealed class ActorTriggerContact
        {
            public HashSet<Collider> Colliders { get; } = new HashSet<Collider>();

            public bool DirectionWasClassified { get; set; }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            if (trigger == null)
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(trigger.center, trigger.size);
            Vector3 inward = Vector3.forward;
            Gizmos.DrawLine(trigger.center, trigger.center + inward * 0.8f);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
#endif
    }
}

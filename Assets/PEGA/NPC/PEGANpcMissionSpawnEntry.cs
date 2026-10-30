using System;
using System.Collections;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.Actors;
using Immersive.Framework.PlayerParticipation;
using Immersive.Framework.RuntimeContent;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Actors;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.NPC
{
    internal enum PEGANpcMissionSpawnEntryOutcome
    {
        None = 0,
        Running = 10,
        Entered = 20,
        Failed = 30,
        Cancelled = 40
    }

    internal readonly struct PEGANpcMissionSpawnEntrySnapshot
    {
        public PEGANpcMissionSpawnEntrySnapshot(
            long runId,
            PEGANpcMissionSpawnEntryOutcome outcome,
            GameObject actorRoot,
            string actorId,
            bool gangMembershipConfirmedBeforeNavigation,
            bool navigationStarted,
            bool navigationSnapshotAvailable,
            PEGANpcNavigationSnapshot navigation,
            float physicalDisplacementMeters,
            bool spawnPointKnowledgeConfirmed,
            bool knowledgeConfirmedBeforePublication,
            bool occurrencePublished,
            bool perceptionEnabledAfterIngress,
            bool autonomyEnabledAfterIngress,
            string spawnPointId,
            string diagnostic)
        {
            RunId = runId;
            Outcome = outcome;
            ActorRoot = actorRoot;
            ActorId = actorId ?? string.Empty;
            GangMembershipConfirmedBeforeNavigation = gangMembershipConfirmedBeforeNavigation;
            NavigationStarted = navigationStarted;
            NavigationSnapshotAvailable = navigationSnapshotAvailable;
            Navigation = navigation;
            PhysicalDisplacementMeters = physicalDisplacementMeters;
            SpawnPointKnowledgeConfirmed = spawnPointKnowledgeConfirmed;
            KnowledgeConfirmedBeforePublication = knowledgeConfirmedBeforePublication;
            OccurrencePublished = occurrencePublished;
            PerceptionEnabledAfterIngress = perceptionEnabledAfterIngress;
            AutonomyEnabledAfterIngress = autonomyEnabledAfterIngress;
            SpawnPointId = spawnPointId ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public long RunId { get; }
        public PEGANpcMissionSpawnEntryOutcome Outcome { get; }
        public GameObject ActorRoot { get; }
        public string ActorId { get; }
        public bool GangMembershipConfirmedBeforeNavigation { get; }
        public bool NavigationStarted { get; }
        public bool NavigationSnapshotAvailable { get; }
        public PEGANpcNavigationSnapshot Navigation { get; }
        public float PhysicalDisplacementMeters { get; }
        public bool SpawnPointKnowledgeConfirmed { get; }
        public bool KnowledgeConfirmedBeforePublication { get; }
        public bool OccurrencePublished { get; }
        public bool PerceptionEnabledAfterIngress { get; }
        public bool AutonomyEnabledAfterIngress { get; }
        public string SpawnPointId { get; }
        public string Diagnostic { get; }

        public bool IsTerminal => Outcome == PEGANpcMissionSpawnEntryOutcome.Entered ||
                                  Outcome == PEGANpcMissionSpawnEntryOutcome.Failed ||
                                  Outcome == PEGANpcMissionSpawnEntryOutcome.Cancelled;
    }

    /// <summary>
    /// Executa uma solicitação explícita de ingresso feita pelo produtor de receita do Spawn Point.
    /// A entrada na Activity, por si só, não agenda nem cria larápios.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/NPC/Mission Spawn Point Ingress")]
    public sealed class PEGANpcMissionSpawnEntry : ActivityContentBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.NPC.MissionSpawnEntry";
        private const float ArrivalRadiusMeters = 0.5f;

        [Header("Gang Membership")]
        [SerializeField]
        [Tooltip("Estado compartilhado da Gangue que receberá o Actor antes do ingresso físico.")]
        private PEGANpcGangKnowledgeState gangKnowledgeState;

        [Header("Activity Player Session")]
        [SerializeField]
        [Tooltip("Observador da Player Session da Activity atual, fornecido aos NPCs criados durante o ingresso.")]
        private PlayerSessionObserver playerSessionObserver;

        [Header("Spawn Point")]
        [SerializeField]
        [Tooltip("Spawn Point físico que o Actor deve atravessar para ingressar na Mission.")]
        private PEGANpcSpawnPoint spawnPoint;

        [SerializeField]
        [Tooltip("Prefab do Actor NonPlayer com ActorId próprio e a composição de larápio do PEGA.")]
        private GameObject ingressActorPrefab;

        [SerializeField]
        [Tooltip("Posição authorada do Actor antes de cruzar o Spawn Point (m). Deve estar sobre o NavMesh.")]
        private Transform spawnSafe;

        [SerializeField]
        [Tooltip("Posição interna que encerra o deslocamento de ingresso após cruzar o Spawn Point (m). Deve estar sobre o NavMesh.")]
        private Transform missionInteriorDestination;

        [NonSerialized]
        private ScopedLogger _log;

        [NonSerialized]
        private bool _loggerInitialized;

        [NonSerialized]
        private bool _claimedForEntry;

        [NonSerialized]
        private long _runSequence;

        [NonSerialized]
        private long _currentRunId;

        [NonSerialized]
        private Coroutine _ingressRoutine;

        [NonSerialized]
        private GameObject _spawnedActor;

        [NonSerialized]
        private GameObject _stagingRoot;

        [NonSerialized]
        private ActorDeclaration _actorDeclaration;

        [NonSerialized]
        private ActorOccurrencePublication _actorPublication;

        [NonSerialized]
        private ActorOccurrencePublicationScope _publicationScope;

        [NonSerialized]
        private PEGANpcNavMeshMovementIntentSource _navigationSource;

        [NonSerialized]
        private IPEGANpcTargetObservationSource _targetObservationSource;

        [System.NonSerialized]
        private MonoBehaviour _targetObservationBehaviour;

        [NonSerialized]
        private PEGANpcAutonomy _autonomy;

        [NonSerialized]
        private ActorOccurrencePublicationToken _publicationToken;

        [NonSerialized]
        private RuntimeContentOwner _activityOwner;

        [NonSerialized]
        private Vector3 _physicalStartPosition;

        [NonSerialized]
        private float _physicalDisplacementMeters;

        [NonSerialized]
        private string _actorIdText = string.Empty;

        [NonSerialized]
        private string _diagnostic = string.Empty;

        [NonSerialized]
        private long _navigationRequestId;

        [NonSerialized]
        private PEGANpcNavigationSnapshot _navigationSnapshot;

        [NonSerialized]
        private bool _navigationSnapshotAvailable;

        [NonSerialized]
        private bool _gangMembershipConfirmedBeforeNavigation;

        [NonSerialized]
        private bool _navigationStarted;

        [NonSerialized]
        private bool _navigationRouteConfirmed;

        [NonSerialized]
        private bool _spawnPointKnowledgeConfirmed;

        [NonSerialized]
        private bool _knowledgeConfirmedBeforePublication;

        [NonSerialized]
        private bool _occurrencePublished;

        [NonSerialized]
        private bool _perceptionEnabledAfterIngress;

        [NonSerialized]
        private bool _autonomyEnabledAfterIngress;

        [NonSerialized]
        private PEGANpcMissionSpawnEntryOutcome _outcome;

        private void Reset()
        {
            if (gangKnowledgeState == null)
            {
                gangKnowledgeState = GetComponent<PEGANpcGangKnowledgeState>();
            }

            _publicationScope = GetComponent<ActorOccurrencePublicationScope>();
        }

        protected override void OnActivityContentEntered(ActivityContentLifecycleContext context)
        {
            EnsureLoggerInitialized();

            if (gangKnowledgeState == null)
            {
                _log.Error(
                    "Mission Activity content entered without its PEGA Gang knowledge state.",
                    LogFields.Field("activity", context.ActivityName));
                return;
            }

            if (!gangKnowledgeState.TryEnsureAuthorizedGroupsLive(
                    out int groupCount,
                    out int createdGroupCount,
                    out string groupDiagnostic))
            {
                _log.Error(
                    "Mission Activity could not materialize the Actor Groups authorized for Gang knowledge.",
                    LogFields.Field("activity", context.ActivityName),
                    LogFields.Field("diagnostic", groupDiagnostic));
                return;
            }

            if (context.ContentRoot == null)
            {
                _log.Error(
                    "Mission Activity could not register its authored Gang members.",
                    LogFields.Field("activity", context.ActivityName),
                    LogFields.Field("diagnostic", "The Framework did not provide an Activity content root."));
                return;
            }

            if (!gangKnowledgeState.TryRegisterSceneMembers(
                    context.ContentRoot.scene,
                    out int registeredMemberCount,
                    out string membershipDiagnostic))
            {
                _log.Error(
                    "Mission Activity could not register its authored Gang members.",
                    LogFields.Field("activity", context.ActivityName),
                    LogFields.Field("diagnostic", membershipDiagnostic));
                return;
            }

            _log.Debug(
                "Mission Activity content entered; authorized Actor Groups are live and authored Gang members are registered. Spawn Point ingress is still waiting for an explicit recipe request.",
                LogFields.Field("activity", context.ActivityName),
                LogFields.Field("groupCount", groupCount),
                LogFields.Field("createdGroupCount", createdGroupCount),
                LogFields.Field("registeredSceneMemberCount", registeredMemberCount),
                LogFields.Field("automaticIngressStarted", false));
        }

        /// <summary>
        /// Starts one physical ingress after the authoritative Spawn Point recipe requests it.
        /// The caller owns the Assault Active schedule; this component does not implement timers.
        /// </summary>
        public bool TryBeginScheduledIngress(out string diagnostic)
        {
            if (!IsActivityContentActive || !HasActivityContentContext ||
                !LastActivityContentContext.IsEntered)
            {
                diagnostic = "A scheduled ingress request requires this Mission Activity content to be entered.";
                EnsureLoggerInitialized();
                _log.Warning(
                    "Mission Spawn Point ingress request was rejected because its Activity content is not entered.",
                    LogFields.Field("diagnostic", diagnostic));
                return false;
            }

            if (_claimedForEntry)
            {
                EnsureLoggerInitialized();
                _log.Warning(
                    "Mission Spawn Point ingress request was rejected because this Activity entry already owns a run.",
                    LogFields.Field("runId", _currentRunId),
                    LogFields.Field("activity", LastActivityContentContext.ActivityName));
                diagnostic = "This Activity entry already owns an ingress run.";
                return false;
            }

            _claimedForEntry = true;
            _currentRunId = NextRunId();
            ResetRunState();

            if (!TryValidateConfiguration(LastActivityContentContext, out _diagnostic))
            {
                Finish(PEGANpcMissionSpawnEntryOutcome.Failed, _diagnostic);
                diagnostic = _diagnostic;
                return false;
            }

            _ingressRoutine = StartCoroutine(RunIngress());
            diagnostic = string.Empty;
            return true;
        }

        protected override void OnActivityContentExited(ActivityContentLifecycleContext context)
        {
            StopIngress(PEGANpcMissionSpawnEntryOutcome.Cancelled,
                "Mission Activity content exited before the ingress producer completed.");
            _claimedForEntry = false;
        }

        private void OnDisable()
        {
            if (_claimedForEntry && _outcome == PEGANpcMissionSpawnEntryOutcome.Running)
            {
                StopIngress(PEGANpcMissionSpawnEntryOutcome.Cancelled,
                    "Mission Spawn Point ingress component was disabled before completion.");
            }
            else if (_claimedForEntry && _outcome == PEGANpcMissionSpawnEntryOutcome.Entered)
            {
                ReleaseOwnedActor();
            }
        }

        private void OnDestroy()
        {
            ReleaseOwnedActor();
        }

        private IEnumerator RunIngress()
        {
            EnsureLoggerInitialized();
            LogState(
                "Mission NPC ingress started from its authored Spawn Safe toward the physical Spawn Point.");

            _stagingRoot = new GameObject("Runtime Mission Spawn Entry");
            _stagingRoot.SetActive(false);
            _stagingRoot.transform.SetParent(transform, false);
            _spawnedActor = Instantiate(
                ingressActorPrefab,
                spawnSafe.position,
                spawnSafe.rotation,
                _stagingRoot.transform);
            _spawnedActor.name = "Larapio - Mission Spawn Entry";

            _actorDeclaration = _spawnedActor.GetComponent<ActorDeclaration>();
            _actorPublication = _spawnedActor.GetComponent<ActorOccurrencePublication>();
            _navigationSource = _spawnedActor.GetComponent<PEGANpcNavMeshMovementIntentSource>();
            if (!TryFindTargetObservationSource(
                    _spawnedActor,
                    out _targetObservationSource,
                    out _targetObservationBehaviour,
                    out string targetObservationDiagnostic))
            {
                Finish(PEGANpcMissionSpawnEntryOutcome.Failed, targetObservationDiagnostic);
                yield break;
            }
            _autonomy = _spawnedActor.GetComponent<PEGANpcAutonomy>();

            _actorIdText = _actorDeclaration != null && _actorDeclaration.ActorId.IsValid
                ? _actorDeclaration.ActorId.StableText
                : string.Empty;

            _targetObservationBehaviour.enabled = false;

            if (_autonomy != null)
            {
                _autonomy.enabled = false;
            }

            if (!_targetObservationSource.TryBindActivityObserverBeforeActivation(
                    playerSessionObserver,
                    out string observerDiagnostic))
            {
                Finish(PEGANpcMissionSpawnEntryOutcome.Failed, observerDiagnostic);
                yield break;
            }

            if (!gangKnowledgeState.TryJoinMember(
                    _spawnedActor,
                    out PEGANpcGangMembership membership,
                    out string membershipDiagnostic))
            {
                Finish(PEGANpcMissionSpawnEntryOutcome.Failed, membershipDiagnostic);
                yield break;
            }

            _physicalStartPosition = _spawnedActor.transform.position;
            _stagingRoot.SetActive(true);
            _spawnedActor.transform.SetParent(transform, true);
            Destroy(_stagingRoot);
            _stagingRoot = null;

            bool isFrameworkMember = false;
            membershipDiagnostic = string.Empty;
            bool membershipQuerySucceeded = membership != null &&
                gangKnowledgeState.TryIsMember(
                    _spawnedActor,
                    out isFrameworkMember,
                    out membershipDiagnostic);
            _gangMembershipConfirmedBeforeNavigation = membershipQuerySucceeded && isFrameworkMember;
            if (!_gangMembershipConfirmedBeforeNavigation)
            {
                Finish(
                    PEGANpcMissionSpawnEntryOutcome.Failed,
                    string.IsNullOrWhiteSpace(membershipDiagnostic)
                        ? "The spawned Actor was not a Framework Actor Group member before navigation began."
                        : membershipDiagnostic);
                yield break;
            }

            if (_navigationSource == null || !_navigationSource.IsReady)
            {
                Finish(
                    PEGANpcMissionSpawnEntryOutcome.Failed,
                    "The spawned Actor does not have a ready shared PEGA NavMesh movement source.");
                yield break;
            }

            if (!_navigationSource.TryBeginNavigation(
                    missionInteriorDestination.position,
                    PEGAActorMovementSpeedRegime.Movement,
                    ArrivalRadiusMeters,
                    out _navigationRequestId,
                    out string navigationDiagnostic))
            {
                Finish(
                    PEGANpcMissionSpawnEntryOutcome.Failed,
                    string.IsNullOrWhiteSpace(navigationDiagnostic)
                        ? "The PEGA navigation source rejected the Mission ingress route."
                        : navigationDiagnostic);
                yield break;
            }

            _navigationStarted = true;
            LogState(
                "Mission NPC ingress navigation request started through the PEGA physical movement source.");

            _navigationRouteConfirmed =
                PlanarDistance(_physicalStartPosition, missionInteriorDestination.position) <= ArrivalRadiusMeters;

            while (isActiveAndEnabled && IsActivityContentActive)
            {
                if (_navigationRouteConfirmed && _spawnedActor != null &&
                    PlanarDistance(_spawnedActor.transform.position, missionInteriorDestination.position) <= ArrivalRadiusMeters)
                {
                    _navigationSource.FinishRequest(
                        _navigationRequestId,
                        PEGANpcNavigationOutcome.Success,
                        "The physical Actor reached the authored Mission interior destination.");
                }

                if (!_navigationSource.TryGetSnapshot(_navigationRequestId, out _navigationSnapshot))
                {
                    Finish(
                        PEGANpcMissionSpawnEntryOutcome.Failed,
                        "The active Mission ingress navigation request no longer has a readable snapshot.");
                    yield break;
                }

                _navigationSnapshotAvailable = true;
                if (_navigationSnapshot.Outcome == PEGANpcNavigationOutcome.Running &&
                    _navigationSnapshot.HasPath &&
                    _navigationSnapshot.PathStatus == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                {
                    _navigationRouteConfirmed = true;
                }

                if (_navigationSnapshot.Outcome == PEGANpcNavigationOutcome.Running &&
                    _navigationRouteConfirmed &&
                    _navigationSnapshot.ActualDistanceToTargetMeters <= ArrivalRadiusMeters)
                {
                    _navigationSource.FinishRequest(
                        _navigationRequestId,
                        PEGANpcNavigationOutcome.Success,
                        "The physical Actor reached the authored Mission interior destination.");
                    if (!_navigationSource.TryGetSnapshot(_navigationRequestId, out _navigationSnapshot))
                    {
                        Finish(
                            PEGANpcMissionSpawnEntryOutcome.Failed,
                            "The completed Mission ingress navigation request no longer has a readable snapshot.");
                        yield break;
                    }

                    _navigationSnapshotAvailable = true;
                }

                if (_navigationSnapshot.Outcome != PEGANpcNavigationOutcome.Running)
                {
                    break;
                }

                yield return null;
            }

            if (!isActiveAndEnabled || !IsActivityContentActive)
            {
                yield break;
            }

            _ingressRoutine = null;
            if (!_navigationSnapshotAvailable ||
                _navigationSnapshot.Outcome != PEGANpcNavigationOutcome.Success)
            {
                Finish(
                    PEGANpcMissionSpawnEntryOutcome.Failed,
                    _navigationSnapshotAvailable && !string.IsNullOrWhiteSpace(_navigationSnapshot.Diagnostic)
                        ? _navigationSnapshot.Diagnostic
                        : "Physical navigation did not complete the authored Mission ingress route.");
                yield break;
            }

            _spawnPointKnowledgeConfirmed = gangKnowledgeState.TryGetKnownEscapeTarget(
                _spawnedActor,
                spawnPoint.StableSpawnPointId,
                out PEGANpcPlanningTarget learnedTarget,
                out string knowledgeDiagnostic) &&
                learnedTarget.Kind == PEGANpcPlanningTargetKind.EscapeDestination &&
                learnedTarget.Provenance == PEGANpcPlanningTargetProvenance.KnowledgeState;
            _knowledgeConfirmedBeforePublication = _spawnPointKnowledgeConfirmed;
            if (!_spawnPointKnowledgeConfirmed)
            {
                Finish(
                    PEGANpcMissionSpawnEntryOutcome.Failed,
                    string.IsNullOrWhiteSpace(knowledgeDiagnostic)
                        ? "The Actor completed navigation, but the Gang has no Knowledge State Target for this physical Spawn Point."
                        : knowledgeDiagnostic);
                yield break;
            }

            if (_actorPublication == null || _publicationScope == null)
            {
                Finish(
                    PEGANpcMissionSpawnEntryOutcome.Failed,
                    "The spawned Actor publication or its exact Activity publication scope is unavailable.");
                yield break;
            }

            if (!_publicationScope.TryPublish(
                    _actorPublication,
                    _activityOwner,
                    out _publicationToken,
                    out string publicationDiagnostic))
            {
                Finish(
                    PEGANpcMissionSpawnEntryOutcome.Failed,
                    string.IsNullOrWhiteSpace(publicationDiagnostic)
                        ? "The Framework did not publish the entered NonPlayer Actor under the active Mission Activity owner."
                        : publicationDiagnostic);
                yield break;
            }

            _occurrencePublished = true;
            if (_targetObservationSource == null || _targetObservationBehaviour == null || _autonomy == null)
            {
                Finish(
                    PEGANpcMissionSpawnEntryOutcome.Failed,
                    "The entered Actor lost its target observation source or Autonomy component before activation.");
                yield break;
            }

            _targetObservationBehaviour.enabled = true;
            _perceptionEnabledAfterIngress = _targetObservationBehaviour.isActiveAndEnabled;
            _autonomy.enabled = true;
            _autonomyEnabledAfterIngress = _autonomy.isActiveAndEnabled;

            if (!_perceptionEnabledAfterIngress || !_autonomyEnabledAfterIngress)
            {
                Finish(
                    PEGANpcMissionSpawnEntryOutcome.Failed,
                    "The entered Actor occurrence was published, but target observation or Autonomy did not become active.");
                yield break;
            }

            _diagnostic = string.Empty;
            Finish(PEGANpcMissionSpawnEntryOutcome.Entered, string.Empty);
        }

        private bool TryValidateConfiguration(
            ActivityContentLifecycleContext context,
            out string diagnostic)
        {
            _publicationScope = GetComponent<ActorOccurrencePublicationScope>();
            if (context.Activity == null || !context.Activity.HasValidActivityId)
            {
                diagnostic = "A valid committed Mission Activity is required to own the spawned NonPlayer Actor.";
                return false;
            }

            if (gangKnowledgeState == null || spawnPoint == null ||
                spawnPoint.GangKnowledgeState != gangKnowledgeState ||
                !spawnPoint.isActiveAndEnabled)
            {
                diagnostic = "The Mission ingress requires an active Spawn Point bound to the same Gang knowledge state.";
                return false;
            }

            if (ingressActorPrefab == null || spawnSafe == null || missionInteriorDestination == null)
            {
                diagnostic = "The Mission ingress requires an Actor prefab, Spawn Safe and interior destination.";
                return false;
            }

            if (playerSessionObserver == null)
            {
                diagnostic = "The Mission ingress requires the current Activity Player Session observer for the spawned NPC's perception.";
                return false;
            }

            if (_publicationScope == null)
            {
                diagnostic = "The Activity content producer requires an ActorOccurrencePublicationScope on the same GameObject.";
                return false;
            }

            if (!TryValidateActorPrefab(ingressActorPrefab, out diagnostic))
            {
                return false;
            }

            try
            {
                _activityOwner = RuntimeContentOwner.Activity(
                    context.Activity.ActivityId.StableText,
                    context.Activity.ActivityName,
                    RuntimeDefinitionToken.FromUnityObject(context.Activity));
            }
            catch (Exception exception)
            {
                diagnostic = "The exact Activity owner could not be created: " + exception.Message;
                return false;
            }

            if (!_activityOwner.IsValid)
            {
                diagnostic = "The exact Mission Activity owner is invalid.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool TryFindTargetObservationSource(
            GameObject actorRoot,
            out IPEGANpcTargetObservationSource source,
            out MonoBehaviour behaviour,
            out string diagnostic)
        {
            source = null;
            behaviour = null;
            if (actorRoot == null)
            {
                diagnostic = "The target observation source owner is missing.";
                return false;
            }

            MonoBehaviour[] components = actorRoot.GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                if (!(components[index] is IPEGANpcTargetObservationSource candidate))
                {
                    continue;
                }

                if (source != null)
                {
                    diagnostic = "The Mission ingress Actor must compose exactly one target observation source.";
                    return false;
                }

                source = candidate;
            }

            if (source == null)
            {
                diagnostic = "The Mission ingress Actor has no target observation source to bind to the current Activity observer.";
                return false;
            }

            behaviour = source as MonoBehaviour;
            diagnostic = behaviour != null
                ? string.Empty
                : "The Mission ingress Actor target observation source must be a composed MonoBehaviour.";
            return behaviour != null;
        }

        private static bool TryValidateActorPrefab(GameObject prefab, out string diagnostic)
        {
            ActorDeclaration declaration = prefab.GetComponent<ActorDeclaration>();
            ActorOccurrencePublication publication = prefab.GetComponent<ActorOccurrencePublication>();
            PEGANpcKnowledgeState individualKnowledge = prefab.GetComponent<PEGANpcKnowledgeState>();
            PEGANpcGangMembership gangMembership = prefab.GetComponent<PEGANpcGangMembership>();
            PEGANpcNavMeshMovementIntentSource navigation =
                prefab.GetComponent<PEGANpcNavMeshMovementIntentSource>();
            bool hasTargetObservationSource = TryFindTargetObservationSource(
                prefab,
                out _,
                out _,
                out string targetObservationDiagnostic);
            PEGANpcAutonomy autonomy = prefab.GetComponent<PEGANpcAutonomy>();

            if (declaration == null || publication == null || individualKnowledge == null ||
                gangMembership == null || navigation == null || !hasTargetObservationSource || autonomy == null)
            {
                diagnostic = string.IsNullOrWhiteSpace(targetObservationDiagnostic)
                    ? "The ingress Actor prefab must provide its root ActorDeclaration, publication, individual Knowledge State, Gang Membership, navigation, a target observation source and Autonomy components."
                    : targetObservationDiagnostic;
                return false;
            }

            ActorId actorId;
            try
            {
                actorId = declaration.ActorId;
            }
            catch (Exception exception)
            {
                diagnostic = "The ingress Actor prefab has an invalid ActorId. " + exception.Message;
                return false;
            }

            if (!actorId.IsValid || declaration.ActorKind != ActorKind.NonPlayer)
            {
                diagnostic = "The ingress Actor prefab requires a valid NonPlayer ActorId.";
                return false;
            }

            Transform compositionMount = publication.CompositionMount;
            if (compositionMount != null &&
                compositionMount != publication.transform &&
                !compositionMount.IsChildOf(publication.transform))
            {
                diagnostic = "The ingress Actor publication composition mount must be the Actor root or one of its children.";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private void StopIngress(PEGANpcMissionSpawnEntryOutcome terminalOutcome, string diagnostic)
        {
            if (_ingressRoutine != null)
            {
                StopCoroutine(_ingressRoutine);
                _ingressRoutine = null;
            }

            if (_navigationRequestId > 0 && _navigationSource != null)
            {
                if (_navigationSource.TryGetSnapshot(_navigationRequestId, out _navigationSnapshot))
                {
                    _navigationSnapshotAvailable = true;
                }

                if (_navigationSnapshotAvailable &&
                    _navigationSnapshot.Outcome == PEGANpcNavigationOutcome.Running)
                {
                    _navigationSource.FinishRequest(
                        _navigationRequestId,
                        PEGANpcNavigationOutcome.Cancelled,
                        diagnostic);
                }
            }

            if (_currentRunId > 0 && _outcome == PEGANpcMissionSpawnEntryOutcome.Running)
            {
                Finish(terminalOutcome, diagnostic);
            }
            else
            {
                ReleaseOwnedActor();
            }
        }

        private void Finish(PEGANpcMissionSpawnEntryOutcome outcome, string diagnostic)
        {
            _outcome = outcome;
            _diagnostic = diagnostic ?? string.Empty;
            _ingressRoutine = null;
            CapturePhysicalDisplacement();
            CaptureTerminalNavigationSnapshot();
            WriteTerminalLog();

            if (outcome != PEGANpcMissionSpawnEntryOutcome.Entered)
            {
                ReleaseOwnedActor();
            }
        }

        private void CaptureTerminalNavigationSnapshot()
        {
            if (_navigationRequestId > 0 && _navigationSource != null &&
                _navigationSource.TryGetSnapshot(_navigationRequestId, out _navigationSnapshot))
            {
                _navigationSnapshotAvailable = true;
            }
        }

        private PEGANpcMissionSpawnEntrySnapshot BuildSnapshot()
        {
            float displacement = _spawnedActor != null
                ? PlanarDistance(_physicalStartPosition, _spawnedActor.transform.position)
                : _physicalDisplacementMeters;

            return new PEGANpcMissionSpawnEntrySnapshot(
                _currentRunId,
                _outcome,
                _spawnedActor,
                _actorIdText,
                _gangMembershipConfirmedBeforeNavigation,
                _navigationStarted,
                _navigationSnapshotAvailable,
                _navigationSnapshot,
                displacement,
                _spawnPointKnowledgeConfirmed,
                _knowledgeConfirmedBeforePublication,
                _occurrencePublished,
                _perceptionEnabledAfterIngress,
                _autonomyEnabledAfterIngress,
                spawnPoint != null ? spawnPoint.StableSpawnPointId : string.Empty,
                _diagnostic);
        }

        private void WriteTerminalLog()
        {
            EnsureLoggerInitialized();
            PEGANpcMissionSpawnEntrySnapshot snapshot = BuildSnapshot();
            if (_outcome == PEGANpcMissionSpawnEntryOutcome.Entered)
            {
                _log.Debug(
                    "Larapio physically entered Mission through its Spawn Point; Gang knowledge was confirmed before NonPlayer publication.",
                    LogFields.Field("runId", _currentRunId),
                    LogFields.Field("actorId", _actorIdText),
                    LogFields.Field("gangKnowledgeState", gangKnowledgeState != null ? gangKnowledgeState.name : "unavailable"),
                    LogFields.Field("spawnPointId", snapshot.SpawnPointId),
                    LogFields.Field("navigationOutcome", _navigationSnapshotAvailable ? _navigationSnapshot.Outcome : PEGANpcNavigationOutcome.Invalid),
                    LogFields.Field("routeLengthMeters", _navigationSnapshotAvailable ? _navigationSnapshot.RouteLengthMeters : 0f),
                    LogFields.Field("physicalDisplacementMeters", snapshot.PhysicalDisplacementMeters),
                    LogFields.Field("knownTargetKind", PEGANpcPlanningTargetKind.EscapeDestination),
                    LogFields.Field("knownTargetProvenance", PEGANpcPlanningTargetProvenance.KnowledgeState),
                    LogFields.Field("activityOwner", _activityOwner.StableText),
                    LogFields.Field("occurrencePublished", _occurrencePublished),
                    LogFields.Field("perceptionEnabled", _perceptionEnabledAfterIngress),
                    LogFields.Field("autonomyEnabled", _autonomyEnabledAfterIngress));
                _log.Info(
                    "Thief entered Mission through a physical route.",
                    LogFields.Field("actorRoot", _spawnedActor != null ? _spawnedActor.name : "Unavailable"));
            }
            else if (_outcome == PEGANpcMissionSpawnEntryOutcome.Cancelled)
            {
                _log.Warning(
                    "Mission ingress was cancelled and its unpublished Actor was released.",
                    LogFields.Field("runId", _currentRunId),
                    LogFields.Field("actorId", _actorIdText),
                    LogFields.Field("spawnPointId", snapshot.SpawnPointId),
                    LogFields.Field("navigationRequestStarted", _navigationStarted),
                    LogFields.Field("navigationOutcome", _navigationSnapshotAvailable ? _navigationSnapshot.Outcome : PEGANpcNavigationOutcome.Invalid),
                    LogFields.Field("routeLengthMeters", _navigationSnapshotAvailable ? _navigationSnapshot.RouteLengthMeters : 0f),
                    LogFields.Field("physicalDisplacementMeters", snapshot.PhysicalDisplacementMeters),
                    LogFields.Field("diagnostic", _diagnostic));
            }
            else
            {
                _log.Error(
                    "Larapio Mission ingress did not complete; the runtime Actor was not left active.",
                    LogFields.Field("runId", _currentRunId),
                    LogFields.Field("outcome", _outcome),
                    LogFields.Field("actorId", _actorIdText),
                    LogFields.Field("spawnPointId", snapshot.SpawnPointId),
                    LogFields.Field("navigationRequestStarted", _navigationStarted),
                    LogFields.Field("navigationOutcome", _navigationSnapshotAvailable ? _navigationSnapshot.Outcome : PEGANpcNavigationOutcome.Invalid),
                    LogFields.Field("routeLengthMeters", _navigationSnapshotAvailable ? _navigationSnapshot.RouteLengthMeters : 0f),
                    LogFields.Field("physicalDisplacementMeters", snapshot.PhysicalDisplacementMeters),
                    LogFields.Field("spawnPointKnowledgeConfirmed", _spawnPointKnowledgeConfirmed),
                    LogFields.Field("occurrencePublished", _occurrencePublished),
                    LogFields.Field("diagnostic", _diagnostic));
            }
        }

        private void LogState(string message)
        {
            EnsureLoggerInitialized();
            _log.Debug(
                message,
                LogFields.Field("runId", _currentRunId),
                LogFields.Field("activityOwner", _activityOwner.IsValid ? _activityOwner.StableText : "unavailable"),
                LogFields.Field("spawnPointId", spawnPoint != null ? spawnPoint.StableSpawnPointId : string.Empty),
                LogFields.Field("safeSpawnX", spawnSafe != null ? spawnSafe.position.x : 0f),
                LogFields.Field("safeSpawnY", spawnSafe != null ? spawnSafe.position.y : 0f),
                LogFields.Field("safeSpawnZ", spawnSafe != null ? spawnSafe.position.z : 0f),
                LogFields.Field("interiorTargetX", missionInteriorDestination != null ? missionInteriorDestination.position.x : 0f),
                LogFields.Field("interiorTargetY", missionInteriorDestination != null ? missionInteriorDestination.position.y : 0f),
                LogFields.Field("interiorTargetZ", missionInteriorDestination != null ? missionInteriorDestination.position.z : 0f));
        }

        private void ReleaseOwnedActor()
        {
            CapturePhysicalDisplacement();
            bool wasPublished = _publicationToken.IsValid;
            if (_publicationToken.IsValid && _actorPublication != null && _publicationScope != null)
            {
                if (!_publicationScope.TryUnpublish(
                        _actorPublication,
                        _publicationToken,
                        out string diagnostic))
                {
                    EnsureLoggerInitialized();
                    _log.Error(
                        "Mission ingress could not release its exact NonPlayer publication token.",
                        LogFields.Field("actorId", _actorIdText),
                        LogFields.Field("activityOwner", _activityOwner.StableText),
                        LogFields.Field("diagnostic", diagnostic));
                }
            }

            _publicationToken = default;
            _occurrencePublished = false;
            if (_spawnedActor != null)
            {
                EnsureLoggerInitialized();
                _log.Debug(
                    "Mission ingress released the runtime Actor owned by this Activity contribution.",
                    LogFields.Field("runId", _currentRunId),
                    LogFields.Field("actorId", _actorIdText),
                    LogFields.Field("activityOwner", _activityOwner.IsValid ? _activityOwner.StableText : "unavailable"),
                    LogFields.Field("wasPublished", wasPublished),
                    LogFields.Field("physicalDisplacementMeters", _physicalDisplacementMeters));
                Destroy(_spawnedActor);
            }

            if (_stagingRoot != null)
            {
                Destroy(_stagingRoot);
            }

            _spawnedActor = null;
            _stagingRoot = null;
            _actorDeclaration = null;
            _actorPublication = null;
            _navigationSource = null;
            _targetObservationSource = null;
            _targetObservationBehaviour = null;
            _autonomy = null;
        }

        private void ResetRunState()
        {
            _outcome = PEGANpcMissionSpawnEntryOutcome.Running;
            _navigationRequestId = 0;
            _navigationSnapshot = default;
            _navigationSnapshotAvailable = false;
            _gangMembershipConfirmedBeforeNavigation = false;
            _navigationStarted = false;
            _navigationRouteConfirmed = false;
            _spawnPointKnowledgeConfirmed = false;
            _knowledgeConfirmedBeforePublication = false;
            _occurrencePublished = false;
            _perceptionEnabledAfterIngress = false;
            _autonomyEnabledAfterIngress = false;
            _activityOwner = default;
            _physicalStartPosition = Vector3.zero;
            _physicalDisplacementMeters = 0f;
            _actorIdText = string.Empty;
            _diagnostic = string.Empty;
            _publicationToken = default;
        }

        private long NextRunId()
        {
            _runSequence++;
            if (_runSequence <= 0)
            {
                _runSequence = 1;
            }

            return _runSequence;
        }

        private void EnsureLoggerInitialized()
        {
            if (_loggerInitialized)
            {
                return;
            }

            _log = PEGALogging.CreateScopedLogger<PEGANpcMissionSpawnEntry>(
                LogCategory,
                gameObject.name);
            _loggerInitialized = true;
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private void CapturePhysicalDisplacement()
        {
            if (_spawnedActor != null)
            {
                _physicalDisplacementMeters = PlanarDistance(
                    _physicalStartPosition,
                    _spawnedActor.transform.position);
            }
        }
    }
}

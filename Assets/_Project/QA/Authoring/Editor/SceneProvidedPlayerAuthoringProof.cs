using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Immersive.Framework.Actors;
using Immersive.Framework.Authoring;
using Immersive.Framework.Camera;
using Immersive.Framework.CameraAuthoring;
using Immersive.Framework.Editor.PlayerParticipation;
using Immersive.Framework.PlayerParticipation;
using Immersive.Framework.UnityInput;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Immersive.PEGA.AuthoringProof
{
    /// <summary>
    /// Isolated, repeatable authoring proof for the installed Scene-Provided Player contracts.
    /// This command never changes SampleScene, application assets, package files, or Build Settings.
    /// </summary>
    internal static class SceneProvidedPlayerAuthoringProof
    {
        private const string MenuPath =
            "Tools/Immersive/QA/Build Scene-Provided Player Proof";

        private const string ProofFolder =
            "Assets/_Project/QA/Authoring";

        private const string ProofScenePath =
            ProofFolder + "/SceneProvidedPlayerAuthoringProof.unity";

        private const string ReportPath =
            ProofFolder + "/SceneProvidedPlayerAuthoringProof.json";

        private const string FrameworkPackageName =
            "com.immersive.framework";

        private const string FrameworkVersion =
            "1.1.0-preview.5";

        private const string GameApplicationPath =
            "Assets/_Project/Settings/ImmersiveFramework/GameApplication.asset";

        private const string SessionProfilePath =
            "Assets/_Project/Players/PlayerSessionProfile_PEGA.asset";

        private const string SlotProfilePath =
            "Assets/_Project/Players/PlayerSlotProfileP1.asset";

        private const string ActorProfilePath =
            "Assets/_Project/Players/PlayerDefaultActorProfile.asset";

        private const string InputActionsPath =
            "Assets/InputSystem_Actions.inputactions";

        private const string CameraAssignmentPath =
            "Assets/_Project/Cameras/SessionCameraAssignment_PlayerP1.asset";

        private const string CameraOutputPrefabPath =
            "Assets/_Project/Cameras/CameraDefault/CameraOutput_Main.prefab";

        [MenuItem(MenuPath, false, 100)]
        private static void BuildProof()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                Debug.LogError(
                    "Scene-Provided Player proof requires the Editor to be idle outside Play Mode.");
                return;
            }

            Scene originalActiveScene = SceneManager.GetActiveScene();
            UnityEngine.Object[] originalSelection = Selection.objects;
            List<LoadedSceneState> originalScenes = CaptureLoadedScenes();
            Scene proofScene = default;
            bool sceneWasLoadedBeforeCommand = false;
            bool sceneWasCreatedInMemory = false;
            bool sceneWasSaved = false;
            int undoGroup = -1;

            try
            {
                ProofConfiguration configuration = LoadAndValidateSharedAssets();
                ProofReport report = LoadOrCreateReport();
                report.currentValidators = configuration.preflightChecks;
                bool firstExecution = report.runCount == 0;
                string currentBranch = ReadGitValue("branch --show-current");
                string currentHead = ReadGitValue("rev-parse HEAD");
                if (!firstExecution &&
                    (report.gitBranch != currentBranch || report.gitHead != currentHead))
                {
                    throw new InvalidOperationException(
                        "Branch or HEAD changed since the first proof run. Start a new isolated proof before continuing.");
                }
                string proofSceneAbsolutePath = ToAbsolutePath(ProofScenePath);
                bool proofSceneExists = File.Exists(proofSceneAbsolutePath);

                if (firstExecution && proofSceneExists)
                {
                    throw new InvalidOperationException(
                        "The proof scene exists without a completed proof report. It will not be adopted or overwritten.");
                }

                if (!firstExecution && !proofSceneExists)
                {
                    throw new InvalidOperationException(
                        "The proof report exists but its scene is missing. Restore the proof scene before running again.");
                }

                if (!firstExecution)
                {
                    string currentSceneHash = ComputeFileSha256(proofSceneAbsolutePath);
                    if (!string.Equals(
                            currentSceneHash,
                            report.lastSnapshot.sceneFileSha256,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "The proof scene changed after the previous recorded run. Preserve and inspect it before another execution.");
                    }
                }

                EnsureProofFolder();
                proofScene = GetOrOpenProofScene(
                    proofSceneExists,
                    out sceneWasLoadedBeforeCommand,
                    out sceneWasCreatedInMemory);

                if (proofSceneExists && proofScene.isDirty)
                {
                    throw new InvalidOperationException(
                        "The already-open proof scene has unsaved edits. Save or preserve those edits before running this command.");
                }

                bool compositionWasAbsent =
                    CountInScene<SceneProvidedLocalPlayerAuthoring>(proofScene) == 0;

                if (firstExecution &&
                    (proofScene.rootCount != 0 || HasAnyPlayerCompositionComponent(proofScene)))
                {
                    throw new InvalidOperationException(
                        "The new proof scene is not empty. No composition will be added to an unrecognized scene.");
                }

                if (firstExecution)
                {
                    report.initialComposition = CaptureInitialComposition(proofScene);
                }

                if (!firstExecution && compositionWasAbsent)
                {
                    throw new InvalidOperationException(
                        "The recorded proof scene no longer contains its Scene-Provided Local Player.");
                }

                if (CountInScene<SceneProvidedLocalPlayerAuthoring>(proofScene) > 1)
                {
                    throw new InvalidOperationException(
                        "The proof scene contains multiple Scene-Provided Player authorings; identity is ambiguous.");
                }

                if (!compositionWasAbsent)
                {
                    ValidateExistingProofComposition(proofScene, configuration);
                }

                if (!SceneManager.SetActiveScene(proofScene))
                {
                    throw new InvalidOperationException(
                        "Unity could not make the isolated proof scene active for the official creator API.");
                }

                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Build Scene-Provided Player Authoring Proof");

                SceneProvidedLocalPlayerAuthoring authoring =
                    compositionWasAbsent
                        ? CreateProofComposition(configuration)
                        : GetSingleInScene<SceneProvidedLocalPlayerAuthoring>(proofScene);

                ValidatePlayerComposition(proofScene, authoring, configuration, report);

                if (!HasEquivalentLoadedSceneStates(originalScenes, proofScene))
                {
                    throw new InvalidOperationException(
                        "An unrelated loaded scene changed dirty state during the proof. The proof scene will not be saved.");
                }

                EditorSceneManager.MarkSceneDirty(proofScene);
                if (!EditorSceneManager.SaveScene(proofScene, ProofScenePath))
                {
                    throw new InvalidOperationException(
                        "Unity did not save the isolated proof scene.");
                }

                sceneWasSaved = true;
                ProofSnapshot snapshot = CaptureSnapshot(proofScene);
                string previousSceneHash =
                    firstExecution ? string.Empty : report.lastSnapshot.sceneFileSha256;

                report.unityVersion = Application.unityVersion;
                report.frameworkVersion = configuration.frameworkVersion;
                report.gitBranch = currentBranch;
                report.gitHead = currentHead;
                report.proofScenePath = ProofScenePath;
                report.runCount++;
                report.lastRunUtc = DateTime.UtcNow.ToString("O");
                report.lastRunOutcome = "STRUCTURAL_PASS";
                report.lastSnapshot = snapshot;
                report.lastValidators = report.currentValidators.ToArray();
                report.currentValidators = Array.Empty<ValidatorResult>();

                if (firstExecution)
                {
                    report.compositionInitiallyAbsent = compositionWasAbsent;
                    report.firstSnapshot = snapshot;
                    report.secondExecutionEquivalent = false;
                    report.idempotenceVerified = false;
                }
                else if (report.runCount == 2)
                {
                    report.secondExecutionEquivalent =
                        snapshot.StructureEquals(report.firstSnapshot) &&
                        string.Equals(
                            snapshot.sceneFileSha256,
                            previousSceneHash,
                            StringComparison.Ordinal);
                    report.idempotenceVerified = report.secondExecutionEquivalent;
                }
                else
                {
                    report.secondExecutionEquivalent =
                        report.secondExecutionEquivalent &&
                        snapshot.StructureEquals(report.firstSnapshot) &&
                        string.Equals(
                            snapshot.sceneFileSha256,
                            previousSceneHash,
                            StringComparison.Ordinal);
                    report.idempotenceVerified = report.secondExecutionEquivalent;
                }

                WriteReport(report);

                Debug.Log(
                    $"Scene-Provided Player proof run {report.runCount} completed. " +
                    $"Scene='{ProofScenePath}', validators='{report.lastValidators.Length}', " +
                    $"secondExecutionEquivalent='{report.secondExecutionEquivalent}', " +
                    $"idempotenceVerified='{report.idempotenceVerified}', " +
                    $"sceneSha256='{snapshot.sceneFileSha256}'.");
            }
            catch (Exception exception)
            {
                if (!sceneWasSaved && undoGroup >= 0)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                }

                Debug.LogError(
                    "Scene-Provided Player authoring proof stopped safely. " +
                    exception.GetType().Name + ": " + exception.Message);

                if (sceneWasCreatedInMemory && !sceneWasSaved && proofScene.IsValid())
                {
                    EditorSceneManager.CloseScene(proofScene, true);
                }
            }
            finally
            {
                if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(originalActiveScene);
                }

                Selection.objects = originalSelection;
                RestoreLoadedSceneSet(originalScenes, proofScene, sceneWasLoadedBeforeCommand);
            }
        }

        private static ProofConfiguration LoadAndValidateSharedAssets()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string lockPath = Path.Combine(projectRoot, "Packages", "packages-lock.json");
            if (!File.Exists(lockPath))
            {
                throw new FileNotFoundException("Packages/packages-lock.json is required for this proof.", lockPath);
            }

            string lockText = File.ReadAllText(lockPath);
            string versionPattern =
                "\"" + FrameworkPackageName + "\"\\s*:\\s*\\{[^}]*\"version\"\\s*:\\s*\"([^\"]+)\"";
            System.Text.RegularExpressions.Match versionMatch =
                System.Text.RegularExpressions.Regex.Match(lockText, versionPattern);
            if (!versionMatch.Success ||
                !string.Equals(versionMatch.Groups[1].Value, FrameworkVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Resolved Framework version must be '{FrameworkVersion}'. Lock entry was not confirmed.");
            }

            UnityEditor.PackageManager.PackageInfo resolvedFramework =
                UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .FirstOrDefault(package => package.name == FrameworkPackageName);
            if (resolvedFramework == null || resolvedFramework.version != FrameworkVersion)
            {
                throw new InvalidOperationException(
                    $"Package Manager reports Framework '{(resolvedFramework == null ? "missing" : resolvedFramework.version)}', expected '{FrameworkVersion}'.");
            }

            var preflightChecks = new List<ValidatorResult>
            {
                CreateValidator("Resolved Framework package", true, FrameworkVersion, false)
            };

            GameApplicationAsset application =
                LoadRequiredAsset<GameApplicationAsset>(GameApplicationPath);
            PlayerSessionProfile sessionProfile =
                LoadRequiredAsset<PlayerSessionProfile>(SessionProfilePath);
            PlayerSlotProfile slotProfile =
                LoadRequiredAsset<PlayerSlotProfile>(SlotProfilePath);
            ActorProfile actorProfile =
                LoadRequiredAsset<ActorProfile>(ActorProfilePath);
            InputActionAsset inputActions =
                LoadRequiredAsset<InputActionAsset>(InputActionsPath);
            SessionCameraAssignmentAsset cameraAssignment =
                LoadRequiredAsset<SessionCameraAssignmentAsset>(CameraAssignmentPath);
            GameObject cameraOutputPrefab =
                LoadRequiredAsset<GameObject>(CameraOutputPrefabPath);

            if (!application.PlayerSessionEnabled ||
                application.DefaultPlayerSessionProfile != sessionProfile)
            {
                throw new InvalidOperationException(
                    "GameApplication does not enable or reference the expected Player Session Profile.");
            }

            if (!sessionProfile.TryValidate(out string sessionIssue))
            {
                throw new InvalidOperationException("Player Session Profile is invalid: " + sessionIssue);
            }

            preflightChecks.Add(CreateValidator(
                "GameApplication and Player Session Profile",
                true,
                "Application references this enabled, structurally valid Session Profile.",
                false));

            if (sessionProfile.HostProvisioning != PlayerHostProvisioningMode.SceneProvided)
            {
                throw new InvalidOperationException(
                    "The Player Session Profile must declare SceneProvided host provisioning for this proof.");
            }

            if (sessionProfile.SupportedSlotCount != 1 ||
                sessionProfile.SupportedSlots[0] != slotProfile)
            {
                throw new InvalidOperationException(
                    "The proof requires exactly the configured P1 Slot in the Player Session Profile.");
            }

            if (!slotProfile.TryGetPlayerSlotId(out _, out string slotIssue))
            {
                throw new InvalidOperationException(
                    "Player Slot is invalid: " + slotIssue);
            }

            if (slotProfile.DefaultActorProfile != actorProfile ||
                actorProfile.ActorKind != ActorKind.Player ||
                actorProfile.ActorRole != ActorRole.Protagonist)
            {
                throw new InvalidOperationException(
                    "Player Slot does not reference the expected Player Protagonist Actor Profile.");
            }

            if (!actorProfile.TryGetActorProfileId(out _, out string actorIssue))
            {
                throw new InvalidOperationException("Actor Profile is invalid: " + actorIssue);
            }

            preflightChecks.Add(CreateValidator(
                "Player Slot and Actor Profile",
                true,
                "P1 is the sole supported Slot and references a Player Protagonist Actor Profile.",
                false));

            InputActionMap playerActionMap = inputActions.FindActionMap("Player", false);
            if (playerActionMap == null ||
                inputActions.actionMaps.Count(map => map != null && map.name == "Player") != 1)
            {
                throw new InvalidOperationException(
                    "The existing Input Actions asset must contain exactly one 'Player' map for authoring selection.");
            }

            preflightChecks.Add(CreateValidator(
                "Input Actions authoring map",
                true,
                "The Player Action Map is selected by name only during authoring; runtime resolution uses its GUID.",
                false));

            if (!cameraAssignment.TryBuild(out Immersive.Framework.Camera.SessionCameraAssignment builtAssignment, out string assignmentIssue))
            {
                throw new InvalidOperationException("Session Camera Assignment is invalid: " + assignmentIssue);
            }

            if (cameraAssignment.OutputDefinitions.Count != 1)
            {
                throw new InvalidOperationException(
                    "The proof requires exactly one Output Definition in the Session Camera Assignment.");
            }

            CameraOutputDefinition assignedOutput = cameraAssignment.OutputDefinitions[0];

            if (builtAssignment.OccurrenceMode != CameraOccurrenceMode.IndividualPerPlayer ||
                builtAssignment.MembershipPolicy != CameraMembershipPolicy.ExplicitPlayerSlots ||
                builtAssignment.TargetPolicy != CameraTargetPolicy.MemberActorTargets ||
                !builtAssignment.MemberSlots.Contains(slotProfile.PlayerSlotId) ||
                builtAssignment.MemberOutputs.Count != 1 ||
                builtAssignment.MemberOutputs[0].PlayerSlotId != slotProfile.PlayerSlotId ||
                builtAssignment.MemberOutputs[0].OutputId != assignedOutput.OutputId ||
                !application.StartupCameraAssignments.Contains(cameraAssignment))
            {
                throw new InvalidOperationException(
                    "The existing Camera Assignment or GameApplication mapping does not match P1 individual Actor Subject targeting.");
            }

            if (application.CameraSession == null)
            {
                throw new InvalidOperationException("GameApplication Camera Session configuration is missing.");
            }

            if (!application.CameraSession.TryValidate(out string cameraSessionIssue))
            {
                throw new InvalidOperationException("GameApplication Camera Session is invalid: " + cameraSessionIssue);
            }

            if (!application.CameraSession.OutputPrefabs.Contains(cameraOutputPrefab))
            {
                throw new InvalidOperationException(
                    "GameApplication Camera Session does not own the expected physical Output prefab.");
            }

            ValidateCameraOutputPrefab(cameraOutputPrefab, assignedOutput);
            preflightChecks.Add(CreateValidator(
                "Session Camera Assignment and Camera Output",
                true,
                "Individual P1 Actor targeting, rig/output mapping, physical Output definition, and fallback reference are valid.",
                false));

            return new ProofConfiguration(
                application,
                sessionProfile,
                slotProfile,
                actorProfile,
                inputActions,
                playerActionMap,
                cameraAssignment,
                cameraOutputPrefab,
                assignedOutput,
                versionMatch.Groups[1].Value,
                preflightChecks.ToArray());
        }

        private static void ValidateCameraOutputPrefab(
            GameObject outputPrefab,
            CameraOutputDefinition expectedDefinition)
        {
            if (expectedDefinition == null)
            {
                throw new InvalidOperationException(
                    "The Session Camera Assignment must contain exactly one Output Definition.");
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(CameraOutputPrefabPath);
            try
            {
                CameraOutputAuthoring[] outputs =
                    prefabContents.GetComponentsInChildren<CameraOutputAuthoring>(true);
                if (outputs.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected one CameraOutputAuthoring; found {outputs.Length}.");
                }

                if (outputs[0].OutputDefinition != expectedDefinition ||
                    outputs[0].FallbackCameraRig == null)
                {
                    throw new InvalidOperationException(
                        "Camera Output prefab does not reference the assigned Output Definition and an explicit fallback Rig.");
                }

                if (!outputs[0].TryValidateDefinition(out string issue))
                {
                    throw new InvalidOperationException("Camera Output definition is invalid: " + issue);
                }

                if (outputPrefab.GetComponentInChildren<CameraOutputAuthoring>(true) == null)
                {
                    throw new InvalidOperationException(
                        "The referenced Output prefab has no CameraOutputAuthoring component.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static SceneProvidedLocalPlayerAuthoring CreateProofComposition(
            ProofConfiguration configuration)
        {
            SceneProvidedLocalPlayerAuthoring authoring =
                SceneProvidedLocalPlayerCreator.Create();
            if (authoring == null)
            {
                throw new InvalidOperationException(
                    "The installed Framework Scene-Provided creator returned no authoring component.");
            }

            GameObject hostRoot = authoring.gameObject;
            PlayerInput playerInput = GetExactlyOneComponent<PlayerInput>(hostRoot);
            LocalPlayerHostAuthoring host = GetExactlyOneComponent<LocalPlayerHostAuthoring>(hostRoot);
            UnityPlayerInputGateAdapter inputGate =
                GetExactlyOneComponent<UnityPlayerInputGateAdapter>(hostRoot);

            ConfigureReference(authoring, "localPlayerHost", host);
            ConfigureReference(authoring, "playerSlotProfile", configuration.slotProfile);
            ConfigureReference(authoring, "actorProfile", configuration.actorProfile);
            ConfigurePlayerInput(playerInput, configuration.inputActions);
            ConfigureInputGate(inputGate, playerInput, configuration.inputActions, configuration.playerActionMap);

            Transform actorMount = host.ActorMount;
            if (actorMount == null || actorMount.parent != host.transform || actorMount.childCount != 0)
            {
                throw new InvalidOperationException(
                    "The official creator did not provide one empty direct Actor Mount.");
            }

            GameObject actorRoot = CreateChild(actorMount, "Proof Player Actor");
            PlayerActorDeclaration declaration = Undo.AddComponent<PlayerActorDeclaration>(actorRoot);
            PlayerActorRuntimeHost runtimeHost = Undo.AddComponent<PlayerActorRuntimeHost>(actorRoot);
            ActorCameraSubjectAuthoring cameraSubject =
                Undo.AddComponent<ActorCameraSubjectAuthoring>(actorRoot);
            Transform observation = CreateChild(actorRoot.transform, "Observation Transform").transform;

            ConfigureReference(runtimeHost, "playerActorDeclaration", declaration);
            ConfigureReference(runtimeHost, "presentationMount", null);
            ConfigureReference(cameraSubject, "observationTransform", observation);

            if (!string.IsNullOrWhiteSpace(
                    new SerializedObject(declaration).FindProperty("actorId").stringValue))
            {
                throw new InvalidOperationException(
                    "The proof Actor declaration unexpectedly contains a runtime Actor identity.");
            }

            return authoring;
        }

        private static void ValidateExistingProofComposition(
            Scene scene,
            ProofConfiguration configuration)
        {
            SceneProvidedLocalPlayerAuthoring authoring =
                GetSingleInScene<SceneProvidedLocalPlayerAuthoring>(scene);
            GameObject root = authoring.gameObject;
            LocalPlayerHostAuthoring host = GetExactlyOneComponent<LocalPlayerHostAuthoring>(root);
            PlayerInput playerInput = GetExactlyOneComponent<PlayerInput>(root);
            UnityPlayerInputGateAdapter inputGate =
                GetExactlyOneComponent<UnityPlayerInputGateAdapter>(root);

            if (authoring.LocalPlayerHost != host ||
                authoring.PlayerSlotProfile != configuration.slotProfile ||
                authoring.ActorProfile != configuration.actorProfile ||
                host.PlayerInput != playerInput ||
                inputGate.PlayerInput != playerInput ||
                playerInput.actions == null ||
                !ReferenceEquals(
                    inputGate.GameplayActionMapReference.ActionAsset,
                    configuration.inputActions) ||
                inputGate.GameplayActionMapReference.ActionMapId !=
                    configuration.playerActionMap.id.ToString("D"))
            {
                throw new InvalidOperationException(
                    "Existing proof scene references differ from the authorized baseline; no repair was guessed.");
            }

            Transform mount = host.ActorMount;
            if (mount == null || mount.parent != root.transform || mount.childCount != 1)
            {
                throw new InvalidOperationException(
                    "Existing proof Actor Mount is missing, ambiguous, or contains extra direct children.");
            }

            GameObject actorRoot = mount.GetChild(0).gameObject;
            PlayerActorRuntimeHost runtimeHost = GetExactlyOneComponent<PlayerActorRuntimeHost>(actorRoot);
            PlayerActorDeclaration declaration = GetExactlyOneComponent<PlayerActorDeclaration>(actorRoot);
            ActorCameraSubjectAuthoring cameraSubject =
                GetExactlyOneComponent<ActorCameraSubjectAuthoring>(actorRoot);

            if (runtimeHost.PlayerActorDeclaration != declaration ||
                cameraSubject.ObservationTransform == null ||
                cameraSubject.ObservationTransform != actorRoot.transform &&
                !cameraSubject.ObservationTransform.IsChildOf(actorRoot.transform) ||
                actorRoot.GetComponentInChildren<CameraOutputAuthoring>(true) != null ||
                !string.IsNullOrWhiteSpace(
                    new SerializedObject(declaration).FindProperty("actorId").stringValue))
            {
                throw new InvalidOperationException(
                    "Existing proof Actor references or ownership are invalid; no repair was guessed.");
            }
        }

        private static void ValidatePlayerComposition(
            Scene scene,
            SceneProvidedLocalPlayerAuthoring authoring,
            ProofConfiguration configuration,
            ProofReport report)
        {
            LocalPlayerHostAuthoring host = authoring.LocalPlayerHost;
            PlayerActorRuntimeHost runtimeHost =
                host.ActorMount.GetChild(0).GetComponent<PlayerActorRuntimeHost>();
            PlayerActorDeclaration declaration = runtimeHost.PlayerActorDeclaration;
            ActorCameraSubjectAuthoring subject =
                declaration.GetComponent<ActorCameraSubjectAuthoring>();
            UnityPlayerInputGateAdapter inputGate = host.GetComponent<UnityPlayerInputGateAdapter>();

            RecordValidator(
                report,
                "Player Session Profile",
                configuration.sessionProfile.TryValidate(out string sessionIssue),
                sessionIssue,
                false);
            RecordValidator(
                report,
                "Player Slot / Actor Profile",
                authoring.PlayerSlotProfile == configuration.slotProfile &&
                authoring.ActorProfile == configuration.actorProfile &&
                declaration.ActorKind == ActorKind.Player &&
                declaration.ActorRole == ActorRole.Protagonist,
                "Expected explicit P1 and Player Protagonist profile references.",
                false);

            bool inputValid = inputGate.TryValidateAuthoring(out string inputIssue);
            RecordValidator(report, "Unity PlayerInput Gate Adapter", inputValid, inputIssue, false);

            bool hostValid = host.TryValidateAdmissionConfiguration(
                runtimeHost,
                allowExistingActorRuntime: true,
                out string hostIssue);
            RecordValidator(report, "Local Player Host Scene-Provided admission", hostValid, hostIssue, false);

            bool runtimeHostValid = runtimeHost.TryValidateConfiguration(out string runtimeHostIssue);
            RecordValidator(report, "Player Actor Runtime Host", runtimeHostValid, runtimeHostIssue, false);

            string subjectIssue = subject == null
                ? "Actor Camera Subject component is missing."
                : string.Empty;
            bool subjectValid = subject != null && subject.TryValidateConfiguration(out subjectIssue);
            RecordValidator(
                report,
                "Actor Camera Subject",
                subjectValid,
                subjectIssue,
                false);

            Undo.RecordObject(authoring, "Validate Scene-Provided Player Proof");
            SceneProvidedLocalPlayerAuthoringResult authoringResult =
                SceneProvidedLocalPlayerAuthoringUtility.Validate(authoring, logDiagnostics: false);
            RecordValidator(
                report,
                "Scene-Provided Local Player Authoring Utility",
                authoringResult.Succeeded,
                authoringResult.Message,
                true);

            report.currentValidators = AppendValidator(
                report.currentValidators,
                new ValidatorResult
                {
                    name = "Local Player Host Custom Editor aggregate validator",
                    status = "BLOCKED",
                    diagnostic = "This aggregate validator is internal to the Framework Editor assembly. Its public Scene-Provided admission validator ran instead; reflection was not used.",
                    mayWriteSerializedState = false
                });
            report.currentValidators = AppendValidator(
                report.currentValidators,
                new ValidatorResult
                {
                    name = "Actor Declaration runtime identity validation",
                    status = "BLOCKED",
                    diagnostic = "Descriptor validation was not invoked because ActorId is intentionally empty; this proof does not fabricate runtime occurrence identity.",
                    mayWriteSerializedState = false
                });

            if (!hostValid || !runtimeHostValid || !inputValid || !subjectValid ||
                !authoringResult.Succeeded || report.currentValidators.Any(result => result.status == "FAIL"))
            {
                throw new InvalidOperationException(
                    "One or more public authoring validators failed. The proof scene will not be saved.");
            }

            if (CountInScene<PlayerInput>(scene) != 1 ||
                CountInScene<SceneProvidedLocalPlayerAuthoring>(scene) != 1 ||
                CountInScene<LocalPlayerHostAuthoring>(scene) != 1 ||
                CountInScene<PlayerActorRuntimeHost>(scene) != 1 ||
                CountInScene<ActorDeclaration>(scene) != 1 ||
                CountInScene<ActorCameraSubjectAuthoring>(scene) != 1 ||
                CountInScene<CameraOutputAuthoring>(scene) != 0)
            {
                throw new InvalidOperationException(
                    "Final composition cardinality or Camera Output ownership is incorrect.");
            }
        }

        private static void ConfigurePlayerInput(
            PlayerInput playerInput,
            InputActionAsset inputActions)
        {
            Undo.RecordObject(playerInput, "Configure Proof PlayerInput");
            SerializedObject serializedInput = new SerializedObject(playerInput);
            serializedInput.Update();
            SerializedProperty actionsProperty = serializedInput.FindProperty("m_Actions");
            if (actionsProperty == null)
            {
                throw new InvalidOperationException(
                    "Unity PlayerInput serialized actions field could not be resolved.");
            }

            actionsProperty.objectReferenceValue = inputActions;
            serializedInput.ApplyModifiedProperties();
            EditorUtility.SetDirty(playerInput);
        }

        private static void ConfigureInputGate(
            UnityPlayerInputGateAdapter inputGate,
            PlayerInput playerInput,
            InputActionAsset inputActions,
            InputActionMap actionMap)
        {
            Undo.RecordObject(inputGate, "Configure Proof Input Gate");
            SerializedObject serializedGate = new SerializedObject(inputGate);
            serializedGate.Update();

            SerializedProperty playerInputProperty = serializedGate.FindProperty("playerInput");
            SerializedProperty actionAssetProperty =
                serializedGate.FindProperty("gameplayActionMap.actionAsset");
            SerializedProperty actionMapIdProperty =
                serializedGate.FindProperty("gameplayActionMap.actionMapId");
            SerializedProperty cachedNameProperty =
                serializedGate.FindProperty("gameplayActionMap.cachedActionMapName");
            SerializedProperty legacyNameProperty =
                serializedGate.FindProperty("gameplayActionMapName");

            if (playerInputProperty == null || actionAssetProperty == null ||
                actionMapIdProperty == null || cachedNameProperty == null ||
                legacyNameProperty == null)
            {
                throw new InvalidOperationException(
                    "Unity PlayerInput Gate serialized authoring fields could not be resolved.");
            }

            playerInputProperty.objectReferenceValue = playerInput;
            actionAssetProperty.objectReferenceValue = inputActions;
            actionMapIdProperty.stringValue = actionMap.id.ToString("D");
            cachedNameProperty.stringValue = actionMap.name;
            legacyNameProperty.stringValue = string.Empty;

            serializedGate.ApplyModifiedProperties();
            EditorUtility.SetDirty(inputGate);
        }

        private static void ConfigureReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object reference)
        {
            Undo.RecordObject(target, "Configure Scene-Provided Player Proof");
            SerializedObject serializedTarget = new SerializedObject(target);
            serializedTarget.Update();
            SerializedProperty property = serializedTarget.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Serialized authoring field '{propertyName}' was not found on '{target.GetType().Name}'.");
            }

            property.objectReferenceValue = reference;
            serializedTarget.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static GameObject CreateChild(Transform parent, string diagnosticName)
        {
            var child = new GameObject(diagnosticName);
            Undo.RegisterCreatedObjectUndo(child, "Create Scene-Provided Player Proof Object");
            Undo.SetTransformParent(child.transform, parent, "Parent Scene-Provided Player Proof Object");
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                throw new FileNotFoundException(
                    $"Required proof dependency '{assetPath}' could not be loaded as {typeof(T).Name}.");
            }

            return asset;
        }

        private static T GetExactlyOneComponent<T>(GameObject gameObject)
            where T : Component
        {
            T[] matches = gameObject.GetComponents<T>();
            if (matches.Length != 1 || matches[0] == null)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one {typeof(T).Name} on the identified GameObject; found {matches.Length}.");
            }

            return matches[0];
        }

        private static T GetSingleInScene<T>(Scene scene)
            where T : Component
        {
            T[] matches = GetSceneComponents<T>(scene);
            if (matches.Length != 1 || matches[0] == null)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one {typeof(T).Name} in the proof scene; found {matches.Length}.");
            }

            return matches[0];
        }

        private static int CountInScene<T>(Scene scene)
            where T : Component => GetSceneComponents<T>(scene).Length;

        private static T[] GetSceneComponents<T>(Scene scene)
            where T : Component
        {
            var results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return results.ToArray();
        }

        private static bool HasAnyPlayerCompositionComponent(Scene scene)
        {
            return CountInScene<PlayerInput>(scene) > 0 ||
                   CountInScene<SceneProvidedLocalPlayerAuthoring>(scene) > 0 ||
                   CountInScene<LocalPlayerHostAuthoring>(scene) > 0 ||
                   CountInScene<PlayerActorRuntimeHost>(scene) > 0 ||
                   CountInScene<ActorDeclaration>(scene) > 0 ||
                   CountInScene<ActorCameraSubjectAuthoring>(scene) > 0 ||
                   CountInScene<UnityPlayerInputGateAdapter>(scene) > 0;
        }

        private static ProofReport LoadOrCreateReport()
        {
            string absolutePath = ToAbsolutePath(ReportPath);
            if (!File.Exists(absolutePath))
            {
                if (File.Exists(ToAbsolutePath(ProofScenePath)))
                {
                    throw new InvalidOperationException(
                        "A proof scene already exists without its evidence report. It will not be modified.");
                }

                return new ProofReport();
            }

            ProofReport report = JsonUtility.FromJson<ProofReport>(File.ReadAllText(absolutePath));
            if (report == null ||
                report.runCount < 1 ||
                report.firstSnapshot == null ||
                report.lastSnapshot == null ||
                report.lastValidators == null ||
                !string.Equals(report.proofScenePath, ProofScenePath, StringComparison.Ordinal) ||
                !report.compositionInitiallyAbsent)
            {
                throw new InvalidOperationException(
                    "Existing proof report is incomplete or belongs to a different proof setup.");
            }

            return report;
        }

        private static void RecordValidator(
            ProofReport report,
            string name,
            bool passed,
            string diagnostic,
            bool serializedMutation)
        {
            report.currentValidators = report.currentValidators ?? Array.Empty<ValidatorResult>();
            var results = new List<ValidatorResult>(report.currentValidators)
            {
                new ValidatorResult
                {
                    name = name,
                    status = passed ? "PASS" : "FAIL",
                    diagnostic = diagnostic ?? string.Empty,
                    mayWriteSerializedState = serializedMutation
                }
            };
            report.currentValidators = results.ToArray();
        }

        private static ValidatorResult CreateValidator(
            string name,
            bool passed,
            string diagnostic,
            bool serializedMutation)
        {
            return new ValidatorResult
            {
                name = name,
                status = passed ? "PASS" : "FAIL",
                diagnostic = diagnostic ?? string.Empty,
                mayWriteSerializedState = serializedMutation
            };
        }

        private static ValidatorResult[] AppendValidator(
            ValidatorResult[] current,
            ValidatorResult next)
        {
            var results = new List<ValidatorResult>(current ?? Array.Empty<ValidatorResult>())
            {
                next
            };
            return results.ToArray();
        }

        private static InitialComposition CaptureInitialComposition(Scene scene)
        {
            GameObject[] gameObjects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .Distinct()
                .ToArray();
            return new InitialComposition
            {
                gameObjectCount = gameObjects.Length,
                playerInputCount = CountInScene<PlayerInput>(scene),
                sceneProvidedAuthoringCount = CountInScene<SceneProvidedLocalPlayerAuthoring>(scene),
                localPlayerHostCount = CountInScene<LocalPlayerHostAuthoring>(scene),
                actorRuntimeHostCount = CountInScene<PlayerActorRuntimeHost>(scene),
                actorDeclarationCount = CountInScene<ActorDeclaration>(scene),
                cameraSubjectCount = CountInScene<ActorCameraSubjectAuthoring>(scene)
            };
        }

        private static void WriteReport(ProofReport report)
        {
            string absolutePath = ToAbsolutePath(ReportPath);
            File.WriteAllText(absolutePath, JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);
        }

        private static ProofSnapshot CaptureSnapshot(Scene scene)
        {
            string absolutePath = ToAbsolutePath(ProofScenePath);
            GameObject[] gameObjects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .Distinct()
                .ToArray();

            string[] gameObjectIds = gameObjects
                .Select(GlobalObjectId.GetGlobalObjectIdSlow)
                .Select(id => id.ToString())
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            string[] componentIds = gameObjects
                .SelectMany(gameObject => gameObject.GetComponents<Component>())
                .Where(component => component != null)
                .Select(GlobalObjectId.GetGlobalObjectIdSlow)
                .Select(id => id.ToString())
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            SceneProvidedLocalPlayerAuthoring authoring =
                GetSingleInScene<SceneProvidedLocalPlayerAuthoring>(scene);
            LocalPlayerHostAuthoring host = authoring.LocalPlayerHost;
            PlayerActorRuntimeHost runtimeHost =
                host.ActorMount.GetChild(0).GetComponent<PlayerActorRuntimeHost>();
            PlayerActorDeclaration declaration = runtimeHost.PlayerActorDeclaration;
            ActorCameraSubjectAuthoring subject =
                declaration.GetComponent<ActorCameraSubjectAuthoring>();
            UnityPlayerInputGateAdapter inputGate =
                host.GetComponent<UnityPlayerInputGateAdapter>();

            string[] references =
            {
                "authoring.host=" + GetObjectIdentity(authoring.LocalPlayerHost),
                "authoring.slot=" + GetObjectIdentity(authoring.PlayerSlotProfile),
                "authoring.actorProfile=" + GetObjectIdentity(authoring.ActorProfile),
                "host.playerInput=" + GetObjectIdentity(host.PlayerInput),
                "host.actorMount=" + GetObjectIdentity(host.ActorMount),
                "host.runtimePrefab=" + GetObjectIdentity(host.PlayerActorRuntimeHostPrefab),
                "playerInput.actions=" + GetObjectIdentity(host.PlayerInput.actions),
                "gate.playerInput=" + GetObjectIdentity(inputGate.PlayerInput),
                "gate.actions=" + GetObjectIdentity(inputGate.GameplayActionMapReference.ActionAsset),
                "gate.actionMapId=" + inputGate.GameplayActionMapReference.ActionMapId,
                "runtimeHost.declaration=" + GetObjectIdentity(runtimeHost.PlayerActorDeclaration),
                "actor.actorId=" + new SerializedObject(declaration).FindProperty("actorId").stringValue,
                "subject.observation=" + GetObjectIdentity(subject.ObservationTransform)
            };

            var snapshot = new ProofSnapshot
            {
                gameObjectCount = gameObjects.Length,
                playerInputCount = CountInScene<PlayerInput>(scene),
                sceneProvidedAuthoringCount = CountInScene<SceneProvidedLocalPlayerAuthoring>(scene),
                localPlayerHostCount = CountInScene<LocalPlayerHostAuthoring>(scene),
                actorRuntimeHostCount = CountInScene<PlayerActorRuntimeHost>(scene),
                actorDeclarationCount = CountInScene<ActorDeclaration>(scene),
                cameraSubjectCount = CountInScene<ActorCameraSubjectAuthoring>(scene),
                cameraOutputAuthoringCount = CountInScene<CameraOutputAuthoring>(scene),
                gameObjectIds = gameObjectIds,
                componentIds = componentIds,
                serializedReferences = references.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                sceneFileSha256 = ComputeFileSha256(absolutePath)
            };
            snapshot.structureFingerprint = snapshot.ComputeStructureFingerprint();
            return snapshot;
        }

        private static string GetObjectIdentity(UnityEngine.Object target)
        {
            if (target == null)
            {
                return "<none>";
            }

            if (target is Component || target is GameObject)
            {
                return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            }

            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                       target,
                       out string guid,
                       out long localId)
                ? guid + ":" + localId
                : "<unresolved-asset-id>";
        }

        private static Scene GetOrOpenProofScene(
            bool sceneExists,
            out bool wasLoadedBeforeCommand,
            out bool wasCreatedInMemory)
        {
            Scene scene = SceneManager.GetSceneByPath(ProofScenePath);
            wasLoadedBeforeCommand = scene.IsValid() && scene.isLoaded;
            wasCreatedInMemory = false;

            if (wasLoadedBeforeCommand)
            {
                return scene;
            }

            if (sceneExists)
            {
                return EditorSceneManager.OpenScene(ProofScenePath, OpenSceneMode.Additive);
            }

            scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            wasCreatedInMemory = true;
            return scene;
        }

        private static void EnsureProofFolder()
        {
            string[] segments = ProofFolder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static List<LoadedSceneState> CaptureLoadedScenes()
        {
            var result = new List<LoadedSceneState>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded)
                {
                    result.Add(new LoadedSceneState(scene, scene.path, scene.isDirty));
                }
            }

            return result;
        }

        private static bool HasEquivalentLoadedSceneStates(
            IReadOnlyList<LoadedSceneState> before,
            Scene proofScene)
        {
            foreach (LoadedSceneState original in before)
            {
                if (original.scene == proofScene)
                {
                    continue;
                }

                if (!original.scene.IsValid() || !original.scene.isLoaded ||
                    original.scene.isDirty != original.wasDirty)
                {
                    return false;
                }
            }

            return true;
        }

        private static void RestoreLoadedSceneSet(
            IReadOnlyList<LoadedSceneState> originalScenes,
            Scene proofScene,
            bool sceneWasLoadedBeforeCommand)
        {
            if (sceneWasLoadedBeforeCommand || !proofScene.IsValid() || !proofScene.isLoaded)
            {
                return;
            }

            bool wasOriginal = originalScenes.Any(state => state.scene == proofScene);
            if (!wasOriginal)
            {
                EditorSceneManager.CloseScene(proofScene, true);
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string ComputeFileSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static string ReadGitValue(string arguments)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Git process could not be started for proof metadata.");
                }

                string output = process.StandardOutput.ReadToEnd().Trim();
                string error = process.StandardError.ReadToEnd().Trim();
                process.WaitForExit();
                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    throw new InvalidOperationException(
                        $"Git metadata preflight failed for '{arguments}': {error}");
                }

                return output;
            }
        }

        private sealed class ProofConfiguration
        {
            public readonly GameApplicationAsset application;
            public readonly PlayerSessionProfile sessionProfile;
            public readonly PlayerSlotProfile slotProfile;
            public readonly ActorProfile actorProfile;
            public readonly InputActionAsset inputActions;
            public readonly InputActionMap playerActionMap;
            public readonly SessionCameraAssignmentAsset cameraAssignment;
            public readonly GameObject cameraOutputPrefab;
            public readonly CameraOutputDefinition outputDefinition;
            public readonly string frameworkVersion;

            public ProofConfiguration(
                GameApplicationAsset application,
                PlayerSessionProfile sessionProfile,
                PlayerSlotProfile slotProfile,
                ActorProfile actorProfile,
                InputActionAsset inputActions,
                InputActionMap playerActionMap,
                SessionCameraAssignmentAsset cameraAssignment,
                GameObject cameraOutputPrefab,
                CameraOutputDefinition outputDefinition,
                string frameworkVersion,
                ValidatorResult[] preflightChecks)
            {
                this.application = application;
                this.sessionProfile = sessionProfile;
                this.slotProfile = slotProfile;
                this.actorProfile = actorProfile;
                this.inputActions = inputActions;
                this.playerActionMap = playerActionMap;
                this.cameraAssignment = cameraAssignment;
                this.cameraOutputPrefab = cameraOutputPrefab;
                this.outputDefinition = outputDefinition;
                this.frameworkVersion = frameworkVersion;
                this.preflightChecks = preflightChecks;
            }

            public readonly ValidatorResult[] preflightChecks;
        }

        [Serializable]
        private sealed class ProofReport
        {
            public string unityVersion;
            public string frameworkVersion;
            public string gitBranch;
            public string gitHead;
            public string proofScenePath;
            public bool compositionInitiallyAbsent;
            public int runCount;
            public InitialComposition initialComposition;
            public string lastRunUtc;
            public string lastRunOutcome;
            public bool secondExecutionEquivalent;
            public bool idempotenceVerified;
            public ProofSnapshot firstSnapshot;
            public ProofSnapshot lastSnapshot;
            public ValidatorResult[] currentValidators = Array.Empty<ValidatorResult>();
            public ValidatorResult[] lastValidators = Array.Empty<ValidatorResult>();
        }

        [Serializable]
        private sealed class InitialComposition
        {
            public int gameObjectCount;
            public int playerInputCount;
            public int sceneProvidedAuthoringCount;
            public int localPlayerHostCount;
            public int actorRuntimeHostCount;
            public int actorDeclarationCount;
            public int cameraSubjectCount;
        }

        [Serializable]
        private sealed class ProofSnapshot
        {
            public int gameObjectCount;
            public int playerInputCount;
            public int sceneProvidedAuthoringCount;
            public int localPlayerHostCount;
            public int actorRuntimeHostCount;
            public int actorDeclarationCount;
            public int cameraSubjectCount;
            public int cameraOutputAuthoringCount;
            public string[] gameObjectIds;
            public string[] componentIds;
            public string[] serializedReferences;
            public string sceneFileSha256;
            public string structureFingerprint;

            public bool StructureEquals(ProofSnapshot other)
            {
                return other != null &&
                       gameObjectCount == other.gameObjectCount &&
                       playerInputCount == other.playerInputCount &&
                       sceneProvidedAuthoringCount == other.sceneProvidedAuthoringCount &&
                       localPlayerHostCount == other.localPlayerHostCount &&
                       actorRuntimeHostCount == other.actorRuntimeHostCount &&
                       actorDeclarationCount == other.actorDeclarationCount &&
                       cameraSubjectCount == other.cameraSubjectCount &&
                       cameraOutputAuthoringCount == other.cameraOutputAuthoringCount &&
                       string.Equals(structureFingerprint, other.structureFingerprint, StringComparison.Ordinal);
            }

            public string ComputeStructureFingerprint()
            {
                var builder = new StringBuilder();
                builder.Append(gameObjectCount).Append('|')
                    .Append(playerInputCount).Append('|')
                    .Append(sceneProvidedAuthoringCount).Append('|')
                    .Append(localPlayerHostCount).Append('|')
                    .Append(actorRuntimeHostCount).Append('|')
                    .Append(actorDeclarationCount).Append('|')
                    .Append(cameraSubjectCount).Append('|')
                    .Append(cameraOutputAuthoringCount).Append('\n');

                AppendValues(builder, gameObjectIds);
                AppendValues(builder, componentIds);
                AppendValues(builder, serializedReferences);
                using (SHA256 sha256 = SHA256.Create())
                {
                    return BitConverter.ToString(
                        sha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())))
                        .Replace("-", string.Empty);
                }
            }

            private static void AppendValues(StringBuilder builder, IEnumerable<string> values)
            {
                foreach (string value in values ?? Array.Empty<string>())
                {
                    builder.Append(value).Append('\n');
                }
            }
        }

        [Serializable]
        private sealed class ValidatorResult
        {
            public string name;
            public string status;
            public string diagnostic;
            public bool mayWriteSerializedState;
        }

        private sealed class LoadedSceneState
        {
            public readonly Scene scene;
            public readonly string path;
            public readonly bool wasDirty;

            public LoadedSceneState(Scene scene, string path, bool wasDirty)
            {
                this.scene = scene;
                this.path = path;
                this.wasDirty = wasDirty;
            }
        }
    }
}

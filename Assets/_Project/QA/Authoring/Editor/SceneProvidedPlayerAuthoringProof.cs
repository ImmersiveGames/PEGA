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
    public static class SceneProvidedPlayerAuthoringProof
    {
        private const string MenuPath =
            "Tools/Immersive/QA/Build Scene-Provided Player Proof";

        private const string ProofFolder =
            "Assets/_Project/QA/Authoring";

        private const string ProofScenePath =
            ProofFolder + "/SceneProvidedPlayerAuthoringProof.unity";

        private const string ReportPath =
            ProofFolder + "/SceneProvidedPlayerAuthoringProof.json";

        private const int ReportSchemaVersion = 2;

        private const string ToolSourcePath =
            "Assets/_Project/QA/Authoring/Editor/SceneProvidedPlayerAuthoringProof.cs";

        private const string ExpectedBlockedHostEditorValidator =
            "Local Player Host Custom Editor aggregate validator";

        private const string ExpectedBlockedActorIdentityValidator =
            "Actor Declaration runtime identity validation";

        private const string RuntimeValidationNotRun = "NOT_RUN";

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

        public static void ExecuteBatch()
        {
            try
            {
                int previousRunCount = 0;
                string reportAbsolutePath = ToAbsolutePath(ReportPath);
                if (File.Exists(reportAbsolutePath))
                {
                    ProofReport previousReport = JsonUtility.FromJson<ProofReport>(
                        File.ReadAllText(reportAbsolutePath));
                    previousRunCount = previousReport == null ? 0 : previousReport.runCount;
                }

                BuildProof();

                if (!File.Exists(reportAbsolutePath))
                {
                    throw new InvalidOperationException(
                        "The proof command did not create its report.");
                }

                ProofReport report = JsonUtility.FromJson<ProofReport>(
                    File.ReadAllText(reportAbsolutePath));
                if (report == null ||
                    report.runCount != previousRunCount + 1 ||
                    !string.Equals(
                        report.lastRunOutcome,
                        "STRUCTURAL_PASS",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        report.structuralOutcome,
                        "PASS",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        report.runtimeOutcome,
                        RuntimeValidationNotRun,
                        StringComparison.Ordinal) ||
                    report.lastValidators == null ||
                    !HasOnlyExpectedBatchValidatorStatuses(report.lastValidators))
                {
                    throw new InvalidOperationException(
                        "The proof command did not record one successful run with passing applicable validators.");
                }

                Debug.Log(
                    $"SCENE_PROVIDED_PLAYER_PROOF_RESULT=PASS runCount='{report.runCount}' " +
                    $"idempotenceVerified='{report.idempotenceVerified}'.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "SCENE_PROVIDED_PLAYER_PROOF_RESULT=FAIL " +
                    exception.GetType().Name + ": " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

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
            SceneContextSnapshot sceneContextBefore = CaptureSceneContext();
            string toolSourceSha256 = ComputeToolSourceSha256();
            Scene proofScene = default;
            bool sceneWasLoadedBeforeCommand = false;
            bool proofSceneOpenedByCommand = false;
            bool proofScenePersisted = false;
            bool proofCompositionSaved = false;
            bool sceneActivationSetterReturned = false;
            bool runStartedWithCompletedProof = false;
            int undoGroup = -1;
            ProofReport report = null;

            try
            {
                EnsureProofFolder();
                bool proofSceneExists = File.Exists(ToAbsolutePath(ProofScenePath));
                report = LoadOrCreateReport(proofSceneExists);
                if (report.schemaVersion != ReportSchemaVersion)
                {
                    throw new InvalidOperationException(
                        "The proof report schema is unsupported; preserve it and inspect it before continuing.");
                }

                if (!string.IsNullOrEmpty(report.toolSourceSha256) &&
                    !string.Equals(report.toolSourceSha256, toolSourceSha256, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The proof tool source changed since the recorded run. Preserve the report and start a new isolated proof deliberately.");
                }

                report.toolSourceSha256 = toolSourceSha256;
                runStartedWithCompletedProof = report.runCount > 0;
                report.lastAttemptOutcome = string.Empty;
                report.lastAttemptDiagnostic = string.Empty;
                report.lastAttemptValidators = Array.Empty<ValidatorResult>();
                report.runtimeOutcome = RuntimeValidationNotRun;
                bool firstExecution = report.runCount == 0;
                string currentBranch = ReadGitValue("branch --show-current");
                string currentHead = ReadGitValue("rev-parse HEAD");
                if ((!string.IsNullOrEmpty(report.gitBranch) || !string.IsNullOrEmpty(report.gitHead)) &&
                    (report.gitBranch != currentBranch || report.gitHead != currentHead))
                {
                    throw new InvalidOperationException(
                        "Branch or HEAD changed since the first proof run. Start a new isolated proof before continuing.");
                }

                report.gitBranch = currentBranch;
                report.gitHead = currentHead;
                report.lastSceneContextBefore = sceneContextBefore;
                string proofSceneAbsolutePath = ToAbsolutePath(ProofScenePath);
                proofSceneExists = File.Exists(proofSceneAbsolutePath);

                ProofConfiguration configuration = LoadAndValidateSharedAssets();
                report.currentValidators = configuration.preflightChecks;

                if (string.Equals(report.recoveryState, "ORPHANED_SCENE", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A proof scene exists without trustworthy creation evidence. It will not be adopted, overwritten, or deleted.");
                }

                if (string.Equals(report.recoveryState, "PARTIAL_COMPOSITION_SAVED", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A saved composition has no successful proof report. It is preserved for manual inspection and will not be rebuilt automatically.");
                }

                if (!firstExecution && !proofSceneExists)
                {
                    throw new InvalidOperationException(
                        "The proof report exists but its scene is missing. Restore the proof scene before running again.");
                }

                if (proofSceneExists && report.runCount > 0)
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

                bool recoveringPersistedEmptyScene = string.Equals(
                    report.recoveryState,
                    "PARTIAL_SCENE_PERSISTED",
                    StringComparison.Ordinal);
                if (recoveringPersistedEmptyScene)
                {
                    if (!proofSceneExists ||
                        string.IsNullOrWhiteSpace(report.recoverySceneSha256) ||
                        !string.Equals(
                            ComputeFileSha256(proofSceneAbsolutePath),
                            report.recoverySceneSha256,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "The persisted partial proof scene differs from its recovery hash. It will not be adopted or overwritten.");
                    }
                }

                if (proofSceneExists && report.runCount == 0 && !recoveringPersistedEmptyScene)
                {
                    throw new InvalidOperationException(
                        "A proof scene exists without a recognized partial-run record. Preserve it and inspect the recovery report.");
                }

                GetOrOpenProofScene(
                    proofSceneExists,
                    recoveringPersistedEmptyScene,
                    ref proofScene,
                    out sceneWasLoadedBeforeCommand,
                    out proofSceneOpenedByCommand,
                    out proofScenePersisted);

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

                if (firstExecution && report.initialComposition == null)
                {
                    report.initialComposition = CaptureInitialComposition(proofScene);
                    report.compositionInitiallyAbsent = compositionWasAbsent;
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

                VerifyPersistedProofScene(proofScene);
                report.requestedSceneBeforeActivation = CaptureSceneIdentity(proofScene);
                sceneActivationSetterReturned = EditorSceneManager.SetActiveScene(proofScene);
                report.sceneActivationSetterReturned = sceneActivationSetterReturned;
                Scene activeAfterActivation = SceneManager.GetActiveScene();
                report.activeSceneAfterActivation = CaptureSceneIdentity(activeAfterActivation);
                if (!sceneActivationSetterReturned ||
                    !SameSceneIdentity(activeAfterActivation, proofScene) ||
                    !SameSceneIdentity(SceneManager.GetActiveScene(), proofScene))
                {
                    throw new InvalidOperationException(
                        "The persisted proof scene did not become the active scene. " +
                        DescribeScene(proofScene) + "; active=" + DescribeScene(activeAfterActivation));
                }

                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Build Scene-Provided Player Authoring Proof");

                SceneProvidedLocalPlayerAuthoring authoring =
                    compositionWasAbsent
                        ? CreateProofComposition(configuration)
                        : GetSingleInScene<SceneProvidedLocalPlayerAuthoring>(proofScene);

                if (!SameSceneIdentity(authoring.gameObject.scene, proofScene))
                {
                    throw new InvalidOperationException(
                        "The official creator produced its Local Player root outside the proof scene. " +
                        "The composition will not be saved.");
                }
                report.creatorRootScene = CaptureSceneIdentity(authoring.gameObject.scene);

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

                proofCompositionSaved = true;
                proofScenePersisted = true;
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
                report.structuralOutcome = "PASS";
                report.runtimeOutcome = RuntimeValidationNotRun;
                report.recoveryState = "COMPLETED";
                report.recoveryDiagnostic = string.Empty;
                report.recoverySceneSha256 = string.Empty;
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

                Debug.Log(
                    $"Scene-Provided Player proof run {report.runCount} passed structural checks; finalizing evidence. " +
                    $"Scene='{ProofScenePath}', validators='{report.lastValidators.Length}', " +
                    $"secondExecutionEquivalent='{report.secondExecutionEquivalent}', " +
                    $"idempotenceVerified='{report.idempotenceVerified}', " +
                    $"sceneSha256='{snapshot.sceneFileSha256}'.");
            }
            catch (Exception exception)
            {
                if (!proofCompositionSaved && undoGroup >= 0)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                }

                if (report != null)
                {
                    bool orphanedScene = string.Equals(
                        report.recoveryState,
                        "ORPHANED_SCENE",
                        StringComparison.Ordinal);
                    report.lastAttemptOutcome = orphanedScene
                        ? "ORPHANED_SCENE"
                        : proofCompositionSaved && !runStartedWithCompletedProof
                            ? "PARTIAL_COMPOSITION_SAVED"
                            : proofScenePersisted
                                ? "PARTIAL_SCENE_PERSISTED"
                                : "FAILED_BEFORE_PERSISTENCE";
                    report.lastAttemptDiagnostic = exception.GetType().Name + ": " + exception.Message;
                    report.lastAttemptValidators = report.currentValidators ?? Array.Empty<ValidatorResult>();
                    report.currentValidators = Array.Empty<ValidatorResult>();
                    report.recoveryState = orphanedScene
                        ? "ORPHANED_SCENE"
                        : runStartedWithCompletedProof
                            ? "COMPLETED_WITH_FAILED_ATTEMPT"
                            : proofCompositionSaved
                                ? "PARTIAL_COMPOSITION_SAVED"
                                : proofScenePersisted
                                    ? "PARTIAL_SCENE_PERSISTED"
                                    : "FAILED_BEFORE_PERSISTENCE";
                    report.recoveryDiagnostic = report.lastAttemptDiagnostic;
                    if (!runStartedWithCompletedProof && proofScenePersisted && File.Exists(ToAbsolutePath(ProofScenePath)))
                    {
                        report.recoverySceneSha256 = ComputeFileSha256(ToAbsolutePath(ProofScenePath));
                    }
                }

                Debug.LogError(
                    "Scene-Provided Player authoring proof stopped safely. " +
                    exception.GetType().Name + ": " + exception.Message);

            }
            finally
            {
                bool activeSceneRestored = false;
                string restorationDiagnostic = string.Empty;
                if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                {
                    bool setterReturned = SameSceneIdentity(
                        SceneManager.GetActiveScene(),
                        originalActiveScene) || EditorSceneManager.SetActiveScene(originalActiveScene);
                    Scene activeAfterRestore = SceneManager.GetActiveScene();
                    activeSceneRestored = setterReturned && SameSceneIdentity(activeAfterRestore, originalActiveScene);
                    if (!activeSceneRestored)
                    {
                        restorationDiagnostic = "Original active scene restoration failed: " +
                            DescribeScene(originalActiveScene) + "; active=" + DescribeScene(activeAfterRestore);
                    }
                }
                else
                {
                    restorationDiagnostic = "The original active scene is no longer valid and loaded.";
                }

                Selection.objects = originalSelection;
                bool selectionRestored = Selection.objects.SequenceEqual(originalSelection);
                bool proofSceneLifecycleRestored = RestoreLoadedSceneSet(
                    originalScenes,
                    proofScene,
                    sceneWasLoadedBeforeCommand,
                    proofSceneOpenedByCommand,
                    activeSceneRestored,
                    proofCompositionSaved,
                    out string closeDiagnostic);
                if (!string.IsNullOrEmpty(closeDiagnostic))
                {
                    restorationDiagnostic = string.IsNullOrEmpty(restorationDiagnostic)
                        ? closeDiagnostic
                        : restorationDiagnostic + " " + closeDiagnostic;
                }

                if (report != null)
                {
                    report.activeSceneRestorationSucceeded = activeSceneRestored;
                    report.selectionRestorationSucceeded = selectionRestored;
                    report.proofSceneLifecycleRestored = proofSceneLifecycleRestored;
                    report.restorationDiagnostic = restorationDiagnostic;
                    report.lastSceneContextAfter = CaptureSceneContext();
                    bool loadedSceneContextRestored = SameSceneContext(
                        sceneContextBefore,
                        report.lastSceneContextAfter);
                    report.loadedSceneContextRestorationSucceeded = loadedSceneContextRestored;
                    if (!loadedSceneContextRestored)
                    {
                        string contextDiagnostic = "Loaded scene set/order or active scene differs from the preflight snapshot.";
                        restorationDiagnostic = string.IsNullOrEmpty(restorationDiagnostic)
                            ? contextDiagnostic
                            : restorationDiagnostic + " " + contextDiagnostic;
                        report.restorationDiagnostic = restorationDiagnostic;
                    }

                    if (proofCompositionSaved &&
                        (!activeSceneRestored || !selectionRestored || !proofSceneLifecycleRestored ||
                         !loadedSceneContextRestored))
                    {
                        report.lastAttemptOutcome = "RESTORATION_FAILED";
                        report.lastAttemptDiagnostic = restorationDiagnostic;
                        report.structuralOutcome = "FAIL";
                        report.lastRunOutcome = "RESTORATION_FAILED";
                        report.recoveryState = "COMPLETED_WITH_FAILED_ATTEMPT";
                        report.recoveryDiagnostic = restorationDiagnostic;
                    }

                    if (string.IsNullOrEmpty(report.lastAttemptOutcome))
                    {
                        report.lastAttemptOutcome = "COMPLETED";
                        report.lastAttemptDiagnostic = string.Empty;
                    }

                    try
                    {
                        WriteReport(report);
                        Debug.Log(
                            $"Scene-Provided Player proof report saved: runCount='{report.runCount}', " +
                            $"structural='{report.structuralOutcome}', runtime='{report.runtimeOutcome}', " +
                            $"toolSha256='{report.toolSourceSha256}'.");
                    }
                    catch (Exception reportException)
                    {
                        Debug.LogError(
                            "Scene-Provided Player proof could not persist its evidence report. " +
                            reportException.GetType().Name + ": " + reportException.Message);
                    }
                }

                if (!activeSceneRestored || !selectionRestored || !proofSceneLifecycleRestored ||
                    (report != null && !report.loadedSceneContextRestorationSucceeded))
                {
                    Debug.LogError(
                        "Scene-Provided Player proof context restoration was incomplete. " + restorationDiagnostic);
                }
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

        private static ProofReport LoadOrCreateReport(bool proofSceneExists)
        {
            string absolutePath = ToAbsolutePath(ReportPath);
            if (!File.Exists(absolutePath))
            {
                if (proofSceneExists)
                {
                    return new ProofReport
                    {
                        schemaVersion = ReportSchemaVersion,
                        proofScenePath = ProofScenePath,
                        recoveryState = "ORPHANED_SCENE",
                        lastAttemptOutcome = "ORPHANED_SCENE",
                        lastAttemptDiagnostic = "A proof scene exists without a report identifying its creation and expected hash."
                    };
                }

                return new ProofReport
                {
                    schemaVersion = ReportSchemaVersion,
                    proofScenePath = ProofScenePath,
                    recoveryState = "NEW"
                };
            }

            ProofReport report = JsonUtility.FromJson<ProofReport>(File.ReadAllText(absolutePath));
            if (report == null ||
                report.schemaVersion != ReportSchemaVersion ||
                report.runCount < 0 ||
                !string.Equals(report.proofScenePath, ProofScenePath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Existing proof report is malformed, from another schema, or belongs to another proof setup. Preserve it before continuing.");
            }

            bool completedReportValid = report.runCount > 0 &&
                report.firstSnapshot != null &&
                report.lastSnapshot != null &&
                report.lastValidators != null &&
                report.compositionInitiallyAbsent;
            bool partialReportValid = report.runCount == 0 &&
                (string.Equals(report.recoveryState, "FAILED_BEFORE_PERSISTENCE", StringComparison.Ordinal) ||
                 string.Equals(report.recoveryState, "PARTIAL_SCENE_PERSISTED", StringComparison.Ordinal) ||
                 string.Equals(report.recoveryState, "PARTIAL_COMPOSITION_SAVED", StringComparison.Ordinal) ||
                 string.Equals(report.recoveryState, "ORPHANED_SCENE", StringComparison.Ordinal));
            bool failedAttemptReportValid = report.runCount > 0 && completedReportValid &&
                string.Equals(report.recoveryState, "COMPLETED_WITH_FAILED_ATTEMPT", StringComparison.Ordinal);
            if (!completedReportValid && !partialReportValid && !failedAttemptReportValid)
            {
                throw new InvalidOperationException(
                    "Existing proof report is incomplete or has no recognized recovery state. Preserve it before continuing.");
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

        private static bool HasOnlyExpectedBatchValidatorStatuses(ValidatorResult[] validators)
        {
            if (validators == null || validators.Any(result => result == null))
            {
                return false;
            }

            string[] blockedNames = validators
                .Where(result => string.Equals(result.status, "BLOCKED", StringComparison.Ordinal))
                .Select(result => result.name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] expectedBlockedNames =
            {
                ExpectedBlockedActorIdentityValidator,
                ExpectedBlockedHostEditorValidator
            };
            Array.Sort(expectedBlockedNames, StringComparer.Ordinal);

            return validators.All(result =>
                       string.Equals(result.status, "PASS", StringComparison.Ordinal) ||
                       string.Equals(result.status, "BLOCKED", StringComparison.Ordinal)) &&
                   blockedNames.SequenceEqual(expectedBlockedNames, StringComparer.Ordinal);
        }

        private static string ComputeToolSourceSha256() =>
            ComputeFileSha256(ToAbsolutePath(ToolSourcePath));

        private static void VerifyPersistedProofScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
            {
                throw new InvalidOperationException(
                    "The proof scene must be valid, loaded, and not a preview scene before authoring: " +
                    DescribeScene(scene));
            }

            if (!string.Equals(NormalizeScenePath(scene.path), ProofScenePath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The loaded scene path does not identify the authorized proof scene: " + DescribeScene(scene));
            }

            Scene resolvedByPath = SceneManager.GetSceneByPath(ProofScenePath);
            if (!resolvedByPath.IsValid() || !resolvedByPath.isLoaded ||
                resolvedByPath.handle.GetRawData() != scene.handle.GetRawData() ||
                !File.Exists(ToAbsolutePath(ProofScenePath)))
            {
                throw new InvalidOperationException(
                    "The proof scene could not be resolved back to the same loaded scene handle by its persisted path. " +
                    "requested=" + DescribeScene(scene) + "; resolved=" + DescribeScene(resolvedByPath));
            }
        }

        private static string NormalizeScenePath(string path) =>
            (path ?? string.Empty).Replace('\\', '/');

        private static bool SameSceneIdentity(Scene left, Scene right) =>
            left.IsValid() && right.IsValid() &&
            left.isLoaded && right.isLoaded &&
            left.handle.GetRawData() == right.handle.GetRawData() &&
            string.Equals(NormalizeScenePath(left.path), NormalizeScenePath(right.path), StringComparison.Ordinal);

        private static string DescribeScene(Scene scene) =>
            $"handle='{scene.handle}', name='{scene.name}', path='{NormalizeScenePath(scene.path)}', " +
            $"valid='{scene.IsValid()}', loaded='{scene.isLoaded}', preview='{scene.IsValid() && EditorSceneManager.IsPreviewScene(scene)}', " +
            $"active='{scene.IsValid() && SameSceneIdentity(SceneManager.GetActiveScene(), scene)}'";

        private static SceneIdentitySnapshot CaptureSceneIdentity(Scene scene)
        {
            return new SceneIdentitySnapshot
            {
                handle = scene.handle.GetRawData(),
                name = scene.name ?? string.Empty,
                path = NormalizeScenePath(scene.path),
                isValid = scene.IsValid(),
                isLoaded = scene.IsValid() && scene.isLoaded,
                isPreview = scene.IsValid() && EditorSceneManager.IsPreviewScene(scene),
                isDirty = scene.IsValid() && scene.isDirty,
                isActive = scene.IsValid() && SameSceneIdentity(SceneManager.GetActiveScene(), scene)
            };
        }

        private static SceneContextSnapshot CaptureSceneContext()
        {
            var loadedScenes = new List<SceneIdentitySnapshot>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded)
                {
                    loadedScenes.Add(CaptureSceneIdentity(scene));
                }
            }

            return new SceneContextSnapshot
            {
                sceneCount = SceneManager.sceneCount,
                loadedScenes = loadedScenes.ToArray(),
                activeScene = CaptureSceneIdentity(SceneManager.GetActiveScene())
            };
        }

        private static bool SameSceneContext(
            SceneContextSnapshot expected,
            SceneContextSnapshot actual)
        {
            if (expected == null || actual == null ||
                expected.sceneCount != actual.sceneCount ||
                expected.loadedScenes == null || actual.loadedScenes == null ||
                expected.loadedScenes.Length != actual.loadedScenes.Length ||
                expected.activeScene == null || actual.activeScene == null)
            {
                return false;
            }

            if (expected.activeScene.handle != actual.activeScene.handle ||
                !string.Equals(expected.activeScene.path, actual.activeScene.path, StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = 0; index < expected.loadedScenes.Length; index++)
            {
                SceneIdentitySnapshot left = expected.loadedScenes[index];
                SceneIdentitySnapshot right = actual.loadedScenes[index];
                if (left == null || right == null ||
                    left.handle != right.handle ||
                    !string.Equals(left.name, right.name, StringComparison.Ordinal) ||
                    !string.Equals(left.path, right.path, StringComparison.Ordinal) ||
                    left.isValid != right.isValid ||
                    left.isLoaded != right.isLoaded ||
                    left.isPreview != right.isPreview ||
                    left.isDirty != right.isDirty ||
                    left.isActive != right.isActive)
                {
                    return false;
                }
            }

            return true;
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

        private static void GetOrOpenProofScene(
            bool sceneExists,
            bool recoveringPersistedEmptyScene,
            ref Scene proofScene,
            out bool wasLoadedBeforeCommand,
            out bool openedByCommand,
            out bool persisted)
        {
            Scene scene = SceneManager.GetSceneByPath(ProofScenePath);
            wasLoadedBeforeCommand = scene.IsValid() && scene.isLoaded;
            openedByCommand = false;
            persisted = sceneExists;

            if (wasLoadedBeforeCommand)
            {
                proofScene = scene;
                VerifyPersistedProofScene(scene);
                return;
            }

            if (sceneExists)
            {
                scene = EditorSceneManager.OpenScene(ProofScenePath, OpenSceneMode.Additive);
                openedByCommand = true;
                proofScene = scene;
                VerifyPersistedProofScene(scene);
                return;
            }

            scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            openedByCommand = true;
            proofScene = scene;
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene) || scene.rootCount != 0)
            {
                throw new InvalidOperationException(
                    "Unity did not create a valid empty non-preview scene for the proof: " + DescribeScene(scene));
            }

            if (!EditorSceneManager.SaveScene(scene, ProofScenePath))
            {
                throw new InvalidOperationException(
                    "Unity did not persist the empty proof scene at its authorized path: " + DescribeScene(scene));
            }

            persisted = File.Exists(ToAbsolutePath(ProofScenePath));
            if (!persisted)
            {
                throw new InvalidOperationException(
                    "SaveScene returned success but the proof scene file is absent at the authorized path.");
            }

            VerifyPersistedProofScene(scene);
            if (!EditorSceneManager.CloseScene(scene, true))
            {
                throw new InvalidOperationException(
                    "The initial empty proof scene was persisted, but Unity could not close it before reopening. " +
                    DescribeScene(scene));
            }

            if (scene.IsValid() && scene.isLoaded)
            {
                throw new InvalidOperationException(
                    "CloseScene reported success but the initial proof scene remains loaded: " + DescribeScene(scene));
            }

            proofScene = default;
            scene = EditorSceneManager.OpenScene(ProofScenePath, OpenSceneMode.Additive);
            proofScene = scene;
            VerifyPersistedProofScene(scene);
            if (!recoveringPersistedEmptyScene && scene.rootCount != 0)
            {
                throw new InvalidOperationException(
                    "The reopened first-run proof scene was not empty. It will not be modified.");
            }
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
                    result.Add(new LoadedSceneState(
                        scene,
                        NormalizeScenePath(scene.path),
                        scene.name,
                        EditorSceneManager.IsPreviewScene(scene),
                        scene.isDirty));
                }
            }

            return result;
        }

        private static bool HasEquivalentLoadedSceneStates(
            IReadOnlyList<LoadedSceneState> before,
            Scene proofScene)
        {
            var expectedHandles = before.Select(state => state.scene.handle.GetRawData()).ToList();
            if (!expectedHandles.Contains(proofScene.handle.GetRawData()))
            {
                expectedHandles.Add(proofScene.handle.GetRawData());
            }

            foreach (LoadedSceneState original in before)
            {
                if (original.scene == proofScene)
                {
                    continue;
                }

                if (!original.scene.IsValid() || !original.scene.isLoaded ||
                    original.scene.isDirty != original.wasDirty ||
                    !string.Equals(NormalizeScenePath(original.scene.path), original.path, StringComparison.Ordinal) ||
                    !string.Equals(original.scene.name, original.name, StringComparison.Ordinal) ||
                    EditorSceneManager.IsPreviewScene(original.scene) != original.isPreview)
                {
                    return false;
                }
            }

            var actualHandles = new List<ulong>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded)
                {
                    actualHandles.Add(scene.handle.GetRawData());
                }
            }

            return actualHandles.SequenceEqual(expectedHandles);
        }

        private static bool RestoreLoadedSceneSet(
            IReadOnlyList<LoadedSceneState> originalScenes,
            Scene proofScene,
            bool sceneWasLoadedBeforeCommand,
            bool proofSceneOpenedByCommand,
            bool activeSceneRestored,
            bool proofCompositionSaved,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (sceneWasLoadedBeforeCommand)
            {
                return proofScene.IsValid() && proofScene.isLoaded;
            }

            if (!proofSceneOpenedByCommand || !proofScene.IsValid() || !proofScene.isLoaded)
            {
                return true;
            }

            if (!activeSceneRestored)
            {
                diagnostic = "Proof scene remains loaded because the original active scene could not be restored safely.";
                return false;
            }

            if (!proofCompositionSaved && proofScene.isDirty)
            {
                diagnostic = "Proof scene remains loaded with unsaved state after rollback; it was not discarded.";
                return false;
            }

            bool closed = EditorSceneManager.CloseScene(proofScene, true);
            bool isClosed = !proofScene.IsValid() || !proofScene.isLoaded;
            if (!closed || !isClosed)
            {
                diagnostic = "Temporary proof scene closure was not confirmed: " + DescribeScene(proofScene);
                return false;
            }

            return true;
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
            public int schemaVersion;
            public string toolSourceSha256;
            public string unityVersion;
            public string frameworkVersion;
            public string gitBranch;
            public string gitHead;
            public string proofScenePath;
            public string structuralOutcome;
            public string runtimeOutcome;
            public string recoveryState;
            public string recoveryDiagnostic;
            public string recoverySceneSha256;
            public string lastAttemptOutcome;
            public string lastAttemptDiagnostic;
            public bool sceneActivationSetterReturned;
            public bool activeSceneRestorationSucceeded;
            public bool selectionRestorationSucceeded;
            public bool proofSceneLifecycleRestored;
            public bool loadedSceneContextRestorationSucceeded;
            public string restorationDiagnostic;
            public SceneIdentitySnapshot requestedSceneBeforeActivation;
            public SceneIdentitySnapshot activeSceneAfterActivation;
            public SceneIdentitySnapshot creatorRootScene;
            public SceneContextSnapshot lastSceneContextBefore;
            public SceneContextSnapshot lastSceneContextAfter;
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
            public ValidatorResult[] lastAttemptValidators = Array.Empty<ValidatorResult>();
        }

        [Serializable]
        private sealed class SceneContextSnapshot
        {
            public int sceneCount;
            public SceneIdentitySnapshot[] loadedScenes;
            public SceneIdentitySnapshot activeScene;
        }

        [Serializable]
        private sealed class SceneIdentitySnapshot
        {
            public ulong handle;
            public string name;
            public string path;
            public bool isValid;
            public bool isLoaded;
            public bool isPreview;
            public bool isDirty;
            public bool isActive;
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
            public readonly string name;
            public readonly bool isPreview;
            public readonly bool wasDirty;

            public LoadedSceneState(Scene scene, string path, string name, bool isPreview, bool wasDirty)
            {
                this.scene = scene;
                this.path = path;
                this.name = name;
                this.isPreview = isPreview;
                this.wasDirty = wasDirty;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Immersive.Framework.Actors;
using Immersive.Framework.Camera;
using Immersive.Framework.CameraAuthoring;
using Immersive.Framework.PlayerParticipation;
using Immersive.Framework.PlayerSlots;
using Immersive.Framework.UnityInput;
using PEGA.Prototyping.PlayerPlaceholder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

internal static class PlayerActorPlaceholderAuthoringMenu
{
    private const string ScenePath = "Assets/FrameworkValidation/Scenes/FrameworkValidationScene.unity";
    private const string RootPath = "Assets/FrameworkValidation/PlayerPlaceholder";
    private const string PrefabPath = RootPath + "/PlayerActorPlaceholder.prefab";
    private const string MaterialPath = RootPath + "/PlayerVisualPlaceholder.mat";
    private const string MoveReferencePath = RootPath + "/PlayerMoveActionReference.asset";
    private const string RecordPath = RootPath + "/OriginalComposition.json";
    private const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string CameraAssignmentPath = "Assets/FrameworkValidation/Cameras/SessionCameraAssignment_PlayerP1.asset";
    private const string CameraOutputPath = "Assets/FrameworkValidation/Cameras/CameraDefault/CameraOutputMain.asset";
    private const string CameraOutputPrefabPath = "Assets/FrameworkValidation/Cameras/CameraDefault/CameraOutput_Main.prefab";

    [MenuItem("Immersive/Framework Validation/Player Placeholder/Build or Validate Actor")]
    private static void BuildOrValidate()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before authoring the Player Actor.");
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            {
                throw new InvalidOperationException(
                    $"Open and activate '{ScenePath}' before using this menu. Current scene='{scene.path}'.");
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Composition composition = Preflight(scene, prefabAsset != null);
            if (prefabAsset != null)
            {
                ValidateOriginalRecord(composition, allowPrefabObjectIdsToChange: true);
                ValidateCompletedComposition(composition, prefabAsset);
                Debug.Log(
                    $"[Player Placeholder] Existing prefab and Scene-Provided occurrence are valid. No changes made. Prefab='{PrefabPath}', ActorMount='{GlobalObjectId.GetGlobalObjectIdSlow(composition.Host.ActorMount)}'.");
                EditorUtility.DisplayDialog(
                    "Player Placeholder",
                    "The prefab and FrameworkValidationScene Actor occurrence are already composed and valid. No changes were made.",
                    "OK");
                return;
            }

            EnsureTargetsAbsent();
            ValidateOriginalRecord(composition, allowPrefabObjectIdsToChange: false);

            if (!EditorUtility.DisplayDialog(
                    "Build Player Actor Placeholder",
                    "This will create the reusable Actor prefab, material and Move action reference, then connect the existing Actor under its current ActorMount. The scene will be marked dirty but will not be saved. Review the Scene and prefab before saving.",
                    "Build",
                    "Cancel"))
            {
                return;
            }

            BuildComposition(composition);
        }
        catch (Exception exception)
        {
            Debug.LogError("[Player Placeholder] Authoring stopped before completion. " + exception);
            EditorUtility.DisplayDialog("Player Placeholder authoring stopped", exception.Message, "OK");
        }
    }

    private static Composition Preflight(Scene scene, bool completedPrefabExists)
    {
        SceneProvidedLocalPlayerAuthoring[] authorings = ComponentsInScene<SceneProvidedLocalPlayerAuthoring>(scene);
        if (authorings.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one Scene-Provided authoring in the active FrameworkValidationScene; found {authorings.Length}.");
        }

        SceneProvidedLocalPlayerAuthoring authoring = authorings[0];
        LocalPlayerHostAuthoring host = authoring.LocalPlayerHost;
        if (host == null || host.gameObject.scene != scene ||
            authoring.GetComponentInParent<LocalPlayerHostAuthoring>(true) != host)
        {
            throw new InvalidOperationException("Scene-Provided authoring does not reference its exact ancestral Local Player Host.");
        }

        PlayerInput playerInput = host.PlayerInput;
        UnityPlayerInputGateAdapter gate = host.GetComponent<UnityPlayerInputGateAdapter>();
        if (playerInput == null || playerInput.gameObject != host.gameObject ||
            gate == null || gate.PlayerInput != playerInput ||
            host.ActorMount == null || host.ActorMount.gameObject.scene != scene ||
            !host.ActorMount.IsChildOf(host.transform))
        {
            throw new InvalidOperationException(
                "The original Host, co-located PlayerInput, Input Gate or explicit ActorMount reference is incomplete.");
        }

        if (!authoring.TryGetPlayerSlotId(out PlayerSlotId _, out string slotIssue))
        {
            throw new InvalidOperationException("Scene-Provided Player Slot is invalid. " + slotIssue);
        }

        ActorProfile actorProfile = authoring.ActorProfile;
        if (actorProfile == null)
        {
            throw new InvalidOperationException("Scene-Provided authoring has no ActorProfile reference.");
        }

        if (!actorProfile.TryGetActorProfileId(out _, out string profileIssue) ||
            actorProfile.ActorKind != ActorKind.Player ||
            actorProfile.ActorRole != ActorRole.Protagonist ||
            actorProfile.VisualContentPrefab != null)
        {
            throw new InvalidOperationException(
                "The existing ActorProfile must be a valid Player Protagonist with no separate visual prefab. " + profileIssue);
        }

        Transform mount = host.ActorMount;
        if (mount.childCount != 1)
        {
            throw new InvalidOperationException(
                $"The existing ActorMount must have exactly one direct Actor root; found {mount.childCount} children.");
        }

        PlayerActorRuntimeHost[] runtimeHosts = mount.GetComponentsInChildren<PlayerActorRuntimeHost>(true);
        if (runtimeHosts.Length != 1 || runtimeHosts[0].transform.parent != mount ||
            runtimeHosts[0].GetComponents<PlayerActorRuntimeHost>().Length != 1 ||
            ComponentsInScene<PlayerActorRuntimeHost>(scene).Length != 1 ||
            ComponentsInScene<LocalPlayerHostAuthoring>(scene).Length != 1)
        {
            throw new InvalidOperationException(
                "ActorMount must own exactly one direct PlayerActorRuntimeHost and no nested alternatives.");
        }

        PlayerActorRuntimeHost actor = runtimeHosts[0];
        if (completedPrefabExists &&
            (!PrefabUtility.IsPartOfPrefabInstance(actor.gameObject) ||
             PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor.gameObject) != PrefabPath))
        {
            throw new InvalidOperationException(
                "The target prefab exists, but the existing Actor is not its connected instance. Refusing to replace the scene Actor.");
        }

        if (!completedPrefabExists && PrefabUtility.IsPartOfPrefabInstance(actor.gameObject))
        {
            throw new InvalidOperationException(
                "The inline Actor is already part of a prefab instance. Inspect its provenance before converting it.");
        }

        if (!actor.TryValidateConfiguration(out string actorIssue))
        {
            throw new InvalidOperationException("Existing PlayerActorRuntimeHost is invalid. " + actorIssue);
        }

        if (!host.TryValidateAdmissionConfiguration(actor, true, out string hostIssue))
        {
            throw new InvalidOperationException("Existing Local Player Host admission composition is invalid. " + hostIssue);
        }

        PlayerActorDeclaration declaration = actor.PlayerActorDeclaration;
        ActorCameraSubjectAuthoring subject = actor.GetComponent<ActorCameraSubjectAuthoring>();
        if (declaration == null || actor.GetComponentsInChildren<PlayerActorDeclaration>(true).Length != 1 ||
            declaration.PlayerInput != null || subject == null || subject.ObservationTransform == null ||
            subject.ObservationTransform == actor.transform || !subject.ObservationTransform.IsChildOf(actor.transform))
        {
            throw new InvalidOperationException(
                "The canonical Actor declaration or explicit in-Actor ObservationTransform is missing or ambiguous.");
        }

        if (!subject.TryValidateConfiguration(out string subjectIssue))
        {
            throw new InvalidOperationException("Existing Actor Camera Subject is invalid. " + subjectIssue);
        }

        SerializedObject declarationObject = new SerializedObject(declaration);
        SerializedProperty actorId = declarationObject.FindProperty("actorId");
        if (actorId == null || !string.IsNullOrEmpty(actorId.stringValue))
        {
            throw new InvalidOperationException(
                "The occurrence ActorId must be empty in serialized authoring; the tool will not persist or replace it.");
        }

        if (!completedPrefabExists && host.PlayerActorRuntimeHostPrefab != null)
        {
            throw new InvalidOperationException(
                "Host already references an Actor Runtime Host prefab, but no completed prefab was found at the target path.");
        }

        if (!completedPrefabExists &&
            (actor.GetComponent<PlayerGameplayInputReader>() != null ||
             actor.GetComponent<PlayerMovementPlaceholder>() != null ||
             actor.GetComponent<CharacterController>() != null ||
             actor.GetComponentInChildren<Collider>(true) != null ||
             actor.GetComponentInChildren<Renderer>(true) != null ||
             actor.GetComponentInChildren<PlayerInput>(true) != null ||
             actor.GetComponentInChildren<LocalPlayerHostAuthoring>(true) != null ||
             actor.GetComponentInChildren<SceneProvidedLocalPlayerAuthoring>(true) != null))
        {
            throw new InvalidOperationException(
                "The inline Actor already contains placeholder components, physics, visuals or provisioning ownership. Refusing to duplicate or replace them.");
        }

        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        InputAction move = actions != null ? actions.FindAction("Player/Move", false) : null;
        if (actions == null || move == null || move.expectedControlType != "Vector2" || playerInput.actions != actions)
        {
            throw new InvalidOperationException(
                "The existing PlayerInput and Input Actions asset must share the existing Player/Move Vector2 action.");
        }

        if (host.PlayerInput != playerInput || gate.PlayerInput != playerInput ||
            gate.GameplayActionMapName != "Player")
        {
            throw new InvalidOperationException("Input Gate and PlayerInput do not resolve to the existing Player action map.");
        }

        if (Shader.Find("Universal Render Pipeline/Lit") == null)
        {
            throw new InvalidOperationException("URP Lit shader is unavailable. No fallback material will be substituted.");
        }

        ValidateCameraAssignment(authoring.PlayerSlotProfile);

        if (completedPrefabExists && host.PlayerActorRuntimeHostPrefab !=
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<PlayerActorRuntimeHost>())
        {
            throw new InvalidOperationException("Host prefab provenance does not point to the existing reusable Actor prefab.");
        }

        return new Composition(scene, authoring, host, playerInput, gate, actor, declaration,
            subject, subject.ObservationTransform, authoring.PlayerSlotProfile, actorProfile,
            actions, move, actor.transform.localPosition, actor.transform.localRotation,
            actor.transform.localScale, subject.ObservationTransform.localPosition);
    }

    private static void BuildComposition(Composition composition)
    {
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Player Actor Placeholder");
        var createdAssets = new List<string>();
        try
        {
            CreateOriginalRecord(composition);
            InputActionReference moveReference = InputActionReference.Create(composition.MoveAction);
            if (moveReference == null)
            {
                throw new InvalidOperationException("Could not create an InputActionReference for the existing Player/Move action.");
            }

            moveReference.name = "PlayerMoveActionReference";
            AssetDatabase.CreateAsset(moveReference, MoveReferencePath);
            createdAssets.Add(MoveReferencePath);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = "PlayerVisualPlaceholder",
                color = new Color(0.12f, 0.52f, 0.78f, 1f)
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
            createdAssets.Add(MaterialPath);

            GameObject actor = composition.Actor.gameObject;
            Undo.RecordObject(actor, "Name Player Actor Placeholder");
            actor.name = "PlayerActorPlaceholder";

            CharacterController controller = Undo.AddComponent<CharacterController>(actor);
            Undo.RecordObject(controller, "Configure Placeholder Character Controller");
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.skinWidth = 0.08f;
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;

            PlayerGameplayInputReader inputReader = Undo.AddComponent<PlayerGameplayInputReader>(actor);
            PlayerMovementPlaceholder movement = Undo.AddComponent<PlayerMovementPlaceholder>(actor);
            SetObjectReference(movement, "moveAction", moveReference);
            SetFloat(movement, "movementSpeed", 4f);

            Undo.RecordObject(composition.ObservationTransform, "Position Actor Observation Transform");
            composition.ObservationTransform.localPosition = new Vector3(0f, 0.9f, 0f);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(visual, "Create Player Visual Placeholder");
            visual.name = "PlayerVisualPlaceholder";
            Undo.SetTransformParent(visual.transform, actor.transform, "Parent Player Visual Placeholder");
            Undo.RecordObject(visual.transform, "Configure Player Visual Placeholder Transform");
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(0.8f, 1.8f, 0.8f);

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Undo.DestroyObjectImmediate(visualCollider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer == null)
            {
                throw new InvalidOperationException("Unity Cube primitive did not provide the required Renderer.");
            }

            Undo.RecordObject(renderer, "Assign Player Visual Placeholder Material");
            renderer.sharedMaterial = material;

            if (controller == null || inputReader == null || movement == null)
            {
                throw new InvalidOperationException("One or more required Actor-local components could not be added.");
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                actor, PrefabPath, InteractionMode.UserAction, out bool saved);
            if (!saved || prefab == null)
            {
                throw new InvalidOperationException("Unity did not save and connect the Actor prefab.");
            }

            createdAssets.Add(PrefabPath);

            PlayerActorRuntimeHost prefabHost = prefab.GetComponent<PlayerActorRuntimeHost>();
            if (prefabHost == null)
            {
                throw new InvalidOperationException("Saved prefab is missing its root PlayerActorRuntimeHost.");
            }

            Undo.RecordObject(composition.Host, "Set Player Actor Runtime Host Prefab");
            SerializedObject hostObject = new SerializedObject(composition.Host);
            SerializedProperty prefabProperty = hostObject.FindProperty("playerActorRuntimeHostPrefab");
            if (prefabProperty == null)
            {
                throw new InvalidOperationException("Installed LocalPlayerHostAuthoring prefab property was not found.");
            }

            prefabProperty.objectReferenceValue = prefabHost;
            hostObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(composition.Host);

            ValidateCompletedComposition(composition, prefab);
            EditorSceneManager.MarkSceneDirty(composition.Scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                $"[Player Placeholder] Created prefab and connected the original Actor. Scene remains unsaved for review. Actor='{GlobalObjectId.GetGlobalObjectIdSlow(composition.Actor)}', Declaration='{GlobalObjectId.GetGlobalObjectIdSlow(composition.Declaration)}', Subject='{GlobalObjectId.GetGlobalObjectIdSlow(composition.Subject)}', Observation='{GlobalObjectId.GetGlobalObjectIdSlow(composition.ObservationTransform)}', ActorMount='{GlobalObjectId.GetGlobalObjectIdSlow(composition.Host.ActorMount)}'.");
            EditorUtility.DisplayDialog(
                "Player Placeholder created",
                "The prefab, material and Move action reference were created, and the existing Actor is now connected under the same ActorMount. The scene was not saved. Inspect the prefab and all references, then save the assets and scene when satisfied.",
                "OK");
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            foreach (string assetPath in createdAssets.AsEnumerable().Reverse())
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }

            AssetDatabase.Refresh();
            throw;
        }
    }

    private static void ValidateCompletedComposition(Composition composition, GameObject prefab)
    {
        PlayerActorRuntimeHost[] actors = composition.Host.ActorMount.GetComponentsInChildren<PlayerActorRuntimeHost>(true);
        if (actors.Length != 1 || actors[0] != composition.Actor ||
            composition.Actor.transform.parent != composition.Host.ActorMount ||
            !PrefabUtility.IsPartOfPrefabInstance(composition.Actor.gameObject) ||
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(composition.Actor.gameObject) != PrefabPath)
        {
            throw new InvalidOperationException("Expected exactly one connected prefab Actor under the original ActorMount.");
        }

        if (composition.Host.PlayerActorRuntimeHostPrefab != prefab.GetComponent<PlayerActorRuntimeHost>() ||
            composition.Authoring.LocalPlayerHost != composition.Host ||
            composition.Authoring.PlayerSlotProfile != composition.SlotProfile ||
            composition.Authoring.ActorProfile != composition.ActorProfile ||
            composition.Host.PlayerInput != composition.PlayerInput ||
            composition.Host.GetComponent<UnityPlayerInputGateAdapter>() != composition.Gate ||
            composition.Gate.PlayerInput != composition.PlayerInput ||
            composition.Actor.PlayerActorDeclaration != composition.Declaration ||
            composition.Declaration.PlayerInput != null ||
            composition.Actor.GetComponent<ActorCameraSubjectAuthoring>() != composition.Subject ||
            composition.Subject.ObservationTransform != composition.ObservationTransform)
        {
            throw new InvalidOperationException("An original Host, Input, Profile, declaration or Camera Subject reference changed.");
        }

        if (!composition.Subject.TryValidateConfiguration(out string subjectIssue))
        {
            throw new InvalidOperationException("Connected Camera Subject is invalid. " + subjectIssue);
        }

        if (!composition.Actor.TryValidateConfiguration(out string actorIssue))
        {
            throw new InvalidOperationException("Connected Actor Runtime Host is invalid. " + actorIssue);
        }

        PlayerActorDeclaration[] declarations = composition.Actor.GetComponentsInChildren<PlayerActorDeclaration>(true);
        PlayerGameplayInputReader[] readers = composition.Actor.GetComponentsInChildren<PlayerGameplayInputReader>(true);
        CharacterController[] controllers = composition.Actor.GetComponentsInChildren<CharacterController>(true);
        PlayerMovementPlaceholder[] movements = composition.Actor.GetComponentsInChildren<PlayerMovementPlaceholder>(true);
        if (declarations.Length != 1 || readers.Length != 1 || controllers.Length != 1 || movements.Length != 1 ||
            composition.Actor.GetComponentInChildren<PlayerInput>(true) != null ||
            composition.Actor.GetComponentInChildren<LocalPlayerHostAuthoring>(true) != null ||
            composition.Actor.GetComponentInChildren<SceneProvidedLocalPlayerAuthoring>(true) != null)
        {
            throw new InvalidOperationException("Actor must contain one declaration, reader, controller and movement script, without provisioning-specific components.");
        }

        SerializedObject declarationObject = new SerializedObject(composition.Declaration);
        SerializedObject movementObject = new SerializedObject(movements[0]);
        if (declarationObject.FindProperty("actorId").stringValue.Length != 0 ||
            movementObject.FindProperty("moveAction").objectReferenceValue !=
                AssetDatabase.LoadAssetAtPath<InputActionReference>(MoveReferencePath) ||
            composition.MoveAction.id != composition.Actions.FindAction("Player/Move", false).id)
        {
            throw new InvalidOperationException("ActorId must remain empty and movement must use the existing Move action reference.");
        }

        if (prefab.GetComponent<PlayerActorRuntimeHost>() == null ||
            prefab.GetComponentInChildren<PlayerInput>(true) != null ||
            prefab.GetComponentInChildren<LocalPlayerHostAuthoring>(true) != null ||
            prefab.GetComponentInChildren<SceneProvidedLocalPlayerAuthoring>(true) != null ||
            AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null)
        {
            throw new InvalidOperationException("Reusable prefab includes provisioning ownership or its visual material is missing.");
        }

        ActorCameraSubjectAuthoring prefabSubject = prefab.GetComponent<ActorCameraSubjectAuthoring>();
        Transform prefabObservation = prefabSubject != null ? prefabSubject.ObservationTransform : null;
        if (prefabSubject == null || prefabObservation == null || prefabObservation == prefab.transform ||
            !prefabObservation.IsChildOf(prefab.transform) ||
            prefab.GetComponent<PlayerGameplayInputReader>() == null ||
            prefab.GetComponent<CharacterController>() == null ||
            prefab.GetComponent<PlayerMovementPlaceholder>() == null)
        {
            throw new InvalidOperationException("Prefab root or explicit prefab Camera Subject composition is incomplete.");
        }

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length != 1 || renderers[0].sharedMaterial != AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) ||
            prefab.GetComponentsInChildren<Collider>(true).Length != 1 ||
            prefab.GetComponent<CharacterController>() == null)
        {
            throw new InvalidOperationException("Prefab placeholder visual/material or single CharacterController collision shape is invalid.");
        }

        InputActionReference moveReference = AssetDatabase.LoadAssetAtPath<InputActionReference>(MoveReferencePath);
        if (moveReference == null || moveReference.action == null || moveReference.action.id != composition.MoveAction.id)
        {
            throw new InvalidOperationException("Persisted PlayerMoveActionReference does not resolve to the existing Player/Move action GUID.");
        }
    }

    private static void CreateOriginalRecord(Composition composition)
    {
        if (File.Exists(ToAbsolutePath(RecordPath)))
        {
            ValidateOriginalRecord(composition, allowPrefabObjectIdsToChange: false);
            return;
        }

        composition.Authoring.TryGetPlayerSlotId(out PlayerSlotId slotId, out _);
        composition.ActorProfile.TryGetActorProfileId(out ActorProfileId actorProfileId, out _);

        var record = new OriginalCompositionRecord
        {
            unityVersion = Application.unityVersion,
            frameworkVersion = "1.1.0-preview.6",
            scenePath = composition.Scene.path,
            sceneGuid = AssetDatabase.AssetPathToGUID(composition.Scene.path),
            sceneProvidedAuthoring = GlobalObjectId.GetGlobalObjectIdSlow(composition.Authoring).ToString(),
            localPlayerHost = GlobalObjectId.GetGlobalObjectIdSlow(composition.Host).ToString(),
            playerInput = GlobalObjectId.GetGlobalObjectIdSlow(composition.PlayerInput).ToString(),
            inputGate = GlobalObjectId.GetGlobalObjectIdSlow(composition.Gate).ToString(),
            actorMount = GlobalObjectId.GetGlobalObjectIdSlow(composition.Host.ActorMount).ToString(),
            actorRoot = GlobalObjectId.GetGlobalObjectIdSlow(composition.Actor).ToString(),
            declaration = GlobalObjectId.GetGlobalObjectIdSlow(composition.Declaration).ToString(),
            cameraSubject = GlobalObjectId.GetGlobalObjectIdSlow(composition.Subject).ToString(),
            observationTransform = GlobalObjectId.GetGlobalObjectIdSlow(composition.ObservationTransform).ToString(),
            slotProfilePath = AssetDatabase.GetAssetPath(composition.SlotProfile),
            slotId = slotId.ToString(),
            actorProfilePath = AssetDatabase.GetAssetPath(composition.ActorProfile),
            actorProfileId = actorProfileId.ToString(),
            inputActionsPath = AssetDatabase.GetAssetPath(composition.Actions),
            moveActionGuid = composition.MoveAction.id.ToString(),
            cameraAssignmentPath = CameraAssignmentPath,
            cameraAssignmentGuid = AssetDatabase.AssetPathToGUID(CameraAssignmentPath),
            cameraOutputPath = CameraOutputPath,
            cameraOutputGuid = AssetDatabase.AssetPathToGUID(CameraOutputPath),
            actorPosition = composition.ActorPosition.ToString("F5"),
            actorRotation = composition.ActorRotation.eulerAngles.ToString("F5"),
            actorScale = composition.ActorScale.ToString("F5"),
            observationPosition = composition.ObservationPosition.ToString("F5"),
            actorIdWasEmpty = true
        };
        File.WriteAllText(ToAbsolutePath(RecordPath), JsonUtility.ToJson(record, true));
        AssetDatabase.Refresh();
    }

    private static void ValidateOriginalRecord(Composition composition, bool allowPrefabObjectIdsToChange)
    {
        string absolutePath = ToAbsolutePath(RecordPath);
        if (!File.Exists(absolutePath))
        {
            return;
        }

        OriginalCompositionRecord record = JsonUtility.FromJson<OriginalCompositionRecord>(File.ReadAllText(absolutePath));
        composition.Authoring.TryGetPlayerSlotId(out PlayerSlotId slotId, out _);
        composition.ActorProfile.TryGetActorProfileId(out ActorProfileId actorProfileId, out _);
        if (record == null ||
            record.sceneGuid != AssetDatabase.AssetPathToGUID(composition.Scene.path) ||
            record.sceneProvidedAuthoring != GlobalObjectId.GetGlobalObjectIdSlow(composition.Authoring).ToString() ||
            record.localPlayerHost != GlobalObjectId.GetGlobalObjectIdSlow(composition.Host).ToString() ||
            record.playerInput != GlobalObjectId.GetGlobalObjectIdSlow(composition.PlayerInput).ToString() ||
            record.inputGate != GlobalObjectId.GetGlobalObjectIdSlow(composition.Gate).ToString() ||
            record.actorMount != GlobalObjectId.GetGlobalObjectIdSlow(composition.Host.ActorMount).ToString() ||
            (!allowPrefabObjectIdsToChange &&
             (record.actorRoot != GlobalObjectId.GetGlobalObjectIdSlow(composition.Actor).ToString() ||
              record.declaration != GlobalObjectId.GetGlobalObjectIdSlow(composition.Declaration).ToString() ||
              record.cameraSubject != GlobalObjectId.GetGlobalObjectIdSlow(composition.Subject).ToString() ||
              record.observationTransform != GlobalObjectId.GetGlobalObjectIdSlow(composition.ObservationTransform).ToString())) ||
            record.actorProfilePath != AssetDatabase.GetAssetPath(composition.ActorProfile) ||
            record.slotProfilePath != AssetDatabase.GetAssetPath(composition.SlotProfile) ||
            record.slotId != slotId.ToString() ||
            record.actorProfileId != actorProfileId.ToString() ||
            record.moveActionGuid != composition.MoveAction.id.ToString() ||
            record.cameraAssignmentPath != CameraAssignmentPath ||
            record.cameraAssignmentGuid != AssetDatabase.AssetPathToGUID(CameraAssignmentPath) ||
            record.cameraOutputPath != CameraOutputPath ||
            record.cameraOutputGuid != AssetDatabase.AssetPathToGUID(CameraOutputPath) ||
            !record.actorIdWasEmpty)
        {
            throw new InvalidOperationException(
                "OriginalComposition.json does not match the current Actor composition and references. No authoring changes were made.");
        }
    }

    private static void ValidateCameraAssignment(PlayerSlotProfile expectedSlot)
    {
        SessionCameraAssignmentAsset assignment = AssetDatabase.LoadAssetAtPath<SessionCameraAssignmentAsset>(CameraAssignmentPath);
        CameraOutputDefinition output = AssetDatabase.LoadAssetAtPath<CameraOutputDefinition>(CameraOutputPath);
        GameObject outputPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraOutputPrefabPath);
        CameraOutputAuthoring[] outputAuthorings = outputPrefab != null
            ? outputPrefab.GetComponentsInChildren<CameraOutputAuthoring>(true)
            : Array.Empty<CameraOutputAuthoring>();
        if (assignment == null || output == null || !output.HasValidId || outputPrefab == null ||
            outputAuthorings.Length != 1 || outputAuthorings[0].OutputDefinition != output ||
            outputAuthorings[0].UnityCamera == null || outputAuthorings[0].CinemachineBrain == null)
        {
            throw new InvalidOperationException(
                "Existing Camera Output authoring/prefab is missing or its explicit Camera/Brain/Definition references are incomplete.");
        }

        if (!outputAuthorings[0].TryValidateDefinition(out string outputIssue))
        {
            throw new InvalidOperationException("Existing Camera Output definition is invalid. " + outputIssue);
        }

        if (!assignment.TryBuild(out _, out string assignmentIssue))
        {
            throw new InvalidOperationException("Existing Session Camera Assignment is invalid. " + assignmentIssue);
        }

        if (assignment.OutputDefinitions.Count != 1 || assignment.OutputDefinitions[0] != output)
        {
            throw new InvalidOperationException("Session Camera Assignment does not reference the expected explicit Output Definition.");
        }

        var serialized = new SerializedObject(assignment);
        SerializedProperty memberSlots = serialized.FindProperty("memberSlots");
        SerializedProperty outputs = serialized.FindProperty("outputDefinitions");
        SerializedProperty mappings = serialized.FindProperty("individualMemberOutputMappings");
        if (memberSlots == null || outputs == null || mappings == null ||
            !ContainsReference(memberSlots, expectedSlot) || !ContainsReference(outputs, output))
        {
            throw new InvalidOperationException("Camera Assignment Slot membership or Output reference differs from the existing Player P1 composition.");
        }

        bool hasExpectedMapping = false;
        for (int index = 0; index < mappings.arraySize; index++)
        {
            SerializedProperty mapping = mappings.GetArrayElementAtIndex(index);
            if (mapping.FindPropertyRelative("playerSlotProfile")?.objectReferenceValue == expectedSlot &&
                mapping.FindPropertyRelative("outputDefinition")?.objectReferenceValue == output)
            {
                hasExpectedMapping = true;
                break;
            }
        }

        if (!hasExpectedMapping)
        {
            throw new InvalidOperationException("Camera Assignment has no explicit P1-to-Output mapping.");
        }
    }

    private static bool ContainsReference(SerializedProperty array, UnityEngine.Object expected)
    {
        for (int index = 0; index < array.arraySize; index++)
        {
            if (array.GetArrayElementAtIndex(index).objectReferenceValue == expected)
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureTargetsAbsent()
    {
        string[] targets = { PrefabPath, MaterialPath, MoveReferencePath };
        foreach (string target in targets)
        {
            if (File.Exists(ToAbsolutePath(target)) ||
                File.Exists(ToAbsolutePath(target + ".meta")) ||
                AssetDatabase.LoadMainAssetAtPath(target) != null)
            {
                throw new InvalidOperationException($"Partial authoring target exists at '{target}'. Inspect it before continuing.");
            }
        }
    }

    private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        Undo.RecordObject(target, "Set Player Move Action Reference");
        var serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException($"Serialized field '{propertyName}' was not found on '{target.GetType().Name}'.");
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedProperties();
    }

    private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
    {
        Undo.RecordObject(target, "Set Player Movement Speed");
        var serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException($"Serialized field '{propertyName}' was not found on '{target.GetType().Name}'.");
        }

        property.floatValue = value;
        serializedObject.ApplyModifiedProperties();
    }

    private static T[] ComponentsInScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static string ToAbsolutePath(string assetPath) =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath.Replace('/', Path.DirectorySeparatorChar)));

    private sealed class Composition
    {
        public Composition(Scene scene, SceneProvidedLocalPlayerAuthoring authoring,
            LocalPlayerHostAuthoring host, PlayerInput playerInput, UnityPlayerInputGateAdapter gate,
            PlayerActorRuntimeHost actor, PlayerActorDeclaration declaration,
            ActorCameraSubjectAuthoring subject, Transform observationTransform,
            PlayerSlotProfile slotProfile, ActorProfile actorProfile,
            InputActionAsset actions, InputAction moveAction,
            Vector3 actorPosition, Quaternion actorRotation, Vector3 actorScale,
            Vector3 observationPosition)
        {
            Scene = scene; Authoring = authoring; Host = host; PlayerInput = playerInput; Gate = gate;
            Actor = actor; Declaration = declaration; Subject = subject; ObservationTransform = observationTransform;
            SlotProfile = slotProfile; ActorProfile = actorProfile; Actions = actions; MoveAction = moveAction;
            ActorPosition = actorPosition; ActorRotation = actorRotation; ActorScale = actorScale;
            ObservationPosition = observationPosition;
        }

        public Scene Scene { get; }
        public SceneProvidedLocalPlayerAuthoring Authoring { get; }
        public LocalPlayerHostAuthoring Host { get; }
        public PlayerInput PlayerInput { get; }
        public UnityPlayerInputGateAdapter Gate { get; }
        public PlayerActorRuntimeHost Actor { get; }
        public PlayerActorDeclaration Declaration { get; }
        public ActorCameraSubjectAuthoring Subject { get; }
        public Transform ObservationTransform { get; }
        public PlayerSlotProfile SlotProfile { get; }
        public ActorProfile ActorProfile { get; }
        public InputActionAsset Actions { get; }
        public InputAction MoveAction { get; }
        public Vector3 ActorPosition { get; }
        public Quaternion ActorRotation { get; }
        public Vector3 ActorScale { get; }
        public Vector3 ObservationPosition { get; }
    }

    [Serializable]
    private sealed class OriginalCompositionRecord
    {
        public string unityVersion;
        public string frameworkVersion;
        public string scenePath;
        public string sceneGuid;
        public string sceneProvidedAuthoring;
        public string localPlayerHost;
        public string playerInput;
        public string inputGate;
        public string actorMount;
        public string actorRoot;
        public string declaration;
        public string cameraSubject;
        public string observationTransform;
        public string slotProfilePath;
        public string slotId;
        public string actorProfilePath;
        public string actorProfileId;
        public string inputActionsPath;
        public string moveActionGuid;
        public string cameraAssignmentPath;
        public string cameraAssignmentGuid;
        public string cameraOutputPath;
        public string cameraOutputGuid;
        public string actorPosition;
        public string actorRotation;
        public string actorScale;
        public string observationPosition;
        public bool actorIdWasEmpty;
    }
}

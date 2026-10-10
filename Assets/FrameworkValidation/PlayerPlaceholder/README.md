# Player Placeholder

PEGA's reusable Actor prototype supplies a simple visible body and horizontal
locomotion for validating the Framework Player path. It is not production
gameplay.

## Framework contracts and temporary behavior

The prefab composes real Framework contracts: `PlayerActorRuntimeHost`,
`PlayerActorDeclaration`, `PlayerGameplayInputReader`,
`ActorCameraSubjectAuthoring` and its explicit `ObservationTransform`. A
`CharacterController` owns physical collision. The declaration's authored
`ActorId` is empty; occurrence identity belongs to the Framework at runtime.

`PlayerMovementPlaceholder`, the cube visual and its material are provisional.
There is no jump, gravity, sprint, animation, combat or pause implementation.
The Actor prefab contains no `PlayerInput`, Local Player Host, Player Session or
Scene-Provided authoring component.

## Prefab composition

`PlayerActorPlaceholder.prefab` has one Actor root with the Framework declaration
and runtime host, Camera Subject, `PlayerGameplayInputReader`,
`PlayerMovementPlaceholder` and `CharacterController`. Its child
`ObservationTransform` is explicitly referenced by the Camera Subject. Its child
`PlayerVisualPlaceholder` is a Unity cube using `PlayerVisualPlaceholder.mat`;
the cube's collider is removed so the root CharacterController is the single
movement collision shape.

`PlayerMoveActionReference.asset` points to the existing `Player/Move` action in
`Assets/InputSystem_Actions.inputactions`. The movement script reads it through
the Framework reader, which resolves the current occurrence's `PlayerInput`
action by action identity and refuses reads unless that occurrence is bound,
ready and gameplay input is available. No parallel device polling or action map
ownership is introduced. WASD and the action's other existing bindings move on
world X/Z; diagonal input is normalized; speed defaults to 4 units per second.

## SampleScene: Scene-Provided

`Assets/Scenes/SampleScene.unity` keeps the original Scene-Provided authoring,
Local Player Host, co-located `PlayerInput`, Input Gate, P1 Slot Profile,
`ActorProfile` and Activity/camera configuration. Its one Actor is a connected
instance of this prefab under the Host's explicit `ActorMount`. The Host's
`PlayerActorRuntimeHostPrefab` references the same prefab as provenance. The
Actor Profile does not supply a second visual prefab.

The original composition is recorded in `OriginalComposition.json`, including
the Host/Input/Gate/Mount references, Slot and Actor Profile identities, camera
assignment/output identities and the pre-conversion Actor/Subject object IDs.

## Editor authoring tool

`Editor/PlayerActorPlaceholderAuthoringMenu.cs` provides
**PEGA > Player Placeholder > Build or Validate SampleScene Actor**. It is a
project-specific rebuild/validation tool, not runtime infrastructure. It checks
the active scene and existing contracts, verifies the P1 camera assignment and
Output references, and asks before first authoring. It uses Unity Prefab/Scene
and Undo APIs, never edits serialized YAML directly, never saves the scene, and
does not alter GameApplication, Route, Activity, Build Settings or package
assets. A complete composition follows the tool's no-op validation branch on a
subsequent menu run; that second menu execution has not been performed for this
consolidation. Partial or ambiguous states stop with a diagnostic. The original
composition record is retained because the tool uses it to validate provenance
on later runs.

## Validation status

Static inspection confirms one Actor prefab instance under the original
`ActorMount`, matching Host prefab provenance, an empty authored ActorId, the
expected Actor-local components and action reference, and retained Scene-
Provided Host/Input/Gate/Profile references. The P1 camera assignment maps its
Slot to the existing Output. Static inspection does not certify compilation,
camera framing or lifecycle behavior.

The user reported a successful first Play Mode: Boot Succeeded, Player Session
initialized, Scene-Provided admitted, Actor adopted, Activity Ready and Camera
Output Ready. The reported `PlayerPauseInput`-absent warning is non-blocking for
movement and remains a Pause-specific follow-up; no Pause component was added.
Those runtime log lines were not present in the workspace files inspected for
this consolidation. A second Play Mode, cleanup/re-admission and restart check
remain **PENDING**. Manager-Provisioned authoring and runtime are intentionally
not configured or validated.

## Manual lifecycle check

1. Start Play Mode through the normal GameApplication/Route/Activity flow and
   confirm Boot, Session admission, Actor adoption, Activity readiness and
   Camera Output readiness.
2. Move with WASD. Confirm the visual follows the Actor, the CharacterController
   collides with the sample floor, and movement stops while gameplay input is
   unavailable.
3. Exit Play Mode fully, then start a second Play Mode. Confirm there is still
   one Actor under the Host, input and camera work again, and no stale binding
   or duplicate occurrence appears.
4. If the project exposes its supported Activity cleanup/re-admission path,
   exercise it and confirm the same one-Actor/input/camera invariants.
5. Run the Editor authoring menu again outside Play Mode; it should validate the
   complete composition without changes.

## Future Manager-Provisioned use and replacement

The same Actor prefab can be referenced by a separately authored
Manager-Provisioned Local Player Host. That Host will need its own `PlayerInput`,
`LocalPlayerHostAuthoring`, Input Gate, explicit `ActorMount` and
`PlayerActorRuntimeHostPrefab` reference, plus the Framework's Manager-Provisioned
authority and Actor selection/profile configuration. These Host/session settings
are specific to provisioning; movement, visual and Actor-owned camera subject
stay in the reusable prefab. Manager-Provisioned remains unconfigured until its
own implementation task.

When production presentation or locomotion replaces this prototype, replace the
cube/material and `PlayerMovementPlaceholder` while preserving the Actor
declaration/runtime host, Actor-local input reader contract, CharacterController
if still appropriate, and explicit Camera Subject/Observation Transform
relationship. Keep `ActorProfile.VisualContentPrefab` empty while presentation
remains authored inside this Actor prefab, to avoid duplicate visual content.

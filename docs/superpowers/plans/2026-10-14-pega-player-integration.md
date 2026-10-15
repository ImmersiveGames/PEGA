# PEGA Minimal Player Integration Implementation Plan

> **For agentic workers:** When executing this plan, use `superpowers:executing-plans` and work through the steps in order. Preserve the user's existing working-tree changes.

**Goal:** Integrate one Manager-Provisioned P1 into the PEGA Mission Activity, materialize a default Player Actor with Rick's visual presentation, and verify Session join/leave plus the gameplay input gate.

**Architecture:** Keep Session configuration in ScriptableObject profiles; register one persistent provisioning surface under the existing GameApplication composition; let the Framework create the technical Local Player Host and Actor runtime; keep Activity-owned Join/Leave controls, participation requirement, relocation anchor, and input-gate diagnostics in Mission Activity content. The generic Actor runtime owns occurrence identity and contextual gameplay binding; Rick is subordinate visual content.

**Tech Stack:** Unity 6000.5.0f1, PEGA's resolved `com.immersive.framework` 1.1.0 (revision `c96227afccdb510b9d6bf5603d58f2ea8d126292`), Unity Input System, existing PEGA `Player`/`UI` action maps, UGUI.

**Spec:** Trello PEGA card #75 (`[MC-01] Integrar Player/Actor do Framework na missão`) and its acceptance checklist; current consumer package `Documentation~/Guides/Player-Usage.md`, `Documentation~/Guides/Activity-Readiness.md`, and `Documentation~/API/Public-API.md`. The FirstGame sample is a structural reference only: it resolves to a different 1.1.0 package artifact (`10aeb459df42...`), so the installed PEGA package defines the applicable contract. `PlayerSessionJoinTrigger` is Stable for technical Join/Host commit; Manager-Provisioned setup, Session/Actor configuration, Actor preparation, Activity GameplayReady, and Leave remain Experimental.

## Global Constraints

- Do not edit GDD, MVP, TASKS, ADR, Trello, GitHub, GanttFlow, Immersive package sources, or the input-actions asset in this cut.
- Preserve all pre-existing dirty, deleted, and untracked work. In particular, treat the current PEGA art/face and Input System changes as user-owned.
- Do not update package versions or copy FirstGame prefabs/assets. Use the resolved PEGA 1.1.0 package APIs and the existing PEGA action asset.
- Keep this to one P1, one default Actor, Session/Activity composition, an explicit Join path, and gate diagnostics. Do not add selection UI, a second Player, movement/collision, a final camera assignment, pause, or framework changes.
- Do not run Unity build, tests, Play Mode, smoke or batchmode. The user approved a temporary Editor authoring helper; normal Editor import/compile is allowed only to execute that helper. Remove the helper after authoring. The user performs runtime validation below.

## Review Focus

- Exactly one P1 Slot is admitted and resolves its authored default Actor Profile; no hidden Actor or Slot fallback.
- The Session provisioning endpoint exists once in the GameApplication-owned persistent composition. The Host prefab is the same explicit prefab referenced by provisioning and `PlayerInputManager`.
- The Host owns `PlayerInput`, the `Player` action-map gate adapter, and an empty `ActorMount`; the Actor runtime prefab owns the canonical `PlayerActorDeclaration` with empty authored occurrence ID and contains no `PlayerInput`.
- Join is requested through the Activity-scoped `PlayerSessionJoinTrigger`; `Joined` is not treated as proof of Actor preparation or `GameplayReady`.
- The visual prefab is presentation-only and does not become Actor identity, physical state, or camera authority.
- Existing GameApplication camera output, FOUND-01 route/activity behavior, input asset, and unrelated working-tree changes remain intact.

---

## Ordered Execution Plan

### Task 1 — Reconfirm the PEGA consumer state and target compositions

- [x] Read the current `PEGAApplication.asset`, `PersistentContent.unity`, `MissionRoute.asset`, `MissionActivity.asset`, and `MissionActivityContent.unity` immediately before editing; preserve the existing FOUND-01/GameFlow and camera-output references.
- [x] Recheck the resolved package fingerprint/revision and the exact serialized fields in its installed validators/Inspectors. Keep using PEGA's installed 1.1.0 artifact; do not infer parity from FirstGame's different artifact hash.
- [x] Inspect the Rick source prefab and its dependencies. Build a PEGA-owned visual-only presentation prefab around the existing Rick visual hierarchy; keep `Assets/PEGA/Arts/3D Models/Characters/Player/Rick Ronda/Male_Security_01.prefab` unchanged and exclude any test-only behavior from the runtime presentation.
- [x] Confirm whether the persistent composition already has its canonical EventSystem/UI input module. If absent, add exactly one EventSystem plus Input System UI module to `PersistentContent.unity`, wired to the existing `UI` action map; do not create another EventSystem in Activity content.

**Files:** Read-only preflight on the files listed above, the PEGA Input System action asset, the Rick source prefab, and resolved framework authoring/validation sources. No edits in this step.

### Task 2 — Author P1, Session, Actor, and visual profiles

- [x] Create `Assets/PEGA/Players/Prefabs/PF_PEGA_PlayerPresentation_Rick.prefab` as a visual-only composition using the existing Rick art, without editing the source art prefab.
- [x] Create `Assets/PEGA/Players/Profiles/PEGA_PlayerDefault_ActorProfile.asset` as Player/Protagonist and reference the Rick presentation prefab. Do not create a character-selection list.
- [x] Create `Assets/PEGA/Players/Profiles/PEGA_P1_PlayerSlotProfile.asset` with the single stable P1 identity and the default Actor Profile above.
- [x] Create `Assets/PEGA/Players/Profiles/PEGA_PlayerSessionProfile.asset` with exactly that supported Slot, initial joining open, Manager-Provisioned provisioning, and configured-default Actor resolution.

**Files:** Create the four assets above, persistent `InputActionReference` assets for the existing UI map, and their Unity-generated `.meta` files. Use explicit serialized profile references; do not store runtime Session or Actor occurrence state in these assets.

### Task 3 — Create the Actor runtime and technical Local Player Host prefabs

- [x] Create `Assets/PEGA/Players/Prefabs/PF_PEGA_PlayerActorRuntime.prefab` with one root `PlayerActorRuntimeHost`, one canonical `PlayerActorDeclaration` whose authored occurrence ID is empty, a visual-content mount, and the `PlayerGameplayInputReader` endpoint required by the #75 checklist. Add no `PlayerInput`, movement, collision, or camera components.
- [x] Create `Assets/PEGA/Players/Prefabs/PF_PEGA_LocalPlayerHost.prefab` with one `PlayerInput` using the existing PEGA input-actions asset and `Player` map; add `LocalPlayerHostAuthoring`, `UnityPlayerInputGateAdapter` targeting that same `PlayerInput` and `Player` map, and an empty child `ActorMount`.
- [x] Set `LocalPlayerHostAuthoring.PlayerActorRuntimeHostPrefab` to the Actor runtime prefab. Keep Actor placement and occurrence identity out of the reusable Host and visual assets.

**Files:** Create the two prefabs and their Unity-generated `.meta` files. No permanent C# source is expected. The user approved a temporary Unity Editor authoring helper; remove it and its generated `.meta` after authoring.

### Task 4 — Add the persistent Manager-Provisioned endpoint and enable the Session

- [x] Create `Assets/PEGA/Players/Prefabs/PF_PEGA_LocalPlayerProvisioning.prefab` with one `PlayerInputManager` configured for `JoinPlayersManually`, `InvokeCSharpEvents`, and a one-player cap; set `playerPrefab` to `PF_PEGA_LocalPlayerHost`; add `LocalPlayerProvisioningAuthoring` pointing to that same Host; and add one `LocalPlayerProvisioningEndpointRegistration` explicitly referencing the authoring component. The Activity Join trigger remains the sole authored Join request path.
- [x] Add one instance of that provisioning prefab to the existing GameApplication-owned persistent composition in `Assets/PEGA/Scenes/PersistentContent.unity`; add the canonical EventSystem/UI input module here only if the preflight found it absent. Do not put the provisioning authority in Activity content.
- [x] Enable Player Session on `Assets/PEGA/PEGAApplication.asset` and assign `PEGA_PlayerSessionProfile.asset`. Preserve the existing camera output, persistent content, route, and all unrelated settings.

**Files:** Create the provisioning prefab and `.meta`; modify `Assets/PEGA/Scenes/PersistentContent.unity` and `Assets/PEGA/PEGAApplication.asset` only for the explicit provisioning/Session references above.

### Task 5 — Compose P1 participation, relocation, Join/Leave, and gate diagnostics in Mission Activity

- [x] Configure `Assets/PEGA/GameFlow/MissionActivity.asset` for `ExplicitSlots` containing only P1, reject a zero-participant projection, require `GameplayReady`, and apply explicit relocation. These participation behavior settings are Experimental in the resolved package.
- [x] In `Assets/PEGA/Scenes/MissionActivityContent.unity`, add a small development Canvas with one `PlayerSessionJoinTrigger` explicitly serialized with `scope=Activity`, connected to a `Join P1` Button's `OnClick → Invoke()` UnityEvent; this is a Join control, not Actor selection UI. The resolved package defaults this trigger to `Unspecified`, which rejects the request. Reuse the persistent EventSystem.
- [x] Add one `PlayerSessionLeaveCommandTrigger` explicitly serialized with `scope=Activity` and targeting the P1 Slot Profile, plus a `Leave P1` development Button so manual Play Mode validation can cover Leave/Rejoin. Preserve the package's Experimental maturity note for Leave.
- [x] Add an explicit P1 relocation anchor and one `ActivityPlayerRelocationAuthoring` binding from the exact Mission Activity and P1 Slot to that anchor.
- [x] Add one `PlayerGameplayAvailabilityBlockTrigger` explicitly serialized with `scope=Activity` for P1. Use its package Inspector runtime controls to request/release a block and verify the Host's canonical gate adapter; do not toggle `PlayerInput` or the action map directly.
- [x] Keep the FOUND-01 diagnostic object and current Activity content intact. Do not add locomotion or a final camera assignment.

**Files:** Modify `Assets/PEGA/GameFlow/MissionActivity.asset` and `Assets/PEGA/Scenes/MissionActivityContent.unity`. No gameplay C# is expected; the buttons use public parameterless trigger methods through UGUI UnityEvents.

### Task 6 — Validate the composed integration in Unity (manual, user-run)

- [ ] Open the PEGA project in Unity 6000.5.0f1 and confirm import/compile completes without errors.
- [ ] Run the GameApplication, Session Profile, Slot Profile, Actor Profile, provisioning, Host, and Activity validators. Confirm one provisioning endpoint, one supported Slot, exact cross-references, valid `Player` map, empty Host Actor Mount, and valid relocation binding.
- [ ] Enter the PEGA Mission Activity, press `Join P1`, and confirm one Session Host and one prepared P1 Actor appear at the P1 relocation anchor with the Rick visual. Confirm the Activity reaches its authored readiness requirement; inspect diagnostics if it does not.
- [ ] Use the `PlayerGameplayAvailabilityBlockTrigger` Inspector controls to request a block and release it. Confirm only the configured P1 gameplay map is gated/restored by the adapter, and no component directly mutates `PlayerInput` as a workaround.
- [ ] Press `Leave P1`, then `Join P1`; confirm the old occurrence is released and exactly one new Host/Actor exists. Repeat Play Mode enter/exit and confirm no duplicate persistent Host or Actor remains.
- [ ] Confirm there is no Actor selection UI, second Player, movement/collision behavior, or definitive camera assignment in this cut. Record Unity version, resolved package revision, validator output, and relevant logs as delivery evidence.

## Risks and limitations

- The Stable contract covers only the bounded `PlayerSessionJoinTrigger` request and technical Session/Host commit. Manager-Provisioned setup, profile configuration, Actor preparation, Activity readiness/relocation, and Leave are Experimental; the manual run must report observed results without promoting those APIs' maturity.
- `GameplayReady` confirms the Framework's contextual gameplay projection; it does not prove movement, camera, or complete presentation behavior. Those remain later tasks (#34/#29 and subsequent work).
- The Trello #75 checklist still shows FOUND-01 as incomplete even though card #79 is in Concluído; local `PEGA GDD/TASKS.md` also retains an unchecked marker. Do not change either source in this implementation. Record the discrepancy for separate production-governance reconciliation.
- The temporary authoring helper ran through the normal Unity Editor import/compile path; no Play Mode or runtime acceptance was performed. The manual Unity validation above remains required.


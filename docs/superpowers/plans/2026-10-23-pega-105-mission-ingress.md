# #105 Mission Ingress Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Spawn one PEGA burglar into Mission through a physical Spawn Point and let the accepted ingress create the Gang-shared potential escape fact.

**Architecture:** A PEGA `ActivityContentBehaviour` owns one spawned Actor and drives only its initial ingress through the existing shared NavMesh/CharacterController movement. It joins the Gang before movement, confirms the Spawn Point fact, then publishes the NonPlayer occurrence under the exact committed Activity owner and enables the existing perception/autonomy. The Framework remains an occurrence publisher, not the spawn/movement owner.

**Tech Stack:** Unity 6.5, PEGA C#, Immersive Framework Actor Occurrence Publication, `ActivityContentBehaviour`, `RuntimeContentOwner`, `PEGANpcGangKnowledgeState`, `PEGANpcSpawnPoint`, `PEGANpcNavMeshMovementIntentSource`, `PEGAActorMovementMechanic`, canonical PEGA logger, temporary Native Editor authoring with `PrefabUtility`.

**Spec:** `docs/superpowers/specs/2026-10-23-pega-105-mission-ingress-design.md`

## Global Constraints

- Do not edit Framework package source or serialized Route/Activity assets.
- Do not hand-edit Unity scene or prefab YAML.
- Preserve existing Mission actor positions and physical geometry.
- Do not implement temporal recipes, GOAP plans, escape Actions, or knowledge injection.
- Do not rerun the already accepted 34-check gang-knowledge smoke.
- Do not run Unity build/import/tests/Play Mode/batchmode in this session.
- Temporary authoring/observation scripts are removed only after the user's Unity acceptance.

## Review Focus

- Owner mismatch or unavailable Activity publication scope prevents publication: use only `RuntimeContentOwner.Activity` for `context.Activity`.
- Spawn Safe is not on NavMesh or physical path is blocked: fail closed, report route/physical outcome separately, and destroy the unpublished Actor.
- Compound colliders invoke multiple trigger callbacks: retain the Spawn Point's Actor-level contact aggregation; do not count callbacks as multiple entries.
- Knowledge event is absent despite movement success: do not enable Perception/Autonomy or publish; report as failed ingress.
- Activity exit during ingress: cancel the exact navigation request and unpublish only with the owned publication token.

---

### Task 1: Activity-owned physical ingress producer

**Files:**
- Create: `Assets/PEGA/NPC/PEGANpcMissionSpawnEntry.cs`
- Temporary observation: `Assets/PEGA/NPC/Temporary/PEGANpcMissionSpawnEntryValidationProbe.cs`

**Interfaces:**
- Consumes: `ActivityContentLifecycleContext.Activity`, `ActorOccurrencePublicationScope`, `ActorOccurrencePublication`, `PEGANpcGangKnowledgeState.TryJoinMember`, `PEGANpcSpawnPoint`, and the Actor-root `PEGANpcNavMeshMovementIntentSource`.
- Produces: one owned runtime Actor, exact Activity publication token, and lifecycle logs for spawned, physically entered/known, published, failed, cancelled, and released states.

- [x] Add the passive Unity validation observer; it reads one terminal ingress snapshot and reports route, physical displacement, membership, Gang knowledge, publication, Perception and Autonomy. It cannot move Actors, write facts or command Actions.
- [x] Implement `PEGANpcMissionSpawnEntry` as a single-entry lifecycle owner. It joins the Actor to the Gang before movement, suppresses Perception/Autonomy during transit, uses `TryBeginNavigation`, verifies physical completion plus the typed Gang Target, publishes with the exact Activity owner, then enables existing Perception/Autonomy.
- [x] On failures and Activity exit, cancel the owned navigation request, unpublish with the exact token if published, and release only the Actor instance created by this producer.
- [x] Keep canonical diagnostics event-based; the observer polls only to read the final snapshot and emits no per-frame logs.

### Task 2: Idempotent Native Mission authoring

**Files:**
- Create: `Assets/PEGA/NPC/Editor/PEGANpcMissionIngressAuthoring.cs`
- Create via Unity: one dedicated ingress Actor prefab variant with a unique ActorId.
- Modify via Unity only: `Assets/PEGA/Scenes/MissionActivityContent.unity`.

**Interfaces:**
- Uses a one-time, narrow Editor menu operation. It targets the exact Mission Activity Content scene and its existing two `PEGANpcAutonomy` roots; it refuses dirty/ambiguous scenes and saves only after every reference/cardinality check passes.
- Creates/reuses one `ActivityContentContribution`, `ActorOccurrencePublicationScope`, `PEGANpcGangKnowledgeState`, one north-entry `PEGANpcSpawnPoint`, safe/inside anchors, and membership links for the two existing NPCs. It creates a variant of the existing NPC base prefab with a distinct stable ActorId, and wires the ingress producer.

- [x] Implement a preflight for the exact loaded, saved Mission Activity scene, exact Activity asset, two current NPC roots, stable ActorId, unique Spawn Point id and dedicated Prefab variant before scene authoring.
- [x] Implement the Native authoring operation with `Undo`, `SerializedObject`, `PrefabUtility`, `EditorSceneManager`, and `AssetDatabase`; preserve current NPC Transforms and roll back without saving if postflight fails.
- [x] Make a second invocation validate and reuse the persisted composition without adding objects/components or changing NPC positions.
- [ ] Run `Tools > PEGA > Temporary Validation > Prepare #105 Mission Ingress` in Unity and confirm the authoring postflight/save.

### Task 3: Static review and Unity handoff

**Files:**
- Modify: this plan only if implementation facts require a ruling.

- [x] Run static whitespace/status checks and inspect new source while preserving pre-existing scene and NAV edits.
- [ ] Do not claim compile/import/runtime success without Unity evidence.
- [x] Give the user the exact authoring/observer commands, Play Mode start path, and canonical log events to inspect.
- [ ] Keep the #105 legacy smoke and authoring/observer scripts until the new ingress receives Unity acceptance; then remove only the temporary scripts and retain definitive runtime and authored Mission/prefab assets.

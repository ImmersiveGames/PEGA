# PEGA NPC Knowledge State Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved PEGA-owned NPC knowledge store for #68, with subject-scoped identity evidence, deterministic fact merging, and no hidden-world reads.

**Architecture:** `PEGANpcKnowledgeState` owns transient knowledge on each NPC root. Typed identity facts are keyed by Immersive `ActorId` and store recognized `ActorProfileId`; radial presence stays anonymous, and Rick contact/last-position facts remain explicit and independently queryable. A private generic reducer applies the approved timestamp and same-time conflict policy; no perception, Action, GOAP, sharing, or Framework code is added.

**Tech Stack:** Unity 6000.5.0f1, C#, resolved `com.immersive.framework` 1.2.0-preview.4 (`ActorId`/`ActorProfileId`, Experimental), PEGA `PEGALogging`.

**Spec:** `docs/superpowers/specs/2026-10-19-pega-npc-knowledge-state-design.md`

## Global Constraints

- Keep #68 independently acceptable without #69 perception or #91 GOAP integration.
- State records only supplied evidence; it does not inspect target declarations, profiles, Transforms, occurrences, or World State.
- Use observation time, not arrival order; equal-time disagreements invalidate only the same `(ActorId, fact)`.
- Keep facts in memory, with no TTL, serialization, sharing, or general inference registry.
- Do not hand-edit prefab or scene YAML. Use the existing `Assets/PEGA/Scenes/MissionActivityContent.unity`, which already has two NPCs. The user authors the permanent Knowledge State and temporary debug component manually; do not create a separate scene or automatic attachment command.
- Do not run Unity build/import/tests, Play Mode, smoke, or batchmode.
- Preserve existing user changes in the NPC movement source, runtime prefab, mission scene, package lock, and untracked `Assets/PEGA/Actors.meta`.

## Review Focus

1. Invalid/default `ActorId` or `ActorProfileId` and invalid evidence timestamps are rejected visibly.
2. Rick and Petra profile evidence under different subject IDs at the same time remains independent.
3. Different profile identities for the same subject and timestamp invalidate only that subject, in either arrival order.
4. Older evidence cannot replace newer state; identical same-time evidence unions origins idempotently.
5. Losing Rick contact records `Known(false)` and retains the last known position without reading the target Transform.

---

### Task 1: Implement typed NPC knowledge state

**Files:**
- Create: `Assets/PEGA/NPC/PEGANpcKnowledgeState.cs`
- Create: `Assets/PEGA/NPC/PEGANpcKnowledgeState.cs.meta`

**Interfaces:**
- Consumes: `Immersive.Framework.Actors.ActorId`, `ActorProfileId`, `PEGA.Diagnostics.PEGALogging`, `Immersive.Logging.Records.LogFields`.
- Produces: public read-only fact snapshots and narrowly typed evidence/invalidation operations for perceived-area presence, per-ActorId character identity, Rick visual contact, and last observed Rick position.

- [x] Add closed status/origin/invalidation enums and immutable generic fact snapshots. Keep the mutable reducer private to PEGA NPC knowledge.
- [x] Implement timestamp ordering, same-time equal-value merge, conflict invalidation, explicit invalidation, merged origin/cause sets, exact Vector3 equality, and clear rejection results/logging.
- [x] Implement the component with one anonymous radial-presence fact and subject-keyed typed identity/contact/position state. Keep the component transient and free of target reads.
- [x] Run static diff and source-contract checks only; automated and Unity tests are not part of this execution.

### Task 2: Author the runtime NPC composition

**Files:**
- Modify through Unity Editor authoring: `Assets/PEGA/NPC/PEGA_NPCActorRuntimeBase.prefab`

**Interfaces:**
- Consumes: the component created by Task 1.
- Produces: exactly one `PEGANpcKnowledgeState` component on the NPC Actor root.

- [x] Inspect the on-disk prefab and preserve its existing movement edits.
- [x] The current prefab file now serializes one `PEGANpcKnowledgeState` on its root, apparently from the user's prior Editor command invocation before the demo failed.
- [ ] User inspects the prefab and the two existing scene instances in Unity; do not add a duplicate component.

### Task 3: Use the existing PEGA scene for manual validation

**Files:**
- Inspect only: `Assets/PEGA/Scenes/MissionActivityContent.unity`
- Create: `Assets/PEGA/NPC/PEGANpcKnowledgeStateDebugEvidenceSource.cs`
- Create: `Assets/PEGA/NPC/PEGANpcKnowledgeStateDebugEvidenceSource.cs.meta`

**Interfaces:**
- Uses: the two existing NPC instances, sibling Knowledge State component, typed Actor/Actor Profile APIs, and manual context-menu actions in Play Mode.
- Produces: explicitly synthetic evidence for storage checks only; no separate scene, production evidence producer, perception, or GOAP integration.

- [x] Confirm that the existing mission scene contains two NPC prefab instances suitable for inspection.
- [x] Remove the temporary demo driver, Editor authoring command, and demo documentation after the user rejected a separate environment.
- [x] Add a temporary Editor-only debug component with synthetic baseline facts, known absence, contact loss/position retention, same-time identity conflicts in either order, and snapshots.
- [ ] User manually adds the debug component to each existing NPC instance and assigns the Rick/Petra Actor Profiles; the tool resolves the player ActorId through the existing Player Slot binding and the second subject from the owning NPC's ActorDeclaration, then the user confirms each Knowledge State is independent.
- [ ] User runs the generated facts and checks in Play Mode; no real evidence producer is required for this controlled #68 storage demonstration.
- [ ] Remove the debug components from scene instances before player builds.

### Task 4: Validate delivery boundary

**Files:**
- Inspect: `Assets/PEGA/NPC/PEGANpcKnowledgeState.cs`
- Inspect: `Assets/PEGA/NPC/PEGANpcKnowledgeStateDebugEvidenceSource.cs`
- Inspect: `Assets/PEGA/NPC/PEGA_NPCActorRuntimeBase.prefab`

- [x] Run static whitespace/diff checks and inspect changed paths to confirm unrelated user edits are preserved; new source files are clean. `git diff --check` reports five blank `m_Name:` entries in the pre-existing dirty mission scene and two serialized blank names in the NPC prefab after the prior Editor command invocation.
- [x] Confirm no existing dirty user files were edited by this task; the prior movement source, prefab, mission scene, package lock, and untracked `Assets/PEGA/Actors.meta` remain present.
- [ ] Leave Unity import and Play Mode review to the user; do not claim them or Trello acceptance as complete.

## Execution Notes

- The runtime component and source metadata were implemented. The user clarified that the current mission scene is the appropriate test scene and asked to remove the separate demo scripts and scene; those temporary files have been removed.
- The user will attach both the permanent state if absent and the temporary synthetic debug component manually through the Inspector. No editor command, perception source, or GOAP integration is in scope.
- Do not run Unity import/build/tests, Play Mode, smoke, or batchmode. The user owns Unity validation.
- Existing repository edits are user-owned and must remain untouched.
- `git diff --check` reports trailing whitespace on five `m_Name:` lines in the pre-existing modified `Assets/PEGA/Scenes/MissionActivityContent.unity`; these were not changed. The Knowledge State source has no trailing whitespace and a final newline.
- `PEGANpcKnowledgeState` is not attached to the prefab yet. The user will add it manually in Unity; Trello #68 stays open until its acceptance evidence is reviewed.

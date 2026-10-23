# #105 Gang Spawn Knowledge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Record a real Spawn Point ingress as a gang-shared potential escape Target and make it queryable by current and later-bound NPC members.

**Architecture:** PEGA owns one gang knowledge state per gang in the Mission. Spawn Points publish accepted physical ingress evidence to that state; members hold an explicit reference and resolve immutable typed Target snapshots from it. The Framework remains responsible only for its existing Actor occurrence contract; no new Framework API is needed.

**Tech Stack:** Unity C#, trigger `BoxCollider` with kinematic `Rigidbody`, `CharacterController` velocity, shared PEGA NavMesh movement source, Foundation EventBus, PEGA canonical logger.

**Spec:** `docs/superpowers/specs/2026-10-23-pega-105-gang-spawn-knowledge-design.md`

## Global Constraints

- Keep all game-specific Spawn Point and Gang knowledge behavior in PEGA.
- Do not change Route Spatial Entry, Activity Explicit Relocation, Framework source, or Actor occurrence ownership.
- Do not read hidden destination transforms from GOAP; capture the position only when real ingress is accepted.
- Do not implement timed recipes, actor spawning, GOAP autonomy, re-planning, or #91 N05 escape execution.
- Preserve pre-existing Mission and NavMesh edits; do not edit their YAML.
- Do not run Unity/import/build/Play Mode in this session. Unity validation remains user-executed.
- Use canonical PEGA logger without per-frame logging; temporary validation reports a consolidated result.

## Review Focus

- Trigger callbacks from the wrong direction or another gang must not publish a fact.
- Repeated use of one Spawn Point must not create duplicate Targets or events.
- A later-bound member must see existing facts through the shared owner without individual-state copying.
- An authored escape marker without use evidence must remain absent from Targets.
- The Target identity and position must remain snapshots of accepted evidence, not live scene reads.

---

### Task 1: Gang knowledge owner and member access

**Files:**
- Create: `Assets/PEGA/NPC/PEGANpcGangKnowledgeState.cs`
- Create: `Assets/PEGA/NPC/PEGANpcGangMembership.cs`
- Modify: `Assets/PEGA/NPC/PEGANpcPlanSelectionContracts.cs` only if the existing target factory is inaccessible to this PEGA assembly.

**Interfaces:**
- `PEGANpcGangKnowledgeState.TryJoinMember(GameObject actorRoot, out PEGANpcGangMembership membership, out string diagnostic)` binds an NPC root to this gang's shared state.
- `PEGANpcGangMembership.GetKnownEscapeTargets()` returns read-only typed Targets from that shared state.
- `PEGANpcGangKnowledgeState.SubscribeToChanges(...)` publishes accepted fact changes after state mutation and isolates subscriber exceptions.

- [x] Confirm available planning Target factory/provenance and current PEGA event/logger patterns.
- [x] Implement immutable gang fact snapshots, strict input validation, duplicate handling, and late membership access.
- [x] Run static inspection and diff checks; leave Unity execution pending.

### Task 2: Physical Spawn Point producer

**Files:**
- Create: `Assets/PEGA/NPC/PEGANpcSpawnPoint.cs`
- Modify: `Assets/PEGA/NPC/PEGANpcEscapeDestination.cs` only if a missing target contract is proven necessary.

**Interfaces:**
- The Spawn Point requires an explicit stable id and Gang knowledge owner; its own position is captured as the potential exit only after valid physical ingress.
- A real matching gang member crossing the trigger toward the configured interior direction records a snapshot in the shared state.
- Repeated ingress is idempotent; invalid direction, invalid references, or a different membership do not mutate knowledge.

- [x] Implement trigger configuration validation, CharacterController-based direction evidence, accepted fact publication and canonical diagnostics.
- [x] Verify no scene-wide search, hidden-target polling, direct Actor movement, or GOAP call is introduced.

### Task 3: Temporary Mission validation

**Files:**
- Create: `Assets/PEGA/NPC/Temporary/PEGANpcGangSpawnKnowledgeValidationProbe.cs`
- Create an Editor-only Play Mode menu and an Editor-guarded runtime probe; use a transient trigger and do not save scene or prefab data.

**Interfaces:**
- One Play Mode menu command asks the shared navigation source to cross runtime-only triggers in the open Mission; it does not execute Actions/GOAP or inject knowledge. The probe owns each temporary movement request and calls its terminal Success only after a source snapshot confirms that the physical Actor is on NavMesh and inside the existing PEGA navigation arrival radius. It selects two NPCs only when neither has a GOAP navigation Action running, pauses and restores their Autonomy flags for the controlled interval, and returns the moving NPC within that same configured arrival radius.
- Trigger callbacks are aggregated by Actor across its active colliders; the runtime processes one direction-qualified ingress until every collider for that Actor exits the volume. A later re-entry remains a distinct use. The smoke reports collider callbacks separately from logical Actor crossings, so compound Actor colliders do not inflate traversal counts.
- The consolidated summary reports contact with both matching and foreign-Gang Spawn Points during the same inward traversal, access by two current Mission NPC members, access by a member associated after the fact, isolation of a member in another Gang, outward-direction rejection, duplicate-use idempotence, and cleanup/restoration conditions.

- [x] Add the scoped Play Mode menu command and runtime-only Mission fixture; no scene or prefab is saved.
- [x] Inspect cleanup and restoration paths; preserve the manually arranged Mission scene and shared assets.
- [x] Perform static checks only; provide exact Unity steps and mark all runtime assertions pending.

### Task 4: Reconcile operational checklist

**Files:**
- Trello #105 checklist `Entregáveis #105 — Spawn Point e conhecimento da Gangue`.

- [x] Verify the checklist matches implementation boundaries and contains no completed acceptance before Unity evidence.
- [x] Leave #105 in progress until the user provides Unity results; do not complete #91 N05.

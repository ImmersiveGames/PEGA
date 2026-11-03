# Group Knowledge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add optional, isolated Group Knowledge to the two existing Mission Actor Groups and provide a reproducible Editor proof of sharing and membership-based denial.

**Architecture:** An optional owner-local `PEGAActorGroupKnowledgeState` creates one existing generic `PEGAKnowledgeFactStore(Group)` per real `GroupId` returned by Framework creation. An Actor-bound `PEGAActorGroupKnowledgeReader` holds direct references to relevant owner states and derives its ActorId from the root `ActorDeclaration`; each read rechecks live Group and current membership through the owner's official Framework binding before reading facts. This cut wires only the current Mission scene and does not compose Group facts into GOAP planning.

**Tech Stack:** Unity 6000.5.0f1, PEGA C# runtime in `Assets/PEGA`, resolved Immersive Framework 1.2.0-preview.4 Actor Group APIs, existing `PEGAKnowledgeFactStore`, Unity Editor diagnostics.

**Spec:** `docs/superpowers/specs/2026-11-02-group-knowledge-design.md`

## Global Constraints

- Group Knowledge state and reader components remain optional; Actor Group creation and membership continue if they are absent.
- Register stores only from the actual `ActorGroupSnapshot` returned by `TryCreateGroup`; do not infer liveness or authority from `GroupProfile`.
- Every read checks the current Framework Group and membership before any store access. A denied result contains only empty `Unknown(Group)` data.
- Keep one store per GroupId, under the exact Activity or Route contribution that created that Group.
- Preserve Individual and Global Knowledge without copying facts between scopes.
- Do not modify the Immersive Framework, ADRs, GDD, MVP, TASKS, Actions, Desires, planning, or current NPC behavior.
- Do not add cross-scene/Session resolution or GOAP composition in this cut.
- Do not run Unity build/import/tests/Play Mode/smoke/batch mode. Prepare a Play Mode proof window and list manual Unity steps.
- Preserve all pre-existing workspace changes; do not clean or revert unrelated work.

## Review Focus

- **Knowledge state absent:** Group creation and membership still succeed. Verify `PEGAActorGroupComposition` has no `RequireComponent` for Knowledge and continues its original path when the optional state is absent.
- **Group creation evidence:** Each registered store uses the snapshot returned from its own successful `TryCreateGroup`; no profile-only path creates an authority.
- **Membership revoked:** After official Leave, the same Actor-bound reader returns `NotMember` and empty `Unknown(Group)`; after Join, access is restored.
- **Authorized key absent:** Return `Authorized` plus empty `Unknown(Group)`, distinct from denial.
- **Scope isolation:** Two Groups have distinct values in separate stores; neither fact appears in the other Group or an Individual store, and Global remains independently readable.

---

### Task 1: Optional Group owner state

**Files:**
- Create: `Assets/PEGA/NPC/PEGAActorGroupKnowledgeState.cs`
- Create: `Assets/PEGA/NPC/PEGAActorGroupKnowledgeState.cs.meta`
- Modify: `Assets/PEGA/NPC/PEGAActorGroupComposition.cs`

**Interfaces:**
- Consumes: `ActorGroupAccessBinding`, `ActorGroupSnapshot`, `RuntimeContentScope`, `ActivityContentContribution`, `RouteContentContribution`, `PEGAKnowledgeFactStore`.
- Produces: optional `PEGAActorGroupKnowledgeState` with typed `ObserveFact<TAddress,TValue>(GroupId, key, value, evidence)` and `InvalidateFact<TAddress,TValue>(GroupId, key, evidence)` methods; one active `PEGAKnowledgeFactStore(Group)` per registered GroupId.

- [ ] Add the owner state with no `RequireComponent` relationship to `PEGAActorGroupComposition`. Initialize it from the exact Activity/Route lifecycle context and bind the official `ActorGroupAccessBinding` before registration.
- [ ] Add a registration method that accepts only a valid snapshot returned directly by successful `TryCreateGroup`. Create a fresh Group-scope store for that snapshot's GroupId; reject duplicates without replacing an existing store.
- [ ] Add typed write/invalidate methods that require the owner to be active and the GroupId to be registered, then delegate to the existing generic store and policies.
- [ ] Implement exact Activity and Route exit cleanup plus `OnDestroy`; clear every owned store and reference.
- [ ] Update `PEGAActorGroupComposition.TryCompose` to locate the state optionally, initialize it before Group creation, and pass each successful creation snapshot to it immediately.
- [ ] If Knowledge state is absent or registration fails, report Knowledge unavailability while continuing Framework Group creation and membership unchanged.

### Task 2: Actor-bound Group reader

**Files:**
- Create: `Assets/PEGA/NPC/PEGAActorGroupKnowledgeReader.cs`
- Create: `Assets/PEGA/NPC/PEGAActorGroupKnowledgeReader.cs.meta`

**Interfaces:**
- Consumes: the reader root's `ActorDeclaration` and explicitly assigned `PEGAActorGroupKnowledgeState[]` references.
- Produces: `ReadGroupFact<TAddress,TValue>(GroupId, PEGAKnowledgeFactKey<TAddress,TValue>, double) -> PEGAKnowledgeGroupReadResult<TValue>` with statuses `Authorized`, `NotMember`, `GroupUnavailable`, and `AuthorityUnavailable`.

- [ ] Add an optional component requiring `ActorDeclaration`, with direct serialized references to relevant owner states. Do not use `GroupProfile`, scene searches, static registries, or caller-provided ActorIds as authorization.
- [ ] Read `ActorId` from this component's own `ActorDeclaration` on each request and resolve only a referenced state that owns the requested GroupId.
- [ ] Ensure the owner state checks `TryGetGroup` and current `TryIsMember` before calling `PEGAKnowledgeFactStore.Get`.
- [ ] Return the store's Group-scoped fact only for `Authorized`. Every other status returns `PEGAKnowledgeFact<T>.Unknown(Group)` with no value, candidate, timestamp, or evidence.
- [ ] Keep read diagnostics free of fact values and evidence, especially on denial.

### Task 3: Mission wiring and proof

**Files:**
- Modify: `Assets/PEGA/Scenes/MissionActivityContent.unity`
- Create: `Assets/PEGA/Diagnostics/Editor/PEGAGroupKnowledgeProofWindow.cs`
- Create: `Assets/PEGA/Diagnostics/Editor/PEGAGroupKnowledgeProofWindow.cs.meta`

**Interfaces:**
- Consumes: the optional owner state, three Actor-bound readers, both existing Group identities, official Framework membership queries, canonical typed Knowledge definitions, existing Global and Individual authorities.
- Produces: an Editor menu proof that writes and reads facts without invoking Actions or GOAP decisions.

- [ ] Add `PEGAActorGroupKnowledgeState` beside the current Mission `PEGAActorGroupComposition` and directly reference it from the three robber readers. Do not change Group Profiles or membership arrays.
- [ ] Add a Play Mode Editor proof that discovers the three Actors and confirms two live GroupIds through `ActorGroupAccessBinding`.
- [ ] Use `PEGAGlobalKnowledgeFacts.MainObjectiveIdentified.For(0)` in both Group stores; write `false` to Group 1 and `true` to Group 2 with distinct valid evidence. Use a later valid observation time for any repeated write so the existing evidence policy accepts it.
- [ ] Assert both first-Group members read the same value, status, scope, observation time, and provenance; assert the third reads its own distinct value.
- [ ] Assert both directions of cross-Group reads return denial plus empty Unknown(Group), and ensure the proof output omits denied values and evidence.
- [ ] Assert an authorized absent key returns `Authorized + Unknown`, then exercise Leave → denied → Join → authorized through official Framework APIs. Restore membership in `finally`.
- [ ] Assert all three GOAP consumers still read the same Global fact through the existing Global authority and the first robber's Individual store remains Unknown for the Group fact.
- [ ] Display a concise proof result with status, scope, timestamps/provenance for authorized reads, and only denial status for denied reads.

### Task 4: Static review and Unity handoff

**Files:**
- Review the Task 1–3 diff and the pre-existing dirty workspace state; no additional files are planned.

- [ ] Run `git diff --check` without changing unrelated files.
- [ ] Search Group Knowledge call sites and serialized Mission components to confirm every state/reader reference resolves to the new script GUIDs and no old Group Knowledge path has been reintroduced.
- [ ] Confirm statically that the optional Knowledge component cannot block Group creation, that only actual snapshots register stores, and that authorization branches precede every store read.
- [ ] Do not run Unity or add Unity tests. Hand off the manual checklist below.

## Manual Unity checklist

1. Open the Mission content scene and enter Play Mode; wait for the Activity to create both Groups and confirm all three memberships through the Framework.
2. Open `PEGA/Diagnostics/Group Knowledge Proof` and run the proof. Confirm the two first-Group members share their fact, the third sees only its Group's distinct fact, both cross-Group directions are denied without values, and authorized absence differs from denial.
3. Confirm the Leave/rejoin check denies the same reader immediately after Leave and restores access after Join.
4. Confirm Global reads remain available to all three and Individual Knowledge remains Unknown for Group facts.
5. Disable/remove the optional Group Knowledge state in a disposable scene copy and confirm Groups/memberships still compose; do not alter the working Mission during this check.
6. Exit the Activity and re-enter; confirm Group stores are new and start empty.

## Remaining limits

This first cut composes direct references in the current Mission scene. Cross-scene resolution and multiple simultaneous Framework Sessions remain future work and require a separate explicit design. No Unity runtime evidence is claimed until the user runs the Editor proof.

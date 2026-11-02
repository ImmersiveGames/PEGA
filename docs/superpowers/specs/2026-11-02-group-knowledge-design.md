# Group Knowledge Design

## Intent

Add the smallest PEGA implementation that demonstrates independent collective Knowledge for the two Actor Groups in the current Mission: members share their Group's facts, and non-members cannot read them. Keep Group Knowledge optional so existing Actor Group creation and membership still work without it.

## Goals

- Reuse `PEGAKnowledgeFactStore` with `PEGAKnowledgeScope.Group`, canonical typed definitions, evidence, provenance, and validity policies.
- When the optional owner state is present, create one independent store for each actual Framework `GroupId`, registered from the snapshot returned by `TryCreateGroup`.
- Keep each store under the exact Activity or Route owner that created its Group; clear it when that owner exits.
- Let an Actor-bound reader request a specific Group fact using its own Framework `ActorDeclaration` identity.
- On each read, check the live Group and current membership through the official `ActorGroupAccessBinding` before reading the store.
- Return a distinct access status. Denied queries return only empty `Unknown(Group)` metadata and expose no fact value or evidence.
- Prove the two Mission members share the first Group's facts, the third Actor reads only the second Group's facts, and all three retain access to existing Global Knowledge.

## Non-goals

- Do not make Group Knowledge a requirement for creating or using Actor Groups. A missing or inactive Knowledge state must not stop Framework Group creation or membership.
- Do not add Group fact authoring payloads, propagation, cross-scope copying, or a new Group runtime.
- Do not add fact readers to GOAP planning, Actions, Desires, or NPC decisions in this cut.
- Do not build cross-scene or multiple-Session authority resolution. The Mission's Groups and NPCs are composed in the same scene; direct serialized references are enough for this proof.
- Do not alter Immersive Framework source, GDD, MVP, TASKS, ADRs, or unrelated gameplay.
- Do not invoke Unity build/import/tests/Play Mode/smoke/batch mode during this task. A manual Play Mode proof is prepared for the user to execute.

## Constraints and existing contracts

- `GroupId` is the Framework runtime identity. `GroupProfile` helps select a requested Group but does not prove that the Group is live or that an Actor is a member.
- The official `ActorGroupAccessBinding` queries the current Session-wide Group registry. `TryIsMember` returning `false` means a live Group exists but the Actor is not currently a member.
- Groups can remain live when empty; releasing the exact Activity or Route owner closes its Groups and removes membership.
- `PEGAActorGroupComposition` already creates Groups from the Framework snapshot and confirms membership. Knowledge registration must use the same snapshot immediately after successful creation.
- `PEGAKnowledgeFactStore` already supports `Individual`, `Group`, and `Global`; no generic storage changes are needed.
- Existing Individual and Global authorities remain independent and unchanged.
- Mission content currently keeps both Groups and all three robber Actors in the same Activity content scene, so authorities can be explicitly referenced by the Actor readers in that scene.

## Approaches considered

### 1. Optional owner-local state with direct Mission references — selected

Add an optional `PEGAActorGroupKnowledgeState` beside `PEGAActorGroupComposition`. When present and active, the composition passes each real snapshot returned by `TryCreateGroup` to the state, which creates a separate store per GroupId. Actor roots that need to read Group Knowledge get a `PEGAActorGroupKnowledgeReader` with direct references to the relevant state component. The reader derives ActorId from its own `ActorDeclaration`; the state validates current membership with its Framework binding before touching facts.

This fits the existing Mission composition, keeps Group lifetime with its exact owner, and avoids a context asset, lookup registry, or changes to GOAP planning. If Knowledge components are absent, Group creation and membership follow their current path.

### 2. Cross-scene authority context — deferred

A shared PEGA context could resolve owner states across separately loaded scenes. The current proof does not need that composition, and the Framework does not expose Session identity from the Group binding. Adding the resolver now would introduce extra asset configuration and lifecycle behavior without helping demonstrate the Mission requirement.

### 3. Restore the historical Group Knowledge implementation — rejected

The historical code already rechecked membership and used the generic store, but its ScriptableObject context, authored initial facts, and event/source structure are unnecessary for this focused cut. Reuse the valid store and membership contract without restoring that implementation wholesale.

## Runtime design

### Optional Group owner state

Create `PEGAActorGroupKnowledgeState` on the same content root as `PEGAActorGroupComposition`; do not add it as a `RequireComponent`. It implements the current Activity and Route exit callbacks and clears every store when its exact contribution exits or is destroyed. The composition looks up this component optionally. Immediately after `TryCreateGroup` succeeds, it registers the returned valid `ActorGroupSnapshot` with the state, which creates one `PEGAKnowledgeFactStore(Group)` for that GroupId while the optional state is active.

If the state is absent, the composition does not create Knowledge storage and continues Group creation and membership unchanged. If Knowledge registration fails, log that Knowledge is unavailable for that Group while leaving the official Group lifecycle intact.

Provide typed `ObserveFact` and `InvalidateFact` operations on the owner state, addressed by the actual GroupId. They delegate to that Group's existing generic store. No Group-specific initial-fact authoring is added.

### Actor-bound read access

Create an optional `PEGAActorGroupKnowledgeReader` component on a consumer Actor root. It gets ActorId from the root's `ActorDeclaration` and has serialized references to the `PEGAActorGroupKnowledgeState` components the Actor may request. Its read method takes an explicit `GroupId` and typed key; it never accepts an ActorId argument.

For each query, the reader resolves only a referenced state that owns the requested live GroupId. The state checks `TryGetGroup` and `TryIsMember` using the official owner binding and the reader's bound ActorId. It reads from `PEGAKnowledgeFactStore` only after both checks succeed. A stale/unavailable Group, missing authority, or non-member receives a distinct access status with `Unknown(Group)` containing no value, candidate, observation time, or evidence. An authorized key absent from that store is reported as authorized with the store's `Unknown(Group)` fact.

This read API is for the explicit PEGA runtime reader and its proof. It is not composed into `PEGANpcAutonomy` or used by GOAP decisions in this step.

### Existing scopes

The Group state has no reference to an Actor's `PEGANpcKnowledgeState` or `PEGAGlobalKnowledgeState`. Facts are never copied between scopes. The Global capability remains on the existing Mission authority and remains accessible to all three GOAP consumers.

## Mission proof

Add the optional owner state to the existing Mission content root and direct references from each of the three robber readers to that state. The proof uses the current two Framework Groups and current membership; it does not alter their profiles or memberships.

Use the existing canonical `PEGAGlobalKnowledgeFacts.MainObjectiveIdentified` definition at address `0` in the Group and Global stores, writing different Group values with distinct evidence so the two Group authorities are distinguishable. Each scope retains a separate store despite sharing the typed fact key. A Play Mode Editor proof verifies:

1. Both first-Group robbers read the same Known value, Group scope, observation time, and provenance.
2. The third robber reads the distinct Known value from its own Group.
3. Cross-Group reads report denied with an empty `Unknown(Group)` result; the proof output never includes values or evidence for denials.
4. An authorized read of an absent key reports authorized plus `Unknown(Group)`, distinguishing absence from denial.
5. A current member temporarily leaves through `ActorGroupAccessBinding`; the same reader is denied immediately, then membership is restored in a `finally` block.
6. All three consumers still read the same Global value and metadata from the existing Global authority.
7. The first robber's Individual store remains `Unknown(Individual)` for the Group fact.

The proof does not run Actions or request a planning decision. It is an Editor window run by the user in Play Mode. Unity execution remains pending because this repository's rules prohibit running Unity.

## Implementation decisions left to the builder

- Keep Group Knowledge state and reader components optional; absence cannot disable or fail Actor Group composition.
- Use explicit result statuses `Authorized`, `NotMember`, `GroupUnavailable`, and `AuthorityUnavailable`; every non-authorized result carries only empty `Unknown(Group)` data.
- Compare the requested GroupId to the actual IDs registered from Framework snapshots; never use GroupProfile as liveness or authorization proof.
- Keep stores private to `PEGAActorGroupKnowledgeState`, one per GroupId, and clear them at the exact owner's exit.
- Preserve the current scene's two Groups, three memberships, GOAP, perception, and Global/Individual paths.

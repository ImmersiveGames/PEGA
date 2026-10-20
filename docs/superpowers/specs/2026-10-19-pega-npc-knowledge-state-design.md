# PEGA NPC Knowledge State

- **Status:** Draft for user review
- **Date:** 2026-10-19
- **Scope:** First implementation slice for Trello card [#68 — NPC Knowledge State e memória sem onisciência](https://trello.com/c/WzynxvkM/68-npc-knowledge-state-e-mem%C3%B3ria-sem-onisci%C3%AAncia)

## 1. Goal

Create an individual, PEGA-owned knowledge store for each NPC. It records only evidence that the NPC has legitimately received, distinguishes unknown facts from known false facts and invalidated facts, keeps observations for different logical Actors independent, and retains Rick's last observed position when visual contact is lost.

This slice establishes the state contract and its read/write boundary. Perception, Actions, GOAP, information sharing, and the behavior that consumes these facts remain separate work.

## 2. Authority and traceability

- **Trello #68** is the operational work item. Its four acceptance checklist items cover fact states, legitimate evidence, individual queries without hidden-world access, and evidence-based updates/invalidation. The card remains in `A fazer`; this design does not update Trello.
- **OBJ-07 — Manter conhecimento limitado e válido dos larápios** in `docs/pega-objective-specifications.md` supplies the behavior contract.
- **GDD — “Conhecimento modular, memória e validade da informação”** defines independent facts, positive and negative knowledge, per-fact validity/update/invalidation, and observation-time ordering. **“Perda de alvo”** requires retaining only the last known position when contact is lost. **“Conhecimento da gangue e informação global”** defines sharing scopes; this slice implements Individual scope only.
- `MVP.md`, `TASKS.md`, and `ADR-PREPRODUCAO.md` provide the related MVP boundary, work decomposition, and production ordering. They are not changed by this task.

## 3. Ownership and placement

- Put `PEGANpcKnowledgeState` in `Assets/PEGA/NPC` and attach one instance to the root of each NPC Actor that uses this slice.
- PEGA owns these game-specific facts. The component follows its NPC Actor's lifetime and keeps independent in-memory state for that NPC.
- Facts start as `Unknown` for each runtime instance. They are not authored tuning data and are not serialized into the prefab or saved between runs.
- The component uses the resolved Framework `ActorId` only as the subject key supplied with evidence and `ActorProfileId` only as the recognized character-profile value supplied with evidence. It performs no identity lookup, occurrence resolution, lifecycle or composition operation, and reads no target declaration or profile. These Framework identity APIs are Experimental; this uses existing resolved contracts and adds no package, assembly, Framework source, or framework-owned component.
- Add the component to the NPC runtime root through Unity authoring when implementing the approved plan; do not hand-edit prefab YAML.

## 4. Fact model

Use a closed status model for each typed fact:

| Status | Meaning | Value available to consumers |
|---|---|---|
| `Unknown` | No accepted evidence has established this fact. | None |
| `Known` | Accepted evidence establishes a value, including `false`. | Yes |
| `Invalidated` | Evidence or an explicit fact-specific policy invalidated the current value. | No |

Invalidation is distinct from a known negative. Readers can obtain a value only when status is `Known`. Every fact stores its observation time and the set of evidence origins at that time, not the order or time at which updates arrived. An invalidated fact retains its observation time, merged origins, and invalidation cause for explanation; an invalidated value is never returned as current knowledge.

The component exposes only the card's typed facts. Character identity evidence is kept per observed logical target, keyed by `ActorId`; this is not a general-purpose fact registry. The separate contact and position facts remain scoped to the Rick target required by the card. An absent target identity entry queries as `Unknown`:

1. **Presence in perceived area** (`bool`): an anonymous radial-presence result. `true` means the source observed presence; `false` is allowed only when the legitimate source can establish absence for the completed observation. This fact carries no identity, direction, or exact position.
2. **Observed character identity** (`ActorProfileId`), keyed by subject `ActorId`: the character profile established by legitimate visual identity evidence. PEGA's current Rick and Petra profiles provide distinct profile IDs. Here `ActorProfileId` is the current profile label for the character, not a universal unique-person ID. This is a typed identity value, not an `is Rick` boolean. Recognizing Rick for one `ActorId` and Petra for another stores two independent known facts.
3. **Visual contact with Rick** (`bool`): whether the NPC currently has visual contact with the logical target whose known identity is Rick's profile ID. Its evidence carries that Rick subject's `ActorId`. Losing contact writes a new `Known(false)` fact and does not alter identity knowledge for another subject.
4. **Last observed Rick position** (`Vector3`): the position captured by legitimate visual evidence for that same Rick subject `ActorId`. It changes only when newer accepted evidence observes Rick's position. Loss of contact does not move, extrapolate, or invalidate it. Other recognized identities do not add contact or position facts in this slice.

Facts update independently per subject. An anonymous radial result cannot update identity, visual contact, or position. Recognizing Petra under a different `ActorId` does not write `false` to Rick's identity, nor imply that Rick is absent. A same-time disagreement about the identity of the same `ActorId` remains conflicting evidence for that subject only.

## 5. Evidence write and read contract

- Provide narrowly typed write operations for the card's facts and read-only immutable snapshots for consumers. Identity/contact/position writes carry the subject `ActorId`; identity writes also carry the recognized `ActorProfileId`. Do not expose public mutable fields, arbitrary string keys, a generic `SetFact` entry point, or a reference to another Actor.
- The evidence producer supplies a valid subject `ActorId` and, for identity, a valid observed `ActorProfileId`; reject default/invalid IDs. The store neither resolves an `ActorOccurrenceId` nor reads `ActorDeclaration`, an `ActorProfile`, Transform, World State, or any current target state to manufacture evidence. The identity values must come from the legitimate evidence itself. `ActorId` and `ActorProfileId` are compared only within their own identity domains.
- Each write carries an explicit origin (`Perception` or `ActionResult`) and the observation time in simulation seconds supplied by its producer. The producer captures `Time.timeAsDouble` when the fact is observed and propagates that exact value with the evidence; the store never samples a clock itself. Compare finite timestamps exactly, without an epsilon: equal values identify the same observation time, and every distinct value has a total numeric order.
- In production, only a perception result or a legitimate completed Action result may write evidence. No such producer is added in this slice, so the runtime state remains `Unknown` until a source is integrated. The temporary controlled driver in the #68 demonstration is validation-only and is not shipped as a gameplay evidence source.
- Require explicit fact-specific invalidation evidence. There is no generic TTL, automatic forgetting, or automatic invalidation on contact loss. Contact loss is represented by known `false`; last position remains known.
- Resolve evidence per `(subject ActorId, fact)` with an order-independent merge. Compare observation timestamps, not arrival order: an earlier timestamp is ignored, a later timestamp replaces the current fact, and an equal timestamp merges into it. Evidence for different `ActorId` subjects is independent. Reject non-finite or negative timestamps and non-finite position coordinates.
- For evidence at the same timestamp, identical known values remain `Known`; their origins are merged as a set. Different known values make the fact `Invalidated` with cause `ConflictingEvidence`; no value remains readable. Explicit invalidation at that timestamp also yields `Invalidated`. Once invalidated at a timestamp, further evidence at that same timestamp cannot restore a value. A later timestamp can establish a new `Known` value or invalidate it again. The merge unions origins and invalidation causes, so the final status, readable value, metadata, and cause are the same for every arrival order. Duplicate evidence is idempotent.
- Compare `bool` values directly, `ActorProfileId` values within the ActorProfile identity domain, and `Vector3` coordinates component by component after finite-value validation. Different recognized profile IDs for the same subject at the same timestamp conflict; the same profile ID for different subjects does not. Any distinct position for the same subject at the same timestamp is conflicting evidence; do not use an approximate vector comparison or source-priority tie-break.
- Expose status, observation time, merged origin set, and invalidation cause through knowledge queries so a consumer can distinguish `Unknown`, `Known(false)`, conflict-invalidated, and explicitly invalidated facts. Consumers cannot query the World State through this component or ask it to resolve hidden objects.

| Resolution strategy | Decision | Reason |
|---|---|---|
| Keep the first value and reject later same-time evidence | Reject | Arrival order determines the winner. |
| Give `Perception` or `ActionResult` a fixed precedence | Reject | The GDD defines no general trust ranking between those sources. |
| Merge all evidence with the same timestamp; invalidate on disagreement and union origins | Choose | The final fact is commutative, duplicates are idempotent, and uncertainty is preserved without inventing a winner. |

The explicit write boundary documents and narrows allowed call sites; it is not an authentication system. In the controlled #68 demonstration, a temporary driver supplies explicit test evidence through this boundary. Its data validates storage semantics, not a real perception or Action integration. Future perception and Action work must keep production writes at their legitimate result points and must not make arbitrary gameplay code a source of truth.

Order independence describes the resolved snapshot after the complete same-timestamp evidence set has been submitted. A query between separate writes can only reflect the evidence received so far. The controlled demonstration submits each set before reading its final result; a future consumer integration must read after its producer phase has submitted the relevant evidence. Consumer scheduling is outside #68.

## 6. Observability

Use the existing PEGA `PEGALogging` path for meaningful accepted state transitions, same-time conflict invalidation, and rejected stale/invalid evidence. Include the NPC context, fact, previous/new status, origin set, and observation time. Do not log every frame, store a second debug history, expose hidden World State, or create a general diagnostics framework or custom Editor tool for this slice.

## 7. Exclusions

- Perception implementation and radial/visual sensing rules owned by card #69.
- GOAP integration/planning owned by its separate work, including card #91.
- Action implementation from card #63; this store only leaves a typed origin for a future legitimate Action result.
- Gangue/Global propagation, shared memory, item investigation, robbery knowledge, and a general knowledge/inference framework.
- Framework package/API changes, Actor occurrence lookup, Unity lifecycle replacement, serialized debug state, and user-level Codex changes. Subject/profile IDs must arrive in the accepted evidence; this slice does not add recognition, ID generation, or a framework-to-character mapping.

The existing `PEGANpcWanderFleeMovementIntentSource` has a separate movement decision path. Its current live target-position read is not changed here; planner use of `PEGANpcKnowledgeState` belongs to its own approved integration work.

## 8. Resolved ecosystem evidence

The consumer is PEGA at Unity `6000.5.0f1`. `Packages/packages-lock.json` resolves `com.immersive.framework` to source hash `7baf6a336ea98aa6558d06e401791c4015e4181e`; the installed cache package declares `1.2.0-preview.4`. This is the current consumer resolution, not a version floor for this feature.

The knowledge contract remains PEGA-owned and requires no new package or dependency update. The resolved Framework package contains no Knowledge State, perception result, character-recognition, or shared gameplay-observation-clock contract. Its public API and source distinguish three identity layers: `ActorId` is the stable logical Actor identity; `ActorOccurrenceId` identifies one physical Actor incarnation and changes on root replacement; `ActorProfileId` identifies a reusable authored Actor profile/classification. All three surfaces are Experimental in the resolved package. Evidence inspected: `Library/PackageCache/com.immersive.framework@7baf6a336ea9/Documentation~/API/Public-API.md`, `Documentation~/Guides/Actor-Composition-Usage.md`, and the corresponding `Runtime/Actors/ActorId.cs`, `ActorOccurrenceId.cs`, `ActorProfileId.cs`, `ActorDeclaration.cs`, and `ActorProfile.cs`. The current PEGA profile assets `Assets/PEGA/Player/ActorProfile_RickRonda.asset` and `ActorProfile_PetraPatrol.asset` define distinct profile IDs for Rick (`actor-profile.pega.rick-ronda`) and Petra (`actor-profile.pega.petra-patrol`), so this slice uses `ActorId` as the evidence subject and the corresponding `ActorProfileId` as the recognized identity value. `ActorProfileId` labels a reusable Actor option, so this bounded mapping is valid for the current one-profile-per-character PEGA assets; it must not be generalized to unique-person identity if PEGA later adds multiple profiles for one character. This does not authorize reading a target's declaration/profile as evidence; the producer must supply both values through a legitimate observation result. If the source cannot legitimately provide the subject `ActorId`, that is an integration contract to resolve with perception, not a reason for the store to inspect hidden target state. Do not use `ActorOccurrenceId` as the memory key because physical replacement would split knowledge about the same logical Actor. The resolved `com.immersive.foundation` `EventBus<TEvent>` is an explicitly instantiated delivery primitive; it does not define game observation time, per-fact merging, or conflict policy, and is not needed for synchronous writes to one NPC's own state. Use Unity's simulation timestamp captured by the PEGA evidence producer, not a Framework lifecycle, composition revision, or event subscription order. In the resolved Unity version, `Time.timeAsDouble` provides double-precision seconds at the beginning of the current frame, returns the same value for calls in that frame, and uses `Time.fixedTimeAsDouble` in `FixedUpdate`; producers capture it at the evidence point and pass it unchanged ([Unity 6.5 `Time.timeAsDouble`](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Time-timeAsDouble.html)). The `.4` changelog concerns ActorProfile composition parts and dependency versions, not knowledge storage. The local Framework source checkout used for comparison is at the same hash; its newest local tag is `.3`, which does not change the consumer-resolution evidence. The FirstGame sample has no relevant NPC knowledge example and is not an authority for this contract.

## 9. Demonstration protocol

Demonstrate storage behavior and evidence legitimacy separately; one does not prove the other.

### 9.1 State transitions and isolation

In a disposable Unity scratch scene, place two NPC roots with `PEGANpcKnowledgeState` and use a temporary development-only evidence driver to send controlled subject IDs, profile identities, fact values, and exact observation timestamps through the same typed write operations. Observe immutable query snapshots and the corresponding `PEGALogging` transition records. Both NPCs start `Unknown`; NPC A receives anonymous presence; one subject is recognized as Rick and another as Petra at the same timestamp, with independent known identity entries; only Rick's subject records contact and position P1; loss of contact for Rick records `Known(false)` and preserves P1; a later timestamp replaces the older fact; explicit invalidation produces `Invalidated`; NPC B stays unchanged throughout. Remove the temporary driver and scratch scene from the runtime deliverable after collecting the controlled demonstration evidence.

For order independence, first submit Rick and Petra identity evidence for different subject `ActorId` values at the same timestamp and verify both remain `Known` with no cross-subject conflict. Then submit two different `ActorProfileId` values for the same subject and same timestamp in both arrival orders; only that subject's identity must become `Invalidated`, with the same merged origins and conflict cause. Also run boolean contact evidence (`Known(true)` then `Known(false)`, and the reverse) and two distinct positions for the same subject at the same timestamp; both permutations must have identical invalidated results. Repeat with identical values from both origins in opposite orders; both results must remain `Known` with the same merged origin set. Send duplicate inputs to confirm idempotence. Reverse the arrival order of earlier and later timestamps and confirm that both runs end at the later fact. The #68 controlled demonstration validates this memory capability and can satisfy #68 independently of #69.

Once run and recorded, this controlled sequence will be the #68 acceptance evidence for the store's state semantics, ordering, isolation, and contact-loss behavior. Its synthetic input does not claim that production perception or Action sources are already integrated; their own source and sensing behavior are accepted separately.

### 9.2 Separate source integration acceptance

The real perception path will later verify that sensing produces only the allowed facts and writes at accepted observation/loss events; it belongs to card #69 and has its own acceptance. The Action-result path has its own integration point as well. Neither integration gates completion of #68: #68 accepts the store's typed evidence contract, per-fact reducer, and controlled demonstration. Report the source-integration status on its own card and do not claim that the #68 driver proves real sensing behavior.

## 10. Acceptance and manual validation

After code implementation and Unity authoring, perform the demonstrations above and validate manually in the Unity Editor and Play Mode; these checks are instructions, not evidence that they have already passed:

1. Two NPC roots begin with independent `Unknown` facts; updating one NPC does not affect the other.
2. Anonymous radial-presence evidence can be `Known(true)` or, when the observation supports it, `Known(false)`, while all target identity, contact, and position entries remain unchanged.
3. At one timestamp, visual identity evidence for a Rick subject and a Petra subject records each `ActorProfileId` as `Known` under its own `ActorId`; neither observation changes the other's fact or status.
4. A visual result identifying Rick records its profile ID as `Known`, `Known(true)` contact, and the captured position under the Rick subject's `ActorId`, with that observation's origin and time.
5. Legitimate loss-of-contact evidence for Rick's subject records `Known(false)` contact and preserves the last observed position and its original evidence metadata; Petra's entry remains unchanged.
6. Explicit invalidation yields `Invalidated`; a known `false` remains distinguishable from both `Unknown` and `Invalidated`.
7. Replay same-time different identities for the same subject and for distinct subjects in both arrival orders. Only same-subject disagreement invalidates; different subjects remain independent. Final status, value availability, merged origins, and invalidation cause are identical for every same-subject permutation. Earlier timestamps cannot replace later facts, duplicate evidence is idempotent, invalid/default identity IDs and non-finite timestamps/positions are rejected, and relevant transitions/rejections are logged without per-frame output.
8. Knowledge queries expose only stored evidence. They never read the target's current Transform, resolve an occurrence, or discover a hidden World State object.
9. The component is attached to the NPC Actor root in the authored runtime prefab, with no Framework package modification.

No Unity build, import, test, Play Mode, smoke, or batchmode command is permitted for this project. Static inspection can verify source contracts and diffs, but Unity compilation/import and the checks above require manual confirmation. The Trello card must remain open until its acceptance evidence exists.

## 11. Risks and decisions for review

- No perception or Action producer currently supplies evidence. The #68 controlled driver demonstrates the component's evidence contract and reducer; real source integrations remain independently reviewed in their own work.
- The reducer uses a conservative join: a same-time disagreement invalidates that fact rather than inventing a source priority or picking whichever value arrived first. This matches the GDD's per-fact conflict policy requirement while remaining order-independent.
- Identity storage is keyed by the target's logical `ActorId` and records an observed `ActorProfileId`. Using the Framework profile value for Rick/Petra is bounded to the current PEGA profile mapping; this slice does not add a narrative-character registry or infer identity from target metadata.
- Same-time conflicts invalidate the fact until newer evidence arrives. This preserves arrival-order independence but intentionally leaves no active value for consumers to use; producers must supply a later observation before the fact becomes known again.
- The card's “presence” result must remain anonymous unless a separate legitimate observation establishes identity. Any future sensor that means something different must define its fact semantics before writing it.
- The approved runtime component and its prefab attachment cannot be claimed as delivered until Unity authoring and manual validation are confirmed.

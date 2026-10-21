# PEGA GOAP Desires and Plan Selection

- **Status:** Written proposal; awaiting user review before implementation planning
- **Date:** 2026-10-21
- **Scope:** First implementation slice for Trello [#64 — Desires and plan selection](https://trello.com/c/iWwXiDee/64-npc-desejos-e-escolha-de-planos)

## 1. Goal

Implement the first PEGA-owned Desire and GOAP plan-selection capability for the Normal context. The selector must evaluate more than one valid plan, keep a Desire applicable when no plan can be built, compare configured Desire priority and estimated plan time deterministically, and keep predicted plan effects separate from observed Action results.

The first acceptance compares the real pursuit and escape movement Actions already present in PEGA. It does not execute a selected plan autonomously, poll for changes, subscribe to replan events, or add new gameplay Actions. Escape means movement toward a known exit target; reaching its marker does not resolve the mission as `Escaped`.

## 2. Authority and traceability

- **Trello #64** is in `Em andamento`. Its acceptance requires: configure an initial Desire with applicability and satisfaction; compose/select plans from available Actions; separate plan prediction from observed execution; and demonstrate a different plan after known facts change. The revised card also requires viable alternatives, a no-solution result, and configurable deterministic resolution of priority and time cost.
- **GDD — “Peso, oportunidade e custo dos planos”** defines estimated seconds as movement time plus resolved Action durations, treats Desire weight and plan cost as independent inputs, leaves their combination configurable, and requires deterministic tie resolution.
- **GDD — “Contrato de Actions”** separates planning from physical execution, applies predicted Effects only after successful completion, and requires execution to validate reality.
- **GDD — “Contextos e satisfação dos Desejos”** defines per-archetype Desire configuration, the Normal context, applicability independent of plan availability, individual accumulated satisfaction, and satisfaction only after a completed Action produces the actual desired state.
- **GDD — “Conhecimento modular, memória e validade da informação”** requires the planner to resolve Targets only from known candidates; scene presence alone cannot make an object known.
- **GDD — “Fuga e escolha de saída”** says used entry points and discovered exits become known targets. It does not authorize treating every scene exit marker as NPC knowledge.
- **OBJ-08** is broader than #64 and includes autonomous execution and re-planning. This slice only supplies Desire evaluation and a selected plan value for later consumption.
- No GDD, MVP, TASKS, ADR, or Trello content is modified by this design.

## 3. Workspace and ecosystem evidence

`Assets/PEGA/NPC` has no Desire, archetype, planner, or plan-selection implementation. The relevant existing capabilities are:

| Existing contract | Current role and constraint |
|---|---|
| `PEGANpcKnowledgeState` | Individual facts for perceived-area presence, subject identity, Rick visual contact, and Rick's last observed position. It currently has no known-exit fact. |
| `PEGANpcApproachKnownRickAction` | A real #63 Action. It resolves Rick's subject identity and last observed position from this NPC's Knowledge State, then starts physical navigation when explicitly called. |
| `PEGANpcTravelToEscapeAction` | A real #63 Action. It requires a caller-selected escape destination; it does not discover or choose one. |
| `PEGANpcEscapeDestination` | A scene output reference with a world position. `MissionRoute` contains E01, but this component does not establish that an NPC knows E01. |
| `PEGANpcNavMeshMovementIntentSource` | Owns route execution through `NavMeshAgent` while `PEGAActorMovementMechanic` owns physical movement. It currently exposes completed route data, not a side-effect-free route estimate for planning. |
| `PEGANpcNavigationAction` | Owns execution lifecycle and snapshots. Its `EffectObserved` currently means Action outcome is `Success`; that alone is insufficient for Desire satisfaction. |

The current Mission has no PEGA spawn-point/exit-knowledge producer. The local Framework package resolves to `com.immersive.framework` `1.2.0-preview.4` at commit `7baf6a336ea98aa6558d06e401791c4015e4181e`; its current Actor identity and logging contracts are useful, but no GOAP/Desire/Planner contract exists in the resolved Immersive Framework, Foundation, or Logging packages. Framework `ActorProfile` remains character identity/composition data and must not be repurposed as a thief archetype. Keep this feature in `Assets/PEGA/NPC`; do not change an Immersive package.

## 4. Why one Desire and one Action are insufficient

A single `Pursue Rick` Desire paired with `ApproachKnownRickAction` can demonstrate applicability and a viable movement request, but it cannot show plan selection, the influence of priority versus cost, or a choice among valid alternatives. Adding another Desire that points directly to the same Action would only duplicate a mapping and would not demonstrate GOAP composition.

The minimum meaningful choice set uses two applicable declarative goals and two existing planning operators. This creates two distinct candidate plans in one evaluation instead of binding one Desire to one Action:

1. **Pursue the last known Rick position** — the goal state is that the NPC reaches the known position. `ApproachKnownRickAction` declares preconditions from this NPC's legitimate Knowledge State and predicts the corresponding position effect.
2. **Move toward a known escape** — the goal state is that the NPC reaches an explicitly known escape destination. `TravelToEscapeAction` declares its known-target precondition and predicts arrival at that destination. This is not the terminal mission effect `Escaped`.

The planner discovers which operators can achieve each Desire's goal through their declared preconditions and effects. The profile does not hard-wire a Desire to an Action. In the first repertoire each valid plan is one step; the plan representation remains an ordered list so later Actions can form chains without changing the execution boundary.

When both targets are known and both NavMesh routes are complete, both plans are viable alternatives. When neither target is known, or when the required routes are invalid, both Desires may remain applicable but have no valid plan and the result is `NoPlan`. The plans are one step because the available #63 movement repertoire contains only these two Actions; this acceptance proves goal/effect matching and selection across alternatives, but not multi-Action chain search or multiple plans for one Desire. No Action is added just to lengthen the demonstration.

## 5. Known escape-target source required for the first choice

The current scene marker E01 cannot be passed directly to the planner: it is a location reference, not a knowledge fact. The GDD specifically says a gang knows an entry as a potential exit after a member uses that Spawn Point, or learns another exit through discovery. The workspace has E01 and `PEGANpcTravelToEscapeAction`, but no Spawn Point use event, known-exit fact, or gang-knowledge producer. Therefore the two-plan acceptance is not achievable from existing runtime facts alone.

To retain the escape alternative without fabricating knowledge, the minimum prerequisite is a **real entry-use fact source**: when a participating thief actually enters through an authored Spawn Point, the source records that entry as a known escape target with the GDD's Gang scope; the planner resolves only that fact. For the current Mission fixture this only works if E01 is configured as the actual entry used by the two NPCs. The destination marker remains a separate target reference and does not write knowledge. No scan of scene exits or arbitrary initial list of known exits is allowed.

**Review decision:** confirm whether #64 may include this narrow Spawn Point-to-Knowledge-State producer, or whether the valid Escape alternative must wait for the production card that implements real Spawn Point use. The current Mission has two scene NPCs but no such producer, so a debug setter, per-NPC synthetic fact, or authoring E01 as known without actual entry use would violate the source rule. If the prerequisite is deferred, #64 can still demonstrate applicability, `NoPlan`, and pursuit selection, but cannot yet claim two valid competing movement plans or weight-versus-cost selection across pursuit and escape.

This does not add a general target registry, scene scan, general gang-sharing framework, or a synthetic test-only write. If accepted, it adds only the typed used-entry fact and its narrow Gang-scope publication required by the existing GDD rule. Existing #68 facts and conflict rules remain unchanged.

## 6. Ownership and data model

All new runtime and authoring contracts belong to `Assets/PEGA/NPC`:

- **`PEGANpcArchetypeProfile`**: PEGA `ScriptableObject` referenced by each thief. It selects the Desire configuration for the Normal context, per-Desire base weight/satisfaction configuration, and one explicit deterministic priority-cost policy. It does not add components or Actions to an Actor.
- **Desire definition**: stable PEGA Desire ID, context, base weight, applicability predicate, desired fact predicate, satisfaction gain, satisfaction-to-weight modifier, and completed-execution limit. Applicability depends on context/profile/limits, not on whether a plan currently exists.
- **Per-NPC Desire state**: transient accumulated satisfaction keyed by Desire ID on the NPC root. It is never shared between thieves. Satisfaction survives a later loss of the world condition and is not serialized as a current fact.
- **Action planning contract**: a read-only query on each existing Action supplies planning Preconditions, predicted Effects, compatible known targets, resolved intrinsic duration, and execution binding. It must not call `TryStart`, change movement, write Knowledge State, or inspect the target's current Transform.
- **Plan and selection snapshots**: immutable output containing applicability, plan viability/reason, chosen Desire, ordered Action/Target steps, predicted terminal facts, estimated seconds, effective Desire weight, and deterministic score/tie-break details. Predicted facts are never published as observed facts.
- **Known exit fact/source**: typed PEGA knowledge for an entry actually used, with Gang scope as required by the GDD and a stable destination identity. It is not an Immersive `ActorId` or `ActorProfileId`; identities from different domains are never compared. This producer is conditional on the review decision in section 5.

The selector is invoked explicitly with the active context, profile, Knowledge State, current Action components, and individual satisfaction state. The current cut supplies `Normal`; it does not add a global mission-context authority.

## 7. Planning and selection behavior

1. Evaluate each configured Desire's applicability and completion limit independently of plan construction.
2. Read only accepted Knowledge State facts visible to this NPC, including an entry-use fact with Gang scope if the producer is in scope. Resolve candidate Action/Target operators from that input and the Actions physically present on the same NPC.
3. Build plans by matching operator Effects to Desire goal facts and applying operator Preconditions to the initial or projected planning state. Do not use fixed Action sequences or inspect hidden target positions.
4. For each navigation step, estimate route length with a side-effect-free NavMesh query using the same agent type/area mask and the resolved speed regime used by execution. Convert route length to estimated seconds and add resolved Action durations. Do not call `SetDestination` or move the Actor.
5. Reject a candidate if its target is not known, the Action is unavailable, its preconditions fail, its resolved speed/duration is invalid, or its NavMesh path is not complete. This removes the plan, not the Desire's applicable status.
6. Keep weight and estimated seconds as separate values. An archetype-owned `PEGANpcPlanSelectionPolicy` supplies a deterministic cost-preference curve. The initial policy computes `effectiveWeight × costPreference(estimatedSeconds)`; higher score wins. Both the Desire's base weight/satisfaction modifier and the cost curve are editable PEGA data, with provisional values. This allows a lower-weight Desire to win when its plan is sufficiently cheaper without imposing one universal GDD formula.
7. Resolve exact score ties by stable Desire ID, then Action ID, then Target ID in ordinal order. Never use component enumeration order, Unity instance IDs, arrival order, or random tie-breaking.
8. Emit one canonical decision log for an explicit evaluation, including applicable/plannable statuses, candidate IDs, target provenance, route seconds, action duration, effective weight, score, selected plan or no-plan reason. Do not log per frame.

The route estimate represents the current baked NavMesh and resolved speed, not a guarantee that execution will succeed. Physical execution still validates collisions and current World State through the existing Action lifecycle.

For a reproducible choice check, the temporary Unity fixture uses a configurable cost curve `1 / (1 + estimatedSeconds / referenceSeconds)` with `referenceSeconds = 10`. With pursuit weight `2`, pursuit cost `18s`, escape weight `1`, and escape cost `2s`, escape scores `0.833` versus pursuit `0.714`. Changing only pursuit weight to `3` makes pursuit score `1.071` and it wins. Resetting weights and changing only pursuit cost to `2s` makes pursuit score `1.667` and it wins. These values demonstrate independent weight/cost influence; they are not gameplay balance defaults. The curve and reference time remain authorable policy data.

## 8. Prediction, execution, and satisfaction

- A selected plan contains **predicted** Effects only. Selection neither starts an Action nor writes those Effects to Knowledge State.
- The existing Action execution snapshot remains the source of **observed** outcome and movement result. A later #91 executor may explicitly consume the selected plan; that integration is not part of #64.
- Satisfaction is evaluated only after an Action reports `Success` and a post-execution observation confirms the configured Desire predicate. For the initial movement Desires, the observed physical arrival at the selected known target is checked from the real navigation snapshot. `Success` by itself never increments satisfaction.
- A `PEGANpcDesireState` update records the configured gain and completed execution count for that Desire only. Interrupted, Invalid, Cancelled, abandoned, or successful-but-unsatisfying Actions add nothing.
- An explicit selector call after a Knowledge State change can produce a new plan and Target. No callback, polling loop, or event-driven re-planning is introduced; the event-driven behavior stays with the later re-planning work.

## 9. Manual validation approach

Use the existing PEGA Mission and its two thief NPCs; do not create a separate scene or auto-edit prefab composition. A temporary, manually attached validation probe in the same PEGA scene can invoke evaluation and controlled Action starts, then report a consolidated `PASS`/`FAIL` summary through the canonical logger. It must not generate synthetic Rick or exit evidence. E01 is known only if actual configured Spawn Point use provides the fact described in section 5; Rick facts come from #69 perception.

The user runs these checks in Unity:

1. With no accepted Rick identity/position and no used-entry knowledge fact, both Normal Desires report `applicable=true`, neither has a valid plan, and the selection reports `NoPlan`.
2. After real perception establishes Rick's identity and last position, and actual Spawn Point use establishes E01 as a known exit (if that producer is accepted), the planner reports two complete candidates with distinct Targets, predicted Effects, estimated costs, and no movement side effect.
3. Set a long pursuit route with a higher pursuit weight and a short E01 route with a lower escape weight. Confirm the configured cost policy selects according to both independent inputs; change only the weight or route opportunity and confirm the selected candidate changes deterministically.
4. Move Rick so #69 records a newer legitimate position and request evaluation again. Confirm the new plan target comes from the updated Knowledge State; do not expect automatic re-planning.
5. Manually execute each selected existing Action through the temporary probe. Confirm the prediction remains separate from the execution snapshot and only the Desire whose actual postcondition is observed receives satisfaction. A successful Action that satisfies the other Desire adds no satisfaction to that other Desire.
6. Confirm evaluation of the second NPC uses its own Knowledge State and satisfaction ledger.
7. Remove temporary validation components after review; keep permanent PEGA profiles/state components only after the user authors them in the Inspector.

No Unity import, build, EditMode/PlayMode test, smoke, or batchmode command is run by Codex. Static source/diff inspection does not replace the user's Unity validation.

## 10. Boundaries and exclusions

- No automatic plan execution, task loop, Update-driven decision, or autonomous behavior integration from #91.
- No event subscription, automatic plan invalidation/reselection, loss-of-target timer, or re-planning behavior from later cards.
- No perception, general target discovery, hidden-world scan, general gang-sharing framework, generic event bus, route-risk model, randomness, or new gameplay Action. The only possible addition is the narrow used-entry fact source described in section 5.
- No complete Normal-context Desire catalogue, Item Principal in Play or Final Escape context implementation, contingency Desire, capture/combat, theft, or investigation behavior.
- No Framework source/package changes and no reuse of Framework Actor Profiles as NPC archetypes.
- No hand-edits to scene or prefab YAML and no automatic component attachment. User authoring and Unity validation remain manual.

## 11. Risks and review decisions

1. **Known E01 source:** the two-plan acceptance needs the real Spawn Point use fact, absent in the workspace. Confirm inclusion of that narrow producer in #64 or defer the escape plan until the relevant production card provides it. Do not claim scene presence or a debug injection as knowledge.
2. **One-step repertoire:** the two current movement Actions produce only one-step plans. This is genuine goal/effect matching and plan choice across two applicable goals, but does not yet exercise a multi-Action chain or two alternative plans for one Desire. No new Action should be invented solely to make the chain longer.
3. **Policy tuning:** the weight-cost curve and the two Desire values are provisional. The policy must remain editable and deterministic; Unity playtesting owns balancing.
4. **NavMesh estimate:** a complete current NavMesh path and maximum speed estimate time but cannot guarantee the CharacterController will reach the target. Runtime result remains authoritative.
5. **Immersive API maturity:** `ActorId` and `ActorProfileId` are used only in their own domains through the resolved Framework contract. No Framework change or migration is proposed.

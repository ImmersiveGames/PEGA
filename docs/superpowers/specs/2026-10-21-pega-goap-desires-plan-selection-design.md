# PEGA GOAP Desires and Plan Selection

- **Recorded status (2026-10-21):** The original status reported implementation and Unity manual validation passed for the verified scope. The companion plan retains unchecked items, and this document set does not map the reported verification to individual plan steps. Preserve both records without inferring completion or non-completion of those steps or the full #64 acceptance. Live selection between pursuit and escape was recorded as pending legitimate knowledge of E01.
- **Date:** 2026-10-21
- **Scope:** First implementation slice for Trello [#64 — Desires and plan selection](https://trello.com/c/iWwXiDee/64-npc-desejos-e-escolha-de-planos)

## 1. Goal

Implement the first PEGA-owned Desire configuration and GOAP plan-selection capability. Validate the selection algorithm in isolation using detached planning inputs, then validate live NPC integration only against facts the NPC actually knows. These are separate evidence tracks: an isolated choice result does not prove a gameplay Target or Desire is available. The implementation does not execute a selected plan autonomously, poll for changes, subscribe to replan events, add new gameplay Actions, or author a definitive mission Desire configuration.

## 2. Authority and traceability

- **Trello #64** is in `Em andamento`. Its acceptance requires: configure an initial Desire with applicability and satisfaction; compose/select plans from available Actions; separate plan prediction from observed execution; and demonstrate a different plan after known facts change. The revised card also requires viable alternatives, a no-solution result, and configurable deterministic resolution of priority and time cost.
- **GDD — “Peso, oportunidade e custo dos planos”** defines estimated seconds as movement time plus resolved Action durations, treats Desire weight and plan cost as independent inputs, leaves their combination configurable, and requires deterministic tie resolution.
- **GDD — “Contrato de Actions”** separates planning from physical execution, applies predicted Effects only after successful completion, and requires execution to validate reality.
- **GDD — “Contextos e satisfação dos Desejos”** defines per-archetype Desire configuration, the Normal context, applicability independent of plan availability, individual accumulated satisfaction, and satisfaction only after a completed Action produces the actual desired state.
- **GDD — “Conhecimento modular, memória e validade da informação”** requires the planner to resolve Targets only from known candidates; scene presence alone cannot make an object known.
- **GDD — “Fuga e escolha de saída”** says used entry points and discovered exits become known targets. It does not authorize treating every scene exit marker as NPC knowledge.
- **OBJ-08** is broader than #64 and includes autonomous execution and re-planning. This slice only supplies Desire evaluation and a selected plan value for later consumption.
- No GDD, MVP, TASKS, ADR, or Trello content is modified by this design.

### Scope of the demonstrative slice and later architecture

This document describes the first #64 slice. Its one-Action plan selection is a limitation of that slice and is not a general GOAP architecture decision. The later accepted [ADR-GOAP-01](../../../PEGA%20GDD/ADRs/ADR-GOAP-01-composicao-e-fronteiras.md), [ADR-GOAP-02](../../../PEGA%20GDD/ADRs/ADR-GOAP-02-modelo-causal-e-planejamento.md), and [ADR-GOAP-03](../../../PEGA%20GDD/ADRs/ADR-GOAP-03-selecao-e-estabilidade.md), together with the current [GDD GOAP contract](../../../PEGA%20GDD/GDD.md#planejamento-inimigo-por-goap), define the later architectural target: Goals are independent of Actions and candidate plans may be causal chains.

The current [TASKS record](../../../PEGA%20GDD/TASKS.md) separates the recorded causal foundation (#106) from its integration into real plan selection (#107). This #64 slice neither replaces that integration task nor asserts that it has been completed. The status above preserves what this document reported for its verified scope; it does not reconcile the unchecked items in the companion plan or establish broader acceptance without separate evidence.

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

An isolated choice check needs at least two valid plan candidates, a no-plan case, and changes to weight/cost that change the winner. Use a temporary demonstration configuration with two clearly labeled demo-only Desires and the two existing #63 movement operators:

1. **Demo: approach known Rick position** — `ApproachKnownRickAction` predicts arrival at the position in the supplied planning input.
2. **Demo: move to known escape target** — `TravelToEscapeAction` predicts arrival at the supplied known target. Reaching its marker is not the terminal mission effect `Escaped`.

The selector matches declared Action preconditions/effects to each goal; the demo profile does not bind a Desire directly to an Action. Each plan is one step because these are the available movement operators. This verifies deterministic choice among plan candidates, but not multi-Action chain search.

The demonstration inputs belong to a **detached isolated planning fixture** passed directly to the selector. The escape candidate is an immutable operator/Target value representing the declared `TravelToEscapeAction` effect; it does not invoke that Action's runtime target resolution. The fixture data is not written to `PEGANpcKnowledgeState`, assigned to an NPC, resolved from hidden scene objects, or treated as accepted gameplay facts. Log the mode as `IsolatedFixture` so the result cannot be confused with live integration.

## 5. Live knowledge and gameplay-content boundary

The GDD says a gang learns an entry as a possible exit only after a member uses its Spawn Point, or learns an additional exit through discovery (GDD §§ “Fuga e escolha de saída” and “Spawn Points e receitas de invasão”). E01 is currently only a scene destination marker; the workspace has no Spawn Point use event, known-exit fact, or gang-knowledge producer. #64 will not add those systems, treat E01 as known, or configure the escape demo Desire as mission behavior.

The live planner reads only the NPC's accepted Knowledge State and Targets resolved from that knowledge. In the current Mission, Rick's last observed position can come from real #69 perception and feed `ApproachKnownRickAction`; an escape candidate is unavailable until a legitimate known-exit source exists. Report that absence explicitly. Do not scan the scene, convert `PEGANpcEscapeDestination` into a fact, or inject an exit into a live NPC.

The GDD approves thieves using GOAP and describes context-dependent pursuit of a **known principal-item carrier** and configurable early escape. It does not define **approach Rick** as a default Desire for a thief archetype; Rick and Petra remain player-controlled, not GOAP agents. The current #68 Knowledge State also has no principal-item possession/carrier fact, and no known-exit producer. Therefore, `Demo: approach known Rick position` and `Demo: move to known escape target` exist only in temporary validation configuration. The implementation provides authorable Desire contracts, but does not create or assert a production archetype profile/weight set or an accepted mission behavior until its gameplay facts and content are approved.

Validation has two independent outcomes: (a) isolated selector checks establish the algorithm can choose among valid candidates; (b) live integration checks establish the current NPC uses real Knowledge State and existing Actions correctly. Passing (a) does not establish live escape planning, a mission-approved pursuit Desire, or the card's real-known-Target alternatives criterion. That criterion remains unaccepted until the project has at least two legitimate live candidates for the approved Desire content.

## 6. Ownership and data model

All new runtime and authoring contracts belong to `Assets/PEGA/NPC`:

- **`PEGANpcDesireProfile`**: PEGA `ScriptableObject` contract for selecting Desire configuration and a deterministic priority-cost policy by context. It can be assigned per archetype by game content, but the first cut does not populate a definitive mission profile or weight set; demo configuration remains temporary. It does not add components or Actions to an Actor and is distinct from Immersive `ActorProfile`.
- **Desire definition**: stable PEGA Desire ID, context, base weight, applicability predicate, desired fact predicate, satisfaction gain, satisfaction-to-weight modifier, and completed-execution limit. Applicability depends on context/profile/limits, not on whether a plan currently exists.
- **Per-NPC Desire state**: transient accumulated satisfaction keyed by Desire ID on the NPC root. It is never shared between thieves. Satisfaction survives a later loss of the world condition and is not serialized as a current fact.
- **Action planning contract**: a read-only query on each existing Action supplies planning Preconditions, predicted Effects, compatible known targets, resolved intrinsic duration, and execution binding. It must not call `TryStart`, change movement, write Knowledge State, or inspect the target's current Transform.
- **Plan and selection snapshots**: immutable output containing applicability, plan viability/reason, chosen Desire, ordered Action/Target steps, predicted terminal facts, estimated seconds, effective Desire weight, and deterministic score/tie-break details. Predicted facts are never published as observed facts.
- **Known Targets**: planning candidates are resolved from accepted Knowledge State through an explicit read-only query. The selector never promotes `PEGANpcEscapeDestination` or another scene object into knowledge. No known-exit fact/source is added in #64.
- **Isolated planning input**: temporary detached state and operator/Target data used only to verify selection logic; it is never assigned to or written into an NPC Knowledge State.

The selector is invoked explicitly with the active context, profile, Knowledge State, current Action components, and individual satisfaction state. The demonstration passes `Normal` as test input; it does not add a global mission-context authority or assert a permanent Normal-context Desire set.

## 7. Planning and selection behavior

1. Evaluate each configured Desire's applicability and completion limit independently of plan construction.
2. In live mode, read only accepted Knowledge State facts visible to this NPC and compatible Action components on its root. In isolated validation mode, accept a detached immutable planning input and do not resolve scene objects or access an NPC Knowledge State.
3. Build plans by matching operator Effects to Desire goal facts and applying operator Preconditions to the initial or projected planning state. Do not use fixed Action sequences or inspect hidden target positions.
4. For each navigation step, estimate route length with a side-effect-free NavMesh query using the same agent type/area mask and the resolved speed regime used by execution. Convert route length to estimated seconds and add resolved Action durations. Do not call `SetDestination` or move the Actor.
5. Reject a candidate if its target is not known, the Action is unavailable, its preconditions fail, its resolved speed/duration is invalid, or its NavMesh path is not complete. This removes the plan, not the Desire's applicable status.
6. Keep weight and estimated seconds as separate values. An archetype-owned `PEGANpcPlanSelectionPolicy` supplies a deterministic cost-preference curve. The initial policy computes `effectiveWeight × costPreference(estimatedSeconds)`; higher score wins. Both the Desire's base weight/satisfaction modifier and the cost curve are editable PEGA data, with provisional values. This allows a lower-weight Desire to win when its plan is sufficiently cheaper without imposing one universal GDD formula.
7. Resolve exact score ties by stable Desire ID, then Action ID, then Target ID in ordinal order. Never use component enumeration order, Unity instance IDs, arrival order, or random tie-breaking.
8. Emit one canonical decision log for an explicit evaluation, including applicable/plannable statuses, candidate IDs, target provenance, route seconds, action duration, effective weight, score, selected plan or no-plan reason. Do not log per frame.

The route estimate represents the current baked NavMesh and resolved speed, not a guarantee that execution will succeed. Physical execution still validates collisions and current World State through the existing Action lifecycle.

For a reproducible isolated choice check, the temporary fixture uses a configurable cost curve `1 / (1 + estimatedSeconds / referenceSeconds)` with `referenceSeconds = 10`. With demo pursuit weight `2`, demo pursuit cost `18s`, demo escape weight `1`, and demo escape cost `2s`, escape scores `0.833` versus pursuit `0.714`. Changing only pursuit weight to `3` makes pursuit score `1.071` and it wins. Resetting weights and changing only pursuit cost to `2s` makes pursuit score `1.667` and it wins. These values exist only in the detached validation fixture; they are not gameplay balance defaults. The curve and reference time remain authorable policy data.

## 8. Prediction, execution, and satisfaction

- A selected plan contains **predicted** Effects only. Selection neither starts an Action nor writes those Effects to Knowledge State.
- The existing Action execution snapshot remains the source of observed movement outcome. Its current `EffectObserved` property is equivalent to `Outcome == Success`; it must not be treated alone as proof that a Desire predicate became true. A later #91 executor may explicitly consume the selected plan; that integration is not part of #64.
- After any Action completes, evaluate its observed post-execution state against the satisfaction predicates of **all Desire definitions exposed by the active context**, not only the Desire that selected the plan. A completed Action may satisfy multiple Desires; increment each individual ledger whose predicate is actually true. For movement, require a successful execution snapshot with a resolved target and measured physical distance within the declared arrival radius, then match the observed Action effect/Target to each predicate. `Success` alone never increments satisfaction.
- A `PEGANpcDesireState` update records each matching Desire's configured gain and completed-execution count independently. Interrupted, Invalid, Cancelled, abandoned, or successful-but-unsatisfying Actions add nothing. Plan origin and current selection do not filter satisfaction checks.
- An explicit selector call after a Knowledge State change can produce a new plan and Target. No callback, polling loop, or event-driven re-planning is introduced; the event-driven behavior stays with the later re-planning work.

## 9. Manual validation approach

Use the existing PEGA Mission; do not create a separate scene. A temporary Editor setup utility prepared the demo-only Desire profile, ensured the definitive `PEGANpcDesireState` component on the NPC prefab, and attached/wired the probe to the existing Mission Actors. The probe reported separate summaries for `IsolatedFixture` and `LiveKnowledge` through the canonical logger. Isolated facts, targets, and costs were passed to the selector as detached values, never written to either NPC. Live checks used the NPC's real Knowledge State. After validation, the probe, setup utility, demo profile, and scene attachment were removed; the definitive Desire State component remains.

The user runs these checks in Unity:

1. **Isolated no-plan:** detached demo Desires remain applicable with no matching Target/operators; report `NoPlan`, without mutating an NPC.
2. **Isolated alternatives:** provide two detached valid plans and confirm the selector returns both candidates and one deterministic winner; log `validationMode=IsolatedFixture`.
3. **Weight/cost:** use the numeric policy values in section 7, changing one input at a time; confirm both can independently change the selected plan and exact ties use stable IDs, not input order.
4. **Multiple satisfaction:** use one physically completed movement Action and two demo-only Desire definitions whose satisfaction predicates both match that same observed effect and Target. Confirm both per-NPC ledgers gain their configured amounts regardless of which Desire originated the plan. Invalid, cancelled, or otherwise non-arrived execution gains nothing. The probe does not fabricate an observed result or write synthetic knowledge.
5. **Live Knowledge State:** with the temporary demo profile on one existing NPC, let #69 produce Rick identity/last position. Confirm only the pursuit demo candidate resolves from that real fact. Confirm escape is absent because E01 is merely a scene marker and no known-exit fact exists; this is an explicit unavailable capability, not a failure of the isolated selector.
6. **Live change and execution:** move Rick, allow #69 to record a newer position, explicitly reevaluate, then manually start the #63 Action. Confirm the predicted Target comes from Knowledge State, movement is not started by selection, and the observed completion remains separate from prediction. Do not expect automatic replanning.
7. Confirm each NPC's live evaluation reads only its own current Knowledge State and satisfaction ledger. The current Mission can prove one live Rick candidate and a physical execution, but it cannot prove real alternative selection across escape and pursuit: E01 is not a known Target. Keep that first-card acceptance item open until legitimate live alternatives exist. Remove the temporary probe and demo configuration after review.

**Unity validation reported by the user (2026-10-21):** the controlled smoke completed with **26 PASS, 0 FAIL, 0 UNAVAILABLE**, including cancellation during physical movement without satisfaction and a newer perception-derived Rick position followed by explicit plan reselection. This complements the earlier isolated selector, live planning, physical Action execution, and Desire satisfaction validations. These results validate the capabilities exercised by those checks; they do not establish live pursuit-versus-escape choice.

**Still pending:** live selection between pursuing Rick and escaping to E01 after E01 is legitimately known by the NPC. The current Knowledge State has no known-exit fact or producer. Do not read the scene escape marker as knowledge, implement the missing producer in #64, or close #64 on this evidence.

No Unity import, build, EditMode/PlayMode test, smoke, or batchmode command is run by Codex. Static source/diff inspection does not replace the user's Unity validation.

## 10. Boundaries and exclusions

- No automatic plan execution, task loop, Update-driven decision, or autonomous behavior integration from #91.
- No event subscription, automatic plan invalidation/reselection, loss-of-target timer, or re-planning behavior from later cards.
- No Spawn Point system, gang-knowledge propagation, target discovery, hidden-world scan, generic event bus, route-risk model, randomness, or new gameplay Action.
- No definitive `Approach Rick` Desire or production archetype weight configuration; the Rick Desire and choice values exist only in temporary demonstration content.
- No complete Normal-context Desire catalogue, Item Principal in Play or Final Escape context implementation, contingency Desire, capture/combat, theft, or investigation behavior.
- No Framework source/package changes and no reuse of Framework Actor Profiles as NPC archetypes.
- The temporary Editor setup utility, probe, and demo profile are removed after validation; no permanent validation infrastructure remains. The definitive `PEGANpcDesireState` component stays on the NPC runtime prefab.

## 11. Risks and review decisions

1. **No live exit source:** the current project has no fact making E01 a known Target. #64 supports only known Targets; real escape integration is deferred and cannot be marked accepted here.
2. **No approved live pursuit/escape content yet:** GDD supports pursuit of a known principal-item carrier and configurable escape, but the current Knowledge State has no carrier fact and no known-exit source. Keep Rick approach and escape alternatives in detached/demo validation; do not author a production archetype choice or balance values.
3. **One-step repertoire:** the two existing movement Actions produce one-step candidate plans. The isolated choice check does not exercise a multi-Action chain.
4. **Policy tuning:** the weight-cost curve and demo values are provisional. The policy must remain editable and deterministic; Unity playtesting owns balancing.
5. **NavMesh estimate:** a complete current NavMesh path and resolved speed estimate time but cannot guarantee the CharacterController will reach the target. Runtime result remains authoritative.
6. **Immersive API maturity:** `ActorId` and `ActorProfileId` are used only in their own domains through the resolved Framework contract. No Framework change or migration is proposed.

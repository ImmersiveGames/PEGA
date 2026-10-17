---
name: pega-objective-specification
description: Use when a PEGA macro objective needs a traceable behavioral contract and verifiable acceptance before production planning or implementation.
---

# PEGA Objective Specification

## Purpose

Turn a selected PEGA macro objective into a precise description of what the game must do, under which conditions, according to which rules, and with what observable result. Specify behavior, not a technical solution.

Use an existing player/gameplay analysis and macro objective map as the starting point. Consult the original sources for every rule that shapes behavior or acceptance. This skill specifies only the objective or objectives requested; it does not regenerate the full GDD analysis or objective catalog.

## Source authority

- **GDD:** normative game behavior and rules.
- **MVP:** delivery scope and acceptance for the vertical slice and Demo.
- **ADR:** approved production/architecture decisions within their stated scope. The microcycle is a subset of the vertical slice, not a new MVP scope. Under ADR PP-37, capability development precedes test-environment preparation and pursuit composition; the older microcycle remains historical coverage, not a sequence of implementation stages.
- **Macro analysis:** navigation and derived grouping, not authority to change a rule or scope.
- **TASKS/Trello:** operational planning, not a source of game behavior.
- **Code/assets:** evidence of implementation, not authority to invent or resolve design rules.

When sources conflict, cite both claims and their locations. Do not choose one silently. Do not expose credentials or change GDD, MVP, ADR, TASKS, Trello, or gameplay implementation during specification.

## Method

1. **Select the objective boundary.** Start from the requested macro objective and its family/parent. Keep the macro-analysis label as a trace reference, separate from the objective-specification ID. Use a stable ID from the objective catalog when one exists; otherwise assign a provisional local ID for the requested specification. Never use ADR delivery IDs (MC-, VS-, DEM-), TASKS IDs, or Trello IDs as objective IDs; cite them as production-scope references. If no macro analysis exists, route through the player-cycle, gameplay-cycle, and activity-planning skills before specifying a full set of objectives. Do not create a full catalog unless requested.
2. **Trace the contract.** Read the relevant GDD rules, MVP scope, and applicable ADR decisions. Record file and section/decision references for each material statement. Distinguish direct source rules from a conclusion derived by combining cited rules.
3. **Define one coherent outcome.** State what changes in the game and how the outcome can be recognized. Keep separate outcomes separate when each can be accepted independently. Keep states, timers, events, controls, mechanics, and content examples as rules or checks when they support a larger outcome rather than defining a separate goal.
4. **Specify conditions and behavior.** Record only source-supported preconditions, triggers, information available to each actor, transition rules, consequences, final states, invariants, and relevant variations or negative cases. Distinguish the player's intention/action from the world response when both matter. A derived relation may combine explicit rules only when it does not choose an unstated trigger, order, or state-transition moment. If it does, mark the result unresolved instead of making it an acceptance expectation.
5. **Make acceptance verifiable.** Write a small set of observable scenarios using the acceptance table below. Every criterion needs its own direct source references or an explicit “derived from” label with all supporting references. Include a negative case when it proves an important boundary or prevents a false success. Keep test setup values that are merely fixtures separate from game rules. An unsupported expected result is a gap or product decision, not an acceptance criterion.
6. **Mark boundaries and relationships.** State what this objective includes, what belongs to adjacent objectives, and real functional dependencies or shared rules. These relationships do not establish technical implementation order.
7. **Classify gaps and readiness.** Separate missing rules from values/content that can be configured later. State whether each gap blocks identifying the objective, specifying its behavior, defining acceptance, or integrating it with another objective. Report source conflicts explicitly. Do not call an objective fully specified when an essential rule is still unresolved.

## Objective contract

Use the fields that apply; omit unsupported or irrelevant fields instead of filling them artificially.

| Field | Required content |
|---|---|
| Identity | Stable objective-specification ID, name, macro-analysis label/family/parent as separate references, and source references. If no catalog identity exists, assign a provisional local ID for the requested specification. ADR MC-/VS-/DEM-, TASKS, and Trello IDs are production references, never objective IDs. |
| Purpose | The game result or player experience that justifies the objective. |
| Observable outcome | What happens and what observation distinguishes success from non-completion. |
| Preconditions and dependencies | Required initial states and functional dependencies supported by sources. |
| Expected behavior | Rules, triggers, information constraints, transitions, and consequences needed to define the outcome. |
| Final conditions and invariants | Resulting states and guarantees that must remain true. |
| Variations and exceptions | Source-defined alternatives, interruptions, failures, or negative cases relevant to acceptance. |
| Acceptance criteria | A short table with one row per scenario: setup (Given), event/action (When), observable pass condition (Then), failure boundary, required evidence, and direct sources or derived-from references. |
| Boundaries and relations | Included and excluded behavior; independent neighboring outcomes; dependencies, shared rules, and integrations. |
| Delivery scope | First microcycle when relevant, complete vertical slice, Demo, post-Demo, optional/playtest, or unallocated—only as supported by MVP/ADR. |
| Gaps | Missing rule/decision, source conflict, acceptance/integration gap, or detail that can wait. |

Use this shape when acceptance is needed:

Delivery scope classifies the gameplay coverage an objective belongs to; it does not establish technical order. Use ADR PP-37 for production priority: satisfy Player capabilities needed for pursuit, then NPC movement/autonomy with the real GDD systems, prepare the joint test environment, compose and evaluate the central chase, expand gameplay mechanics afterward, and mature the complete vertical slice/Demo. The GDD/MVP scope remains unchanged. Do not make a subsystem wait for an artificial mini-cycle merely because its objective is scoped to that gameplay subset.

| Scenario | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
Acceptance is a behavior contract, not a list of scripts or a proposed test suite. It must not depend on an implementer inventing a rule essential to the outcome. If a necessary rule is absent, write the supported portion and mark the criterion blocked by that specific gap.

## Gap classes

- **Objective identification:** the intended result or necessary relationship cannot be identified.
- **Behavior specification:** the objective is clear, but a rule needed to determine what the game must do is absent.
- **Acceptance:** behavior is mostly described, but an observable pass/fail condition cannot yet be stated.
- **Integration:** this objective's input/output or relationship to another objective is not defined well enough to establish the handoff.
- **Source conflict:** normative sources make incompatible claims about the same behavior under the same conditions; cite both claims and leave resolution to the source owner. Ambiguous wording is not a conflict by itself. If multiple readings can fit the sources, record the competing readings as a behavior-specification or acceptance gap and do not choose one silently.
- **Can wait:** balancing, mission values, content selection, or presentation detail can be supplied later without changing the objective's required behavior or its current acceptance boundary.

A gap can leave an objective identifiable while blocking its behavior specification or acceptance. Do not merge those judgments. Do not turn an unspecified but nonessential configuration value into a blocker.

## Boundaries

- Keep **product objectives** distinct from an NPC's **GOAP Desire/Goal**.
- Recovering an item and completing a larápio's capture are separate outcomes when each can succeed without the other.
- A choice, the world state it selects, and a timed phase transition may be related without being one objective or three objectives automatically; use independent observable outcomes and source boundaries to decide.
- Preserve the difference between “not automatically disclosed” and “never discoverable.” Do not add a privacy rule stronger than the sources.
- Do not infer tie-breaking for simultaneous inputs from “last valid choice wins”; record the unspecified case.
- Do not prescribe or create Unity Scenes, Routes, Activities, Actors, classes, components, APIs, scripts, prefabs, assets, input actions, or technical work order. Leave architectural choices open unless the user separately requests technical planning.

## Output

Return, in order:

1. The selected macro objective(s), source set, and applicable delivery scope.
2. One behavioral contract per requested objective using the applicable fields above.
3. A compact trace map showing where material source rules are specified, out of scope, or unresolved.
4. Gaps and conflicts, classified by the point they block; distinguish them from details that can wait.
5. A readiness statement for behavior specification, acceptance, and cross-objective integration. Readiness of the contract does not mean implementation is complete or validated.

### Short example of a source boundary

“In sequential valid selections, the last selection prevails” is a supported acceptance condition when traced to the GDD. The source does not define precedence for two choices accepted at the same instant; record that as an acceptance gap rather than adding a player priority or technical ordering rule.

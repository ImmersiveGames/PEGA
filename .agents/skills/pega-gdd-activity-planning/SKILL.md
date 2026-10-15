---
name: pega-gdd-activity-planning
description: Use when combining PEGA player and gameplay GDD analyses into macro objectives, checking coverage, or identifying product gaps before planning work.
---

# PEGA GDD Activity Planning

## Purpose

Turn the GDD's player and gameplay goals into a complete, traceable set of macro activity objectives. The source is the game outcome; activities are deliverables that make those outcomes possible.

## Method

1. Read the player-cycle and gameplay-cycle analyses alongside the GDD and MVP. Use an ADR only to clarify an approved production decision. Preserve the source hierarchy: GDD defines game behavior, MVP defines delivery scope, ADR records decisions, TASKS/Trello operationalize approved work.
2. List the player outcomes and world outcomes separately before proposing activities. Link outcomes that cause, enable, or reveal each other. For every outcome, record whether a candidate activity covers it, another outcome depends on it, or its scope is outside/unallocated in the MVP. Do not silently drop an outcome because the slice has one choice or one example.
3. Derive a goal hierarchy from GDD outcome statements, not from each verb, state, rule, or input bullet. The input analysis bullets are evidence, not one-card-per-bullet instructions. Each activity row is a macro objective that delivers a meaningful player outcome or a complete world capability. For each row, state one observable completion outcome and list its supporting source outcomes/checks. A timer transition, NPC arrival/decision/action, item-state change, or similar rule is a check unless the GDD defines it as an independent game goal.
4. Preserve distinct top-level outcome families even when they happen in one mission. For PEGA, mission timing/transitions, autonomous robber behavior, player response and item/capture outcomes, and mission resolution can be distinct activity families when the GDD defines each. Keep each family as a macro row and put its specific mechanics under checks; do not make one row per check. The full mission/cycle can organize these families but is not automatically one activity.
5. Keep two objectives separate when each gives a meaningful player/world outcome that can be accepted and used on its own. Keep character selection and mission selection separate when the GDD gives each its own choice; materializing the selected character is acceptance for character selection, not a reason to combine it with mission selection or the whole gameplay cycle. Do not split an umbrella goal into individual verbs unless the GDD gives those verbs independent goals/results. If the source does not establish the boundary, mark it uncertain. A row count above the source macro goals is a prompt to check whether supporting conditions became activities; explain each justified independent outcome.
6. Compare the candidate objectives against the GDD and MVP for coverage, scope, dependencies, overlaps, missing outcomes, and source conflicts. Keep the player and gameplay perspectives visible in the final map.
7. Classify each gap at the level where it matters:
   - **Blocks defining an objective now:** the intended game outcome or necessary dependency cannot be identified without a product decision.
   - **Source/scope conflict:** GDD, MVP, or ADR make incompatible claims; cite both and leave resolution to the source owner.
   - **Can wait:** balancing, exact content, configuration, presentation detail, or a rule needed only to refine acceptance later, when the macro objective is already clear.
   Do not call a detail a blocker simply because its final value or UI is unknown.
8. Present genuine product decisions as concise evidence-backed questions/options. Keep recommendations separate from source statements. Do not edit GDD, MVP, TASKS, ADR, Trello, or implementation files during analysis.

## Output

Deliver, in this order:

1. the goal and MVP frame;
2. a short player-cycle lane and gameplay-cycle lane, linked where causally related;
3. candidate macro activities, each with one objective and observable completion outcome, grouped supporting checks, MVP scope, and dependencies;
4. a coverage/gap summary using the three gap classes above;
5. product decisions that block defining objectives, phrased for the user to resolve.

Use only enough checks or detail to explain an objective boundary, dependency, scope, or gap. Analysis IDs are local labels, never Trello card IDs. Do not prescribe Unity scenes, Framework concepts, APIs, components, scripts, assets, or implementation order.

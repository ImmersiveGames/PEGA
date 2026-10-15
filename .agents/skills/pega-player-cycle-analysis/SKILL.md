---
name: pega-player-cycle-analysis
description: Use when analyzing the player journey, player choices, actions, feedback, or experience goals described in a PEGA GDD and MVP.
---

# PEGA Player Cycle Analysis

## Purpose

Describe the experience the game must let the player have, from entering a game flow through play and its outcome. Keep this analysis at the level of meaningful player goals and decisions; the activity-planning skill decides which outcomes are separate macro objectives, not production deliverables.

## Method

1. Read the GDD sections describing the main loop, modes and menus, player verbs, mission flow, feedback/HUD, results, and continued play. Use the MVP to mark each outcome's delivery scope. Consult an ADR when it changes that scope.
2. Trace the player journey in order, including pre-mission choices, play, resolution, and any continued-play loop stated by the sources.
3. For each major phase, state the player's goal or decision, the representative actions, and the feedback or outcome the player can observe. Keep distinct decisions visible when each produces its own observable result, such as choosing a character versus choosing a mission. Do not expand every control or interaction into a separate macro objective.
4. Keep NPC decisions, clocks, spawning, and other world simulation in the gameplay perspective. Mention them only as a cause, constraint, or feedback the player encounters.
5. Mark each outcome as slice, Demo, later, optional, or unresolved using the MVP. If a source does not define an identity, order, rule, or feedback, name the gap without choosing an answer.

## Output

Give a short ordered journey with one row or bullet per major player goal/phase. Include:

- player goal or decision;
- representative action and observable feedback/result;
- GDD/MVP scope and section references;
- material dependency or open question, when one affects the journey.

Close with a compact scope/gap summary. Preserve the player's perspective; do not turn the analysis into a card breakdown or prescribe Unity scenes, Framework concepts, APIs, components, scripts, assets, or implementation order.

This analysis describes the player-facing journey and macro outcomes; it is not a complete behavioral acceptance contract. When a selected macro objective needs precise behavior and verifiable acceptance, hand it to `pega-objective-specification` without replacing or repeating this journey analysis.

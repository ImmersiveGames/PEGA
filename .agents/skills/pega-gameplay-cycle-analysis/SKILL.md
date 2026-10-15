---
name: pega-gameplay-cycle-analysis
description: Use when analyzing GDD-defined world simulation, NPC behavior, timers, objects, items, power-ups, or mission resolution for a PEGA game.
---

# PEGA Gameplay Cycle Analysis

## Purpose

Describe the observable outcomes the game world must produce across a mission. This is the simulation perspective, connected to but separate from the player's journey.

## Method

1. Read the GDD's mission loop, time and phase rules, NPC/AI behavior, interactive environment, item states, and resolution. Use the MVP to classify delivery scope; consult an ADR only when it changes the approved production scope.
2. Trace the world cycle from mission setup through timed progression, NPC entry/knowledge/decisions/actions, relevant object and item responses, phase transitions, and final resolution. Include power-ups only at their stated scope. Include other NPC classes only where the GDD defines their behavior.
3. Group specific rules under a small set of macro world outcomes. Keep a mechanic or content example as supporting detail unless it produces an independently observable outcome. Keep player actions as inputs/causes; describe the resulting world state or response here, and leave the player's goal/action/feedback analysis to the player-cycle skill.
4. For every outcome, distinguish GDD behavior from mission content/configuration. Mark scope as slice, Demo, later, optional, or unresolved only when the MVP supports that label. A GDD feature with no established MVP scope remains unallocated; do not promote every described feature into a slice or Demo requirement.
5. Record material gaps where a missing rule prevents defining an outcome. Leave values and content choices that can be supplied later as configuration/content detail, without inventing them.

## Output

Give a concise ordered cycle or table of macro world outcomes. Include:

- observable world outcome and its defining rule;
- GDD/MVP scope with section references;
- player-cycle relationship, when it creates or reveals the outcome;
- material gap or later content/configuration detail, when relevant.

Cover the temporal cycle, NPC/simulation cycle, interactive-world/item outcomes, power-up scope, and mission resolution where the sources define them. Preserve the separation from player activities. Do not prescribe Unity scenes, Framework concepts, APIs, components, scripts, assets, or implementation order.

# PEGA GDD Activity Analysis Skills Implementation Plan — Concluído

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` for native execution or `superpowers:subagent-driven-development` for subagent-driven execution. Each skill must complete its own baseline, write, pressure-test, correction, and deployment cycle before work proceeds to the next skill.

**Goal:** Add three project-local skills that separately analyze the player cycle, gameplay cycle, and macro activity plan/gaps, then route the existing PEGA GDD-analysis workflow through them.

**Architecture:** Create one focused skill per analytical responsibility and make `pega-production-governance` route GDD activity analysis through all three. Keep the integrated analysis document at macro activity level, preserve the two perspectives and their links, and classify unresolved gaps without inventing or silently editing normative sources.

**Tech Stack:** Project-local Markdown skills in `.agents/skills/`; GDD/MVP source reading; fresh-context subagent pressure scenarios; static Markdown/frontmatter review. No Unity execution or external Trello writes.

**Spec:** `docs/superpowers/specs/2026-10-15-pega-gdd-analysis-skills-design.md`

## Global Constraints

- GDD is normative for game behavior; MVP sets scope and acceptance; ADR records production decisions; TASKS and Trello do not define game goals.
- Find the correct macro activities before adding detailed checks. Add detail only to distinguish outcomes, dependencies, or material gaps.
- Keep player outcomes and gameplay/world outcomes as separate, causally linked perspectives.
- Missing rules remain explicit gaps. Do not choose gameplay behavior or technical implementation by convention.
- Do not edit GDD, MVP, TASKS, ADR, Trello, GitHub, or GanttFlow as part of this plan.
- Keep the skills project-local. Do not edit global Codex settings or copy global skills into PEGA.
- Do not run Unity build/import/tests, Play Mode, smoke, or batchmode.
- Execution used the existing visible PEGA checkout on `main`; the user authorized skill edits and clarified that gameplay implementation was out of scope. No worktree or branch was created.
- `.git` initially required reviewed write access; access was granted. Each skill received a path-limited local commit after its validation. No commit was pushed.

## Review Focus

- A player-cycle output includes autonomous NPC/system behavior as if it were a player action.
- A gameplay-cycle output misses clocks, NPC entry/decision/action, interactive objects, item states, or mission resolution.
- A synthesis merges dependent-but-independently-acceptable outcomes, or makes one card cover several macro objectives.
- A GDD/MVP omission is silently filled in instead of classified as a decision gap or later content detail.
- A product-level activity analysis prescribes a Unity Scene, Framework Route/Activity, Actor, prefab, API, component, or script.

---

### Task 1: Player-Cycle Analysis Skill

**Files:**
- Create: `.agents/skills/pega-player-cycle-analysis/SKILL.md`
- Update: `docs/superpowers/reports/2026-10-15-pega-gdd-analysis-skills-validation.md`

**Interfaces:**
- Consumes: GDD, MVP, and the user's request to analyze the player cycle.
- Produces: a macro-level player-cycle analysis with observable player goals/actions/feedback, source references, MVP scope, and only material dependencies or gaps.

- [x] **Step 1: Run the player-cycle baseline scenario in a fresh subagent without this skill.** Use the current GDD/MVP and ask for only the player-cycle macro activities. Record the actual response and whether it mixes NPC/world outcomes into player activities, breaks macros into details, or invents missing rules.
- [x] **Step 2: Write the minimum skill that corrects observed baseline failures.** Frontmatter must use `name: pega-player-cycle-analysis`; description starts with `Use when...`, uses third person, and names GDD player journey/activity analysis as its trigger.
- [x] **Step 3: Repeat the same scenario with the skill instructions supplied.** Pass when player goals/actions/feedback remain the output's focus; world behavior is referenced only as a dependency or observable feedback; macro activities are sourced and MVP-scoped; missing rules stay open.
- [x] **Step 4: Record baseline, post-skill result, and any corrections in the validation report.** Do not proceed until this skill passes.
- [x] **Step 5: Review the exact new skill diff and complete its deployment.** If needed, request sandbox-reviewed local Git write access and commit only `.agents/skills/pega-player-cycle-analysis/SKILL.md`. If denied, stop before Task 2 and report the deployment block.

### Task 2: Gameplay-Cycle Analysis Skill

**Files:**
- Create: `.agents/skills/pega-gameplay-cycle-analysis/SKILL.md`
- Update: `docs/superpowers/reports/2026-10-15-pega-gdd-analysis-skills-validation.md`

**Interfaces:**
- Consumes: GDD, MVP, and the user's request to analyze gameplay/world outcomes.
- Produces: macro-level world/gameplay outcomes with observable rules, actors/states, source references, MVP scope, and material gaps.

- [x] **Step 1: Run the gameplay-cycle baseline scenario in a fresh subagent without this skill.** Ask for macro world outcomes from the GDD/MVP, including temporal transitions, NPCs, objects/items, and resolution. Record omissions, technical prescriptions, unsupported details, and rationalizations verbatim.
- [x] **Step 2: Write the minimum skill that addresses the observed baseline failures.** Frontmatter uses `name: pega-gameplay-cycle-analysis`; description starts `Use when...` and triggers on GDD gameplay/world simulation analysis.
- [x] **Step 3: Repeat the same scenario with the skill instructions supplied.** Pass when it captures the macro gameplay cycle, separates source-stated behavior from open content, marks MVP scope, and avoids turning gameplay terms into implementation instructions.
- [x] **Step 4: Record baseline, post-skill result, and any corrections.** Do not proceed until this skill passes.
- [x] **Step 5: Review the exact new skill diff and complete its deployment.** If needed, request sandbox-reviewed local Git write access and commit only `.agents/skills/pega-gameplay-cycle-analysis/SKILL.md`. If denied, stop before Task 3 and report the deployment block.

### Task 3: GDD Activity Planning and Gap Synthesis Skill

**Files:**
- Create: `.agents/skills/pega-gdd-activity-planning/SKILL.md`
- Update: `docs/superpowers/reports/2026-10-15-pega-gdd-analysis-skills-validation.md`

**Interfaces:**
- Consumes: player-cycle and gameplay-cycle analyses plus GDD/MVP/ADR evidence.
- Produces: macro activity candidates, links/dependencies across perspectives, coverage check, and gaps grouped by whether they block defining a macro activity or can wait as content/configuration detail.

- [x] **Step 1: Run the synthesis baseline scenario in a fresh subagent without this skill.** Supply two short perspective analyses with dependent but separately acceptable outcomes, an MVP-excluded item, and an unresolved behavior. Ask for macro activities and gaps. Record merging, fragmentation, invented decisions, and omitted gaps.
- [x] **Step 2: Write the minimum synthesis skill addressing those failures.** Frontmatter uses `name: pega-gdd-activity-planning`; description starts `Use when...` and triggers on combining player/gameplay analysis, identifying GDD coverage gaps, or deriving PEGA macro activities.
- [x] **Step 3: Repeat the same scenario with the skill instructions supplied.** Pass when it proposes one objective per macro activity, links causal dependencies without fusing independent outcomes, marks MVP scope, distinguishes blocking product decisions from later details, and leaves normative sources unchanged.
- [x] **Step 4: Record baseline, post-skill result, and any corrections.** Do not proceed until this skill passes.
- [x] **Step 5: Review the exact new skill diff and complete its deployment.** If needed, request sandbox-reviewed local Git write access and commit only `.agents/skills/pega-gdd-activity-planning/SKILL.md`. If denied, stop before Task 4 and report the deployment block.

### Task 4: Route GDD Analysis Through the Three Skills

**Files:**
- Modify: `.agents/skills/pega-production-governance/SKILL.md`
- Update: `docs/superpowers/reports/2026-10-15-pega-gdd-analysis-skills-validation.md`

**Interfaces:**
- Consumes: all three verified project-local skills.
- Produces: a concise routing rule for the GDD activity-analysis flow while preserving Audit, Prepare Task, Reconcile, and Review Delivery behavior.

- [x] **Step 1: Run a governance-routing baseline scenario in a fresh subagent without changes to the current governance skill.** Ask for an integrated macro GDD analysis and record whether the current single flow separates both cycles, finds gameplay gaps, and avoids implementation invention.
- [x] **Step 2: Replace the monolithic GDD activity-analysis procedure with routing and shared invariants.** Keep authority, source evidence, read-only limits, and all other four governance flows intact.
- [x] **Step 3: Repeat the same scenario with the updated governance skill and all three specialist instructions supplied.** Pass when the response routes through player cycle, gameplay cycle, and synthesis; returns macro activities and categorized gaps; and leaves unrelated governance flows and sources untouched.
- [x] **Step 4: Review the complete governance skill diff, including the previously authorized uncommitted changes; preserve their version-policy and player-planning rules.** Record baseline and post-skill evidence. Do not include the modified `AGENTS.md` in this commit.
- [x] **Step 5: Complete deployment for `.agents/skills/pega-production-governance/SKILL.md`.** If needed, request sandbox-reviewed local Git write access and commit only that skill file; do not stage or commit `AGENTS.md` in this task. If denied, stop before Task 5 and report the deployment block.

### Task 5: Rebuild the GDD Activity Analysis at Macro Level and Treat Its Gaps

**Files:**
- Modify: `docs/pega-gdd-activity-analysis.md`
- Update: `docs/superpowers/reports/2026-10-15-pega-gdd-analysis-skills-validation.md`

**Interfaces:**
- Consumes: the three verified skills; current GDD/MVP/ADR; current analysis as evidence of prior activity/gap coverage.
- Produces: one integrated analysis with distinct player and gameplay macro cycles, clear cross-links, and gaps grouped into decisions needed before defining objectives versus detail that can wait.

- [x] **Step 1: Run the new analysis flow over GDD/MVP/ADR.** Use the two specialist skills followed by the synthesis skill. Keep the output at macro activity level; use the prior ACT/GP detail only to validate coverage and discover gaps.
- [x] **Step 2: Update the analysis document.** Consolidate over-fragmented rows into macro activities; preserve source evidence and the player/gameplay relationship; state explicitly when a gap is a product decision, a missing macro objective, or a later content/configuration detail.
- [x] **Step 3: Review the result against every GDD main-loop outcome and the MVP cut.** Pass when both perspectives are covered, every macro activity has an observable result/source/scope, relationships are explicit, and unresolved rules remain questions rather than assumed answers.
- [x] **Step 4: Present any product decisions needed to resolve blocking gaps as evidence-backed options for the user.** Do not edit GDD, MVP, TASKS, ADR, or Trello while producing the analysis.
- [x] **Step 5: Review the final document/report diff.** Commit the analysis, spec, plan, validation report, and the existing authorized `AGENTS.md` change only if local Git write access is available and the complete diff still matches the approved ecosystem direction; do not push.

## Completion Checks

- [x] Each skill has a recorded no-skill baseline and a passing repeat using its instructions before the next skill is authored.
- [x] Three new project-local skills exist; governance routes GDD activity analysis to all three and retains the other flows.
- [x] The analysis document presents macro activities by player and gameplay perspective, connected by explicit relationships.
- [x] Blocking product gaps are separated from details that do not prevent identifying macro activities; no source decision or Trello state is invented or changed.
- [x] All changes are visible in the existing PEGA checkout; no unattached branch or worktree contains the deliverable.

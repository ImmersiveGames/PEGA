# PEGA Workflow Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the PEGA project readable by the CGS `$help` pipeline at Production while recording local acceptance evidence independently from Trello card completion.

**Architecture:** Add project-local configuration and workflow metadata that `$help` can read from the PEGA root. Create a small local Production work register with story records linked to existing cards and PEGA source documents; keep Trello's operational fields separate and report disagreements without synchronizing them automatically.

**Tech Stack:** YAML and Markdown; reuse the CGS workflow catalog, config guidance, templates and read-only helpers as project support files. Do not copy skills or add dependencies.

**Spec:** `docs/superpowers/specs/2026-11-24-pega-workflow-bootstrap-design.md`

## Global Constraints

- Resolve PEGA as `project.stage: Production`, `modes.rigor: standard`, Unity `6000.6.10f1`, C#.
- Keep GDD, MVP, ADR and TASKS as their current authorities; Trello remains the operational source for its card fields.
- A local story is accepted only when its criteria have local evidence; Trello status and code presence alone are not acceptance evidence.
- Do not edit GDD, MVP, ADR, TASKS, Trello, GitHub or GanttFlow.
- Do not edit or copy CGS skills, alter Codex settings, install software, or modify the machine `PATH`.
- Do not run Unity, builds, Play Mode, smoke checks or tests.
- Git Bash is installed but its MSYS runtime is blocked by the current sandbox (`NtCreateDirectoryObject ... 0xC0000022`). Use the documented factual fallback unless a specific elevated helper invocation is approved.

## Review Focus

- **Stage/config drift:** `project.yaml` and `production/stage.txt` must both resolve to `Production`; verify by reading the exact values and the catalog's resolution rules.
- **False workflow gaps:** catalog entries for PEGA's existing GDD/MVP/ADR/TASKS must point to their real locations or clearly state that the non-current phase is not mapped; inspect every Production glob against the PEGA tree.
- **Status conflation:** each local story must distinguish pipeline status, last observed Trello status/date, and acceptance evidence; compare the two sources without auto-syncing them.
- **Unverified delivery:** keep #92/#93 stories incomplete wherever manual acceptance evidence is absent; label code commits and Trello comments by their evidence type.
- **Helper failure:** record that Bash execution is sandbox-blocked and use direct config/catalog inspection; do not report a missing file or an empty project merely because a helper failed.

---

### Task 1: Add PEGA project settings and the `$help` support contract

**Files:**
- Create: `project.yaml`
- Create: `production/stage.txt`
- Create: `.agents/docs/config-resolution.md`
- Create: `.agents/docs/workflow-modes.md`
- Create: `.agents/docs/workflow-catalog.yaml`
- Create: `.agents/docs/templates/session-state.md`
- Create: `.agents/docs/templates/sprint-status.yaml`
- Create: `.agents/docs/templates/story.md`
- Create: `.agents/hooks/yaml-helper.sh`
- Create: `.agents/scripts/artifact-check.sh`
- Create: `.agents/docs/pega-workflow-adoption.md`

**Interfaces:**
- Consumes: The approved bootstrap design, CGS workflow support files, PEGA `AGENTS.md`, `PEGA GDD/TASKS.md`, `PEGA GDD/ADR-PREPRODUCAO.md`, and current Unity version from `ProjectSettings/ProjectVersion.txt`.
- Produces: A root configuration resolved by the help skill, a Production stage mirror, and a catalog whose Production checks describe PEGA's local work/evidence workflow.

- [x] Copy only workflow support documents/templates and the two read-only helpers from `D:\Projetos\Codex-Game-Studios`; do not copy `.agents/skills/`.
- [x] Add `project.yaml` with `schema_version: 1`, `project.stage: Production`, `modes.rigor: standard`, `engine.name: Unity`, `engine.version: 6000.6.10f1`, and `engine.language: C#`. Leave the four rigor-fronted knobs unset.
- [x] Write `Production` to `production/stage.txt`.
- [x] Keep the upstream phase ladder, but adapt the current Production catalog entries to PEGA's local work register and story/evidence paths. Do not require a timeboxed sprint or invent capacity/dates.
- [x] Add a short provenance/authority note in `.agents/docs/pega-workflow-adoption.md`: where catalog/support files came from, which phase is currently mapped, how PEGA paths map to the workflow, and how to report Trello/local differences.
- [x] Verify statically that config keys, stage mirror, catalog phase IDs, source paths and helper paths are present. If the Bash helpers cannot execute in the sandbox, record them as unavailable and retain the factual fallback.

### Task 2: Create local Production work records and evidence checkpoints

**Files:**
- Create: `production/session-state/active.md`
- Create: `production/workflow-adoption.md`
- Create: `production/sprint-status.yaml`
- Create: `production/epics/vertical-slice/story-001-possession-transfer.md`
- Create: `production/epics/vertical-slice/story-002-confrontation-attack.md`

**Interfaces:**
- Consumes: Task 1's status/story templates, current read-only Trello cards #92 and #93, and their linked GDD/MVP/ADR sections.
- Produces: Local status and acceptance records that `$help` can read under `Production`, with a traceable Trello reference but independent evidence fields.

- [x] Record #92 and #93 as locally in progress, preserving the board observation date and link on each story; do not mark any unverified criterion complete.
- [x] Derive local acceptance criteria from the applicable PEGA GDD/MVP/ADR and approved task scope; treat board descriptions as operational context, not new rules.
- [x] In #92, retain the reported V01 evidence as reported and leave unverified transfer/soltura criteria open.
- [x] In #93, record that attack-related code exists in commits `8bc0845` and `76a5ddb`, while manual gameplay acceptance remains unverified.
- [x] Include evidence path/format, Unity manual validation requirement, and an explicit `NOT VERIFIED` state where no evidence exists.
- [x] Initialize `production/session-state/active.md` without asserting which gameplay card is the sole active task; record the latest mission lifecycle commit and the unresolved link to DEM-05 as a reconciliation item.
- [x] Record the previously identified active art item as unmapped; its card ID/title/scope were not revalidated, so no story or owner was inferred.
- [x] Verify the YAML story paths match the files exactly and that all local statuses and evidence states agree with the story files.

### Task 3: Confirm the help pipeline's read-only fallback

**Files:**
- Read: `project.yaml`
- Read: `production/stage.txt`
- Read: `.agents/docs/config-resolution.md`
- Read: `.agents/docs/workflow-modes.md`
- Read: `.agents/docs/workflow-catalog.yaml`
- Read: `production/session-state/active.md`
- Read: `production/sprint-status.yaml`
- Read: `production/epics/vertical-slice/story-*.md`

**Interfaces:**
- Consumes: The PEGA-local bootstrap from Tasks 1–2.
- Produces: A final static verification record in the response, distinguishing resolved facts from the Bash execution limitation.

- [x] Manually resolve the expected configuration chain: explicit `Production`, explicit `standard` rigor, and `workflow: standard` by the documented rigor expansion.
- [x] Confirm the help skill can find every required local path and that each Production catalog check is meaningful for PEGA.
- [x] Confirm `sprint-status.yaml` reports only the two scoped stories and does not imply acceptance from Trello status.
- [x] Confirm the session checkpoint names the unmapped mission lifecycle work instead of inferring its card or changing task priority.
- [x] Do not run tests; report Unity/Play Mode acceptance as manual work still required.

## Review Focus Check

- The stage mirror and config are compared directly, so stage drift is covered.
- Production catalog paths are checked against existing PEGA and new local records, so false missing-file reports are covered.
- Local status, Trello snapshot and acceptance evidence are stored separately, so source conflation is covered.
- Stories preserve the actual evidence state, including reported-only or absent evidence, so acceptance is not inferred.
- Bash failure is handled by the documented fallback and named as a sandbox limitation, so helper failure is not mistaken for missing project data.

## Execution Handoff

This plan follows the approved hybrid approach. The actual Git Bash installation was found at `C:\Program Files\Git\bin\bash.exe`, but this sandbox blocks its MSYS startup; do not change machine `PATH` or install another shell. The manual fallback is sufficient for the bootstrap's current phase. If a future run needs the helpers, invoke them from a less-restricted Git Bash session or request approval for that specific command.

## Execution record

- Static file checks resolved `Production`/`standard`, found both story paths and required pipeline files, and confirmed the two helper copies match their CGS source hashes.
- No Unity, build, Play Mode, smoke command, or test was run. Helper execution remains unverified because MSYS startup is denied in the current sandbox.
- Unrelated working-tree changes under `Assets/PEGA`, `Packages`, and `ProjectSettings` were preserved.

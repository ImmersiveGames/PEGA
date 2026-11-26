# PEGA workflow bootstrap design

## Intent

Make the existing PEGA project readable by the CGS `$help` pipeline at its current Production stage. The skill should be able to resolve the project settings, read the workflow catalog and find a local checkpoint and production work status. Local story records must carry acceptance criteria and evidence so a Trello card move alone never marks PEGA work accepted.

## Goals

- Configure PEGA as a Unity project in `Production`, using `modes.rigor: standard`.
- Reuse the existing PEGA GDD, MVP, ADRs, TASKS and work reports by reference; do not recreate them in CGS default folders.
- Add the minimum project-local workflow metadata and production records needed for `$help` to read PEGA through its documented fallback.
- Keep Trello as the operational source for card state, owners, checklists and estimates. Keep local story status and acceptance evidence as a separate view of delivery. Report disagreements without automatically syncing either source.
- Seed only work whose current state can be sourced, and label unresolved mapping between recent commits and Trello explicitly.

## Non-goals and anti-goals

- Do not modify GDD, MVP, ADR, TASKS, Trello, GitHub or GanttFlow as part of this bootstrap.
- Do not implement gameplay, create Unity tests, or run Unity/build/Play Mode.
- Do not copy or edit the CGS skill itself, install skills, change Codex settings, or vendor the entire CGS tooling framework.
- Do not treat code presence, Trello completion, or a historical report as runtime acceptance evidence.
- Do not invent a sprint calendar, estimates, owners or a single active task where sources disagree.

## Constraints

- PEGA's current project instructions make GDD/MVP/ADR/TASKS authoritative for game rules, scope, decisions and decomposition; Trello remains operational for its board fields.
- The latest local commit (2026-11-23) adds mission lifecycle content assets. Trello still lists DEM-05 Routes/Activities as `A fazer`, while #92 and #93 are `Em andamento`; this is a reconciliation signal, not enough evidence to assign the lifecycle work to a card.
- The PEGA checkout has no `project.yaml`, workflow catalog, stage mirror, session checkpoint or `production/` records today.
- Git Bash exists at `C:\Program Files\Git\bin\bash.exe`, but the restricted host blocks its MSYS startup with `NtCreateDirectoryObject ... 0xC0000022`. Adding it to `PATH` alone will not remove that sandbox denial. Use the skill's documented read-only fallback here; a less-restricted shell or specifically approved elevated invocation would be needed to run the Bash helpers.
- The installed PEGA governance skills remain separate from the CGS pipeline. This bootstrap makes `$help` able to route and report; it does not install every downstream CGS command.

## Approaches considered

1. **Hybrid project-local bootstrap — selected.** Add only the metadata, catalog mapping, checkpoint and local work/evidence records needed for `$help` at Production. Keep Trello's operational role and link local records to cards. This is the smallest approach that adds independent acceptance evidence without replacing PEGA's existing authorities.
2. **Local-first tracker.** Make local stories and status files the authority for all progress and make Trello a mirror. This would require changing PEGA's current authority rules and reconciling the board; it is outside the requested minimum.
3. **Full CGS framework vendoring.** Copy its scripts, templates and skills into PEGA. This would create a second maintained copy of the workflow framework and conflicts with the instruction not to copy skills into PEGA.

## Chosen design

Create a thin PEGA adapter for the CGS `$help` contract:

- `project.yaml` records schema version 1, `project.stage: Production`, `modes.rigor: standard`, and verified engine facts: Unity `6000.6.10f1`, C#.
- `production/stage.txt` mirrors `Production` for the documented legacy fallback.
- `.agents/docs/config-resolution.md`, `.agents/docs/workflow-modes.md` and `.agents/docs/workflow-catalog.yaml` provide the local facts the help skill needs. The catalog retains the phase ladder and maps PEGA's existing document paths and Production flow instead of pretending the default `design/` and `src/` tree exists.
- `production/session-state/active.md` records a bounded checkpoint. It must say when no current task can be established and name the unresolved lifecycle-to-Trello mapping instead of choosing one by inference.
- `production/sprint-status.yaml` and concise local story records represent the currently confirmed PEGA work. Each record links to its source card and normative PEGA sections, tracks acceptance separately from Trello state, and remains incomplete until local evidence is recorded.
- A short `production/workflow-adoption.md` explains how to update the local checkpoint, story evidence and status snapshot, and how to report disagreement with Trello.

The initial local records cover the confirmed in-progress PEGA work (#92 and #93). The active art item and the 2026-11-23 mission lifecycle commit are recorded as unmapped/needs reconciliation until their local scope and source card are established; they are not silently turned into stories or assigned to DEM-05.

Because Bash is unavailable here, the first usable `$help` run uses its documented factual fallback. Adding Bash helpers or changing the external skill is a separate decision if the fallback proves insufficient.

## Acceptance for this bootstrap

- The help skill can resolve `Production` and `standard` from PEGA-local files using its documented fallback.
- It can read the workflow catalog and the bounded checkpoint from the PEGA root.
- It can identify locally tracked stories and their next acceptance/evidence action without treating a Trello card as proof of delivery.
- Existing PEGA normative documents remain in place and are referenced at their current paths.
- Unmapped local commits, missing manual Unity evidence and differences between Trello and local story state are reported as unresolved, not inferred away.

## Implementation decisions

- The Production catalog requires a local register instead of a timeboxed sprint and routes implementation/delivery review through the installed PEGA Production Governance skill. Earlier phases retain upstream catalog paths and are explicitly marked as unmapped.
- The local stories cover #92 possession and the first #93 attack increment. Trello status/checklists are snapshots; local acceptance remains open until evidence is recorded against the GDD/MVP/ADR/TASKS scope.
- Manual Unity acceptance remains outstanding; this bootstrap does not claim it.

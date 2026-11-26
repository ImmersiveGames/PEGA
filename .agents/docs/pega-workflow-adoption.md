# PEGA adoption of the CGS `$help` workflow

## Provenance and scope

The shared support contract was copied from the local `D:/Projetos/Codex-Game-Studios/.agents/` checkout on 2026-11-24:

- `docs/config-resolution.md`
- `docs/workflow-modes.md`
- `docs/workflow-catalog.yaml`
- `docs/templates/session-state.md`, `sprint-status.yaml`, and `story.md`
- `hooks/yaml-helper.sh` and `scripts/artifact-check.sh`

No skills were copied. The Production section of `workflow-catalog.yaml` is the PEGA adapter: it tracks a local register and evidence-aware stories, and routes implementation/review through the installed `pega-production-governance` skill. Earlier catalog phases retain upstream paths as reference and are explicitly **not mapped to PEGA** in this bootstrap.

## PEGA source mapping

- Gameplay rules: `PEGA GDD/GDD.md`
- MVP scope and acceptance: `PEGA GDD/MVP.md`
- Production/architecture decisions: `PEGA GDD/ADR-PREPRODUCAO.md` and the focused ADRs under `PEGA GDD/ADRs/`
- Derived task breakdown: `PEGA GDD/TASKS.md`
- Local production status and stories: `production/sprint-status.yaml` and `production/epics/vertical-slice/`
- Session checkpoint: `production/session-state/active.md`

The configuration sets `project.stage: Production`, `modes.rigor: standard`, Unity `6000.6.10f1`, and C#. The stage mirror is `production/stage.txt`. At standard rigor, `workflow` resolves to `standard` through the copied rigor expansion; the four rigor-fronted knobs remain unset.

## Trello and local evidence

Trello remains the operational source for card list/status, members, checklist state, estimates, and dates. The PEGA register stores local pipeline status and acceptance evidence separately. Story records snapshot their Trello link/state/date but never infer local completion from a card or checklist move. Report differences as reconciliation items and do not auto-sync either direction.

`production/sprint-status.yaml` is intentionally a non-timeboxed work register (`sprint: null`). It currently lists only #92 and #93 as locally `in-progress`. Acceptance evidence states are defined in `production/workflow-adoption.md`; reported Trello comments and static code changes are not runtime proof.

## Running `$help`

The installed Git for Windows Bash is at `C:\Program Files\Git\bin\bash.exe`. In a working Git Bash session at the PEGA root, the skill's explicit bootstrap commands are:

```bash
bash ".agents/hooks/yaml-helper.sh" resolve_config --keys project.stage,workflow
bash .agents/scripts/artifact-check.sh --phase production
```

In this Codex session, PowerShell can see Git Bash but the restricted Windows sandbox denies MSYS startup (`NtCreateDirectoryObject ... 0xC0000022`). Adding Bash to `PATH` alone will not fix that sandbox denial. Use `$help`'s documented factual fallback until the active Codex Windows sandbox/permission profile allows child-process startup; do not disable the sandbox or change machine-wide settings as part of PEGA workflow work. No helper execution is claimed by this bootstrap.

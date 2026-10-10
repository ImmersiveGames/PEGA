# PEGA Production Governance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a project-local Codex governance skill for PEGA that audits and prepares production work using existing documents, Git, Trello and the Immersive ecosystem without changing global configuration or external records.

**Architecture:** A concise root `AGENTS.md` routes PEGA governance and package-consuming code work to one project-scoped skill. The skill implements Audit, Prepare Task, Reconcile and Review Delivery using local documents, actual diffs, resolved packages and existing read-only connector calls. A synthetic conflict fixture and a validation report make the five required scenarios reviewable.

**Tech Stack:** Markdown, Codex `AGENTS.md` and project-scoped skills, PowerShell/static repository inspection, existing GitHub and Trello connectors. No new service, package, dependency, MCP server, database or executable client.

**Spec:** `docs/superpowers/specs/2026-10-10-pega-production-governance-design.md`

## Global Constraints

- Keep the implementation local to the PEGA repository; do not edit global Codex settings, globally installed skills, plugin installations or connector settings.
- Use `.agents/skills/pega-production-governance/SKILL.md` for project-local skill discovery; do not add a loader, registry, dispatcher or duplicate global skill.
- For PEGA code work involving Immersive packages, require the existing `immersive-ecosystem` skill before package API-specific guidance or implementation.
- Resolve package source/version from the PEGA manifest, lock, installed `package.json` and package cache before API guidance; do not use the Framework checkout or another release as a substitute.
- Skills must derive evidence-backed minimum versions per required contract and maturity; never pin an observed/current or “latest” version as a workflow requirement. If the floor is unproven, report it as unknown and withhold version-specific guidance.
- Do not modify Framework package source, PEGA gameplay, GDD, MVP, TASKS, ADR, project package manifest/lock, Trello, GitHub or GanttFlow.
- All Phase 1 workflows are read-only; never invoke external write tools or mark cards complete.
- Do not run Unity build/import/tests, Play Mode, smokes or batchmode. Report Editor validation as pending/manual where needed.
- Do not claim real external integration from simulated tests. Label a simulated unavailable-connector probe as such.

## Review Focus

- Unknown contract introduction history or maturity: the skill reports an unknown minimum and withholds API-specific guidance instead of guessing. Test in Task 1 and Scenario 1.
- Task text conflicts with the GDD: the audit cites both sources and proposes resolution without writing either. Test in Task 1 and Scenario 2.
- Missing or stale Trello context: the workflow distinguishes unavailable connector, missing card and locally verifiable facts. Test in Task 2 and Scenario 5.
- Accidental local/external writes: Audit and Reconcile use read-only tools, with before/after local and card evidence. Test in Task 2 and Scenario 4.
- Unverified implementation or runtime claims: code/API presence is separated from Editor/runtime validation. Test in Task 2 and Scenario 1.

---

### Task 1: Define the local Codex surface and governance rules

**Files:**
- Create: `AGENTS.md`
- Create: `.agents/skills/pega-production-governance/SKILL.md`
- Create: `docs/superpowers/fixtures/pega-governance/contradictory-task.md`

**Interfaces:**
- Consumes: The source hierarchy and four workflows defined by the spec; the installed `immersive-ecosystem` and Framework package skills.
- Produces: Project instruction rules that route to skill `pega-production-governance`; one skill with the exact workflow names `Audit`, `Prepare Task`, `Reconcile`, `Review Delivery`; one clearly synthetic contradiction input.

- [x] **Step 1: Create the controlled conflict fixture**

  Add a short synthetic task excerpt proposing that a valid attack against an item-carrying actor preserves the carried items. Label the entire fixture `SYNTHETIC TEST FIXTURE — NOT A PEGA DECISION`. The contradiction target is the GDD decision under “Posse, desejo e inversão da perseguição”: every valid attack against an actor carrying items drops all of them.

- [x] **Step 2: Add root project instructions**

  Create `AGENTS.md` with concise rules for GDD/MVP/TASKS/ADR/code/Trello authority, read-only Reconcile, no external writes, evidence requirements and Unity validation restrictions. Route production-governance requests to `.agents/skills/pega-production-governance/SKILL.md`. Require `immersive-ecosystem` before PEGA code guidance that uses Immersive package APIs; if missing, stop API-specific guidance. State that Framework source changes require a separate Framework task and its package/core skills.

- [x] **Step 3: Add the four-workflow project skill**

  Create `.agents/skills/pega-production-governance/SKILL.md` with valid `name`/`description` frontmatter and the four exact routes. Define the source hierarchy, stable-link traceability, evidence labels, required Trello reads, fallback behavior, authority boundaries, and no-write policy. Audit must detect requirement conflicts, missing/unsupported tasks, stale references, acceptance conflicts, duplicates, circular dependencies, implementation/documentation divergence and progress claims without evidence. Reconcile must list affected files/cards, supporting decisions and affected dependencies. Review Delivery must inspect the diff, acceptance, allowed validation, regressions and documentation needs. For PEGA code tasks, require `immersive-ecosystem`; use its package resolution and validation process. Require the skill to report both the resolved version and an evidence-backed minimum per contract/maturity without pinning versions in skill rules. Unknown minimum means no version-specific API guidance, no dependency update and no guess. Keep the skill instruction-only; invoke existing connector tools through the host.

- [x] **Step 4: Check local structure and version policy**

  Run: `rg -n '^(name|description):|Audit|Prepare Task|Reconcile|Review Delivery|immersive-ecosystem|minimum version|read-only' AGENTS.md .agents/skills/pega-production-governance/SKILL.md`
  Expected: both files contain the intended discoverable skill metadata, all four routes, framework-skill handoff, version-floor policy and read-only wording.

  Run: `rg -n 'preview\.[0-9]+|\b[0-9]+\.[0-9]+\.[0-9]+\b' .agents/skills/pega-production-governance/SKILL.md`
  Expected: no fixed package version literal is used as a skill condition. Current audit snapshots may remain in the design/report, never in the skill policy.

  Run: `rg -n -i 'minimum version|resolved version|unknown|latest' .agents/skills/pega-production-governance/SKILL.md`
  Expected: the skill explicitly describes runtime resolution, evidence-backed minimums, unknown-floor behavior and rejection of “latest” as a minimum policy.

  Manually inspect the conflict fixture's synthetic label and confirm it is outside `PEGA GDD/`.

### Task 2: Exercise the governance workflows against live PEGA sources

**Files:**
- Create: `docs/superpowers/reports/2026-10-10-pega-production-governance-validation.md`
- Read: `PEGA GDD/GDD.md`, `PEGA GDD/MVP.md`, `PEGA GDD/TASKS.md`, `PEGA GDD/ADR-PREPRODUCAO.md`, resolved package docs/source and relevant PEGA code
- Read: Trello card `https://trello.com/c/EfPEV4Ip` and its checklists

**Interfaces:**
- Consumes: Task 1 project skill and fixture.
- Produces: A concise scenario report with outcome `PASS`, `FAIL` or `NOT VERIFIED`, evidence links/paths, tool/validation limitations and no duplicated source material.

- [ ] **Step 1: Confirm project skill discovery** — Not verified: project skill registry reload/discovery was unavailable in this task.

  Reopen/reload the PEGA project in Codex if needed, then confirm the project skill appears through the normal skill surface or is explicitly selectable by name. Invoke the skill in `Prepare Task` mode. If the host does not discover it, record the exact limitation; do not change global settings or install it globally.

- [x] **Step 2: Run Scenario 1 — Prepare MC-01** — Manually exercised with live Trello and the installed package; minimum version remains unknown.

  Invoke `Prepare Task` with `https://trello.com/c/EfPEV4Ip`. Require the report to include card/list state, incomplete checklist items, deliverable and task reference, resolved package source/version, relevant API/maturity evidence, minimum required version evidence or `unknown`, blocking dependencies and manual Editor acceptance steps. Apply `immersive-ecosystem` before package-specific guidance. Verify the response does not claim runtime success or treat the observed resolved version as the skill's minimum.

- [x] **Step 3: Run Scenario 2 — Controlled conflict** — Static fixture/GDD comparison; no source edits.

  Invoke `Audit` on the synthetic fixture alongside the cited GDD section. Require both claims, their locations, impact and correction owner/action in the result. Confirm the GDD and fixture bytes remain unchanged by the audit.

- [x] **Step 4: Run Scenario 3 — Traceability** — Partial; fresh code-to-acceptance runtime evidence is missing.

  Trace a documented decision or requirement through its deliverable, Trello card URL, relevant code path and available validation evidence. Use existing IDs and URLs; mark missing links and prototype-only evidence. Do not create metadata or claim unsupported runtime proof.

- [x] **Step 5: Run Scenario 4 — Read-only security** — No external writes; no formal pre-run hash set.

  Capture a Git status and hashes/snapshots of the audited production documents before `Audit` and `Reconcile`. Run both modes. Reconcile must report the actual ADR-versus-Trello capacity/date discrepancy as a proposed decision/clarification, identify its sources, and leave schedule/card/document changes to the owner. Recheck those documents and the Trello card through read calls. Confirm no source changed, no external record changed, and no write connector tool was invoked. Report the governance implementation files separately from production-source snapshots.

- [ ] **Step 6: Run Scenario 5 — External read unavailable** — Not verified: no isolated failure context; global connectors were left intact.

  Do not disable or alter global connectors. Exercise the skill's fallback with a controlled unavailable-read result or card-read failure; the report must continue local checks and clearly label this as a simulated/request-level failure probe. Do not claim this proves service-wide Trello outage behavior. If no safe failure context is available, report `NOT VERIFIED` and the remaining limitation.

- [x] **Step 7: Record evidence and remaining validation**

  Create `docs/superpowers/reports/2026-10-10-pega-production-governance-validation.md` listing each scenario, evidence, tool calls at read/write level, status and limitations. Record that Unity execution is `NO` and list manual Editor checks for MC-01. State whether `/skills` discovery required reload. Do not write to Trello or GitHub.

- [x] **Step 8: Exercise Review Delivery against existing code evidence** — Partial; prototype evidence only, no card update.

  Invoke `Review Delivery` with the MC-01 card and the existing Player placeholder prototype under `Assets/_Project/Prototyping/PlayerPlaceholder/` as review evidence. Inspect the current source/diff and card without claiming this prototype is a completed delivery. Require a report that separates prototype evidence from the card's incomplete acceptance/checklists and unrun Editor/runtime validation. Do not update the card. Record this workflow exercise and its limits in the validation report.

### Task 3: Review the integration diff against its design

**Files:**
- Read: `docs/superpowers/specs/2026-10-10-pega-production-governance-design.md`
- Read: Task 1 project files, Task 2 fixture and validation report
- Read: Local Git diff and status

**Interfaces:**
- Consumes: All Task 1 and Task 2 artifacts.
- Produces: A read-only delivery review against the design and acceptance scenarios; a short repair list if required.

- [x] **Step 1: Review boundaries and authority**

  Confirm the project skill uses one project-local discovery mechanism, references existing global Framework skills without copying them, uses dynamic minimum-version evidence, and leaves gameplay, package sources, production docs and global settings untouched.

- [x] **Step 2: Review all five acceptance results** — Partial and NOT VERIFIED results remain explicitly labeled.

  Confirm each scenario has evidence or is explicitly `NOT VERIFIED`, simulated service failures are not reported as real integration proof, and no Unity validation is described as executed.

- [x] **Step 3: Check file hygiene and final scope**

  Inspect trailing whitespace, placeholders, `git status --short` and the full diff. Expected task artifacts are `AGENTS.md`, the project skill, the synthetic fixture and validation report; the approved design and implementation plan are also updated in this session. Do not commit or modify Trello/GitHub.

## Manual validation checklist

- Reload the PEGA project in Codex and confirm project skill discovery without changing any global skill/configuration.
- Confirm Audit and Reconcile invoke only read tools and leave production documents and Trello unchanged.
- Confirm Prepare Task resolves the installed package and produces evidence-backed minimums, or explicitly reports an unknown floor.
- In Unity Editor, manually verify the MC-01 acceptance criteria according to the card and Framework's installed authoring/validation guidance. Codex must not run Unity.

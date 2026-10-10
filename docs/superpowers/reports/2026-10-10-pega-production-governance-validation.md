# PEGA Production Governance — Validation Record

**Date:** 2026-10-10
**Scope:** Project-local Phase 1 workflows; read-only against production sources and connected systems.

## Evidence and results

### Scenario 1 — Prepare MC-01: PARTIAL / minimum version unknown

- Trello read: [`MC-01 Integrar Player/Actor do Framework na missão`](https://trello.com/c/EfPEV4Ip), board `PEGA Larapio`, list `A fazer`, open/incomplete, no members, no checklist attached, 0 h performed / initial 5 h remaining, due 2026-10-14. The card links to `PEGA GDD/TASKS.md#dependencias-tecnicas-e-autoria-no-immersive-framework`.
- Consumer reality: Unity `6000.5.0f1`; `Packages/manifest.json` declares the Framework from OpenUPM; lock reports registry resolution `1.1.0-preview.6`; installed `Library/PackageCache/com.immersive.framework@ed40a507d908/package.json` confirms package identity/version and repository revision `fca1dd130bbd9a953526c6b7b21fedead4ab1e07`.
- Applied `immersive-ecosystem` sequence: read project version, manifest/lock, effective installed package metadata, installed documentation index, guides, API maturity and PEGA prototype. The installed consumer package is the authority; the separate Framework checkout is not used as API authority.
- Installed evidence: `Documentation~/README.md`; `Documentation~/API/Public-API.md` (Player, Camera, Input/Pause); `Documentation~/Guides/Player-Usage.md`; `Documentation~/Guides/Camera-Usage.md`; `CHANGELOG.md`. Scene-Provided authoring, Local Player Host and Player Slot authoring are Stable in their stated scope. `ActorProfile`, Session setup/admission, Camera Assignment, Output Definition and Actor Camera Subject surfaces are Experimental. Input Gate/Pause guide documents the single-player gate flow; camera authoring and its dependencies have separately scoped maturity.
- **Evidence-backed minimum release for the complete card contract: unknown.** Installed docs describe present contracts/maturity and changelog records some later changes, but this record does not establish the first released version containing every required Player, Activity, movement/input-gate, camera and restart contract at the required maturity. Do not infer a floor from the current resolution or set one from `latest`; withhold any version-specific portability claim until release history proves it.
- Local evidence: [`Assets/_Project/Prototyping/PlayerPlaceholder/README.md`](/C:/Projetos/PEGA/Assets/_Project/Prototyping/PlayerPlaceholder/README.md) and `PlayerMovementPlaceholder.cs` describe a prototype with movement and Scene-Provided composition. README reports a user-observed first Play Mode, but that observation is not fresh Editor evidence; second-run, cleanup/re-admission, restart/no-duplicate checks remain pending.
- Remaining manual acceptance: Unity import/compile; enter mission Activity and confirm Player admission/readiness; move and collide; confirm input is blocked/released by the configured Input Gate; verify camera subject/output; exit and re-enter Play Mode or restart through the supported path; confirm one Actor, correct input and camera with no stale bindings/duplicates. No Unity operation was run.

### Scenario 2 — Controlled conflict: PASS (static audit exercise)

- Synthetic source: [`contradictory-task.md`](/C:/Projetos/PEGA/docs/superpowers/fixtures/pega-governance/contradictory-task.md), explicitly labeled `SYNTHETIC TEST FIXTURE — NOT A PEGA DECISION`, proposes retaining carried items after a valid attack.
- Normative source: `PEGA GDD/GDD.md:694`, section “Posse, desejo e inversão da perseguição” (decision: a valid attack against an item-carrying actor drops all carried items).
- Finding: direct gameplay-rule conflict; GDD owns resolution. Proposed action is to reject or rewrite the synthetic task to match the GDD, or obtain an explicit design decision before changing the GDD. No source was edited by this exercise.

### Scenario 3 — Traceability: PARTIAL

- `PEGA GDD/ADR-PREPRODUCAO.md`, MC-01 scope and Framework boundary/evidence, identifies a controlável Player and says ActorProfile does not implement PEGA inventory or gameplay.
- `PEGA GDD/MVP.md` “Player e Actor” and `PEGA GDD/TASKS.md` Framework dependency section refine the requirement and validation expectations.
- The linked Trello card `https://trello.com/c/EfPEV4Ip` identifies MC-01 and acceptance for Activity entry, movement, Input Gate and restart without duplicate Actor.
- Existing code evidence is the prototype under `Assets/_Project/Prototyping/PlayerPlaceholder/`; it demonstrates authored movement/input-reader composition, not delivered gameplay or runtime acceptance. Its README records static composition and an earlier user-reported Play Mode, but no fresh log/certification was inspected here. Therefore the path from code to verified acceptance remains incomplete.

### Scenario 4 — Read-only security: PARTIAL

- Only read operations were made against Trello. No Trello/GitHub write tool was invoked; no connector settings, global skills, package manifest/lock, gameplay code, GDD/MVP/TASKS/ADR or card were intentionally changed.
- A formal before/after source hash set was not captured before authoring the governance files, so this run cannot claim a complete hash-based before/after proof. The current repository status/diff must be used to verify the implementation scope. The governance files and existing design/plan are expected local changes and are not production-source edits.
- No credentials were requested, copied, printed or stored.
- Reconcile finding: `PEGA GDD/ADR-PREPRODUCAO.md` §6 (lines 144–174) says no defensible calendar exists without confirmed weekly capacity and directs converting effort to duration only with sufficient capacity information. Trello's `[PLANO] Microciclo 01 — Linha de base de cronograma` (`https://trello.com/c/hO3uDL1F`) instead defines 20 programming h/week, 10 art/LD h/week and a 2026-10-13—2026-12-04 planning window for 176.5 h; MC-01 separately has 5 h scheduled 2026-10-13—2026-10-14 at 4 h/day. This is a decision/current-state divergence. Proposed owner action: production owner confirms whether Trello represents a newer capacity decision; if so, record that decision in the ADR while preserving estimate history, otherwise revise the derived Trello plan. No source/card was changed.

### Scenario 5 — Trello unavailable: NOT VERIFIED

Trello was available and returned MC-01. No safe isolated connector-failure context exists in this task, and global connector settings were not changed. The fallback behavior is specified in the project skill, but service-unavailable behavior was not exercised and no simulated failure is represented as a real outage.

## Additional workflow exercise — Review Delivery: PARTIAL / delivery incomplete

Compared the open MC-01 card with the existing Player placeholder files as prototype evidence. The prototype includes a CharacterController movement script and documentation for its Scene-Provided Host/Input/Gate/Slot/Actor Profile/Camera composition. The card remains in `A fazer`, has no checklist attached, reports zero hours, and the acceptance is not evidenced by a fresh Editor/runtime run. Movement is statically represented; Activity entry, gate behavior, camera behavior and restart/no-duplicate acceptance are not freshly verified. This prototype is not presented as completed delivery, and the card was not changed.

## Discovery and verification limits

- The project-local skill file has moved to `.agents/skills/pega-production-governance/SKILL.md`. The current task host did not expose a reliable command to reload Codex's project skill registry or prove `/skills` discovery in that earlier validation turn. Discovery there was **NOT VERIFIED**; no global setting or skill was changed.
- Static checks only; no tests were authored or run, no Unity build/import/test/Play Mode/smoke/batchmode was run, and no external write was made.
- This report records a controlled manual exercise of the skill procedures against live local/package/Trello evidence. It is not an independent invocation by a newly indexed project skill.

## Manual checks remaining

1. Reload/reopen PEGA in Codex and confirm the project skill is discoverable by name without changing global settings.
2. In Unity Editor, perform MC-01's acceptance and repeat/restart lifecycle checks listed above; capture logs/evidence.
3. If an authorized isolated connector-failure test context becomes available, exercise Scenario 5 without disabling the user's global integration.
4. Capture production-source file hashes and Trello read snapshots before and after any future Audit/Reconcile run when a formal no-write proof is required.

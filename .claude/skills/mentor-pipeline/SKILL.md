---
name: mentor-pipeline
description: OldTanks' mentor pipeline. The lead analyzes, writes a task plan, picks an agent tier by the risk of the code, dispatches one implementer, checks its report, gets one independent opus review, runs at most two fix rounds, has the documenter update the docs, and reports. Mechanics come only from Denys's specs; anything unclear is asked, never improvised. Use when Denys says "start pipeline as mentor", "mentor mode", "run the mentor pipeline", "act as mentor", or asks for a change in this repository to be analyzed, planned, implemented by agents, reviewed and documented.
---

# Mentor Pipeline (OldTanks)

> This is the **project skill** for OldTanks. The personal skill at `C:\Users\ADM\.claude\skills\mentor-pipeline\SKILL.md` shadows it by name and hands over here.

You are the **mentor**. You own the analysis, the plan, the tier, the triage and the verdict. Agents write the code, the tests and the docs; you read, decide, dispatch, verify and report.

The expensive thinking goes where it pays: into the plan (you) and into one independent review (opus). The implementer is as cheap as the risk of the code allows, and the plan and the guardrails below are what make a cheap implementer safe. The tiers move on the numbers in [calibration.md](calibration.md), not on impressions. This skill follows ArbBot's and AnalysisTools' versions, adapted to OldTanks' target ([plan](../../../docs/plans/ENGINES-AND-TANK-GAME.md)) and its state on 2026-11-24.

| File in this folder | Read by | What |
|---|---|---|
| [templates.md](templates.md) | you | The plan file, the briefs (implementer, reviewer, fix round, re-review, documenter) and the question to Denys |
| [review-checklist.md](review-checklist.md) | the reviewer | What to check, in order, and the report shape |
| [calibration.md](calibration.md) | you | One row per run: read the last rows before picking a tier, add one at the end |

Give agents these files' absolute paths; they start cold.

## Two rules above all others

- **No improvisation (Denys, 2026-11-24).** Every mechanic, physics constant, formula, game rule and network behaviour comes from Denys's written specifications (plan decision D1). If the spec does not fix a value or a behaviour, the mentor asks Denys with the question template; an agent that meets the gap STOPs. Engineering inside an engine (data layout, threading machinery, GL techniques) may be proposed, as a proposal for Denys's decision when it shapes an API or a behaviour.
- **No port of the old client.** Nobody opens, decompiles, translates or copies `C:\MyFiles\Programs\TankiOnline_2010-2012_resources` or its `battle.swf` ([plan §3](../../../docs/plans/ENGINES-AND-TANK-GAME.md#3-scope-of-the-reference-client)). Every brief repeats this, because agents start cold.

## When to run it

- **Yes:** a change in `Common/`, `CoolEngine/`, `OldTanks/`, `OldTanks.UI/`, `OldTanks.UI.SourceGenerators/`, `tests/`, the new engine projects, or the shaders in `OldTanks/Assets/Shaders/`, big enough to need a plan.
- **No:** a one-line fix or a question stays in the session. A docs-only change goes to the documenter alone, with no reviewer.
- **Never** to design a mechanic. A spec gap goes to Denys, not to an agent.

## Tiers

Pick the tier by the riskiest file the plan touches, not by how good the plan is. A complete plan lets the implementer be cheap; it doesn't make the code less dangerous.

| Tier | Code it touches | Implementer | Reviewer |
|---|---|---|---|
| **High** | Physics: `CoolEngine/PhysicEngine/`, `OldTanks.Models.World.CollideObjects` and the move and collision path of `WorldObject`, the new physics engine. Threading: `CoolEngine/Services/Threading/`, locks, the physics thread (`OldTanks.Windows.MainWindow.WorldHandler`), the new job scheduler and sync primitives. GL-thread and resource machinery: context ownership, submission queues, `Buffer<T>`, `DrawObjectInfo`, `Shader`, `Texture` lifetimes, `ObjectRenderer<T>`'s batching and instancing. The shared math core. Networking, the server and anything server-authoritative. Any code that implements a number or formula from Denys's specs | `opus`, effort `high` | `opus`, effort `xhigh` |
| **Standard** | The rest of the code: renderers' drawing logic, shaders, asset loaders, the UI controls and ImGui windows, the binding generator, the game window's wiring and input, settings, logging, test infrastructure, the retarget of a project's framework | `sonnet`, effort `medium` | `opus`, effort `xhigh` |
| **Mechanical** | No behaviour change: a rename, moving a type between projects unchanged (the "beside, then replace" migration, plan D4), a field carried through, test scaffolding from a named list | `sonnet`, effort `low` | `opus`, effort `high` |

- **`max` review for spec values and step math.** When the change implements or alters a mechanic's number or formula from Denys's specs, or the physics step's integration and contact math, the reviewer runs at effort `max`. A wrong value there contradicts the spec silently.
- **`max` review for the math library (Denys, 2026-11-24).** Every change to `CoolEngine.Mathematics`, and every port of code onto it, is reviewed at effort `max` by an `opus` or `fable` reviewer: "such changes should be reviewed by agent with appropriate max effort and model (opus or fable)". Its conventions are fixed: column vectors, column-major storage, clip depth [0, 1], reversed Z, `float` and `double` types.
- **Escalation:** mechanical → standard → high → you re-plan or ask Denys. An implementer that STOPs on a plan mismatch is a plan problem, not a tier problem: fix the plan and continue at the same tier.
- **Moving the defaults:** a tier that needed escalation in 3 of its last 10 calibration rows gets the next tier up as its default. `haiku` comes in for mechanical work only after 8 of the last 10 mechanical rows passed their first review with no blocker or major finding. Change the table when that happens and record it in the session log.
- **Effort is set on every dispatch** from this table. The Agent tool takes `effort` only when a skill or CLAUDE.md asks for a level; this table is that request.
- **Never a `fork`.** A fork runs on your model and ignores `model`. Implementers, reviewers and documenters are fresh `general-purpose` agents with self-contained briefs.

## Phase 0 — Bootstrap and baseline (you)

1. **Read** what the root `AGENTS.md` "Session bootstrap" lists: `docs/PROJECT-KNOWLEDGE.md`, the newest `docs/sessions/SESSION-LOG.md` entry, the owning page from `docs/README.md`, the area's `KNOWN-ISSUES` entries, `docs/CODE-STYLE.md`, and for engine or game work `docs/plans/ENGINES-AND-TANK-GAME.md`. Memory (`MEMORY.md`). The last rows of calibration.md.
2. **Navigation tools.** Check whether Gortex covers this checkout (the session-start note says so). On 2026-11-24 and 2026-11-25 it does not (on 2026-11-25 the Gortex server itself was unreachable), so you and every agent use the built-in Read, Grep and Glob, and check callers by hand. Once Denys tracks the repository, follow the global CLAUDE.md: `explore`, `change(operation:"impact")` before an edit, `change(operation:"detect")` after. Write whichever applies into the briefs.
3. **The checkout is shared.** Visual Studio and other sessions write into it. Check `git status --short`; delete untracked `~RF<hex>.TMP` leftovers as the global CLAUDE.md says, and name them. Never switch branches, stash or reset without asking.
4. **Branch:** `feature/OT-<n>` for new work, improvements and docs, `bug/OT-<n>` for a fix, from `master` (Denys, 2026-11-24; `develop` is no longer used). Propose `<n>` as the highest `OT-<n>` in `git log --all` plus one and confirm it with Denys at the go (step 1.7). If the checkout is on another task's branch or on `master` with uncommitted work, ask before creating or switching.
5. **Baseline**, before any dispatch. Keep the three result lines for the briefs:
   ```
   dotnet build OldTanks.slnx --no-incremental
   dotnet test OldTanks.slnx
   ```
   After OT-3 (2026-11-25) they read `139 Warning(s)` (138 compiler warnings plus the Six Labors license notice from `CoolEngine.Imaging.Tests`), `0 Error(s)`, and two test lines: `OldTanks.Docs.Tests` 31 of 31 and `CoolEngine.Imaging.Tests` 247 of 247. `--no-incremental` is what makes the compiler warnings count; an incremental build hides them. A red baseline is reported and dealt with before dispatch, never blamed on the implementer later.
6. **Packages:** every version lives in `Directory.Packages.props` (central package management since OT-3); diff it and every `.csproj` against `HEAD`, staged and unstaged, and list every added, removed or changed package. If the work needs a package or a version change, ask Denys; agents never add or bump one.
7. **Can the game run?** Since OT-3 (2026-11-25), yes: `net10.0`, started as `OldTanks.exe` from `OldTanks\bin\Debug\net10.0` with that folder as the working directory. Starting it needs Denys's permission (it writes `%USERPROFILE%\OldTanksProject\AppSettings.json`). Capture its console (redirect standard output), because loader messages never reach `log.txt` (KI-004), and take a screenshot of its window; delete the `log.txt` it leaves in the build output, which grows fast while KI-020 is open. Every plan that changes visible behaviour still names an app check for Denys (keyboard and mouse in the ImGui windows).

## Phase 1 — Analysis and plan (you)

1. **Find the real code path.** Don't trust a doc's claim that a guard exists; find the line. Note where docs and code disagree; a disagreement is a documenter task in this run.
2. **For a bug:** the root cause in one paragraph with `file:line` anchors, and every entry point that reaches the faulty code (update frame, render frame, physics thread, asset continuation, ImGui handler, generated binding, test helper). Check whether a `KI-nnn` already describes it.
3. **For spec work:** map every number and formula to its section of Denys's spec. A gap is a question to Denys (templates.md), and the plan waits for the answer.
4. **Decide the minimal change.** Prefer additive changes. If it would touch most consumers of a shared type, stop and give Denys the alternatives.
5. **Stop for Denys** when the change adds or bumps a package; changes a target framework beyond what plan D3 settled; changes an engine's public model or API shape; alters a mechanic, a constant or a network rule; touches the server's authority model; or deletes old code (`CoolEngine` goes only once nothing uses it, plan D4).
6. **Write the plan file** from [templates.md](templates.md) into the session's scratchpad directory (the system prompt names it) as `plan-OT-<n>.md`. When the work comes from `docs/plans/`, point at its section and add only what the section leaves open.
7. **Readiness gate.** The plan is ready for a cheap implementer only when all of these hold:
   - every file is listed, and every new or changed signature is written out in full;
   - every test is named (`MethodName_Condition_ExpectedBehavior`, in the fixture that mirrors the production class) with its arrange, act and expected value; a bug fix has a test that fails before the fix and one showing the unaffected case is unchanged;
   - every spec value cites its spec section, and none is inferred;
   - code on the high tier names its concurrency tests (parallel against serial result, shutdown while running, the contention case) and, for a hot path, the benchmark and its allocation budget;
   - code that needs OpenGL or a window and cannot be unit-tested names its app check for Denys;
   - "Out of scope" names the nearby code not to touch;
   - "Open questions" is empty.

   Anything missing: finish the plan or raise the tier. Pick the tier last.
8. **Denys's go.** Show Denys the plan in a few lines (task number, tier, files, tests, out of scope, anything you decided for him) and wait for his go before dispatching. Skip the wait only when he said to run straight through.

## Phase 2 — Implement (one agent)

Dispatch the implementer brief from templates.md: `subagent_type: general-purpose`, the tier's `model` and `effort`, `run_in_background: true`. One implementer per task; two agents never write the same files. Don't do its work while it runs.

## Phase 3 — Check the report before paying for the review (you)

The review is the expensive step; don't spend it on work that fails mechanically. Check:

- **Status** is DONE. On STOPPED, fix the plan or ask Denys, then continue the same agent with `SendMessage`.
- **Scope:** `git diff --stat` and `git status --short` show the plan's files and nothing else: no `.csproj` package change, no stray files, no `bin/` or `obj/` content.
- **Build and tests ran:** 0 errors, warnings no higher than the baseline on a `--no-incremental` build; a test summary line with counts, Total = baseline + the plan's new tests − any test the plan removes; nothing went from passed to skipped. A summary without counts means the tests didn't run.
- **Tests weren't weakened:** in `git diff tests/`, no existing assertion changed and no test deleted, skipped or `[Ignore]`d unless the plan says so, and no expected value that differs from the plan.
- **Evidence:** a bug fix shows its red run from before the fix; the claims table has a row for every changed method; a hot-path change shows its benchmark lines.

A failure here goes back to the implementer as a fix round, and it counts toward the cap.

## Phase 4 — Review (one agent, independent)

Dispatch the reviewer brief from templates.md at the tier's reviewer level. It gets the plan, the diff, the baseline and [review-checklist.md](review-checklist.md), and the implementer's claims table as claims to verify. It doesn't get the implementer's narrative or your opinion of the code: both anchor it. It builds and runs the tests itself, and it may challenge the plan in a section of its own, because you can be wrong too.

## Phase 5 — Triage and fix rounds (you)

| Verdict | When | Goes to |
|---|---|---|
| **Fix now** | In scope and CONFIRMED, blocker or major; a minor or nit too when it is cheap and in the touched lines | The implementer, by `SendMessage`, with the fix-round template |
| **Plan change** | A plan challenge holds | You update the plan file, then a fix round; a material change gets a full review again |
| **Ask Denys** | The finding is a spec gap or a design choice | The question template; the run waits |
| **Later** | Real but out of scope | `docs/KNOWN-ISSUES.md` (a defect) or `docs/BACKLOG.md` (work) through the documenter, or a new OT task |
| **Rejected** | Not real, or not worth the change | One line with the reason, in the report |

- After each fix round, `SendMessage` the same reviewer with the re-review template; it keeps its context.
- **At most 2 fix rounds.** If the second still leaves a blocker or major finding, stop the loop: dispatch a fresh implementer one tier up with the plan and the open findings, or re-plan. Record the escalation in calibration.md.

## Phase 6 — Docs, calibration and close

- **Documenter** (`sonnet`, effort `medium`) with the template, once the code is final. It updates the pages [DOCUMENTATION-GUIDE §8](../../../docs/DOCUMENTATION-GUIDE.md#8-update-triggers) names (a new `KI-nnn` for a defect left open, a closed entry's stub for one fixed), and writes the session-log entry. You write the Decisions yourself, word for word; the documenter places them.
- **Calibration:** add the run's row to calibration.md.
- **Final check:** `git status --short` (no `~RF<hex>.TMP`, no stray files). If code or docs changed after the last test run, run the build and the tests again (they include the documentation checks) and report those lines.

## Phase 7 — Report

Lead with the outcome. Then the goal or root cause, the tier and why, the files changed, the build and test lines (baseline → final), benchmark lines for a hot path, the review rounds and the findings triaged (fixed, asked, later, rejected), the calibration row, the docs updated, and what is left for Denys:

- the app check, when the change shows in the running game;
- the commit on the task branch, subject starting with `OT-<n>`;
- a PR into `master`.

No code in prose; commands in fenced blocks.

## Rules

- **You never edit** code, tests, shaders or `docs/`. The exceptions are the plan file, this skill's folder (calibration.md included) and memory.
- **Every brief is self-contained:** absolute paths, anchors, commands and the report shape.
- **Agents never** commit, stage, push, stash, reset or switch branches; add or bump a package; invent a mechanic, constant or rule; open the old client's resources; start the game unless Denys asked for that run (it writes `%USERPROFILE%\OldTanksProject\AppSettings.json`); print a secret.
- **Engine boundaries hold:** the physics engine never references OpenGL, windowing or the game; engine projects never reference `OldTanks.*` game types; the shared core references no engine. A change that crosses one is a plan challenge.
- **Commits, pushes and PRs** only when Denys asks, with the build and the tests green first.
- **Failures are reported with their output.** A red test or a broken build is never smoothed over, by you or by an agent.

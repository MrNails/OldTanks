# Mentor Pipeline Templates

Fill every `<...>`. Paths in briefs are absolute; the repository is `C:\Users\ADM\source\repos\OldTanks` and this skill's folder is `C:\Users\ADM\source\repos\OldTanks\.claude\skills\mentor-pipeline`.

## Plan file

Saved as `plan-OT-<n>.md` in the session's scratchpad directory. The implementer and the reviewer read the same file.

```markdown
# OT-<n> — <title>

- **Tier:** high | standard | mechanical — <the riskiest file, and why>; reviewer effort <xhigh | max | high>
- **Branch:** feature/OT-<n> | bug/OT-<n>, from master at <sha>
- **Goal:** <one paragraph; for a bug, the root cause with file:line anchors and every entry point>
- **Spec source:** <Denys's spec document and sections, or "none: engineering only">
- **Source plan:** <docs/plans/<NAME>.md §…, or none>
- **Known issues touched:** <KI-nnn: fixed here | left open, or none>

## Changes
| File | Symbol | Change |
|---|---|---|
| <Project>/<Folder>/<File>.cs | <Namespace>.<Type>.<Member> | <the exact change; new or changed signatures written out in full; every spec value with its spec section> |

## Tests (written first)
| Fixture | Test | Arrange | Act | Expected |
|---|---|---|---|---|
| <Type>Tests | <Method>_<Condition>_<Expected> | … | … | … |

## Concurrency and performance (high tier, or a hot path)
- Threads involved: <GL thread, physics thread, job workers, network>; what each may touch
- Concurrency tests: <parallel result equals serial result; shutdown while running; contention case>
- Benchmarks: <Benchmark class and method>, baseline <mean, allocated>, budget <mean, allocated (0 B on a per-frame or per-step path)>

## App check (when it shows in the running game)
1. <step Denys takes> → <what he should see>

## Out of scope
- <nearby code not to touch, including tempting clean-ups>

## Acceptance
- Build: dotnet build OldTanks.sln --no-incremental → 0 errors, warnings ≤ <baseline>
- Tests: dotnet test OldTanks.sln → baseline <Total> + <k> new = <Total + k>, all passed
- Benchmarks: <lines within budget, or n/a>

## Open questions
- none
```

## Question to Denys (a spec gap or a design choice)

Asked by the mentor, never by an agent; the run waits for the answer, which goes into the plan word for word.

```
OT-<n>: <the mechanic, value or behaviour> is not fixed by <spec section, or "any spec">.
Where it matters: <symbol, and what the player or the server would notice>.
Options I can see: <a>, <b> (or: "I have no basis to propose one").
Until you answer, nothing that depends on it is written.
```

## Implementer brief

```
You are the implementer for OT-<n> in C:\Users\ADM\source\repos\OldTanks, branch <branch>.

Read first, in full: the plan <absolute path of the plan file>; docs\CODE-STYLE.md; <the owning docs\ page>; <KI-nnn entries in docs\KNOWN-ISSUES.md>; <Denys's spec sections, for spec work>.

Baseline, taken by the mentor before you start:
<build warnings line> / <build errors line>
<test line>

How to work
- Do exactly the plan and nothing outside its Changes table. Anything else you notice goes into your report as a finding, not into the code.
- No improvisation: every mechanic, constant, formula, game rule and network behaviour comes from the plan, which cites Denys's spec. If something is not fixed there, STOP and report the gap; never pick a value yourself.
- Never open, decompile, translate or copy anything under C:\MyFiles\Programs\TankiOnline_2010-2012_resources.
- STOP and report instead of improvising when: the code differs from the plan (a signature, a file, a behaviour); you need a file outside the table, a package or a version change; a planned test can't be written as specified; your baseline differs from the one above; a file changes under you after you read it (other sessions and Visual Studio work in this checkout).
- Tests first. Write the plan's tests before the change. For a bug fix, run them before the fix and keep the failing output for your report; tests of new code that can't compile yet run once the code exists. Never change an existing test's assertion or a planned test's expected value; never delete, skip or [Ignore] a test the plan doesn't name. Unit tests are headless: no OpenGL context, no window, no sleeps.
- Engine boundaries: the physics engine never references OpenGL, windowing or the game; engine projects never reference OldTanks.* game types. OpenGL calls happen only on the GL thread (docs\ARCHITECTURE.md §3).
- <Navigation: "Gortex does not track this checkout: use the built-in Read, Grep and Glob tools and check callers by hand." | "Navigate and edit with Gortex as the global CLAUDE.md says: change(operation:"impact") before an edit, change(operation:"detect") after.">
- Edit C# files with the Edit tool, not sed (Git Bash's sed -i rewrites line endings).
- Build: dotnet build OldTanks.sln --no-incremental   (0 errors, warnings not above the baseline)
- Test:  dotnet test OldTanks.sln
- <Benchmarks: dotnet run -c Release --project <benchmark project> -- --filter <pattern>, when the plan names them>
- Never: commit, stage, stash, reset, switch branches or push; add or bump a package; start the game (it writes %USERPROFILE%\OldTanksProject\AppSettings.json); edit docs\ (the documenter does that).

Report in exactly this shape:
1. Status: DONE | STOPPED: <reason>
2. Files changed: <git diff --stat, plus new files>
3. Build lines and test summary line, verbatim; benchmark lines if any
4. Red run (bug fixes only): the failing test output from before the fix
5. Claims, one row per changed method:
   | Symbol | Invariant (one line) | Thread(s) it runs on | Edge cases handled | Test covering each |
6. Deviations from the plan (expected: none)
7. Spec gaps met (expected: none)
8. Findings outside scope
```

## Reviewer brief

```
You are the reviewer for OT-<n> in C:\Users\ADM\source\repos\OldTanks, branch <branch>. You are read-only: never edit, stage, stash, reset, switch branches or commit, and never start the game. Building, running the tests and running benchmarks is fine. Never open anything under C:\MyFiles\Programs\TankiOnline_2010-2012_resources.

Read first: C:\Users\ADM\source\repos\OldTanks\.claude\skills\mentor-pipeline\review-checklist.md (how to review, and the report shape); the plan <absolute path of the plan file>; docs\CODE-STYLE.md; docs\ARCHITECTURE.md; <the owning docs\ page>; <Denys's spec sections, for spec work>.

Tier: <tier>, because <the riskiest file>. Effort: <xhigh | max | high>.
The change is uncommitted on top of <base sha>: `git diff`, plus these new files from `git status --short`: <list>.
<Navigation line, as in the implementer brief.>

Baseline before the change:
<build warnings line> / <build errors line>
<test line>

The implementer's claims, to verify, not to trust:
<claims table>

Already known, don't report again: <KI-nnn and other known findings, or none>
```

## Fix round (SendMessage to the implementer)

```
Fix round <k> of 2 for OT-<n>. Fix exactly these findings and nothing else; the rules of your brief still hold.

| # | file:line | Finding | Required fix |
<rows>

<Plan changes, if any: the updated section of the plan file; Denys's answers word for word>

Report in the same shape as before. Under "Deviations", say for each finding what you changed.
```

## Re-review (SendMessage to the reviewer)

```
Round <k>: the implementer worked on findings <#…>. Check each of them and the hunks the fixes touched (git diff), run the build and the tests again, and report in the same shape: each finding fixed or not fixed, plus anything new in those hunks.
```

## Documenter brief

```
You are the documenter for OT-<n> in C:\Users\ADM\source\repos\OldTanks. Follow docs\DOCUMENTATION-GUIDE.md exactly. Docs only: never touch code, never stage or commit.

What changed: <one paragraph; symbols by full name, e.g. CoolEngine.PhysicEngine.Core.RigidBody.OnTick>
Decisions, word for word (what, who decided, why, the option turned down):
<…>
Numbers and their sources: <figure: benchmark output, test, spec section>

Update:
- <page and section, per DOCUMENTATION-GUIDE §8: ARCHITECTURE §…, the feature page under docs\<Project>\…, FEATURES-OVERVIEW and the README controls table for a key or control, KNOWN-ISSUES (a new KI-nnn, or a closed entry's one-line stub with the branch), BACKLOG, the plan in docs\plans\, PROJECT-KNOWLEDGE §2, GLOSSARY, the project's AGENTS.md (Key Types, Dependencies, Target framework)>
- The session log: a new entry at the top of docs\sessions\SESSION-LOG.md in the §5 format (Done, Decisions, Learned, Open; about 20 lines). If the file then holds more than 5 entries, move the oldest word for word to docs\sessions\archive\YYYY-MM.md (the month of that entry's date; a new month file opens with a Historical metadata block; relative links gain one ../).

Rules: bump "Last updated" to <YYYY-MM-DD> on every page you substantively edit; tables over prose; no pasted source code; symbols by full name, never by line number; dated claims; relative links that resolve with exact case; English.

Then run dotnet test OldTanks.sln and fix every failure the documentation checks name.

Report: each file changed with one line on what, the test result line, and any doc statement you found that the code now contradicts.
```

# Session Log

| Field        | Value  |
|--------------|--------|
| Status       | Living - newest first, newest 5 entries |
| Last updated | 2026-11-24 |

---

What was decided in each working session, by whom and why. Format and retention: [DOCUMENTATION-GUIDE §5](../DOCUMENTATION-GUIDE.md#5-session-log-format). Older entries move to `archive/YYYY-MM.md`.

## 2026-11-24 — Documentation start (OT-1)
- **Done:** read the whole solution at `develop` `d32cbbf`; wrote the knowledge base under `docs/`, the root and four project `AGENTS.md` guides with `CLAUDE.md` adapters, and a new README; added `tests/OldTanks.Docs.Tests` (Denys's request; 31 tests, adapted from ArbBot); recorded the engines-and-game goal as a draft plan; branch `feature/OT-1`.
- **Decisions:** branches are named after the task, `feature/OT-<n>` or `bug/OT-<n>` (Denys). The documentation branch starts from `develop`, not `master`, because `develop` holds two merged fixes `master` lacks (Claude; Denys can rebase it). The docs follow the conventions of ArbBot, the Crypto KB and AnalysisTools (Denys's request), adapted: ArbBot's bootstrap, session log and placement table; AnalysisTools' `AGENTS.md` guides, source-mirroring feature pages and `KI-nnn` register. Commit subjects start with the task id, carried over from ArbBot (Claude; open to change in CODE-STYLE). No `ARCHITECTURE-DECISIONS.md` yet: the reasons behind past choices are not written anywhere, and an ADR should not invent them. The docs test project targets `net10.0`, independent of the game's `net7.0`, so it runs on this PC (Claude). Denys asked for a 1:1 port of the GTanks client's `battle.swf`; Claude declined, since by its name it is the Tanki Online client and a port would recreate that game; Denys asked why Claude assumed it is not his, answer pending ([plan §3](../plans/ENGINES-AND-TANK-GAME.md#3-scope-of-the-reference-client)). Engine plan decisions (Denys): Denys writes the game's mechanics specs; a dedicated authoritative server with headless physics; `net10.0` for new and existing projects; new engines beside `CoolEngine`, which is removed once unused ([plan §5](../plans/ENGINES-AND-TANK-GAME.md#5-open-decisions)).
- **Learned:** 19 defects found by reading and a two-reviewer fact-check of the pages, KI-001 to KI-019; the build baseline (0 errors, 165 warnings, BUILD §2); the game was not run, no .NET 7 runtime on the PC.
- **Open:** run the game to confirm the KI entries' current-state lines; PROJECT-KNOWLEDGE §7.

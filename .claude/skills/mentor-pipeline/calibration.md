# Mentor Pipeline Calibration (OldTanks)

One row per pipeline run, newest at the bottom. Read the last rows before picking a tier. The defaults in [SKILL.md](SKILL.md#tiers) move on these numbers: escalation in 3 of a tier's last 10 rows raises its default; 8 of the last 10 mechanical rows passing their first review with no blocker or major finding lets `haiku` in.

- **Rounds:** fix rounds after the first review (0 means the first review left nothing to fix now).
- **Findings:** blocker / major / minor / nit in the first review.
- **Escalated:** the tier the run ended on, when it moved.
- **Spec questions:** questions the run sent to Denys (a high count means the spec, not the tier, needs work).

The starting defaults come from ArbBot's and AnalysisTools' calibration, where `sonnet` at effort `medium` with an `opus` review at `xhigh` held for standard work, and high-risk code stayed on `opus` at effort `high`.

| Date | Task | Tier | Implementer | Reviewer | Rounds | Findings B/M/m/n | Escalated | Spec questions | Note |
|---|---|---|---|---|---|---|---|---|---|
| 2026-11-25 | OT-3 (.NET 10, central packages, CoolEngine.Imaging PNG/JPEG decoders, generator and ImGui migrations) | standard by the table, run at the high implementer level at Denys's request | `opus` `high`: 3 in parallel worktrees (WP1 PNG + tests, WP2 JPEG, WP3 upgrade fixes), then 1 fresh for WP4 (strict completion) and 1 fresh for WP4b; `sonnet` `medium` documenter | `opus` `xhigh`, same agent for 4 reviews | WP1 2, WP2 2, WP4 2, WP4b 0 | 0/0/6/4 first review; re-reviews 0/0/2/2, 0/0/2/2, 0/0/0/0 | no tier change; two re-plans (WP4 after Denys turned the decoders strict mid-run; WP4b after WP4's round cap) | 0 mechanics; about 25 engineering decisions asked (packages, ImageSharp, strictness, limits, generator, EventHandler, plans) | Mentor plan errors caught by agents: asset counts (3 PNG / 9 JPEG), a JPEG tolerance measured over RGBA instead of RGB, lenient wording left in the plan after the strict decision. Parallel implementers needed worktrees plus scaffolding stubs and fixed helper signatures; integration was clean. Every JPEG bit-identical to Windows; smoke run found pre-existing KI-020 |

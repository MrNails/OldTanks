# Mentor Pipeline Calibration (OldTanks)

One row per pipeline run, newest at the bottom. Read the last rows before picking a tier. The defaults in [SKILL.md](SKILL.md#tiers) move on these numbers: escalation in 3 of a tier's last 10 rows raises its default; 8 of the last 10 mechanical rows passing their first review with no blocker or major finding lets `haiku` in.

- **Rounds:** fix rounds after the first review (0 means the first review left nothing to fix now).
- **Findings:** blocker / major / minor / nit in the first review.
- **Escalated:** the tier the run ended on, when it moved.
- **Spec questions:** questions the run sent to Denys (a high count means the spec, not the tier, needs work).

No runs yet (2026-11-24). The starting defaults come from ArbBot's and AnalysisTools' calibration, where `sonnet` at effort `medium` with an `opus` review at `xhigh` held for standard work, and high-risk code stayed on `opus` at effort `high`.

| Date | Task | Tier | Implementer | Reviewer | Rounds | Findings B/M/m/n | Escalated | Spec questions | Note |
|---|---|---|---|---|---|---|---|---|---|

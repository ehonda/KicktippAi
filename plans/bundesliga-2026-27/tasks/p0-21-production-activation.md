# P0-21 — Validate production and activate schedules

- Status: Not started
- Priority: P0
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-20](p0-20-seed-and-development-validation.md), [P0-24](p0-24-bonus-copy-post-compatibility.md), [P0-25](p0-25-roster-enrichment-and-team-total.md), and every required production entrypoint copied from [P0-19](p0-19-community-workflow-triad.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0008](../decisions/0008-launch-club-elo-from-a-dated-seed.md), [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md), [ADR-0050](../decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md), [ADR-0051](../decisions/0051-require-explicit-launch-roster-enrichment-overlay.md)

## Outcome

Each selected production community succeeds manually before its context and prediction schedules are deliberately enabled.

## Work items

- [ ] Confirm the official first prediction cutoff, desired refresh times, context-before-prediction spacing, owners, and rollback procedure.
- [ ] Obtain and record owner approval for the exact production model, reasoning, output cap, accepted hosted prompt versions, cost ceiling, and arena challenger matrix after P0-23 evidence or its explicit owner waiver; prove Luna/none was not inherited.
- [ ] Record the late Club Elo decision: accepted unattended source/reuse terms or dated-seed operation with network fetching disabled.
- [ ] Record the proposed schedule and activation gate in an ADR.
- [ ] Before any initial prediction in each production community, explicitly publish rosters from ADR-0050's exact pinned DuckDB SHA/revision/date with the paired `--require-launch-coverage --launch-enrichment-overlay` mode from ADR-0051; require `NotEvaluated` DuckDB membership gates plus `LAUNCH_ENRICHMENT_OVERLAY`, then record v2 headed snapshot/disposition/document versions, reconstructed-final totals at or above 464 ages / 464 positions / 450 values, exactly 18 final `Team Accumulated` rows, and no regression from the accepted arena validation. This publication alone is not prediction-posting or schedule authority.
- [ ] Manually dispatch production context collection and inspect all publication dispositions.
- [ ] Manually dispatch one production matchday run and required bonus run; confirm the expected Kicktipp writes.
- [ ] Verify `pes-squad` generated independently, `schadensfresse` generated independently, and the accepted `pes-squad` prediction was copy-posted to `ehonda-ai-arena` without an extra model call.
- [ ] For bonus copy-posting, enforce P0-24's exact normalized question and complete-option-set compatibility; every ordinary source/provenance/question/option mismatch generates and persists exactly one independent prediction with `community_context: "ehonda-ai-arena"` in the same invocation, never the requested `pes-squad` copy-source context, while invalid target selection or immutable-context safety violations fail closed.
- [ ] Inspect production traces for competition, prompt/model identity, context documents, tokens, costs, and errors.
- [ ] Confirm no 2025/26 identity, WM26 collector, or transfer document appears.
- [ ] Enable schedules only for communities whose manual evidence passed; keep failed/unverified communities manual-only.
- [ ] Observe the first scheduled context and prediction sequence and record run links/results.

## Validation evidence

Production activation validation has not run. The read-only [production prerequisite audit from 2026-08-25](../../../docs/onboarding-bundesliga-2026-27/production-prerequisite-audit-2026-08-25.md) confirmed `pes-squad` authentication and Bundesliga 2026/27 read readiness without proving posting rights. It also confirmed `schadensfresse` authentication but found no current prediction-input rows, so community-admin remediation remains required. GitHub Actions secret-name presence, the final model and arena participant identities, pinned enriched v2 production roster publications, manual production writes, and every schedule gate remain open.

A telemetry-disabled read-only refresh on 2026-08-26 reconfirmed this state
without a write or Langfuse path. `pes-squad` exited successfully with exactly
9 current inputs (0 completed / 9 pending), 18 standings teams, 47 selected
Kicktipp context documents, 288 history rows, 18 Club Elo `LaunchSeed` rows
under `NetworkDisabled`, and the 18-club fallback roster path.
`schadensfresse` authenticated and completed its GET requests, but still exposed
9 completed / 0 pending results and 0 current prediction-input rows; it exited
at the exact-nine gate and skipped later profile stages. Neither refresh exposed
deadlines or proved POST permission. A repeated names-only GitHub check also
remained blocked by HTTP 403, so required Actions secret presence is unverified,
not absent. External remediation, production selection, roster publication,
manual writes, and activation remain open.

## Complete when

- The activation ADR is accepted and contains the exact schedules and rollback trigger.
- Manual and first scheduled runs succeed for every activated community.
- The repository workflow status documentation matches reality.

# P0-21 — Validate production and activate schedules

- Status: In progress — Owner configuration, hosted prompt v3, callers, and canonical secret provisioning are prepared; manual production evidence and schedules remain
- Priority: P0
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-20](p0-20-seed-and-development-validation.md), [P0-24](p0-24-bonus-copy-post-compatibility.md), [P0-25](p0-25-roster-enrichment-and-team-total.md), and every required production entrypoint copied from [P0-19](p0-19-community-workflow-triad.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md) (superseded), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0008](../decisions/0008-launch-club-elo-from-a-dated-seed.md), [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md), [ADR-0050](../decisions/0050-publish-enriched-launch-rosters-with-derived-team-subtotals.md), [ADR-0051](../decisions/0051-require-explicit-launch-roster-enrichment-overlay.md), [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)

## Outcome

Each selected production community succeeds manually before its context and prediction schedules are deliberately enabled.

## Owner dispatch authorization — 2026-08-27

After this repository preparation is independently reviewed, integrated,
pushed, and green, P0-21 may manually dispatch context and then predictions for
`pes-squad`, `relaxdays-tippt`, and every selected `ehonda-ai-arena`
participant. Run independent primaries before dependent secondary copies and
stop the affected chain on failure. This explicitly authorizes the resulting
initial prediction writes for those ready rows, subject to the runtime gates
below. `schadensfresse` remains unrun and manual-only pending administrator
setup.

If all manual evidence passes, the Owner also authorizes a later lane to record
the activation ADR and add schedules for only those ready rows. This repository
lane remains schedule-free; it does not create an outer scheduled workflow.

## Work items

- [ ] Confirm the official first prediction cutoff, desired refresh times, context-before-prediction spacing, owners, and rollback procedure.
- [x] Obtain and record Owner approval for the exact production model, reasoning, output cap, accepted hosted prompt versions, planning ceiling, and arena challenger matrix. ADR-0052 records Sol/`xhigh` production, all challenger caps, and proves Luna/`none` was not inherited.
- [x] Prepare the exact primary/copy/challenger callers as manual-only,
      schedule-free `workflow_dispatch` entrypoints, pin live match v3 / bonus
      v1, and record the Owner-confirmed canonical Kicktipp secret pairs.
- [x] Prepare the reusable context workflow's false-by-default, fail-closed
      launch-roster input. `pes-squad`, `relaxdays-tippt`, and the pending
      `schadensfresse` caller opt in to download the exact public artifact and
      run the SHA/revision/date-gated paired overlay before normal profile
      collection. Arena callers omit it because their shared context already
      has the verified exact enriched head.
- [ ] Record the late Club Elo decision: accepted unattended source/reuse terms or dated-seed operation with network fetching disabled.
- [ ] Record the proposed schedule and activation gate in an ADR.
- [ ] Before any initial prediction in each non-arena production community,
      dispatch its prepared context caller and record the pinned overlay's
      `NotEvaluated` DuckDB membership gates plus
      `LAUNCH_ENRICHMENT_OVERLAY`, v2 headed snapshot/disposition/document
      versions, reconstructed-final totals at or above 464 ages / 464
      positions / 450 values, and exactly 18 final `Team Accumulated` rows.
      For arena, re-verify that normal profile collection preserves exact
      enriched snapshot `591adbc3cbc99ee93591f074ad218703c9badb2af4e267142898145825b77ea2`
      with no regression. Roster publication alone is not prediction-posting
      or schedule authority.
- [ ] Manually dispatch production context collection and inspect all publication dispositions.
- [ ] Manually dispatch one production matchday run and required bonus run; confirm the expected Kicktipp writes.
- [ ] Verify `pes-squad` and `schadensfresse` generated independently and the
      accepted `pes-squad` prediction was copy-posted to both
      `relaxdays-tippt` and the arena Sol/`xhigh` participant without an extra
      model call.
- [ ] Validate self-contained arena Sol/`high`, Luna/`medium`, Terra/`xhigh`,
      and Luna/`none` in context-before-prediction order.
- [ ] For bonus copy-posting, enforce P0-24's exact normalized question and complete-option-set compatibility; every ordinary source/provenance/question/option mismatch generates and persists exactly one independent prediction with `community_context: "ehonda-ai-arena"` in the same invocation, never the requested `pes-squad` copy-source context, while invalid target selection or immutable-context safety violations fail closed.
- [ ] Inspect production traces for competition, prompt/model identity, context documents, tokens, costs, and errors.
- [ ] Confirm no 2025/26 identity, WM26 collector, or transfer document appears.
- [ ] Enable schedules only for communities whose manual evidence passed; keep failed/unverified communities manual-only.
- [ ] Observe the first scheduled context and prediction sequence and record run links/results.

## Validation evidence

Production activation validation has not run. The read-only [production prerequisite audit from 2026-08-25](../../../docs/onboarding-bundesliga-2026-27/production-prerequisite-audit-2026-08-25.md) confirmed `pes-squad` authentication and Bundesliga 2026/27 read readiness without proving posting rights. It also confirmed `schadensfresse` authentication but found no current prediction-input rows, so community-admin remediation remains required. ADR-0052 now fixes the model/participant identities and exact callers, including the in-workflow pinned roster path for the three non-arena communities. On 2026-08-27 the Owner confirmed every canonical Actions Kicktipp pair provisioned; that is not API enumeration or successful use. Pinned enriched v2 non-arena roster publications, preservation inspection for the arena head, manual production writes, and every schedule gate remain open.

A telemetry-disabled read-only refresh on 2026-08-26 reconfirmed this state
without a write or Langfuse path. `pes-squad` exited successfully with exactly
9 current inputs (0 completed / 9 pending), 18 standings teams, 47 selected
Kicktipp context documents, 288 history rows, 18 Club Elo `LaunchSeed` rows
under `NetworkDisabled`, and the 18-club fallback roster path.
`schadensfresse` authenticated and completed its GET requests, but still exposed
9 completed / 0 pending results and 0 current prediction-input rows; it exited
at the exact-nine gate and skipped later profile stages. Neither refresh exposed
deadlines or proved POST permission. A repeated names-only GitHub check remained
blocked by HTTP 403; the later Owner provisioning confirmation is authoritative
for presence only. External remediation, runtime authentication/permission,
roster publication, manual writes, and activation remain open.

## Complete when

- The activation ADR is accepted and contains the exact schedules and rollback trigger.
- Manual and first scheduled runs succeed for every activated community.
- The repository workflow status documentation matches reality.

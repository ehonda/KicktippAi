# P0-21 — Validate production and activate schedules

- Status: Not started
- Priority: P0
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-20](p0-20-seed-and-development-validation.md), [P0-24](p0-24-bonus-copy-post-compatibility.md), and every required production entrypoint copied from [P0-19](p0-19-community-workflow-triad.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0008](../decisions/0008-launch-club-elo-from-a-dated-seed.md), [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md)

## Outcome

Each selected production community succeeds manually before its context and prediction schedules are deliberately enabled.

## Work items

- [ ] Confirm the official first prediction cutoff, desired refresh times, context-before-prediction spacing, owners, and rollback procedure.
- [ ] Obtain and record owner approval for the exact production model, reasoning, output cap, accepted hosted prompt versions, cost ceiling, and arena challenger matrix after P0-23 evidence or its explicit owner waiver; prove Luna/none was not inherited.
- [ ] Record the late Club Elo decision: accepted unattended source/reuse terms or dated-seed operation with network fetching disabled.
- [ ] Record the proposed schedule and activation gate in an ADR.
- [ ] Manually dispatch production context collection and inspect all publication dispositions.
- [ ] Manually dispatch one production matchday run and required bonus run; confirm the expected Kicktipp writes.
- [ ] Verify `pes-squad` generated independently, `schadensfresse` generated independently, and the accepted `pes-squad` prediction was copy-posted to `ehonda-ai-arena` without an extra model call.
- [ ] For bonus copy-posting, enforce P0-24's exact normalized question and complete-option-set compatibility; every ordinary source/provenance/question/option mismatch generates and persists exactly one independent prediction with `community_context: "ehonda-ai-arena"` in the same invocation, never the requested `pes-squad` copy-source context, while invalid target selection or immutable-context safety violations fail closed.
- [ ] Inspect production traces for competition, prompt/model identity, context documents, tokens, costs, and errors.
- [ ] Confirm no 2025/26 identity, WM26 collector, or transfer document appears.
- [ ] Enable schedules only for communities whose manual evidence passed; keep failed/unverified communities manual-only.
- [ ] Observe the first scheduled context and prediction sequence and record run links/results.

## Validation evidence

Production activation validation has not run. The read-only [production prerequisite audit from 2026-08-25](../../../docs/onboarding-bundesliga-2026-27/production-prerequisite-audit-2026-08-25.md) confirmed `pes-squad` authentication and Bundesliga 2026/27 read readiness without proving posting rights. It also confirmed `schadensfresse` authentication but found no current prediction-input rows, so community-admin remediation remains required. GitHub Actions secret-name presence, the final model and arena participant identities, manual production writes, and every schedule gate remain open.

## Complete when

- The activation ADR is accepted and contains the exact schedules and rollback trigger.
- Manual and first scheduled runs succeed for every activated community.
- The repository workflow status documentation matches reality.

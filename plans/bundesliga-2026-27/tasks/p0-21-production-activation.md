# P0-21 — Validate production and activate schedules

- Status: Not started
- Priority: P0
- Depends on: [P0-06](p0-06-model-ledger-and-cost-baseline.md), [P0-20](p0-20-seed-and-development-validation.md), and every required production entrypoint copied from [P0-19](p0-19-community-workflow-triad.md)
- Decisions: [ADR-0005](../decisions/0005-launch-community-and-prediction-topology.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0007](../decisions/0007-require-context-hygiene-before-launch.md), [ADR-0008](../decisions/0008-launch-club-elo-from-a-dated-seed.md), [ADR-0013](../decisions/0013-club-elo-snapshot-and-freshness-contract.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0045](../decisions/0045-verify-versioned-prompt-promotion-before-validation.md)

## Outcome

Each selected production community succeeds manually before its context and prediction schedules are deliberately enabled.

## Work items

- [ ] Confirm the official first prediction cutoff, desired refresh times, context-before-prediction spacing, owners, and rollback procedure.
- [ ] Obtain and record owner approval for the exact production model, reasoning, output cap, hosted prompt versions, cost ceiling, and arena challenger matrix; prove Luna/none was not inherited.
- [ ] Record the late Club Elo decision: accepted unattended source/reuse terms or dated-seed operation with network fetching disabled.
- [ ] Record the proposed schedule and activation gate in an ADR.
- [ ] Manually dispatch production context collection and inspect all publication dispositions.
- [ ] Manually dispatch one production matchday run and required bonus run; confirm the expected Kicktipp writes.
- [ ] Verify `pes-squad` generated independently, `schadensfresse` generated independently, and the accepted `pes-squad` prediction was copy-posted to `ehonda-ai-arena` without an extra model call.
- [ ] For bonus copy-posting, require exact normalized question and option compatibility; generate any incompatible arena question independently.
- [ ] Inspect production traces for competition, prompt/model identity, context documents, tokens, costs, and errors.
- [ ] Confirm no 2025/26 identity, WM26 collector, or transfer document appears.
- [ ] Enable schedules only for communities whose manual evidence passed; keep failed/unverified communities manual-only.
- [ ] Observe the first scheduled context and prediction sequence and record run links/results.

## Validation evidence

Not run yet.

## Complete when

- The activation ADR is accepted and contains the exact schedules and rollback trigger.
- Manual and first scheduled runs succeed for every activated community.
- The repository workflow status documentation matches reality.

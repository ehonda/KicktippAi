# P0-19 — Validate production and activate schedules

- Status: Not started
- Priority: P0
- Depends on: [P0-18](p0-18-seed-and-development-validation.md)

## Outcome

Each selected production community succeeds manually before its context and prediction schedules are deliberately enabled.

## Work items

- [ ] Confirm the official first prediction cutoff, desired refresh times, context-before-prediction spacing, owners, and rollback procedure.
- [ ] Record the proposed schedule and activation gate in an ADR.
- [ ] Manually dispatch production context collection and inspect all publication dispositions.
- [ ] Manually dispatch one production matchday run and required bonus run; confirm the expected Kicktipp writes.
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

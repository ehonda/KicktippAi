# P0-03 — Fix Bundesliga matchday completion

- Status: Not started
- Priority: P0
- Depends on: [P0-01](p0-01-current-competition.md)

## Outcome

A 2026/27 Bundesliga matchday is complete only when all nine fixtures exist and are completed.

## Work items

- [ ] Introduce central competition metadata for expected matches per matchday instead of a comparison with the old season ID.
- [ ] Set the 2026/27 expected match count to nine and make `FirebaseMatchOutcomeRepository` consume it.
- [ ] Define the behavior for unknown competitions explicitly; do not infer completion merely because every currently stored row is complete.
- [ ] Cover zero through eight completed rows, nine completed rows, nine rows with one unavailable result, duplicate/extra rows, and WM26 behavior in tests.
- [ ] Ensure incomplete-matchday collection uses the same metadata rather than a second constant.

## Validation

- Run the `FirebaseMatchOutcomeRepository_GetIncompleteMatchdaysAsync` test trees in `tests/FirebaseAdapter.Tests`.
- Run affected `MatchOutcomeCollectionServiceTests` in `tests/Orchestrator.Tests`.

## Complete when

- Eight completed stored matches still report the matchday incomplete.
- Exactly nine completed, distinct fixtures report it complete.
- Unknown competition behavior is tested and documented.

# P0-03 — Fix Bundesliga matchday completion

- Status: Complete
- Priority: P0
- Depends on: [P0-01](p0-01-current-competition.md)
- Decisions: [ADR-0001](../decisions/0001-current-bundesliga-season-only.md), [ADR-0012](../decisions/0012-competition-aware-matchday-completion.md)

## Outcome

A 2026/27 Bundesliga matchday is complete only when all nine fixtures exist and are completed.

## Work items

- [x] Introduce central competition metadata for expected matches per matchday instead of a comparison with the old season ID.
- [x] Set the 2026/27 expected match count to nine and make `FirebaseMatchOutcomeRepository` consume it.
- [x] Define the behavior for unknown competitions explicitly; do not infer completion merely because every currently stored row is complete.
- [x] Cover zero through eight completed rows, nine completed rows, nine rows with one unavailable result, duplicate/extra rows, and WM26 behavior in tests.
- [x] Ensure incomplete-matchday collection uses the same metadata rather than a second constant.

## Validation

- Run the `FirebaseMatchOutcomeRepository_GetIncompleteMatchdaysAsync` test trees in `tests/FirebaseAdapter.Tests`.
- Run affected `MatchOutcomeCollectionServiceTests` in `tests/Orchestrator.Tests`.

## Validation evidence

- 2026-08-16: focused Core completion-policy tests passed 13/13, covering completed counts zero through eight, exactly nine, pending, duplicate, extra, blank IDs, WM26 variable matchdays, and unknown competition rejection.
- 2026-08-16: existing Firebase incomplete-matchday trees passed 6/6 and the new competition-policy integration tree passed 3/3 against the Firestore emulator.
- 2026-08-16: focused `MatchOutcomeCollectionServiceTests` passed 4/4, including rejection before client or repository creation.
- 2026-08-16: full affected suites passed: `Core.Tests` 81/81, `FirebaseAdapter.Tests` 219/219, and `Orchestrator.Tests` 825/825.
- 2026-08-16: the two affected stale-metadata integration regressions passed after advancing their explicit seed competition to `bundesliga-2026-27`.
- [ADR-0012](../decisions/0012-competition-aware-matchday-completion.md) records the exact Bundesliga, WM26, unknown-competition, identity, and fail-fast behavior. P0-02 competition scoping and GUID prediction IDs remain unchanged.

## Complete when

- Eight completed stored matches still report the matchday incomplete.
- Exactly nine completed, distinct fixtures report it complete.
- Unknown competition behavior is tested and documented.

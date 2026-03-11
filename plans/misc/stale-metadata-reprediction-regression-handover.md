# Stale Metadata Reprediction Regression Handover

## Goal

Add an integration test that verifies the stale-metadata regression cannot happen again in the real repository-backed flow.

This should cover the scenario where:

1. An initial prediction exists at `repredictionIndex: 0`.
2. Context documents change, so that initial prediction becomes outdated.
3. A reprediction is created at `repredictionIndex: 1` using the updated context.
4. Verification runs again and must treat the latest prediction as up to date.
5. The matchday reprediction flow must not create `repredictionIndex: 2` unless context changed again after `repredictionIndex: 1`.

## Why This Needs an Integration Test

We now have:

- command-level regression tests that mock the old inconsistent repository contract back in
- repository-level tests that verify metadata methods return the latest reprediction

What is still missing is an end-to-end test that exercises the real repository implementation and the real command logic together. That integration test should prove that the fixed repository behavior prevents the stale-metadata mismatch from reappearing in the orchestration flow.

## Target Commands

The desired integration test should execute these commands in sequence:

1. `verify-matchday`
2. `matchday`

The intent is to mirror the operational sequence where verification decides whether a follow-up prediction run is necessary.

## Proposed Test Shape

### Setup

- Use the Firestore-backed test infrastructure, not pure mocks.
- Seed one match with two stored prediction versions:
  - `repredictionIndex: 0` with an earlier `CreatedAt`
  - `repredictionIndex: 1` with a later `CreatedAt`
- Ensure the latest prediction's context document names reflect the already-updated context.
- Seed context documents so their latest versions are:
  - newer than prediction index 0
  - not newer than prediction index 1
- Seed Kicktipp-facing inputs so the placed prediction matches the latest stored prediction.

### Execute

- Run `verify-matchday --check-outdated`.
- Assert that verification succeeds and does not classify the latest prediction as outdated.
- Then run `matchday` in reprediction mode against the same match.

### Assert

- No new prediction should be saved at `repredictionIndex: 2`.
- The latest reprediction index should remain `1`.
- If trace or console output is asserted, it should indicate the prediction is up to date or skipped for reprediction.

## Suggested Implementation Notes

- Reuse existing Firestore emulator fixtures from `tests/FirebaseAdapter.Tests` if possible.
- If command tests currently rely too heavily on mocks, consider introducing a small integration-style test harness in `tests/Orchestrator.Tests` that wires the real `FirebasePredictionRepository` and real `FirebaseContextRepository` into the command.
- Keep the test scoped to a single match to avoid unnecessary orchestration complexity.
- Prefer deterministic timestamps and explicit context document names matching the regression analysis, for example:
  - `recent-history-fcb.csv`
  - `away-history-fcb.csv`
  - optionally `recent-history-b04.csv`

## Related Fixed Methods

These repository methods were fixed to order metadata reads by latest `repredictionIndex`:

- `GetPredictionMetadataAsync`
- `GetCancelledMatchPredictionMetadataAsync`
- `GetBonusPredictionMetadataByTextAsync`

## Related Regression Tests Already Added

- `VerifyMatchdayCommand_Outdated_Tests.Stale_prediction_metadata_from_initial_prediction_can_mark_latest_prediction_as_outdated`
- `VerifyMatchdayCommand_Outdated_Tests.Stale_cancelled_match_prediction_metadata_can_mark_latest_prediction_as_outdated`
- `VerifyBonusCommand_Outdated_Tests.Stale_bonus_prediction_metadata_from_initial_prediction_can_mark_latest_prediction_as_outdated`
- repository regressions for latest metadata retrieval in `FirebasePredictionRepository` tests

## Fresh-Session Recommendation

Implement this in a fresh session because it likely needs:

- additional command-test infrastructure wiring
- careful Firestore fixture reuse
- potential coordination between Orchestrator and FirebaseAdapter test helpers

That work is broader than the current regression fix and is easier to reason about independently.

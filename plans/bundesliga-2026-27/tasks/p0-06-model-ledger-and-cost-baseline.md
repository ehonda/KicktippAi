# P0-06 — Pin the model ledger and launch cost baseline

- Status: Not started
- Priority: P0
- Depends on: [P0-05](p0-05-prompt-route.md)

## Outcome

The launch configuration has one recorded model, reasoning level, output cap, prompt version, and pre-launch whole-season cost estimate.

## Work items

- [ ] Select the production model, reasoning effort, maximum output tokens, and any fallback behavior; record them in an ADR.
- [ ] Add the exact configuration to the repository's model/onboarding ledger rather than relying on command defaults.
- [ ] Estimate 306 fixtures and the documented reprediction baseline using the 2026/27 prompt/context assumptions and current pricing evidence.
- [ ] Record the estimate in `docs/experiments/whole-season-cost-estimates.md` with its assumptions and date.
- [ ] Ensure every planned community workflow passes the exact model configuration.
- [ ] Add a verification test that trace metadata and prediction identity include model, reasoning effort, output cap, prompt version, and competition.

## Validation

- Run the relevant matchday/bonus telemetry and prediction identity tests.
- Run the project cost-estimation workflow using the repository's `whole-season-estimates` and `estimate-experiment-cost-skill` instructions.

## Complete when

- A reviewer can reproduce the estimate and identify the exact launch configuration from tracked files.
- No production workflow depends on a floating model or reasoning default.

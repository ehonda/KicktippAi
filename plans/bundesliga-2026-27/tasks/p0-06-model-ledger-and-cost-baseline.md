# P0-06 — Pin the model ledger and launch cost baseline

- Status: Not started
- Priority: P0
- Depends on: [P0-05](p0-05-prompt-route.md) (complete; owner-approved production prompt versions and hashes recorded below)
- Decisions: [ADR-0004](../decisions/0004-hosted-prompts-with-local-fallback.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md)

## Outcome

The cheap plumbing configuration and the later owner-approved launch configuration each have a recorded model, reasoning level, output cap, prompt version, and appropriate cost evidence.

## Work items

- [ ] Pin `gpt-5.6-luna` with `none` reasoning and a safe explicit output cap as the development/arena plumbing identity; prevent it from becoming a production default.
- [ ] Prepare reproducible experiment and whole-season cost evidence for the late owner decision, accounting for missing new-season outcomes and possible training contamination in older-season evaluation.
- [ ] Pause production onboarding for the owner to select the final model, reasoning effort, maximum output tokens, prompts, arena challengers, fallback behavior, and cost ceiling; record the approved values in an ADR.
- [ ] Add the exact configuration to the repository's model/onboarding ledger rather than relying on command defaults.
- [ ] Estimate 306 fixtures and the documented reprediction baseline using the 2026/27 prompt/context assumptions and current pricing evidence.
- [ ] Record the estimate in `docs/experiments/whole-season-cost-estimates.md` with its assumptions and date.
- [ ] Ensure every planned community workflow passes its exact ledger configuration rather than a command default.
- [ ] Add a verification test that trace metadata and prediction identity include model, reasoning effort, output cap, prompt version, and competition.

## Validation

- Run the relevant matchday/bonus telemetry and prediction identity tests.
- Run the project cost-estimation workflow using the repository's `whole-season-estimates` and `estimate-experiment-cost-skill` instructions.

## P0-05 prompt identity input

- Match: `kicktippai/bundesliga-2026-27/predict-one-match` version 2, normalized SHA-256 `94a7aa775546028d3ded89f626873d7dfce162d1f08bb9573e102dd427ac08c1`.
- Bonus: `kicktippai/bundesliga-2026-27/predict-bonus` version 1, normalized SHA-256 `332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`.
- The owner approved production promotion on 2026-08-21; `staging`, `production`, and automatic `latest` resolve those versions. P0-06 must pin the numbered versions in the ledger rather than the floating labels.

## Complete when

- A reviewer can reproduce the estimate and identify the exact test and launch configurations from tracked files.
- No production workflow depends on a floating model or reasoning default.
- The Luna/none test identity cannot be silently promoted to production.

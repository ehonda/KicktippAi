# P0-06 — Pin the model ledger and launch cost baseline

- Status: Not started
- Priority: P0
- Depends on: [P0-05](p0-05-prompt-route.md) candidate implementation and stable hashes; its final production-label promotion shares this task's owner gate
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

## Complete when

- A reviewer can reproduce the estimate and identify the exact test and launch configurations from tracked files.
- No production workflow depends on a floating model or reasoning default.
- The Luna/none test identity cannot be silently promoted to production.

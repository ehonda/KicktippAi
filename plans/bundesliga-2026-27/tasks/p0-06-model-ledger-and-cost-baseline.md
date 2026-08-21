# P0-06 — Pin the model ledger and launch cost baseline

- Status: In progress — no-spend implementation complete; paid Luna base evidence and final production owner decision pending
- Priority: P0
- Depends on: [P0-05](p0-05-prompt-route.md) (complete; owner-approved production prompt versions and hashes recorded below)
- Decisions: [ADR-0004](../decisions/0004-hosted-prompts-with-local-fallback.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0033](../decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md)

## Outcome

The cheap plumbing configuration and the later owner-approved launch configuration each have a recorded model, reasoning level, output cap, prompt version, and appropriate cost evidence.

## Work items

- [x] Pin `gpt-5.6-luna` with `none` reasoning and a safe explicit output cap as the development/arena plumbing identity; prevent it from becoming a production default.
- [x] Prepare the reproducible no-spend experiment and whole-season cost procedure for the late owner decision, accounting for the model cutoff and the lack of a matching paid base row.
- [ ] Pause production onboarding for the owner to select the final model, reasoning effort, maximum output tokens, prompts, arena challengers, fallback behavior, and cost ceiling; record the approved values in an ADR.
- [x] Add the exact configuration to the repository's model/onboarding ledger rather than relying on command defaults.
- [ ] Estimate 306 fixtures and the documented 493-call reprediction baseline using the 2026/27 prompt/context assumptions and a paid Luna/none base row. The prescribed estimator currently fails closed because that row does not exist.
- [x] Record the no-spend estimator result, assumptions, official prices, and date in `docs/experiments/whole-season-cost-estimates.md`.
- [x] Specify that every planned community workflow passes its exact ledger configuration rather than a command default; P0-19 still owns workflow implementation.
- [x] Add verification tests that trace metadata and prediction identity include model, reasoning effort, output cap, prompt version, and competition.

## Validation

- Run the relevant matchday/bonus telemetry and prediction identity tests.
- Run the project cost-estimation workflow using the repository's `whole-season-estimates` and `estimate-experiment-cost-skill` instructions.

## P0-05 prompt identity input

- Match: `kicktippai/bundesliga-2026-27/predict-one-match` version 2, normalized SHA-256 `94a7aa775546028d3ded89f626873d7dfce162d1f08bb9573e102dd427ac08c1`.
- Bonus: `kicktippai/bundesliga-2026-27/predict-bonus` version 1, normalized SHA-256 `332bac6d654871d843fc8a47345ff3e2b1f902fa8d1d2243166283304bb005e9`.
- The owner approved production promotion on 2026-08-21; `staging`, `production`, and automatic `latest` resolve those versions. P0-06 must pin the numbered versions in the ledger rather than the floating labels.

## Evidence — 2026-08-21

- Validation ledger: `docs/onboarding-bundesliga-2026-27/model-config-onboarding.md`.
- Runtime identity includes competition, model, reasoning, cap, and exact prompt name/version; legacy incomplete identities do not match pinned configurations.
- Default Bundesliga production routes auto-pin match v2 and bonus v1, explicit candidate labels remain label-resolved, and explicit numeric versions take precedence.
- Official Luna cutoff: `2026-02-16`; prescribed sampling cutoff: `2026-02-18T00:00:00 Europe/Berlin (+01)`.
- No-spend estimator command: `estimate --counts 306,493 --model gpt-5.6-luna --reasoning-effort none`; exact result: `No matching base estimate JSON row found for model='gpt-5.6-luna', reasoningEffort='none'.`
- One-item paid preflight is prepared but was not run without separate approval. The 20-item base run requires a second confirmation based on the observed one-item cost.
- Focused tests: Core identity 4/4, Orchestrator routing/provider 49/49, OpenAI cost/telemetry/runtime identity 50/50, Firebase exact identity 8/8, matchday/bonus/verify commands 112/112, and reconstruction/export/cost exact selectors 21/21.
- Full affected suites: Core 142/142, OpenAI integration 218/218, Firebase adapter 261/261, Orchestrator 916/916, Integration 3/3. One timing-sensitive Langfuse retry test failed during an earlier contended run (three requests observed instead of two), then passed 1/1 in isolation and in the clean 916/916 rerun.

## Complete when

- A reviewer can reproduce the estimate and identify the exact test and launch configurations from tracked files.
- No production workflow depends on a floating model or reasoning default.
- The Luna/none test identity cannot be silently promoted to production.

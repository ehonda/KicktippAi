# P0-06 — Pin the model ledger and launch cost baseline

- Status: In progress — Luna cost baseline complete; final production owner decision pending
- Priority: P0
- Depends on: [P0-05](p0-05-prompt-route.md) (complete; owner-approved production prompt versions and hashes recorded below)
- Decisions: [ADR-0004](../decisions/0004-hosted-prompts-with-local-fallback.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0033](../decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0040](../decisions/0040-use-hash-bound-2025-26-context-for-preseason-cost-experiments.md), [ADR-0043](../decisions/0043-freeze-historical-experiment-aliases-and-eligible-pool.md), [ADR-0046](../decisions/0046-bind-cost-usage-to-langfuse-dataset-runs.md)

## Outcome

The cheap plumbing configuration and the later owner-approved launch configuration each have a recorded model, reasoning level, output cap, prompt version, and appropriate cost evidence.

## Work items

- [x] Pin `gpt-5.6-luna` with `none` reasoning and a safe explicit output cap as the development/arena plumbing identity; prevent it from becoming a production default.
- [x] Prepare the reproducible no-spend experiment and whole-season cost procedure for the late owner decision, accounting for the model cutoff and the lack of a matching paid base row.
- [x] Add the hash-bound, read-only Bundesliga 2025/26 compatibility route required to prepare the preseason cost sample without relaxing live 2026/27 validation or mutating historical Firestore rows.
- [x] Freeze the producer-era 18-team alias catalog and sample once from the complete context-eligible historical pool rather than selecting fixtures before reconstruction coverage is known.
- [ ] Pause production onboarding for the owner to select the final model, reasoning effort, maximum output tokens, prompts, arena challengers, fallback behavior, and cost ceiling; record the approved values in an ADR.
- [x] Add the exact configuration to the repository's model/onboarding ledger rather than relying on command defaults.
- [x] Estimate 306 fixtures and the documented 493-call reprediction baseline from the paid Luna/none preseason seven-document proxy row, explicitly retaining its possible understatement versus the live eleven-document 2026/27 context.
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

## Evidence — 2026-08-21 and 2026-08-25

- Validation ledger: `docs/onboarding-bundesliga-2026-27/model-config-onboarding.md`.
- Runtime identity includes competition, model, reasoning, cap, and exact prompt name/version; legacy incomplete identities do not match pinned configurations.
- Default Bundesliga production routes auto-pin match v2 and bonus v1, explicit candidate labels remain label-resolved, and explicit numeric versions take precedence.
- Official Luna cutoff: `2026-02-16`; prescribed sampling cutoff: `2026-02-18T00:00:00 Europe/Berlin (+01)`.
- The initial no-spend estimator command `estimate --counts 306,493 --model gpt-5.6-luna --reasoning-effort none` failed closed with `No matching base estimate JSON row found for model='gpt-5.6-luna', reasoningEffort='none'.` The exact paid row below now satisfies that lookup.
- The owner explicitly authorized the paid one-item Luna preflight and subsequent 20-item base run under the estimate-row process, with no second approval pause unless the observed cap or cost materially departed from the prescribed lane. Both stages completed; the final recovery made no model call and created no additional spend.
- The authorized one-item run completed as `repeated-match-slice__pes-squad__gpt-5.6-luna__match-v2__reasoning-none__random-1x1-seed-20260821__cost-preflight__startsat-12h__2026-08-25t04-40-57z` (dataset `cmt86fx6o0aeuad0dg99ivamv`, dataset run `80e17c90-631d-4c89-8640-21fe36fef541`): 2,463 uncached input tokens, 17 output tokens, zero reasoning tokens, flex tier, no fallback, and observed Langfuse cost `$0.0002565`; output used 0.17% of the 10,000-token cap.
- The five-by-four dataset is `cmt86m8gn0awvad0eyx7mn5f6`. Its parallelism-5 attempt completed 20 items but included one flex-429 standard fallback, so it is retained only as retry context. The prescribed parallelism-3 replacement reused the exact manifest/settings/shared-stamp run name and completed 20/20 entirely on flex, without a warning or fallback. The accepted dataset-run ID is `6a3c4e70-ebb4-4c07-9a1a-7af19c32d995`.
- Run-name-only compact collection first failed closed at `40/20`: Langfuse retained both attempts' traces even though `--replace-run` replaced the dataset-run object. After ADR-0046 integrated, exact collection bound dataset ID `cmt86m8gn0awvad0eyx7mn5f6`, the accepted dataset-run ID, prepared-manifest SHA-256 `fcadeeadaadd1356472a1f4f96b7277a05ba3b8a19dcde60e6f2e7d79af577b7`, sample size `20`, and exactly 20 distinct item/trace links. It did not truncate, use a time window, or rerun a prediction.
- The authoritative Luna/none row records 20 flex observations, zero fallback/non-flex/retry requests, `48752` total input tokens, `48692` observed cached-input tokens, `340` output tokens, zero reasoning tokens, maximum per-item output `17 / 10000`, observed Langfuse cost `$0.000696920000`, and an all-input-uncached flex estimate of `$0.005079200000` total / `$0.000253960000` average. Exact estimator stdout is `N=306: $0.077711760000` and `N=493: $0.125202280000`.
- ADR-0040 records the accepted historical compatibility contract. Marked manifests bind the exact 2025/26 seven-document legacy-ID context, completed scores, prompt v2/production route, cutoff dates, and `startsAt -12h`; the runner exact-rereads and re-hashes every distinct fixture before any Langfuse mutation or model construction.
- ADR-0043 isolates the exact producer-era aliases from the live catalog. A read-only audit found all 109 completed post-cutoff fixtures context-eligible (763/763 document references); eligible source-ID hash `6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415`. Preparation binds the eligibility policy, count, and hash before applying seed `20260821` once; it never retries a seed to bypass missing context.
- ADR-0043 implementation validation: solution build completed with 0 errors; focused historical Core 24/24, preparation/runner 38/38, and Firebase emulator 6/6; full Core 277/277, Firebase adapter 289/289, and Integration 4/4; repository estimate skill validation passed. Two serial full-Orchestrator runs each passed 1,070/1,071, with only the unrelated retry-sensitive `Running_run_community_to_date_creates_one_dataset_run_per_participant` activity-count assertion failing (3 observed versus 2 expected); that test passed 1/1 in isolation. No prompt, dataset, Langfuse, or model mutation was made.
- Independent-review provenance remediation forces historical selected-item count/hash from the validated manifest into normalized, propagated, and Langfuse-serialized metadata, and directly tests that a fixture exactly at the Berlin sampling cutoff is excluded while the first whole-second-representable instant after it is included. Focused preparation/runner validation passed 41/41, including an explicit regression preserving non-historical metadata override behavior. Two serial full-Orchestrator runs each passed 1,076/1,077; both reproduced only the unrelated process-global duplicate-activity failure in `Running_run_slice_reconstructs_predicts_and_posts_scores`, which passed 1/1 in isolation. No external operation was made.
- The resulting Luna row is labeled as a preseason seven-document cost proxy that may understate the live 2026/27 eleven-document input. It makes no prediction-quality claim. Full compact evidence is [tracked with the estimate skill](../../../.agents/skills/estimate-experiment-cost-skill/references/gpt-5.6-luna-none-base-estimate-2026-08-25.md).
- Historical compatibility checkpoint validation: full solution build (0 errors); Core 245/245, Firebase adapter 283/283 with the Docker emulator, Orchestrator 1,046/1,046, and Integration 4/4; repository estimate skill validation passed.
- Independent-review remediation machine-enforces the exact 1-by-1 or 5-by-4 topology, generated source/slice identities, selected-item hash, top-level artifact provenance, and the exact two-day Europe/Berlin local-midnight cutoff; it also verifies the resolved hosted prompt's name, numbered version, and `production` label before any run replacement or model construction. Validation: solution build (0 errors), focused preparation 4/4, focused runner 31/31, full Orchestrator 1,053/1,053, and repository estimate skill validation passed. No prompt, Langfuse mutation, or model call was made during remediation.
- Focused tests: Core identity 4/4, Orchestrator routing/provider 49/49, OpenAI cost/telemetry/runtime identity 50/50, Firebase exact identity 8/8, matchday/bonus/verify commands 112/112, and reconstruction/export/cost exact selectors 21/21.
- Full affected suites: Core 142/142, OpenAI integration 218/218, Firebase adapter 261/261, Orchestrator 916/916, Integration 3/3. One timing-sensitive Langfuse retry test failed during an earlier contended run (three requests observed instead of two), then passed 1/1 in isolation and in the clean 916/916 rerun.

## Complete when

- A reviewer can reproduce the estimate and identify the exact test and launch configurations from tracked files.
- No production workflow depends on a floating model or reasoning default.
- The Luna/none test identity cannot be silently promoted to production.

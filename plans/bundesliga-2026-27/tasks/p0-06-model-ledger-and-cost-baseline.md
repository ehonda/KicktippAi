# P0-06 — Pin the model ledger and launch cost baseline

- Status: Complete — ADR-0052 records the Owner-selected production identity, arena matrix, planning ceiling, and match-prompt successor
- Priority: P0
- Depends on: [P0-05](p0-05-prompt-route.md) (complete; owner-approved production prompt versions and hashes recorded below), [P0-23](p0-23-gpt-5-6-production-candidate-evidence.md)
- Decisions: [ADR-0004](../decisions/0004-hosted-prompts-with-local-fallback.md), [ADR-0006](../decisions/0006-stage-validation-with-a-cheap-test-model.md), [ADR-0033](../decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md), [ADR-0039](../decisions/0039-record-bundesliga-community-and-credential-topology.md), [ADR-0040](../decisions/0040-use-hash-bound-2025-26-context-for-preseason-cost-experiments.md), [ADR-0043](../decisions/0043-freeze-historical-experiment-aliases-and-eligible-pool.md), [ADR-0046](../decisions/0046-bind-cost-usage-to-langfuse-dataset-runs.md), [ADR-0052](../decisions/0052-select-production-model-community-matrix-and-match-prompt-v3.md)

## Outcome

The cheap plumbing configuration and the later owner-approved launch configuration each have a recorded model, reasoning level, output cap, prompt version, and appropriate cost evidence.

## Work items

- [x] Pin `gpt-5.6-luna` with `none` reasoning and a safe explicit output cap as the development/arena plumbing identity; prevent it from becoming a production default.
- [x] Prepare the reproducible no-spend experiment and whole-season cost procedure for the late owner decision, accounting for the model cutoff and the lack of a matching paid base row.
- [x] Add the hash-bound, read-only Bundesliga 2025/26 compatibility route required to prepare the preseason cost sample without relaxing live 2026/27 validation or mutating historical Firestore rows.
- [x] Freeze the producer-era 18-team alias catalog and sample once from the complete context-eligible historical pool rather than selecting fixtures before reconstruction coverage is known.
- [x] After P0-23 supplied comparative cost/quality evidence, pause production onboarding for the Owner to select the final model, reasoning effort, maximum output tokens, accepted prompt versions, arena challengers, fallback behavior, and planning ceiling; record the approved values in ADR-0052.
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

The lines above are the immutable v2 experiment input. ADR-0052 selects the
live successor match version 3, normalized SHA-256
`7c223c0765024e52b542bbdb8093ab9b8fcaad505de0c5f8d6c92f4044e175f3`,
with bonus version 1 unchanged. Historical P0-23 manifests stay pinned to v2.

## Owner-selected launch ledger — 2026-08-27

| Role | Model | Reasoning | Maximum output tokens | Match / bonus prompt |
| --- | --- | --- | ---: | --- |
| `production-primary` | `gpt-5.6-sol` | `xhigh` | 10000 | v3 / v1 |
| Arena challenger | `gpt-5.6-sol` | `high` | 10000 | v3 / v1 |
| Arena challenger | `gpt-5.6-luna` | `medium` | 10000 | v3 / v1 |
| Arena challenger | `gpt-5.6-terra` | `xhigh` | 10000 | v3 / v1 |
| Arena challenger/validation | `gpt-5.6-luna` | `none` | 10000 | v3 / v1 |

Every generation row uses Flex first with Standard fallback. The USD 35
whole-season total is an orientation ceiling, not a runtime enforcement gate.
At the evidence-derived 493-call baseline, two independent production streams
plus the four challenger streams project to USD `14.094805910000`; secondary
copies make no additional compatible-path model call. ADR-0052 records the
exact evidence, topology, and Owner reasoning.

The later exploratory/post-hoc Sol/`max` follow-up at exact lane commit
`f7dd2aee6c35fec26a5f09df0f1a68d82495f01b` corroborates but does not
retroactively preregister the decision: average `27.6` versus Sol/`xhigh`
`27.8`, paired xhigh-minus-max `+0.2`, 95% bootstrap CI `[-1.2, 1.6]`,
Holm-adjusted `p = 0.8918`, and a 493-call estimate of `$7.903381600000`.
P0-06 had already settled independently.

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

## Historical P0-23 comparative-evidence handoff — 2026-08-26

This section preserves the checkpoint before the Owner selection recorded in
the launch ledger above.

- [P0-23](p0-23-gpt-5-6-production-candidate-evidence.md) is complete. Its
  [quality-results report](../../../docs/experiments/gpt-5-6-bundesliga-2025-26-production-candidate-quality-results.md)
  and
  [cost-results report](../../../docs/experiments/gpt-5-6-bundesliga-2025-26-production-candidate-cost-results.md)
  provided the evidence for the now-complete Owner selection item above.
- Eight original configurations completed the frozen paired quality sample.
  Luna/`max` remains incomplete after two transient capacity failures and the
  Owner's explicit p1-stop override; it has no imputed score or rank.
- Sol/`xhigh` was added only after the original eight accepted scores were
  visible. Its cost row and quality run completed, but its comparison is
  exploratory and data-dependent rather than preregistered confirmatory
  evidence.
- The nine accepted runs share 200 exact items and yield 20 paired
  repetition-total observations. The descriptive order is Sol/`xhigh`,
  Sol/`high`, Sol/`medium`, Luna/`medium`, Sol/`none`, Terra/`xhigh`,
  Terra/`medium`, Terra/`none`, Luna/`none`; the report contains exact
  uncertainty and corrected pairwise results, so order alone must not be used
  as an automatic selection rule.
- Final P0-23 observed plus reserved exposure is `$4.807937270000` under the
  authorized USD 30 ceiling. Exact 306/493 season estimates are recorded next
  to quality, including post-hoc Sol/`xhigh` at `$2.609782200000` /
  `$4.204649100000` and Luna/`medium` at `$0.105573060000` /
  `$0.170089930000`.
- No production model, cap, fallback policy, participant topology, community
  prediction, workflow, or schedule changed in the P0-23 evidence lane. The
  later Owner selection and ADR-0052 closed that P0-06 decision gate.

## Sol/`max` cost-only follow-up — 2026-08-26

- The Owner authorized one Sol/`max` cost preflight and, if admitted, one exact
  5-by-4 calibration under a new strict USD 5 sub-ledger and the existing USD
  30 P0 ceiling. The initial 60-minute deadline was explicitly extended to 120
  minutes without resetting its `2026-08-26T20:29:20.3751477Z` start. This
  follow-up collected cost evidence only; it did not run a quality experiment
  or change any model-selection decision.
- Both stages reused the already-audited hosted Luna/`none` cost slices without
  resynchronizing them. The one-item dataset is
  `cmt86fx6o0aeuad0dg99ivamv` (dataset SHA-256
  `389b806e89b08169ea0092667d7fc774f0737c6e235e44b4fbf18c81c412c717`),
  and the 20-item dataset is `cmt86m8gn0awvad0eyx7mn5f6` (dataset SHA-256
  `0fbc3e07f926596805a23bbe3241fcf2ec368858f217cb1e05ccbac96c907d18`).
  Both bind the same 109-fixture eligible-pool hash recorded above.
- The 20,000-token-cap preflight completed on Flex with 2,463 input tokens,
  9,893 output tokens, 9,874 reasoning tokens, and observed cost
  `$0.103856000000`. Exact dataset-run collection bound 1/1 item before the
  calibration gate was evaluated; the cap was not escalated.
- The parallelism-5 calibration completed without a retry and collected 20/20
  exact-bound items from dataset run
  `0205df36-af15-47ab-9f7e-4caf844932a3`. It used 48,752 input tokens and
  22,312 output tokens, of which 21,932 were reasoning tokens; 19 requests
  remained on Flex and one used standard fallback. Maximum per-item output was
  6,751 of 20,000. Accepted collection completed after 921.701252 seconds on
  the original clock, inside the extended deadline.
- Observed calibration cost was `$0.265793800000`. The authoritative base row
  normalizes all 48,752 input tokens as uncached Flex input, yielding
  `$0.320624000000` total and `$0.016031200000` per prediction. This normalized
  planning estimate is intentionally higher than the observed mixed
  cached/tier bill; it is not additional spend.
- Exact normalized estimates are `$0.320624000000` for 20 predictions,
  `$1.603120000000` for 100, `$2.404680000000` for 150,
  `$3.206240000000` for 200, `$4.905547200000` for 306, and
  `$7.903381600000` for 493. The
  [tracked compact evidence](../../../.agents/skills/estimate-experiment-cost-skill/references/gpt-5.6-sol-max-base-estimate-2026-08-26.md)
  records the exact run identity, provenance, usage hash, and estimator output.
- Actual incremental settled exposure was `$0.369649800000` across the two new
  experiment attempts, with no new reservation. Actual global exposure was
  `$5.177587070000`: `$5.077987070000` across 33 settled attempts plus the
  three pre-existing reservations totaling `$0.099600000000`. The final
  machine gates displayed `$0.385681000000` and `$5.193618270000` because the
  gate requires a candidate and therefore included one authoritative-row
  `$0.016031200000` sentinel. That sentinel is conservative one-more-call
  headroom, not spent, in flight, or reserved.
- At this checkpoint Sol/`max` had reproducible season-cost evidence but no
  quality result or recommendation. The later separately authorized post-hoc
  quality extension is recorded in the launch-ledger section above and the
  canonical quality-results report; it did not reopen the completed Owner
  production-model decision.

## Complete when

- P0-23 supplies the owner-required comparative evidence, or the owner records
  an explicit evidence waiver and accepted risk, and the owner-selected final
  production model, reasoning effort, maximum output tokens, numbered prompt
  versions, arena challenger matrix, service-tier/fallback policy, cost ceiling,
  and estimator evidence are recorded in the model ledger and a new Accepted
  ADR.
- A reviewer can reproduce the estimate and identify the exact test and launch configurations from tracked files.
- No production workflow depends on a floating model or reasoning default.
- The Luna/none test identity cannot be silently promoted to production.

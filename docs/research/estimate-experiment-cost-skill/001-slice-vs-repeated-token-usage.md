# 001 - Slice vs Repeated-Match Token Usage

Status: Completed

Executed on: 2026-05-01

Repository commit: `b4bdff4 Enable Dependabot and central package management`

## Research Question

Does the average total input and output token count for a given model configuration differ between experiments on slices and experiments on repeated matches?

Sub Experiment A fixed the model configuration to `o3` with `medium` reasoning effort and used `N = 10` measured observations for each dataset type.

## Methodology

Dataset construction:

- Slice dataset: 10 randomly selected `pes-squad` completed matches after `2025-12-01T00:00:00 Europe/Berlin (+01)`, seed `20260501`.
- Repeated-match fixture: selected through a one-item random slice from the same cutoff pool and seed because `prepare-repeated-match` does not support random selection directly.
- Repeated-match dataset: 11 materialized repetitions of the selected fixture, with item `__01` treated as warmup and excluded from the measured repeated-match statistics.

Random repeated fixture:

- `Werder Bremen vs RB Leipzig`, matchday 28
- Starts at `2026-04-04T16:30:00 UTC+02 (+02)`
- Source item id `bundesliga-2025-26__pes-squad__ts1423757288`
- Actual result `1:2`

Prepared artifacts:

- Slice manifest: `artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-10-seed-20260501/slice-manifest.json`
- Repeated fixture selector manifest: `artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-1-seed-20260501-repeated-fixture/slice-manifest.json`
- Repeated-match manifest: `artifacts/langfuse-experiments/repeated-match/pes-squad/md28-werder-bremen-vs-rb-leipzig/repeat-11-measured-10-seed-20260501/slice-manifest.json`

Langfuse datasets:

- `match-predictions/bundesliga-2025-26/pes-squad/slices/all-matchdays-after-20251130t230000z/random-10-seed-20260501`
- `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md28-werder-bremen-vs-rb-leipzig/repeat-11-measured-10-seed-20260501`

Run names:

- `slice__pes-squad__o3__prompt-v1__reasoning-medium__random-10-seed-20260501__startsat-12h__2026-05-01t21-29-33z`
- `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-11-measured-10-seed-20260501__startsat-12h__2026-05-01t21-29-33z`

Commands:

```powershell
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --sample-size 10 --sample-seed 20260501 --starts-after "2025-12-01T00:00:00 Europe/Berlin (+01)"
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --sample-size 1 --sample-seed 20260501 --starts-after "2025-12-01T00:00:00 Europe/Berlin (+01)" --slice-key random-1-seed-20260501-repeated-fixture
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "Werder Bremen" --away "RB Leipzig" --matchday 28 --sample-size 11 --slice-key repeat-11-measured-10-seed-20260501 --dataset-description "Fixture selected by random one-item slice after 2025-12-01 with seed 20260501 for estimate experiment cost skill sub-experiment A."
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-10-seed-20260501/slice-dataset.json
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/repeated-match/pes-squad/md28-werder-bremen-vs-rb-leipzig/repeat-11-measured-10-seed-20260501/slice-dataset.json
dotnet run --project src/Orchestrator -- run-slice o3 --manifest artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-10-seed-20260501/slice-manifest.json --run-name "slice__pes-squad__o3__prompt-v1__reasoning-medium__random-10-seed-20260501__startsat-12h__2026-05-01t21-29-33z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-size 10 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md28-werder-bremen-vs-rb-leipzig/repeat-11-measured-10-seed-20260501/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-11-measured-10-seed-20260501__startsat-12h__2026-05-01t21-29-33z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
```

Usage was gathered from Langfuse trace details through the installed `langfuse` CLI. The relevant observation was `predict-match`; fields used were `usageDetails`, `costDetails`, observation metadata, trace input, and trace metadata.

Statistical test:

- Two-sided exact permutation test over difference in means.
- `N = 10` slice observations vs `N = 10` repeated-match observations after excluding warmup item `__01`.
- There are `184,756` label permutations for each metric.
- Bootstrap confidence intervals used 30,000 seeded resamples with seed `20260501`.

## Outcome

All planned executions completed successfully.

Run summaries:

| Run | Executions | Batch shape | Aggregate score |
| --- | ---: | --- | ---: |
| Slice | 10 | one batch of 10 | total `15`, avg `1.5` |
| Repeated match | 11 | warmup 1, then one batch of 10 | total `42`, avg `3.8181818181818183` |

Usage summary:

| Group | n | Mean input | Mean cached input | Mean uncached input | Mean output | Mean reasoning | Mean total tokens | Total cost |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Slice measured | 10 | 3351.4 | 0.0 | 3351.4 | 2067.2 | 2009.6 | 5418.6 | $0.116202 |
| Repeated warmup | 1 | 3269.0 | 0.0 | 3269.0 | 941.0 | 896.0 | 4210.0 | $0.007033 |
| Repeated measured | 10 | 3269.0 | 1561.6 | 1707.4 | 1591.2 | 1536.0 | 4860.2 | $0.084626 |

Primary comparison:

| Metric | Slice mean | Repeated measured mean | Difference, slice - repeated | Permutation p | Bootstrap 95% CI |
| --- | ---: | ---: | ---: | ---: | --- |
| Input tokens | 3351.4 | 3269.0 | 82.4 | 0.000119 | [42.8, 124.3] |
| Output tokens | 2067.2 | 1591.2 | 476.0 | 0.416290 | [-430.5, 1479.3] |
| Total tokens | 5418.6 | 4860.2 | 558.4 | 0.348893 | [-346.4, 1566.8] |

Supporting cache/cost comparison:

| Metric | Slice mean | Repeated measured mean | Difference, slice - repeated | Permutation p | Bootstrap 95% CI |
| --- | ---: | ---: | ---: | ---: | --- |
| Cached input tokens | 0.0 | 1561.6 | -1561.6 | 0.032508 | [-2521.6, -601.6] |
| Uncached input tokens | 3351.4 | 1707.4 | 1644.0 | 0.000065 | [694.8, 2602.6] |
| Total cost | $0.011620 | $0.008463 | $0.003158 | 0.177185 | [-$0.000730, $0.007326] |

Answer for Sub Experiment A:

- The measured repeated-match sample did **not** show a statistically significant difference in output tokens or total tokens compared with the slice sample.
- Total input tokens did differ significantly in this run, but that result is fixture-confounded. The repeated-match fixture was also present in the slice, and its slice input-token count was exactly `3269`, the same as every repeated-match observation for that fixture. The slice average was higher because other randomly selected fixtures had larger prompt/context inputs.
- Effective uncached input tokens differed strongly because repeated-match calls received cache hits in 5 of the 10 measured observations. This supports the motivation that repeated-match datasets can be cheaper, but it does not by itself prove they estimate ordinary slice input-token usage well.
- The total-cost difference favored repeated-match runs in this sample, but it was not significant at `N = 10` because output/reasoning token variance was large.

Implication for the future estimator:

Repeated-match runs are useful for observing output-token and cache behavior for a model configuration, but a single repeated fixture should not be used as the only estimate for slice input-token usage. Input tokens are dominated by fixture-specific prompt context. If repeated-match datasets are used as a cheap proxy, the workflow needs either several randomly selected repeated fixtures or a representativeness check against reconstructed prompt/input lengths.

## Further Research Directions

- Wait for the user to choose the next model/reasoning-effort sub experiment.
- To separate fixture effects from dataset-type effects, run repeated-match samples for several randomly selected fixtures and compare the fixture-level input lengths to the slice distribution.
- Extend the Orchestrator export or report tooling to include normalized usage details so future research does not need one trace-detail API call per item.
- Track cache-hit position and batch shape explicitly; in this run, only 5 of the 10 post-warmup repeated observations had non-zero cached input tokens.

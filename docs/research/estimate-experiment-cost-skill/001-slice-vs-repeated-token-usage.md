# 001 - Slice vs Single Repeated-Match Token Usage

Status: Completed

Executed on: 2026-05-01

Repository commit: `b4bdff4 Enable Dependabot and central package management`

## Research Question

Does the average total input and output token count for `o3` with `medium` reasoning effort differ between a random slice and repeated predictions for one randomly selected fixture?

## Methodology

This experiment uses the shared token-usage analysis method documented in [token-usage-methodology.md](token-usage-methodology.md): compact Langfuse usage extraction from `predict-match`, two-sided exact permutation tests over difference in means, and seeded bootstrap confidence intervals.

Configuration:

- Community context: `pes-squad`
- Source season: Bundesliga 2025-26
- Match cutoff: all randomly selected items start after `2025-12-01T00:00:00 Europe/Berlin (+01)`
- Model: `o3`
- Reasoning effort: `medium`
- Prompt: local `prompt-v1`
- Evaluation policy: relative `startsat-12h`
- Slice sample size: `N = 10`, seed `20260501`
- Repeated-match measured sample size: `N = 10`

Dataset construction:

- Slice dataset: 10 randomly selected completed matches after the cutoff.
- Repeated-match fixture: selected through a one-item random slice from the same cutoff pool and seed because `prepare-repeated-match` did not directly support random fixture selection.
- Repeated-match dataset: 11 materialized repetitions of the selected fixture, with item `__01` treated as warmup and excluded from the measured repeated-match statistics.

Workflow resolution after review: the extra warmup item was unnecessary for this research question because only total input and total output token counts are in scope. Future total-token comparisons should prepare exactly `N` repeated-match items unless cached input, uncached input, or monetary cost is explicitly part of the question.

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
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "Werder Bremen" --away "RB Leipzig" --matchday 28 --sample-size 11 --slice-key repeat-11-measured-10-seed-20260501 --dataset-description "Fixture selected by random one-item slice after 2025-12-01 with seed 20260501 for estimate experiment cost skill experiment 001."
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-10-seed-20260501/slice-dataset.json
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/repeated-match/pes-squad/md28-werder-bremen-vs-rb-leipzig/repeat-11-measured-10-seed-20260501/slice-dataset.json
dotnet run --project src/Orchestrator -- run-slice o3 --manifest artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-10-seed-20260501/slice-manifest.json --run-name "slice__pes-squad__o3__prompt-v1__reasoning-medium__random-10-seed-20260501__startsat-12h__2026-05-01t21-29-33z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-size 10 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md28-werder-bremen-vs-rb-leipzig/repeat-11-measured-10-seed-20260501/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-11-measured-10-seed-20260501__startsat-12h__2026-05-01t21-29-33z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
```

Reproducibility command:

```powershell
uv run python docs/research/estimate-experiment-cost-skill/analyze_token_usage.py --input C:\tmp\kicktippai-subexp-a-usage.json --left-group slice-measured --right-group repeated-measured --metrics inputTokens outputTokens totalTokens --seed 20260501 --report-json C:\tmp\kicktippai-subexp-a-analysis-check.json
```

## Outcome

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
| Input tokens | 3351.4 | 3269.0 | 82.4 | 0.000119 | [43.1, 123.5] |
| Output tokens | 2067.2 | 1591.2 | 476.0 | 0.416290 | [-431.6, 1487.5] |
| Total tokens | 5418.6 | 4860.2 | 558.4 | 0.348893 | [-368.9, 1590.0] |

Answer:

- The measured repeated-match sample did not show a statistically significant difference in output tokens or total tokens compared with the slice sample.
- Total input tokens did differ significantly, but this result was fixture-confounded. The repeated-match fixture was also present in the slice, and its slice input-token count was exactly `3269`, the same as every repeated-match observation for that fixture.
- The output-token mean difference was numerically larger than the input-token mean difference, but it was not significant because output tokens had much higher variance and broad overlap between groups.
- Effective uncached input tokens differed strongly because repeated-match calls received cache hits in 5 of the 10 measured observations. This supports the cost motivation, but cached input is separate from the total-token question.

## Further Research Directions

- Run the next experiment with several randomly selected repeated fixtures to reduce single-fixture input-context confounding.
- Use exactly `N` repeated-match items for future total-token comparisons unless the user explicitly asks to study cached-input or monetary-cost behavior.

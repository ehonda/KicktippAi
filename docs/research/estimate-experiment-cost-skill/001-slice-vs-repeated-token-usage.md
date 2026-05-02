# 001 - Slice vs Repeated-Match Token Usage

Status: Completed through Sub Experiment B

Executed on: 2026-05-01 UTC run stamps, documented on 2026-05-02

Repository commits:

- Sub Experiment A was originally run at `b4bdff4 Enable Dependabot and central package management`.
- Sub Experiment B analysis was documented with repository head `6f56db5 Fix line endings`.

## Research Question

Does the average total input and output token count for a given model configuration differ between experiments on slices and experiments on repeated matches?

This experiment fixes the model configuration to `o3` with `medium` reasoning effort and tests the question through user-directed sub experiments.

## Methodology

Shared configuration:

- Community context: `pes-squad`
- Source season: Bundesliga 2025-26
- Match cutoff: all randomly selected items must start after `2025-12-01T00:00:00 Europe/Berlin (+01)`.
- Prompt: local `prompt-v1`
- Model: `o3`
- Reasoning effort: `medium`
- Evaluation policy: relative `startsat-12h`, expressed as `--evaluation-policy-kind relative --evaluation-policy-offset -12:00:00`

Dataset construction rules:

- Slice datasets use `prepare-slice` with `--sample-size`, `--sample-seed`, and `--starts-after`.
- `prepare-repeated-match` does not directly support random fixture selection after a cutoff, so repeated-match fixtures are first selected by preparing a random slice from the same cutoff pool. The selected fixture is then used to prepare the repeated-match dataset.
- For total input/output token comparisons, use exactly `N` repeated-match items. Do not create an extra warmup item unless cached input, uncached input, or monetary cost is part of the research question.

Usage collection:

- Usage is gathered from the `predict-match` Langfuse generation observation.
- The reproducible script is [analyze_token_usage.py](analyze_token_usage.py). It writes a compact usage JSON that excludes prompt bodies and model outputs.
- The script now uses the fast Langfuse v2 observations list endpoint by default, filtered by `sessionId`, observation `name = predict-match`, and `type = GENERATION`.
- The previous implementation used `GET /api/public/traces/{traceId}` once per item through the CLI. That was inefficient because each trace-detail call fetched bulky trace content and hit a tight rate-limit bucket. This was especially visible in Sub Experiment B, where 40 items stalled after 6 trace-detail calls.
- This matches the Orchestrator exporter design in `export-experiment-analysis`, which batches observations per run by `sessionId` instead of calling trace detail once per trace. The repository documentation already records that exporter behavior in [docs/langfuse/experiments/analyzing-experiments.md](../../langfuse/experiments/analyzing-experiments.md).

Statistical test:

- Primary test: two-sided exact permutation test over difference in means.
- Test statistic: `mean(slice) - mean(repeated measured)` for each token metric.
- Null hypothesis: if the dataset construction type has no effect on the mean token count, then the observed token values are exchangeable between the two labels.
- Exact procedure: combine the two samples, enumerate every distinct way to assign `n_left` observations to the left group and `n_right` to the right group, compute the difference in means for each relabeling, and count the share where `abs(permuted_difference) >= abs(observed_difference)`.
- For integer token counts, the script uses dynamic programming over subset sums. This is equivalent to enumerating all label partitions, but it avoids materializing very large permutation sets. Sub Experiment B has `C(40, 20) = 137,846,528,820` label partitions.
- Bootstrap confidence intervals use 30,000 seeded percentile resamples. They are descriptive intervals around the observed mean difference; the significance decision comes from the exact permutation p-value.

Why this is the correct test for this experiment:

- The samples are independent, unpaired observations from two dataset construction workflows. We are not comparing the same fixture across two model configurations, so a paired test is not appropriate.
- Token counts are discrete, skew-prone, and can have very different variances for input and output. The permutation test does not require normality or equal variances in the way a two-sample t-test would.
- The question is specifically about a mean token-count difference, so difference in means is the right statistic to permute.
- The test is exact for the observed integer token counts under the exchangeability null. The main caveat is design-level, not test-level: repeated items within the same fixture share prompt context. Sub Experiment B reduces this confound by using five repeated fixtures, but future fixture-generalizable inference should keep increasing the number of random repeated fixtures.

Source material:

- [SciPy `permutation_test` documentation](https://docs.scipy.org/doc/scipy/reference/generated/scipy.stats.permutation_test.html) describes independent-sample permutation tests, exact tests when all distinct partitions are used, and two-sided alternatives.
- [NIST Dataplot Fisher two sample randomization test](https://www.itl.nist.gov/div898/software/dataplot/refman1/auxillar/fishrand.htm) describes equality-of-means randomization tests by all label assignments and p-values from the permutation distribution.
- [NIST Dataplot two sample permutation test](https://www.itl.nist.gov/div898/software/dataplot/refman1/auxillar/permtest.htm) describes combining samples, permuting labels, and comparing the observed statistic to the reference distribution without distributional assumptions.

Question resolved after Sub Experiment A:

- The output-token mean difference was numerically larger than the input-token mean difference, but it was not significant because output tokens had much higher variance and broad overlap between groups.
- The input-token difference was smaller in absolute terms but significant in Sub Experiment A because repeated-match input tokens were nearly constant for a single fixture, while the slice had a consistently higher fixture/context input length. Statistical significance depends on the observed difference relative to the null distribution, not only on the raw difference.

### Sub Experiment A

Dataset construction:

- Slice dataset: 10 randomly selected `pes-squad` completed matches after the cutoff, seed `20260501`.
- Repeated-match fixture: selected through a one-item random slice from the same cutoff pool and seed.
- Repeated-match dataset: 11 materialized repetitions of the selected fixture, with item `__01` treated as warmup and excluded from the measured repeated-match statistics.

Workflow correction after review: because this research question is about total input/output token counts rather than cached-vs-uncached input or monetary cost, future sub experiments should prepare exactly `N` repeated-match items and include all `N` items in the token-count comparison. The extra warmup item in this run was unnecessary for the primary total-token question. It does not affect the total-input conclusion because every repeated-match observation, including the warmup, had `3269` total input tokens.

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

Usage analysis command:

```powershell
uv run python docs/research/estimate-experiment-cost-skill/analyze_token_usage.py --input C:\tmp\kicktippai-subexp-a-usage.json --left-group slice-measured --right-group repeated-measured --metrics inputTokens outputTokens totalTokens --seed 20260501 --report-json C:\tmp\kicktippai-subexp-a-analysis-check.json
```

### Sub Experiment B

Dataset construction:

- Slice dataset: 20 randomly selected `pes-squad` completed matches after the cutoff, seed `20260502`.
- Repeated fixtures: 5 randomly selected fixtures from the same cutoff pool, seed `20260503`.
- Repeated-match datasets: 5 datasets of size 4, giving `M = 5`, `S = 4`, and `N = M * S = 20` repeated-match observations.
- All repeated-match observations are included. Cached input is intentionally out of scope for this sub experiment.

Prepared artifacts:

- Slice manifest: `artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-20-seed-20260502/slice-manifest.json`
- Repeated fixture selector manifest: `artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-5-seed-20260503-repeated-fixtures/slice-manifest.json`
- Repeated-match manifests:
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md19-eintracht-frankfurt-vs-1899-hoffenheim/repeat-4-seed-20260503-fixture-01/slice-manifest.json`
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md21-1-fc-koln-vs-rb-leipzig/repeat-4-seed-20260503-fixture-02/slice-manifest.json`
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md26-borussia-dortmund-vs-fc-augsburg/repeat-4-seed-20260503-fixture-03/slice-manifest.json`
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md26-bayer-04-leverkusen-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-04/slice-manifest.json`
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md31-fsv-mainz-05-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-05/slice-manifest.json`

Langfuse datasets:

- `match-predictions/bundesliga-2025-26/pes-squad/slices/all-matchdays-after-20251130t230000z/random-20-seed-20260502`
- `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md19-eintracht-frankfurt-vs-1899-hoffenheim/repeat-4-seed-20260503-fixture-01`
- `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md21-1-fc-koln-vs-rb-leipzig/repeat-4-seed-20260503-fixture-02`
- `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md26-borussia-dortmund-vs-fc-augsburg/repeat-4-seed-20260503-fixture-03`
- `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md26-bayer-04-leverkusen-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-04`
- `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md31-fsv-mainz-05-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-05`

Repeated fixtures:

| Fixture | Matchday | Starts at | Actual |
| --- | ---: | --- | --- |
| Eintracht Frankfurt vs 1899 Hoffenheim | 19 | `2026-01-24T15:30:00 UTC+01 (+01)` | `1:3` |
| 1. FC Köln vs RB Leipzig | 21 | `2026-02-08T15:30:00 UTC+01 (+01)` | `1:2` |
| Borussia Dortmund vs FC Augsburg | 26 | `2026-03-14T15:30:00 UTC+01 (+01)` | `2:0` |
| Bayer 04 Leverkusen vs FC Bayern München | 26 | `2026-03-14T15:30:00 UTC+01 (+01)` | `1:1` |
| FSV Mainz 05 vs FC Bayern München | 31 | `2026-04-25T16:30:00 UTC+02 (+02)` | `3:4` |

Run names:

- `slice__pes-squad__o3__prompt-v1__reasoning-medium__random-20-seed-20260502__startsat-12h__2026-05-01t22-14-04z`
- `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-01t22-14-04z`
- `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-01t22-14-04z`
- `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-01t22-14-04z`
- `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-01t22-14-04z`
- `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-01t22-14-04z`

Commands:

```powershell
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --sample-size 20 --sample-seed 20260502 --starts-after "2025-12-01T00:00:00 Europe/Berlin (+01)"
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --sample-size 5 --sample-seed 20260503 --starts-after "2025-12-01T00:00:00 Europe/Berlin (+01)" --slice-key random-5-seed-20260503-repeated-fixtures
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "Eintracht Frankfurt" --away "1899 Hoffenheim" --matchday 19 --sample-size 4 --slice-key repeat-4-seed-20260503-fixture-01
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "1. FC Köln" --away "RB Leipzig" --matchday 21 --sample-size 4 --slice-key repeat-4-seed-20260503-fixture-02
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "Borussia Dortmund" --away "FC Augsburg" --matchday 26 --sample-size 4 --slice-key repeat-4-seed-20260503-fixture-03
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "Bayer 04 Leverkusen" --away "FC Bayern München" --matchday 26 --sample-size 4 --slice-key repeat-4-seed-20260503-fixture-04
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "FSV Mainz 05" --away "FC Bayern München" --matchday 31 --sample-size 4 --slice-key repeat-4-seed-20260503-fixture-05
dotnet run --project src/Orchestrator -- run-slice o3 --manifest artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-20-seed-20260502/slice-manifest.json --run-name "slice__pes-squad__o3__prompt-v1__reasoning-medium__random-20-seed-20260502__startsat-12h__2026-05-01t22-14-04z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-size 20 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md19-eintracht-frankfurt-vs-1899-hoffenheim/repeat-4-seed-20260503-fixture-01/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-01t22-14-04z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md21-1-fc-koln-vs-rb-leipzig/repeat-4-seed-20260503-fixture-02/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-01t22-14-04z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-borussia-dortmund-vs-fc-augsburg/repeat-4-seed-20260503-fixture-03/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-01t22-14-04z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-bayer-04-leverkusen-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-04/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-01t22-14-04z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md31-fsv-mainz-05-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-05/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-01t22-14-04z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
```

Usage analysis command:

```powershell
uv run python docs/research/estimate-experiment-cost-skill/analyze_token_usage.py --env ..\KicktippAi.Secrets\src\Orchestrator\.env --group "slice-measured=slice__pes-squad__o3__prompt-v1__reasoning-medium__random-20-seed-20260502__startsat-12h__2026-05-01t22-14-04z" --group "repeated-measured=repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-01t22-14-04z" --group "repeated-measured=repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-01t22-14-04z" --group "repeated-measured=repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-01t22-14-04z" --group "repeated-measured=repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-01t22-14-04z" --group "repeated-measured=repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-01t22-14-04z" --output C:\tmp\kicktippai-subexp-b-usage.json --left-group slice-measured --right-group repeated-measured --expect slice-measured=20 --expect repeated-measured=20 --metrics inputTokens outputTokens --seed 20260502 --report-json C:\tmp\kicktippai-subexp-b-analysis.json
```

## Outcome

### Sub Experiment A Outcome

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

Answer for Sub Experiment A:

- The measured repeated-match sample did not show a statistically significant difference in output tokens or total tokens compared with the slice sample.
- Total input tokens did differ significantly, but that result was fixture-confounded. The repeated-match fixture was also present in the slice, and its slice input-token count was exactly `3269`, the same as every repeated-match observation for that fixture.
- Effective uncached input tokens differed strongly because repeated-match calls received cache hits in 5 of the 10 measured observations. This supports the cost motivation, but cached input is separate from the total-token question.

### Sub Experiment B Outcome

Run summaries:

| Run | Executions | Batch shape | Aggregate score |
| --- | ---: | --- | ---: |
| Slice | 20 | one batch of 20 | total `28`, avg `1.4` |
| Repeated fixture 01 | 4 | repeated-match batch-count 1 | total `6`, avg `1.5` |
| Repeated fixture 02 | 4 | repeated-match batch-count 1 | total `15`, avg `3.75` |
| Repeated fixture 03 | 4 | repeated-match batch-count 1 | total `10`, avg `2.5` |
| Repeated fixture 04 | 4 | repeated-match batch-count 1 | total `0`, avg `0.0` |
| Repeated fixture 05 | 4 | repeated-match batch-count 1 | total `8`, avg `2.0` |

Primary comparison:

| Metric | Slice mean | Repeated measured mean | Difference, slice - repeated | Permutation p | Bootstrap 95% CI |
| --- | ---: | ---: | ---: | ---: | --- |
| Input tokens | 3438.950 | 3379.000 | 59.950 | 0.100263 | [-5.200, 130.000] |
| Output tokens | 1553.900 | 1764.400 | -210.500 | 0.524548 | [-846.752, 396.650] |

Answer for Sub Experiment B:

- With five random repeated fixtures of size four, neither total input tokens nor total output tokens differed significantly from the 20-item slice sample.
- This checks out for the requested model configuration (`o3`, medium effort) and supports the proposed reference dataset shape: `M` random repeated-match fixtures of size `S`, for `N = M * S` total datapoints.
- The input-token p-value moved from significant in Sub Experiment A to non-significant in Sub Experiment B, consistent with the theory that the Sub Experiment A input result was mostly single-fixture context confounding.
- Cached input remains out of scope for this conclusion. The design is useful for total input/output token estimates; cost estimates still need separate cached-input handling.

## Further Research Directions

- Wait for the user to choose the next model/reasoning-effort sub experiment.
- Treat `M` random repeated fixtures of size `S` as the preferred low-cost reference dataset design for total token estimates, with `M` high enough to sample fixture/context variation.
- Use the batched v2 observations path in [analyze_token_usage.py](analyze_token_usage.py) for future token usage pulls.
- Extend the Orchestrator export or report tooling to include normalized usage details directly in analysis bundles, so future research can avoid ad hoc Langfuse usage extraction.

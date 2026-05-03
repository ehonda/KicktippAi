# 003 - Slice vs Multi-Fixture Token Usage Across Model Configs

Status: Completed

Executed on: 2026-05-03 UTC run stamp, documented on 2026-05-04

Repository commit used for live runs: working tree based on `aba7e2d Split token usage experiments`, with the pricing and experiment max-output changes from this document applied before execution.

## Research Question

Does the token-usage correspondence observed in [002](002-slice-vs-multi-fixture-repeated-token-usage.md) also hold for:

- `gpt-5.4-nano` with `xhigh` reasoning effort
- `gpt-5.5` with `none` reasoning effort

The correspondence question compares a slice of size `N` with `M` repeated fixtures of size `S`, where `N = M * S`.

## Methodology

This experiment uses the shared token-usage analysis method documented in [token-usage-methodology.md](token-usage-methodology.md): compact Langfuse usage extraction from `predict-match`, two-sided exact permutation tests over difference in means, and seeded bootstrap confidence intervals.

Configuration:

- Community context: `pes-squad`
- Source season: Bundesliga 2025-26
- Prompt: hosted Langfuse prompt route `langfuse-o3-poc`
- Langfuse prompt name: `kicktippai/predict-one-match-o3-poc`
- Langfuse prompt label: `poc`
- Evaluation policy: relative `startsat-12h`
- Slice sample size: `N = 20`, seed `20260502`
- Repeated-match measured sample size: `N = 20`
- Repeated-match design: `M = 5` random fixtures, `S = 4` repetitions each, so `N = M * S`
- Repeated-match runner setting: `--batch-count 1`
- Full run stamp: `2026-05-03t22-42-35z`

Pricing and model-cap sources checked before full runs:

- [OpenAI API pricing](https://developers.openai.com/api/docs/pricing): `gpt-5.5` standard input `$5.00`, cached input `$0.50`, output `$30.00`; `gpt-5.4-nano` standard input `$0.20`, cached input `$0.02`, output `$1.25`; flex uses the 50% processing rate for these short-context models.
- [OpenAI prompt caching guide](https://developers.openai.com/api/docs/guides/prompt-caching): caching is automatic and based on exact prompt-prefix matches; the docs do not state that small parallel post-warmup batches after a completed warmup should have worse cache hit rates.
- [OpenAI `gpt-5.4-nano` model page](https://developers.openai.com/api/docs/models/gpt-5.4-nano): 128,000 max output tokens.

Expected spend recorded before full runs:

- Initial full-run estimate after historical usage inspection: about `$0.45-$1.25`.
- After the successful `gpt-5.4-nano xhigh` preflight showed 15,004 output tokens and `$0.009450` observed flex cost, the full-run estimate was revised to about `$0.70-$1.60`.

Prepared artifacts reused from [002](002-slice-vs-multi-fixture-repeated-token-usage.md):

- Slice manifest: `artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-20-seed-20260502/slice-manifest.json`
- Repeated-match manifests:
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md19-eintracht-frankfurt-vs-1899-hoffenheim/repeat-4-seed-20260503-fixture-01/slice-manifest.json`
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md21-1-fc-koln-vs-rb-leipzig/repeat-4-seed-20260503-fixture-02/slice-manifest.json`
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md26-borussia-dortmund-vs-fc-augsburg/repeat-4-seed-20260503-fixture-03/slice-manifest.json`
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md26-bayer-04-leverkusen-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-04/slice-manifest.json`
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md31-fsv-mainz-05-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-05/slice-manifest.json`

Preflight and output-token cap finding:

- A one-item `gpt-5.4-nano xhigh` preflight with the default 10,000 output-token cap failed with `OpenAI response did not contain output text`.
- Retrying the same preflight with `--max-output-tokens 20000` succeeded.
- The full `gpt-5.4-nano xhigh` slice with `--max-output-tokens 20000` still failed for 5 of 20 items.
- The completed `gpt-5.4-nano xhigh` runs therefore used `--max-output-tokens 40000`, and the cap is included in run names and trace tags as `maxout-40000`.

Run names used for analysis:

- `slice__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__random-20-seed-20260502__startsat-12h__2026-05-03t22-42-35z`
- `slice__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__random-20-seed-20260502__startsat-12h__2026-05-03t22-42-35z`
- `repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-03t22-42-35z`
- `repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-03t22-42-35z`
- `repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-03t22-42-35z`
- `repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-03t22-42-35z`
- `repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-03t22-42-35z`
- `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-03t22-42-35z`
- `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-03t22-42-35z`
- `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-03t22-42-35z`
- `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-03t22-42-35z`
- `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-03t22-42-35z`

Live execution commands:

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-1-langfuse-poc/slice-dataset.json

dotnet run --project src/Orchestrator -- run-repeated-match gpt-5.4-nano --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-1-langfuse-poc/slice-manifest.json --run-name "preflight__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__repeat-1__exact-time__2026-05-03t22-34-56z" --prompt-key langfuse-o3-poc --prompt-source langfuse --langfuse-prompt-name kicktippai/predict-one-match-o3-poc --langfuse-prompt-label poc --reasoning-effort xhigh --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 1 --replace-run

dotnet run --project src/Orchestrator -- run-repeated-match gpt-5.4-nano --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-1-langfuse-poc/slice-manifest.json --run-name "preflight__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-20000__repeat-1__exact-time__2026-05-03t22-40-46z" --prompt-key langfuse-o3-poc --prompt-source langfuse --langfuse-prompt-name kicktippai/predict-one-match-o3-poc --langfuse-prompt-label poc --reasoning-effort xhigh --max-output-tokens 20000 --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 1 --replace-run

dotnet run --project src/Orchestrator -- run-slice gpt-5.4-nano --manifest artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-20-seed-20260502/slice-manifest.json --run-name "slice__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__random-20-seed-20260502__startsat-12h__2026-05-03t22-42-35z" --prompt-key langfuse-o3-poc --prompt-source langfuse --langfuse-prompt-name kicktippai/predict-one-match-o3-poc --langfuse-prompt-label poc --reasoning-effort xhigh --max-output-tokens 40000 --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-size 20 --replace-run

dotnet run --project src/Orchestrator -- run-slice gpt-5.5 --manifest artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-20-seed-20260502/slice-manifest.json --run-name "slice__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__random-20-seed-20260502__startsat-12h__2026-05-03t22-42-35z" --prompt-key langfuse-o3-poc --prompt-source langfuse --langfuse-prompt-name kicktippai/predict-one-match-o3-poc --langfuse-prompt-label poc --reasoning-effort none --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-size 20 --replace-run
```

The five repeated-match runs for each model were launched in parallel at the outer PowerShell job level. Each job invoked `run-repeated-match` against one of the repeated manifests listed above, using that manifest's matching run name from the analysis list, `--prompt-source langfuse`, `--reasoning-effort xhigh --max-output-tokens 40000` for `gpt-5.4-nano`, `--reasoning-effort none` for `gpt-5.5`, and `--batch-count 1`.

Usage analysis commands:

```powershell
uv run python docs/research/estimate-experiment-cost-skill/analyze_token_usage.py --env ..\KicktippAi.Secrets\src\Orchestrator\.env --group "slice-measured=slice__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__random-20-seed-20260502__startsat-12h__2026-05-03t22-42-35z" --group "repeated-measured=repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-03t22-42-35z" --group "repeated-measured=repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-03t22-42-35z" --group "repeated-measured=repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-03t22-42-35z" --group "repeated-measured=repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-03t22-42-35z" --group "repeated-measured=repeated-match__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-03t22-42-35z" --output C:\tmp\kicktippai-exp3-gpt54nano-xhigh-usage.json --left-group slice-measured --right-group repeated-measured --expect slice-measured=20 --expect repeated-measured=20 --metrics inputTokens outputTokens reasoningTokens totalTokens --seed 20260502 --report-json C:\tmp\kicktippai-exp3-gpt54nano-xhigh-analysis.json

uv run python docs/research/estimate-experiment-cost-skill/analyze_token_usage.py --env ..\KicktippAi.Secrets\src\Orchestrator\.env --group "slice-measured=slice__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__random-20-seed-20260502__startsat-12h__2026-05-03t22-42-35z" --group "repeated-measured=repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-03t22-42-35z" --group "repeated-measured=repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-03t22-42-35z" --group "repeated-measured=repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-03t22-42-35z" --group "repeated-measured=repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-03t22-42-35z" --group "repeated-measured=repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-03t22-42-35z" --output C:\tmp\kicktippai-exp3-gpt55-none-usage.json --left-group slice-measured --right-group repeated-measured --expect slice-measured=20 --expect repeated-measured=20 --metrics inputTokens outputTokens reasoningTokens totalTokens --seed 20260502 --report-json C:\tmp\kicktippai-exp3-gpt55-none-analysis.json
```

## Outcome

Primary comparison for `gpt-5.4-nano xhigh`:

| Metric | Slice mean | Repeated measured mean | Difference, slice - repeated | Permutation p | Bootstrap 95% CI |
| --- | ---: | ---: | ---: | ---: | --- |
| Input tokens | 3438.950 | 3379.000 | 59.950 | 0.100263 | [-5.200, 130.000] |
| Output tokens | 15938.800 | 17031.900 | -1093.100 | 0.665735 | [-5881.920, 3697.055] |
| Reasoning tokens | 15919.800 | 17012.900 | -1093.100 | 0.665735 | [-5895.921, 3794.010] |
| Total tokens | 19377.750 | 20410.900 | -1033.150 | 0.684412 | [-5816.251, 3916.335] |

Primary comparison for `gpt-5.5 none`:

| Metric | Slice mean | Repeated measured mean | Difference, slice - repeated | Permutation p | Bootstrap 95% CI |
| --- | ---: | ---: | ---: | ---: | --- |
| Input tokens | 3438.950 | 3379.000 | 59.950 | 0.100263 | [-5.200, 130.000] |
| Output tokens | 17.000 | 17.000 | 0.000 | 1.000000 | [0.000, 0.000] |
| Reasoning tokens | 0.000 | 0.000 | 0.000 | 1.000000 | [0.000, 0.000] |
| Total tokens | 3455.950 | 3396.000 | 59.950 | 0.100263 | [-5.351, 129.801] |

Descriptive cache and cost evidence:

| Model config | Group | n | Input | Cached input | Cache hit rate | Output | Reasoning | Total tokens | Observed flex cost |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `gpt-5.4-nano xhigh` | Slice | 20 | 68779 | 56320 | 81.9% | 318776 | 318396 | 387555 | `$0.201044` |
| `gpt-5.4-nano xhigh` | Repeated | 20 | 67580 | 45056 | 66.7% | 340638 | 340258 | 408218 | `$0.215602` |
| `gpt-5.5 none` | Slice | 20 | 68779 | 0 | 0.0% | 340 | 0 | 69119 | `$0.177048` |
| `gpt-5.5 none` | Repeated | 20 | 67580 | 48128 | 71.2% | 340 | 0 | 67920 | `$0.065762` |

Answer:

- The Experiment 2 correspondence held for both tested model configs on the primary metrics. No `inputTokens`, `outputTokens`, `reasoningTokens`, or `totalTokens` comparison showed a statistically significant slice-vs-repeated difference.
- For `gpt-5.4-nano xhigh`, output and reasoning tokens dominated cost and variance. The `M = 5`, `S = 4`, `N = 20` repeated-fixture design still matched the slice well enough for the token correspondence question.
- For `gpt-5.5 none`, output tokens were constant at 17 per observation, reasoning tokens were zero, and total-token behavior was effectively input-token behavior.
- Cached input and observed cost differed by group and model, so they remain descriptive evidence only. The primary correspondence metric should stay focused on token counts unless the research question explicitly asks about cache or monetary cost.
- The completed valid analyzed runs cost `$0.659456` total, excluding the failed cap-probing attempts.

## Further Research Directions

- The evidence from experiments 002 and 003 is enough to start the cost-estimate skill workflow: use `M` random repeated fixtures of size `S`, with `N = M * S`, as a low-cost reference design for primary token estimates.
- For reasoning-heavy model configs, run a one-item preflight with the intended prompt route and reasoning effort, and record the observed output/reasoning tokens before the full run.
- Add prompt reconstruction or tokenizer-based input-token estimation next, so the future skill can estimate input cost without a live model call.
- Treat cached-input estimation as a separate follow-up problem. The token correspondence held, but cache-hit behavior was not stable enough to use as the primary evidence for total experiment cost.

# gpt-5.4-nano vs gpt-5.5 None, Post-Cutoff Repeated Match Slice

Date: 2026-05-17

This experiment compares `gpt-5.4-nano` and `gpt-5.5` with reasoning effort
`none` on the same repeated-match-slice dataset. The goal is to compare the two
models on multiple repeated fixtures while avoiding completed matches that fall
before the `gpt-5.5` knowledge cutoff.

For this run, `gpt-5.5` was treated as having a December 1, 2025 knowledge
cutoff. Using a two-day safety margin, this dataset only samples fixtures
strictly after `2025-12-03T00:00:00 Europe/Berlin (+01)`.

## Dataset

| Field | Value |
| --- | --- |
| Dataset | `match-predictions/bundesliga-2025-26/pes-squad/repeated-match-slices/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203` |
| Community | `pes-squad` |
| Slice | `random-15x10-seed-20260517-after-20251203` |
| Source pool | `all-matchdays-after-20251202t230000z` |
| Match count | 15 |
| Repetitions | 10 |
| Predictions | 150 |
| Sample seed | `20260517` |
| Selected item hash | `24b27640a099bdb51e35097f5351499ce43d603a033f9fb263dbbba7b0b762fc` |
| Starts after | `2025-12-03T00:00:00 Europe/Berlin (+01)` |
| Earliest selected fixture | `2025-12-06T15:30:00 UTC+01 (+01)` |
| Latest selected fixture | `2026-05-03T16:30:00 UTC+02 (+02)` |

The earlier smoke dataset used `all-matchdays` and included fixtures before the
model knowledge cutoff. This dataset corrects that by applying the explicit
post-cutoff `--starts-after` filter.

## Configuration

Both runs used the same repeated-match-slice manifest, hosted Langfuse prompt,
relative evaluation policy, and batching settings. The only intended comparison
axis was the model.

| Config | Prompt | Reasoning | Batch count | Parallelism |
| --- | --- | --- | ---: | ---: |
| `gpt-5.4-nano` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `none` | 3 | 3 |
| `gpt-5.5` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `none` | 3 | 3 |

Parallelism was set to `3` for both runs because the prior same-shape smoke run
at parallelism `5` produced rate-limit retry warnings. The repeated-match-slice
execution still kept warmup-plus-batches behavior inside each fixture workflow.

Cost estimates before the run were:

| Model | Command | Estimated cost |
| --- | --- | ---: |
| `gpt-5.4-nano none` | `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 150 --model gpt-5.4-nano --reasoning-effort none` | `$0.053820750000` |
| `gpt-5.5 none` | `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 150 --model gpt-5.5 --reasoning-effort none` | `$1.305375000000` |

## Langfuse Runs

| Config | Run name |
| --- | --- |
| `gpt-5.4-nano none` | `repeated-match-slice__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-none__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-05-16t23-24-24z` |
| `gpt-5.5 none` | `repeated-match-slice__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-05-16t23-24-24z` |

## Results

For `repeated-match-slice`, the primary metric is `avg_kicktipp_points`: the
mean over repetition-total rows, where each row sums points across the 15
selected fixtures for the same repetition index.

| Rank | Config | Total Kicktipp points | Avg repetition-total points |
| ---: | --- | ---: | ---: |
| 1 | `gpt-5.4-nano none` | 189 | 18.9000 |
| 2 | `gpt-5.5 none` | 162 | 16.2000 |

The paired comparison favored `gpt-5.4-nano none` by `2.7` average
repetition-total points. Across the 10 paired repetition-total rows,
`gpt-5.4-nano none` had 8 wins, 0 ties, and 2 losses against `gpt-5.5 none`.

| Statistic | Value |
| --- | ---: |
| Pairings | 10 |
| Mean difference | 2.7000 |
| Median difference | 3.5000 |
| Wilcoxon p-value | 0.0234 |
| Mean-difference 95% bootstrap CI | [1.1000, 4.5000] |
| Median-difference 95% bootstrap CI | [2.0000, 7.0000] |

## Interpretation

On this post-cutoff repeated-match slice, `gpt-5.4-nano none` outperformed
`gpt-5.5 none` by 27 total Kicktipp points across the 150 predictions. The
paired test is significant at alpha `0.05`, but the effective paired sample is
only 10 repetition-total rows, so the result is still best treated as a useful
signal rather than a final model-selection decision.

The direction is nevertheless clear for this slice: under the same hosted
prompt, reasoning effort, evaluation policy, and batching structure,
`gpt-5.4-nano none` was both cheaper and higher-scoring than `gpt-5.5 none`.

## Pages Report

The hosted comparison page is generated at:

`experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/gpt54nano-vs-gpt55-none-2026-05-16t23-24-24z.analysis.report.html`

Repo-relative link:
[gpt-5.4-nano vs gpt-5.5 repeated-match-slice report](../../experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/gpt54nano-vs-gpt55-none-2026-05-16t23-24-24z.analysis.report.html)

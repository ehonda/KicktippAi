# gpt-5.5 Medium vs o3 Medium, Post-Cutoff Repeated Match Slice

Date: 2026-05-17

This experiment compares `gpt-5.5` and `o3` with reasoning effort `medium` on
the same post-cutoff repeated-match-slice dataset. It extends the earlier
post-cutoff comparison by testing the medium-reasoning configurations on 15
sampled fixtures with 10 repetitions each.

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

The dataset only samples fixtures after the two-day safety boundary used for
the `gpt-5.5` December 1, 2025 knowledge cutoff assumption.

## Configuration

Both runs used the same repeated-match-slice manifest, hosted Langfuse prompt,
reasoning effort, and relative evaluation policy. The intended comparison axis
was the model.

| Config | Prompt | Reasoning | Batch count | Parallelism |
| --- | --- | --- | ---: | ---: |
| `gpt-5.5` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `medium` | 3 | 1 |
| `o3` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `medium` | 3 | 3 |

The first `gpt-5.5 medium` attempt at parallelism `3` failed before creating a
dataset run, so it was retried with the documented repeated-match-slice fallback
parallelism `1`. The `o3 medium` run completed at parallelism `3`.

Conservative cost estimates before the run were:

| Model | Command | Estimated cost |
| --- | --- | ---: |
| `gpt-5.5 medium` | `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 150 --model gpt-5.5 --reasoning-effort medium` | `$2.546137500000` |
| `o3 medium` | `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 150 --model o3 --reasoning-effort medium` | `$1.565490000000` |

## Langfuse Runs

| Config | Run name |
| --- | --- |
| `gpt-5.5 medium` | `repeated-match-slice__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-medium__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-05-16t23-55-32z` |
| `o3 medium` | `repeated-match-slice__pes-squad__o3__langfuse-o3-poc__reasoning-medium__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-05-16t23-55-32z` |

## Results

For `repeated-match-slice`, the primary metric is `avg_kicktipp_points`: the
mean over repetition-total rows, where each row sums points across the 15
selected fixtures for the same repetition index.

| Rank | Config | Total Kicktipp points | Avg repetition-total points |
| ---: | --- | ---: | ---: |
| 1 | `o3 medium` | 198 | 19.8000 |
| 2 | `gpt-5.5 medium` | 150 | 15.0000 |

The paired comparison favored `o3 medium` by `4.8` average repetition-total
points. Across the 10 paired repetition-total rows, `o3 medium` won all 10.

| Statistic | Value |
| --- | ---: |
| Pairings | 10 |
| Mean difference | 4.8000 |
| Median difference | 6.0000 |
| Wilcoxon p-value | 0.0020 |
| Mean-difference 95% bootstrap CI | [3.5000, 6.2000] |
| Median-difference 95% bootstrap CI | [5.0000, 9.0000] |

## Interpretation

On this post-cutoff repeated-match slice, `o3 medium` clearly outperformed
`gpt-5.5 medium`: it scored 48 more total Kicktipp points across 150
predictions and won every repetition-total pairing.

The effective paired sample remains only 10 repetition-total rows, so this is
still a slice-level result rather than a season-wide proof. Within this dataset,
however, the direction and effect size are both strong.

## Pages Report

The hosted comparison page is generated at:

`experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/gpt55-medium-vs-o3-medium-2026-05-16t23-55-32z.analysis.report.html`

Repo-relative link:
[gpt-5.5 medium vs o3 medium repeated-match-slice report](../../experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/gpt55-medium-vs-o3-medium-2026-05-16t23-55-32z.analysis.report.html)

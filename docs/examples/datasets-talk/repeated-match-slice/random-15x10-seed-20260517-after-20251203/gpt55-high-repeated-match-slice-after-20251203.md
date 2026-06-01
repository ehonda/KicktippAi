# gpt-5.5 High, Post-Cutoff Repeated Match Slice

Date: 2026-05-19

This follow-up runs `gpt-5.5` with reasoning effort `high` on the same
post-cutoff repeated-match-slice dataset used for the earlier `gpt-5.4-nano
none`, `gpt-5.5 none`, `gpt-5.5 medium`, and `o3 medium` comparisons.

The goal was to see whether higher reasoning effort closes the gap between
`gpt-5.5 medium` and `o3 medium` while keeping the comparison fixed to the same
15 sampled fixtures and 10 repetitions per fixture.

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

The `starts-after` boundary applies the two-day safety margin for the assumed
`gpt-5.5` December 1, 2025 knowledge cutoff.

## Configuration

`gpt-5.5 high` used the same hosted Langfuse prompt and relative evaluation
policy as the existing runs. It was started at `--parallelism 1` because the
earlier `gpt-5.5 medium` run needed that documented fallback.

| Config | Prompt | Reasoning | Batch count | Parallelism |
| --- | --- | --- | ---: | ---: |
| `gpt-5.5` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `high` | 3 | 1 |

Run name:

`repeated-match-slice__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-high__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-05-18t22-28-33z`

The conservative estimate from the cost-estimator row was `$8.017462500000` for
150 predictions. The observed Langfuse cost was `$15.4524585`; compact usage
showed 150 observations, 516,710 input tokens, 376,832 cached input tokens,
498,521 output tokens, and 495,671 reasoning tokens. The run mostly fell back to
the default service tier rather than flex, which explains why the actual cost
landed above the flex-based conservative row.

## Results

For `repeated-match-slice`, the primary metric is `avg_kicktipp_points`: the
mean over repetition-total rows, where each row sums points across the 15
selected fixtures for the same repetition index.

Across all five available runs on this dataset, the ranking was:

| Rank | Config | Avg repetition-total points |
| ---: | --- | ---: |
| 1 | `o3 medium` | 19.8000 |
| 2 | `gpt-5.5 high` | 19.5000 |
| 3 | `gpt-5.4-nano none` | 18.9000 |
| 4 | `gpt-5.5 none` | 16.2000 |
| 5 | `gpt-5.5 medium` | 15.0000 |

`gpt-5.5 high` was a clear improvement over `gpt-5.5 medium`: it won all 10
paired repetition-total rows, with a mean difference of 4.5 points and a
Wilcoxon p-value of 0.0020.

Against `o3 medium`, `gpt-5.5 high` was close but did not overtake it. `o3
medium` led by 0.3 average repetition-total points, won 8 of the 10 paired rows,
and the Wilcoxon p-value was 0.4102, so this slice does not show a significant
difference between those two.

The five-run comparison had a Friedman p-value of 0.0001. After Holm
correction, `gpt-5.5 high` significantly beat `gpt-5.5 none` and `gpt-5.5
medium`, but not `o3 medium` or `gpt-5.4-nano none`.

## Reports

The hosted comparison pages are generated at:

`experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/gpt55-medium-vs-gpt55-high-2026-05-18t22-28-33z.analysis.report.html`

`experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/o3-medium-vs-gpt55-high-2026-05-18t22-28-33z.analysis.report.html`

`experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/all-runs-2026-05-18t22-28-33z.analysis.report.html`

Repo-relative links:

[gpt-5.5 medium vs gpt-5.5 high repeated-match-slice report](../../experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/gpt55-medium-vs-gpt55-high-2026-05-18t22-28-33z.analysis.report.html)

[o3 medium vs gpt-5.5 high repeated-match-slice report](../../experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/o3-medium-vs-gpt55-high-2026-05-18t22-28-33z.analysis.report.html)

[all 150-item repeated-match-slice runs report](../../experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/all-runs-2026-05-18t22-28-33z.analysis.report.html)

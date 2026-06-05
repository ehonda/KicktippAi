# o3 Low vs Medium vs High, Post-Cutoff Repeated Match Slice

Date: 2026-06-05

This follow-up compares `o3` reasoning efforts `low`, `medium`, and `high` on
the same post-cutoff repeated-match-slice dataset. The `o3 medium` run already
existed on this 15x10 slice, so this experiment adds fresh `low` and `high`
variants and evaluates all three together.

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

The `starts-after` boundary keeps the fixture pool aligned with the earlier
post-cutoff hosted-prompt comparisons that assumed a December 1, 2025 knowledge
cutoff plus a two-day safety margin.

## Configuration

All three runs used the same repeated-match-slice manifest, hosted Langfuse
prompt, and relative evaluation policy. The intended comparison axis was
reasoning effort.

| Config | Prompt | Reasoning | Max output tokens | Batch count | Parallelism |
| --- | --- | --- | ---: | ---: | ---: |
| `o3 low` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `low` | default `10000` | 3 | 3 |
| `o3 medium` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `medium` | default `10000` | 3 | 3 |
| `o3 high` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `high` | `20000` | 3 | 3 |

The reused `o3 medium` baseline comes from the earlier comparison documented in
`gpt55-medium-vs-o3-medium-repeated-match-slice-after-20251203.md`.

The first `o3 high` attempt at the default output cap failed on the first batch
with `OpenAI response did not contain output text` for `VfL Wolfsburg vs 1. FC
Union Berlin`. The rerun succeeded with `--max-output-tokens 20000` and reused
the same run name via `--replace-run`.

## Langfuse Runs

| Config | Run name |
| --- | --- |
| `o3 low` | `repeated-match-slice__pes-squad__o3__langfuse-o3-poc__reasoning-low__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-06-05t21-31-30z` |
| `o3 medium` | `repeated-match-slice__pes-squad__o3__langfuse-o3-poc__reasoning-medium__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-05-16t23-55-32z` |
| `o3 high` | `repeated-match-slice__pes-squad__o3__langfuse-o3-poc__reasoning-high__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-06-05t21-31-30z` |

## Results

For `repeated-match-slice`, the primary metric is `avg_kicktipp_points`: the
mean over repetition-total rows, where each row sums points across the 15
selected fixtures for the same repetition index.

| Rank | Config | Total Kicktipp points | Avg repetition-total points |
| ---: | --- | ---: | ---: |
| 1 | `o3 high` | 200 | 20.0000 |
| 2 | `o3 medium` | 198 | 19.8000 |
| 3 | `o3 low` | 190 | 19.0000 |

The ranking difference was small, and the three-run comparison did not show a
meaningful separation:

| Statistic | Value |
| --- | ---: |
| Pairings | 10 |
| Friedman p-value | 0.9726 |

Pairwise comparisons likewise stayed inconclusive after Holm correction:

| Comparison | Avg repetition-total delta | Raw p-value | Adj. p-value | Significant | W/T/L |
| --- | ---: | ---: | ---: | --- | --- |
| `o3 high` vs `o3 medium` | 0.2000 | 0.7969 | 1.0000 | no | 4 / 1 / 5 |
| `o3 high` vs `o3 low` | 1.0000 | 0.2969 | 0.8906 | no | 5 / 1 / 4 |
| `o3 medium` vs `o3 low` | 0.8000 | 0.6562 | 1.0000 | no | 4 / 2 / 4 |

These win/tie/loss counts are over the 10 repetition-total pairings, not the
full 150 individual predictions.

## Interpretation

`o3 high` finished first by raw score, but only by 2 total Kicktipp points over
`o3 medium` across 150 predictions. The paired repetition totals were mixed
rather than one-sided: `o3 high` beat `o3 medium` in 4 of the 10 pairings and
lost 5.

That leaves this slice without evidence that changing the reasoning effort away
from `medium` materially improves results. `o3 low` trailed the other two, but
it also failed to separate statistically from `o3 medium` or `o3 high` on this
sample.

The practical recommendation is to keep `o3 medium` as the default for now.
`o3 high` is only weakly ahead on raw totals here, required a larger output cap
to complete, and did not produce a statistically convincing gain. If reasoning
effort is still a live product question, the next step should be a larger slice
or another repeated-match-slice family rather than a config change based on
this 15x10 sample alone.

## Pages Report

The hosted comparison page is generated at:

`experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/o3-low-medium-high-2026-06-05t21-31-30z.analysis.report.html`

Repo-relative link:
[o3 low vs medium vs high repeated-match-slice report](../../experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/o3-low-medium-high-2026-06-05t21-31-30z.analysis.report.html)

The expanded all-models page for this same dataset, now including `gpt-5.4-nano
(none)`, `gpt-5.5 (none)`, `gpt-5.5 (medium)`, `gpt-5.5 (high)`, `o3 (low)`,
`o3 (medium)`, and `o3 (high)`, is generated at:

`experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/all-runs-2026-06-05t21-31-30z.analysis.report.html`

Repo-relative link:
[all 150-item repeated-match-slice runs report with all seven configs](../../experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/all-runs-2026-06-05t21-31-30z.analysis.report.html)

# Prod+ o3 Reasoning Effort, Random 40

Date: 2026-05-05

This experiment compares the current production baseline, `current-prod` / `o3`
with medium reasoning effort, against the same model with high reasoning effort.
The goal is to check whether turning up reasoning effort improves Kicktipp scoring
before committing to a larger and more expensive run.

## Dataset

| Field | Value |
| --- | --- |
| Dataset | `match-predictions/bundesliga-2025-26/pes-squad/slices/all-matchdays-after-20251130t230000z/random-40-seed-20260505-prod-plus-o3-effort` |
| Community | `pes-squad` |
| Slice | `random-40-seed-20260505-prod-plus-o3-effort` |
| Source pool | `all-matchdays-after-20251130t230000z` |
| Sample size | 40 |
| Sample seed | `20260505` |
| Selected item hash | `7a4f98114e1b81ec999d50f5250903fca14dc2995d9d5961b29e5278e9c0c6a8` |
| Starts after | `2025-12-01T00:00:00 Europe/Berlin (+01)` |

## Configuration

| Config | Run description | Prompt | Reasoning | Max output tokens | Batch size |
| --- | --- | --- | --- | ---: | ---: |
| `current-prod` | `Prod+: o3 medium` | `prompt-v1` | `medium` | 20,000 | 10 |
| candidate | `Prod+: o3 high` | `prompt-v1` | `high` | 20,000 | 20 |

Both runs used the relative evaluation policy with offset `-12:00:00`.
Batch size only controlled execution concurrency and is not part of the model
configuration being compared.

## Langfuse Runs

| Config | Run name |
| --- | --- |
| `o3 medium` | `slice__pes-squad__o3__prompt-v1__reasoning-medium__maxout-20000__random-40-seed-20260505-prod-plus-o3-effort__startsat-12h__2026-05-04t22-49-35z` |
| `o3 high` | `slice__pes-squad__o3__prompt-v1__reasoning-high__maxout-20000__random-40-seed-20260505-prod-plus-o3-effort__startsat-12h__2026-05-04t22-49-35z` |

## Results

| Rank | Config | Total Kicktipp points | Average points |
| ---: | --- | ---: | ---: |
| 1 | `o3 medium` | 51 | 1.275 |
| 2 | `o3 high` | 50 | 1.250 |

The paired comparison favored `o3 medium` by 1 total Kicktipp point. The mean
paired difference was 0.025 points per match with a 95% bootstrap confidence
interval of -0.175 to 0.225. The median paired difference was 0.0, with
per-item win/tie/loss counts of 5/32/3 for `o3 medium` versus `o3 high`.

The Wilcoxon signed-rank test p-value was 0.8852, so this random 40-item slice
does not show a statistically significant improvement from high reasoning.

## Pages Report

The hosted comparison page is generated at:

`experiment-analysis/slices/pes-squad/all-matchdays-after-20251130t230000z/random-40-seed-20260505-prod-plus-o3-effort/random-40-seed-20260505-prod-plus-o3-effort-2026-05-04t22-49-35z.analysis.report.html`

Repo-relative link:
[random-40 analysis report](../../experiment-analysis/slices/pes-squad/all-matchdays-after-20251130t230000z/random-40-seed-20260505-prod-plus-o3-effort/random-40-seed-20260505-prod-plus-o3-effort-2026-05-04t22-49-35z.analysis.report.html)

## Decision

Do not switch production to `o3 high` based on this slice. The candidate was
slightly lower on total points, most pairings tied, and the statistical test does
not indicate a reliable lift. A larger slice would be needed if there is still a
strong reason to retest high reasoning.

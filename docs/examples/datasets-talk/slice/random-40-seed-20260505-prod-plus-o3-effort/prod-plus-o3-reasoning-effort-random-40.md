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

## Meaningful Effect Size

We treat `0.1` Kicktipp points per match as a useful smallest effect size of
interest for this comparison.

The practical calibration comes from the latest local `pes-squad`
community-to-date artifact, prepared on 2026-05-05 through matchday 32. That
artifact has 288 completed matches and 11 participants. A lift of `0.1`
points per match is worth 28.8 points over this nearly complete Bundesliga
season, enough to move `kicktipp.ai` from 395 points to about 424 points and
therefore ahead of the current 408-point leader.

Participant names are anonymized here except `E.Honda` and `kicktipp.ai`, using
the same privacy convention as the community-to-date Pages reports.

| Rank | Participant | Total points | Avg points / match |
| ---: | --- | ---: | ---: |
| 1 | Participant A | 408 | 1.417 |
| 2 | Participant B | 403 | 1.399 |
| 3 | Participant C | 401 | 1.392 |
| 4 | `kicktipp.ai` | 395 | 1.372 |
| 5 | Participant D | 390 | 1.354 |
| 6 | Participant E | 388 | 1.347 |
| 7 | `E.Honda` | 378 | 1.313 |
| 8 | Participant F | 376 | 1.306 |
| 9 | Participant G | 375 | 1.302 |
| 10 | Participant H | 374 | 1.299 |
| 11 | Participant I | 356 | 1.236 |

On this 40-match experiment slice, the same `0.1` effect corresponds to 4 total
Kicktipp points. The observed gap was only 1 point.

## Resampling Analysis

The resampling pass used the existing 40 paired item scores. It did not produce
fresh predictions. For each fixture, we computed:

```text
d_i = points(o3 medium)_i - points(o3 high)_i
```

The observed paired differences were:

| Difference | Count |
| ---: | ---: |
| -2 | 2 |
| -1 | 1 |
| 0 | 32 |
| +1 | 4 |
| +2 | 1 |

Summary statistics:

| Statistic | Value |
| --- | ---: |
| Paired sample size | 40 |
| Non-zero paired differences | 8 |
| Mean difference, medium minus high | 0.025 |
| Median difference | 0.000 |
| Paired-difference standard deviation | 0.660 |
| Standard error of the mean difference | 0.104 |
| Wilcoxon signed-rank p-value | 0.8852 |
| Exact sign-flip mean-difference p-value | 1.0000 |

The bootstrap used 100,000 resamples of the paired differences with replacement,
seed `20260505`.

| Bootstrap quantity | Value |
| --- | ---: |
| 90% basic CI for mean difference | [-0.150, 0.200] |
| 95% basic CI for mean difference | [-0.175, 0.225] |
| Bootstrap mean of resampled means | 0.025 |
| Bootstrap SD of resampled means | 0.103 |
| Share of resampled means <= -0.1 | 13.5% |
| Share of resampled means >= +0.1 | 27.2% |
| Share of resampled means outside +/-0.1 | 40.8% |
| Share of resampled means inside +/-0.1 | 59.2% |

These shares are not posterior probabilities that the true effect is inside or
outside the margin. They are a practical check on how unstable the N=40 mean is
under empirical resampling.

## Equivalence Test

We tested practical equivalence using a `+/-0.1` points-per-match margin. For a
TOST-style equivalence decision at alpha `0.05`, the 90% confidence interval for
the mean paired difference must fit entirely inside `[-0.1, +0.1]`.

It does not:

| Method | 90% interval | Equivalent at +/-0.1? |
| --- | ---: | --- |
| Paired t CI | [-0.151, 0.201] | No |
| Bootstrap basic CI | [-0.150, 0.200] | No |

The paired t TOST p-value was 0.2382. This means the experiment is
inconclusive: it neither shows a statistically significant difference nor rules
out a meaningful `0.1`-point-per-match difference in either direction.

## N Orientation

Using the observed paired-difference standard deviation of 0.660, approximate
paired-t planning gives the following orientation. These are not exact
Wilcoxon/bootstrap guarantees, but they are useful scale estimates.

| Target effect or precision | Approximate N |
| --- | ---: |
| 90% CI half-width <= 0.10 around zero | 118 |
| 90% CI inside +/-0.10 if the point estimate stays near +0.025 | 210 |
| 80% two-sided paired-t power for a true 0.10 effect | 344 |
| 90% two-sided paired-t power for a true 0.10 effect | 460 |
| 80% two-sided paired-t power for a true 0.20 effect | 88 |
| 80% two-sided paired-t power for a true 0.25 effect | 57 |

The earlier back-of-the-envelope N around 950 was based on the screenshot-level
result before inspecting the actual paired differences. The exact paired data
has lower variance because 32 of the 40 items tied, but a `0.1` effect still
requires hundreds of independent fixtures for a conventional significance test.

For future experiments, N=40 is enough to notice large effects around
`0.2`-`0.25` points per match, but not enough to decide a `0.1` margin cleanly.

## Methods Reference

See [Statistical Methods for Experiment Analysis](statistical-methods-for-experiment-analysis.md)
for the references and procedure notes behind the Wilcoxon p-value, bootstrap
confidence intervals, TOST equivalence testing, paired permutation/sign-flip
checks, and N planning.

## Pages Report

The hosted comparison page is generated at:

`experiment-analysis/slices/pes-squad/all-matchdays-after-20251130t230000z/random-40-seed-20260505-prod-plus-o3-effort/random-40-seed-20260505-prod-plus-o3-effort-2026-05-04t22-49-35z.analysis.report.html`

Repo-relative link:
[random-40 analysis report](../../experiment-analysis/slices/pes-squad/all-matchdays-after-20251130t230000z/random-40-seed-20260505-prod-plus-o3-effort/random-40-seed-20260505-prod-plus-o3-effort-2026-05-04t22-49-35z.analysis.report.html)

## Decision

Do not switch production to `o3 high` based on this slice. The candidate was
slightly lower on total points, most pairings tied, the statistical test does
not indicate a reliable lift, and the equivalence test cannot rule out a
meaningful `+/-0.1` points-per-match difference.

The practical decision remains to keep `o3 medium` as the production baseline.
If high reasoning is retested, the run should either target a larger effect
threshold such as `0.2` points per match or use a substantially larger and more
carefully blocked sample.

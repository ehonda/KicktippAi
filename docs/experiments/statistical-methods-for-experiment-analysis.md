# Statistical Methods for Experiment Analysis

Date: 2026-05-05

This note collects the statistical procedures we use when interpreting
KicktippAi experiment analysis bundles. The goal is to make the report numbers
readable and to keep future experiment decisions tied to explicit effect sizes,
not only p-values.

## Data Shape

For fixed-slice model comparisons, every run predicts the same prepared match
items. That lets us analyze paired scores rather than independent samples.

For a two-run comparison, define the paired item difference as:

```text
d_i = points(run A)_i - points(run B)_i
```

Pairing matters because match difficulty is a major source of variance. A hard
fixture affects both runs, so comparing paired differences is more sensitive
than comparing two unpaired point distributions.

## Wilcoxon Signed-Rank P-Value

Primary two-run significance test:

```text
scipy.stats.wilcoxon(differences, zero_method="wilcox", alternative="two-sided", method="auto")
```

How we use it:

- Input is the per-item paired Kicktipp-point difference.
- Zero differences are excluded from the signed-rank statistic.
- The alternative is two-sided because either run may be better.
- The p-value answers whether the paired differences are surprisingly far from
  zero under the no-difference null.

Why this instead of a plain paired t-test:

- Kicktipp points are discrete and bounded.
- In our slices, ties are common.
- The signed-rank test is a conservative default for paired, non-normal score
  differences.

Reference:

- [SciPy `scipy.stats.wilcoxon`](https://docs.scipy.org/doc/scipy/reference/generated/scipy.stats.wilcoxon.html)

For experiments with more than two comparable runs, the report uses a Friedman
omnibus test followed by pairwise Wilcoxon tests with Holm correction.

References:

- [SciPy `scipy.stats.friedmanchisquare`](https://docs.scipy.org/doc/scipy/reference/generated/scipy.stats.friedmanchisquare.html)
- [statsmodels `multipletests`](https://www.statsmodels.org/stable/generated/statsmodels.stats.multitest.multipletests.html)

## Bootstrap Confidence Intervals

Bootstrap confidence intervals summarize the uncertainty around effect sizes,
especially the mean and median paired difference.

Procedure:

1. Build the paired-difference vector `d`.
2. Resample `len(d)` differences with replacement.
3. Compute the statistic on the resample, usually `mean(d)` or `median(d)`.
4. Repeat many times.
5. Convert the resampled statistic distribution into a confidence interval.

Our Python report tool uses SciPy bootstrap-style intervals with the `basic`
method. In the o3 medium vs high follow-up, we used 100,000 direct resamples of
the paired differences to inspect the `+/-0.1` points-per-match margin.

Important interpretation:

- Bootstrap does not create new evidence.
- It does not replace fresh predictions.
- It estimates how much the observed result moves when the existing paired
  sample is resampled.

Reference:

- [SciPy `scipy.stats.bootstrap`](https://docs.scipy.org/doc/scipy/reference/generated/scipy.stats.bootstrap.html)

## Equivalence Testing

Non-significant is not the same as equivalent. Equivalence testing asks whether
we can rule out effects large enough to matter.

Our practical setup:

- Pick a smallest effect size of interest before interpreting equivalence.
- For the o3 reasoning-effort comparison, use `+/-0.1` Kicktipp points per
  match.
- Run a TOST-style check: a 90% confidence interval for the paired mean
  difference must fit entirely inside `[-0.1, +0.1]`.

Why 90% for a 5% TOST:

- TOST runs two one-sided tests at alpha `0.05`.
- The equivalent confidence-interval rule uses a `1 - 2 * alpha` interval.
- At alpha `0.05`, that is a 90% interval.

References:

- [Lakens: Equivalence Tests](https://lakens.github.io/statistical_inferences/09-equivalencetest.html)
- [Lakens 2017 practical primer](https://doi.org/10.1177/1948550617697177)
- [statsmodels `ttost_paired`](https://www.statsmodels.org/stable/generated/statsmodels.stats.weightstats.ttost_paired.html)

## Paired Permutation / Sign-Flip Checks

A paired permutation test asks how unusual the observed paired difference is if
the labels are exchangeable within each pair.

For two runs on the same items:

1. Keep each fixture pair together.
2. Randomly swap run labels within each pair, or equivalently flip the sign of
   each paired difference.
3. Recompute the mean difference.
4. Compare the observed statistic to the sign-flipped distribution.

This is a useful sanity check because it respects pairing and does not require a
normality assumption. For the o3 medium vs high slice, only 8 of the 40 paired
differences were non-zero, so an exact sign-flip check over those non-zero pairs
has only `2^8 = 256` possible sign assignments.

Reference:

- [SciPy `scipy.stats.permutation_test`](https://docs.scipy.org/doc/scipy/reference/generated/scipy.stats.permutation_test.html)

## N Planning

N planning depends on what decision we want.

For significance:

```text
effect_size = target_mean_difference / sd(paired_differences)
```

Then use paired/one-sample t-test power as a scale estimate. This is only an
orientation because our primary p-value uses Wilcoxon and Kicktipp scores are
discrete.

For equivalence:

```text
90% CI half-width ~= 1.645 * sd(paired_differences) / sqrt(N)
```

To declare equivalence at margin `m`, the interval must fit inside `[-m, +m]`.
If the point estimate is not exactly zero, the available half-width is smaller
than `m`.

References:

- [statsmodels `TTestPower`](https://www.statsmodels.org/stable/generated/statsmodels.stats.power.TTestPower.html)
- [statsmodels power user guide](https://www.statsmodels.org/stable/stats.html#power-and-sample-size-calculations)

## Practical Reading Order

For this repository, read the methods in this order:

1. Wilcoxon p-value: did this slice show a clear difference?
2. Bootstrap CI: how wide is the plausible paired mean-difference range?
3. Equivalence test: can we rule out a meaningful effect such as `+/-0.1`?
4. N planning: is a larger run likely to answer the actual decision question?

For the o3 medium vs high experiment, the result is:

- not significant by Wilcoxon,
- not equivalent at `+/-0.1`,
- too noisy at N=40 for a `0.1` decision,
- still enough to say there is no observed reason to pay for high reasoning in
  production.

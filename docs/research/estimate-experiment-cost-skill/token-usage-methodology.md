# Token Usage Methodology

This note applies to experiments that compare total input and output token usage between slice datasets and repeated-match datasets.

## Usage Extraction

- Usage is gathered from the `predict-match` Langfuse generation observation.
- [analyze_token_usage.py](analyze_token_usage.py) writes a compact usage JSON that excludes prompt bodies and model outputs. It keeps only the run/item identity plus token and cost fields needed for reproducible comparisons.
- The script uses the Langfuse v2 observations list endpoint by default, filtered by `sessionId`, observation `name = predict-match`, and `type = GENERATION`.
- Avoid one `GET /api/public/traces/{traceId}` detail call per item. That path fetches bulky trace content and hits a tight rate-limit bucket. The Orchestrator exporter already follows the same faster pattern by batching observations per run by `sessionId`, as documented in [docs/langfuse/experiments/analyzing-experiments.md](../../langfuse/experiments/analyzing-experiments.md).

## Dataset Construction

- Slice datasets use `prepare-slice` with `--sample-size`, `--sample-seed`, and `--starts-after`.
- When a repeated-match experiment needs random fixtures after a cutoff and `prepare-repeated-match` does not support that selection directly, first prepare a random slice from the same cutoff pool, then use the selected fixture or fixtures for `prepare-repeated-match`.
- For total input/output token comparisons, prepare exactly `N` repeated-match items. Do not create an extra warmup item unless cached input, uncached input, or monetary cost is part of the research question.
- When estimating slice-like total token usage from repeated-match datasets, prefer `M` randomly selected repeated fixtures of size `S`, with `N = M * S`, instead of one repeated fixture.

## Statistical Test

Primary test: two-sided exact permutation test over difference in means.

Procedure:

- Test statistic: `mean(slice) - mean(repeated measured)` for each token metric.
- Null hypothesis: if dataset construction type has no effect on the mean token count, then the observed token values are exchangeable between the two labels.
- Exact calculation: combine the two samples, enumerate every distinct way to assign `n_left` observations to the left group and `n_right` to the right group, compute the difference in means for each relabeling, and count the share where `abs(permuted_difference) >= abs(observed_difference)`.
- For integer token counts, the script uses dynamic programming over subset sums. This is equivalent to enumerating all label partitions, but avoids materializing very large permutation sets.
- Bootstrap confidence intervals use 30,000 seeded percentile resamples. They are descriptive intervals around the observed mean difference; the significance decision comes from the exact permutation p-value.

Why this is the right test:

- The samples are independent, unpaired observations from two dataset construction workflows. We are not comparing the same fixture across two model configurations, so a paired test is not appropriate.
- Token counts are discrete, skew-prone, and can have very different variances for input and output. The permutation test does not require normality or equal variances in the way a two-sample t-test would.
- The question is specifically about a mean token-count difference, so difference in means is the statistic to permute.
- The test is exact for the observed integer token counts under the exchangeability null. The main caveat is design-level, not test-level: repeated items within the same fixture share prompt context, so fixture selection must be handled carefully.

Source material:

- [SciPy `permutation_test` documentation](https://docs.scipy.org/doc/scipy/reference/generated/scipy.stats.permutation_test.html) describes independent-sample permutation tests, exact tests when all distinct partitions are used, and two-sided alternatives.
- [NIST Dataplot Fisher two sample randomization test](https://www.itl.nist.gov/div898/software/dataplot/refman1/auxillar/fishrand.htm) describes equality-of-means randomization tests by all label assignments and p-values from the permutation distribution.
- [NIST Dataplot two sample permutation test](https://www.itl.nist.gov/div898/software/dataplot/refman1/auxillar/permtest.htm) describes combining samples, permuting labels, and comparing the observed statistic to the reference distribution without distributional assumptions.

# First Experiment Run Design

## Status

Accepted for the initial Task 5 implementation direction.

## Scope

This document revisits the run design for the first Bundesliga 2025/2026 experiment in Langfuse.

It focuses on the question that emerged from the current Task 5 implementation: how should we model dataset runs, scores, repetitions, and slices so Langfuse can natively support the experiment views we actually care about?

This decision applies to the first sampled match experiment and should also guide nearby follow-up work on model, prompt, and slice comparisons.

For the concrete naming, metadata, and score contract that should guide implementation, see [first-experiment-run-contract.md](first-experiment-run-contract.md).

## Initial Problem

The current Task 5 implementation spread repetitions across multiple Langfuse dataset runs, one run per repetition, under a shared run-family name.

That workaround improved per-repetition visibility, but it also introduced a structural mismatch with Langfuse's comparison model:

- Langfuse's native comparison views and averages operate at the dataset-run level
- Our real comparison target for Task 5 is usually a model or prompt variant on one fixed slice
- When repetitions become the primary dataset runs, the UI compares repetitions instead of the variant-level result we want
- This weakens the value of native Langfuse averages and side-by-side compare views for the first milestone

In short: the current workaround optimizes for repetition visibility, but not for the native comparison scope that matters most for Task 5.

## Research Findings

### 1. Langfuse's native experiment unit is a dataset run

Langfuse models a hosted experiment as one dataset run over dataset items, with dataset run items linking dataset items to traces.

Sources:

- [Data Model](https://langfuse.com/docs/evaluation/experiments/data-model)
- [Experiments via SDK](https://langfuse.com/docs/evaluation/experiments/experiments-via-sdk)
- [Core Concepts](https://langfuse.com/docs/evaluation/core-concepts)

Implication:

- One dataset run should represent one unit that we want to compare in Langfuse's run tables and compare views

### 2. Hosted datasets are the right foundation for Task 5

Langfuse's SDK documentation explicitly recommends the experiment runner for hosted datasets and states that hosted datasets automatically create dataset runs that can be inspected and compared in the UI.

Source:

- [Experiments via SDK](https://langfuse.com/docs/evaluation/experiments/experiments-via-sdk)

Implication:

- The canonical hosted dataset remains the correct substrate for the first experiment

### 3. Run-level scores are supported and are the right place for aggregate metrics

Langfuse supports dataset-run-level scores. This feature was added specifically to support overall experiment metrics such as precision, recall, and F1.

Sources:

- [Dataset Run Level Scores](https://langfuse.com/changelog/2025-05-07-run-level-scores)
- [Data Model](https://langfuse.com/docs/evaluation/experiments/data-model)
- [Experiments via SDK](https://langfuse.com/docs/evaluation/experiments/experiments-via-sdk)

GitHub clarification:

- Langfuse maintainers confirmed that run-level scores are supported, but Langfuse still does not natively calculate metrics such as precision, recall, or F1 for you; users compute them and post them as run-level scores: [langfuse/langfuse#2512](https://github.com/langfuse/langfuse/issues/2512)

Implication:

- Task 5 should compute aggregate `kicktipp_points` and supporting metrics itself and attach them to the dataset run as first-class run-level scores

### 4. Compare-view filtering has improved, but it is still run-centric

Langfuse added compare-view filters in late 2025. Those filters support narrowing experiment comparisons by evaluator scores, cost, latency, and similar metrics.

Source:

- [Filters in Compare View](https://langfuse.com/changelog/2025-11-07-compare-view-filters)

Implication:

- Compare view is more useful than before, but it still compares dataset runs, not run families

### 5. Dashboards are stronger than score analytics for slice-oriented analysis

Custom Dashboards support filtering by metadata, model parameters, tags, score thresholds, and other dimensions. Score Analytics is useful for score-centric analysis but is narrower.

Sources:

- [Custom Dashboards](https://langfuse.com/docs/metrics/features/custom-dashboards)
- [Score Analytics](https://langfuse.com/docs/evaluation/evaluation-methods/score-analytics)

Implication:

- Slice and cohort analysis across many runs should lean on dashboards and metadata conventions, not only on the dataset compare table

### 6. Repetition and cross-run statistics are still a product gap

The docs and surfaced GitHub discussions show that run-level scoring is available, but cross-run statistical aggregation for repeated experiments remains a known gap.

Sources:

- [Support for Statistical Aggregation (mean, variance...) across dataset runs](https://github.com/orgs/langfuse/discussions/10925)
- [langfuse/langfuse#2512](https://github.com/langfuse/langfuse/issues/2512)

Implication:

- We should not expect Langfuse to natively aggregate repeated dataset runs into one first-class comparison row today

### 7. Dataset version pinning is not yet first-class

Langfuse docs state that experiments currently run on the latest dataset version at experiment time.

Sources:

- [Experiments via SDK](https://langfuse.com/docs/evaluation/experiments/experiments-via-sdk)
- [Experiments via UI](https://langfuse.com/docs/evaluation/experiments/experiments-via-ui)

Implication:

- Stable benchmark setups should use explicit frozen slices rather than relying on the mutable canonical dataset alone

## Alternatives

### 1. One run per variant on one fixed slice

Definition:

- Keep one canonical hosted dataset for completed match items
- Select one fixed subset of dataset item IDs for the experiment
- Run one dataset run per variant, where a variant is a specific combination such as model, prompt version, timestamp policy, and sample definition
- Attach item-level scores to traces and aggregate scores to the dataset run

Pros:

- Best fit for Langfuse's native compare view and dataset-run averages
- One compare-row equals one unit we actually care about
- Simple mental model for model-vs-model and prompt-vs-prompt comparisons
- Keeps the canonical dataset match-centric

Cons:

- Does not make repeated executions of the same item a first-class native concept

Assessment:

- This is the right default design for the initial Task 5 implementation

### 2. Canonical dataset plus separate repetition runs as secondary analysis

Definition:

- Keep Alternative 1 as the primary experiment design
- If stochastic robustness is needed, run extra repetitions as additional dataset runs with shared family metadata such as `experimentFamily`, `sampleHash`, and `rep`
- Aggregate those repetition families outside the core compare table, for example in wrapper output or dashboards

Pros:

- Preserves the native Langfuse comparison model for the main experiment view
- Still allows variance inspection later

Cons:

- Repetition-family statistics are still not first-class in Langfuse

Assessment:

- This is a sensible future improvement, but it is not required for the current Task 5 milestone

### 3. Repetition-expanded shadow dataset

Definition:

- Create a dedicated hosted shadow dataset for repetition studies
- Expand one logical match item into synthetic repetition items such as `matchId::rep-01`, `matchId::rep-02`, and so on
- Run one dataset run per variant on that repetition-expanded dataset

Pros:

- Produces one native dataset-run row whose averages already include all repetitions
- Uses Langfuse's built-in run-level comparison model instead of fighting it
- Avoids the run-family aggregation problem for fixed-repetition experiments

Cons:

- Semantically artificial compared with the canonical match-centric dataset
- Requires deliberate stable ID handling because dataset item IDs are project-global
- Best treated as a special-purpose dataset, not the canonical one

Assessment:

- If we want an experiment of the form "run `n` repetitions for a fixed match or fixed sample and compare the averaged outcome natively inside Langfuse", this is the way to go

### 4. Frozen slice datasets for stable benchmarks

Definition:

- Create dedicated hosted datasets for benchmark slices, for example a fixed random sample or another named cohort
- Keep those slices stable over time instead of sampling dynamically from the mutable canonical dataset each time

Pros:

- Strong reproducibility
- Clean benchmark semantics in Langfuse
- Avoids the current lack of dataset version pinning for important recurring comparisons

Cons:

- More dataset objects to manage
- Better for benchmark suites than for one-off exploratory runs

Assessment:

- This is the recommended pattern for stable benchmark setups

## Recommendation

### Task 5 now

Use Alternative 1 for the first real sampled experiment:

- Keep the canonical hosted dataset as the source of truth
- Draw one fixed sample from that dataset for the experiment
- Run one dataset run per variant on that fixed sample
- Attach item-level Kicktipp scores to traces
- Attach aggregate Kicktipp scores to the dataset run
- Store the sample definition in run metadata so compared runs are guaranteed to refer to the same slice

This gives Task 5 the strongest native Langfuse support today.

### Fixed-match or fixed-sample repetition experiments

If we later want a true "`n` repetitions for a fixed match" or "`n` repetitions for a fixed slice" experiment and want Langfuse's native averages to reflect those repetitions inside one comparable run, use Alternative 3.

That shadow-dataset approach is the cleanest fit for Langfuse's current data model.

### Stable benchmark setups

For recurring benchmark suites, use Alternative 4.

Frozen benchmark slice datasets are the best way to get reproducible long-lived comparisons until Langfuse offers first-class dataset version pinning.

### Not needed right now

Alternative 2 remains a future improvement.

It may become useful once we intentionally revisit stochastic variance studies, but it should not shape the initial Task 5 implementation.

## Task 5 Implementation Guidance

The initial Task 5 run design should treat the compared variant as the primary dataset-run unit.

Recommended run metadata fields:

- `model`
- `promptVersion` or equivalent prompt-build identifier
- `evaluationTimestampPolicy`
- `sampleSize`
- `sampleSeed` when sampling is seeded
- `sampleHash`
- `selectedItemIdsHash`
- `sliceKind`, for example `random-sample` or `benchmark-slice`

Recommended aggregate run-level scores:

- `avg_kicktipp_points`
- `exact_hit_rate`
- `outcome_correct_rate`
- `mean_home_goal_error`
- `mean_away_goal_error`
- `mean_goal_difference_error`

Recommended operational rule:

- Any runs meant to be compared in Langfuse must share the same selected dataset items

## Follow-up Notes

- Task 5 should stop using the current per-repetition-run workaround as the primary design
- Task 6 should pick up only the later enhancements that remain useful after this design decision, especially stable benchmarks and richer automation

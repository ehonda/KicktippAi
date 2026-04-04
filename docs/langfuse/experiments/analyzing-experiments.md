# Analyzing Experiments

## Current Status

Analysis tooling is not implemented yet as a first-class workflow in the repository.

What is implemented today is the score production layer:

- trace-level `kicktipp_points`
- dataset-run-level `total_kicktipp_points`
- dataset-run-level `avg_kicktipp_points`

That means we can already execute comparable experiment runs and inspect the resulting scores in Langfuse, but we do not yet have a dedicated command or report pipeline for statistical comparison.

## What We Do Today

Today, experiment analysis is a manual or semi-manual process.

Typical workflow:

1. Run multiple models or prompt variants on the same fixed slice or repeated-match dataset.
2. Inspect dataset runs, traces, and score distributions in the Langfuse UI.
3. If needed, query run-level scores through the Langfuse public API.

Important repository-specific notes:

- run-level metrics are retrieved reliably through Langfuse `v2/scores`
- trace-level score retrieval and metadata filtering have some quirks documented in `docs/langfuse.md`

## Planned Direction

The planned analysis work is tracked in:

- `plans/langfuse-integration/phase-2/tasks/further-improvement/02-statistical-evaluation-and-analysis-tooling.md`

The current direction is:

- keep score generation in the application and Langfuse
- normalize comparable experiment data into a shared analysis bundle
- perform the actual statistical analysis in Python

## Planned Analysis Contract

The future tooling is expected to normalize comparable runs into a flat bundle that includes at least:

- run identity
- model and prompt identity
- slice identity
- source item identity
- prepared dataset item identity
- trace identity
- predicted scoreline
- expected scoreline
- item-level Kicktipp points
- run-level aggregate scores

The point of that bundle is to make downstream statistics independent from Langfuse's raw API object shapes.

## Planned Statistical Methods

For fixed-slice comparisons, the planned default methods are:

- paired permutation test or Wilcoxon signed-rank test for two-run comparisons
- effect sizes alongside significance results
- Friedman test plus corrected pairwise comparisons for more than two comparable runs

The analysis plan explicitly does not treat a plain paired t-test as the default because Kicktipp points are discrete and bounded.

## Likely Implementation Shape

The current recommendation is:

1. define a shared normalized analysis contract in .NET
2. export or emit that contract from experiment data
3. consume that contract from Python for statistics and reporting

That keeps the experiment runner and the analysis layer loosely coupled.

## Until Then

Until dedicated analysis tooling exists, treat Langfuse as the inspection surface and the source of recorded run scores, but not yet as the place where statistical significance is determined.

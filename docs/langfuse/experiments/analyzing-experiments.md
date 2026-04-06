# Analyzing Experiments

## Current Status

Analysis tooling is partially implemented as a first-class workflow in the repository.

What is implemented today is the score production layer:

- trace-level `kicktipp_points`
- dataset-run-level `total_kicktipp_points`
- dataset-run-level `avg_kicktipp_points`

What is also implemented now:

- `export-experiment-analysis` to export comparable Langfuse runs into one normalized JSON bundle
- a Python report command to run Wilcoxon/Friedman-based statistical comparisons from that normalized bundle

That means we can now execute comparable experiment runs, inspect the resulting scores in Langfuse, export a stable analysis input bundle, and generate a machine-readable JSON report plus a human-readable Markdown summary.

## What We Do Today

Today, experiment analysis is a manual or semi-manual process.

Typical workflow:

1. Run multiple models or prompt variants on the same fixed slice or repeated-match dataset.
2. Inspect dataset runs, traces, and score distributions in the Langfuse UI.
3. Export a normalized bundle with `export-experiment-analysis`.
4. Generate a statistical report from that bundle with `uv run experiment-analysis-report`.
5. If needed, query run-level scores through the Langfuse public API.

Example export command:

```powershell
dotnet run --project src/Orchestrator -- export-experiment-analysis --dataset-name match-predictions/bundesliga-2025-26/pes-squad/slices/all-matchdays/random-16-seed-20260403 --run-names "slice__pes-squad__o3__prompt-v1__random-16-seed-20260403__startsat-12h__2026-04-03t12-00-00z,slice__pes-squad__gpt-5-nano__prompt-v1__random-16-seed-20260403__startsat-12h__2026-04-03t12-00-00z"
```

Example report command:

```powershell
uv run experiment-analysis-report --input artifacts/langfuse-experiments/analysis/match-predictions/bundesliga-2025-26/pes-squad/slices/all-matchdays/random-16-seed-20260403/2026-04-03t12-00-00z.analysis.json
```

Important repository-specific notes:

- run-level metrics are retrieved reliably through Langfuse `v2/scores`
- trace-level score retrieval and metadata filtering have some quirks documented in `docs/langfuse.md`
- the export command uses dataset run -> dataset run items -> dataset items -> trace detail joins and does not rely on `v2/scores` trace filtering

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

The current repository workflow is:

1. define a shared normalized analysis contract in .NET
2. export that contract from Langfuse-backed experiment data via `export-experiment-analysis`
3. consume that contract from Python via `experiment-analysis-report` for statistics and reporting

That keeps the experiment runner and the analysis layer loosely coupled.

## Report Outputs

The Python report command currently produces:

- a JSON report with ranked runs, pairwise outcome counts, Wilcoxon results, bootstrap confidence intervals, and corrected pairwise comparisons
- a Markdown summary with the same comparison information in a review-friendly format

Two-run bundles are reported with:

- primary metric deltas
- paired Wilcoxon signed-rank results
- effect-size confidence intervals for mean and median paired Kicktipp-point differences
- per-item win/tie/loss counts

Three-or-more-run bundles are reported with:

- Friedman omnibus test results
- corrected pairwise Wilcoxon comparisons
- per-item win/tie/loss counts for each run ordering

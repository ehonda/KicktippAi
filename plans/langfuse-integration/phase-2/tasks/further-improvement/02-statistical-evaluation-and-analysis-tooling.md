# Further Improvement — Statistical Evaluation And Analysis Tooling

## Status

Deferred

## Context

Task 5 now emits the score set that later statistical evaluation should build on:

- Trace-level `kicktipp_points`
- Dataset-run-level `total_kicktipp_points`
- Dataset-run-level `avg_kicktipp_points`

The next gap is not score generation anymore, but reliable retrieval and normalization of comparable run data so fixed-slice experiment variants can be analyzed with common statistical tests.

## Verified Langfuse Findings

Verified on `2026-04-04` against live Task 5 experiment data.

- Langfuse UI comparison views and Score Analytics are useful for inspection, distribution checks, and correlation or agreement metrics, but they do not provide paired significance testing between experiment runs.
- The earlier apparent "missing aggregate scores" problem was an endpoint-version issue, not an ingestion failure.
- `GET /api/public/v2/scores?datasetRunId=<datasetRunId>` returned the expected run-level `avg_kicktipp_points` and `total_kicktipp_points` entries for a verified Task 5 slice run.
- The older `GET /api/public/scores` path returned trace-level `kicktipp_points` entries but did not surface the run-level aggregate scores for that same dataset run.
- `GET /api/public/v2/scores?name=kicktipp_points` returns trace-level scores with `traceId` and `observationId`, but the top-level `datasetRunId` is `null` for those scores.
- In the current Task 5 runner, trace-level score metadata duplicates the run linkage into `metadata.datasetRunId`, `metadata.datasetItemId`, and `metadata.sourceDatasetItemId`. That duplication is useful for our local tooling, but it is a runner-specific convenience rather than the Langfuse-native parent relation.
- `GET /api/public/datasets/{datasetName}/runs/{runName}` still does not return aggregate score payloads. Run-level score retrieval remains a separate `v2/scores` call.

## Practical Consequences

- Use `v2/scores` for any dataset-run-level metric verification or export.
- Do not use the older `/scores` path to decide whether run-level metrics were ingested.
- For per-item paired analysis, do not assume `datasetRunId` filtering alone will retrieve trace-level scores through Langfuse's native model.
- For portable tooling, the stable retrieval path is: dataset run -> dataset run items -> trace IDs -> trace-level scores.
- For current Task 5 runs, a simpler short-term path is acceptable because the runner already duplicates run linkage and match linkage into score metadata.

## Recommended Analysis Architecture

### Short-Term Direction

Use Python for the actual statistics layer, but keep the input contract language-neutral.

- Python is the best fit for the statistical work because the ecosystem already has the right libraries for paired tests, multiple-comparison correction, and confidence intervals.
- The Python tooling can leverage the Langfuse Python SDK where it makes authentication and object retrieval simpler.
- Keep a direct public-API fallback for score queries, especially around `v2/scores`, because dataset-run and trace-level score retrieval use different access patterns.
- Treat the Python layer as the statistics consumer, not as the only source of truth for experiment structure.

### Normalized Analysis Bundle

Future tooling should converge on one flat analysis bundle per comparison set. At minimum, each row should capture:

- `datasetRunId`
- `runName`
- `model`
- `promptKey`
- `sliceKey`
- `sourcePoolKey`
- `sourceDatasetItemId`
- `datasetItemId`
- `traceId`
- `observationId`
- `kicktippPoints`
- `predictedHomeGoals`
- `predictedAwayGoals`
- `expectedHomeGoals`
- `expectedAwayGoals`

The bundle should also include run-level metadata and aggregates separately so Python does not need to rediscover them.

## Dotnet Options For Cleaner Analysis

### Option 1 — Read-Only Langfuse Export Command

Add a `.NET` command that reads experiment data from Langfuse and writes a normalized JSON or CSV analysis bundle.

What it would do:

- Query run-level aggregate scores from `v2/scores`
- Query dataset run items for the run being analyzed
- Resolve trace-level scores per item
- Materialize one flat artifact for later Python analysis

Benefits:

- Keeps the analysis input contract stable even if the runner later moves from JS to `.NET`
- Works for already completed historical runs
- Encapsulates Langfuse API quirks in one place

Costs:

- Requires explicit pagination and join logic
- Still depends on read-after-write consistency and public API behavior

### Option 2 — Emit Analysis Artifacts During Experiment Execution

Have the runner write the normalized per-item and per-run analysis bundle locally as part of the experiment flow.

What it would do:

- Persist the exact per-item score rows already known at run time
- Persist the run-level aggregate scores after they are posted
- Store the artifact next to the existing slice artifacts

Benefits:

- Avoids later API joins for the runs created by our tooling
- Preserves the exact analysis inputs used at execution time
- Simplifies later Python work substantially

Costs:

- Only helps for runs created after the artifact contract exists
- Still needs a separate retrieval path for older or external runs

### Option 3 — Introduce A Shared `.NET` Analysis Contract Layer

Define a small `.NET` model layer for experiment-comparison bundles without committing to `.NET` as the statistics engine.

What it would do:

- Create stable DTOs for per-item rows and run-level summaries
- Support both Option 1 and Option 2 with the same artifact shape
- Give future `.NET` commands and future Python tooling the same input contract

Benefits:

- Reduces coupling between runner language and analysis language
- Makes a later runner rewrite easier
- Lets the Python layer stay focused on statistics instead of Langfuse object-shape cleanup

Costs:

- Adds a small amount of up-front design work before the analysis tool exists

## Recommended Dotnet Direction

Near term:

- Add the shared `.NET` analysis contract layer
- Prefer either a read-only export command or an emitted local artifact, depending on which implementation is simpler in the next session
- Keep the actual statistical tests in Python

Medium term:

- If the experiment runner moves into `.NET`, preserve the same normalized artifact shape so the Python analysis layer does not need to change

## Statistical Methods To Target

For fixed-slice comparisons where the same dataset items are scored in each run:

- Use a paired permutation test or Wilcoxon signed-rank test as the primary two-run significance check
- Report effect sizes alongside significance: mean difference, median difference, bootstrap confidence interval, and win or tie or loss counts
- For more than two comparable runs on the same slice, use a Friedman test followed by corrected pairwise comparisons
- Do not treat a plain paired t-test as the default primary test because Kicktipp points are discrete and bounded

## Python Tooling Prerequisites

The local machine checked in this session is not ready for modern Python work yet.

- `python` and `py` currently resolve to `3.6`
- `uv` is not installed
- `winget` is available

When the Python analysis tooling is started in a fresh session, the intended baseline should be:

- Python `3.13`
- `uv`
- `langfuse`
- `pandas`
- `scipy`
- `statsmodels`

The Python tooling should use the Langfuse Python SDK where it is ergonomic, with direct `v2/scores` HTTP calls as the fallback when score-query behavior is clearer or more complete via the public API.

## Next Session Entry Criteria

Before planning the concrete Python tool, decide:

1. Whether the first implementation should read only from Langfuse APIs, only from emitted local artifacts, or from a hybrid export path
2. Whether the first tool should rely on the current Task 5 score metadata shortcut or only on the portable dataset-run-item plus trace-score joins
3. What the normalized analysis bundle schema should be

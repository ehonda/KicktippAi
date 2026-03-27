# Task 5 — First Experiment

## Status

In progress

## Objective

Run the first real Bundesliga 2025/2026 match experiment using the canonical hosted dataset, configurable sampling, timestamp-based context reconstruction, and Kicktipp-centric scoring.

## Why This Comes Fifth

This is the first milestone payoff task. It depends on the data contract, prompt reconstruction, runner choice, and hosted dataset all being stable.

The runner choice is now stable for the first milestone: use JS/TS unless a later session deliberately modernizes the local Python environment and decides to migrate.

## Required Outputs

- A runnable first experiment flow
- Configurable sample size and evaluation timestamp policy
- Item-level scores and run-level aggregate scores in Langfuse
- Verification notes showing the run is visible and analyzable in the UI

## Planned Work

1. Select a sample from the hosted dataset
2. Choose an evaluation timestamp policy such as `startsAt - 7 days`
3. Reconstruct the required context-document set at that timestamp
4. Run predictions for the sample
5. Attach Kicktipp-based scores and supporting diagnostics
6. Verify trace linkage and dataset-run visibility in Langfuse

## Inputs

- Results of [02-task-01-data-foundation.md](done/02-task-01-data-foundation.md)
- Results of [03-task-02-prompt-reconstruction.md](done/03-task-02-prompt-reconstruction.md)
- Results of [04-task-03-runner-spike.md](done/04-task-03-runner-spike.md)
- Results of [05-task-04-dataset-sync.md](done/05-task-04-dataset-sync.md)
- [../first-experiment-run-design.md](../first-experiment-run-design.md)
- [../first-experiment-run-contract.md](../first-experiment-run-contract.md)
- [buli-25-26-experiments.md](../buli-25-26-experiments.md)
- [../dataset-contract-and-reconstruction-spec.md](../dataset-contract-and-reconstruction-spec.md)
- `tools/langfuse-runner-spike/`

## Runner Stack Locked For This Task

- Use the JS/TS Langfuse SDK for the first experiment implementation
- Reuse the flushing pattern from the Task 3 spike so short-lived local experiment runs reliably deliver traces before process exit
- Promote the Task 3 local-data flow to hosted-dataset execution rather than rewriting the runner shape from scratch

## Manual Steps

Use [manual-steps.md](manual-steps.md#task-5--first-experiment) during implementation.

## Scoring Expectations

Primary score:

- `kicktipp_points`

Supporting scores:

- `exact_hit`
- `outcome_correct`
- `home_goal_error`
- `away_goal_error`
- `goal_difference_error`

Run-level aggregates should summarize the primary score and the main supporting metrics.

## Completion Criteria

- A sampled experiment run completes successfully
- The run appears in Langfuse as a dataset run
- Item-level and run-level scores are visible
- Reconstruction follows the agreed timestamp-based rule
- The scoring matches manual expectations on checked examples

## Current Implementation Notes

- Exact-time export and prompt reconstruction are implemented through `.NET` commands that accept NodaTime's invariant `ZonedDateTime` `G` pattern, for example `2026-03-15T12:00:00 Europe/Berlin (+01)`
- The experiment wrapper lives in `.github/copilot/skills/langfuse-experiment-runner/` and orchestrates export plus JS execution for one or more models
- The JS runner emits progress output during warm-up and batching, verifies dataset runs via the Langfuse API, and returns aggregate scores plus run details
- The Langfuse OTEL span processor is configured with the `x-langfuse-ingestion-version: 4` header

## Current Design Decision

- Follow [../first-experiment-run-design.md](../first-experiment-run-design.md) as the source of truth for Task 5 run modeling
- Follow [../first-experiment-run-contract.md](../first-experiment-run-contract.md) as the source of truth for Task 5 run naming, metadata, and score identifiers
- The primary Task 5 unit is now one dataset run per comparable variant on one fixed slice of hosted dataset items
- Compared variants must share the same selected dataset items so Langfuse-native compare views and averages stay meaningful
- Item-level Kicktipp scores stay attached to traces and aggregate metrics should be attached to the dataset run as run-level scores

## Deferred Repetition Work

- Repetition-family analysis is not part of the initial Task 5 direction anymore
- If we later need "`n` repetitions for a fixed match" or "`n` repetitions for a fixed slice" with native Langfuse averages, use the repetition-expanded shadow-dataset approach documented in [../first-experiment-run-design.md](../first-experiment-run-design.md)
- Any future repetition-family aggregation over multiple dataset runs belongs in Task 6 or later follow-up work

## Handoff Notes

Task 4 populated the canonical hosted dataset:

- Dataset name: `match-predictions/bundesliga-2025-26/pes-squad`
- Current hosted dataset size from the full completed `pes-squad` slice: `235` items as of `2026-03-21`
- Dataset ID in Langfuse: `cmn0ycdfb0001ad0767gcvfey`
- Sync behavior: full-scope export from .NET plus JS hosted sync with schema-enforced dataset definition and stable-ID item skipping when canonical content is unchanged
- Recommended pre-experiment refresh: re-run the full hosted sync first so any newly completed matches are available before sampling

When complete, update [07-task-06-follow-up-evaluation.md](07-task-06-follow-up-evaluation.md) with what should be automated or expanded next.

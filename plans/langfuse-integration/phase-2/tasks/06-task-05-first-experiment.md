# Task 5 — First Experiment

## Status

Blocked by Tasks 2-4

## Objective

Run the first real Bundesliga 2025/2026 match experiment using a hosted Langfuse dataset, configurable random sampling, and Kicktipp-centric scoring.

## Why This Comes Fifth

This is the first milestone payoff task. It depends on the data contract, prompt reconstruction, runner choice, and hosted dataset all being stable.

## Required Outputs

- A runnable first experiment flow
- Configurable sample size and run metadata
- Item-level scores and run-level aggregate scores in Langfuse
- Verification notes showing the run is visible and analyzable in the UI

## Planned Work

1. Select a random sample from the hosted dataset or from a synced subset
2. Run predictions for the sample
3. Attach Kicktipp-based scores and supporting diagnostics
4. Verify trace linkage and dataset-run visibility in Langfuse

## Inputs

- Results of [02-task-01-data-foundation.md](done/02-task-01-data-foundation.md)
- Results of [03-task-02-prompt-reconstruction.md](03-task-02-prompt-reconstruction.md)
- Results of [04-task-03-runner-spike.md](04-task-03-runner-spike.md)
- Results of [05-task-04-dataset-sync.md](05-task-04-dataset-sync.md)
- [buli-25-26-experiments.md](../buli-25-26-experiments.md)

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
- The scoring matches manual expectations on checked examples

## Handoff Notes

When complete, update [07-task-06-follow-up-evaluation.md](07-task-06-follow-up-evaluation.md) with what should be automated or expanded next.

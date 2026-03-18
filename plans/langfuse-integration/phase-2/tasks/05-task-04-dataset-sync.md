# Task 4 — Hosted Dataset Sync

## Status

Ready

## Objective

Create the hosted Langfuse dataset flow for the first milestone, including stable item IDs, idempotent synchronization, and enough metadata for filtering and reproducibility.

## Why This Comes Fourth

The hosted dataset is the substrate for the first real experiment run. This task depends on the data contract, reconstruction format, and chosen runner environment being stable.

## Required Outputs

- A hosted dataset naming strategy
- A deterministic item-ID scheme
- An idempotent upload or sync path
- Verification that synced items are usable for experiment runs in Langfuse

## Planned Work

1. Define the dataset name and lifecycle for the first milestone
2. Implement create-or-update behavior for dataset items
3. Validate the hosted dataset in Langfuse
4. Record operational notes for re-syncs and incremental updates

## Inputs

- Results of [02-task-01-data-foundation.md](done/02-task-01-data-foundation.md)
- Results of [03-task-02-prompt-reconstruction.md](done/03-task-02-prompt-reconstruction.md)
- Results of [04-task-03-runner-spike.md](done/04-task-03-runner-spike.md)
- `src/Orchestrator/Commands/Observability/ExportExperimentItem/`
- `tools/langfuse-runner-spike/`

## Runner Stack Locked For This Task

- Use JS/TS for the first hosted-dataset sync implementation
- Reuse the .NET `export-experiment-item` seam as the starting materialization pattern for hosted dataset items
- Preserve the stable item ID from the exported dataset item when moving from local spike artifacts to hosted dataset records

## Manual Steps

Use [manual-steps.md](manual-steps.md#task-4--hosted-dataset-sync) during implementation.

## Open Questions To Resolve In This Task

- Should sampling be handled by the runner over the full hosted dataset, or by creating temporary sampled datasets for the first milestone?
- What metadata is essential for filtering in the Langfuse UI from day one?

## Completion Criteria

- The hosted dataset exists in Langfuse
- Items are synced idempotently with stable IDs
- The dataset is ready for the first experiment task without structural changes

## Handoff Notes

Document the final dataset name and sync behavior in [06-task-05-first-experiment.md](06-task-05-first-experiment.md) before handoff.

The first implementation should avoid re-opening the runner-language question unless the local Python environment has been upgraded intentionally and that change is worth the migration cost.

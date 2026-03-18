# Task 4 — Hosted Dataset Sync

## Status

In progress

## Objective

Create the canonical hosted Langfuse dataset flow for the first milestone, using stable match-centric item IDs, idempotent synchronization, and only the match-centric metadata needed for filtering.

## Why This Comes Fourth

The hosted dataset is the substrate for the first real experiment run. This task depends on the canonical match dataset contract, timestamp-based reconstruction rules, and chosen runner environment being stable.

## Required Outputs

- A hosted dataset naming strategy
- A deterministic match-centric item-ID scheme
- An idempotent upload or sync path
- Verification that synced items are usable for experiment runs in Langfuse

## Planned Work

1. Define the canonical hosted dataset contract and lifecycle for the first milestone
2. Implement create-or-update behavior for hosted match items
3. Validate the hosted dataset in Langfuse
4. Record operational notes for re-syncs and incremental updates

## Inputs

- Results of [02-task-01-data-foundation.md](done/02-task-01-data-foundation.md)
- Results of [03-task-02-prompt-reconstruction.md](done/03-task-02-prompt-reconstruction.md)
- Results of [04-task-03-runner-spike.md](done/04-task-03-runner-spike.md)
- `src/Orchestrator/Commands/Observability/ExportExperimentDataset/`
- `tools/langfuse-runner-spike/`
- [../dataset-contract-and-reconstruction-spec.md](../dataset-contract-and-reconstruction-spec.md)

## Runner Stack Locked For This Task

- Use JS/TS for the first hosted-dataset sync implementation
- Reuse the .NET export/materialization seam as the source for canonical hosted dataset items
- Use persisted Kicktipp match identity (`TippSpielId`) as the basis for hosted item IDs instead of model-specific export IDs

## Manual Steps

Use [manual-steps.md](manual-steps.md#task-4--hosted-dataset-sync) during implementation.

## Decisions Locked In This Task

- Sampling is handled by the runner over the full hosted dataset
- Hosted dataset items are match-centric, not historical-prediction-centric
- Hosted dataset metadata is limited to match-centric filtering fields and does not include replay metadata
- Context reconstruction for later experiments is timestamp-based and documented in [../dataset-contract-and-reconstruction-spec.md](../dataset-contract-and-reconstruction-spec.md)

## Completion Criteria

- The hosted dataset exists in Langfuse
- Items are synced idempotently with stable match-centric IDs
- The dataset is ready for the first experiment task without structural changes

## Handoff Notes

Document the final dataset name and sync behavior in [06-task-05-first-experiment.md](06-task-05-first-experiment.md) before handoff.

The first implementation should avoid re-opening the runner-language question unless the local Python environment has been upgraded intentionally and that change is worth the migration cost.

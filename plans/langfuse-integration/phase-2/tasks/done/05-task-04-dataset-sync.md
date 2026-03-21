# Task 4 — Hosted Dataset Sync

## Status

Completed

## Objective

Create the canonical hosted Langfuse dataset flow for the first milestone, using stable match-centric item IDs, idempotent synchronization, schema-enforced `input` and `expectedOutput` contracts, and only the match-centric metadata needed for filtering.

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
3. Enforce the hosted dataset schemas in Langfuse for `input` and `expectedOutput`
4. Validate the hosted dataset in Langfuse
5. Record operational notes for re-syncs and incremental updates

## Inputs

- Results of [02-task-01-data-foundation.md](02-task-01-data-foundation.md)
- Results of [03-task-02-prompt-reconstruction.md](03-task-02-prompt-reconstruction.md)
- Results of [04-task-03-runner-spike.md](04-task-03-runner-spike.md)
- `src/Orchestrator/Commands/Observability/ExportExperimentDataset/`
- `tools/langfuse-runner-spike/`
- [../../dataset-contract-and-reconstruction-spec.md](../../dataset-contract-and-reconstruction-spec.md)

## Runner Stack Locked For This Task

- Use JS/TS for the first hosted-dataset sync implementation
- Reuse the .NET export/materialization seam as the source for canonical hosted dataset items
- Use persisted Kicktipp match identity (`TippSpielId`) as the basis for hosted item IDs instead of model-specific export IDs

## Manual Steps

Use [../manual-steps.md](../manual-steps.md#task-4--hosted-dataset-sync) during implementation.

## Decisions Locked In This Task

- Sampling is handled by the runner over the full hosted dataset
- Hosted dataset items are match-centric, not historical-prediction-centric
- Hosted dataset metadata is limited to match-centric filtering fields and does not include replay metadata
- Context reconstruction for later experiments is timestamp-based and documented in [../../dataset-contract-and-reconstruction-spec.md](../../dataset-contract-and-reconstruction-spec.md)
- Langfuse schema enforcement is applied to dataset `input` and `expectedOutput` during hosted sync
- Incremental sync is deferred to [../further-improvement/01-incremental-dataset-sync.md](../further-improvement/01-incremental-dataset-sync.md)

## Implementation Outcome

- The .NET `export-experiment-dataset` command remains the canonical source of hosted dataset items and now feeds the existing JS sync workspace directly.
- `tools/langfuse-runner-spike/sync-dataset.mjs` now validates the exported artifact locally against the canonical hosted dataset contract before any Langfuse write occurs.
- The sync script now upserts the hosted dataset definition with explicit `inputSchema` and `expectedOutputSchema` so malformed items are rejected by Langfuse.
- The sync script now performs idempotent full-scope reruns by fetching each existing dataset item by stable ID and skipping the Langfuse upsert when the canonical `input`, `expectedOutput`, and `metadata` payloads are unchanged.
- `npm run sync:dry-run -- --input <artifact>` now provides a local contract-validation path that does not require Langfuse credentials.

## Validation Notes

- Sample validation used `artifacts/langfuse-dataset/pes-squad-md26.json` exported from matchday `26`, which contained `9` canonical hosted items.
- The first live sample sync created dataset `match-predictions/bundesliga-2025-26/pes-squad` in Langfuse with dataset ID `cmn0ycdfb0001ad0767gcvfey` and synced `9` hosted items.
- Re-running the same sample sync reported `created: 0`, `updated: 0`, and `unchanged: 9`.
- Full-scope export used `artifacts/langfuse-dataset/pes-squad-full.json` and produced `235` completed-match items for `pes-squad` as of `2026-03-21`.
- The first full-scope sync reported `created: 226`, `updated: 0`, and `unchanged: 9`, which reflects the nine previously synced matchday-26 sample items plus the remaining completed matches.
- Re-running the full-scope sync reported `created: 0`, `updated: 0`, and `unchanged: 235`.
- Autonomous Langfuse API verification confirmed the hosted dataset name, dataset metadata, and both schemas through `GET /api/public/v2/datasets/{datasetName}`.
- Autonomous Langfuse API verification confirmed hosted item retrieval through `GET /api/public/dataset-items/{id}` and paged listing through `GET /api/public/dataset-items?datasetName=...`, which returned pages of `100`, `100`, and `35` items.
- Manual inspection of the Langfuse API responses looked good and the task was accepted as complete.

## Completion Criteria

- The hosted dataset exists in Langfuse
- Items are synced idempotently with stable match-centric IDs
- Langfuse enforces the canonical hosted `input` and `expectedOutput` schemas
- The dataset is ready for the first experiment task without structural changes

## Handoff Notes

Document the final dataset name and sync behavior in [../06-task-05-first-experiment.md](../06-task-05-first-experiment.md) before handoff.

The current hosted dataset name is `match-predictions/bundesliga-2025-26/pes-squad`.

The current sync behavior is:

- export the full completed `pes-squad` slice from .NET into a local artifact
- re-run the JS sync against that artifact
- let the script upsert the dataset definition and schemas
- skip item writes when the stable ID already exists with unchanged canonical content
- rely on the deferred follow-up note for any future incremental optimization

The first implementation should avoid re-opening the runner-language question unless the local Python environment has been upgraded intentionally and that change is worth the migration cost.

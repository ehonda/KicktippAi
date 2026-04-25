# Data Model

## Overview

The experiment setup models Langfuse runs around prepared historical football prediction datasets.

The core concepts are:

- one logical source dataset per community
- prepared hosted datasets derived from that source dataset
- task types that match the prepared dataset type
- cross-task scores posted back to Langfuse

The source dataset is still represented in manifests and run metadata as `sourceDatasetName`, but it is no longer a first-class export workflow that users are expected to generate and keep around manually.

## Dataset Types

Two prepared dataset types are supported.

### `slice`

A `slice` dataset is a sampled subset of completed historical matches.

Use it when you want to compare models, prompt variants, or evaluation-time strategies on a fixed set of different fixtures.

Properties:

- sampled from persisted historical outcomes
- each prepared dataset item maps to one historical source match
- batching is simple batched execution controlled by `batchSize`

### `repeated-match`

A `repeated-match` dataset repeats the same historical fixture multiple times.

Use it when you want to measure variance for one match under the same prompt and evaluation-time setup.

Properties:

- all prepared items map back to the same historical source match
- prepared item ids are unique per repetition
- batching uses a warmup plus batches strategy controlled by `batchCount`

## Identity Model

Each prepared dataset keeps both source-level and prepared-level identifiers.

- `sourceDatasetItemId`: stable id for the historical source match
- `sliceDatasetItemId`: hosted dataset item id for the prepared dataset entry

For `slice`, a source match usually appears once in a prepared dataset.

For `repeated-match`, the same `sourceDatasetItemId` appears multiple times with different `sliceDatasetItemId` values.

The manifest also stores:

- `selectedItemIds`: deduplicated list of source item ids selected into the prepared dataset
- `selectedItemIdsHash`: stable hash of that selected source-id set

## Prepared Artifacts

Both preparation commands emit the same two artifact files:

- `slice-dataset.json`: hosted dataset artifact that can be uploaded to Langfuse
- `slice-manifest.json`: execution manifest consumed by the run commands

The manifest carries the structural experiment contract:

- `sliceKey`
- `sliceKind`
- `sampleMethod`
- `communityContext`
- `sourcePoolKey`
- `sourceDatasetName`
- `sliceDatasetName`
- `competition`
- `season`
- `sampleSeed`
- `sampleSize`
- `selectedItemIds`
- `selectedItemIdsHash`
- `items`

Each manifest item carries:

- `sourceDatasetItemId`
- `sliceDatasetItemId`
- `homeTeam`
- `awayTeam`
- `matchday`
- `startsAt`

## Task Types

Prepared dataset type and task type intentionally align.

- `slice` dataset -> `slice` task
- `repeated-match` dataset -> `repeated-match` task

That task type is propagated into run metadata and trace tags so downstream inspection and future analysis tooling can treat scoring as a cross-task concern.

## Run Metadata

When a run command starts, it builds or normalizes run metadata from the manifest plus CLI flags.

Important fields include:

- `runner`
- `taskType`
- `communityContext`
- `competition`
- `sourceDatasetName`
- `datasetName`
- `promptKey`
- `sliceKind`
- `sliceKey`
- `sourcePoolKey`
- `selectedItemIdsHash`
- `selectedItemIdsCount`
- `sampleSize`
- `evaluationTimestampPolicyKey`
- `evaluationTimestampPolicy`
- `evaluationTime`
- `startedAtUtc`
- `sampleSeed`
- `sampleMethod`
- `includeJustification`
- `promptVersion`
- `sourceDatasetKind`
- `datasetItemIdMap`
- `model`
- `batchStrategy`
- `batchSize`
- `batchCount`

`slice` runs use `batchStrategy = simple-batched`.

`repeated-match` runs use `batchStrategy = warmup-plus-batches`.

## Score Model

Scores are intentionally modeled as cross-task outputs rather than task-specific evaluator objects.

The currently posted score set is:

- item-level `kicktipp_points` on the linked trace or prediction observation
- run-level `total_kicktipp_points`
- run-level `avg_kicktipp_points`

These scores are created by the application and posted to Langfuse through the public API.

That means evaluators are currently an application concern, while Langfuse is the system of record for the resulting score values.

## Langfuse Experiment Identity

Langfuse's Experiments Beta UI is driven by SDK-compatible experiment markers in addition to ordinary dataset run identity.

For direct .NET runs, the runner therefore emits:

- trace name `experiment-item-run`
- environment `sdk-experiment`
- trace metadata `experiment_name`
- trace metadata `experiment_run_name`
- trace metadata `dataset_item_id`
- root observation input/output on `experiment-item-run`
- dataset run item `observationId`
- dataset run metadata `experiment_name`
- dataset run metadata `experiment_run_name`
- item-level `kicktipp_points` scores linked to the item trace or prediction observation

`experiment_name` is the stable grouping identity for the experiment. For prepared slice and repeated-match runs, it is derived from task type, community, and slice key. For community-to-date runs, it is the run family name.

`experiment_run_name` is the concrete Langfuse dataset run name. Analysis-bundle publishing uses an alias run name with the default suffix `__experiments-beta` so existing dataset runs are not modified.

Experiment trace tags are intentionally domain-specific. Generic tags such as `phase-2` and `experiment` are not emitted by current runners because the `sdk-experiment` environment and experiment metadata already identify these traces for Langfuse.

## Why This Model

This structure gives us three useful properties:

- fixed-slice comparisons stay easy because the selected source item set is explicit and stable
- repeated-match variance checks reuse the same scoring and trace model as slice runs
- future analysis tooling can operate on a shared contract without caring whether the run came from `slice` or `repeated-match`

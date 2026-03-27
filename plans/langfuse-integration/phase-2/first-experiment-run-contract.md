# First Experiment Run Contract

## Status

Draft for implementation.

## Purpose

This document defines the concrete naming, metadata, score, and tagging contract for the Task 5 experiment flow.

It is intentionally narrower than [first-experiment-run-design.md](first-experiment-run-design.md):

- that document explains the design choice
- this document defines the identifiers and field names that the code should use

The goal is to make the first Task 5 implementation predictable, comparable, and easy to query in Langfuse and in local wrapper output.

## Principles

### 1. The comparable unit is the dataset run

One dataset run should correspond to one experiment variant on one fixed slice.

### 2. Compared runs must share the same slice identity

If two runs are meant to be compared in Langfuse, they must refer to the same selected dataset items.

### 3. Names should stay human-readable

Run names should encode the main comparison dimensions without forcing users to open metadata first.

### 4. Metadata should carry the full machine-readable contract

Names are for scanability. Metadata is the source of truth for filtering, dashboards, and wrapper-side validation.

### 5. Canonical and special-purpose datasets must remain distinct

The canonical hosted dataset stays match-centric. Shadow or benchmark datasets must signal their purpose in the dataset name.

## Dataset Naming

### Canonical dataset

Format:

`match-predictions/bundesliga-2025-26/{communityContext}`

Example:

`match-predictions/bundesliga-2025-26/pes-squad`

Use for:

- the main completed-match hosted dataset
- default Task 5 sampled runs

### Stable benchmark dataset

Format:

`match-predictions/bundesliga-2025-26/{communityContext}/benchmark/{benchmarkKey}`

Examples:

- `match-predictions/bundesliga-2025-26/pes-squad/benchmark/random-50-seed-2026-03-24`
- `match-predictions/bundesliga-2025-26/pes-squad/benchmark/matchdays-20-26`

Use for:

- stable benchmark suites
- recurring benchmark runs that should not depend on the mutable canonical dataset state

### Repetition-expanded shadow dataset

Format:

`match-predictions/bundesliga-2025-26/{communityContext}/shadow/{shadowKey}`

Examples:

- `match-predictions/bundesliga-2025-26/pes-squad/shadow/vfb-stuttgart-vs-rb-leipzig-r17`
- `match-predictions/bundesliga-2025-26/pes-squad/shadow/random-20-seed-17-r9`

Use for:

- special-purpose fixed-repetition experiments where repeated executions should average natively inside one dataset run

## Slice Identity

Every Task 5 run should carry a stable slice identity.

### Slice kinds

Allowed initial values:

- `single-match`
- `random-sample`
- `benchmark-slice`
- `shadow-repetition-slice`

### Slice key

The slice key should be human-readable and stable enough to reuse across compared runs.

Examples:

- `vfb-stuttgart-vs-rb-leipzig-md26`
- `random-25-seed-20260324`
- `matchdays-20-26`
- `vfb-stuttgart-vs-rb-leipzig-r17`

### Slice hash

Use a stable hash of the selected canonical dataset item IDs in sorted order.

Recommended field names:

- `selectedItemIdsHash`
- `selectedItemIdsCount`

If the wrapper persists the explicit selected item IDs in local output, it should also include:

- `selectedItemIds`

but the Langfuse run metadata should prefer the hash over a large raw ID array.

## Run Name Contract

## Format

Initial recommended format:

`task-5__{communityContext}__{model}__{promptKey}__{sliceKey}__{evalPolicyKey}__{startedAtUtc}`

### Segment definitions

- `task-5`: fixed prefix for the first milestone experiment flow
- `{communityContext}`: for example `pes-squad`
- `{model}`: for example `o3` or `gpt-5-nano`
- `{promptKey}`: a short stable identifier for the prompt-building variant
- `{sliceKey}`: human-readable slice identity
- `{evalPolicyKey}`: a short key for the timestamp policy
- `{startedAtUtc}`: UTC timestamp at run creation time to guarantee uniqueness

### Examples

- `task-5__pes-squad__o3__prompt-v1__random-25-seed-20260324__startsat-minus-7d__2026-03-28t16-42-10z`
- `task-5__pes-squad__gpt-5-nano__prompt-v1__vfb-stuttgart-vs-rb-leipzig-md26__historical-prediction-time__2026-03-28t16-43-55z`

### Notes

- Do not encode repetition counters into the normal Task 5 run name
- Repetition numbering belongs only in the shadow-dataset case
- Keep the name scan-friendly; detailed fields belong in metadata

## Prompt Key Contract

The run name needs a short prompt identifier even if prompt reconstruction stays in .NET.

Recommended initial values:

- `prompt-v1`: current production-like prompt reconstruction shape
- `prompt-v2`: next deliberate prompt-build revision

If the prompt logic is not versioned formally yet, use a repo-local identifier that can still remain stable across compared runs.

Recommended metadata fields:

- `promptKey`
- `promptVersion`
- `promptBuild`

At least one of these should be present. `promptKey` is the short human-readable identifier used in the run name.

## Evaluation Policy Contract

Use one short key in names and a fuller structured payload in metadata.

### Initial policy keys

- `historical-prediction-time`
- `startsat-minus-7d`
- `startsat-minus-3d`
- `startsat-minus-24h`

### Recommended metadata fields

- `evaluationTimestampPolicyKey`
- `evaluationTimestampPolicy`
- `evaluationTimeUtc` when an explicit evaluation time is forced for the run

`evaluationTimestampPolicy` can be an object, for example:

```json
{
  "kind": "relative-to-match-start",
  "offset": "P7D",
  "reference": "startsAt"
}
```

## Run Metadata Contract

All Task 5 runs should include the following top-level metadata fields.

### Required fields

- `runner`: fixed value `task-5-first-experiment`
- `task`: fixed value `task-5`
- `communityContext`
- `competition`
- `datasetName`
- `model`
- `promptKey`
- `sliceKind`
- `sliceKey`
- `selectedItemIdsHash`
- `selectedItemIdsCount`
- `sampleSize`
- `evaluationTimestampPolicyKey`
- `startedAtUtc`

### Recommended fields

- `benchmarkKey` for benchmark slices
- `sampleSeed` for seeded random samples
- `sampleMethod`, for example `random-sample` or `explicit-item-list`
- `includeJustification`
- `promptVersion`
- `promptBuild`
- `orchestratorExportVersion`
- `sourceDatasetKind`, for example `canonical`, `benchmark`, or `shadow`

### Deferred or special-purpose fields

- `repetitionCount`: only for shadow-dataset runs or later repetition-specific work
- `shadowKey`: only for shadow datasets
- `benchmarkFrozenAtUtc`: for frozen benchmark dataset definitions

## Trace Tag Contract

Use a small stable tag set on traces to make dashboard filtering easier.

Recommended tags:

- `task-5`
- `phase-2`
- `experiment`
- `community:{communityContext}`
- `slice:{sliceKey}`
- `model:{model}`
- `prompt:{promptKey}`

Do not encode long hashes into tags. Keep those in metadata.

## Trace Metadata Contract

Each trace produced by a Task 5 run should include enough metadata to tie it back to the run contract and the dataset item.

### Required fields

- `datasetRunName`
- `datasetItemId`
- `communityContext`
- `model`
- `promptKey`
- `sliceKind`
- `sliceKey`
- `evaluationTimestampPolicyKey`

### Recommended fields

- `matchday`
- `season`
- `homeTeam`
- `awayTeam`
- `selectedItemIdsHash`

## Score Naming Contract

### Item-level scores

Keep the existing item-level names because they already match the domain language well:

- `kicktipp_points`
- `exact_hit`
- `outcome_correct`
- `home_goal_error`
- `away_goal_error`
- `goal_difference_error`

### Run-level scores

Use explicit aggregate names so item-level and run-level score series are easy to distinguish.

Recommended run-level score names:

- `avg_kicktipp_points`
- `exact_hit_rate`
- `outcome_correct_rate`
- `mean_home_goal_error`
- `mean_away_goal_error`
- `mean_goal_difference_error`

### Optional run-level counts

If useful for validation and dashboards, also emit:

- `evaluated_item_count`

## Local Wrapper Output Contract

The local wrapper output should be a superset of what is stored in Langfuse metadata.

Recommended top-level result fields:

- `datasetName`
- `runName`
- `model`
- `promptKey`
- `sliceKind`
- `sliceKey`
- `selectedItemIdsHash`
- `selectedItemIds`
- `sampleSeed`
- `evaluationTimestampPolicyKey`
- `aggregateScores`
- `datasetRunId`

This allows local validation without depending on Langfuse's current cross-run aggregation limits.

## Shadow-Dataset Special Case

If we implement the repetition-expanded shadow-dataset path later, use the same metadata contract with these additions:

- `sourceDatasetKind: shadow`
- `shadowKey`
- `repetitionCount`
- `shadowSourceSliceKey`

For shadow dataset item IDs, use a suffix-based scheme:

`{canonicalDatasetItemId}::rep-{nn}`

Examples:

- `tippspiel-123456::rep-01`
- `tippspiel-123456::rep-17`

## Benchmark-Dataset Special Case

For stable benchmark datasets, add:

- `sourceDatasetKind: benchmark`
- `benchmarkKey`
- `benchmarkFrozenAtUtc`

The benchmark key should appear both in the dataset name and in run metadata.

## Immediate Implementation Target

For the first code pass that realigns Task 5 with the accepted run design, the minimum contract to implement is:

- canonical dataset naming stays unchanged
- run name format from this document
- required run metadata fields from this document
- required trace metadata fields from this document
- existing item-level score names remain unchanged
- recommended run-level aggregate score names are adopted

That is enough to make the first Task 5 flow consistent with the accepted design before any benchmark or shadow-dataset extension work starts.

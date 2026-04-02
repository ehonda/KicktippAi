# Task 5 — First Experiment

## Status

In progress

## Objective

Run the first real Bundesliga 2025/2026 match experiment using reusable hosted slice datasets derived from the canonical dataset, configurable sampling, timestamp-based context reconstruction, and Kicktipp-only scoring.

## Why This Comes Fifth

This is the first milestone payoff task. It depends on the data contract, prompt reconstruction, runner choice, and hosted dataset all being stable.

The runner choice is now stable for the first milestone: use JS/TS unless a later session deliberately modernizes the local Python environment and decides to migrate.

## Required Outputs

- A runnable first experiment flow
- Configurable sample size and evaluation timestamp policy
- Reusable local slice artifacts and hosted slice datasets
- Item-level scores and run-level aggregate scores in Langfuse
- Verification notes showing the run is visible and analyzable in the UI

## Planned Work

1. Select a sample from the canonical dataset pool and materialize it as a reusable hosted slice dataset
2. Choose an evaluation timestamp policy such as `startsAt - 12 hours`
3. Reconstruct the required context-document set at that timestamp
4. Run predictions for the sample
5. Attach Kicktipp-based item-level and run-level scores
6. Verify trace linkage, dataset-run visibility, and compare-view behavior in Langfuse, first via the Langfuse API skill and then in the UI

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

Primary item-level score:

- `kicktipp_points`

Run-level aggregate scores:

- `total_kicktipp_points`
- `avg_kicktipp_points`

## Completion Criteria

- A sampled experiment run completes successfully
- The run appears in Langfuse as a dataset run on a reusable slice dataset
- Item-level and run-level Kicktipp scores are visible
- Reconstruction follows the agreed timestamp-based rule
- The scoring matches manual expectations on checked examples
- Any remaining compare-view-only score-column noise is documented if it persists

## Current Implementation Notes

- Exact-time export and prompt reconstruction are implemented through `.NET` commands that accept NodaTime's invariant `ZonedDateTime` `G` pattern, for example `2026-03-15T12:00:00 Europe/Berlin (+01)`
- `export-experiment-item` now also supports a relative evaluation policy input, currently limited to `kind=relative` with a NodaTime duration offset against `startsAt`, for example `-12:00:00`
- The experiment wrapper lives in `.github/copilot/skills/langfuse-experiment-runner/` and now supports both the legacy single-match path and a sampled reusable-slice path
- The sampled slice JS runner processes items directly in parallel batches instead of using the old warm-up optimization, because prompt caching does not help across heavily varying per-item prompts
- Sampled slices are now materialized once as local bundle artifacts and as hosted Langfuse slice datasets under `match-predictions/bundesliga-2025-26/pes-squad/slices/<sourcePoolKey>/<sliceKey>`
- Repeated runs of the same slice reuse `slice-dataset.json`, `slice-manifest.json`, and per-model exported experiment items instead of resampling or re-exporting everything
- Slice datasets intentionally expose `fixture` and `score` fields so Langfuse compare views show football data in home-first order without the earlier home/away confusion
- The Task 5 runners now emit only `kicktipp_points` on traces and only `total_kicktipp_points` plus `avg_kicktipp_points` on dataset runs
- The wrapper uses the dedicated Langfuse API skill script for autonomous verification of traces and generation observations after the run
- The Langfuse OTEL span processor is configured with the `x-langfuse-ingestion-version: 4` header
- Live API verification on `2026-04-02` confirmed the newest slice run no longer emits the legacy supporting score names; remaining empty legacy columns in compare view look like Langfuse-side UI behavior rather than current runner output

## Current Validation Slice

- Reusable slice datasets live under `match-predictions/bundesliga-2025-26/pes-squad/slices/<sourcePoolKey>/<sliceKey>`
- Keep real comparisons on fixed sampled slices and use a low-cost `1`-item slice for ingestion and score-verification checks
- A recent low-cost verification slice used source pool `matchdays-26`, slice key `random-1-seed-20260402`, and batch size `1`
- Default sampled-run policy is `startsAt - 12h`

## Implementation Evidence

- First successful sampled slice comparison executed on `2026-03-28` across a fixed `10`-item slice for `o3` and `gpt-5-nano`
- Reusable slice-dataset flow validated on `2026-04-02`
- Slice dataset name: `match-predictions/bundesliga-2025-26/pes-squad/slices/matchdays-26/random-1-seed-20260402`
- Slice artifact path: `artifacts/langfuse-runner-spike/runs/slices/pes-squad/matchdays-26/random-1-seed-20260402/slice-dataset.json`
- Slice manifest path: `artifacts/langfuse-runner-spike/runs/slices/pes-squad/matchdays-26/random-1-seed-20260402/slice-manifest.json`
- Dataset run id (`gpt-5-nano` verification run): `54841c1a-adca-454d-b82b-7a47a8f0e483`
- Trace id (`gpt-5-nano` verification run): `fd2a40bbb2ff5f04d53d8f350dfec6d4`
- Repeated execution reused both the existing slice dataset artifact and the sampled exported item instead of regenerating them
- Autonomous verification confirmed the verification run created `1` dataset run item, the trace existed, and one generation observation was attached
- Langfuse `v2/scores` verification for dataset run `54841c1a-adca-454d-b82b-7a47a8f0e483` returned only `total_kicktipp_points` and `avg_kicktipp_points`
- Langfuse trace-detail verification for trace `fd2a40bbb2ff5f04d53d8f350dfec6d4` returned exactly one trace-level score: `kicktipp_points`
- `GET /api/public/score-configs` returned zero project score configs on `2026-04-02`, so archiving score configs is not currently an available cleanup lever for the extra compare-view columns
- The remaining empty legacy score columns in compare view therefore look like Langfuse-side UI behavior around historical or empty score names rather than current runner output

## Manual Scoring Spot Checks

- Checked item `bundesliga-2025-26__pes-squad__ts1423757255` on the `2026-04-02` verification slice; expected outcome `0:1`, recorded prediction `1:1`, and emitted `kicktipp_points=0` are consistent with a missed away-win prediction
- Historical multi-item spot checks from the first successful slice comparison also matched manually derived Kicktipp scoring; the runner simply no longer emits the older supporting diagnostics

## Remaining Task 5 Closure Work

- Final manual compare-view acceptance is still pending for closure
- If Langfuse Cloud has not yet fully rolled out the empty-score-column fix for this project view, treat the remaining empty legacy columns as external UI noise rather than a local runner regression
- Do not reintroduce the removed supporting scores just to populate those empty columns
- Once the UI acceptance note is recorded, Task 5 can be reevaluated for completion

## Current Design Decision

- Follow [../first-experiment-run-design.md](../first-experiment-run-design.md) as the source of truth for Task 5 run modeling
- Follow [../first-experiment-run-contract.md](../first-experiment-run-contract.md) as the source of truth for Task 5 run naming, metadata, and score identifiers
- The primary Task 5 unit is now one dataset run per comparable variant on one fixed slice of hosted dataset items
- Compared variants must share the same selected dataset items so Langfuse-native compare views and averages stay meaningful
- Item-level Kicktipp scores stay attached to traces and aggregate metrics should be attached to the dataset run as run-level scores
- Do not reintroduce legacy supporting score names solely to work around compare-view presentation noise; the trace and dataset-run payloads are already verified clean

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
- Task 5 now derives reusable slice datasets from this canonical pool instead of running repeated comparisons directly against the canonical dataset name
- Recommended pre-experiment refresh: re-run the full hosted sync first so any newly completed matches are available before sampling a new slice; repeated runs of an existing slice should reuse the stored slice artifacts and hosted slice dataset

When complete, update [07-task-06-follow-up-evaluation.md](07-task-06-follow-up-evaluation.md) with what should be automated or expanded next.

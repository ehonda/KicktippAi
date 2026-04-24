# Running Experiments

## Overview

The active workflow is:

1. Prepare a hosted dataset artifact and manifest.
2. Sync the hosted dataset artifact to Langfuse.
3. Execute the prepared manifest with one of the run commands.

For local verification and development runs, prefer `gpt-5-nano` to keep cost low.

For live manual verification, prefer the `pes-squad` community because it collects context regularly. The lighter-weight test community may not have enough historical context coverage for experiment reconstruction.

## Common Commands

Preparation:

- `prepare-slice`
- `prepare-repeated-match`

Upload:

- `sync-dataset`

Execution:

- `run-slice`
- `run-repeated-match`
- `run-community-to-date`

Publishing existing analysis bundles:

- `publish-experiment-analysis`

## Langfuse Experiments Beta UI

Langfuse's Experiments Beta UI currently expects the same experiment markers emitted by the official SDK experiment runner. Our .NET runner uses the public API directly, so new runs must explicitly mimic those markers instead of only creating dataset run items.

New `run-slice`, `run-repeated-match`, and `run-community-to-date` executions now:

- name item traces `experiment-item-run`
- set the Langfuse environment to `sdk-experiment`
- attach trace metadata fields `experiment_name` and `experiment_run_name`
- attach the same fields to dataset run metadata when creating dataset run items
- pass the root observation id when linking a trace to the dataset run item

That means newly executed experiments should appear in the Experiments Beta UI after Langfuse ingests the traces and dataset run items. Use the same time-range filter you would use for ordinary traces.

Existing analysis bundles can be published without rerunning predictions. The publisher creates new dataset-run aliases that point at the existing traces and dataset items, adds the SDK-compatible experiment metadata, and posts aggregate run scores. By default, it appends `__experiments-beta` to each source run name so the original dataset runs are left alone.

Use `--dry-run` first if you want to inspect the aliases before writing:

```powershell
dotnet run --project src/Orchestrator -- publish-experiment-analysis --input artifacts/langfuse-experiments/analysis/task-5/match-predictions/bundesliga-2025-26/pes-squad/slices/all-matchdays/random-16-seed-578661/comparison-2026-04-06t21-56-17z.json --dry-run
```

Publish the current browser reports shown in the local Experiment Analysis index:

```powershell
dotnet run --project src/Orchestrator -- publish-experiment-analysis --input artifacts/langfuse-experiments/analysis/verification/ehonda-ai-arena/community-to-date-md28__2026-04-07t01-08-32z.analysis.json --replace-runs
dotnet run --project src/Orchestrator -- publish-experiment-analysis --input artifacts/langfuse-experiments/analysis/verification/schadensfresse/community-to-date-md29__2026-04-12t00-12-39z.analysis.json --replace-runs
dotnet run --project src/Orchestrator -- publish-experiment-analysis --input artifacts/langfuse-experiments/analysis/task-5/match-predictions/bundesliga-2025-26/pes-squad/slices/all-matchdays/random-16-seed-578661/comparison-2026-04-06t21-56-17z.json --replace-runs
```

Use `--experiment-name` if you want to override the derived grouping name. Otherwise the publisher derives stable names such as `community-to-date__ehonda-ai-arena__community-to-date-md28` and `task-5__pes-squad__random-16-seed-578661`.

## Slice Workflow

### 1. Prepare a slice

This creates `slice-dataset.json` and `slice-manifest.json`.

```powershell
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --matchdays 26 --sample-size 1 --sample-seed 20260405 --output-directory artifacts/langfuse-experiments/verification/pes-squad-slice
```

### 2. Sync the dataset

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/verification/pes-squad-slice/slice-dataset.json
```

### 3. Run the slice

The command can use either an exact historical evaluation time or a relative policy.

Example using `gpt-5-nano` and an exact historical timestamp that was verified live:

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
dotnet run --project src/Orchestrator -- run-slice gpt-5-nano --manifest artifacts/langfuse-experiments/verification/pes-squad-slice/slice-manifest.json --run-name "slice__pes-squad__gpt-5-nano__random-1-seed-20260405__exact-time__$runStamp" --prompt-key prompt-v1 --evaluation-time "2026-03-14T12:00:00 Europe/Berlin (+01)" --batch-size 1 --replace-run
```

This exact workflow was verified successfully on `2026-04-05`.

## Repeated-Match Workflow

### 1. Prepare a repeated-match dataset

Use `sample-size > 1` if you want the warmup plus batches behavior to actually exercise more than one execution.

```powershell
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "VfB Stuttgart" --away "RB Leipzig" --matchday 26 --sample-size 2 --output-directory artifacts/langfuse-experiments/verification/pes-squad-repeated-match
```

### 2. Sync the dataset

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/verification/pes-squad-repeated-match/slice-dataset.json
```

### 3. Run the repeated-match experiment

Example using `gpt-5-nano` and one warmup plus one additional batch:

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
dotnet run --project src/Orchestrator -- run-repeated-match gpt-5-nano --manifest artifacts/langfuse-experiments/verification/pes-squad-repeated-match/slice-manifest.json --run-name "repeated-match__pes-squad__gpt-5-nano__repeat-2__exact-time__$runStamp" --prompt-key prompt-v1 --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 1 --replace-run
```

This exact workflow was verified successfully on `2026-04-05`.

## Choosing Evaluation Time

There are two supported execution modes:

- exact time: `--evaluation-time`
- relative policy: `--evaluation-policy-kind` plus `--evaluation-policy-offset`

Exact time is often the safest choice for manual verification because it lets you probe reconstruction directly with `reconstruct-prompt` before spending a full run.

Relative policy is useful for systematic comparisons, for example a fixed `-12:00:00` offset from `startsAt` across an entire slice.

## Choosing Batch Settings

`run-slice`:

- uses `--batch-size`
- intended for ordinary parallel execution across different fixtures

`run-repeated-match`:

- uses `--batch-count`
- runs the first execution as warmup
- distributes the remaining executions across the requested number of follow-up batches

## Re-running Experiments

Use `--replace-run` when you want to reuse the same Langfuse run name after changing prompt or evaluation settings.

Both run commands also still support `--run-metadata-file` for compatibility with older prepared artifacts, but new runs should prefer direct CLI flags.

## Troubleshooting

### Missing historical context

If a run fails during prompt reconstruction because a required document did not exist yet at the requested timestamp:

1. switch to a community with stronger context coverage, typically `pes-squad`
2. probe the exact timestamp with `reconstruct-prompt`
3. use a later exact historical evaluation time, or prepare a different slice fixture

### Verifying a fixture before a full run

Use `reconstruct-prompt` with the intended `--evaluation-time` before running the experiment:

```powershell
dotnet run --project src/Orchestrator -- reconstruct-prompt gpt-5-nano --community-context pes-squad --home "VfB Stuttgart" --away "RB Leipzig" --matchday 26 --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)"
```

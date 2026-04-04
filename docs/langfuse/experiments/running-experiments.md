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

# Running Experiments

## Overview

The active workflow is:

1. Prepare a hosted dataset artifact and manifest.
2. Sync the hosted dataset artifact to Langfuse only when creating that dataset initially.
3. Execute the prepared manifest with one of the run commands.

If the dataset already exists in Langfuse, assume it has not changed and skip resync by default to save execution time.

Resync only when one of these is true:

- you are uploading the dataset for the first time
- you explicitly changed the prepared dataset artifact or dataset name
- the run fails with errors that suggest missing, renamed, or stale dataset items or dataset metadata

For local verification and development runs, prefer `gpt-5-nano` to keep cost low.

For live manual verification, prefer the `pes-squad` community because it collects context regularly. The lighter-weight test community may not have enough historical context coverage for experiment reconstruction.

## Common Commands

Preparation:

- `prepare-slice`
- `prepare-repeated-match`
- `prepare-repeated-match-slice`

Upload:

- `sync-dataset`

Execution:

- `run-slice`
- `run-repeated-match`
- `run-repeated-match-slice`
- `run-community-to-date`

Experiment run commands keep console output compact by default: progress remains visible through `[progress]` lines, while structured logging is limited to warnings and errors.

Publishing existing analysis bundles:

- `publish-experiment-analysis`

## Langfuse Experiments Beta UI

Langfuse's Experiments Beta UI currently expects the same experiment markers emitted by the official SDK experiment runner. Our .NET runner uses the public API directly, so new runs must explicitly mimic those markers instead of only creating dataset run items.

New `run-slice`, `run-repeated-match`, `run-repeated-match-slice`, and `run-community-to-date` executions now:

- name item traces `experiment-item-run`
- set the Langfuse environment to `sdk-experiment`
- attach trace metadata fields `experiment_name` and `experiment_run_name`
- attach the same fields to dataset run metadata when creating dataset run items
- attach the compact dataset-item input to the root `experiment-item-run` observation, not only to the trace, so the Experiments Beta results table shows the fixture instead of runner internals
- attach `langfuse.experiment.item.expected_output` to the root item span so the Experiments Beta expected-output column shows the actual scoreline
- post item-level `kicktipp_points` scores on the item trace or prediction observation
- pass the root observation id when linking a trace to the dataset run item

That means newly executed experiments should appear in the Experiments Beta UI after Langfuse ingests the traces and dataset run items. Use the same time-range filter you would use for ordinary traces.

Use `--run-description` when a run needs a short human label in the Experiments Beta Description column. Keep `--run-name` machine-stable and detailed enough for grouping, replacement, and usage collection; use the description for compact scan labels that avoid widening the long Name column. The cost-estimator base-estimate workflow currently uses descriptions such as `o3 xhigh, preflight` and `o3 xhigh, base estimate (1/5)`. Other experiment workflows can leave descriptions empty until they get their own description convention.

Existing analysis bundles can be published without rerunning predictions. The publisher creates new dataset-run aliases that point at the existing traces and dataset items, adds SDK-compatible experiment metadata to the alias run, and posts aggregate run scores. By default, it appends `__experiments-beta` to each source run name so the original dataset runs are left alone.

This cannot fully repair older traces that were already ingested with `environment = development`, root observation name `match-experiment-item`, and no `experiment_name` / `experiment_run_name` trace metadata. Langfuse's public API lets us create alias dataset runs, but it does not update existing trace records. Those aliases are still useful for API/reporting workflows, but they may stay hidden in the Experiments Beta UI.

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

Use `--starts-after` when you want the random slice to exclude older completed matches, for example to avoid fixtures that may fall before a model's knowledge cutoff. The flag uses the same NodaTime invariant `ZonedDateTime` `G` format as `--evaluation-time`, and only matches with `startsAt` strictly after the supplied timestamp are eligible.

```powershell
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --sample-size 10 --sample-seed 20260403 --starts-after "2026-01-01T00:00:00 Europe/Berlin (+01)"
```

### 2. Sync the slice dataset once

Run this when you first create the hosted dataset in Langfuse. For later reruns against the same prepared artifact, skip this step unless you explicitly changed the dataset or the run errors suggest dataset drift.

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

A 25-repetition `o3` vs `gpt-5-nano` repeated-match workflow with `--batch-count 3` was verified successfully on `2026-04-26`.

## Repeated-Match Workflow

### 1. Prepare a repeated-match dataset

Use `sample-size > 1` if you want the warmup plus batches behavior to actually exercise more than one execution. Add `--dataset-description` when the repeated fixture needs context in exported reports.

```powershell
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "VfB Stuttgart" --away "RB Leipzig" --matchday 26 --sample-size 25 --dataset-description "Stuttgart's 1-0 Matchday 26 win over Leipzig was a close top-four clash where Stuttgart leapfrogged Leipzig."
```

### 2. Sync the repeated-match dataset once

Run this when you first create the hosted dataset in Langfuse. For later reruns against the same prepared artifact, skip this step unless you explicitly changed the dataset or the run errors suggest dataset drift.

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-25/slice-dataset.json
```

### 3. Run the repeated-match experiment

Example using `gpt-5-nano` with one warmup plus three follow-up batches. With 25 repetitions, `--batch-count 3` runs the first prediction alone, then distributes the remaining 24 predictions as three batches of eight.

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
dotnet run --project src/Orchestrator -- run-repeated-match gpt-5-nano --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-25/slice-manifest.json --run-name "repeated-match__pes-squad__gpt-5-nano__prompt-v1__repeat-25__exact-time__$runStamp" --prompt-key prompt-v1 --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 3 --replace-run
```

This exact workflow was verified successfully on `2026-04-05`.

## Repeated-Match Slice Workflow

Use this when you want repeated predictions over multiple random fixtures. The prepared dataset contains `match-count * repetitions` items, but execution still uses repeated-match warmup behavior per fixture.

### 1. Prepare a repeated-match slice dataset

```powershell
dotnet run --project src/Orchestrator -- prepare-repeated-match-slice --community-context pes-squad --match-count 15 --repetitions 10 --sample-seed 20260517 --starts-after "2025-12-01T00:00:00 Europe/Berlin (+01)"
```

The default output path is `artifacts/langfuse-experiments/repeated-match-slices/<community>/<source-pool-key>/<slice-key>`.

### 2. Sync the repeated-match slice dataset once

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/repeated-match-slices/pes-squad/all-matchdays-after-20251130t230000z/random-15x10-seed-20260517/slice-dataset.json
```

### 3. Run the repeated-match slice experiment

`--batch-count` controls the post-warmup batches inside each fixture workflow. `--parallelism` controls how many fixture workflows run at once and defaults to `5`.

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
dotnet run --project src/Orchestrator -- run-repeated-match-slice gpt-5.4-nano --manifest artifacts/langfuse-experiments/repeated-match-slices/pes-squad/all-matchdays-after-20251130t230000z/random-15x10-seed-20260517/slice-manifest.json --run-name "repeated-match-slice__pes-squad__gpt-5.4-nano__prompt-v1__reasoning-none__random-15x10-seed-20260517__startsat-12h__$runStamp" --prompt-key prompt-v1 --reasoning-effort none --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 3 --parallelism 5 --replace-run
```

If a run hits OpenAI rate limits or flex-capacity failures, retry the same manifest and run settings with lower parallelism, first `--parallelism 3` and then `--parallelism 1`.

### Reasoning effort experiments

`run-slice`, `run-repeated-match`, and `run-repeated-match-slice` can optionally pass OpenAI reasoning effort through the Responses API request:

```powershell
dotnet run --project src/Orchestrator -- run-repeated-match gpt-5.5 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-25/slice-manifest.json --run-name "repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-25__exact-time__$runStamp" --prompt-key langfuse-o3-poc --prompt-source langfuse --langfuse-prompt-name kicktippai/predict-one-match-o3-poc --langfuse-prompt-label poc --reasoning-effort none --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 3 --replace-run
```

When `--reasoning-effort` is omitted, the request omits Responses reasoning options so production and default experiment behavior keep using OpenAI's model default. Supported values are `none`, `minimal`, `low`, `medium`, `high`, and `xhigh`. Explicit-effort analysis labels include the effort next to the model name, for example `gpt-5.5 (none)`.

### Langfuse hosted prompt POC

The hosted-prompt route is opt-in for experiment runs. Production and local experiment runs keep using file-based prompts unless `--prompt-source langfuse` is passed.

The POC prompt is a Langfuse text prompt named `kicktippai/predict-one-match-o3-poc`, labeled `poc`, created from `prompts/o3/match.md` with the context section replaced by `{{context_documents}}`.

Use a one-item preflight before spending the full 25-run comparison:

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "VfB Stuttgart" --away "RB Leipzig" --matchday 26 --sample-size 1 --slice-key repeat-1-langfuse-poc
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-1-langfuse-poc/slice-dataset.json
dotnet run --project src/Orchestrator -- run-repeated-match gpt-5.5 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-1-langfuse-poc/slice-manifest.json --run-name "repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__repeat-1__exact-time__$runStamp" --prompt-key langfuse-o3-poc --prompt-source langfuse --langfuse-prompt-name kicktippai/predict-one-match-o3-poc --langfuse-prompt-label poc --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 1 --replace-run
```

Then run the comparable 25x experiment against the shared `repeat-25` manifest:

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-25/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__repeat-25__exact-time__$runStamp" --prompt-key prompt-v1 --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 3 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match gpt-5.5 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-25/slice-manifest.json --run-name "repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__repeat-25__exact-time__$runStamp" --prompt-key langfuse-o3-poc --prompt-source langfuse --langfuse-prompt-name kicktippai/predict-one-match-o3-poc --langfuse-prompt-label poc --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 3 --replace-run
```

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

`run-repeated-match-slice`:

- uses `--batch-count` inside each selected fixture workflow
- runs the first repetition for each fixture as warmup
- distributes the remaining repetitions for that fixture across the requested number of follow-up batches
- uses `--parallelism` to limit concurrent fixture workflows, defaulting to `5`
- should be retried with `--parallelism 3` and then `--parallelism 1` if rate limits or flex-capacity failures appear

## Re-running Experiments

Use `--replace-run` when you want to reuse the same Langfuse run name after changing prompt or evaluation settings.

Run commands also still support `--run-metadata-file` for compatibility with older prepared artifacts, but new runs should prefer direct CLI flags.

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

### Dataset sync errors

If a rerun fails with errors that suggest the hosted dataset no longer matches the prepared manifest, for example missing dataset items, renamed dataset items, or a dataset-name mismatch, resync the prepared artifact and rerun:

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/.../slice-dataset.json
```

This should be an exception path rather than the default because skipping unnecessary resyncs saves execution time.

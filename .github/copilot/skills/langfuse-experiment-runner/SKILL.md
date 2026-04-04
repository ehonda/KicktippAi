---
name: langfuse-experiment-runner
description: Prepare and run Langfuse experiments directly through the Orchestrator without the legacy JS runner.
---

# Langfuse Experiment Runner

Use this skill to run Langfuse experiments end-to-end with dotnet commands only.

Two preparation modes are supported:

1. Fresh sampled slices built directly from completed historical outcomes
2. Dedicated repeated-match datasets for variance checks on one historical match

## When To Use It

- Compare multiple models against the same fixed slice
- Re-run the same dataset with `--replace-run` after prompt or scoring changes
- Measure variance on a single match at one exact historical evaluation time

## Fresh Random Slice Workflow

Prepare a deterministic slice. Reuse the emitted `sliceArtifactPath` and `sliceManifestPath` from the JSON summary:

```powershell
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --sample-size 16 --sample-seed 20260403
```

Sync the hosted dataset:

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/slices/pes-squad/all-matchdays/random-16-seed-20260403/slice-dataset.json
```

Run one model at a time against the same manifest. Build the run name explicitly so comparisons stay easy to read:

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
dotnet run --project src/Orchestrator -- run-slice o3 --manifest artifacts/langfuse-experiments/slices/pes-squad/all-matchdays/random-16-seed-20260403/slice-manifest.json --run-name "slice__pes-squad__o3__prompt-v1__random-16-seed-20260403__startsat-12h__$runStamp" --prompt-key prompt-v1 --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-size 8 --replace-run
dotnet run --project src/Orchestrator -- run-slice gpt-5-nano --manifest artifacts/langfuse-experiments/slices/pes-squad/all-matchdays/random-16-seed-20260403/slice-manifest.json --run-name "slice__pes-squad__gpt-5-nano__prompt-v1__random-16-seed-20260403__startsat-12h__$runStamp" --prompt-key prompt-v1 --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-size 8 --replace-run
```

## Repeated-Match Workflow

Prepare the dedicated repeated dataset:

```powershell
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "VfB Stuttgart" --away "RB Leipzig" --matchday 26 --sample-size 16
```

Sync it once:

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-16/slice-dataset.json
```

Run it with an exact historical evaluation time:

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-16/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__repeat-16__exact-time__$runStamp" --prompt-key prompt-v1 --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 3 --replace-run
```

## Verification

Use the [langfuse-api](../langfuse-api/SKILL.md) skill after a run when you want to inspect traces or observations in detail.

```powershell
.github/copilot/skills/langfuse-api/scripts/Query-LangfuseApi.ps1 -Endpoint "traces" -QueryParams @{ limit = 20; tags = "slice:random-16-seed-20260403" }
```

## Notes

- The Orchestrator loads the repository secrets `.env` automatically; `dotenvx` is not required for the dotnet commands.
- `run-slice` and `run-repeated-match` still accept `--run-metadata-file` for old prepared artifacts, but new runs should prefer direct flags.
- Sampled slices and repeated-match datasets both emit reusable `slice-dataset.json` and `slice-manifest.json` artifacts.
- The run command keeps the OTEL ingestion header `x-langfuse-ingestion-version: 4`, so traces continue to use the faster Langfuse ingestion path.

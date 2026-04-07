---
name: langfuse-experiment-analysis
description: Run or reuse Langfuse experiments with the current Orchestrator and Python analysis tooling. Use this to compare models, prompt variants, justification variants, evaluation settings, or participant-backed community snapshots across slice, repeated-match, and community-to-date datasets, then export comparable runs and generate JSON, Markdown, and browser-friendly HTML reports.
---

# Langfuse Experiment Analysis

Use this skill to run Langfuse experiments end to end and produce a statistical comparison report with the repository's current tooling.

This skill extends the workflow in [langfuse-experiment-runner](../langfuse-experiment-runner/SKILL.md) with the analysis steps that happen after successful comparable runs:

1. export comparable runs from Langfuse with `export-experiment-analysis`
2. generate JSON, Markdown, and default HTML reports with `uv run experiment-analysis-report`

Use [langfuse-api](../langfuse-api/SKILL.md) when you need raw trace or observation inspection during debugging.

## Supported Experiment Shapes

This skill handles the current repository task types and their settings:

1. fixed slice experiments built with `prepare-slice` and executed with `run-slice`
2. repeated-match experiments built with `prepare-repeated-match` and executed with `run-repeated-match`
3. community-to-date experiments built with `prepare-community-to-date` and executed with participant-backed runs over Kicktipp-posted predictions
4. already prepared manifests where preparation can be skipped and only sync, run, export, and reporting are needed
5. existing Langfuse datasets where preparation and sync are already done and the task is only run discovery, export, and reporting

## Decision Flow

### 1. Choose the task type

- Use `slice` when you want to compare variants across a fixed sample of historical matches.
- Use `repeated-match` when you want variance checks on repeated executions of one historical match.
- Use `community-to-date` when you want one run per Kicktipp participant over all finished matches up to a cutoff matchday, without prompt reconstruction.

### 2. Decide whether to prepare or reuse artifacts

- If no prepared manifest exists, run `prepare-slice`, `prepare-repeated-match`, or `prepare-community-to-date` first.
- If `slice-manifest.json` and `slice-dataset.json` already exist, reuse them.
- If the user gives an existing Langfuse dataset name, do not re-prepare or re-sync unless explicitly asked.

### 2a. If the user already has a Langfuse dataset

- Start from the provided dataset name.
- If run names are already known, skip directly to export and reporting.
- If run names are not known, inspect Langfuse and identify the comparable run names before exporting.
- Treat preparation and sync as complete unless the user explicitly asks to rebuild the dataset.

### 3. Choose the evaluation mode

- Use `--evaluation-time` when you need an exact historical reconstruction timestamp.
- Use `--evaluation-policy-kind` plus `--evaluation-policy-offset` when you want a systematic relative policy across a slice.

### 4. Choose the comparison variants

Typical comparison axes:

- model name
- prompt key
- `--include-justification`
- evaluation time or policy

Keep everything except the intended comparison axis fixed.

### 5. Confirm comparability before exporting

Comparable runs must share the same prepared dataset item set.

In practice that means:

- same dataset name
- same manifest lineage
- same slice or repeat identity
- same selected item set

## Workflow

### A. Prepare the dataset if needed

Fixed slice example:

```powershell
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --matchdays 26 --sample-size 16 --sample-seed 20260403
```

Repeated-match example:

```powershell
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "VfB Stuttgart" --away "RB Leipzig" --matchday 26 --sample-size 16
```

Community-to-date example with a small cutoff:

```powershell
dotnet run --project src/Orchestrator -- prepare-community-to-date --community-context schadensfresse --cutoff-matchday 10
```

### B. Sync the hosted dataset

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input <path-to-slice-dataset.json>
```

### C. Run each variant against the same manifest

Use one shared `$runStamp` so related runs stay grouped and easy to compare.

Fixed slice example with a relative evaluation policy:

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
dotnet run --project src/Orchestrator -- run-slice o3 --manifest <path-to-slice-manifest.json> --run-name "slice__pes-squad__o3__prompt-v1__random-16-seed-20260403__startsat-12h__$runStamp" --prompt-key prompt-v1 --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-size 8 --replace-run
dotnet run --project src/Orchestrator -- run-slice gpt-5-nano --manifest <path-to-slice-manifest.json> --run-name "slice__pes-squad__gpt-5-nano__prompt-v1__random-16-seed-20260403__startsat-12h__$runStamp" --prompt-key prompt-v1 --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-size 8 --replace-run
```

Repeated-match example with an exact historical evaluation time:

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest <path-to-slice-manifest.json> --run-name "repeated-match__pes-squad__o3__prompt-v1__repeat-16__exact-time__$runStamp" --prompt-key prompt-v1 --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 3 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match gpt-5-nano --manifest <path-to-slice-manifest.json> --run-name "repeated-match__pes-squad__gpt-5-nano__prompt-v1__repeat-16__exact-time__$runStamp" --prompt-key prompt-v1 --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 3 --replace-run
```

### D. Export a normalized analysis bundle

```powershell
dotnet run --project src/Orchestrator -- export-experiment-analysis --dataset-name <langfuse-dataset-name> --run-names "<run-name-a>,<run-name-b>" --output artifacts/langfuse-experiments/analysis/<label>.analysis.json
```

Add more run names for 3-plus-run comparisons.

### E. Generate the statistical report

```powershell
uv run experiment-analysis-report --input artifacts/langfuse-experiments/analysis/<label>.analysis.json
```

This writes:

- `<label>.analysis.report.json`
- `<label>.analysis.report.md`
- `experiment-analysis/.../<label>.analysis.report.html` by default for browser viewing and GitHub Pages publishing

Add `--no-html-output` only when you explicitly do not want the browser artifact.

## Task-Type-Specific Interpretation

Use the report's primary metric in a task-aware way:

- `slice` reports are ranked primarily by `total_kicktipp_points`
- `repeated-match` reports are ranked primarily by `avg_kicktipp_points`
- `community-to-date` reports are ranked primarily by `total_kicktipp_points`

Statistical comparisons are still based on paired per-item Kicktipp-point differences.

For two-run comparisons the report includes:

- primary metric delta
- paired Wilcoxon signed-rank result
- bootstrap confidence intervals for mean and median paired differences
- per-item win/tie/loss counts

For three-or-more-run comparisons the report includes:

- Friedman omnibus test result
- corrected pairwise Wilcoxon comparisons
- per-item win/tie/loss counts for each run ordering

## Quality Checks

Before considering the workflow complete, verify all of the following:

1. each compared run completed successfully against the intended manifest
2. all compared runs target the same dataset name and prepared item set
3. `export-experiment-analysis` succeeded and emitted one bundle file
4. `uv run experiment-analysis-report` succeeded and emitted JSON, Markdown, and HTML outputs unless HTML was intentionally disabled
5. the report's primary metric matches the task type:
   - `total_kicktipp_points` for `slice`
   - `avg_kicktipp_points` for `repeated-match`
   - `total_kicktipp_points` for `community-to-date`

## Troubleshooting

- If dotnet commands cannot see Langfuse credentials, run them from the repo root so the Orchestrator can auto-load `../KicktippAi.Secrets/src/Orchestrator/.env`.
- If you need direct API inspection outside the Orchestrator flow, use `dotenvx` through [langfuse-api](../langfuse-api/SKILL.md).
- If a comparison run fails, do not export mixed successful and failed variants as if they were complete; re-run the failed variant or reduce the comparison set.
- If a justification variant returns empty model output, verify that variant independently before treating it as a comparable experimental arm.

## Example Prompts

- Run a slice experiment for `pes-squad`, compare `o3` vs `gpt-5-nano`, and generate the statistical report.
- Reuse this repeated-match manifest, run three models at one exact historical evaluation time, export the bundle, and summarize the report.
- Prepare a small community-to-date dataset for `schadensfresse` through one cutoff matchday, run the participant-backed experiment, export the comparable runs, and summarize the report.
- We already have a Langfuse dataset and two run names. Export them and generate the Wilcoxon report plus the browser artifact.

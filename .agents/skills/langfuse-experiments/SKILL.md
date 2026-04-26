---
name: langfuse-experiments
description: Run KicktippAi Langfuse experiments end to end. Use when preparing, syncing, or running slice, repeated-match, or community-to-date experiment datasets; exporting comparable runs; generating JSON, Markdown, and HTML comparison reports; publishing browser-friendly GitHub Pages experiment analysis pages; verifying the Pages index; and committing plus pushing the resulting tracked files.
---

# Langfuse Experiments

Use this skill to run KicktippAi Langfuse experiments, produce statistical comparison reports, publish browser-friendly report pages through the repository's GitHub Pages workflow, and commit plus push the tracked results.

## Grounding

Before running or publishing anything:

- Read `AGENTS.md`, `docs/langfuse.md`, `docs/langfuse/experiments/running-experiments.md`, and `docs/langfuse/experiments/analyzing-experiments.md`.
- Read `plans/langfuse-integration/phase-2/AGENTS.md` and linked trackers when the request is Phase 2 experiment work or changes experiment behavior.
- Prefer the repository's Orchestrator and Python tooling over direct ad hoc Langfuse API scripting.
- Run commands from the repository root so Orchestrator can auto-load the external secrets directory.
- Run every `dotnet` command outside the sandbox. Use `uv` for Python commands.

## Decision Flow

- If the user provides an existing Langfuse dataset name and run names, skip preparation, sync, and execution; export and report those runs.
- If the user provides a prepared `slice-dataset.json` and `slice-manifest.json`, sync if needed, then run variants against the same manifest.
- Choose `slice` for fixed historical match samples, `repeated-match` for variance on one fixture, and `community-to-date` for participant-backed Kicktipp snapshots through a cutoff matchday.
- Keep all settings fixed except the intended comparison axis, such as model, prompt key, hosted prompt, reasoning effort, justification setting, or evaluation policy.
- Use one shared UTC `$runStamp` for all related run names. Prefer `gpt-5-nano` for cheap verification unless the user specified models.

## Run Workflow

Prepare a dataset only when needed:

```powershell
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --sample-size 16 --sample-seed 20260403
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "VfB Stuttgart" --away "RB Leipzig" --matchday 26 --sample-size 25 --dataset-description "Short report context."
dotnet run --project src/Orchestrator -- prepare-community-to-date --community-context schadensfresse --cutoff-matchday 10
```

Sync the prepared hosted dataset:

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input path/to/slice-dataset.json
```

Run each comparable variant against the same manifest:

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
dotnet run --project src/Orchestrator -- run-slice o3 --manifest path/to/slice-manifest.json --run-name "slice__pes-squad__o3__prompt-v1__random-16-seed-20260403__startsat-12h__$runStamp" --prompt-key prompt-v1 --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-size 8 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match gpt-5-nano --manifest path/to/slice-manifest.json --run-name "repeated-match__pes-squad__gpt-5-nano__prompt-v1__repeat-25__exact-time__$runStamp" --prompt-key prompt-v1 --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 3 --replace-run
dotnet run --project src/Orchestrator -- run-community-to-date --manifest path/to/slice-manifest.json --run-family-name "community-to-date__schadensfresse__md10__$runStamp" --replace-runs
```

Use `reconstruct-prompt` with the intended `--evaluation-time` before expensive exact-time runs when historical context coverage is uncertain.

## Export And Report

Export comparable Langfuse runs into one normalized bundle:

```powershell
dotnet run --project src/Orchestrator -- export-experiment-analysis --dataset-name "match-predictions/bundesliga-2025-26/pes-squad/slices/all-matchdays/random-16-seed-20260403" --run-names "run-name-a,run-name-b" --output artifacts/langfuse-experiments/analysis/descriptive-name.analysis.json
```

Generate the statistical report:

```powershell
uv run experiment-analysis-report --input artifacts/langfuse-experiments/analysis/descriptive-name.analysis.json
```

Keep HTML enabled by default. The command writes report JSON and Markdown next to the analysis bundle, and writes the browser report under `experiment-analysis/...`. Use that `experiment-analysis/.../*.report.html` path as the tracked GitHub Pages artifact.

Publish analysis bundles back into Langfuse Experiments Beta only when requested or when repairing beta UI visibility:

```powershell
dotnet run --project src/Orchestrator -- publish-experiment-analysis --input artifacts/langfuse-experiments/analysis/descriptive-name.analysis.json --dry-run
dotnet run --project src/Orchestrator -- publish-experiment-analysis --input artifacts/langfuse-experiments/analysis/descriptive-name.analysis.json --replace-runs
```

## Pages Verification

Verify the hosted report index locally before committing or pushing:

```powershell
.github/scripts/Build-PagesSite.ps1 -CoverageReportDir coverage-report -ExperimentAnalysisDir experiment-analysis -OutputDir artifacts/pages-site-check
```

Then confirm `artifacts/pages-site-check/experiment-analysis/index.html` exists and links the new report, for example:

```powershell
Select-String -Path artifacts/pages-site-check/experiment-analysis/index.html -Pattern "new-report-file-name"
```

Do not commit `artifacts/pages-site-check`, `artifacts/langfuse-experiments`, `coverage-report`, or `pages-site`. Commit tracked browser reports under `experiment-analysis/` and the skill files only.

## Commit And Push

Before staging, inspect the diff and status. Stage only intended tracked deliverables:

```powershell
git diff -- .agents/skills/langfuse-experiments experiment-analysis
git status --short --branch
git add .agents/skills/langfuse-experiments experiment-analysis
git commit -m "Add Langfuse experiments skill"
```

For report-only publication commits, use a message such as `Publish Langfuse experiment analysis`.

Before requesting approval for a push, verify and record the exact target:

```powershell
git branch --show-current
git remote -v
git status --short --branch
git log -1 --oneline
```

Push with an explicit remote and branch, for example `git push origin main`, and include the exact commit, local branch, remote branch, and remote URL in the escalation justification. If git reports dubious ownership inside the sandbox, run the git command outside the sandbox instead of changing `safe.directory` unless the user asks for that configuration change.

## Quality Checks

- Verify all compared runs completed successfully against the same dataset and prepared item set.
- Verify `export-experiment-analysis` emitted one bundle with at least two unique run names.
- Verify `uv run experiment-analysis-report` emitted JSON, Markdown, and HTML unless HTML was intentionally disabled.
- Verify the report primary metric matches the task type: `total_kicktipp_points` for `slice` and `community-to-date`, `avg_kicktipp_points` for `repeated-match`.
- Verify the generated Pages index links the new report before pushing to `main`, because CI deploys GitHub Pages from pushes to `main`.

---
name: langfuse-experiments
description: Run KicktippAi Langfuse experiments end to end. Use when preparing, syncing, or running slice, repeated-match, repeated-match-slice, or community-to-date experiment datasets; exporting comparable runs; generating JSON, Markdown, and HTML comparison reports; publishing browser-friendly GitHub Pages experiment analysis pages; documenting experiment design and results under docs/experiments; verifying the Pages index; and committing plus pushing the resulting tracked files.
---

# Langfuse Experiments

Use this skill to run KicktippAi Langfuse experiments, produce statistical comparison reports, create companion long-form experiment writeups under `docs/experiments`, publish browser-friendly report pages through the repository's GitHub Pages workflow, and commit plus push the tracked results.

## Grounding

Before running or publishing anything:

- Read `AGENTS.md`, `docs/langfuse.md`, `docs/langfuse/experiments/running-experiments.md`, and `docs/langfuse/experiments/analyzing-experiments.md`.
- Keep workflow and tooling guidance under `docs/langfuse/experiments`, and keep experiment-specific narrative writeups under `docs/experiments`.
- When extending an existing investigation or publishing a new result, inspect a nearby `docs/experiments/*.md` file and follow that level of detail.
- Read `plans/langfuse-integration/phase-2/AGENTS.md` and linked trackers only when the request changes experiment behavior or needs historical implementation/design context.
- Use the official `$langfuse` skill and the installed `langfuse` command for generic Langfuse API inspection, docs lookup, SDK guidance, and prompt management. Keep this skill focused on KicktippAi-specific experiment orchestration and reporting.
- Prefer the repository's Orchestrator and Python tooling over direct ad hoc Langfuse API scripting.
- Run commands from the repository root so Orchestrator can auto-load the external secrets directory.
- Run every `dotnet` command outside the sandbox. Use `uv` for Python commands.

## Decision Flow

- If the user provides an existing Langfuse dataset name and run names, skip preparation, sync, and execution; export and report those runs.
- If the user provides a prepared `slice-dataset.json` and `slice-manifest.json`, sync only when the dataset is being created in Langfuse for the first time, or when the user explicitly says the dataset changed.
- If the prepared dataset already exists in Langfuse, assume it was not modified and skip resync by default to save execution time.
- If a run fails with errors that suggest missing, renamed, or drifted dataset items or dataset names, resync the dataset artifact and retry.
- Choose `slice` for fixed historical match samples, `repeated-match` for variance on one fixture, `repeated-match-slice` for repeated predictions over multiple sampled fixtures, and `community-to-date` for participant-backed Kicktipp snapshots through a cutoff matchday.
- Keep all settings fixed except the intended comparison axis, such as model, prompt key, hosted prompt, reasoning effort, justification setting, or evaluation policy.
- Use one shared UTC `$runStamp` for all related run names. Prefer `gpt-5-nano` for cheap verification unless the user specified models.

## Run Workflow

Prepare a dataset only when needed:

```powershell
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --sample-size 16 --sample-seed 20260403
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "VfB Stuttgart" --away "RB Leipzig" --matchday 26 --sample-size 25 --dataset-description "Short report context."
dotnet run --project src/Orchestrator -- prepare-repeated-match-slice --community-context pes-squad --match-count 15 --repetitions 10 --sample-seed 20260517
dotnet run --project src/Orchestrator -- prepare-community-to-date --community-context schadensfresse --cutoff-matchday 10
```

Sync the prepared hosted dataset only when you are creating it in Langfuse for the first time, or when explicit changes or run-time errors indicate the hosted dataset may be stale. Skip resync by default to save execution time:

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input path/to/slice-dataset.json
```

Run each comparable variant against the same manifest:

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
dotnet run --project src/Orchestrator -- run-slice o3 --manifest path/to/slice-manifest.json --run-name "slice__pes-squad__o3__prompt-v1__random-16-seed-20260403__startsat-12h__$runStamp" --prompt-key prompt-v1 --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-size 8 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match gpt-5-nano --manifest path/to/slice-manifest.json --run-name "repeated-match__pes-squad__gpt-5-nano__prompt-v1__repeat-25__exact-time__$runStamp" --prompt-key prompt-v1 --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 3 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match-slice gpt-5.4-nano --manifest path/to/slice-manifest.json --run-name "repeated-match-slice__pes-squad__gpt-5.4-nano__prompt-v1__random-15x10-seed-20260517__startsat-12h__$runStamp" --prompt-key prompt-v1 --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 3 --parallelism 5 --replace-run
dotnet run --project src/Orchestrator -- run-community-to-date --manifest path/to/slice-manifest.json --run-family-name "community-to-date__schadensfresse__md10__$runStamp" --replace-runs
```

For `run-repeated-match-slice`, `--parallelism` defaults to `5`. If a live run hits rate limits or flex-capacity failures, retry with `--parallelism 3`, then `--parallelism 1`.

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

## Document The Experiment

For any experiment that is being shared, used for a decision, or committed with a Pages artifact, create or update a companion writeup under `docs/experiments/<descriptive-slug>.md`. Use existing files in that folder as the pattern.

Cover at least:

- the question, hypothesis, or product decision being tested
- the dataset or slice identity, selection rules, sample size, seeds, and evaluation policy
- the compared variants and the single intended comparison axis that changed
- the primary results, uncertainty or statistical interpretation, and practical significance
- the decision or recommendation, limitations, and the next step if the result is inconclusive
- the exact tracked `experiment-analysis/.../*.report.html` path and a repo-relative link to that report

Back-linking from the published artifact needs special handling. `.github/scripts/Build-PagesSite.ps1` publishes `experiment-analysis/` into the Pages output, but it does not publish `docs/experiments/`. Do not add a broken Pages-relative link from the generated HTML report to `docs/experiments/...`. When the published artifact needs to link back to the long-form writeup, use the canonical GitHub URL for the committed `docs/experiments/...` file after the target remote and branch are known, or extend the publishing surface as part of the task.

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

If you added a link from the published artifact or related published surface back to `docs/experiments/...`, verify that it resolves to the committed GitHub URL rather than to a broken Pages-relative path.

Do not commit `artifacts/pages-site-check`, `artifacts/langfuse-experiments`, `coverage-report`, or `pages-site`. Commit tracked browser reports under `experiment-analysis/`, companion writeups under `docs/experiments/`, and the skill files only.

## Commit And Push

Before staging, inspect the diff and status. Stage only intended tracked deliverables:

```powershell
git diff -- .agents/skills/langfuse-experiments docs/experiments experiment-analysis
git status --short --branch
git add .agents/skills/langfuse-experiments docs/experiments experiment-analysis
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
- Verify any committed or published experiment result has a companion `docs/experiments/...` writeup that captures the design, results, interpretation, and the final report path.
- Verify any link from the published artifact back to the long-form writeup uses a valid GitHub URL or another actually published surface, not a relative `docs/experiments/...` Pages path.
- Verify the report primary metric matches the task type: `total_kicktipp_points` for `slice` and `community-to-date`, `avg_kicktipp_points` for `repeated-match` and `repeated-match-slice`.
- Verify the generated Pages index links the new report before pushing to `main`, because CI deploys GitHub Pages from pushes to `main`.

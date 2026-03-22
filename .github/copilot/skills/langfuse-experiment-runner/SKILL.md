---
name: langfuse-experiment-runner
description: Export exact-timestamp experiment items and run hosted Langfuse dataset experiments for one or more models.
---

# Langfuse Experiment Runner

Use this skill to run the Task 5 experiment flow end-to-end:

1. Export one exact-timestamp experiment item per model from the .NET orchestrator
2. Execute the JS runner against the canonical hosted Langfuse dataset
3. Print the verified dataset run identifiers and aggregate scores

## When To Use It

- Run the first milestone experiment for a single match or a small sample
- Compare multiple models against the same historical evaluation timestamp
- Re-run the same experiment with `-ReplaceRun` after changing prompts or scoring

## First Experiment Example

```powershell
.github/copilot/skills/langfuse-experiment-runner/scripts/Invoke-LangfuseExperiment.ps1 \
  -HomeTeam "VfB Stuttgart" \
  -AwayTeam "RB Leipzig" \
  -Matchday 26 \
  -EvaluationTime "2026-03-15T12:00 Europe/Berlin" \
  -Models @("o3", "gpt-5-nano") \
  -Repetitions 17 \
  -BatchSize 8 \
  -ReplaceRun
```

## Parameters

- `-HomeTeam` and `-AwayTeam`: match identity
- `-Matchday`: historical matchday to reconstruct
- `-EvaluationTime`: exact local timestamp plus TZDB zone, for example `2026-03-15T12:00 Europe/Berlin`
- `-Models`: models to compare, defaults to `o3` and `gpt-5-nano`
- `-CommunityContext`: defaults to `pes-squad`
- `-Repetitions`: defaults to `17`
- `-BatchSize`: defaults to `8`
- `-ReplaceRun`: delete any existing dataset run with the same name before execution

## Notes

- The wrapper keeps the first repetition serial for every exported item before batching later repetitions.
- It uses the exact-timestamp `.NET` export path, so the prompt reflects the chosen historical evaluation instant.
- The JS runner verifies the dataset run via the Langfuse API before exit and prints `datasetRunId`, run counts, and aggregate scores.
- For deeper post-run inspection, use the existing [langfuse-api](../langfuse-api/SKILL.md) skill.

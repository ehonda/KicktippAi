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
  -EvaluationTime "2026-03-15T12:00:00 Europe/Berlin (+01)" \
  -Models @("o3", "gpt-5-nano") \
  -Repetitions 5 \
  -BatchSize 8 \
  -ReplaceRun
```

## Parameters

- `-HomeTeam` and `-AwayTeam`: match identity
- `-Matchday`: historical matchday to reconstruct
- `-EvaluationTime`: explicit evaluation time in NodaTime's invariant ZonedDateTime `G` pattern, for example `2026-03-15T12:00:00 Europe/Berlin (+01)`
- `-Models`: models to compare, defaults to `o3` and `gpt-5-nano`
- `-CommunityContext`: defaults to `pes-squad`
- `-Repetitions`: defaults to `5` for development runs
- `-BatchSize`: defaults to `8`
- `-ReplaceRun`: delete any existing dataset run with the same name before execution

## Notes

- The wrapper keeps the first repetition serial for every exported item before batching later repetitions.
- Each repetition is stored as its own Langfuse dataset run under a shared run-family name so every repetition stays visible in the current Langfuse UI/API behavior.
- It uses the exact-timestamp `.NET` export path, so the prompt reflects the chosen historical evaluation instant.
- The runner sets the OTEL header `x-langfuse-ingestion-version: 4` on the Langfuse span processor so traces show up on the faster ingestion path.
- The JS runner verifies every repetition run via the Langfuse API before exit and prints the run-family name, repetition-level dataset run ids, run counts, and aggregate scores.
- For deeper post-run inspection, use the existing [langfuse-api](../langfuse-api/SKILL.md) skill.

Use `-Repetitions 17` for the full milestone run after development validation.

Current limitation: because repetitions are spread across multiple dataset runs to stay visible in the current Langfuse UI/API behavior, Langfuse's built-in run-average tables do not directly show a single cross-repetition average per model. Use the wrapper's returned aggregate scores for now and revisit the data model in a follow-up session.

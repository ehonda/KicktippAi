---
name: langfuse-experiment-runner
description: Export exact-timestamp experiment items and run hosted Langfuse dataset experiments for one or more models.
---

# Langfuse Experiment Runner

Use this skill to run the Task 5 experiment flow end-to-end.

Two modes are supported:

1. Legacy single-match mode: export one exact-timestamp experiment item per model and run the original JS repetition runner
2. Sampled slice mode: refresh the canonical hosted dataset, select a deterministic slice, export one reconstructed item per selected match and model, execute the Task 5 slice runner, and verify traces via the Langfuse API skill

## When To Use It

- Run the first milestone experiment for a single match or a small sample
- Compare multiple models against the same historical evaluation timestamp
- Re-run the same experiment with `-ReplaceRun` after changing prompts or scoring

## Sampled Slice Example

```powershell
.github/copilot/skills/langfuse-experiment-runner/scripts/Invoke-LangfuseExperiment.ps1 \
  -SampleCanonicalDataset \
  -SampleSize 10 \
  -Models @("o3", "gpt-5-nano") \
  -BatchSize 10 \
  -ReplaceRun
```

## Legacy Single-Match Example

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

- `-SampleCanonicalDataset`: run the sampled Task 5 slice flow against the canonical hosted dataset
- `-SampleSize`: number of dataset items to sample, defaults to `10`
- `-SampleSeed`: optional deterministic random seed for slice selection
- `-EvaluationPolicyKind`: sampled-slice evaluation policy kind, currently `relative`
- `-EvaluationPolicyOffset`: sampled-slice NodaTime duration offset against `startsAt`, defaults to `-12:00:00`
- `-PromptKey`: short prompt variant identifier used in run metadata and run names, defaults to `prompt-v1`
- `-HomeTeam` and `-AwayTeam`: match identity for legacy single-match mode
- `-Matchday`: historical matchday to reconstruct for legacy single-match mode
- `-EvaluationTime`: explicit evaluation time in NodaTime's invariant ZonedDateTime `G` pattern for legacy single-match mode, for example `2026-03-15T12:00:00 Europe/Berlin (+01)`
- `-Models`: models to compare, defaults to `o3` and `gpt-5-nano`
- `-CommunityContext`: defaults to `pes-squad`
- `-Repetitions`: defaults to `5` for legacy development runs
- `-BatchSize`: defaults to `10` for sampled slices and `8` for legacy single-match runs unless overridden
- `-SkipDatasetRefresh`: skip the canonical dataset refresh step before sampled-slice runs
- `-SkipLangfuseVerification`: skip the autonomous Langfuse API verification step after sampled-slice runs
- `-ReplaceRun`: delete any existing dataset run with the same name before execution

## Notes

- Sampled slice runs use one dataset run per model on one fixed slice and process the slice directly in parallel batches.
- Sampled slice runs use the relative evaluation policy path in `.NET`, currently limited to offsets relative to `startsAt`.
- Legacy single-match mode keeps the original repetition-oriented runner behavior.
- It uses the exact-timestamp `.NET` export path, so the prompt reflects the chosen historical evaluation instant.
- The runner sets the OTEL header `x-langfuse-ingestion-version: 4` on the Langfuse span processor so traces show up on the faster ingestion path.
- Sampled slice runs additionally verify traces and generation observations through the dedicated [langfuse-api](../langfuse-api/SKILL.md) skill script before returning summary output.
- For deeper post-run inspection, use the existing [langfuse-api](../langfuse-api/SKILL.md) skill.

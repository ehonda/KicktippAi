# Phase 2 — Common Context

This document captures the shared context needed across all Phase 2 tasks so a new session can start implementation without reconstructing the planning background.

## Scope

Phase 2 introduces **evaluation and experiments** on top of the completed Langfuse tracing foundation from Phase 1.

The first milestone is:

- Build a repeatable experiment workflow for **Bundesliga 2025/2026 match predictions**
- Use **hosted Langfuse datasets** so experiments produce dataset runs that can be compared in the Langfuse UI
- Score results primarily by **Kicktipp points**, with supporting diagnostics for debugging and prompt iteration

Out of scope for the first milestone:

- Bonus prediction experiments
- LLM-as-a-Judge rollout for justification quality
- Phase 3 prompt management integration

## Decisions Already Made

- Phase 1 tracing is complete and should be treated as stable input infrastructure
- Phase 2 planning is now in progress and tracked in this directory
- Prefer the **Python SDK first** for Langfuse experiment execution because the experiment runner and evaluation examples are strongest there
- Keep **JS/TS as the fallback** if Python setup becomes the main source of friction
- Keep the **data preparation and prompt reconstruction logic in .NET**, close to the existing repositories and domain models
- Use a **hosted Langfuse dataset**, not just a local in-memory dataset, because hosted datasets create dataset runs and support comparison views

## Langfuse Modeling Notes

For the first match experiment pipeline:

- Dataset item `input` should be the match-to-predict payload
- Dataset item `expectedOutput` should be the actual match result and scoring ground truth
- Dataset item `metadata` should contain prompt reconstruction and filtering data
- The experiment task output is the newly generated prediction for the run
- Historical predictions may be stored in metadata as a baseline, but they should not be modeled as the dataset item output

## Repository Findings Relevant To Phase 2

Existing reusable pieces:

- [src/FirebaseAdapter/FirebasePredictionRepository.cs](src/FirebaseAdapter/FirebasePredictionRepository.cs): historical prediction timestamps, model/community filters, context document names, and reprediction indices
- [src/FirebaseAdapter/FirebaseContextRepository.cs](src/FirebaseAdapter/FirebaseContextRepository.cs): versioned context documents, including lookup by exact version
- [src/OpenAiIntegration/IPredictionService.cs](src/OpenAiIntegration/IPredictionService.cs): prediction surface for match and bonus predictions
- [src/OpenAiIntegration/PredictionService.cs](src/OpenAiIntegration/PredictionService.cs): current `predict-match` prompt-building and tracing behavior
- [src/Orchestrator/Commands/Operations/RandomMatch/RandomMatchCommand.cs](src/Orchestrator/Commands/Operations/RandomMatch/RandomMatchCommand.cs): single-match prediction workflow and Langfuse trace metadata pattern
- [src/Orchestrator/Commands/Observability/AnalyzeMatch/AnalyzeMatchDetailedCommand.cs](src/Orchestrator/Commands/Observability/AnalyzeMatch/AnalyzeMatchDetailedCommand.cs): repeated-run analysis pattern that is useful reference material for experiments

Known gaps:

- Actual match outcomes for experiments should be collected from Kicktipp `tippuebersicht` pages by `spieltagIndex`, then persisted into Firebase as the internal authoritative store used by dataset export
- The current repository surface is good for reading predictions, but a dedicated **export/materialization seam** is still needed for experiment items
- Prompt reconstruction rules are not yet materialized into a reusable service or export contract

## Task 1 Decisions

- Dataset scope is the Kicktipp `community`
- The first rollout should target only `pes-squad`
- The initial hosted dataset slice should include all completed Bundesliga 2025/2026 matches available in `pes-squad`
- Only matches with persisted outcomes are eligible dataset items
- Outcome collection should be implemented as a reusable .NET service and integrated into `collect-context`
- Automatic Langfuse dataset updates remain a later improvement, not part of the initial implementation

## Manual Validation Guidance

Use these repo conventions during implementation:

- Prefer `gpt-5-nano` for cheap and fast manual validation runs
- Prefer `ehonda-test-buli` as the default test community

```powershell
dotnet run --project src/Orchestrator -- matchday gpt-5-nano --community ehonda-test-buli
```

Useful commands:

```powershell
dotnet run --project src/Orchestrator -- random-match gpt-5-nano --community ehonda-test-buli
```

Useful Langfuse inspection command:

```powershell
.github/copilot/skills/langfuse-api/scripts/Query-LangfuseApi.ps1 -Endpoint "traces" -QueryParams @{limit=10}
```

## Handoff Expectations

A new session should be able to continue the work by reading:

1. [AGENTS.md](AGENTS.md)
2. This file
3. [01-phase-2-tracker.md](01-phase-2-tracker.md)
4. [manual-steps.md](tasks/manual-steps.md)
5. The next incomplete numbered task tracker

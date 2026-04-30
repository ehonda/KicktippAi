# Langfuse Integration - Implementation Status

## Summary

The initial Langfuse integration is complete. This planning directory is now mostly historical; active usage documentation lives in:

- [docs/langfuse.md](../../docs/langfuse.md) for repository-specific tracing and filtering behavior
- [docs/langfuse/experiments](../../docs/langfuse/experiments) for experiment preparation, execution, analysis, and publishing
- [agent-files/langfuse-agent-tooling.md](../../agent-files/langfuse-agent-tooling.md) for global agent/CLI/MCP setup

| Phase | Title | Status |
|-------|-------|--------|
| **1** | Observability & Cost Tracking | Complete |
| **2** | Evaluation & Experiments | Complete for the initial integration |
| **3** | Prompt Management | POC complete; broader migration deferred |

---

## Phase 1 - Observability & Cost Tracking

**Status:** Complete

All implementation steps from [01-phase-1-observability.md](01-phase-1-observability.md) are done and verified.

### Completed Work

| File | Change |
|------|--------|
| `src/Orchestrator/Orchestrator.csproj` | Added OpenTelemetry packages and related dependency updates. |
| `src/OpenAiIntegration/Telemetry.cs` | Shared `ActivitySource` named `"KicktippAi"`. |
| `src/OpenAiIntegration/ICostCalculationService.cs` | Added `CostBreakdown` and detailed cost calculation. |
| `src/OpenAiIntegration/CostCalculationService.cs` | Implemented detailed cost calculation while preserving existing total-cost behavior. |
| `src/OpenAiIntegration/PredictionService.cs` | Added generation spans and Langfuse observation attributes. |
| `src/Orchestrator/Commands/Operations/Matchday/MatchdayCommand.cs` | Added root workflow activity and trace-level metadata. |
| `src/Orchestrator/Commands/Operations/Bonus/BonusCommand.cs` | Added root workflow activity and trace-level metadata. |
| `src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs` | Registers Langfuse tracing and the public API client when credentials are present. |

### Resolved Issues

- [phase-1-traces-missing](issues/resolved/phase-1-traces-missing.md) - Traces were not arriving in Langfuse because the app used a raw `ServiceCollection`/Spectre.Console.Cli setup where hosted services are not started. The fix eagerly builds and registers the `TracerProvider` for disposal on shutdown.

---

## Phase 2 - Evaluation & Experiments

**Status:** Complete for the initial integration

The repository now has a first-class Langfuse experiment workflow for prepared historical football prediction datasets. The active documentation is under [docs/langfuse/experiments](../../docs/langfuse/experiments).

Implemented capabilities include:

- prepared slice, repeated-match, and community-to-date datasets
- hosted dataset sync through Langfuse's public API
- experiment execution with SDK-compatible experiment markers for Langfuse Experiments Beta
- Kicktipp item-level and run-level scoring
- normalized analysis export from Langfuse-backed runs
- Python-based statistical reports with JSON, Markdown, and HTML outputs
- optional publication of report pages through the repository's GitHub Pages workflow

The detailed Phase 2 trackers under [phase-2](phase-2) are retained as historical implementation records. Use them when investigating design decisions or changing experiment behavior, not as the default starting point for running experiments.

---

## Phase 3 - Prompt Management

**Status:** POC complete; broader migration deferred

The initial prompt-management work proved an opt-in Langfuse hosted prompt route for experiment runs. File-based prompts remain the default for production and ordinary local runs.

Current POC behavior:

- experiment runs can opt into hosted prompts with the Langfuse prompt source flags
- hosted prompt name, label, and version are recorded in run metadata, trace metadata, and Langfuse prompt-link observation tags
- the POC prompt `kicktippai/predict-one-match-o3-poc` is documented in [running-experiments.md](../../docs/langfuse/experiments/running-experiments.md)

Deferred work:

- production prompt migration
- hosted prompts for bonus predictions
- hosted prompt support for justification prompts
- a full prompt promotion/versioning workflow

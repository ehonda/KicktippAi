# Langfuse Integration — Implementation Status

## Summary

| Phase | Title | Status |
|-------|-------|--------|
| **1** | Observability & Cost Tracking | ✅ Complete |
| **2** | Evaluation & Experiments | ⬜ Not started |
| **3** | Prompt Management | ⬜ Not started |

---

## Phase 1 — Observability & Cost Tracking

**Status:** ✅ Complete

All implementation steps from [01-phase-1-observability.md](01-phase-1-observability.md) are done and verified.

### Completed Work

| File | Change |
|------|--------|
| `src/Orchestrator/Orchestrator.csproj` | Added `OpenTelemetry` 1.15.0, `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.15.0. Upgraded `Microsoft.Extensions.*` from 9.0.1 → 10.0.3. |
| `src/OpenAiIntegration/Telemetry.cs` (new) | Shared `ActivitySource` named `"KicktippAi"` |
| `src/OpenAiIntegration/ICostCalculationService.cs` | Added `CostBreakdown` record and `CalculateCostBreakdown()` method |
| `src/OpenAiIntegration/CostCalculationService.cs` | Implemented `CalculateCostBreakdown()`; refactored `CalculateCost()` to delegate to it |
| `src/OpenAiIntegration/PredictionService.cs` | Added `Activity` spans + `SetLangfuseGenerationAttributes()` helper |
| `src/Orchestrator/Commands/Operations/Matchday/MatchdayCommand.cs` | Root activity span `"matchday-workflow"` with Langfuse trace-level attributes |
| `src/Orchestrator/Commands/Operations/Bonus/BonusCommand.cs` | Root activity span `"bonus-workflow"` with Langfuse trace-level attributes |
| `src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs` | `AddLangfuseTracing()` — eagerly builds `TracerProvider` via `Sdk.CreateTracerProviderBuilder()` |

### Resolved Issues

- [phase-1-traces-missing](issues/resolved/phase-1-traces-missing.md) — Traces were not arriving in Langfuse. Root cause: `AddOpenTelemetry()` (from `OpenTelemetry.Extensions.Hosting`) relies on `IHostedService`, which is never started in this app's raw `ServiceCollection`/Spectre.Console.Cli setup. Fixed by building the `TracerProvider` eagerly with `Sdk.CreateTracerProviderBuilder()` and registering it as a singleton for disposal on shutdown.

---

## Phase 2 — Evaluation & Experiments

**Status:** ⬜ Not started

See [02-phase-2-evaluation.md](02-phase-2-evaluation.md).

---

## Phase 3 — Prompt Management

**Status:** ⬜ Not started

See [03-phase-3-prompt-management.md](03-phase-3-prompt-management.md).

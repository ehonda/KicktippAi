# Phase 1 — Observability & Cost Tracking

## Objective

Instrument the Orchestrator and `PredictionService` with OpenTelemetry so every prediction run produces a Langfuse trace with nested generation observations, token usage, and cost data.

## Prerequisites

See [manual-steps.md](manual-steps.md) for Langfuse account and credential setup — these must be completed before starting implementation.

## Implementation Steps

### 1. Add NuGet Packages

Add to `src/Orchestrator/Orchestrator.csproj` (or a shared project if preferred):

- `OpenTelemetry`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `OpenTelemetry.Extensions.Hosting`

Always check for the latest versions before adding.

### 2. Add Langfuse Credentials to `.env`

Add three new environment variables (loaded by `EnvironmentHelper`):

```
LANGFUSE_PUBLIC_KEY=pk-lf-...
LANGFUSE_SECRET_KEY=sk-lf-...
LANGFUSE_BASE_URL=https://cloud.langfuse.com
```

`LANGFUSE_BASE_URL` should default to `https://cloud.langfuse.com` if not set.

### 3. Configure the OpenTelemetry Pipeline

Create a new configuration class or extend `ServiceRegistrationExtensions` to:

- Read Langfuse credentials from environment variables
- If credentials are present, register the OTel tracing pipeline:
  - Register an `ActivitySource` (e.g., `"KicktippAi"`)
  - Add the OTLP HTTP exporter:
    - Endpoint: `{LANGFUSE_BASE_URL}/api/public/otel`
    - Protocol: `OtlpExportProtocol.HttpProtobuf` (Langfuse does **not** support gRPC)
    - Header: `Authorization=Basic {base64(publicKey:secretKey)}`
- If credentials are absent, skip OTel registration entirely (graceful degradation)

### 4. Define a Shared `ActivitySource`

Create a static class (e.g., `Telemetry` in `OpenAiIntegration` or `Core`) exposing a single `ActivitySource`:

```csharp
internal static class Telemetry
{
    public static readonly ActivitySource Source = new("KicktippAi");
}
```

Both the Orchestrator commands and `PredictionService` will use this to create activities.

### 5. Instrument `PredictionService`

In `PredictMatchAsync` and `PredictBonusQuestionAsync`:

1. Start an `Activity` via `Telemetry.Source.StartActivity("predict-match")` (or `"predict-bonus"`)
2. After the OpenAI API response, set Langfuse-mapped attributes on the `Activity`:

| Attribute | Value |
|-----------|-------|
| `langfuse.observation.type` | `"generation"` |
| `gen_ai.request.model` | Model name (e.g., `"o3"`) |
| `langfuse.observation.input` | Serialized messages JSON (system prompt + user message) |
| `langfuse.observation.output` | The structured JSON response text |
| `langfuse.observation.usage_details` | JSON object: `{ "input": N, "output": N, "cache_read_input_tokens": N, "reasoning_tokens": N }` from `ChatTokenUsage` |
| `langfuse.observation.cost_details` | JSON object: `{ "input": X, "cache_read_input_tokens": X, "output": X, "total": X }` in USD, computed by `CostCalculationService` |

### 6. Instrument Orchestrator Commands

Wrap `MatchdayCommand.ExecuteMatchdayWorkflow` and `BonusCommand` in root `Activity` spans:

- Set trace-level attributes:
  - `langfuse.session.id` → e.g., `"matchday-27-ehonda-test-buli"`
  - `langfuse.trace.tags` → `[community, model]`
  - `langfuse.trace.metadata.community` → community name
  - `langfuse.trace.metadata.matchday` → matchday number
  - `langfuse.trace.metadata.model` → model name
- Each `PredictionService` call becomes a child generation span automatically via OTel context propagation

### 7. Flush on Exit

The Orchestrator is a short-lived CLI app. Ensure `TracerProvider.ForceFlush()` or `Dispose()` is called before process exit so all spans reach Langfuse. This can be handled via the DI container's disposal or an explicit call at the end of command execution.

### 8. Graceful Degradation

- If `LANGFUSE_PUBLIC_KEY` or `LANGFUSE_SECRET_KEY` are not set, do not register the OTel pipeline
- `Telemetry.Source.StartActivity()` returns `null` when no listener is registered — all instrumentation code should use `activity?.SetTag(...)` patterns and thus becomes a no-op
- Existing CI, testing, and local development workflows are unaffected

## Verification

1. Configure Langfuse credentials in `.env`
2. Run: `dotnet run --project src/Orchestrator -- matchday gpt-5-nano --community ehonda-test-buli`
3. Check Langfuse Cloud dashboard:
   - A trace appears with the correct session ID and tags
   - Child generation observations show model name, prompt, response, token usage, and cost
   - Cost data matches the existing `TokenUsageTracker` CLI output
4. Remove Langfuse credentials from `.env` and re-run — verify no errors (graceful degradation)

## Files Likely Modified

| File | Change |
|------|--------|
| `src/Orchestrator/Orchestrator.csproj` | Add OTel NuGet packages |
| `src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs` | Register OTel pipeline |
| `src/OpenAiIntegration/Telemetry.cs` (new) | Shared `ActivitySource` |
| `src/OpenAiIntegration/PredictionService.cs` | Add `Activity` spans + Langfuse attributes |
| `src/Orchestrator/Commands/Operations/Matchday/MatchdayCommand.cs` | Root activity span |
| `src/Orchestrator/Commands/Operations/Bonus/BonusCommand.cs` | Root activity span |
| `src/Orchestrator/EnvironmentHelper.cs` | Load Langfuse env vars |
| `.env` | Add Langfuse credentials |

# Phase 1 — Traces Not Appearing in Langfuse Dashboard

## Status

**Open** — Traces are not arriving in Langfuse despite a successful prediction run (exit code 0, predictions placed).

## Context

Phase 1 observability (see [01-phase-1-observability.md](../../01-phase-1-observability.md)) has been fully implemented: all files modified, builds pass, unit tests pass (140 OpenAiIntegration + 600 Orchestrator). However, manual verification shows **nothing in the Langfuse dashboard** after running:

```powershell
dotnet run --project src/Orchestrator -- matchday gpt-5-nano --community ehonda-test-buli
```

The command completes successfully (exit code 0, 8 predictions placed), but no traces appear.

## What Was Implemented

All changes listed in the plan's "Files Likely Modified" table are in place:

| File | Change |
|------|--------|
| `src/Orchestrator/Orchestrator.csproj` | Added `OpenTelemetry` 1.15.0, `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.15.0. Upgraded `Microsoft.Extensions.*` from 9.0.1 → 10.0.3 (required by OTel transitive deps). |
| `src/OpenAiIntegration/Telemetry.cs` (new) | Shared `ActivitySource` named `"KicktippAi"` |
| `src/OpenAiIntegration/ICostCalculationService.cs` | Added `CostBreakdown` record and `CalculateCostBreakdown()` method |
| `src/OpenAiIntegration/CostCalculationService.cs` | Implemented `CalculateCostBreakdown()`; refactored `CalculateCost()` to delegate to it |
| `src/OpenAiIntegration/PredictionService.cs` | Added `Activity` spans in `PredictMatchAsync` ("predict-match") and `PredictBonusQuestionAsync` ("predict-bonus"), plus `SetLangfuseGenerationAttributes()` helper |
| `src/Orchestrator/Commands/Operations/Matchday/MatchdayCommand.cs` | Root activity span `"matchday-workflow"` with Langfuse trace-level attributes |
| `src/Orchestrator/Commands/Operations/Bonus/BonusCommand.cs` | Root activity span `"bonus-workflow"` with Langfuse trace-level attributes |
| `src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs` | `AddLangfuseTracing()` method — see attempts below |

## Attempt 1: Host-Based `AddOpenTelemetry()` (Original / Preferred)

This is the standard approach from the plan and the one we'd prefer if we can make it work.

### Code

```csharp
// In ServiceRegistrationExtensions.cs — AddLangfuseTracing()

using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static IServiceCollection AddLangfuseTracing(this IServiceCollection services)
{
    var publicKey = Environment.GetEnvironmentVariable("LANGFUSE_PUBLIC_KEY");
    var secretKey = Environment.GetEnvironmentVariable("LANGFUSE_SECRET_KEY");

    if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(secretKey))
        return services;

    var baseUrl = Environment.GetEnvironmentVariable("LANGFUSE_BASE_URL") ?? "https://cloud.langfuse.com";
    var authHeader = $"Authorization=Basic {Convert.ToBase64String(
        Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}"))}";

    services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("KicktippAi"))
        .WithTracing(tracing => tracing
            .AddSource(Telemetry.Source.Name)
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri($"{baseUrl}/api/public/otel/v1/traces");
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.Headers = authHeader;
            }));

    return services;
}
```

### Required Package

```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.15.0" />
```

### Why It Didn't Work

`AddOpenTelemetry()` (from `OpenTelemetry.Extensions.Hosting`) registers an `IHostedService` that **lazily builds** the `TracerProvider` when the host starts. This Orchestrator app uses a raw `ServiceCollection` with Spectre.Console.Cli's `TypeRegistrar`/`TypeResolver` — there is **no `IHost`**, so the `IHostedService` is never started and the `TracerProvider` is never constructed. All `StartActivity()` calls return `null`.

### Endpoint Note

The endpoint used was `{baseUrl}/api/public/otel/v1/traces`. This may also be incorrect — the OTLP HTTP exporter by default auto-appends `/v1/traces` to whatever base URL you provide, so doubling up would produce `{baseUrl}/api/public/otel/v1/traces/v1/traces`. The correct base for the exporter is `{baseUrl}/api/public/otel` (the exporter adds `/v1/traces` itself).

## Attempt 2: Eager `Sdk.CreateTracerProviderBuilder()` (Current)

To bypass the `IHost` requirement, we switched to building the `TracerProvider` eagerly.

### Code (current state of `ServiceRegistrationExtensions.cs`)

```csharp
public static IServiceCollection AddLangfuseTracing(this IServiceCollection services)
{
    var publicKey = Environment.GetEnvironmentVariable("LANGFUSE_PUBLIC_KEY");
    var secretKey = Environment.GetEnvironmentVariable("LANGFUSE_SECRET_KEY");

    if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(secretKey))
        return services;

    var baseUrl = Environment.GetEnvironmentVariable("LANGFUSE_BASE_URL") ?? "https://cloud.langfuse.com";
    var authHeader = $"Authorization=Basic {Convert.ToBase64String(
        Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}"))}";

    // Build eagerly — no IHost needed
    var tracerProvider = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("KicktippAi"))
        .AddSource(Telemetry.Source.Name)
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri($"{baseUrl}/api/public/otel");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
            options.Headers = authHeader;
        })
        .Build()!;

    // Register as singleton so DI container disposes it on shutdown
    // (TypeResolver.Dispose() → ServiceProvider.Dispose() → TracerProvider.Dispose() → ForceFlush)
    services.AddSingleton(tracerProvider);

    return services;
}
```

### Package Changes

Removed `OpenTelemetry.Extensions.Hosting` (no longer needed).

### Result

**Still not working** — traces do not appear in Langfuse after running the matchday command.

## Disposal / Flush Path

The flush path should work as follows:

1. Spectre.Console.Cli calls `TypeResolver.Dispose()` after command execution
2. `TypeResolver.Dispose()` calls `ServiceProvider.Dispose()`
3. `ServiceProvider.Dispose()` disposes all singletons, including `TracerProvider`
4. `TracerProvider.Dispose()` calls `ForceFlush()` on all exporters, sending buffered spans to Langfuse

`TypeResolver` code (for reference):

```csharp
public sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider) =>
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public object? Resolve(Type? type) => type is null ? null : _provider.GetService(type);

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
            disposable.Dispose();
    }
}
```

**Open question:** Is `TypeResolver.Dispose()` actually being called? If not, the `TracerProvider` never flushes and spans are lost.

## Debugging Checklist

Things to verify in the next debugging session:

### 1. Are credentials loaded?

Add a diagnostic log or breakpoint to confirm `LANGFUSE_PUBLIC_KEY` and `LANGFUSE_SECRET_KEY` are non-empty at the point `AddLangfuseTracing()` reads them. The `.env` file is loaded by `EnvironmentHelper.LoadEnvironmentVariables` on startup.

### 2. Is the `TracerProvider` actually being built?

Add a log/breakpoint after `Sdk.CreateTracerProviderBuilder()...Build()` to confirm the provider is non-null.

### 3. Does `StartActivity()` return non-null?

Add a log in `MatchdayCommand.ExecuteMatchdayWorkflow` right after `Telemetry.Source.StartActivity("matchday-workflow")` to check if `activity` is null or not. If null, the `ActivitySource` listener is not registered (provider issue).

### 4. Is `TypeResolver.Dispose()` called?

Verify that Spectre.Console.Cli actually calls `Dispose()` on the `TypeRegistrar`/`TypeResolver`. If not, `TracerProvider` is never flushed and spans are silently lost. Consider adding an explicit `ForceFlush()` at the end of command execution as a workaround.

### 5. Is the OTLP exporter getting HTTP errors?

Enable OpenTelemetry self-diagnostics to see if the exporter logs transport errors:

```csharp
// Temporary diagnostic — add before building the TracerProvider
OpenTelemetry.Internal.OpenTelemetrySdkEventSource.Log.EnableEvents(
    System.Diagnostics.Tracing.EventLevel.Verbose);
```

Or set environment variable `OTEL_LOG_LEVEL=debug` before running.

### 6. Is the endpoint correct?

With `Sdk.CreateTracerProviderBuilder()`, the OTLP HTTP exporter auto-appends `/v1/traces` to the configured endpoint. So the configured endpoint should be:

```
https://cloud.langfuse.com/api/public/otel
```

Which results in the actual HTTP POST going to:

```
https://cloud.langfuse.com/api/public/otel/v1/traces
```

Verify this matches what Langfuse expects.

### 7. Is the auth header formatted correctly?

Expected format: `Authorization=Basic {base64(publicKey:secretKey)}`. Note the `=` (not `:`) between key and value — this is the OTLP exporter's header format, not standard HTTP. Verify this is correct for the `OpenTelemetry.Exporter.OpenTelemetryProtocol` package.

### 8. Registration timing issue?

The `TracerProvider` is built inside `AddLangfuseTracing()`, which is called from `AddOrchestratorInfrastructure()`, which is called during `TypeRegistrar.Build()`. Verify this happens **before** any `StartActivity()` calls. Since command execution happens after DI setup, this should be fine, but worth confirming.

## Key Architectural Constraint

This app uses **Spectre.Console.Cli** with a raw `ServiceCollection`/`ServiceProvider` — there is **no `IHost`** or `IHostBuilder`. Any OTel integration pattern that relies on `IHostedService`, `IHostApplicationLifetime`, or `IHostedLifecycleService` will silently fail. The solution must either:

1. Build the `TracerProvider` eagerly (Attempt 2), or
2. Manually resolve and start the `IHostedService` from the `ServiceProvider`, or
3. Explicitly call `TracerProvider.ForceFlush()`/`Dispose()` at the end of command execution

## Instrumented Files Reference

### `src/OpenAiIntegration/Telemetry.cs`

```csharp
using System.Diagnostics;

namespace OpenAiIntegration;

public static class Telemetry
{
    public static readonly ActivitySource Source = new("KicktippAi");
}
```

### `src/OpenAiIntegration/PredictionService.cs` — Instrumented Methods

**`PredictMatchAsync`** — starts `"predict-match"` activity, calls `SetLangfuseGenerationAttributes()` after API response.

**`PredictBonusQuestionAsync`** — starts `"predict-bonus"` activity, calls `SetLangfuseGenerationAttributes()` after API response.

**`SetLangfuseGenerationAttributes()`** — private helper that sets:
- `langfuse.observation.type` = `"generation"`
- `gen_ai.request.model` = model name
- `langfuse.observation.input` = serialized messages JSON
- `langfuse.observation.output` = response JSON
- `langfuse.observation.usage_details` = `{ input, output, cache_read_input_tokens, reasoning_tokens }`
- `langfuse.observation.cost_details` = `{ input, cache_read_input_tokens, output, total }` (from `CalculateCostBreakdown`)

### `src/Orchestrator/Commands/Operations/Matchday/MatchdayCommand.cs`

Root activity `"matchday-workflow"` in `ExecuteMatchdayWorkflow()`. Sets `langfuse.session.id`, `langfuse.trace.tags`, `langfuse.trace.metadata.*`.

### `src/Orchestrator/Commands/Operations/Bonus/BonusCommand.cs`

Root activity `"bonus-workflow"` in `ExecuteBonusWorkflow()`. Sets `langfuse.session.id`, `langfuse.trace.tags`, `langfuse.trace.metadata.*`.

## Packages in `Orchestrator.csproj`

```xml
<PackageReference Include="OpenTelemetry" Version="1.15.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.15.0" />
<!-- OpenTelemetry.Extensions.Hosting removed in Attempt 2 -->
```

All `Microsoft.Extensions.*` packages upgraded to 10.0.3 to resolve transitive dependency conflicts with OTel 1.15.0.

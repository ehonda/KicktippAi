# Langfuse Integration — Overview

## Goal

Add LLM observability and cost analytics to KicktippAi via **Langfuse Cloud**, using the **OpenTelemetry .NET SDK** to export traces to Langfuse's OTLP endpoint.

## Why Langfuse

Langfuse is an open-source LLM engineering platform. It provides trace inspection, cost dashboards, prompt management, and evaluation tooling — all behind a single UI.

### Current State vs. With Langfuse

| Area | Current State | With Langfuse |
|------|--------------|---------------|
| **Trace inspection** | Console logs + Firebase records | Full trace UI: prompt, response, latency, nested spans |
| **Cost analytics** | Custom `CostCommand` + hardcoded `ModelPricingData` | Dashboard with cost breakdown by model, time, community |
| **Token usage** | `TokenUsageTracker` console summary | Visual per-generation usage details (cached, reasoning, text) |
| **Debugging** | Read Firebase docs or console output | Click through any trace to see exact I/O and timing |
| **Session grouping** | None | Group traces by matchday run / community |

## Integration Path

Langfuse provides native SDKs for Python and JS/TS only. For .NET, the documented approach is:

**OpenTelemetry .NET SDK → OTLP HTTP Exporter → Langfuse Cloud `/api/public/otel` endpoint**

Key constraints:
- Langfuse does **not** support gRPC — must use `OtlpExportProtocol.HttpProtobuf`
- Langfuse-specific data (model name, token usage, cost, I/O) is communicated via `langfuse.*` OTel span attributes
- Authentication uses HTTP Basic Auth: `base64(publicKey:secretKey)`

## Phases

| Phase | Scope | Document |
|-------|-------|----------|
| **1** | Observability + cost tracking via OpenTelemetry | [01-phase-1-observability.md](01-phase-1-observability.md) |
| **2** | Evaluation & experiments | [02-phase-2-evaluation.md](02-phase-2-evaluation.md) |
| **3** | Prompt management | [03-phase-3-prompt-management.md](03-phase-3-prompt-management.md) |

Current progress across all phases is tracked in [implementation-status.md](implementation-status.md).

Manual steps (account setup, credential provisioning, etc.) are documented separately in [manual-steps.md](manual-steps.md).

## Decisions

- **OTel over REST API**: OpenTelemetry is Langfuse's recommended path for non-Python/JS languages and gives us ecosystem compatibility.
- **Keep existing cost tracking**: `CostCalculationService` stays for CLI output; Langfuse provides the analytics layer on top — avoids disrupting existing workflows.
- **Langfuse Cloud**: No self-hosting infrastructure needed; just API keys in `.env`.
- **Graceful opt-in**: Langfuse is enabled only when credentials are present in `.env`, so existing CI, testing, and development workflows are unaffected.

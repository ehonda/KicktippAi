---
description: Query the Langfuse REST API to inspect traces, observations, and other observability data for debugging.
---

# Langfuse API

Use this skill to query the Langfuse REST API for debugging and inspecting LLM observability data. The project sends
OpenTelemetry traces to Langfuse (see `src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs`).

## How to Query

Run the PowerShell script at `.github/scripts/Query-LangfuseApi.ps1`:

```powershell
.github/scripts/Query-LangfuseApi.ps1 -Endpoint "<endpoint>" [-QueryParams @{key="value"}]
```

## Supported Endpoints

### List Traces

```powershell
.github/scripts/Query-LangfuseApi.ps1 -Endpoint "traces" -QueryParams @{limit=10}
```

Optional query parameters:

- `limit` — Max items to return (default: 10)
- `name` — Filter by trace name
- `tags` — Filter by tag
- `fromTimestamp` — ISO 8601 start time (e.g., `2026-02-27T00:00:00Z`)
- `toTimestamp` — ISO 8601 end time

### Get Trace by ID

```powershell
.github/scripts/Query-LangfuseApi.ps1 -Endpoint "traces/<traceId>"
```

### List Observations

```powershell
.github/scripts/Query-LangfuseApi.ps1 -Endpoint "observations" -QueryParams @{limit=10}
```

Optional query parameters:

- `limit` — Max items to return
- `traceId` — Filter by trace ID
- `name` — Filter by observation name
- `type` — Filter by type (`GENERATION`, `SPAN`, `EVENT`)

### Get Observation by ID

```powershell
.github/scripts/Query-LangfuseApi.ps1 -Endpoint "observations/<observationId>"
```

## Authentication

Credentials (`LANGFUSE_PUBLIC_KEY`, `LANGFUSE_SECRET_KEY`) are loaded from the external secrets file at
`../KicktippAi.Secrets/src/Orchestrator/.env` via `dotenvx`. The base URL is hardcoded to
`https://cloud.langfuse.com`.

## Further Reference

- [Langfuse Public API docs](https://langfuse.com/docs/api-and-data-platform/features/public-api.md)
- [API Reference](https://api.reference.langfuse.com)

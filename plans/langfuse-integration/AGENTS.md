# Langfuse Integration — Agent Context

This directory contains plans and tracking documents for the **multi-phase Langfuse integration** currently in progress. See [implementation-status.md](implementation-status.md) for the current state of each phase.

## Creating Test Traces

Use the `random-match` command to quickly produce a single-prediction trace in Langfuse without running a full matchday:

```powershell
dotnet run --project src/Orchestrator -- random-match gpt-5-nano --community ehonda-test-buli
```

## Querying the Langfuse API

Use the `langfuse-api` skill to inspect traces and observations via the Langfuse REST API (e.g., for debugging):

```powershell
# List recent traces
.github/copilot/skills/langfuse-api/scripts/Query-LangfuseApi.ps1 -Endpoint "traces" -QueryParams @{limit=10}

# Get a specific trace
.github/copilot/skills/langfuse-api/scripts/Query-LangfuseApi.ps1 -Endpoint "traces/<traceId>"
```

See the full skill definition at [.github/copilot/skills/langfuse-api/SKILL.md](../../../../.github/copilot/skills/langfuse-api/SKILL.md) for all supported endpoints and parameters.

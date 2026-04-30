# Langfuse Integration — Agent Context

This directory contains historical plans and tracking documents for the multi-phase Langfuse integration. The initial integration is complete; see [implementation-status.md](implementation-status.md) for the completion summary.

For current usage, start with the active docs instead:

- [docs/langfuse.md](../../docs/langfuse.md) for repository-specific tracing and filtering behavior
- [docs/langfuse/experiments](../../docs/langfuse/experiments) for experiment workflows
- [agent-files/langfuse-agent-tooling.md](../../agent-files/langfuse-agent-tooling.md) for global agent/CLI/MCP setup

Use [phase-2/AGENTS.md](phase-2/AGENTS.md) only when researching historical Phase 2 decisions or intentionally changing experiment behavior.

## Creating Test Traces

Use the `random-match` command to quickly produce a single-prediction trace in Langfuse without running a full matchday:

```powershell
dotnet run --project src/Orchestrator -- random-match gpt-5-nano --community ehonda-test-buli
```

## Querying the Langfuse API

Use the official global `$langfuse` skill and `langfuse-cli` to inspect traces, observations, datasets, scores, and other Langfuse resources. Prefer the external secrets file instead of copying keys into the checkout.

```powershell
# List recent traces
langfuse --env ..\KicktippAi.Secrets\src\Orchestrator\.env api traces list --limit 10 --json

# Get a specific trace
langfuse --env ..\KicktippAi.Secrets\src\Orchestrator\.env api traces get <traceId> --json
```

If the CLI is not installed globally, use `npx langfuse-cli` with the same arguments. Use `langfuse api __schema` and `langfuse api <resource> --help` to discover the current command surface.

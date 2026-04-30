# Langfuse Integration — Agent Context

This directory contains plans and tracking documents for the **multi-phase Langfuse integration** currently in progress. See [implementation-status.md](implementation-status.md) for the current state of each phase.

When working on Phase 2, start with [phase-2/AGENTS.md](phase-2/AGENTS.md). That document explains the local document structure, the handoff flow, and which task tracker to pick up next.

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

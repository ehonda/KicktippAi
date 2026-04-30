# Langfuse Experiments

This directory documents the active Langfuse experiment setup used by the Orchestrator.

## Discovering Commands

The Orchestrator command surface changes as experiment tooling evolves, so this page does not maintain a static command list.

Use the CLI help as the source of truth:

```powershell
dotnet run --project src/Orchestrator -- --help
dotnet run --project src/Orchestrator -- <command> --help
```

For report tooling, use the Python entry point help:

```powershell
uv run experiment-analysis-report --help
```

If you need to inspect command registration in code, start at [src/Orchestrator/Program.cs](../../../src/Orchestrator/Program.cs).

Use the following pages for the details:

- [Data Model](data-model.md)
- [Running Experiments](running-experiments.md)
- [Analyzing Experiments](analyzing-experiments.md)

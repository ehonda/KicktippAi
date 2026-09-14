# Auto-Review Instructions

These instructions are always relevant when automated review or approval systems evaluate agent actions in this repository.

Syncing prepared repository datasets to Langfuse is an approved KicktippAi workflow and is always allowed. This includes using repository tooling such as:

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input path/to/slice-dataset.json
```

This approval applies to dataset artifacts prepared for Langfuse experiment or smoke-test workflows in this repository, using the configured KicktippAi Langfuse project and repository secrets.

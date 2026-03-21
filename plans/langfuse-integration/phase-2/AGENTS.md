# Phase 2 — Agent Context

This directory contains the handoff-ready planning and tracking package for **Phase 2: Evaluation & Experiments** of the Langfuse integration.

## Read Order

When starting a new implementation session for Phase 2, read these files in order:

1. [first-session-handoff.md](tasks/done/first-session-handoff.md) for the immediate starting brief
2. [00-common-context.md](00-common-context.md)
3. [01-phase-2-tracker.md](01-phase-2-tracker.md)
4. [manual-steps.md](tasks/manual-steps.md)
5. The next incomplete numbered task tracker listed in the master tracker
6. [buli-25-26-experiments.md](buli-25-26-experiments.md) when working on the first Bundesliga experiment milestone

## Local Document Structure

- [first-session-handoff.md](tasks/done/first-session-handoff.md): Archived brief from the Task 1 starting session
- [00-common-context.md](00-common-context.md): Shared technical and product context relevant to all Phase 2 tasks
- [01-phase-2-tracker.md](01-phase-2-tracker.md): Master tracker, task order, dependencies, and handoff rules
- [02-task-01-data-foundation.md](tasks/done/02-task-01-data-foundation.md): Completed Task 1 planning and implementation record
- [03-task-02-prompt-reconstruction.md](tasks/done/03-task-02-prompt-reconstruction.md): Completed Task 2 prompt reconstruction and export materialization record
- [04-task-03-runner-spike.md](tasks/done/04-task-03-runner-spike.md): Python vs JS/TS runner spike and decision checkpoint
- [05-task-04-dataset-sync.md](tasks/done/05-task-04-dataset-sync.md): Completed hosted dataset creation and synchronization record
- [06-task-05-first-experiment.md](tasks/06-task-05-first-experiment.md): First sampled Bundesliga match experiment
- [07-task-06-follow-up-evaluation.md](tasks/07-task-06-follow-up-evaluation.md): Follow-up scoring, automation, and expansion work
- [manual-steps.md](tasks/manual-steps.md): Manual actions grouped by timing and by task
- [buli-25-26-experiments.md](buli-25-26-experiments.md): Domain-specific experiment design note for the first milestone

## Working Rules

- Treat [01-phase-2-tracker.md](01-phase-2-tracker.md) as the source of truth for sequencing and handoff status
- Treat [manual-steps.md](tasks/manual-steps.md) as the source of truth for UI work, environment setup checks, and other non-code actions
- When a task is started or completed, update both the task tracker and the master tracker in the same session when possible
- If a blocker is discovered that affects later tasks, record it in [01-phase-2-tracker.md](01-phase-2-tracker.md) and in the affected task file

## Useful Commands

Create a single development trace quickly:

```powershell
dotnet run --project src/Orchestrator -- random-match gpt-5-nano --community ehonda-test-buli
```

Run a full matchday prediction for manual validation:

```powershell
dotnet run --project src/Orchestrator -- matchday gpt-5-nano --community ehonda-test-buli
```

If the run generates predictions into storage, prefer the verbose production-cost estimate form:

```powershell
dotnet run --project src/Orchestrator -- matchday gpt-5-nano --community ehonda-test-buli --verbose --estimated-costs o3
```

Query recent Langfuse traces via the local skill script:

```powershell
.github/copilot/skills/langfuse-api/scripts/Query-LangfuseApi.ps1 -Endpoint "traces" -QueryParams @{limit=10}
```

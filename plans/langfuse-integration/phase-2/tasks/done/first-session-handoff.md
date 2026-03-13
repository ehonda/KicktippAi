# Phase 2 — First Session Handoff

This note is the shortest path for the next implementation session.

## Session Goal

Start **Task 1: Data Foundation** and leave the repository with the first implementation decisions recorded clearly enough that the next task can begin without reopening the same design questions.

The first session does not need to finish all of Phase 2. It should establish the data foundation that the rest of the phase depends on.

## Start Here

Read these files in order:

1. [00-common-context.md](../../00-common-context.md)
2. [01-phase-2-tracker.md](../../01-phase-2-tracker.md)
3. [manual-steps.md](../manual-steps.md)
4. [02-task-01-data-foundation.md](02-task-01-data-foundation.md)
5. [buli-25-26-experiments.md](../../buli-25-26-experiments.md)

## Primary Objective For The Session

Answer these questions with enough precision that implementation can proceed:

1. What is the authoritative source for actual match outcomes that should populate dataset `expectedOutput`?
2. What exact fields belong in the first milestone's dataset `input`, `expectedOutput`, and `metadata`?
3. Where should the .NET export or materialization seam live in the codebase?

## Expected Deliverables

By the end of the first session, the following should be true:

- [02-task-01-data-foundation.md](02-task-01-data-foundation.md) has concrete findings, not only open questions
- [01-phase-2-tracker.md](../../01-phase-2-tracker.md) reflects whether Task 1 is in progress or complete
- Any newly discovered blockers are written down explicitly
- The first dataset contract is specific enough that Task 2 can build prompt reconstruction against it

## What Is Already Decided

- Phase 1 tracing is complete and should be treated as stable
- The first milestone targets **Bundesliga 2025/2026 match experiments**
- Use a **hosted Langfuse dataset** for the first real experiment flow
- Prefer **Python first** for the Langfuse experiment runner, but keep JS/TS as the fallback
- Keep the **data preparation and prompt reconstruction logic in .NET**

## What Is Not Yet Decided

- Whether Firebase already contains authoritative actual outcomes in the required shape
- The exact first-pass dataset schema and metadata shape
- Whether the export seam should be service-only, command-only, or both
- The exact historical scope for the first hosted dataset upload

## Manual Steps To Perform In This Session

From [manual-steps.md](../manual-steps.md), the session should explicitly cover these items:

1. Review Langfuse pricing and limits if that has not yet been checked for Phase 2 volume expectations
2. Decide the initial dataset scope for the first milestone
3. Manually confirm actual-result provenance
4. Manually inspect representative historical prediction and context records

Record outcomes back into the task file.

## Suggested Technical Starting Points

Focus on these repository areas first:

- [src/Core/IPredictionRepository.cs](src/Core/IPredictionRepository.cs)
- [src/FirebaseAdapter/FirebasePredictionRepository.cs](src/FirebaseAdapter/FirebasePredictionRepository.cs)
- [src/FirebaseAdapter/FirebaseContextRepository.cs](src/FirebaseAdapter/FirebaseContextRepository.cs)
- [src/OpenAiIntegration/IPredictionService.cs](src/OpenAiIntegration/IPredictionService.cs)
- [src/OpenAiIntegration/PredictionService.cs](src/OpenAiIntegration/PredictionService.cs)

These planning files are the source of truth while doing that work:

- [02-task-01-data-foundation.md](02-task-01-data-foundation.md)
- [00-common-context.md](../../00-common-context.md)
- [buli-25-26-experiments.md](../../buli-25-26-experiments.md)

## Useful Commands

Create a small development trace if needed for context validation:

```powershell
dotnet run --project src/Orchestrator -- random-match gpt-5-nano --community ehonda-test-buli
```

Inspect a live manual validation run if needed:

```powershell
dotnet run --project src/Orchestrator -- matchday gpt-5-nano --community ehonda-test-buli
```

Inspect recent Langfuse traces:

```powershell
.github/copilot/skills/langfuse-api/scripts/Query-LangfuseApi.ps1 -Endpoint "traces" -QueryParams @{limit=10}
```

## Session Boundary Guidance

If the first session cannot finish Task 1 fully, it should still leave behind:

- the current hypothesis for actual-result provenance
- the most likely export seam location
- the unresolved questions that still block Task 1 completion
- any manual findings that later sessions should not have to rediscover

Do not start Task 2 until Task 1 has frozen the data contract well enough that prompt reconstruction can be implemented against it.

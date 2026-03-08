# Task 1 — Data Foundation

## Status

Ready

## Objective

Confirm the authoritative data sources for the first experiment pipeline, freeze the dataset contract, and define the .NET export/materialization seam that later tasks will build on.

## Why This Comes First

Every later task depends on stable answers to three questions:

- Where does `expectedOutput` come from?
- What exact fields belong in `input`, `expectedOutput`, and `metadata`?
- Where in the .NET codebase should experiment-item materialization live?

## Required Outputs

- A confirmed source for actual match outcomes
- A written experiment item contract for the first milestone
- A concrete implementation target for export/materialization in the repo
- A list of any repository interface changes that are required before coding starts

## Planned Work

1. Confirm whether actual outcomes live in Firebase, another persisted store, or must be enriched from Kicktipp history
2. Freeze the hosted dataset item contract for the first match experiment milestone
3. Decide whether to add a new Orchestrator export command, a dedicated service, or both
4. Identify any repository gaps between current read APIs and what export/materialization needs

## Inputs

- [00-common-context.md](00-common-context.md)
- [buli-25-26-experiments.md](buli-25-26-experiments.md)
- [src/Core/IPredictionRepository.cs](src/Core/IPredictionRepository.cs)
- [src/FirebaseAdapter/FirebasePredictionRepository.cs](src/FirebaseAdapter/FirebasePredictionRepository.cs)
- [src/FirebaseAdapter/FirebaseContextRepository.cs](src/FirebaseAdapter/FirebaseContextRepository.cs)

## Manual Steps

Use [manual-steps.md](manual-steps.md#task-1--data-foundation) during implementation.

## Open Questions To Resolve In This Task

- Is Firebase the authoritative source for actual outcomes, or only for predictions and context usage?
- Should the initial export seam be a reusable service only, or also a CLI command for manual validation?
- How much historical scope should be included in the first hosted dataset upload?

## Completion Criteria

- The expected-output source is explicitly confirmed
- The first-match experiment item schema is frozen in writing
- The planned export seam is specific enough that Task 2 can implement against it without reopening the design

## Handoff Notes

When this task is complete, update:

- [01-phase-2-tracker.md](01-phase-2-tracker.md)
- [03-task-02-prompt-reconstruction.md](03-task-02-prompt-reconstruction.md) if the export seam affects prompt reconstruction design
- [manual-steps.md](manual-steps.md) if additional non-code checks become necessary

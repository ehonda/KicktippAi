# Task 1 — Data Foundation

## Status

Completed

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

1. Freeze Kicktipp `tippuebersicht` as the external source for actual outcomes and persist them into Firebase as the internal source used by dataset export
2. Freeze the hosted dataset item contract for the first match experiment milestone
3. Implement the reusable service seam that collects outcomes and integrates into `collect-context`
4. Identify any repository gaps between current read APIs and what export/materialization needs

## Inputs

- [00-common-context.md](../../00-common-context.md)
- [buli-25-26-experiments.md](../../buli-25-26-experiments.md)
- [src/Core/IPredictionRepository.cs](src/Core/IPredictionRepository.cs)
- [src/FirebaseAdapter/FirebasePredictionRepository.cs](src/FirebaseAdapter/FirebasePredictionRepository.cs)
- [src/FirebaseAdapter/FirebaseContextRepository.cs](src/FirebaseAdapter/FirebaseContextRepository.cs)

## Manual Steps

Use [manual-steps.md](../manual-steps.md#task-1--data-foundation) during implementation.

## Questions Resolved In This Task

- `tippspielId` is persisted and used by the current Firebase outcome storage path; the provided cross-community samples did not show a collision between `pes-squad` and `ehonda-test-buli`
- The persisted outcome contract currently uses a normalized `Pending` or `Completed` availability state, which is sufficient for postponed-match refresh handling in the collection flow
- A dedicated export-oriented prediction read model can wait for later dataset-export work; Task 2 should consume the frozen outcome contract and focus on prompt reconstruction

## Decisions Locked In This Task

- Dataset scope is the Kicktipp `community`
- The first rollout is limited to `pes-squad`
- The initial hosted dataset slice is all completed Bundesliga 2025/2026 matches available in `pes-squad`
- Actual match outcomes come from Kicktipp `tippuebersicht` pages navigated by `spieltagIndex`
- Firebase becomes the internal authoritative store for experiment `expectedOutput` after those outcomes are collected
- Only matches with persisted outcomes are eligible dataset items
- The initial seam is a reusable .NET service integrated into `collect-context`
- Automatic Langfuse dataset updates are explicitly deferred

## Experiment Item Contract

For the first match experiment milestone:

- `input` is the match-to-predict payload used for `predict-match`
- `expectedOutput` is sourced from the persisted Firebase match-outcome record and must include at least home goals, away goals, and a normalized completion status so Kicktipp scoring can be computed deterministically
- `metadata` includes `community`, `competition`, `matchday`, `homeTeam`, `awayTeam`, prediction timestamp, context document names, resolved context versions, and optional historical baseline fields
- The system prompt is not stored directly in the dataset item and remains a reconstruction concern for Task 2

## Outcome Collection Shape

- `collect-context kicktipp` should first determine the current matchday from `tippuebersicht` without query parameters
- It should then ask Firebase which matchdays from `1..currentMatchday` are not fully persisted yet
- On the first run this can be the full range; on later runs this should usually shrink to current and any postponed matchdays that still contain incomplete outcomes
- For each incomplete matchday, the collector should re-fetch `tippuebersicht?spieltagIndex=<n>` and upsert match outcome rows
- Postponed matches are handled by re-running incomplete matchdays until their scores appear on the original matchday page
- `tippspielId` is persisted on the outcome record and is the current document-identity basis for the Firebase storage path

## Repository Interface Changes Required Before Export Work

- Add a match-outcome repository interface to the core layer
- Add a Firebase match-outcome repository and Firestore model for persisted outcome rows
- Add a Kicktipp client seam for reading `tippuebersicht` matchday outcomes and the current displayed matchday
- Add a reusable collection service that combines the Kicktipp client and the outcome repository for `collect-context`
- Document that the current prediction repository surface is not yet ideal for dataset export because export needs prediction values plus stable metadata without per-match ad hoc lookups

## Completion Criteria

- The expected-output source is explicitly confirmed
- The first-match experiment item schema is frozen in writing
- The planned export seam is specific enough that Task 2 can implement against it without reopening the design

## Handoff Notes

When this task is complete, update:

- [01-phase-2-tracker.md](../../01-phase-2-tracker.md)
- [03-task-02-prompt-reconstruction.md](03-task-02-prompt-reconstruction.md) if the export seam affects prompt reconstruction design
- [manual-steps.md](../manual-steps.md) if additional non-code checks become necessary

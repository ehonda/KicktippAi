# Phase 2 — Master Tracker

This is the master tracker for Phase 2. It defines the task order, dependencies, handoff expectations, and the current state of implementation planning.

## Goal

Set up a reproducible Langfuse experiment workflow for KicktippAi that can:

- materialize historical match data into a reusable experiment dataset
- reconstruct prompts deterministically
- run sampled experiments against hosted Langfuse datasets
- score results in Kicktipp-relevant terms
- support future expansion into richer evaluations and automation

## Task Sequence

| Order | Task | Purpose | Depends On | Status |
|------|------|---------|------------|--------|
| 1 | [02-task-01-data-foundation.md](02-task-01-data-foundation.md) | Confirm data sources, freeze dataset contract, design export seam | None | Ready |
| 2 | [03-task-02-prompt-reconstruction.md](03-task-02-prompt-reconstruction.md) | Resolve exact context versions and reproducible prompt reconstruction | Task 1 | Blocked by Task 1 |
| 3 | [04-task-03-runner-spike.md](04-task-03-runner-spike.md) | Choose Python or JS/TS experiment runner with a narrow spike | Tasks 1-2 | Blocked by Tasks 1-2 |
| 4 | [05-task-04-dataset-sync.md](05-task-04-dataset-sync.md) | Create and synchronize the hosted Langfuse dataset | Tasks 1-3 | Blocked by Tasks 1-3 |
| 5 | [06-task-05-first-experiment.md](06-task-05-first-experiment.md) | Run the first sampled Bundesliga experiment with scoring | Tasks 1-4 | Blocked by Tasks 1-4 |
| 6 | [07-task-06-follow-up-evaluation.md](07-task-06-follow-up-evaluation.md) | Add follow-up metrics, automation, and next experiment layers | Task 5 | Blocked by Task 5 |

## Current Status

- Planning scaffold created
- Shared context consolidated into [00-common-context.md](00-common-context.md)
- Manual actions consolidated into [manual-steps.md](manual-steps.md)
- Domain-specific first-milestone design kept in [buli-25-26-experiments.md](buli-25-26-experiments.md)
- Task 1 data-foundation decisions are being locked and implemented

## Cross-Task Risks

### 1. Outcome Persistence Must Be Added

The current Firestore prediction and context models clearly support prediction export and prompt reconstruction inputs. Actual match outcomes should be scraped from Kicktipp `tippuebersicht` pages by matchday and persisted into a new Firebase collection before dataset export can rely on them.

Task 1 must add the repository and collection seam for this.

### 2. Runner Language Is Not Locked

Python is the preferred starting point for Langfuse experiments, but JS/TS is intentionally still available as fallback.

This must be resolved in Task 3.

### 3. Hosted Dataset Requirement Changes The Flow

The first experiment should target a hosted Langfuse dataset, not only a local list of items, because dataset runs and comparison views are part of the intended outcome.

This affects dataset sync design in Task 4.

## Handoff Rules

- Before starting a task, update its `Status` section from `Ready` or `Blocked` to `In progress`
- When a task completes, update this master tracker and the task file in the same session if possible
- Any blocker discovered during implementation must be recorded here and in the affected task file
- If a manual step is required, record evidence or completion notes in [manual-steps.md](manual-steps.md)

## What A New Session Should Do First

1. Read [AGENTS.md](AGENTS.md)
2. Read [first-session-handoff.md](first-session-handoff.md)
3. Read [00-common-context.md](00-common-context.md)
4. Read [manual-steps.md](manual-steps.md)
5. Start with [02-task-01-data-foundation.md](02-task-01-data-foundation.md)

## Completion Criteria For The First Milestone

The first milestone is complete when all of the following are true:

- A hosted Langfuse dataset exists for Bundesliga 2025/2026 match experiments
- Dataset items have stable IDs, validated structure, and reproducible reconstruction metadata
- A first sampled experiment run can be executed and appears as a dataset run in Langfuse
- Trace linkage and scoring are visible in Langfuse
- Kicktipp points are implemented as the primary experiment metric
- Supporting metrics are available to diagnose experiment behavior

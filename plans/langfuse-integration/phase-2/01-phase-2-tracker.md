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
| 1 | [02-task-01-data-foundation.md](tasks/done/02-task-01-data-foundation.md) | Confirm data sources, freeze dataset contract, design export seam | None | Completed |
| 2 | [03-task-02-prompt-reconstruction.md](tasks/done/03-task-02-prompt-reconstruction.md) | Resolve exact context versions and reproducible prompt reconstruction | Task 1 | Completed |
| 3 | [04-task-03-runner-spike.md](tasks/done/04-task-03-runner-spike.md) | Choose Python or JS/TS experiment runner with a narrow spike | Task 2 | Completed |
| 4 | [05-task-04-dataset-sync.md](tasks/05-task-04-dataset-sync.md) | Create and synchronize the hosted Langfuse dataset | Task 3 | In progress |
| 5 | [06-task-05-first-experiment.md](tasks/06-task-05-first-experiment.md) | Run the first sampled Bundesliga experiment with scoring | Tasks 3-4 | Blocked by Task 4 |
| 6 | [07-task-06-follow-up-evaluation.md](tasks/07-task-06-follow-up-evaluation.md) | Add follow-up metrics, automation, and next experiment layers | Task 5 | Blocked by Task 5 |

## Task Checklist

- [x] [Task 1 — Data Foundation](tasks/done/02-task-01-data-foundation.md)
- [x] [Task 2 — Prompt Reconstruction](tasks/done/03-task-02-prompt-reconstruction.md)
- [x] [Task 3 — Runner Spike](tasks/done/04-task-03-runner-spike.md)
- [ ] [Task 4 — Hosted Dataset Sync](tasks/05-task-04-dataset-sync.md)
- [ ] [Task 5 — First Experiment](tasks/06-task-05-first-experiment.md)
- [ ] [Task 6 — Follow-up Evaluation](tasks/07-task-06-follow-up-evaluation.md)

## Current Status

- Planning scaffold created
- Shared context consolidated into [00-common-context.md](00-common-context.md)
- Manual actions consolidated into [manual-steps.md](tasks/manual-steps.md)
- Domain-specific first-milestone design kept in [buli-25-26-experiments.md](buli-25-26-experiments.md)
- Task 1 data-foundation work is complete and archived in [02-task-01-data-foundation.md](tasks/done/02-task-01-data-foundation.md)
- Task 2 prompt reconstruction is complete and archived in [03-task-02-prompt-reconstruction.md](tasks/done/03-task-02-prompt-reconstruction.md)
- Task 3 runner spike is complete
- JS/TS is now the selected first-milestone runner because local Python on this machine is limited to `3.6` and would require machine-level upgrade work before repo implementation could proceed
- Task 4 hosted dataset sync is now in progress against the match-centric dataset contract and timestamp-based reconstruction rule documented in [dataset-contract-and-reconstruction-spec.md](dataset-contract-and-reconstruction-spec.md)

## Cross-Task Risks

### 1. Dataset Export Must Use The Persisted Outcome Store

The Phase 2 outcome collection seam is now in place: actual match outcomes are collected from Kicktipp `tippuebersicht` pages and persisted into Firebase.

Later tasks must build on that persisted store rather than reintroducing live scraping into dataset export or experiment execution flows.

### 2. Runner Environment Is Now JS/TS For The First Milestone

Task 3 selected JS/TS for the first milestone and verified a minimal local experiment trace against Langfuse.

Later tasks should target the JS/TS runner unless the local Python environment is modernized intentionally in a future session.

### 3. Hosted Dataset Requirement Changes The Flow

The first experiment should target a hosted Langfuse dataset, not only a local list of items, because dataset runs and comparison views are part of the intended outcome.

This affects dataset sync design in Task 4.

## Handoff Rules

- Before starting a task, update its `Status` section from `Ready` or `Blocked` to `In progress`
- When a task completes, update this master tracker and the task file in the same session if possible
- Any blocker discovered during implementation must be recorded here and in the affected task file
- If a manual step is required, record evidence or completion notes in [manual-steps.md](tasks/manual-steps.md)

## What A New Session Should Do First

1. Read [AGENTS.md](AGENTS.md)
2. Read [first-session-handoff.md](tasks/done/first-session-handoff.md)
3. Read [00-common-context.md](00-common-context.md)
4. Read [manual-steps.md](tasks/manual-steps.md)
5. Start with the next incomplete task from the checklist, currently [05-task-04-dataset-sync.md](tasks/05-task-04-dataset-sync.md)

## Completion Criteria For The First Milestone

The first milestone is complete when all of the following are true:

- A hosted Langfuse dataset exists for Bundesliga 2025/2026 match experiments
- Dataset items have stable IDs, validated structure, and reproducible reconstruction metadata
- A first sampled experiment run can be executed and appears as a dataset run in Langfuse
- Trace linkage and scoring are visible in Langfuse
- Kicktipp points are implemented as the primary experiment metric
- Supporting metrics are available to diagnose experiment behavior

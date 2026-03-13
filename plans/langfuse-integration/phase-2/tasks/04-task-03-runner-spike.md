# Task 3 — Runner Spike

## Status

Blocked by Task 2

## Objective

Run a narrow implementation spike to choose the experiment-runner language for the first milestone: Python first, JS/TS fallback.

## Why This Comes Third

The runner decision should be made after the data contract and prompt reconstruction are stable, but before hosted dataset synchronization and experiment execution are implemented.

## Required Outputs

- A language decision for the first milestone
- A written reason for the decision
- A minimal working proof that the chosen runner can consume exported items and deliver traces to Langfuse

## Planned Work

1. Prepare one or a few exported experiment items from the .NET side
2. Test Python against the exported data and Langfuse setup
3. If Python setup is disproportionately painful, test a JS/TS fallback path
4. Record the decision and the reasons

## Inputs

- Results of [02-task-01-data-foundation.md](done/02-task-01-data-foundation.md)
- Results of [03-task-02-prompt-reconstruction.md](03-task-02-prompt-reconstruction.md)
- [src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs](src/Orchestrator/Infrastructure/ServiceRegistrationExtensions.cs)

## Manual Steps

Use [manual-steps.md](manual-steps.md#task-3--runner-spike) during implementation.

## Decision Criteria

- Ease of local setup in this repo
- Ease of consuming exported experiment items
- Ease of running Langfuse experiments and attaching scores
- Reliability of trace delivery and flushing
- Overall complexity cost for the first milestone

## Completion Criteria

- Python or JS/TS is selected explicitly
- The selection is backed by a successful minimal spike
- Later tasks can assume a concrete runner environment

## Handoff Notes

Once complete, update [05-task-04-dataset-sync.md](05-task-04-dataset-sync.md) and [06-task-05-first-experiment.md](06-task-05-first-experiment.md) so they target the chosen runner stack explicitly.

# Task 2 — Prompt Reconstruction

## Status

Blocked by Task 1

## Objective

Implement and validate reproducible prompt reconstruction for historical match predictions so experiment inputs can be recreated consistently even if prompt-building evolves later.

## Why This Comes Second

The experiment dataset is only credible if prompt reconstruction is deterministic. This task translates historical context references and timestamps into concrete versioned prompt inputs.

## Required Outputs

- A written reconstruction rule based on prediction time and versioned context documents
- A concrete representation of resolved context versions in experiment metadata
- Validation notes proving that reconstructed prompts are reproducible for sample records

## Planned Work

1. Resolve the reconstruction algorithm in implementation terms
2. Materialize resolved context document versions for exported items
3. Decide how reconstructed prompt inputs are represented for downstream experiment runners
4. Validate the approach against sample historical predictions

## Inputs

- Results of [02-task-01-data-foundation.md](02-task-01-data-foundation.md)
- [src/FirebaseAdapter/FirebaseContextRepository.cs](src/FirebaseAdapter/FirebaseContextRepository.cs)
- [src/OpenAiIntegration/PredictionService.cs](src/OpenAiIntegration/PredictionService.cs)

## Manual Steps

Use [manual-steps.md](manual-steps.md#task-2--prompt-reconstruction) during implementation.

## Reconstruction Rule To Implement

For each referenced context document:

- identify the original prediction creation timestamp
- select the latest context document version whose creation time is less than or equal to that timestamp
- record the resolved version in experiment metadata

## Open Questions To Resolve In This Task

- Do any context documents need special-case handling beyond timestamp-based version resolution?
- What metadata shape is easiest for both Python and JS/TS runners to consume?

## Completion Criteria

- Reconstruction works for representative historical records
- Resolved versions are recorded in a stable metadata format
- Task 3 can use the exported material without re-opening reconstruction design

## Handoff Notes

If reconstruction changes what the runner needs to consume, update [04-task-03-runner-spike.md](04-task-03-runner-spike.md) before handing off.

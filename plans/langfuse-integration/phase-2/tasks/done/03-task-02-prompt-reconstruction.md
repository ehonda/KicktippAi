# Task 2 — Prompt Reconstruction

## Status

Done

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

Task 2 should assume that actual outcomes already come from the persisted Firebase match-outcome store created in Task 1. Prompt reconstruction should not scrape live Kicktipp data during dataset export.

## Manual Steps

Use [manual-steps.md](../manual-steps.md#task-2--prompt-reconstruction) during implementation.

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

## Outcome

- Added historical context lookup by prediction timestamp so reconstruction resolves the latest context version created at or before the stored prediction time.
- Extracted shared prompt composition logic from the production match prediction path and reused it for reconstruction to avoid format drift.
- Added a permanent `reconstruct-prompt` Orchestrator command for manual validation and inspection of reconstructed match JSON, system prompt text, and resolved context versions.
- Recorded resolved context document versions in the reconstruction result as stable metadata suitable for downstream experiment export.
- Tightened Firebase retrieval determinism for context version lookup, stored-match fallback, and bonus prediction lookup so historical reconstruction does not depend on arbitrary Firestore result ordering.

## Validation Notes

- Focused automated coverage passed for `OpenAiIntegration.Tests`, `FirebaseAdapter.Tests`, and `Orchestrator.Tests` during implementation.
- Live validation was performed against Langfuse observation `a2565004e679b25e` (`predict-match`, model `o4-mini`, community context `pes-squad`, matchday `26`, `VfB Stuttgart` vs `RB Leipzig`).
- Additional historical validation was performed against Langfuse observation `4cf2575fc8ad5d4e` (`predict-match`, model `o3`, community and community context `pes-squad`, matchday `25`, `VfL Wolfsburg` vs `Hamburger SV`).
- The reconstructed system prompt content matched the Langfuse observations after resolving historical context versions from stored prediction metadata.
- The reconstructed match payload matched the Langfuse user input exactly as `{"homeTeam":"VfB Stuttgart","awayTeam":"RB Leipzig","startsAt":"2026-03-15T19:30:00 UTC+01 (+01)"}` for the matchday 26 sample.
- The reconstructed match payload matched the Langfuse user input exactly as `{"homeTeam":"VfL Wolfsburg","awayTeam":"Hamburger SV","startsAt":"2026-03-07T15:30:00 UTC+01 (+01)"}` for the matchday 25 sample.

## Handoff Notes

If reconstruction changes what the runner needs to consume, update [04-task-03-runner-spike.md](../04-task-03-runner-spike.md) before handing off.

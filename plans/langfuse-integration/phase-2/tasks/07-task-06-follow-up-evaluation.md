# Task 6 — Follow-up Evaluation

## Status

Blocked by Task 5

## Objective

Build on the first successful experiment run with follow-up work: richer metrics, better automation, and the next experiment layers.

## Why This Comes Last

This task intentionally stays broad until the first experiment run proves the base workflow in production-like conditions.

## Candidate Follow-up Work

- Add CI-friendly experiment checks or smoke tests
- Add more aggregate metrics and comparison helpers
- Expand beyond random samples to other slices and cohorts
- Add stable benchmark slice datasets for recurring benchmark runs
- Add historical baseline comparisons against stored predictions
- Revisit justification-quality evaluation and LLM-as-a-Judge
- Extend the pattern to bonus prediction experiments later

Do not treat repetition-family aggregation as part of the initial Task 5 deliverable. If later work needs repeated fixed-match or fixed-slice experiments with native Langfuse averages, use the repetition-expanded shadow-dataset design documented in [../first-experiment-run-design.md](../first-experiment-run-design.md).

## Manual Steps

Use [manual-steps.md](manual-steps.md#task-6--follow-up-evaluation) during implementation.

## Completion Criteria

- The next set of Phase 2 priorities is explicit
- Follow-up work is broken into specific implementation items instead of remaining as loose ideas

## Handoff Notes

If this task expands significantly, split it into additional numbered task trackers rather than overloading this file.

## Early Follow-up Inputs From The First Successful Sampled Run

- Add a small helper to persist sampled slice manifests so recurring comparisons can reuse the same selected item list without reseeding from the mutable canonical dataset
- Consider standardizing Langfuse score configs for `kicktipp_points` and the supporting metrics so dashboard and compare views stay schema-consistent
- Add a smoke-level wrapper option that executes a tiny sampled slice and only runs the autonomous Langfuse API verification path for faster regression checks
- Decide whether the current autonomous verification should also query dataset-run specific endpoints, or whether traces plus observations are sufficient until UI evidence is recorded

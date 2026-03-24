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

# Phase 2 — Manual Steps

This document lists every manual action that should be taken during Phase 2 implementation, grouped by timing and by task.

Use this file during implementation, not just for planning.

## Execution Timeline

| When | Task | Manual Step | Blocking |
|------|------|-------------|----------|
| Before Task 1 | Cross-cutting | Review Langfuse pricing and plan limits for hosted datasets, traces, and scores | Yes |
| Before Task 1 | Cross-cutting | Decide the initial historical scope for the first hosted dataset | Yes |
| During Task 1 | Task 1 | Manually validate `tippuebersicht` parsing and postponed-match update behavior | Yes |
| During Task 3 | Task 3 | Verify local Python setup is acceptable, or explicitly fall back to JS/TS | Yes |
| During Task 3 | Task 3 | Verify a minimal spike trace is visible in Langfuse | Yes |
| During Task 4 | Task 4 | Inspect the hosted dataset in the Langfuse UI after first sync | Yes |
| During Task 5 | Task 5 | Inspect dataset run, linked traces, and scores in Langfuse | Yes |
| During Task 5 | Task 5 | Manually spot-check Kicktipp scoring against known examples | Yes |
| After Task 5 | Task 6 | Decide whether to add LLM-as-a-Judge or keep it deferred | No |

## Cross-Cutting Manual Steps

### Before Task 1

1. Review Langfuse pricing and limits

- Check current Langfuse Cloud pricing for hosted datasets, traces, and score volume
- Confirm the current plan is sufficient for the expected dataset size and experiment frequency
- Record any limit concerns in [01-phase-2-tracker.md](../01-phase-2-tracker.md)

2. Decide the first dataset scope

- Choose the first historical slice to support
- For the first rollout, use all completed Bundesliga 2025/2026 matches available in `pes-squad`
- Record the decision in [02-task-01-data-foundation.md](done/02-task-01-data-foundation.md)

## Task 1 — Data Foundation

1. Manually confirm actual-result provenance

- Verify that `tippuebersicht` exposes the expected match rows, scores, and matchday navigation for outcome collection
- Verify that Firebase persists incomplete matches and that a later rerun can fill postponed outcomes when they appear on the original matchday page
- Record the answer and any caveats in [02-task-01-data-foundation.md](done/02-task-01-data-foundation.md)

2. Manually inspect representative historical records

- Inspect a few stored predictions and related context-document history records
- Confirm the stored timestamps and context-document names are sufficient for later reconstruction
- Record any anomalies before Task 2 starts

## Task 2 — Prompt Reconstruction

1. Manually validate reconstruction on sample records

- Pick a small set of historical predictions from different matchdays
- Confirm that the resolved context versions make sense chronologically
- Record the observation or trace IDs used for comparison and whether the reconstructed match payload and system prompt matched
- If a reconstructed prompt looks suspicious, record the edge case before moving on

2. Manually review the reconstructed metadata shape

- Confirm the metadata is understandable enough for later debugging in the runner and in Langfuse
- Prefer a shape that future sessions can inspect without reverse-engineering internal DTOs

## Task 3 — Runner Spike

1. Verify local Python readiness first

- Confirm whether the local Python environment is usable without excessive setup churn
- If it is not, explicitly record the reason for falling back to JS/TS

2. Run a minimal spike and inspect Langfuse

- Execute a one-item or tiny-sample spike
- Confirm traces arrive in Langfuse
- Confirm flushing or shutdown behavior is reliable enough for the chosen runner

3. Record the runner decision

- Update [04-task-03-runner-spike.md](done/04-task-03-runner-spike.md) with the chosen language and the reason
- Update downstream task docs so new sessions do not need to repeat the decision

## Task 4 — Hosted Dataset Sync

1. Inspect the hosted dataset in the Langfuse UI

- Use the Langfuse API skill first for autonomous confirmation of dataset existence, schemas, and representative items before switching to the UI
- Confirm the dataset exists with the expected name
- Confirm items are visible and structurally correct
- Confirm metadata fields are useful for filtering and debugging

2. Validate re-sync behavior manually

- Re-run the sync on a small sample or the same scope
- Compare the manual observation with the autonomous sync summary so the UI check confirms the same idempotent outcome
- Confirm that items are updated idempotently rather than duplicated unexpectedly

## Task 5 — First Experiment

1. Inspect the dataset run in Langfuse

- Confirm the experiment appears as a dataset run
- Confirm linked traces exist for the sampled items
- Confirm item-level scores are attached where expected
- Confirm run-level aggregates are visible

2. Spot-check Kicktipp scoring manually

- Take a few sample predictions and compare the computed scores against manual expectations
- Record any disagreement before treating the scoring implementation as stable

3. Validate experiment metadata

- Confirm the run metadata includes enough information to distinguish model, prompt variant, sample size, community context, and timestamp

## Task 6 — Follow-up Evaluation

1. Decide whether to pursue LLM-as-a-Judge next

- Reassess only after the numeric scoring flow is stable
- If adopted, record why it is needed and what quality dimension it covers

2. Decide which automation to add first

- CI smoke check
- richer aggregates
- baseline comparisons
- broader experiment slices

## Evidence To Record During Implementation

When a manual step is completed, record lightweight evidence in the corresponding task file. Examples:

- the confirmed source of actual results
- the selected runner language and why
- the final hosted dataset name
- the first successful dataset run name or timestamp
- any Langfuse UI observations that affect later work

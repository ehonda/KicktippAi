# Further Improvement — Incremental Dataset Sync

## Status

Deferred

## Context

Task 4 intentionally implemented full-scope idempotent reruns for the hosted dataset instead of a stateful incremental sync. That choice kept the first milestone simple and safe because the hosted dataset item IDs are already stable and Langfuse upserts dataset items by ID.

## Why This Is Deferred

- The current full-scope sync is already fast enough for the first milestone and is easy to reason about.
- The first milestone needed a trustworthy canonical dataset before optimizing API-call volume.
- Incremental sync adds complexity around change detection, replay safety, and operational visibility that is not required for the first experiment run.

## Current Baseline

- Dataset name: `match-predictions/bundesliga-2025-26/pes-squad`
- Item IDs: `bundesliga-2025-26__pes-squad__ts{TippSpielId}`
- Current sync behavior: full export from .NET followed by JS item-level idempotent sync that skips unchanged items after fetching them by stable ID

## Candidate Improvements

1. Detect changed or newly eligible matches in the export layer before invoking the Langfuse sync.
2. Add a sync mode that reads only a targeted set of item IDs or matchdays.
3. Record and surface clearer operational metrics about why an item was created, updated, or skipped.
4. Decide whether archived or removed matches need any hosted-dataset cleanup path.

## Guardrails

- Do not replace the stable item ID scheme.
- Keep the hosted dataset metadata match-centric only.
- Preserve the Langfuse schemas introduced in Task 4.
- Treat any optimization as optional unless the full-scope rerun becomes operationally expensive.

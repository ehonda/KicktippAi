# Read Reduction Plan

This directory tracks the remaining work to reduce Firestore reads after the initial repository query fixes and the cost-analysis workflow unscheduling.

See [01-langfuse-experiment-runs.md](01-langfuse-experiment-runs.md) for the next likely high-impact area.

## Analysis

- The scheduled baseline was inflated by the manual-observability path being left on a timer. The cost-analysis workflow scanned prediction collections repeatedly across overlapping matrix configurations.
- The `match-outcomes` repository previously fetched an entire community's outcome history and filtered in memory for `GetMatchdayOutcomesAsync` and `GetIncompleteMatchdaysAsync`.
- Historical context reconstruction previously resolved a document-at-timestamp by loading every version, then filtering in memory.
- Remaining scheduled prediction reads are still amplified by the verify workflow pattern: initial verify, optional generate, final verify.
- The `ehonda-ai-arena` workflow family fans out across multiple models against the same `pes-squad` context, which is deliberate for comparison but still multiplies verification reads.

## Estimated Impact

- Unscheduling `cost-analysis.yml` should remove a predictable twice-daily read spike with no product-facing impact.
- The `GetMatchdayOutcomesAsync` repository fix should materially reduce reads for dataset export and experiment item export because those paths only need one matchday at a time.
- The `GetContextDocumentByTimestampAsync` repository fix should materially reduce experiment reconstruction reads, especially once context version history continues to grow.
- The `GetIncompleteMatchdaysAsync` repository fix is a moderate improvement. It narrows the queried scope, but it still needs to inspect all persisted outcomes up to the current matchday.

## Suggestions On How To Fix

- Split scheduled verification into two modes:
  - a cheap existence and mismatch pass for routine runs
  - an outdated-context pass only when a cheap pass indicates a candidate reprediction or when explicitly requested
- Rework the cost command so it reads each underlying collection once per run and derives all aggregates from that in-memory result set.
- Replace the `ehonda-ai-arena` o3 copy workflow path with a purpose-built command that reads the already-generated latest prediction and posts it to Kicktipp without going through reprediction logic.
- Add cached or precomputed matchday completeness state if `collect-context` continues to rely on persisted outcome completeness checks.
- Consider persisting lightweight aggregate documents for cost reporting instead of deriving all totals from historical prediction collections every time.
- If the direct timestamp query for versioned context documents requires a Firestore composite index in production, create an index for:
  - collection: `context-documents`
  - fields: `documentName` ascending, `communityContext` ascending, `competition` ascending, `createdAt` descending, `version` descending

## Associated Risks

- Splitting verification paths risks behavioral drift if the cheap path and the full outdated path diverge over time.
- Cost aggregation documents reduce reads but introduce consistency concerns if write-time aggregation ever fails or is partially updated.
- A purpose-built copy command for `ehonda-ai-arena` reduces reads, but it needs careful safeguards so it never posts stale data to Kicktipp.
- Additional Firestore indexes improve query efficiency but add deployment steps and should be documented so production rollout does not fail unexpectedly.

## Firestore Index Creation

If production reports a missing index for the new query shapes, create the composite index directly from the Firestore error link or via the Firebase console.

### `context-documents`

- Collection: `context-documents`
- Query scope: collection
- Fields:
  - `documentName` ascending
  - `communityContext` ascending
  - `competition` ascending
  - `createdAt` descending
  - `version` descending

### `match-outcomes` if Firestore requests it

- Collection: `match-outcomes`
- Query scope: collection
- Fields:
  - `communityContext` ascending
  - `competition` ascending
  - `matchday` ascending

### Console Steps

1. Open Firebase Console.
2. Go to Firestore Database.
3. Open the Indexes tab.
4. Choose Create Index.
5. Enter the collection name and field directions above.
6. Save the index and wait for it to finish building.

The fastest path is usually to run the query once in production, open the Firestore error link, and accept the pre-filled index definition.

# Langfuse Experiment Run Read Reduction

This document focuses on the experiment and dataset-export path because it is the most likely next area to tackle after the repository query fixes.

## Analysis

- The sampled Langfuse experiment runner exports a canonical dataset, selects a slice, and then exports one reconstructed experiment item per selected match and model.
- `export-experiment-dataset` loops across matchdays and calls `GetMatchdayOutcomesAsync` once per matchday.
- `export-experiment-item` reads the stored match, reads persisted outcomes for the specific matchday, and reconstructs a historical prompt.
- Prompt reconstruction resolves each required context document individually at a historical timestamp.
- A reconstructed match prompt usually needs seven required context documents and up to two optional transfer documents, based on [MatchContextDocumentCatalog.cs](../../src/Core/MatchContextDocumentCatalog.cs).
- The default sampled flow currently uses a sample size of `10` and models `o3` plus `gpt-5-nano`, so even a small experiment can trigger many repeated repository lookups.

## Estimated Impact

- The repository fixes already reduce this path noticeably because both matchday outcome retrieval and context timestamp resolution are on the hot path.
- The remaining dominant cost is repeated reconstruction per selected item per model, especially when the same matchday or overlapping context documents are reconstructed multiple times.
- A modest slice run can still perform dozens to hundreds of Firestore document reads even after the repository fixes, depending on the number of context document versions and selected items.

## Suggestions On How To Fix

- Batch and cache matchday outcomes inside the experiment export flow so `export-experiment-item` does not reload the same matchday outcomes repeatedly for items from the same matchday.
- Add a reconstruction cache keyed by `(communityContext, documentName, promptTimestamp)` so repeated item exports for the same slice reuse resolved context document versions.
- Consider a slice-oriented export command that reconstructs all selected items in one process with shared caches instead of invoking `export-experiment-item` separately for every selected item and model.
- If experiments routinely use a relative timestamp policy like `startsAt - 12h`, consider materializing the resolved context-version metadata once into the canonical dataset so later runs do not need to rediscover it.
- Keep the current direct timestamp lookup index in place for `context-documents`:
  - collection: `context-documents`
  - fields: `documentName` ascending, `communityContext` ascending, `competition` ascending, `createdAt` descending, `version` descending

## Associated Risks

- Caching resolved context documents inside the export path improves reads but increases implementation complexity and memory usage for larger slices.
- Precomputing resolved context metadata in the dataset improves repeatability and cost, but it couples the dataset more tightly to a specific reconstruction policy.
- A slice-oriented bulk export command would reduce repeated setup overhead, but it changes the current tool contract and requires extra validation so the produced artifacts remain identical to the single-item path.

## Firestore Index Creation

The experiment path depends on the same `context-documents` timestamp lookup introduced by the repository optimization.

- Collection: `context-documents`
- Query scope: collection
- Fields:
  - `documentName` ascending
  - `communityContext` ascending
  - `competition` ascending
  - `createdAt` descending
  - `version` descending

If this index is missing in production, run an affected experiment export once, open the Firestore error link, and create the pre-filled composite index.

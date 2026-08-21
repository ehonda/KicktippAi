# ADR-0031: Correct DFB-Pokal final inventory coverage

- Status: Accepted
- Date: 2026-08-21

## Context

ADR-0029 expected the DuckDB-missing DFB-Pokal final to occur in three selected inventory rows. The canonical completed-row export and full deterministic join show four occurrences: `away-history-vfb.csv`, `home-history-fcb.csv`, `recent-history-fcb.csv`, and `recent-history-vfb.csv`. All four are the exact same FC Bayern München 3:0 VfB Stuttgart final identity and OpenLigaDB match `81581`; the apparent discrepancy was an inventory-count expectation, not a source conflict.

## Decision

Require OpenLigaDB DFB-Pokal match `81581` exactly once in each of the four named documents and nowhere else. Every occurrence must be `DFB`, FC Bayern München home, VfB Stuttgart away, normalized score `3:0`, played date `2026-05-23`, source URL `https://api.openligadb.de/getmatchdata/dfb/2025`, and the frozen DFB response revision from ADR-0029.

Report this as one unique consumed source match covering four inventory rows. The other three completed DFB-Pokal inventory rows remain derived from the pinned DuckDB source. Missing, additional, duplicate, or conflicting uses fail map validation.

## Alternatives considered

- **Keep the expected count of three:** Rejected because it contradicts the canonical 263-row completed inventory.
- **Collapse repeated document rows:** Rejected because each selected document is an independent prompt context with its own exact row identity.

## Consequences

- Source reporting transparently distinguishes one unique final from its four selected-document occurrences.
- The DFB endpoint remains limited to the same single DuckDB-missing identity; no source scope is broadened.

## Affected tasks

- [P0-14](../tasks/p0-14-profile-driven-collection.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

ADR-0029 only where it states that the final covers three inventory rows. All source, validation, attribution, fixed-capture, and no-runtime-fetch requirements remain valid.

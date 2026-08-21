# ADR-0032: Freeze the complete preseason history set and publish it atomically

- Status: Accepted
- Date: 2026-08-21

## Context

The first P0-22 inventory used matchday 1. It covered all 18 `recent-history-*` documents but only the nine clubs that were at home and the nine clubs that were away on that matchday. Later matchdays reverse those roles, so the original 36-document, 263-completed-row map could not resolve the missing nine `home-history-*` and nine `away-history-*` documents.

The provider's home/away lookup also ignored its requested matchday and opened the current `tippabgabe` page. That made a multi-matchday inventory appear to exercise distinct fixture pages while it could repeatedly read the same page. In addition, parsing only the documents that happened to be returned allowed a per-match provider failure or a header-only document to evade the complete-set gate. Finally, ordinary context saves were sequential. Even after an in-memory audit passed, a failure after the first save could expose a partially updated latest history set.

## Decision

When a matchday is explicitly requested, the Kicktipp home/away history lookup uses that exact positive `spieltagIndex`. Read-only preseason capture uses matchdays 1 and 2, verifies the selected names required by every fetched fixture through `MatchContextDocumentCatalog`, and rejects a missing, unexpected, or header-only selected history document. The resulting launch seed contains all 54 manifest-owned names: one `recent-history-*`, one `home-history-*`, and one `away-history-*` document for each of the 18 clubs.

The read-only capture contains 432 raw rows. The canonical map contains the 398 completed inventory rows covering 196 unique source matches; 34 incomplete scheduled rows remain excluded under ADR-0026. The source split is 326 rows / 152 unique matches from the pinned `transfermarkt-datasets` revision, 70 rows / 43 unique matches from the accepted captured OpenLigaDB responses, and 2 rows / 1 unique match from UEFA match 2047743. No date is copied from another document, inferred from row order, or inferred from a recent-history occurrence.

Production map parsing requires the exact 54-name manifest set. Every document must be nonempty, completed-row ordinals must be contiguous from 1, and rows must retain ordinal document ordering. Fragment parsing exists only for focused source-contract tests and is not a production load path. The tracked CSV remains deterministic UTF-8 without BOM, CRLF-only, and final-terminated.

For normal Bundesliga collection, the expected selected-history names are derived from the fetched fixture set before provider enumeration. Zero fixtures, any per-match collection exception, or exact-set disagreement is fatal. The Core collector accepts an explicit expected set and returns the original bytes with diagnostics on mismatch. P0-14 may later move selection policy into profile metadata, but it does not own this fail-closed runtime behavior or the complete preseason seed.

`IContextRepository` provides an explicit atomic ordinary-context batch save; it has no sequential default implementation. The Firestore adapter reads and validates every exact competition/community/document partition before staging any writes, retains publication rows in each version ceiling, preserves same-content no-op behavior, and creates all changed ordinary versions in one transaction. Duplicate names, reserved publication keys, corrupt rows, cancellation, or a transaction/concurrency failure publish none. Bundesliga Apply and normal collect-context submit the complete transformed selected-history set through this operation. Audit and dry-run do not write. Non-Bundesliga, WM26, non-history, and head-to-head paths retain their existing behavior.

## Alternatives considered

- **Let later matchdays add missing home/away maps on demand:** Rejected because production would encounter an unresolved prompt document before reviewed source provenance was available.
- **Treat repeated recent rows as date evidence for missing documents:** Rejected because document occurrence and ordering are not canonical source identity.
- **Accept whichever selected documents the provider returned:** Rejected because provider omissions and header-only responses would make a partial set appear complete.
- **Save audited documents sequentially:** Rejected because an adapter failure can expose a mixed latest set even when the in-memory transformation was complete.
- **Reuse the cross-kind context/KPI publication contract:** Rejected because selected history uses ordinary versioned context keys and needs a narrow atomic operation within that existing storage contract, not a new publication head.

## Consequences

- Every possible Bundesliga team/prefix prompt document has frozen preseason played-date coverage before activation.
- Requested matchday capture is observable and testable at the HTTP seam.
- A failed collection or save leaves the previous complete ordinary history set visible.
- The ordinary repository interface gains a required batch operation, and every implementation must provide true atomic semantics.

## Affected tasks

- [P0-14](../tasks/p0-14-profile-driven-collection.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

ADR-0025's affected complete-set and publication wording, and the 263-row coverage statements in ADR-0030 and ADR-0031. Their source hierarchy, exact identities, provenance, source-specific constraints, and license decisions remain in force.

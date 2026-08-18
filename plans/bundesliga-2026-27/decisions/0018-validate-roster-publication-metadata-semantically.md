# ADR-0018: Validate roster publication metadata semantically

- Status: Accepted
- Date: 2026-08-18

## Context

ADR-0017 added the first local DuckDB adapter and roster reconstruction metadata. Review found that deserializing and normalizing metadata while reconstructing can make a non-canonical or semantically inconsistent headed snapshot appear valid. The collector also needs to distinguish a rejected membership candidate from a genuine schema/enrichment failure without losing exact selected and attempted provenance.

## Decision

This ADR supersedes only the **Published roster metadata / last-known-good reconstruction** subsection of ADR-0017.

Roster publication metadata is canonical JSON with object root and exactly these properties in this ordinal order: `contract`, `qualityReportCsv`, `clubs`. It uses the repository's deterministic JSON serialization: no indentation or relaxed escaping, property names exactly as stated, and no unknown, duplicate, or reordered properties. `clubs` is the exact manifest-slug order. Each club object and member object has the exact ordered property set defined by the Core metadata contract. Arrays are already canonical: values are not normalized, sorted, deduplicated, or repaired while reading. String identity, source references, diagnostics, member order, and metadata date text must match their canonical rendered/source values exactly.

Core validates metadata semantically before building and while reconstructing: selected source, DuckDB gate, selection reason, revision, DuckDB snapshot date, last-known-good snapshot ID, source references, counts, and diagnostics must form an accepted matrix. Selected provenance remains the actual chosen membership source. Attempted DuckDB revision and snapshot date remain visible in the quality report even when that candidate is rejected; they are not substituted for selected provenance. Metadata carries the exact selected membership identities and the exact selected source provenance required for later overlap evaluation.

Membership reading/selection is independent of enrichment. Membership candidates retain raw club/player rows until the per-club gates evaluate duplicates, identity, season, count, coach, and overlap. Enrichment runs only for the exact stable player IDs selected for that club and at that selected membership date. It uses only a date of birth at or before that date and a positive valuation at or before that date; equal latest valuation dates must agree. Position values must be exact canonical roster values. Numeric database values convert only through checked, lossless integer conversion.

A missing DuckDB path means `NOT_AVAILABLE` and normal fallback selection. A genuine local-file, schema, query, conversion, or enrichment failure is distinct. With a valid headed last-known-good snapshot, the command returns success after reporting the failure and the exact loaded snapshot, without publishing or moving a head. Without a headed snapshot it may publish the complete seed with `N/A` enrichment and `ENRICHMENT_UNAVAILABLE`. Per-club invalid membership is isolated and falls back for that club; a global duplicate selected player identity or incomplete 18-club set blocks construction/publication.

## Alternatives considered

- **Normalize metadata at read time:** Rejected because it hides corruption and makes snapshot reconstruction non-reproducible.
- **Treat enrichment failure as a membership rejection:** Rejected because membership and supplemental data have different safety behavior under ADR-0011.
- **Use name matching for enrichment:** Rejected because only stable IDs are an approved enrichment boundary.

## Consequences

- Roster metadata becomes a validated durable contract rather than auxiliary JSON.
- A valid headed snapshot is never overwritten by a degraded refresh.
- Tests use compact parameterized local DuckDB fixtures to cover all gate and enrichment outcomes.

## Affected tasks

- [P0-07](../tasks/p0-07-roster-contract.md)
- [P0-08](../tasks/p0-08-roster-membership-seed.md)
- [P0-09](../tasks/p0-09-roster-collector.md)
- [P0-12](../tasks/p0-12-match-context-and-transfer-retirement.md)
- [P0-15](../tasks/p0-15-context-document-hygiene.md)
- [P1-05](../tasks/p1-05-roster-refresh.md)

## Supersedes

Only the **Published roster metadata / last-known-good reconstruction** subsection of [ADR-0017](0017-roster-collector-duckdb-and-reconstruction-contract.md).

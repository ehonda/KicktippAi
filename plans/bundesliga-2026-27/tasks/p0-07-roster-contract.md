# P0-07 — Define roster seed and document contracts

- Status: Complete
- Priority: P0
- Depends on: [P0-04](p0-04-team-manifest.md)
- Decisions: [ADR-0003](../decisions/0003-duckdb-primary-rosters-with-fallback.md), [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md), [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md), [ADR-0017](../decisions/0017-roster-collector-duckdb-and-reconstruction-contract.md), [ADR-0018](../decisions/0018-validate-roster-publication-metadata-semantically.md), [ADR-0019](../decisions/0019-roster-publication-truth-boundary.md)

## Outcome

Roster membership, enrichment, per-team documents, aggregate documents, summaries, provenance, and failure rules have a testable contract before collection code is generalized.

## Work items

- [x] Define the complete fallback membership-seed schema, including team slug, role, player/coach name, stable IDs when known, source URL, and membership-as-of date.
- [x] Lock the per-team `roster-{slug}.csv` header and sort order.
- [x] Lock the `team-rosters` aggregate and compact `team-squad-summary` schemas.
- [x] Define `N/A` handling for missing supplemental enrichment and forbid `0` as an unknown value.
- [x] Define per-club DuckDB takeover gates: explicit 2026/27 season identity, manifest identity, unique member identity, plausible squad counts, coach policy, completeness, and actionable enrichment diagnostics.
- [x] Define deterministic per-club source selection and provenance between DuckDB, fallback seed, and last-known-good membership.
- [x] Define freshness, last-known-good, and atomic publication behavior.
- [x] Implement the automatic-versus-fallback decision from ADR-0003 without requiring routine human roster approval.
- [x] Add contract fixtures/tests before implementing the collector.

## Validation

- Validate sample output for deterministic rows, CRLF, header-first content, and a final line terminator.
- Review the contract against the repository CSV context-document rules.

## Validation evidence

- 2026-08-16: `dotnet run --project tests/Core.Tests --no-restore -- --treenode-filter $filter` with `$filter = '/*/*/BundesligaRoster*/**'` passed 19/19 tests outside the sandbox.
- 2026-08-16: `dotnet run --project tests/Core.Tests --no-restore` passed 68/68 tests outside the sandbox.
- Golden fixtures under `tests/Core.Tests/Fixtures/BundesligaRosters/` validate exact headers, deterministic ordering, UTF-8 without BOM, CRLF-only records, final CRLF, `N/A`, money formatting, and summary/report bytes.
- [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md) records the takeover thresholds, provenance, freshness, last-known-good, snapshot hashing, and atomic-publication contract consumed by P0-08 and P0-09.

## Complete when

- P0-08 can author the seed without inventing fields.
- P0-09 can implement publication and failure behavior without an unresolved data-contract choice.

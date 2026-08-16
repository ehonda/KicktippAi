# P0-07 — Define roster seed and document contracts

- Status: Not started
- Priority: P0
- Depends on: [P0-04](p0-04-team-manifest.md)
- Decisions: [ADR-0003](../decisions/0003-duckdb-primary-rosters-with-fallback.md), [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md)

## Outcome

Roster membership, enrichment, per-team documents, aggregate documents, summaries, provenance, and failure rules have a testable contract before collection code is generalized.

## Work items

- [ ] Define the complete fallback membership-seed schema, including team slug, role, player/coach name, stable IDs when known, source URL, and membership-as-of date.
- [ ] Lock the per-team `roster-{slug}.csv` header and sort order.
- [ ] Lock the `team-rosters` aggregate and compact `team-squad-summary` schemas.
- [ ] Define `N/A` handling for missing supplemental enrichment and forbid `0` as an unknown value.
- [ ] Define per-club DuckDB takeover gates: explicit 2026/27 season identity, manifest identity, unique member identity, plausible squad counts, coach policy, completeness, and actionable enrichment diagnostics.
- [ ] Define deterministic per-club source selection and provenance between DuckDB, fallback seed, and last-known-good membership.
- [ ] Define freshness, last-known-good, and atomic publication behavior.
- [ ] Implement the automatic-versus-fallback decision from ADR-0003 without requiring routine human roster approval.
- [ ] Add contract fixtures/tests before implementing the collector.

## Validation

- Validate sample output for deterministic rows, CRLF, header-first content, and a final line terminator.
- Review the contract against the repository CSV context-document rules.

## Complete when

- P0-08 can author the seed without inventing fields.
- P0-09 can implement publication and failure behavior without an unresolved data-contract choice.

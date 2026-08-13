# P0-07 — Define roster seed and document contracts

- Status: Not started
- Priority: P0
- Depends on: [P0-04](p0-04-team-manifest.md)
- Decision: [ADR-0002](../decisions/0002-supersede-transfer-documents.md)

## Outcome

Roster membership, enrichment, per-team documents, aggregate documents, summaries, provenance, and failure rules have a testable contract before collection code is generalized.

## Work items

- [ ] Define the authoritative membership seed schema, including team slug, role, player/coach name, stable IDs when known, source URL, and membership-as-of date.
- [ ] Lock the per-team `roster-{slug}.csv` header and sort order.
- [ ] Lock the `team-rosters` aggregate and compact `team-squad-summary` schemas.
- [ ] Define `N/A` handling for missing supplemental enrichment and forbid `0` as an unknown value.
- [ ] Define quality gates: 18 teams, unique member identity within a team, plausible squad counts, one coach policy, unmatched enrichment reporting, and no missing authoritative membership.
- [ ] Define freshness, last-known-good, and atomic publication behavior.
- [ ] Record source/reuse and automatic-versus-reviewed membership decisions in ADRs.
- [ ] Add contract fixtures/tests before implementing the collector.

## Validation

- Validate sample output for deterministic rows, CRLF, header-first content, and a final line terminator.
- Review the contract against the repository CSV context-document rules.

## Complete when

- P0-08 can author the seed without inventing fields.
- P0-09 can implement publication and failure behavior without an unresolved data-contract choice.

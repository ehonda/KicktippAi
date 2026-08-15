# P0-09 — Implement roster enrichment and collection

- Status: Not started
- Priority: P0
- Depends on: [P0-07](p0-07-roster-contract.md), [P0-08](p0-08-roster-membership-seed.md)
- Decision: [ADR-0003](../decisions/0003-duckdb-primary-rosters-with-fallback.md)

## Outcome

A Bundesliga collector selects quality-gated current-season DuckDB membership per club, otherwise preserves fallback or last-known-good membership, enriches safely, and atomically publishes `roster-*`, `team-rosters`, and `team-squad-summary` documents.

## Work items

- [ ] Extract reusable seed/manifest, DuckDB enrichment, CSV rendering, quality-report, and last-known-good mechanics from the WM26 lineup implementation without renaming the historical WM26 contract.
- [ ] Add a Bundesliga roster source and CLI command with explicit seed, manifest, DuckDB path, community context, and competition inputs.
- [ ] Select DuckDB membership automatically only when the club explicitly represents 2026/27 and passes every P0-07 gate; never infer current membership from transfer events alone.
- [ ] Fall back per club to the source-dated seed or last-known-good snapshot when DuckDB is missing, stale, partial, or suspicious.
- [ ] Join enrichment only by stable identifiers or explicitly reviewed mappings.
- [ ] Calculate age, position, latest market value, and summary aggregates according to P0-07.
- [ ] Publish all per-team and aggregate documents to `bundesliga-2026-27` only after every required quality gate succeeds.
- [ ] Preserve the previous complete version when source selection, enrichment, or upstream refresh fails.
- [ ] Emit actionable unmatched-member and coverage diagnostics in dry-run and normal modes.
- [ ] Add source, command, CSV, quality-gate, last-known-good, and Firestore upload tests.

## Validation

- Run the new roster test tree plus unchanged `CollectContextLineupsCommandTests` and `Wm26LineupSourceTests`.
- Dry-run valid DuckDB takeover, fallback, last-known-good, and deliberately incomplete fixtures.

## Complete when

- All 18 roster documents and both aggregates are deterministic and complete.
- Missing enrichment yields `N/A` and a report; invalid DuckDB retains fallback/last-known-good membership; an incomplete 18-club result blocks publication.
- WM26 lineup behavior still passes its existing tests.

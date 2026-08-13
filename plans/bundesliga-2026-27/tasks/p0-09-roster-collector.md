# P0-09 — Implement roster enrichment and collection

- Status: Not started
- Priority: P0
- Depends on: [P0-07](p0-07-roster-contract.md), [P0-08](p0-08-roster-membership-seed.md)
- Decision: [ADR-0002](../decisions/0002-supersede-transfer-documents.md)

## Outcome

A Bundesliga collector treats the reviewed seed as membership truth, enriches it from DuckDB, and atomically publishes `roster-*`, `team-rosters`, and `team-squad-summary` documents.

## Work items

- [ ] Extract reusable seed/manifest, DuckDB enrichment, CSV rendering, quality-report, and last-known-good mechanics from the WM26 lineup implementation without renaming the historical WM26 contract.
- [ ] Add a Bundesliga roster source and CLI command with explicit seed, manifest, DuckDB path, community context, and competition inputs.
- [ ] Join only by stable identifiers or explicitly reviewed mappings; never derive membership from transfers or `players.current_club_*`.
- [ ] Calculate age, position, latest market value, and summary aggregates according to P0-07.
- [ ] Publish all per-team and aggregate documents to `bundesliga-2026-27` only after every required quality gate succeeds.
- [ ] Preserve the previous complete version when enrichment is partial or upstream refresh fails.
- [ ] Emit actionable unmatched-member and coverage diagnostics in dry-run and normal modes.
- [ ] Add source, command, CSV, quality-gate, last-known-good, and Firestore upload tests.

## Validation

- Run the new roster test tree plus unchanged `CollectContextLineupsCommandTests` and `Wm26LineupSourceTests`.
- Dry-run both the checked-in seed and a deliberately incomplete fixture.

## Complete when

- All 18 roster documents and both aggregates are deterministic and complete.
- Missing enrichment yields `N/A` and a report; missing membership blocks publication.
- WM26 lineup behavior still passes its existing tests.

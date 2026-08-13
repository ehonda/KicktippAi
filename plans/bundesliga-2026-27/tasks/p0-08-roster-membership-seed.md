# P0-08 — Author the roster membership seed

- Status: Not started
- Priority: P0
- Depends on: [P0-07](p0-07-roster-contract.md)

## Outcome

The repository contains a reviewed, source-dated membership seed for all 18 Bundesliga clubs.

## Work items

- [ ] Collect each club's current players and coach from the accepted authoritative source process.
- [ ] Populate the roster seed under `data/bundesliga-2026-27/rosters/` using manifest slugs.
- [ ] Add Transfermarkt player/club IDs only when confidently matched; leave supplemental IDs empty rather than guessing.
- [ ] Record source URL and membership-as-of provenance according to the contract.
- [ ] Run validation and resolve duplicate names, cross-team memberships, missing clubs, implausible squad counts, and missing coaches.
- [ ] Review all promoted-club membership manually because the audited DuckDB season membership is stale for those teams.
- [ ] Commit a generated quality report or deterministic validator output format that P0-09 can reuse.

## Validation

- Run the roster seed validator against the checked-in team manifest.
- Have a second review compare every team count and promoted-club roster against the recorded source.

## Complete when

- All 18 teams pass membership gates without relying on DuckDB current-club fields.
- Every row has authoritative provenance and a membership-as-of date.

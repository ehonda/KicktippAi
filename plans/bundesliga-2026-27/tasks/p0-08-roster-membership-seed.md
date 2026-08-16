# P0-08 — Author the roster membership seed

- Status: Not started
- Priority: P0
- Depends on: [P0-07](p0-07-roster-contract.md)
- Decisions: [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md), [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md)

## Outcome

The repository contains a complete, source-dated fallback membership seed for all 18 Bundesliga clubs.

## Work items

- [ ] Collect each club's current players and coach from official club squad sources and cross-check the league listing.
- [ ] Populate the roster seed under `data/bundesliga-2026-27/rosters/` using manifest slugs.
- [ ] Add Transfermarkt player/club IDs only when confidently matched; leave supplemental IDs empty rather than guessing.
- [ ] Record source URL and membership-as-of provenance according to the contract.
- [ ] Run validation and resolve duplicate names, cross-team memberships, missing clubs, implausible squad counts, and missing coaches.
- [ ] Focus the independent audit on promoted clubs, ambiguous identity mappings, boundary squad counts, coaches, and any club whose DuckDB view is missing, stale, or suspicious.
- [ ] Commit a generated quality report or deterministic validator output format that P0-09 can reuse.

## Validation

- Run the roster seed validator against the checked-in team manifest.
- Have one targeted independent audit compare high-risk rows and every team count against the recorded sources.

## Complete when

- All 18 teams have a valid fallback independent of whether their current DuckDB view passes takeover gates.
- Every row has authoritative provenance and a membership-as-of date.

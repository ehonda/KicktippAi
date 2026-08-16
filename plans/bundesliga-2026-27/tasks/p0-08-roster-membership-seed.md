# P0-08 — Author the roster membership seed

- Status: Complete
- Priority: P0
- Depends on: [P0-07](p0-07-roster-contract.md)
- Decisions: [ADR-0010](../decisions/0010-season-scoped-team-identity-manifest.md), [ADR-0011](../decisions/0011-roster-snapshot-and-publication-contract.md)

## Outcome

The repository contains a complete, source-dated fallback membership seed for all 18 Bundesliga clubs.

## Work items

- [x] Collect each club's current players and coach from official club squad sources and cross-check the league listing.
- [x] Populate the roster seed under `data/bundesliga-2026-27/rosters/` using manifest slugs.
- [x] Add Transfermarkt player/club IDs only when confidently matched; leave supplemental IDs empty rather than guessing.
- [x] Record source URL and membership-as-of provenance according to the contract.
- [x] Run validation and resolve duplicate names, cross-team memberships, missing clubs, implausible squad counts, and missing coaches.
- [x] Focus the independent audit on promoted clubs, ambiguous identity mappings, boundary squad counts, coaches, and any club whose DuckDB view is missing, stale, or suspicious.
- [x] Commit a generated quality report or deterministic validator output format that P0-09 can reuse.

## Validation

- Run the roster seed validator against the checked-in team manifest.
- Have one targeted independent audit compare high-risk rows and every team count against the recorded sources.

## Validation evidence

- 2026-08-16: the embedded and checked-in fallback validates against the manifest with 18 clubs, 534 players, 18 primary coaches, 464 confidently matched stable player IDs, and 70 intentionally empty player IDs. Every club has 20-40 players, one membership date, authoritative HTTPS provenance, the manifest club ID, deterministic ordering, and no duplicate stable ID.
- 2026-08-16: the independent source audit covered all team counts and coaches, with extra attention to promoted Schalke (29), Paderborn (30), and Elversberg (28), the Borussia Mönchengladbach page's public GraphQL payload, RB Leipzig's current route, Hoffenheim's current team payload, and Mainz's multi-source summer reconciliation.
- 2026-08-16: dated official transfer evidence assigns Ransford Königsdörffer to Mainz rather than the stale HSV card, while Schalke's current 2026/27 page excludes Junior Dina Ebimbe and Edin Džeko from an earlier stale capture. The resulting seed has no cross-club identity collision.
- 2026-08-16: Transfermarkt IDs were matched read-only against the repository-local research DuckDB snapshot recorded at upstream commit `154367d`; exact normalized names, official dates of birth, and unambiguous club identity were used where needed, and non-unique or absent candidates remained empty.
- 2026-08-16: `roster-membership-quality-report.csv` deterministically records all 18 fallback selections, source references, player/coach counts, stable-ID coverage, and missing-ID diagnostics in ADR-0011's reusable schema.
- 2026-08-16: byte validation passed for both checked-in CSVs: UTF-8 without BOM, CRLF-only records, exact header first, and final CRLF. The seed has 553 physical lines and the report has 19.
- 2026-08-16: `dotnet run --project tests/Core.Tests --no-restore -- --treenode-filter $filter` with `$filter = '/*/*/BundesligaRosterSeedTests/*'` passed 8/8 tests outside the sandbox.
- 2026-08-16: `dotnet run --project tests/Core.Tests --no-restore` passed 96/96 tests outside the sandbox.

## Complete when

- All 18 teams have a valid fallback independent of whether their current DuckDB view passes takeover gates.
- Every row has authoritative provenance and a membership-as-of date.

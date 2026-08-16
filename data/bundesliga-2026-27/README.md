# Bundesliga 2026/27 source data

`team-manifest.csv` is the checked-in join boundary for the 18 current Bundesliga clubs. Runtime code reads the same typed records that roster and Club Elo collectors use; consumers must not derive a Bundesliga slug from a display name.

## Schema and formatting

| Column | Contract |
|---|---|
| `Kicktipp_Name` | Exact, case-sensitive team name returned by the selected Kicktipp community. |
| `Team_Slug` | Stable KicktippAi document slug. It is not a Bundesliga display code. |
| `Official_Name` | Club name from the official Bundesliga club overview. |
| `Official_Roster_Source_Url` | HTTPS Bundesliga club page containing the club's `Kader` section. |
| `Club_Elo_Name` | Exact Club Elo club/API route alias linked from the current Germany ranking. |
| `Transfermarkt_Club_Id` | Optional positive Transfermarkt identifier for DuckDB enrichment. |

Rows are sorted by `Team_Slug` using ordinal comparison. The file is UTF-8 without a byte-order mark, uses CRLF line endings, has no leading blank line, and ends with a final CRLF.

## Source evidence

- Kicktipp names: a read-only dry run on 2026-08-16 using `collect-context kicktipp --community-context ehonda-dev-buli-2627 --competition bundesliga-2026-27 --matchdays 1 --dry-run` returned nine fixtures and all 18 unique names represented here.
- Official names and roster URLs: the [official Bundesliga club overview](https://www.bundesliga.com/de/bundesliga/clubs?firsttab=kader) listed the same 18 clubs on 2026-08-16. Each row links its corresponding official club page and `Kader` section.
- Club Elo aliases: the [Club Elo Germany ranking](https://clubelo.com/GER), source-dated 2026-08-14 when checked on 2026-08-16, listed all 18 clubs and linked their API route aliases represented here, including `Bayern`, `Koeln`, `RBLeipzig`, and `UnionBerlin` where the display label differs. P0-10 locks those aliases and the observed ratings in the complete launch seed below. This mapping does not authorize unattended network refresh; [ADR-0008](../../plans/bundesliga-2026-27/decisions/0008-launch-club-elo-from-a-dated-seed.md) keeps that gate closed.
- Transfermarkt IDs: optional enrichment IDs were cross-checked against the repository-local `transfermarkt-datasets.duckdb` research snapshot recorded on 2026-08-13 at upstream commit `154367d`. They do not establish season membership.

The identity and lookup behavior are fixed by [ADR-0010](../../plans/bundesliga-2026-27/decisions/0010-season-scoped-team-identity-manifest.md).

## Club Elo launch seed

`club-elo-launch-seed.csv` is the complete launch-safe snapshot captured from the [Club Elo Germany ranking](https://clubelo.com/GER) on 2026-08-16. Its single `Rated_At` value is the provider's 2026-08-14 rating date; `Collected_At` separately records the UTC capture time. The 18 rows use the manifest's exact `Team_Slug` and `Club_Elo_Name` joins and are ordered by slug.

The strict embedded Core parser requires the exact header, exact manifest coverage and aliases, positive integer ELO values, positive unique global ranks, one rating date, one collection time, one HTTPS source, ordinal row order, UTF-8 without a byte-order mark, CRLF-only line endings, and a final CRLF. The checked-in seed itself is the deterministic complete fixture; malformed cases are generated narrowly in tests rather than duplicating the source data.

The historical dated CSV endpoint timed out twice during bounded P0-04 verification. No HTTP provider is enabled by this seed. Unattended reuse remains an owner gate; a later explicitly enabled candidate must be complete, no more than seven calendar days old at collection, and strictly newer than the retained complete snapshot. See [ADR-0008](../../plans/bundesliga-2026-27/decisions/0008-launch-club-elo-from-a-dated-seed.md) and [ADR-0013](../../plans/bundesliga-2026-27/decisions/0013-club-elo-snapshot-and-freshness-contract.md).

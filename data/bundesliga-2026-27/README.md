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

## Prediction-authority generations

[ADR-0065](../../plans/bundesliga-2026-27/decisions/0065-require-global-typed-prediction-authority-and-isolated-cutover.md) reserves these season-scoped paths for P1-13:

- `prediction-authority/identity-seeds/<posting-community>/generation-<NNNN>.json` for an immutable identity-seed generation owned by one Posting Community; and
- `prediction-authority/copy-bindings/<posting-community>--from--<source-community>/generation-<NNNN>.json` for an immutable Copy Binding between one Posting Community and one Prediction-source Community.

Arena participants share the `ehonda-ai-arena` Posting Community namespace. Kicktipp IDs are local to a Posting Community and item kind, never globally unique. A Stable Local Item Key fixes the season, Posting Community, item kind, and exact Kicktipp ID. Its Snapshot Hash separately fixes the item's semantic state: subcompetition, round, result basis, teams, and schedule for a match; question text, deadline, prediction limit, and exact option identities for a bonus question.

Generations are additive and immutable. A consumer must pin an exact generation and content hash; directory enumeration, a `latest` convention, or an implicit default is not authority. Each Copy Binding records the exact posting and source keys, Snapshot Hashes, seed generations, and one-to-one option mapping where applicable. It proves correspondence, not prediction compatibility, and cannot clone or backfill a source record into the Posting Community namespace.

Real-community seed and binding content requires P1-13 R5b's existing Owner-controlled authenticated evidence and external-read authority. It must contain identifiers and provenance only—never credentials, cookies, or form payloads. This R0 specification creates no seed generation and changes no runtime data.

## Source evidence

- Kicktipp names: a read-only dry run on 2026-08-16 using `collect-context kicktipp --community-context ehonda-dev-buli-2627 --competition bundesliga-2026-27 --matchdays 1 --dry-run` returned nine fixtures and all 18 unique names represented here.
- Official names and roster URLs: the [official Bundesliga club overview](https://www.bundesliga.com/de/bundesliga/clubs?firsttab=kader) listed the same 18 clubs on 2026-08-16. Each row links its corresponding official club page and `Kader` section.
- Club Elo aliases: the [Club Elo Germany ranking](https://clubelo.com/GER), source-dated 2026-08-14 when checked on 2026-08-16, listed all 18 clubs and linked their API route aliases represented here, including `Bayern`, `Koeln`, `RBLeipzig`, and `UnionBerlin` where the display label differs. P0-10 locks those aliases and the observed ratings in the complete launch seed below. This mapping does not authorize unattended network refresh; [ADR-0008](../../plans/bundesliga-2026-27/decisions/0008-launch-club-elo-from-a-dated-seed.md) keeps that gate closed.
- Transfermarkt IDs: optional enrichment IDs were cross-checked against the repository-local `transfermarkt-datasets.duckdb` research snapshot recorded on 2026-08-13 at upstream commit `154367d`. They do not establish season membership.

The identity and lookup behavior are fixed by [ADR-0010](../../plans/bundesliga-2026-27/decisions/0010-season-scoped-team-identity-manifest.md).

## Roster membership fallback

`rosters/roster-membership-seed.csv` is the complete fallback snapshot collected on 2026-08-16 under [ADR-0011](../../plans/bundesliga-2026-27/decisions/0011-roster-snapshot-and-publication-contract.md). It contains 534 current players and exactly one primary coach for each of the 18 manifest clubs. The 552 data rows are ordered by manifest slug, coach before players, normalized name, and stable player ID. All club IDs are the manifest IDs. Of the 534 players, 464 have a confident stable Transfermarkt ID from the repository-local research snapshot; the other 70 IDs are deliberately empty.

`rosters/roster-membership-quality-report.csv` is the deterministic 18-row fallback audit in ADR-0011's reusable quality-report schema. `Known_*` and value counts remain zero because those are P0-09 enrichment outputs, not membership claims. `MISSING_STABLE_PLAYER_IDS:*` is informational: optional IDs do not invalidate authoritative membership.

| Slug | Players | Primary coach | Official player source | Official coach source |
|---|---:|---|---|---|
| `b04` | 31 | Carles Martínez | [Werkself](https://www.bayer04.de/de-de/team/werkself/bayer-04-leverkusen) | same page |
| `bmg` | 31 | Eugen Polanski | [Fohlenelf](https://www.borussia.de/kader-und-staff-fohlenelf) | same page |
| `bvb` | 27 | Niko Kovac | [Profis](https://www.bvb.de/de/de/mannschaften/fussball/profis.html) | same page |
| `fca` | 29 | Manuel Baum | [Kader](https://www.fcaugsburg.de/team/) | [Funktionsteam](https://www.fcaugsburg.de/team/people) |
| `fcb` | 25 | Vincent Kompany | [Profis](https://fcbayern.com/de/teams/profis) | same page |
| `fck` | 26 | René Wagner | [Männerkader](https://fc.de/mannschaften/maenner/kader) | same page |
| `fcu` | 30 | Mauro Lustrinelli | [Profis Männer](https://www.fc-union-berlin.de/de-e/fussball/profis-maenner/kader-F0GX) | same page |
| `hsv` | 30 | Merlin Polzin | [Spieler](https://www.hsv.de/profis/spieler/) | [current coach evidence](https://www.hsv.de/news/merlin-polzin-jeden-einzelnen-tag-gewinnen) |
| `m05` | 29 | Urs Fischer | [Kader](https://www.mainz05.de/tab/kader) | [Trainer](https://www.mainz05.de/tab/trainer) |
| `rbl` | 34 | Martín Demichelis | [Männer](https://rbleipzig.com/de/teams/maenner) | same page |
| `s04` | 29 | Miron Muslić | [Kader 2026/27](https://schalke04.de/kader-2026-2027/) | [coach profile](https://schalke04.de/teams/profis/person/miron-muslic/) |
| `scf` | 30 | Julian Schuster | [Spieler](https://www.scfreiburg.com/teams/profis/spieler/) | [coach profile](https://www.scfreiburg.com/teams/profis/trainer/julian-schuster/) |
| `scp` | 30 | Ralf Kettemann | [Mannschaft](https://www.scp07.de/Teams/Profis/Mannschaft/) | same page |
| `sge` | 31 | Adi Hütter | [Kader](https://profis.eintracht.de/kader/) | same page |
| `sve` | 28 | Vincent Wagner | [Profikader](https://sv07elversberg.de/teams/profis/kader/) | same page |
| `svw` | 31 | Daniel Thioune | [Spieler](https://www.werder.de/teams/maenner/spieler/) | [Trainerteam](https://www.werder.de/teams/maenner/trainerteam) |
| `tsg` | 29 | Christian Ilzer | [Profis](https://www.tsg-hoffenheim.de/teams/profis/team) | same page |
| `vfb` | 34 | Sebastian Hoeneß | [Kader 2026/27](https://www.vfb.de/de/1893/profis/kader/saisonen/2026-2027/listenansicht/) | same page |

The independent high-risk audit covered all three promoted clubs and the sources whose normal page crawler was stale or incomplete. Borussia Mönchengladbach's official page was cross-checked through the public GraphQL payload used by that page; RB Leipzig's current `/de/teams/maenner` route and Hoffenheim's current team payload replaced stale legacy routes. Mainz's current 29-card squad was reconciled against its [2026/27 training-start evidence](https://www.mainz05.de/news/trainingsauftakt-sommervorbereitung-profis-2627) and dated summer transactions. The [official HSV departure notice](https://www.hsv.de/news/ransford-koenigsdoerffer-verlaesst-den-hsv) and [official Mainz arrival notice](https://www.mainz05.de/news/ransford-konigsdorffer-wird-mainzer) place Ransford Königsdörffer only at Mainz. Schalke's current 29-player 2026/27 page excludes Junior Dina Ebimbe and Edin Džeko from an earlier stale capture. These reconciliations eliminate cross-club identity collisions without inferring membership from Transfermarkt data.

## Club Elo launch seed

`club-elo-launch-seed.csv` is the complete launch-safe snapshot captured from the [Club Elo Germany ranking](https://clubelo.com/GER) on 2026-08-16. Its single `Rated_At` value is the provider's 2026-08-14 rating date; `Collected_At` separately records the UTC capture time. The 18 rows use the manifest's exact `Team_Slug` and `Club_Elo_Name` joins and are ordered by slug.

The strict embedded Core parser requires the exact header, exact manifest coverage and aliases, positive integer ELO values, positive unique global ranks, one rating date, one collection time, one HTTPS source, ordinal row order, UTF-8 without a byte-order mark, CRLF-only line endings, and a final CRLF. The checked-in seed itself is the deterministic complete fixture; malformed cases are generated narrowly in tests rather than duplicating the source data.

The historical dated CSV endpoint timed out twice during bounded P0-04 verification. No HTTP provider is enabled by this seed. Unattended reuse remains an owner gate; a later explicitly enabled candidate must be complete, no more than seven calendar days old at collection, and strictly newer than the retained complete snapshot. See [ADR-0008](../../plans/bundesliga-2026-27/decisions/0008-launch-club-elo-from-a-dated-seed.md) and [ADR-0013](../../plans/bundesliga-2026-27/decisions/0013-club-elo-snapshot-and-freshness-contract.md).

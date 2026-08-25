# Bundesliga history played-date sources

The canonical map is [`history-played-dates.csv`](history-played-dates.csv). Runtime reads only checked-in files and never fetches these providers.

## Frozen coverage

- Read-only Kicktipp inventory: exact requested matchdays 1 and 2 for `ehonda-dev-buli-2627`.
- Selected documents: all 54 manifest combinations (18 recent, 18 home, 18 away), each with at least one completed result.
- Current authenticated selected inventory: 432 raw rows, of which two incomplete scheduled rows are excluded before completed ordinals are assigned. The canonical map has 430 rows covering 212 unique source matches after the completed 2026/27 DFB-Pokal first-round transition.
- Source split: 326 rows / 152 unique matches from `transfermarkt-datasets`; 102 / 59 from OpenLigaDB; 2 / 1 from UEFA.
- Canonical map SHA-256: `E341010B4BB0F95FF31009BF904616C825212118D4E3F80D7D2DBCB0F8732492`.

The checked-in map is the frozen inventory plus played-date provenance. Dates were joined directly to the accepted source identities; no date was derived from another selected document or from row ordering.

## transfermarkt-datasets

- Scope: completed `1.BL`, `DFB`, `CL`, `EL`, and `ConfL` rows covered by the captured DuckDB artifact.
- Revision: `154367dfa6d6eb0b86332e332f9df0a080c7ddce`.
- Repository: <https://github.com/dcaribou/transfermarkt-datasets/tree/154367dfa6d6eb0b86332e332f9df0a080c7ddce>.
- License: CC0 1.0, <https://github.com/dcaribou/transfermarkt-datasets/blob/154367dfa6d6eb0b86332e332f9df0a080c7ddce/LICENSE>.

## OpenLigaDB

- Scope: completed 2025/26 `2.BL` and Bundesliga `Releg` rows absent from the DuckDB artifact; the exact 2025/26 DFB-Pokal final identity absent from that artifact and repeated in four selected inventory rows; and the 16 exact 2026/27 DFB-Pokal identities repeated across 32 authenticated selected-history occurrences.
- League endpoint: <https://api.openligadb.de/getmatchdata/bl2/2025>.
- Relegation endpoint: <https://api.openligadb.de/getmatchdata/rel/2025>.
- DFB-Pokal endpoint: <https://api.openligadb.de/getmatchdata/dfb/2025>.
- Live DFB-Pokal checkpoint endpoint: <https://api.openligadb.de/getmatchdata/dfb/2026>.
- Provider and API documentation: <https://openligadb.de/>.
- License: Open Database License (ODbL) 1.0, <https://opendatacommons.org/licenses/odbl/1-0/>.
- Frozen league response: [`sources/openligadb-bl2-2025.json`](sources/openligadb-bl2-2025.json), SHA-256 `83dbea21fe56c30ed2393dd888efede627cbdf7b26c5694f14753cf792af6a84`.
- Frozen relegation response: [`sources/openligadb-rel-2025.json`](sources/openligadb-rel-2025.json), SHA-256 `0cbe277ed6539364eb4f9f2122e4af33e2e10a5797e159382b27908a74e08d8e`.
- Frozen DFB-Pokal response: [`sources/openligadb-dfb-2025.json`](sources/openligadb-dfb-2025.json), SHA-256 `9d16d5d30e5882c592ec4d8b39b592ea0f102c2e2695da98897f76a87b6ec2a3`.
- Frozen 2026/27 DFB-Pokal response: [`sources/openligadb-dfb-2026.json`](sources/openligadb-dfb-2026.json), 74,988 bytes, SHA-256 `92ca6f8c7175970db15bbdcea15cb79f3f2e83cb52a59300cfcf9591760affa2`.

The 2026/27 response was frozen at `2026-08-25T01:45:04+02:00` while the endpoint was live. It contains 32 positive unique `dfb/2026` fixtures and exactly 30 completed IDs; `81836` and `81852` remain incomplete. Every completed match has exactly one full-time result. Match `81843` additionally has the exact after-extra-time `2:5` result used by Kicktipp with annotation `nach Verlängerung`; match `81832` retains the previously accepted halftime `0:10` and full-time `0:11` evidence.

An authenticated read-only matchdays 1+2 export is the occurrence authority. Its exact 54 documents contain 430 completed rows and two excluded incomplete rows. Relative to the prior 400-row map, it adds 30 occurrences / 15 fixtures while retaining all prior identities. Together with the existing two `81832` occurrences, exactly 16 source matches / 32 document rows use the new frozen revision. Each appears only in the exact `away-history-*` and `recent-history-*` documents recorded by ADR-0041. The other 14 completed response matches and both incomplete matches contribute no map row. Runtime never fuzzy-matches the reviewed provider/Kicktipp naming differences.

Contains information from OpenLigaDB, which is made available under the Open Database License (ODbL) 1.0. The four raw OpenLigaDB response files and the OpenLigaDB-derived rows in `history-played-dates.csv` are made available under ODbL 1.0. The repository's application and tooling code remains MIT-licensed.

## UEFA match 2047743

- Scope: the single 2026 Europa League final identity absent from the pinned DuckDB artifact, repeated in two selected SC Freiburg inventory rows.
- Match-specific record: <https://www.uefa.com/uefaeuropaleague/match/2047743/>.
- Stable source revision: `UEFA-match-2047743`.
- Independent official club corroboration: <https://www.scfreiburg.com/aktuell/nachrichten/profis/spielberichte/2025-26/finalniederlage-gegen-villa>.

Only the factual match identity and played date are recorded. No UEFA page or PDF content is stored or redistributed, and runtime never fetches either site. A linked match-specific PDF could not be byte-hashed after bounded CDN and browser attempts, so this repository does not claim a PDF revision.

## Manual corroboration

The official DFB Data Center's 2025/26 2. Bundesliga schedule and individual match pages were used only for manual factual corroboration: <https://datencenter.dfb.de/competitions/2-bundesliga/seasons/2025-2026>. No DFB content is automatically extracted or redistributed. The DFB portal terms restrict automated scraping and non-private reuse: <https://www.dfb.de/nutzungsbedingungen>.

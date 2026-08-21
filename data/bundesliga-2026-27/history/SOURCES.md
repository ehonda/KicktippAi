# Bundesliga history played-date sources

The canonical map is [`history-played-dates.csv`](history-played-dates.csv). Runtime reads only checked-in files and never fetches these providers.

## Frozen coverage

- Read-only Kicktipp inventory: exact requested matchdays 1 and 2 for `ehonda-dev-buli-2627`.
- Selected documents: all 54 manifest combinations (18 recent, 18 home, 18 away), each with at least one completed result.
- Raw preseason selected inventory: 432 rows, of which 34 incomplete scheduled rows were excluded before completed ordinals were assigned. The live-completion canonical map has 400 rows covering 197 unique source matches after exact match `81832` was prepended to two SGE documents.
- Source split: 326 rows / 152 unique matches from `transfermarkt-datasets`; 72 / 44 from OpenLigaDB; 2 / 1 from UEFA.
- Canonical map SHA-256: `9EDA9A437B54286A20CCBBE89A6B8701FD77478B69450E7528EB07320A87F221`.

The checked-in map is the frozen inventory plus played-date provenance. Dates were joined directly to the accepted source identities; no date was derived from another selected document or from row ordering.

## transfermarkt-datasets

- Scope: completed `1.BL`, `DFB`, `CL`, `EL`, and `ConfL` rows covered by the captured DuckDB artifact.
- Revision: `154367dfa6d6eb0b86332e332f9df0a080c7ddce`.
- Repository: <https://github.com/dcaribou/transfermarkt-datasets/tree/154367dfa6d6eb0b86332e332f9df0a080c7ddce>.
- License: CC0 1.0, <https://github.com/dcaribou/transfermarkt-datasets/blob/154367dfa6d6eb0b86332e332f9df0a080c7ddce/LICENSE>.

## OpenLigaDB

- Scope: completed 2025/26 `2.BL` and Bundesliga `Releg` rows absent from the DuckDB artifact; the exact 2025/26 DFB-Pokal final identity absent from that artifact and repeated in four selected inventory rows; and 2026/27 DFB-Pokal match `81832` repeated in the two exact live SGE rows.
- League endpoint: <https://api.openligadb.de/getmatchdata/bl2/2025>.
- Relegation endpoint: <https://api.openligadb.de/getmatchdata/rel/2025>.
- DFB-Pokal endpoint: <https://api.openligadb.de/getmatchdata/dfb/2025>.
- Live DFB-Pokal checkpoint endpoint: <https://api.openligadb.de/getmatchdata/dfb/2026>.
- Provider and API documentation: <https://openligadb.de/>.
- License: Open Database License (ODbL) 1.0, <https://opendatacommons.org/licenses/odbl/1-0/>.
- Frozen league response: [`sources/openligadb-bl2-2025.json`](sources/openligadb-bl2-2025.json), SHA-256 `83dbea21fe56c30ed2393dd888efede627cbdf7b26c5694f14753cf792af6a84`.
- Frozen relegation response: [`sources/openligadb-rel-2025.json`](sources/openligadb-rel-2025.json), SHA-256 `0cbe277ed6539364eb4f9f2122e4af33e2e10a5797e159382b27908a74e08d8e`.
- Frozen DFB-Pokal response: [`sources/openligadb-dfb-2025.json`](sources/openligadb-dfb-2025.json), SHA-256 `9d16d5d30e5882c592ec4d8b39b592ea0f102c2e2695da98897f76a87b6ec2a3`.
- Frozen 2026/27 DFB-Pokal response: [`sources/openligadb-dfb-2026.json`](sources/openligadb-dfb-2026.json), 30,666 bytes, SHA-256 `b60d4c1ef214ffa2680efb27cace33cc7b47bf9700b4f57e7043736919a8eeab`.

The 2026/27 response was frozen at 20:13 Europe/Berlin while the endpoint was live. It contains 32 unique `dfb/2026` fixtures: completed IDs `81832` and `81848`, plus 30 incomplete fixtures. Only `81832` (SC St. Tönis 0:11 Eintracht Frankfurt, 2026-08-21 18:00 local, halftime 0:10) joins the selected Bundesliga history inventory. Completed match `81848` (Preußen Münster 1:2 Karlsruher SC) is retained in the immutable response but contributes no map row because neither team belongs to the 18-team Bundesliga manifest. Incomplete results are never date evidence.

Contains information from OpenLigaDB, which is made available under the Open Database License (ODbL) 1.0. The four raw OpenLigaDB response files and the OpenLigaDB-derived rows in `history-played-dates.csv` are made available under ODbL 1.0. The repository's application and tooling code remains MIT-licensed.

## UEFA match 2047743

- Scope: the single 2026 Europa League final identity absent from the pinned DuckDB artifact, repeated in two selected SC Freiburg inventory rows.
- Match-specific record: <https://www.uefa.com/uefaeuropaleague/match/2047743/>.
- Stable source revision: `UEFA-match-2047743`.
- Independent official club corroboration: <https://www.scfreiburg.com/aktuell/nachrichten/profis/spielberichte/2025-26/finalniederlage-gegen-villa>.

Only the factual match identity and played date are recorded. No UEFA page or PDF content is stored or redistributed, and runtime never fetches either site. A linked match-specific PDF could not be byte-hashed after bounded CDN and browser attempts, so this repository does not claim a PDF revision.

## Manual corroboration

The official DFB Data Center's 2025/26 2. Bundesliga schedule and individual match pages were used only for manual factual corroboration: <https://datencenter.dfb.de/competitions/2-bundesliga/seasons/2025-2026>. No DFB content is automatically extracted or redistributed. The DFB portal terms restrict automated scraping and non-private reuse: <https://www.dfb.de/nutzungsbedingungen>.

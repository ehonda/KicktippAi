# Bundesliga history played-date sources

The canonical map is [`history-played-dates.csv`](history-played-dates.csv). Runtime reads only checked-in files and never fetches these providers.

## Frozen coverage

- Read-only Kicktipp inventory checkpoint: ordered matchdays 3, 4, then 2 for `ehonda-dev-buli-2627`.
- Selected documents: all 54 manifest combinations (18 recent, 18 home, 18 away), each with at least one completed result.
- The authenticated checkpoint has 390 completed rows and 42 structurally valid incomplete rows excluded before completed ordinals are assigned. The retained accumulated canonical map has 434 rows covering 214 unique source matches; it is not expected to equal a rolling checkpoint.
- Source split: 326 rows / 152 unique matches from `transfermarkt-datasets`; 106 / 61 from OpenLigaDB; 2 / 1 from UEFA.
- Canonical map SHA-256: `2DA4E4518FF3893142AD2BF5C29ACDFF0A45D54F208BD893E026AF292CF1950C`.

The checked-in map is the frozen inventory plus played-date provenance. Dates were joined directly to the accepted source identities; no date was derived from another selected document or from row ordering.

## transfermarkt-datasets

- Scope: completed `1.BL`, `DFB`, `CL`, `EL`, and `ConfL` rows covered by the captured DuckDB artifact.
- Revision: `154367dfa6d6eb0b86332e332f9df0a080c7ddce`.
- Repository: <https://github.com/dcaribou/transfermarkt-datasets/tree/154367dfa6d6eb0b86332e332f9df0a080c7ddce>.
- License: CC0 1.0, <https://github.com/dcaribou/transfermarkt-datasets/blob/154367dfa6d6eb0b86332e332f9df0a080c7ddce/LICENSE>.

## OpenLigaDB

- Scope: completed 2025/26 `2.BL` and Bundesliga `Releg` rows absent from the DuckDB artifact; the exact 2025/26 DFB-Pokal final identity absent from that artifact and repeated in four selected inventory rows; and the 18 exact 2026/27 DFB-Pokal identities repeated across 36 authenticated selected-history occurrences.
- League endpoint: <https://api.openligadb.de/getmatchdata/bl2/2025>.
- Relegation endpoint: <https://api.openligadb.de/getmatchdata/rel/2025>.
- DFB-Pokal endpoint: <https://api.openligadb.de/getmatchdata/dfb/2025>.
- Live DFB-Pokal checkpoint endpoint: <https://api.openligadb.de/getmatchdata/dfb/2026>.
- Provider and API documentation: <https://openligadb.de/>.
- License: Open Database License (ODbL) 1.0, <https://opendatacommons.org/licenses/odbl/1-0/>.
- Frozen league response: [`sources/openligadb-bl2-2025.json`](sources/openligadb-bl2-2025.json), SHA-256 `83dbea21fe56c30ed2393dd888efede627cbdf7b26c5694f14753cf792af6a84`.
- Frozen relegation response: [`sources/openligadb-rel-2025.json`](sources/openligadb-rel-2025.json), SHA-256 `0cbe277ed6539364eb4f9f2122e4af33e2e10a5797e159382b27908a74e08d8e`.
- Frozen DFB-Pokal response: [`sources/openligadb-dfb-2025.json`](sources/openligadb-dfb-2025.json), SHA-256 `9d16d5d30e5882c592ec4d8b39b592ea0f102c2e2695da98897f76a87b6ec2a3`.
- Frozen 2026/27 DFB-Pokal response: [`sources/openligadb-dfb-2026.json`](sources/openligadb-dfb-2026.json), 77,825 bytes, SHA-256 `728d31be6f928fa83cff7bf56925d0456642686a35bf02fb43e428ebd3ce81eb`.

The response was refreshed at `2026-09-04T22:44:10Z`. It contains 32 positive unique, completed `dfb/2026` fixtures. `81836` is Hamburg Eimsbütteler BC–Borussia Dortmund (`0:5`, 2026-09-01) and `81852` is VfL Osnabrück–FC Bayern München (`1:4`, 2026-09-02); all 30 formerly completed identities retain their accepted IDs, dates, and results. The sole prior source-name delta is `81841`, from `SSV Jeddeloh 2` to `SSV Jeddeloh II`; it has no selected occurrence. Every match has exactly one full-time result. Match `81843` additionally has the exact after-extra-time `2:5` result used by Kicktipp with annotation `nach Verlängerung`; match `81832` retains the previously accepted halftime `0:10` and full-time `0:11` evidence.

The ordered 3+4+2 authenticated export is the rolling occurrence authority: its exact 54-document set has SHA-256 `01919a51b7fbc17c27d47a7a16ad03376456e2efb3ea9bbb56e7e3ff89e184b9`, 390 completed rows, and 42 excluded incomplete rows. It confirms four new selected occurrences: `away-history-bvb.csv` and `recent-history-bvb.csv` for `81836`, and `away-history-fcb.csv` and `recent-history-fcb.csv` for `81852`. The retained map keeps older identities that have rolled out of the current window. Runtime never fetches this provider or fuzzy-matches reviewed provider/Kicktipp naming differences.

Contains information from OpenLigaDB, which is made available under the Open Database License (ODbL) 1.0. The four raw OpenLigaDB response files and the OpenLigaDB-derived rows in `history-played-dates.csv` are made available under ODbL 1.0. The repository's application and tooling code remains MIT-licensed.

## UEFA match 2047743

- Scope: the single 2026 Europa League final identity absent from the pinned DuckDB artifact, repeated in two selected SC Freiburg inventory rows.
- Match-specific record: <https://www.uefa.com/uefaeuropaleague/match/2047743/>.
- Stable source revision: `UEFA-match-2047743`.
- Independent official club corroboration: <https://www.scfreiburg.com/aktuell/nachrichten/profis/spielberichte/2025-26/finalniederlage-gegen-villa>.

Only the factual match identity and played date are recorded. No UEFA page or PDF content is stored or redistributed, and runtime never fetches either site. A linked match-specific PDF could not be byte-hashed after bounded CDN and browser attempts, so this repository does not claim a PDF revision.

## Manual corroboration

The official DFB Data Center's 2025/26 2. Bundesliga schedule and individual match pages were used only for manual factual corroboration: <https://datencenter.dfb.de/competitions/2-bundesliga/seasons/2025-2026>. No DFB content is automatically extracted or redistributed. The DFB portal terms restrict automated scraping and non-private reuse: <https://www.dfb.de/nutzungsbedingungen>.

## Manual weekly refresh procedure

The standing [maintenance issue](https://github.com/ehonda/KicktippAi/issues/101) is the sole weekly audit record. Next review: `2026-W37`.

1. Triage the latest natural production summary. A zero-proxy result needs only the issue's current-week result marker; a proxy warning starts this procedure immediately. Never use this procedure to generate predictions, collect bonus context, or write production context.
2. With the `pes-squad` credential profile, run the read-only ordered current-matchday export required to cover all 54 selected documents (the current `3,4,2` order is a checkpoint, not runtime authority). Record its document count, raw CSV hash, and capture time; the export alone is not source evidence.

   ```powershell
   $historyRefreshStamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')
   $historyRefreshRoot = Join-Path (Resolve-Path '.tmp') "buli2627-history-refresh/$historyRefreshStamp"
   New-Item -ItemType Directory -Path $historyRefreshRoot -Force | Out-Null
   $historyInventory = Join-Path $historyRefreshRoot 'history-played-dates-inventory.csv'
   dotnet run --no-build --no-restore --project src/Orchestrator --configuration Release -- bundesliga-history export-inventory --community-context pes-squad --competition bundesliga-2026-27 --from-kicktipp --matchdays 3,4,2 --output $historyInventory
   $historyNames = @(Import-Csv -LiteralPath $historyInventory | Select-Object -ExpandProperty Document_Name | Sort-Object -Unique)
   if ($historyNames.Count -ne 54) { throw "Expected 54 selected history documents; found $($historyNames.Count). Choose a sufficient current ordered matchday set." }
   (Get-FileHash -LiteralPath $historyInventory -Algorithm SHA256).Hash.ToLowerInvariant()
   ```
3. A maintainer may manually fetch the required OpenLigaDB DFB response, preserve its raw bytes, and record its hash. Provider failure, identity drift, malformed data, or an exact conflict is fatal; there is no cron, runtime fetch, or automatic retry.
4. Join only exact competition/team/score identities, retain accumulated reviewed rows, and update the map, raw-source hash, and provenance together in review. Do not fuzzy-match, infer a date, or promote a collection-date proxy. Run the focused history checks and a dry run before closing the weekly issue comment.

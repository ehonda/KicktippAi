# ADR-0041: Freeze the completed DFB-Pokal first-round history transition

- Status: Accepted
- Date: 2026-08-25

## Context

The P0-20 non-dry development collection remained fail closed when completed
2026/27 DFB-Pokal first-round results entered the selected Kicktipp history
windows after ADR-0035's first live checkpoint. The current-matchday collector
reported 20 unresolved selected rows representing 13 fixtures. That diagnostic
covered only the home/away roles exposed by the current fixture set; it was not
the complete 54-document inventory required by ADR-0032.

An authenticated read-only export for exact matchdays 1 and 2 returned all 54
selected documents, 430 completed rows, and two excluded incomplete rows. Its
31,580 bytes have SHA-256
`77324da1af184b7f566a743783a3b51b1b00e7d6929e6a85e298f9648cb080b1`.
An exact identity diff against the accepted 400-row map found 30 new
occurrences representing 15 fixtures. Every prior accepted identity remained
present; 210 prior rows shifted intact from ordinals 1-7 to 2-8 in the 30
affected away/recent documents.

OpenLigaDB's public `dfb/2026` response now contains the completed first-round
evidence needed by that inventory. It also contains completed matches outside
the exact 18-team selected-history scope and two future fixtures, so response
membership alone cannot authorize a map row.

## Decision

Replace `data/bundesliga-2026-27/history/sources/openligadb-dfb-2026.json`
with the exact response captured at `2026-08-25T01:45:04+02:00` from
`https://api.openligadb.de/getmatchdata/dfb/2026`. The immutable response is
74,988 bytes with SHA-256
`92ca6f8c7175970db15bbdcea15cb79f3f2e83cb52a59300cfcf9591760affa2`.
Runtime never fetches the endpoint.

Snapshot validation requires exactly 32 positive unique IDs, `dfb` and season
`2026` on every record, exact consistent local/UTC datetimes, nonblank teams,
and exactly this completed-ID set:

`{81832, 81833, 81834, 81835, 81837, 81838, 81839, 81840, 81841, 81842, 81843, 81844, 81845, 81846, 81847, 81848, 81849, 81850, 81851, 81853, 81854, 81855, 81856, 81857, 81858, 81859, 81860, 81861, 81862, 81863}`.

IDs `81836` and `81852` remain incomplete and are not date evidence. Every
completed match has exactly one result with `resultTypeID == 2`. Selected match
`81843` additionally requires its exact after-extra-time result
`resultTypeID == 4`, `2:5`, because Kicktipp records `2:5` with annotation
`nach Verlängerung`; its exact result-type-2 record is `2:2`.
Match `81832` retains the exact halftime `0:10` and full-time `0:11` contract
from ADR-0035.

The complete current inventory authorizes exactly these source matches and
document occurrences. Each ID occurs once in the named `away-history` document
and once in the named `recent-history` document, and nowhere else:

| ID | Played date | Exact Kicktipp row identity | Documents |
|---:|---|---|---|
| 81832 | 2026-08-21 | SC St. Tönis 0:11 Eintracht Frankfurt | `away-history-sge.csv`, `recent-history-sge.csv` |
| 81833 | 2026-08-22 | Erzgebirge Aue 0:4 1899 Hoffenheim | `away-history-tsg.csv`, `recent-history-tsg.csv` |
| 81834 | 2026-08-23 | Eintracht Braunschweig 2:4 1. FC Union Berlin | `away-history-fcu.csv`, `recent-history-fcu.csv` |
| 81835 | 2026-08-22 | Eintracht Trier 0:6 RB Leipzig | `away-history-rbl.csv`, `recent-history-rbl.csv` |
| 81837 | 2026-08-23 | TSV Schott Mainz 0:5 Bor. Mönchengladbach | `away-history-bmg.csv`, `recent-history-bmg.csv` |
| 81838 | 2026-08-23 | Fortuna Düsseldorf 1:5 SC Freiburg | `away-history-scf.csv`, `recent-history-scf.csv` |
| 81842 | 2026-08-22 | SV Wehen Wiesbaden 0:4 Bayer 04 Leverkusen | `away-history-b04.csv`, `recent-history-b04.csv` |
| 81843 | 2026-08-24 | Hallescher FC 2:5 FC Schalke 04 (`nach Verlängerung`) | `away-history-s04.csv`, `recent-history-s04.csv` |
| 81844 | 2026-08-22 | Energie Cottbus 0:2 FC Augsburg | `away-history-fca.csv`, `recent-history-fca.csv` |
| 81845 | 2026-08-23 | VfB Krieschow 0:9 FSV Mainz 05 | `away-history-m05.csv`, `recent-history-m05.csv` |
| 81851 | 2026-08-22 | MSV Duisburg 1:3 SV Elversberg | `away-history-sve.csv`, `recent-history-sve.csv` |
| 81853 | 2026-08-22 | Lüneburger SK Hansa 0:3 Werder Bremen | `away-history-svw.csv`, `recent-history-svw.csv` |
| 81854 | 2026-08-24 | SC Verl 0:3 Hamburger SV | `away-history-hsv.csv`, `recent-history-hsv.csv` |
| 81855 | 2026-08-21 | FC Hansa Rostock 0:4 VfB Stuttgart | `away-history-vfb.csv`, `recent-history-vfb.csv` |
| 81861 | 2026-08-23 | 1. FC Phönix Lübeck 2:4 SC Paderborn 07 | `away-history-scp.csv`, `recent-history-scp.csv` |
| 81863 | 2026-08-24 | FC Würzburger Kickers 1:2 1. FC Köln | `away-history-fck.csv`, `recent-history-fck.csv` |

The reviewed provider-to-Kicktipp joins preserve exact source names while
binding the accepted map identities. The differing provider forms are
`TSG Hoffenheim`, `Borussia Mönchengladbach`, `VfB 1921 Krieschow `,
`1. FSV Mainz 05`, `SV 07 Elversberg`, `SV Werder Bremen`, `Hansa Rostock`, and
`Würzburger Kickers`. No fuzzy runtime aliasing is added.

All 32 selected `dfb/2026` map occurrences use the new response revision and
`Verified_At=2026-08-25T01:46:00+02:00`. The resulting canonical map has 430
rows and 212 unique source matches. Its source split is 326 rows / 152 matches
from `transfermarkt-datasets`, 102 / 59 from OpenLigaDB, and 2 / 1 from UEFA.
The 120,593-byte map has SHA-256
`e341010b4bb0f95ff31009bf904616c825212118d4e3f80d7d2dbcb0f8732492`.
The map is ordered by exact document and current completed-row ordinal, uses
UTF-8 without BOM and CRLF with a final terminator, and retains every prior
identity and its non-DFB provenance. Head-to-head documents remain outside the
transform and unchanged.

## Alternatives considered

- **Scope the repair to the first 20 diagnostics / 13 fixtures:** Rejected
  because that was only a current-matchday subset and would leave ten exact
  occurrences in the mandatory 54-document inventory unresolved.
- **Add every completed OpenLigaDB match involving any broadly related club:**
  Rejected because only exact selected-document occurrences authorize map rows.
- **Retain the ADR-0035 response while adding facts from the live endpoint:**
  Rejected because those facts would lack one immutable byte revision.
- **Fetch OpenLigaDB at runtime:** Rejected because the last-known-good history
  contract remains deterministic and network independent.

## Consequences

- The exact 54-document history inventory can pass without guessing any DFB
  date or silently omitting an away/recent occurrence.
- The initial 20/13 failure remains useful evidence that the gate stopped a
  partial publication, but it no longer defines source scope.
- Future DFB completions still require a new authenticated inventory, captured
  response, hash, accepted decision, and exact map update.
- The OpenLigaDB raw response and derived rows remain under ODbL 1.0; runtime
  and tooling code remain MIT-licensed.

## Affected tasks

- [P0-20](../tasks/p0-20-seed-and-development-validation.md)
- [P0-22](../tasks/p0-22-history-played-dates.md)

## Supersedes

ADR-0035 only for the frozen `dfb/2026` byte revision, capture state,
completed-ID set, and the claim that match `81832` is the only selected
current-season DFB identity. ADR-0035's exact-source, no-runtime-fetch,
rolling-window, fail-closed, no-H2H-rewrite, and atomic-publication decisions
remain in force.

# FIFA Ranking Refresh Research

This note records the source inspection done while investigating how to refresh the WM26 FIFA ranking CSVs without manual browser copy/paste. It is intentionally detailed so the full updater can be planned without rediscovering the FIFA page behavior.

## Current Repository Shape

The checked-in ranking source files live in this directory:

- `fifa-rankings.csv`: aggregate KPI document source for WM26 bonus predictions.
- `fifa-ranking-{team-slug}.csv`: one-row match-context documents for individual teams.

Both formats currently use:

```csv
team,rank,ELO
Deutschland,10,1730.37
```

`ELO` is a historical column name in this project. It stores FIFA men's ranking points, not Elo ratings. The current files were generated from the FIFA men's ranking published on 1 April 2026.

The generated upload artifact for the aggregate KPI document is:

```text
kpi-documents/output/ehonda-dev-wm26/fifa-rankings.json
```

## Page Behavior

The human-facing page is:

```text
https://inside.fifa.com/de/fifa-world-ranking/men
```

The visible table is a Next.js client-rendered table. The initial view is collapsed to the first ten rows, but that collapse is presentation only and should not drive the updater design.

Important observations from the page source:

- The HTML contains a `__NEXT_DATA__` JSON script.
- The page route is `/fifa-world-ranking/[...country]`.
- On 24 May 2026 the inspected build ID was `tbRuXrlMPiLrMNzn_MM3V`; do not depend on this exact build ID.
- `pageProps.pageData.ranking.useNewLiveRankingTable` was `true`.
- `pageProps.pageData.ranking.lastUpdateDate` was `2026-04-01T11:55:29.435Z`.
- `pageProps.pageData.ranking.nextUpdateDate` was `2026-06-11T00:00:00Z`.
- The page metadata exposed a current date item with `id` `FRS_Male_Football_20260119`, `iso` `2026-04-01T11:55:29.435Z`, and `matchWindowEndDate` `2026-04-01`.

The full ranking rows are not reliable to scrape from the static HTML. The client bundle loads the `HubRankingTable` component and fetches ranking data from FIFA's public FDCP API.

## Public API Endpoints

Use the `fifarankings` API namespace, not the legacy generic schedule endpoint.

Schedule discovery:

```text
https://api.fifa.com/api/v3/fifarankings/rankingschedules/all?type=0&gender=1&sportType=0&language=de
```

Ranking rows for a selected schedule:

```text
https://api.fifa.com/api/v3/fifarankings/rankings/rankingsbyschedule?rankingScheduleId={IdRankingSchedule}&count=300&language=de
```

Parameters used here:

- `type=0`: normal FIFA ranking schedule.
- `gender=1`: men's rankings.
- `sportType=0`: football.
- `language=de`: localized names matching the current WM26 German documents.
- `count=300`: safely above the current full men's table size. The inspected response returned 211 rows.

The client bundle contained the same ranking row path in its FDCP wrapper:

```text
/fifarankings/rankings/rankingsbyschedule?rankingScheduleId=...
```

Avoid this endpoint for the updater:

```text
https://api.fifa.com/api/v3/rankingschedules/all?type=0&gender=1&language=de
```

It returned legacy IDs such as `id15065`. Passing `id15065` to the `fifarankings/rankings/rankingsbyschedule` endpoint failed with a server-side null-reference error. The full updater should use `fifarankings/rankingschedules/all` so the schedule IDs match the ranking row endpoint.

## Latest Schedule Selection

Do not choose the first schedule blindly and do not infer publication date from the schedule ID suffix.

On 24 May 2026, the schedule endpoint started with:

| IdRankingSchedule | RankingApproved | PublicationDateUTC |
| --- | --- | --- |
| `FRS_Male_Football_20260401` | `false` | null |
| `FRS_Male_Football_20260119` | `true` | `2026-04-01T11:55:29.435Z` |
| `FRS_Male_Football_20251219` | `true` | `2026-01-19T17:11:57.976Z` |

The correct current schedule at that time was `FRS_Male_Football_20260119`, even though the unapproved placeholder appeared before it. The updater should:

1. Fetch the schedule endpoint.
2. Keep only rows with `RankingApproved == true`.
3. Keep only rows with a non-empty `PublicationDateUTC`.
4. Sort by `PublicationDateUTC` descending.
5. Use the first row's `IdRankingSchedule`.

PowerShell sketch:

```powershell
$schedules = Invoke-RestMethod 'https://api.fifa.com/api/v3/fifarankings/rankingschedules/all?type=0&gender=1&sportType=0&language=de'
$latest = $schedules.Results |
    Where-Object { $_.RankingApproved -eq $true -and $_.PublicationDateUTC } |
    Sort-Object { [datetime]$_.PublicationDateUTC } -Descending |
    Select-Object -First 1

$rankings = Invoke-RestMethod "https://api.fifa.com/api/v3/fifarankings/rankings/rankingsbyschedule?rankingScheduleId=$($latest.IdRankingSchedule)&count=300&language=de"
```

## Ranking Response Shape

The tested row endpoint:

```text
https://api.fifa.com/api/v3/fifarankings/rankings/rankingsbyschedule?rankingScheduleId=FRS_Male_Football_20260119&count=300&language=de
```

returned HTTP 200 with JSON and 211 `Results`.

Representative fields:

```json
{
  "IdTeam": "43946",
  "TeamName": [
    {
      "Locale": "de-DE",
      "Description": "Frankreich"
    }
  ],
  "Gender": 1,
  "IdConfederation": "27275",
  "RankingMovement": 2,
  "ConfederationName": "UEFA",
  "IdCountry": "FRA",
  "RatedMatches": 56,
  "Rank": 1,
  "PrevRank": 3,
  "TotalPoints": 1877.322731,
  "PrevPoints": 1869.998606,
  "RankingStatus": 0
}
```

Fields needed for the current CSVs:

- `TeamName[0].Description`: localized display name. Use only after applying project-specific overrides.
- `IdCountry`: stable FIFA country code. Use this as the lookup key.
- `Rank`: FIFA rank.
- `TotalPoints`: ranking points. Round to two decimals using invariant culture and a dot decimal separator.

The first rows matched the checked-in aggregate CSV:

| Team | Rank | Rounded TotalPoints |
| --- | ---: | ---: |
| Frankreich | 1 | 1877.32 |
| Spanien | 2 | 1876.40 |
| Argentinien | 3 | 1874.81 |
| England | 4 | 1825.97 |
| Portugal | 5 | 1763.83 |
| Brasilien | 6 | 1761.16 |
| Niederlande | 7 | 1757.87 |
| Marokko | 8 | 1755.87 |
| Belgien | 9 | 1734.71 |
| Deutschland | 10 | 1730.37 |

## Naming and Matching Gotchas

Do not match teams only by display name. The current checked-in CSV uses some preferred names that differ from FIFA's German API labels.

Known differences from the 1 April 2026 data:

| IdCountry | FIFA API label | Current project label |
| --- | --- | --- |
| `IRN` | `IR Iran` | `Iran` |
| `KOR` | `Republik Korea` | `Südkorea` |
| `CZE` | `Tschechische Republik` | `Tschechien` |
| `KSA` | `Saudiarabien` | `Saudi-Arabien` |
| `BIH` | `Bosnien und Herzegowina` | `Bosnien-Herzegowina` |

The full updater should carry an explicit WM26 team mapping keyed by `IdCountry`, with at least:

- FIFA `IdCountry`.
- Project display name for CSV content.
- Project file slug for `fifa-ranking-{team-slug}.csv`.

The current ranking CSVs do not store `IdCountry`, so the updater should not rely on round-tripping existing CSV rows by name alone.

## Proposed Refresh Algorithm

For the planned full workflow:

1. Load a WM26 team mapping keyed by FIFA `IdCountry`.
2. Fetch the latest approved men's football schedule from the `fifarankings/rankingschedules/all` endpoint.
3. Fetch the full ranking rows from `fifarankings/rankings/rankingsbyschedule`.
4. Build a dictionary by `IdCountry`.
5. For every mapped WM26 team, require a ranking row. Fail clearly if any team is missing.
6. Write `docs/onboarding-wm26/fifa-rankings.csv` with `team,rank,ELO`, ordered by rank unless the future workflow chooses a different canonical order.
7. Write each `docs/onboarding-wm26/fifa-ranking-{team-slug}.csv` with the same header and one row.
8. Regenerate `kpi-documents/output/ehonda-dev-wm26/fifa-rankings.json` from the aggregate CSV if the workflow owns KPI upload artifacts.
9. Record the schedule ID and `PublicationDateUTC` in the command output so a reviewer can verify which official ranking was used.

Recommended validation:

- Assert the ranking response has at least 200 rows.
- Assert the selected schedule is approved and has a publication timestamp.
- Assert all mapped teams resolve by `IdCountry`.
- Assert all generated points have exactly two decimals and use `.` as the decimal separator.
- Compare the top ten against the source response during tests or dry-run output.

## Open Implementation Questions

- Where should the explicit WM26 `IdCountry` to project-name/slug mapping live: code, JSON, or CSV?
- Should the generated aggregate file include all 211 FIFA teams or only the current WM26 team set? The current checked-in aggregate has 48 rows.
- Should KPI JSON regeneration and Firebase upload be part of the same command, or a separate explicit step?
- Should the updater keep historical ranking snapshots, or only replace the current files?

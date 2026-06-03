# WM26 Lineup Source Status

Status as of 2026-06-03.

The current WM26 lineup seed at `data/wm26/lineups/lineups-seed.csv` uses FIFA's official final 26-player squad-list source. It has 1,296 rows: 48 coach rows and 1,248 player rows across all 48 teams in `data/wm26/lineups/wm26-teams.csv`.

The supplemental database is the CC0 `dcaribou/transfermarkt-datasets` DuckDB snapshot. `collect-context lineups` downloads the upstream snapshot into `data/wm26/lineups/private/data/transfermarkt-datasets.duckdb` by default; keep local DuckDB files ignored.

`collect-context lineups` uses the tracked participant manifest `data/wm26/lineups/wm26-teams.csv` during generation. The current final seed has source rows for every manifest team, so generated WM26 lineup context should not contain header-only per-team documents.

Generated prompt context must use:

```csv
Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR
```

Source status is documentation-only. `collect-context lineups` has no `--status` flag, and provisional vs official state is not included in the CSV content shown to the model.

## Official Final Source

FIFA published the confirmed 2026 final squad lists on 2026-06-03 after the team submission deadline on 2026-06-02. The tracked seed was refreshed from the official FIFA final squad-list PDF, not from preliminary squad articles.

Official sources:

- FIFA confirmed squads announcement: https://www.fifa.com/en/articles/fifa-world-cup-2026-squads-confirmed
- FIFA final squad-list PDF: https://fdp.fifa.org/assetspublic/ce281/pdf/SquadLists-English.pdf
- FIFA squad timing: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/squad-lists-number-date

The FIFA team `/squad` page shape was checked first. The page exposes an `EntireSquad` `entryEndpoint`, but the fetched data endpoint still returned labels and page metadata rather than roster player records, so the official PDF was used as the membership source.

## Current Final Source State

| Source state | Teams |
| --- | --- |
| Official FIFA final squad rows included | Algeria, Argentina, Australia, Austria, Belgium, Bosnia-Herzegovina, Brazil, Cabo Verde, Canada, Colombia, Congo DR, Côte d'Ivoire, Croatia, Curaçao, Czechia, Ecuador, Egypt, England, France, Germany, Ghana, Haiti, IR Iran, Iraq, Japan, Jordan, Korea Republic, Mexico, Morocco, Netherlands, New Zealand, Norway, Panama, Paraguay, Portugal, Qatar, Saudi Arabia, Scotland, Senegal, South Africa, Spain, Sweden, Switzerland, Tunisia, Türkiye, Uruguay, USA, Uzbekistan |
| Header-only generated docs | None |

The local Transfermarkt snapshot does not currently include national-team rows for Curaçao, Congo DR, Côte d'Ivoire, Haiti, or Cabo Verde. Their official FIFA roster membership is still present in the seed; unresolved supplemental fields are expected to remain `N/A` after context generation unless future DuckDB snapshots add those teams or player rows.

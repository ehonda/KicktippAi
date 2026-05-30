# WM26 Lineup Source Status

Status as of 2026-05-30.

The current `ehonda-dev-wm26` lineup seed is an ignored private provisional seed at `.agents/skills/wm26-lineups/private/input/lineups-seed.csv`. It contains the public FIFA squad-tracker roster rows that were available on 2026-05-30, not only the first matchday teams.

The local supplemental database used for verification is `.agents/skills/wm26-lineups/private/data/transfermarkt-datasets.duckdb`, downloaded from `dcaribou/transfermarkt-datasets`. Keep it ignored and refresh it locally when the upstream dataset changes.

`$wm26-lineups` now uses the tracked participant manifest `.agents/skills/wm26-lineups/references/wm26-teams.csv` during generation. The manifest makes the generator emit all 48 per-team `lineup-*` context payloads. Teams without usable provisional or official squad rows get header-only CSV content for now, so WM26 `matchday`/`matchday-dev` context checks do not fail because a lineup document is absent.

Generated prompt context must use:

```csv
Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR
```

The `$wm26-lineups` `--status provisional|official` flag is workflow metadata only. It is used in payload descriptions and console output, not in the CSV content shown to the model.

## Official Squad Timing

FIFA says all squads are provisional until the final 26-player lists are announced by FIFA after team submission on 2026-06-02. Until then, use `--status provisional`. Once FIFA publishes the final lists, rebuild full-squad lineup documents for every WM26 team and upload/copy them with `--status official`.

Primary trackers:

- FIFA squad timing: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/squad-lists-number-date
- FIFA squad announcement tracker: https://www.fifa.com/en/articles/all-world-cup-squad-announcements

## Current Provisional Source State

The regenerated private seed has 1,181 rows: 42 coach rows and 1,139 player rows across 42 teams. It accepts narrow preliminary lists above 26 players when FIFA has published them. Croatia's standby list and Côte d'Ivoire's reserves were excluded. Sweden's FIFA article says 26 players but listed 25 names at collection time, so the seed keeps the 25 listed names.

| Source state | Teams |
| --- | --- |
| FIFA squad rows included | Argentina, Austria, Belgium, Bosnia-Herzegovina, Brazil, Cabo Verde, Canada, Colombia, Congo DR, Côte d'Ivoire, Croatia, Curaçao, Czechia, Egypt, England, France, Germany, Ghana, Haiti, IR Iran, Iraq, Japan, Jordan, Korea Republic, Morocco, Netherlands, New Zealand, Norway, Panama, Portugal, Qatar, Saudi Arabia, Scotland, Senegal, South Africa, Spain, Sweden, Switzerland, Tunisia, Türkiye, USA, Uzbekistan |
| Header-only generated docs | Algeria, Australia, Ecuador, Mexico, Paraguay, Uruguay |

Useful direct source pages from this pass:

- FIFA squad tracker: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/all-world-cup-squad-announcements
- FIFA squad timing: https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/squad-lists-number-date
- Australia train-on squad: https://www.fifa.com/en/tournaments/mens/worldcup/articles/australia-name-train-on-squad
- Mexico 55-player prelist: https://www.fifa.com/es/tournaments/mens/worldcup/canadamexicousa2026/articles/mexico-prelista-55-jugadores-mundial-2026

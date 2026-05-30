---
name: wm26-lineups
description: Generate, enrich, upload, or copy FIFA World Cup 2026 lineup context documents for KicktippAi. Use when Codex needs to create provisional or official WM26 lineup CSV/JSON artifacts, enrich FIFA or provisional lineup seed rows from the CC0 dcaribou Transfermarkt DuckDB dataset, upload Firestore lineup-* context documents, upload the aggregate lineups KPI document, copy lineup Firestore documents between communities, or validate lineup context in matchday-dev/bonus-dev traces.
---

# WM26 Lineups

## Guardrails

- Keep source notes, seed CSVs, local DuckDB files, and generated artifacts under ignored paths such as `.agents/skills/wm26-lineups/private/` or `.agents/skills/wm26-lineups/artifacts/`.
- Do not commit source CSVs, downloaded DuckDB databases, generated JSON payloads, or source notes.
- Use official FIFA final squad/lineup material once available. Until then, use provisional squad material.
- FIFA final squad lists are expected on 2 June 2026, when FIFA announces the submitted final 26-player lists. Once available, regenerate full-squad context with `--status official`; do not reduce context to match starters only.
- Use `dcaribou/transfermarkt-datasets` as the only supplemental source for coach, age, position, and market-value data. Use its CC0 DuckDB database artifact; do not scrape websites for supplemental values.
- Always include coaches as `Role=Coach`.
- Use only this output CSV schema:
  `Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR`
- Do not include source URLs, Transfermarkt IDs, or helper columns in generated context/KPI CSV content.
- Generate per-team lineup payloads for every WM26 participant in `references/wm26-teams.csv`. If no usable provisional or official squad is available for a team, emit a header-only `lineup-{team}.csv` context payload rather than omitting the document.
- Render CSV content with exactly one header row, one record per line, CRLF line endings, and a final trailing line ending.
- Render player `Market_Value_EUR` values with dot thousands separators, for example `15.000.000`.
- Use `N/A` for unavailable player age, position, or market values, never `0` for market values; leave coach market values empty.
- `Role` must be exactly `Player` or `Coach`.
- After every run, print exactly one clear final status line:
  `Lineup source status: provisional`
  or
  `Lineup source status: official`
- Run `dotnet run ...` and `git ...` commands outside the sandbox when using Codex in this repository.

## Enrich Mode

Use this when FIFA/provisional lineup membership needs supplemental values from `dcaribou/transfermarkt-datasets`.

1. Download the upstream DuckDB database to an ignored path, for example:
   `.agents/skills/wm26-lineups/private/data/transfermarkt-datasets.duckdb`

   Upstream references:
   - Repository: `https://github.com/dcaribou/transfermarkt-datasets`
   - Kaggle mirror: `https://www.kaggle.com/datasets/davidcariboo/player-scores`
   - Recommended database URL from upstream README: `https://pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb`

2. Prepare a seed CSV with FIFA/provisional membership:
   `Team_Slug,Team,Data_Collected_At,Role,Name,Transfermarkt_National_Team_Id,Transfermarkt_Player_Id`

   The seed may also include optional `Age`, `Position`, and `Market_Value_EUR` columns. Prefer `Transfermarkt_Player_Id` for each player. If it is blank, the enrichment script matches by normalized `Name` within `current_national_team_id`.

   Use `--allow-missing-players` for provisional FIFA/federation roster rows that cannot be resolved in the local DuckDB snapshot. The row is kept with `N/A` supplemental values where needed, and the generated report calls out the missing fields.

3. Enrich the seed CSV:

   ```powershell
   uv --cache-dir .uv-cache run python .agents\skills\wm26-lineups\scripts\enrich_lineup_source.py `
     --input .agents\skills\wm26-lineups\private\input\lineups-seed.csv `
     --duckdb .agents\skills\wm26-lineups\private\data\transfermarkt-datasets.duckdb `
     --output .agents\skills\wm26-lineups\private\input\lineups.csv `
     --status provisional `
     --allow-missing-players
   ```

4. Review all failures. Add explicit `Transfermarkt_Player_Id` values to the seed CSV instead of loosening name matching.

## Generate Mode

Use this when enriched lineup source material should become Firestore context.

1. Prepare or enrich a source CSV, for example:
   `.agents/skills/wm26-lineups/private/input/lineups.csv`

   The source CSV must contain the output schema plus one grouping column:
   `Team_Slug,Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR`

   `Team_Slug` becomes the Firestore context document suffix, for example `lineup-germany.csv`.

2. Generate payloads:

   ```powershell
   uv --cache-dir .uv-cache run python .agents\skills\wm26-lineups\scripts\generate_lineup_payloads.py `
     --input .agents\skills\wm26-lineups\private\input\lineups.csv `
     --community-context ehonda-dev-wm26 `
     --status provisional `
     --output-root .agents\skills\wm26-lineups\artifacts `
     --kpi-output-root kpi-documents\output `
     --teams .agents\skills\wm26-lineups\references\wm26-teams.csv
   ```

3. Upload each per-team context JSON:

   ```powershell
   Get-ChildItem .agents\skills\wm26-lineups\artifacts\context-documents\ehonda-dev-wm26\lineup-*.csv.json |
     ForEach-Object {
       dotnet run --project src\Orchestrator -- upload-context --input $_.FullName --competition fifa-world-cup-2026
     }
   ```

4. Upload the aggregate KPI document:

   ```powershell
   dotnet run --project src\Orchestrator -- upload-kpi lineups -c ehonda-dev-wm26 --competition fifa-world-cup-2026
   ```

5. Review the `Header-only lineup context payloads` and `Missing lineup source data` reports. Header-only payloads are acceptable for teams without usable provisional or official squads; fill them once a narrower public squad exists. Keep `N/A` only when the CC0 dataset has no supplemental value for an otherwise official/provisional roster row.

## Copy Mode

Use this when recent-enough lineup docs already exist in one Firestore community context and should be copied to another.

Run:

```powershell
dotnet run --project src\Orchestrator -- copy-firestore-context `
  --source-community-context ehonda-dev-wm26 `
  --target-community-context <target-community-context> `
  --competition fifa-world-cup-2026 `
  --context-prefix lineup- `
  --kpi-document lineups
```

The command copies latest `lineup-*` context docs and the `lineups` KPI doc. Treat missing source docs or missing KPI docs as hard failures.

For planning without writes, add `--dry-run`.

## Verification

After changing WM26 lineup context or onboarding behavior:

1. Execute generate or copy mode for the target community.
2. Run, outside the sandbox:

   ```powershell
   dotnet run --project src\Orchestrator -- matchday-dev -c ehonda-dev-wm26 --verbose
   dotnet run --project src\Orchestrator -- bonus-dev -c ehonda-dev-wm26 --verbose
   ```

3. Use the global `langfuse` skill and installed `langfuse` CLI to verify traces:
   - matchday traces include only the two participating teams' `lineup-*` docs.
   - bonus traces include `lineups` only for the exact question:
     `Welche Mannschaft stellt den Spieler mit den meisten Toren?`
   - hosted prompt fallback is false and the WM26 default reasoning effort is present.

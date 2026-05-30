# WM26 Lineup Source Process

Use official FIFA final squad material once available. Until then, use provisional squad material from national federations or other reputable non-scraped sources. Use `dcaribou/transfermarkt-datasets` as the only supplemental source for coach, age, position, and market-value data. The regular `collect-context lineups` command downloads the CC0 DuckDB snapshot into an ignored cache by default.

Recommended private source workflow:

1. Prepare or update the tracked seed CSV with FIFA/provisional membership and, where possible, explicit Transfermarkt IDs:
   `Team_Slug,Team,Data_Collected_At,Role,Name,Transfermarkt_National_Team_Id,Transfermarkt_Player_Id`.
   Optional `Age`, `Position`, and `Market_Value_EUR` columns may be included. The tracked seed lives at `data/wm26/lineups/lineups-seed.csv`.
   The repeatable FIFA tracker JSON extraction and seed-generation details are documented in `fifa-squad-seed-generation.md`.
2. Keep deterministic team slugs that match KicktippAi context document naming and `data/wm26/lineups/wm26-teams.csv`.
3. Run `collect-context lineups`. The command fills `Age`, `Position`, and `Market_Value_EUR` from the DuckDB `players` and `national_teams` tables, preserves unresolved roster rows with `N/A`, emits all manifest teams, and uploads Firestore context/KPI documents.
4. Review the `Header-only lineup context payloads` and `Missing lineup source data` reports. Keep `N/A` only when the upstream dataset lacks a supplemental value; do not use `0` for unknown market values.
5. Replace the tracked seed with official full FIFA final squads once available around 2026-06-02, then run `collect-context lineups` again. This is a seed update task, not part of regular scheduled context collection.

Generated lineup CSV context must use exactly:
`Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR`.

Generated CSV context must match the rendering style of the FIFA ranking docs: the header is the first line, the first data row starts on the next line, every row has its own line, and the content ends with a final trailing line ending so Langfuse prompt views do not visually merge adjacent content.

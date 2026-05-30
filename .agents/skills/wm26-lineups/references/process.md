# WM26 Lineup Source Process

Use official FIFA final squad material once available. Until then, use provisional squad material from national federations or other reputable non-scraped sources. Use `dcaribou/transfermarkt-datasets` as the only supplemental source for coach, age, position, and market-value data. The recommended data artifact is the CC0 DuckDB database published by that project; keep the local `.duckdb` file under this skill's ignored `private/` tree.

Recommended private source workflow:

1. Download the upstream DuckDB database to an ignored path such as `.agents/skills/wm26-lineups/private/data/transfermarkt-datasets.duckdb`.
2. Prepare a seed CSV with FIFA/provisional membership and, where possible, explicit Transfermarkt IDs:
   `Team_Slug,Team,Data_Collected_At,Role,Name,Transfermarkt_National_Team_Id,Transfermarkt_Player_Id`.
   Optional `Age`, `Position`, and `Market_Value_EUR` columns may be included. For provisional roster rows that cannot be resolved in the DuckDB snapshot, run with `--allow-missing-players` so the row is kept with `N/A` supplemental values.
   The repeatable FIFA tracker JSON extraction and seed-generation details are documented in `fifa-squad-seed-generation.md`.
3. Run `scripts/enrich_lineup_source.py` to fill `Age`, `Position`, and `Market_Value_EUR` from the DuckDB `players` and `national_teams` tables.
4. Review any missing or ambiguous player matches. Prefer adding `Transfermarkt_Player_Id` to the seed CSV over loosening name matching.
5. Use deterministic team slugs that match KicktippAi context document naming and `references/wm26-teams.csv`.
6. Run generate mode with `--status provisional` until official squads are published.
7. Run generate mode with the `references/wm26-teams.csv` manifest. Teams without usable squad rows must still get header-only `lineup-*` payloads so WM26 matchday context checks do not fail because a document is absent.
8. Read the `Header-only lineup context payloads` and `Missing lineup source data` reports. Keep `N/A` only when the upstream dataset lacks a supplemental value; do not use `0` for unknown market values.
9. Regenerate and upload with `--status official` after official FIFA final squads are available.

Generated lineup CSV context must use exactly:
`Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR`.

Generated CSV context must match the rendering style of the FIFA ranking docs: the header is the first line, the first data row starts on the next line, every row has its own line, and the content ends with a final trailing line ending so Langfuse prompt views do not visually merge adjacent content.

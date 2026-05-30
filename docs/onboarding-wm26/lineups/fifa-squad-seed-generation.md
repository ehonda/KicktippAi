# FIFA Squad Seed Generation

This note documents the repeatable process used on 2026-05-30 to generate the WM26 preliminary lineup seed. Use the same extraction process once FIFA publishes the final squad lists, then replace the tracked seed with official full-squad membership.

The output of this process is the tracked seed CSV:

```text
data/wm26/lineups/lineups-seed.csv
```

Do not commit source JSON, downloaded DuckDB files, generated payloads, or private source notes. Commit the reviewed seed CSV and tracked reference data such as `data/wm26/lineups/wm26-teams.csv`.

## Inputs

- Tracked WM26 participant manifest: `data/wm26/lineups/wm26-teams.csv`
- FIFA squad tracker page:
  `https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/all-world-cup-squad-announcements`
- Local Transfermarkt DuckDB snapshot, when running offline:
  `data/wm26/lineups/private/data/transfermarkt-datasets.duckdb`
- Optional previous seed, for coach names, national-team IDs, player IDs, and spelling fixes.

## FIFA JSON Endpoints

FIFA's public website is a single-page app backed by JSON endpoints. Fetching those endpoints is more reliable than scraping rendered HTML.

Use this base URL:

```text
https://cxm-api.fifa.com/fifaplusweb/api
```

For a FIFA page URL such as:

```text
https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/canada-squad-announcement
```

fetch the page JSON by removing the leading `/en` path segment and prefixing `/pages/en`:

```text
https://cxm-api.fifa.com/fifaplusweb/api/pages/en/tournaments/mens/worldcup/canadamexicousa2026/articles/canada-squad-announcement
```

The page JSON contains `sections[0].entryEndpoint`, usually like:

```text
/sections/article/<entryId>?locale=en
```

Fetch that endpoint from the same base URL to get article rich text containing the roster.

## Tracker Extraction

1. Fetch the tracker page JSON:

   ```text
   https://cxm-api.fifa.com/fifaplusweb/api/pages/en/tournaments/mens/worldcup/canadamexicousa2026/articles/all-world-cup-squad-announcements
   ```

2. Fetch the tracker article section from `sections[0].entryEndpoint`.

3. Walk `richtext.content` in order.

4. Treat heading nodes as the current FIFA team label.

5. In following paragraph nodes, collect hyperlinks whose link text contains `Full` and `squad announcement`.

6. Normalize relative or bare FIFA URLs:
   - `fifa.com/en/articles/...` -> `https://www.fifa.com/en/articles/...`
   - `https://www.fifa.com/en/...` -> page JSON endpoint using `/pages/en/...`

7. Keep only teams that map to `data/wm26/lineups/wm26-teams.csv`. Teams not in the tracker, teams with only too-broad prelists, and teams with train-on groups remain absent from the seed; `collect-context lineups` will create header-only context docs from the manifest.

## Roster Extraction

For each squad announcement article:

1. Fetch the page JSON and then the article section JSON.

2. Walk `richtext.content` in order.

3. Start collecting roster rows when a heading matches a position group:
   - `Goalkeeper` or `Goalkeepers`
   - `Defender` or `Defenders`
   - `Midfielder` or `Midfielders`
   - `Forward`, `Forwards`, `Attacker`, `Attackers`, `Striker`, or `Strikers`

4. For paragraph nodes after a position heading, split player names on line breaks. FIFA often stores names as newline-separated text inside one paragraph.

5. Strip trailing club annotations in parentheses, for example `Son Heungmin (LAFC)` -> `Son Heungmin`.

6. Ignore promotional or CTA headings inserted between roster groups, such as hospitality or ticket headings, unless they introduce `Standby list`, `Reserves`, fixtures, or other non-roster content.

7. Stop collecting when reaching:
   - the standard provisional-squad disclaimer,
   - a fixtures/group heading,
   - `Standby list`,
   - `Reserves`,
   - or any clearly non-roster section after the position groups.

8. Review low or unusual player counts manually. In the 2026-05-30 preliminary run:
   - Croatia's `Standby list` was excluded.
   - Cote d'Ivoire's `Reserves` were excluded.
   - Sweden's article said 26 players but listed 25 names; the seed kept the 25 listed names.
   - Haiti had a promotional heading between midfielders and forwards; forwards still belonged to the roster.

## Seed Row Rules

The seed CSV must have these columns:

```csv
Team_Slug,Team,Data_Collected_At,Role,Name,Transfermarkt_National_Team_Id,Transfermarkt_Player_Id
```

The seed may also include:

```csv
Age,Position,Market_Value_EUR
```

Recommended row rules:

- Order teams by `data/wm26/lineups/wm26-teams.csv`.
- Use the tracked German project team name from the manifest in `Team`.
- Use the collection date in `Data_Collected_At`.
- Add one coach row per team with `Role=Coach`.
- Add one player row per FIFA-listed player with `Role=Player`.
- Use `Position` from the FIFA roster group when the row is otherwise unresolved in DuckDB.
- Prefer explicit `Transfermarkt_Player_Id` for every player that can be confidently resolved.
- Leave `Transfermarkt_Player_Id` blank when unresolved and run enrichment with `--allow-missing-players`.
- Use `N/A` only for genuinely unavailable optional supplemental values.
- Never use `0` for unknown market value.

## Transfermarkt Resolution

Use the local DuckDB snapshot only for supplemental values and IDs. Do not scrape Transfermarkt or other websites for age, position, or market value.

Useful lookups:

```sql
select
  national_team_id,
  name,
  country_name,
  coach_name
from national_teams
order by name;
```

```sql
select
  player_id,
  name,
  date_of_birth,
  position,
  market_value_in_eur,
  current_national_team_id
from players
where cast(current_national_team_id as varchar) = '<national_team_id>'
order by name;
```

Name matching should follow the enrichment script's normalization style: remove accents, lower-case, and collapse non-alphanumeric characters. Prefer adding explicit player IDs over broadening matching rules.

Common manual fixes are spelling and transliteration differences, for example:

- `Neymar Junior` -> `Neymar`
- `Rodrigo Hernandez` -> `Rodri`
- `Pablo Paez 'Gavi'` -> `Gavi`
- `Ben Gannon-Doak` -> `Ben Doak`
- names with missing diacritics such as `Joao`/`João`, `Ruben`/`Rúben`, or `Leao`/`Leão`

## Enrich And Generate

After updating the tracked seed, run the native collection command:

```powershell
dotnet run --project src/Orchestrator -- collect-context lineups --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --verbose
```

For local/offline verification with an existing DuckDB snapshot:

```powershell
dotnet run --project src/Orchestrator -- collect-context lineups --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --duckdb-path data/wm26/lineups/private/data/transfermarkt-datasets.duckdb --dry-run --verbose
```

Final FIFA roster membership should remain present even when the DuckDB snapshot lacks supplemental values; those fields stay as `N/A`.

Review these reports:

- `Header-only lineup context payloads`: should shrink as usable squad lists become available and should be zero after all final squads are processed.
- `Missing lineup source data`: expected when DuckDB lacks a supplemental value; fix easy ID/spelling misses, then keep true gaps as `N/A`.
- Source state in `docs/onboarding-wm26/lineup-source-status.md` must match the tracked seed.

## Final Squad Refresh Checklist

1. Refresh or verify the local DuckDB snapshot, or let `collect-context lineups` download it.
2. Re-run tracker extraction after FIFA publishes final lists.
3. Confirm every manifest team has a final squad article or equivalent official FIFA final-list source.
4. Regenerate `private/input/lineups-seed.csv` with final roster rows.
5. Exclude reserve, standby, replacement-watch, and training-only groups unless FIFA explicitly includes them in the final submitted squad.
6. Commit the official full-squad seed to `data/wm26/lineups/lineups-seed.csv`.
7. Run `collect-context lineups --competition fifa-world-cup-2026`.
8. Confirm the header-only report is empty.
9. Validate `matchday-dev`, `bonus-dev`, and Langfuse traces per the WM26 onboarding docs.

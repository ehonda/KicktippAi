---
name: wm26-final-lineups
description: Refresh FIFA World Cup 2026 final lineup context once official FIFA final squad lists are available. Use when Codex needs to check whether WM26 final squads are published, prefer FIFA team /squad endpoints if they contain player data, update data/wm26/lineups/lineups-seed.csv to official full-squad membership, refresh collect-context lineups, or replace provisional/header-only WM26 lineup context.
---

# WM26 Final Lineups

## Overview

Use this skill to move WM26 lineup context from provisional squad material to official FIFA final squad membership. Keep the workflow conservative: do not replace the tracked seed until all 48 manifest teams have official FIFA roster membership that can be validated.

## Source Priority

Prefer FIFA's team squad tab data once it contains player records. This endpoint is more direct than article parsing and should be checked first:

```text
https://cxm-api.fifa.com/fifaplusweb/api/pages/en/tournaments/mens/worldcup/canadamexicousa2026/teams/{fifa-team-slug}/squad
```

The squad page usually exposes an `EntireSquad` section whose `entryEndpoint` resembles:

```text
teamPage/data/{team-page-id}?locale=en
```

Fetch it as:

```text
https://cxm-api.fifa.com/fifaplusweb/api/{entryEndpoint}
```

Use the squad endpoint as the membership source only if it returns usable player records for every team. If it still returns only labels or page-shell metadata, fall back to FIFA article JSON extraction from the official squad tracker.

Fallback official sources:

- FIFA timing page: `https://www.fifa.com/en/tournaments/mens/worldcup/canadamexicousa2026/articles/squad-lists-number-date`
- FIFA squad tracker: `https://www.fifa.com/en/articles/all-world-cup-squad-announcements`
- FIFA JSON base: `https://cxm-api.fifa.com/fifaplusweb/api`

Use only official FIFA material for roster membership once final lists exist. Use `dcaribou/transfermarkt-datasets` only for supplemental coach, age, position, and market-value data.

## Workflow

1. Confirm the current date and official source state.
   - Check the timing page for FIFA's final-list rule.
   - Check the squad tracker for all 48 teams.
   - Treat article labels like `preliminary`, over-26 rosters, under-26 rosters, reserves, standby lists, and training groups as not-yet-final unless FIFA's squad endpoint gives final player records.

2. Try the FIFA team `/squad` endpoint first.
   - Map the project manifest in `data/wm26/lineups/wm26-teams.csv` to FIFA team page slugs.
   - Fetch the `/teams/{fifa-team-slug}/squad` page JSON.
   - Follow the `EntireSquad` `entryEndpoint`.
   - Prefer this path if it returns concrete player records, because it is the direct squad tab rather than a news article.

3. Fall back to tracker/article extraction only when needed.
   - Follow `docs/onboarding-wm26/lineups/fifa-squad-seed-generation.md`.
   - Fetch raw JSON with `Invoke-WebRequest` or another lossless method, not PowerShell `ConvertTo-Json` at a shallow depth.
   - Parse position groups from article rich text and exclude reserves, standby, replacement-watch, and training-only groups.

4. Gate the seed update.
   - Confirm all 48 teams in `data/wm26/lineups/wm26-teams.csv` have usable official FIFA roster membership.
   - Confirm no generated roster remains header-only.
   - Expect 26 players per team unless FIFA explicitly publishes a final list with 23-25 players; if that happens, document the official exception before updating.
   - Preserve prior coach names, national-team IDs, player IDs, and spelling fixes from `data/wm26/lineups/lineups-seed.csv` where still applicable.

5. Update the tracked seed.
   - Edit `data/wm26/lineups/lineups-seed.csv`.
   - Use `Data_Collected_At` equal to the date the final source was checked.
   - Keep deterministic manifest order.
   - Keep the seed columns compatible with `collect-context lineups`: `Team_Slug,Team,Data_Collected_At,Role,Name,Transfermarkt_National_Team_Id,Transfermarkt_Player_Id,Age,Position,Market_Value_EUR`.
   - Do not commit source JSON, downloaded DuckDB files, generated Firestore payloads, or private notes.

6. Refresh and validate lineup context.
   - Run `collect-context lineups` with `--competition fifa-world-cup-2026`.
   - Use `--dry-run` first if changing source parsing or membership.
   - Review `Header-only lineup context payloads` and `Missing lineup source data`.
   - Run focused tests for changed parser/source behavior.
   - Validate WM26 prediction shortcuts only after the required lineup documents and `lineups` KPI document exist.

7. Update source-state docs and close out.
   - Update `docs/onboarding-wm26/lineup-source-status.md` from provisional to official final-source state.
   - Cross-check `docs/onboarding-wm26/README.md` and `docs/onboarding-wm26/lineups/process.md` if assumptions changed.
   - Inspect `git diff` and `git status`.
   - Commit the intended changes.
   - Before pushing, verify branch, remotes, status, and latest commit, then push explicitly with `git push origin <branch>`.

## Repo Rules

- Use `uv --cache-dir .uv-cache run ...` for Python helper scripts.
- Run `dotnet` and `git add`/`git commit`/`git push` outside the sandbox in this repo.
- Keep generated CSV context compatible with the repo CSV guidance: no leading blank lines, deterministic row order, one record per line, and a final trailing line terminator.
- Use official FIFA membership as source of truth and Transfermarkt only as supplemental data.
- If official final membership is still incomplete, do not update the seed; report exactly which teams remain unresolved and which official endpoints were checked.

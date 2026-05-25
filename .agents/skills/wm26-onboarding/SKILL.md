---
name: wm26-onboarding
description: Onboard KicktippAi FIFA World Cup 2026 communities end to end. Use when preparing WM26 community context, generating and uploading mandatory lineup documents, seeding Firestore context/KPI documents, applying the recent-history played-date map, validating matchday-dev or bonus-dev, inspecting Langfuse traces, or committing and pushing WM26 onboarding changes.
---

# WM26 Onboarding

## Overview

This skill coordinates the operational workflow for FIFA World Cup 2026 KicktippAi communities. It keeps the checked-in WM26 data, mandatory lineup documents, Firestore context documents, dev prediction validation, Langfuse trace review, and closeout steps aligned.

## Workflow

1. Confirm the target community and competition.
   - Dev communities must be listed in `CompetitionResolver.SupportedDevCommunities`.
   - WM26 communities should resolve to `fifa-world-cup-2026`.
   - Run `dotnet` and `git` commands outside the sandbox in this repository.

2. Seed context for a supported dev community.

```powershell
dotnet run --project src/Orchestrator -- collect-context-dev -c ehonda-dev-wm26 --verbose
```

Use `--matchdays`, `--dry-run`, and `--verbose` as needed. The command collects Kicktipp context, fetches live WM26 FIFA rankings, and uploads ranking context/KPI documents to Firestore.

3. For non-dev or explicit workflows, run the two collection paths separately.

```powershell
dotnet run --project src/Orchestrator -- collect-context kicktipp --community-context <community-context> --competition fifa-world-cup-2026
dotnet run --project src/Orchestrator -- collect-context fifa --community-context <community-context> --competition fifa-world-cup-2026
```

4. Generate, upload, or copy mandatory lineup documents with `$wm26-lineups`.

Every WM26 community needs per-team `lineup-*` context documents and the aggregate `lineups` KPI document before prediction validation. Use official FIFA lineup/squad material for membership once available, and use the CC0 `dcaribou/transfermarkt-datasets` DuckDB database as the only supplemental source.

FIFA final squad lists are expected on 2 June 2026, when FIFA announces the submitted final 26-player lists. Treat earlier squad announcements as provisional. Once those final FIFA squad lists are available, regenerate, enrich, and upload/copy the full-squad `lineup-*` context documents and `lineups` KPI document with `$wm26-lineups` using `--status official`. Keep full squads in context; do not switch this workflow to match-starter-only lineups.

Do not run `matchday-dev` until every match team has its required `lineup-{team}.csv` context document. Do not run `bonus-dev` until the `lineups` KPI document exists.

5. Apply the canonical recent-history date map.

```powershell
dotnet run --project src/Orchestrator -- wm26-recent-history apply-date-map --community-context <community-context> --competition fifa-world-cup-2026 --input data/wm26/recent-history/recent-history-match-dates.csv
```

Run with `--dry-run` first when changing the map.

6. Validate predictions.

```powershell
dotnet run --project src/Orchestrator -- matchday-dev -c ehonda-dev-wm26 --verbose
dotnet run --project src/Orchestrator -- bonus-dev -c ehonda-dev-wm26 --verbose
```

Acceptance checks:
- `matchday-dev` finds all required context documents in Firestore with no on-demand fallback warning, including the two participating teams' `lineup-*` docs.
- `bonus-dev` includes KPI context document `fifa-rankings` and includes `lineups` only for the exact top-scorer-team question.
- Langfuse traces show hosted prompts, `langfusePromptFallback=false`, `openaiReasoningEffort=minimal`, ranking context containing `Rank,Team,ELO,Data_Collected_At`, and lineup context containing `Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR`.

7. Inspect Langfuse traces with the repository Langfuse workflow.
   - Use the global `langfuse` skill and installed `langfuse` CLI.
   - Prefer filtering by `environment=development`, the WM26 community tag, and trace/observation names `matchday`, `bonus`, `predict-match`, or `predict-bonus`.

8. Close out.
   - Run focused tests for the changed command/provider areas.
   - Inspect `git diff` and `git status`.
   - Commit the intended changes.
   - Before pushing, verify branch, remotes, status, and latest commit, then push explicitly with `git push origin <branch>`.

## Data Locations

- Per-team FIFA ranking context documents: generated live by `collect-context fifa` as `fifa-ranking-*.csv` Firestore documents
- Aggregate FIFA ranking KPI document: generated live by `collect-context fifa` as Firestore KPI document `fifa-rankings`
- Per-team lineup context documents: generated or copied with `$wm26-lineups` as `lineup-*.csv` Firestore documents
- Aggregate lineup KPI document: generated or copied with `$wm26-lineups` as Firestore KPI document `lineups`
- Recent-history played-date map: `data/wm26/recent-history/recent-history-match-dates.csv`
- Workflow documentation: `docs/onboarding-wm26/README.md`

FIFA ranking CSV payloads must use `Rank,Team,ELO,Data_Collected_At`, format points with two decimal places, and must not contain empty `Data_Collected_At` values.

Lineup CSV payloads must use `Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR`, must include coaches, must use `N/A` instead of `0` for unavailable player market values, and must leave coach market values empty.

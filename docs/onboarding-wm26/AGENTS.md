# WM26 Onboarding Agent Instructions

## Mandatory Lineup Context

Always use `$wm26-lineups` when creating, enriching, uploading, copying, or validating WM26 lineup context. WM26 communities require:

- per-team Firestore context documents named `lineup-*.csv`
- aggregate Firestore KPI document `lineups`

Do not run `matchday-dev` until the required `lineup-*` context documents exist for the match teams. Do not run `bonus-dev` until the `lineups` KPI document exists.

## Data Sources

Use official FIFA lineup or squad material for official membership once available. Use `dcaribou/transfermarkt-datasets` as the only supplemental source for coach, age, position, and market-value data. Use the upstream CC0 DuckDB database from an ignored local path; do not commit the database, source CSVs, generated payloads, or source notes.

Do not scrape websites for supplemental lineup values in this context.

## Verification Workflow

Always execute this verification workflow when changing WM26 context behavior, lineup behavior, onboarding docs, or prompt-context routing:

```powershell
dotnet run --project src/Orchestrator -- matchday-dev -c ehonda-dev-wm26 --verbose
dotnet run --project src/Orchestrator -- bonus-dev -c ehonda-dev-wm26 --verbose
```

Run `dotnet` and `git` outside the sandbox in this repository.

Use `$langfuse` and the installed `langfuse` CLI to inspect the resulting development traces. Record trace or observation IDs and verify:

- matchday traces include only the two participating teams' `lineup-*` docs
- bonus traces include `lineups` only for `Welche Mannschaft stellt den Spieler mit den meisten Toren?`
- hosted prompt fallback is false
- WM26 default reasoning effort is present

If verification cannot be completed, record the exact skipped command and blocker in the final response.

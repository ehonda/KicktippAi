# FIFA World Cup 2026 Manual Onboarding

This first pass supports manual participation for the development community `ehonda-dev-wm26` and competition `fifa-world-cup-2026`.

No scheduled GitHub Actions workflow is enabled yet.

## Defaults

`ehonda-dev-wm26` resolves to `fifa-world-cup-2026`. Existing communities default to `bundesliga-2025-26` and keep their legacy Firestore document IDs.

For WM 2026 manual prediction commands:

- prompt source: `langfuse`
- prompt label: `latest`
- model: `gpt-5-nano` when the model argument is omitted
- reasoning effort: `minimal` unless explicitly overridden

## Prompts

Hosted Langfuse text prompts:

- `kicktippai/wm26/predict-one-match`
- `kicktippai/wm26/predict-bonus`

Both prompts should use `{{context_documents}}` for the context insertion point and carry the `latest` label.

Checked-in fallback copies:

- `prompts/wm26/match.md`
- `prompts/wm26/bonus.md`

The fallback path should almost never run. Langfuse prompt fetching already has service-side and SDK-side caching semantics; the local fallback exists to avoid a failed manual run during an inopportune Langfuse outage or first-fetch problem. When fallback is used, the command prints a console warning and trace metadata includes `langfusePromptFallback=true`.

Hosted WM match prompts with justification are intentionally out of scope for v1. A WM hosted run with `--with-justification` fails clearly until a hosted justification prompt exists.

## Context Documents

WM26 match predictions require:

- `fifa-world-cup-2026-standings.csv`
- `community-rules-ehonda-dev-wm26.md`
- `recent-history-{home-national-team}.csv`
- `recent-history-{away-national-team}.csv`
- `fifa-ranking-{home-national-team}.csv`
- `fifa-ranking-{away-national-team}.csv`

There are no optional WM26 context documents in the first pass. Home/away history and head-to-head history are intentionally omitted for national teams. Community-specific knobs, including this required/optional document policy, are documented in `docs/design/community-configuration.md`.

The checked-in WM26 ranking files live in `docs/onboarding-wm26/` and use the same slug conventions as the recent-history documents, for example `fifa-ranking-deutschland.csv` and `fifa-ranking-elfenbeinkuste.csv`.

WM26 bonus predictions use the aggregate KPI document `fifa-rankings`. The canonical checked-in source is `docs/onboarding-wm26/fifa-rankings.csv`, and the upload artifact is `kpi-documents/output/ehonda-dev-wm26/fifa-rankings.json`.

The `ELO` column stores the FIFA ranking points from the men's FIFA ranking table dated 1 April 2026.

## Recent History Played Dates

Recent-history rows for national teams can describe matches played years before they are collected from Kicktipp. For WM26, `Data_Collected_At` must therefore contain the exact played date, not a first-collection marker.

The canonical played-date source is:

```text
docs/onboarding-wm26/recent-history-match-dates.csv
```

First-time dev setup:

```powershell
dotnet run --project src/Orchestrator -- collect-context kicktipp --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026
dotnet run --project src/Orchestrator -- wm26-recent-history export-date-map --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --output docs/onboarding-wm26/recent-history-match-dates.csv
```

Fill missing `Played_At` values in the date map once, using official sources first: FIFA Match Centre, confederation or national federation pages, then reputable result pages if needed. Keep `Source_Name`, `Source_Url`, and `Verified_At` populated for reviewed rows.

Apply the canonical map back to Firestore:

```powershell
dotnet run --project src/Orchestrator -- wm26-recent-history apply-date-map --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --input docs/onboarding-wm26/recent-history-match-dates.csv --dry-run
dotnet run --project src/Orchestrator -- wm26-recent-history apply-date-map --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --input docs/onboarding-wm26/recent-history-match-dates.csv
```

For future WM26 communities, collect that community's context documents first, then run `apply-date-map` with the same canonical CSV. Do not repeat web lookup unless the command reports rows that are genuinely missing from the map.

## Manual Commands

The guarded development shortcuts post to Kicktipp and overwrite existing database predictions. They are only available for supported development communities such as `ehonda-dev-wm26`; normal `matchday` and `bonus` commands still keep `--override-kicktipp` and `--override-database` off by default.

Matchday:

```powershell
dotnet run --project src/Orchestrator -- matchday-dev -c ehonda-dev-wm26
```

Bonus:

```powershell
dotnet run --project src/Orchestrator -- bonus-dev -c ehonda-dev-wm26
```

For ad hoc dry-runs or non-default combinations, use the normal commands and pass the flags explicitly.

Verification:

```powershell
dotnet run --project src/Orchestrator -- verify matchday -c ehonda-dev-wm26
dotnet run --project src/Orchestrator -- verify bonus -c ehonda-dev-wm26
```

Context and outcomes:

```powershell
dotnet run --project src/Orchestrator -- collect-context kicktipp --community-context ehonda-dev-wm26
```

Every command also accepts `--competition fifa-world-cup-2026` for explicit runs or local experiments.

## Snapshot Collection

Encrypted fixture collection is manual-first for now:

```powershell
dotnet run --project src/Orchestrator -- snapshots all --community ehonda-dev-wm26
```

Commit only encrypted files under:

```text
tests/KicktippIntegration.Tests/Fixtures/Html/Real/ehonda-dev-wm26/*.html.enc
```

Do not commit raw `kicktipp-snapshots` HTML. If credentials or `KICKTIPP_FIXTURE_KEY` are missing, fix that locally before collecting snapshots.

## Follow-Ups

- Enable scheduled workflows after the manual dev path has been exercised.
- Decide production community naming and rollout timing.
- Add hosted WM justification prompts if we want justification mode.
- Add monitoring dashboards/alerts specific to WM prompt fallback and WM competition metadata.

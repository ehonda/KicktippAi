# FIFA World Cup 2026 Onboarding

This first pass supports manual participation for the development community
`ehonda-dev-wm26` and competition `fifa-world-cup-2026`.

No scheduled GitHub Actions workflow is enabled yet. The
`ehonda-ai-arena` `gpt-5-nano` / `minimal` workflows are preliminary
manual-dispatch test entrypoints only.

The tracked lineup seed now uses FIFA's official final 26-player squad
membership for all 48 teams. Refresh lineup context for each WM26 community
before scheduled production activation.

Model configuration onboarding status is tracked in
[model-config-onboarding.md](model-config-onboarding.md). Keep that ledger
current while testing manual communities, and use it as the reference before
bulk workflow activation.

## Reference And Secondary Communities

`rabetrabauken2026` is the WM26 reference production community context. It
resolves to `fifa-world-cup-2026` and has a manual-only context collection
workflow at `.github/workflows/rabetrabauken2026-context-collection.yml`. That
workflow collects Kicktipp context plus WM26 FIFA ranking and lineup context for
the reference community.

`ehonda-ai-arena` is planned as a secondary posting community for the selected
WM26 production model. The preliminary `gpt-5-nano` / `minimal` workflows are
self-contained manual onboarding tests: collect and use `ehonda-ai-arena`
context directly with:

```yaml
community: "ehonda-ai-arena"
community_context: "ehonda-ai-arena"
```

This keeps the preliminary test independent of `rabetrabauken2026` credentials.
The later secondary copy-posting workflow is scoped only to the yet-undetermined
`rabetrabauken2026` WM26 production model: the primary prediction workflow must
first run against `rabetrabauken2026`, and the matching `ehonda-ai-arena`
workflow may then post that stored prediction with
`community_context: "rabetrabauken2026"`. Do not apply that copy-from-primary pattern to
preliminary tests, dev shortcuts, or unrelated WM26 model configurations.

Do not create or activate scheduled model-specific WM26 prediction workflows
until the production model configuration is selected and the full-competition
estimate is documented.
The current `ehonda-ai-arena` `gpt-5-nano` / `minimal` workflows are an
exception for preliminary manual testing; their cron schedules stay disabled and
the preliminary full-competition estimate is tracked in the model configuration
ledger.

## Planned GitHub Actions Cadence

Once the first WM26 communities and model-specific workflows are onboarded, use a three-window daily cadence during the tournament window. The current plan is based on the official FIFA World Cup 26 match schedule PDF dated 2026-04-10, which lists all kickoff times in Eastern Time and is marked subject to change. Re-check the official schedule before enabling the workflows.

Context collection:

```yaml
schedule:
  - cron: '47 23,6,11 * * *'
```

Main matchday prediction communities:

```yaml
schedule:
  - cron: '37 0,7,12 * * *'
```

Slower or secondary model/community workflows:

```yaml
schedule:
  - cron: '7 1,8,13 * * *'
```

Bonus prediction workflows, if enabled on the same cadence:

```yaml
schedule:
  - cron: '47 0,7,12 * * *'
```

GitHub Actions cron runs in UTC. During WM26, Germany is expected to be on CEST, so these correspond approximately to:

| Workflow type | UTC times | Europe/Berlin times |
| --- | --- | --- |
| Context collection | 23:47, 06:47, 11:47 | 01:47, 08:47, 13:47 |
| Main matchday predictions | 00:37, 07:37, 12:37 | 02:37, 09:37, 14:37 |
| Slower/secondary predictions | 01:07, 08:07, 13:07 | 03:07, 10:07, 15:07 |
| Bonus predictions | 00:47, 07:47, 12:47 | 02:47, 09:47, 14:47 |

The offsets preserve the Bundesliga dependency pattern: collect context first, then run the primary prediction workflows, then run slower or secondary model workflows after the shared data has had time to land. The non-zero minute values also avoid top-of-hour scheduled workflow congestion.

WM26 needs a third daily window because many North American kickoffs are late in UTC/Berlin time. The `00:37 UTC` prediction window gives a retry opportunity before late matches, `07:37 UTC` refreshes after overnight results and context updates, and `12:37 UTC` provides another attempt before the earliest remaining kickoff blocks. This gives at least two scheduled prediction tries for upcoming matches in normal tournament flow, including a useful pair of post-group-stage attempts before the first Round of 32 match on 2026-06-28.

## Dev And Production Model Defaults

`ehonda-dev-wm26` resolves to `fifa-world-cup-2026`. Existing communities default to `bundesliga-2025-26` and keep their legacy Firestore document IDs.

For WM26 dev work and low-cost manual testing, the guarded `matchday-dev` and
`bonus-dev` commands use:

- prompt source: `langfuse`
- prompt label: `latest`
- model: `gpt-5-nano`
- reasoning effort: `minimal`

These are dev/test command defaults, not the WM26 production configuration.
Production or scheduled prediction workflows must pass the selected production
model and reasoning effort explicitly. Use the reusable prediction workflow
`reasoning_effort` input for that wiring.
The production configuration is still TBD.

The dev/test `gpt-5-nano` / `minimal` model configuration has a preliminary
full-competition estimate row in `docs/experiments/whole-season-cost-estimates.md`.
Before activating schedules for any selected production configuration, generate
and document that configuration's estimate through the
`estimate-experiment-cost-skill` workflow.

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
- `lineup-{home-national-team}.csv`
- `lineup-{away-national-team}.csv`

There are no optional WM26 context documents in the first pass. Home/away history and head-to-head history are intentionally omitted for national teams. Community-specific knobs, including this required/optional document policy, are documented in `docs/design/community-configuration.md`.

WM26 FIFA ranking documents are generated live by `collect-context fifa` from FIFA's public rankings API and stored in Firestore. There are no checked-in ranking CSV sources. The command writes one per-team match-context document using the same slug conventions as recent-history documents, for example `fifa-ranking-deutschland.csv` and `fifa-ranking-elfenbeinkuste.csv`.

WM26 bonus predictions use the aggregate KPI document `fifa-rankings`, generated by the same command. Ranking CSVs use `Rank,Team,ELO,Data_Collected_At`; the `ELO` column stores FIFA ranking points from the men's FIFA ranking table.

Lineup context is also mandatory for WM26. Generate or refresh per-team `lineup-*` context documents and the aggregate `lineups` KPI document with `collect-context lineups` before running `matchday-dev` or `bonus-dev`. Official FIFA lineup/squad material is the membership source; `dcaribou/transfermarkt-datasets` is the only supplemental source for coach, age, position, and market-value data. The command uses `data/wm26/lineups/wm26-teams.csv` to emit a document for every WM26 participant. The current official final seed contains source rows for all 48 teams.

Lineup CSV payloads use `Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR`. Provisional vs official source state is documented in [lineup-source-status.md](lineup-source-status.md); it is not a command flag and is not included in prompt context.

FIFA published the final 26-player squad lists on 2026-06-03 after the 2026-06-02 team submission deadline. The checked-in seed now uses full-squad official FIFA membership; refresh `lineup-*` context documents and the `lineups` KPI document with `collect-context lineups` for each WM26 community. WM26 context should contain full squads, not only match starters. See [lineup-source-status.md](lineup-source-status.md) for the current source state.

Collecting live FIFA rankings and lineups is required context setup for every WM26 community. The guarded `collect-context-dev` path includes both steps automatically after Kicktipp context collection.

Upload the ranking context and KPI documents with:

```powershell
dotnet run --project src/Orchestrator -- collect-context fifa --community-context ehonda-dev-wm26
dotnet run --project src/Orchestrator -- collect-context lineups --community-context ehonda-dev-wm26
```

## Recent History Played Dates

Recent-history rows for national teams can describe matches played years before they are collected from Kicktipp. For WM26, `Data_Collected_At` must therefore contain the exact played date, not a first-collection marker.

The canonical played-date source is:

```text
data/wm26/recent-history/recent-history-match-dates.csv
```

First-time dev setup:

```powershell
dotnet run --project src/Orchestrator -- collect-context-dev -c ehonda-dev-wm26 --verbose
dotnet run --project src/Orchestrator -- wm26-recent-history export-date-map --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --output data/wm26/recent-history/recent-history-match-dates.csv
```

Fill missing `Played_At` values in the date map once, using official sources first: FIFA Match Centre, confederation or national federation pages, then reputable result pages if needed. Keep `Source_Name`, `Source_Url`, and `Verified_At` populated for reviewed rows.

Apply the canonical map back to Firestore:

```powershell
dotnet run --project src/Orchestrator -- wm26-recent-history apply-date-map --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --input data/wm26/recent-history/recent-history-match-dates.csv --dry-run
dotnet run --project src/Orchestrator -- wm26-recent-history apply-date-map --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --input data/wm26/recent-history/recent-history-match-dates.csv
```

For future WM26 communities, collect that community's context documents first, then run `apply-date-map` with the same canonical CSV. Do not repeat web lookup unless the command reports rows that are genuinely missing from the map.

## Manual Commands

The guarded development shortcuts post to Kicktipp and overwrite existing database predictions. They are only available for supported development communities such as `ehonda-dev-wm26`; normal `matchday` and `bonus` commands still keep `--override-kicktipp` and `--override-database` off by default.

Before running these shortcuts, make sure the required `lineup-*` context documents and the aggregate `lineups` KPI document have been generated with `collect-context lineups`.

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
dotnet run --project src/Orchestrator -- verify gpt-5-nano -c ehonda-dev-wm26 --reasoning-effort minimal
dotnet run --project src/Orchestrator -- verify-bonus gpt-5-nano -c ehonda-dev-wm26 --reasoning-effort minimal
```

Context and outcomes:

```powershell
dotnet run --project src/Orchestrator -- collect-context-dev -c ehonda-dev-wm26 --verbose
```

For production or one-off context uploads, run the individual paths explicitly:

```powershell
dotnet run --project src/Orchestrator -- collect-context kicktipp --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026
dotnet run --project src/Orchestrator -- collect-context fifa --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026
dotnet run --project src/Orchestrator -- collect-context lineups --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026
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

## Manual Follow-Ups After Onboarding

After autonomous onboarding, report any manual items still required before the
workflow can be trusted or scheduled:

- Configure GitHub Actions secrets for each WM26 community workflow:
  per-community Kicktipp username/password secrets, `FIREBASE_PROJECT_ID`,
  `FIREBASE_SERVICE_ACCOUNT_JSON`, `OPENAI_API_KEY`, and
  `LANGFUSE_SECRET_KEY`.
  For the preliminary `ehonda-ai-arena` `gpt-5-nano` / `minimal` workflows,
  use `EHONDA_AI_ARENA_GPT_5_NANO_MINIMAL_KICKTIPP_USERNAME` and
  `EHONDA_AI_ARENA_GPT_5_NANO_MINIMAL_KICKTIPP_PASSWORD`; the matching
  `ehonda-ai-arena` context collection workflow uses the same Kicktipp
  credentials for this preliminary self-contained test.
- Configure repository variable `LANGFUSE_PUBLIC_KEY` if it is not already set.
- Select and document the WM26 production model configuration; do not use the
  `gpt-5-nano` / `minimal` dev command defaults as the production assumption.
- Ensure prediction workflows pass the selected model and reasoning effort
  explicitly through the `reasoning_effort` input.
- Add the full-competition cost estimate for every scheduled model
  configuration.
- Manually trigger context collection once and verify Kicktipp, FIFA ranking,
  lineup, and KPI Firestore documents before prediction workflow testing.
- Manually trigger matchday and bonus workflows once before enabling cron
  schedules.
- When production workflows are activated, update the hard-coded production
  community lists described in `.github/workflows/AGENTS.md` so Langfuse trace
  environments are correct.

## Follow-Ups

- Enable the planned scheduled workflow cadence after the first WM26 communities and models are onboarded.
- Select the WM26 production model configuration and document its full-competition estimate before scheduled activation.
- For each new WM26 community, activate scheduled context collection before prediction workflows; the context workflow must run Kicktipp, FIFA ranking, and lineup collection for the community.
- Refresh `collect-context lineups` for each WM26 community after the official final seed update.
- Decide production community naming and rollout timing.
- Add hosted WM justification prompts if we want justification mode.
- Add monitoring dashboards/alerts specific to WM prompt fallback and WM competition metadata.

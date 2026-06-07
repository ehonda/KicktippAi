# FIFA World Cup 2026 Onboarding

This onboarding wave is finished for now as of 2026-06-06. It covers the
development community `ehonda-dev-wm26`, the WM26 reference production
community `rabetrabauken2026`, and the secondary / experiment community
`ehonda-ai-arena` for competition `fifa-world-cup-2026`.

The selected WM26 production configuration is `o3` with
`reasoning_effort: "high"` and `max_output_tokens: 40000`. It runs primarily
against `rabetrabauken2026`, with matching secondary copy-posting workflows for
`ehonda-ai-arena` that reuse `community_context: "rabetrabauken2026"`.

The scheduled self-contained `ehonda-ai-arena` `gpt-5-nano` / `minimal`
workflows remain active. They do not conflict with the `o3 high` production
path because they keep `community_context: "ehonda-ai-arena"` and use their own
model-specific Kicktipp posting credentials. Additional self-contained
comparison workflows now exist for `gpt-5.5` with `none`, `gpt-5.5` with
`xhigh`, `gpt-5.4-nano` with `none`, and `o3` with `medium`; these stay
manual-only.

The tracked lineup seed now uses FIFA's official final 26-player squad
membership for all 48 teams. Refresh lineup context for each WM26 community
before scheduled production activation.

Model configuration onboarding status is tracked in
[model-config-onboarding.md](model-config-onboarding.md). Keep that ledger
current for any last-minute additions, and use it as the reference before
activating any new schedules beyond the currently selected WM26 set.

## Reference And Secondary Communities

`rabetrabauken2026` is the selected WM26 reference production community
context. It resolves to `fifa-world-cup-2026` and now has the scheduled context
collection workflow `.github/workflows/rabetrabauken2026-context-collection.yml`.
That workflow collects Kicktipp context plus WM26 FIFA ranking and lineup
context for the reference community.

`ehonda-ai-arena` now serves two distinct WM26 roles. The scheduled
`gpt-5-nano` / `minimal` workflows are self-contained and collect plus use
`ehonda-ai-arena` context directly with:

```yaml
community: "ehonda-ai-arena"
community_context: "ehonda-ai-arena"
```

This keeps the self-contained path independent of `rabetrabauken2026`
credentials and stored predictions. Separately, `ehonda-ai-arena` is the
selected secondary posting target for the `o3 high` production path: the
primary prediction workflow runs first against `rabetrabauken2026`, and the
matching `ehonda-ai-arena` workflow then posts that stored prediction with:

```yaml
community: "ehonda-ai-arena"
community_context: "rabetrabauken2026"
```

That copy-from-primary pattern is selected only for `o3 high`. Do not reuse it
for `gpt-5-nano minimal`, `o3 medium`, dev shortcuts, or unrelated WM26 model
tests unless the model configuration ledger is updated first.

The additional `gpt-5.5 none`, `gpt-5.5 xhigh`, `gpt-5.4-nano none`, and
`o3 medium` `ehonda-ai-arena` workflows are self-contained comparison
entrypoints. They stay manual-only, keep
`community_context: "ehonda-ai-arena"`, and rely on the shared
`wm26-ehonda-ai-arena-context-collection.yml` workflow for self-contained
context refreshes. The `o3 high` production pair and the `gpt-5.5 xhigh` pair
explicitly pass `max_output_tokens: 40000`; the other self-contained WM26
prediction workflows pin `10000`.

## Planned GitHub Actions Cadence

The onboarded scheduled WM26 workflows use a three-window daily cadence during
the tournament window. The plan is based on the official FIFA World Cup 26
match schedule PDF dated 2026-04-10, which lists all kickoff times in Eastern
Time and is marked subject to change. Re-check the official schedule before
adding any further scheduled workflows.

WM26 GitHub Actions workflow display names should include `🏆`, and new WM26
workflow filenames should use a `wm26-` prefix so they are visually and
operationally separate from legacy Bundesliga workflow files.

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

Currently active WM26 schedules use that cadence as follows:

- `rabetrabauken2026-context-collection.yml`: context collection cadence
- `wm26-rabetrabauken2026-o3-high-matchday.yml`: main matchday cadence
- `wm26-rabetrabauken2026-o3-high-bonus.yml`: bonus cadence
- `wm26-ehonda-ai-arena-o3-high-matchday.yml` and `wm26-ehonda-ai-arena-o3-high-bonus.yml`: slower secondary cadence
- `wm26-ehonda-ai-arena-gpt-5-nano-minimal-matchday.yml`: main matchday cadence
- `wm26-ehonda-ai-arena-gpt-5-nano-minimal-bonus.yml`: bonus cadence

The scheduled `gpt-5-nano minimal` and `o3 high` paths can coexist because
they use different model configurations, different model-specific Kicktipp
posting credentials, and different `community_context` values.

## Dev And Production Model Defaults

`ehonda-dev-wm26` resolves to `fifa-world-cup-2026`. Existing communities default to `bundesliga-2025-26` and keep their legacy Firestore document IDs.

For WM26 dev work and low-cost manual testing, the guarded `matchday-dev` and
`bonus-dev` commands use:

- prompt source: `langfuse`
- prompt label: `latest`
- model: `gpt-5-nano`
- reasoning effort: `minimal`

These are dev/test command defaults, not the WM26 production configuration.
Production and scheduled prediction workflows must pass their explicit model and
reasoning inputs. The selected WM26 production workflows do this with
`model: "o3"`, `reasoning_effort: "high"`, and
`max_output_tokens: 40000`. The scheduled self-contained
`ehonda-ai-arena` `gpt-5-nano` / `minimal` path remains active as an
independent onboarding and comparison route, and the self-contained
`ehonda-ai-arena` `o3 medium` pair stays manual-only.

The dev/test `gpt-5-nano` / `minimal` model configuration has a documented
full-competition estimate row in
`docs/experiments/whole-season-cost-estimates.md`. The selected `o3 high`
production path and the manual-only `o3 medium` comparison path are also
documented there, but their current estimates still reuse generic base evidence
rather than exact WM-hosted cap-matched runs, so treat them as planning
coverage rather than final runtime-cost proof.

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

Collecting live FIFA rankings and lineups is required context setup for every WM26 community. The guarded `collect-context-dev` path includes both steps automatically after Kicktipp context collection. GitHub Actions WM26 context workflows also apply the known recent-history played-date map after Kicktipp collection, producing prompt-facing recent-history CSVs with `Played_At` while preserving tournament rows dated on or after 2026-06-11.

Upload the ranking context and KPI documents with:

```powershell
dotnet run --project src/Orchestrator -- collect-context fifa --community-context ehonda-dev-wm26
dotnet run --project src/Orchestrator -- collect-context lineups --community-context ehonda-dev-wm26
```

## Recent History Played Dates

Recent-history rows for national teams can describe matches played years before they are collected from Kicktipp. Prompt-facing WM26 recent-history CSVs use `Competition,Played_At,Home_Team,Away_Team,Score,Annotation`. For pre-WM26 history rows, `Played_At` must contain the exact played date, not a first-collection marker. Rows added during the WM26 tournament keep the standard Kicktipp collection-date semantics in `Played_At` unless manually curated later.

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

Strict mode is for manual map validation and fails when a target row is not covered by an exact `Played_At` value. Automated WM26 context workflows use guarded mode instead:

```powershell
dotnet run --project src/Orchestrator -- wm26-recent-history apply-date-map --community-context rabetrabauken2026 --competition fifa-world-cup-2026 --input data/wm26/recent-history/recent-history-match-dates.csv --apply-known-only --preserve-collected-on-or-after 2026-06-11
```

Guarded mode applies known pre-WM26 dates, preserves unmapped rows, and preserves any row whose existing `Played_At` or legacy `Data_Collected_At` is on or after 2026-06-11 before matching date-map entries. This prevents a WM26 tournament row from consuming or being overwritten by an older map entry for the same teams, score, and competition label.

For future WM26 communities, collect that community's context documents first, then run `apply-date-map` with the same canonical CSV. Do not repeat web lookup unless strict mode reports genuinely missing pre-WM26 rows that should be curated into the map.

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
  The selected `rabetrabauken2026` `o3 high` production workflows use
  `RABETRABAUKEN2026_O3_HIGH_KICKTIPP_USERNAME` /
  `RABETRABAUKEN2026_O3_HIGH_KICKTIPP_PASSWORD`, and the matching
  `ehonda-ai-arena` `o3 high` copy-posting workflows use
  `EHONDA_AI_ARENA_O3_HIGH_KICKTIPP_USERNAME` /
  `EHONDA_AI_ARENA_O3_HIGH_KICKTIPP_PASSWORD`.
  Additional self-contained `ehonda-ai-arena` test workflows use
  `EHONDA_AI_ARENA_GPT_5_5_NONE_KICKTIPP_USERNAME` /
  `EHONDA_AI_ARENA_GPT_5_5_NONE_KICKTIPP_PASSWORD`,
  `EHONDA_AI_ARENA_GPT_5_5_XHIGH_KICKTIPP_USERNAME` /
  `EHONDA_AI_ARENA_GPT_5_5_XHIGH_KICKTIPP_PASSWORD`, and
  `EHONDA_AI_ARENA_GPT_5_4_NANO_NONE_KICKTIPP_USERNAME` /
  `EHONDA_AI_ARENA_GPT_5_4_NANO_NONE_KICKTIPP_PASSWORD`.
  The self-contained `o3 medium` comparison workflows use
  `EHONDA_AI_ARENA_O3_MEDIUM_KICKTIPP_USERNAME` /
  `EHONDA_AI_ARENA_O3_MEDIUM_KICKTIPP_PASSWORD`.
- Configure repository variable `LANGFUSE_PUBLIC_KEY` if it is not already set.
- Confirm that every model-specific Kicktipp posting identity has manual
  membership in its target community or communities before trusting scheduled
  runs.
- Manually trigger `rabetrabauken2026-context-collection.yml` once and verify
  Kicktipp, FIFA ranking, lineup, and KPI Firestore documents before relying on
  scheduled production predictions.
- Manually trigger the selected `o3 high` matchday and bonus workflows once per
  posting community after secrets and community membership are in place.
- Manually trigger the self-contained `o3 medium` comparison workflows once if
  that comparison path is needed.
- If we later want tighter WM26 cost evidence, collect WM-hosted cap-matched
  base rows for `o3 high` with `40000` and `o3 medium` before treating their
  current planning estimates as exact runtime-cost coverage.

## Follow-Ups

- For each new WM26 community, activate scheduled context collection before prediction workflows; the context workflow must run Kicktipp collection, the guarded recent-history date-map step, FIFA ranking collection, and lineup collection for the community.
- Refresh `collect-context lineups` for each WM26 community after the official final seed update.
- If a last-minute WM26 model or community is added, record it in `model-config-onboarding.md` and update the cost-estimate coverage before enabling any schedule.
- Add hosted WM justification prompts if we want justification mode.
- Add monitoring dashboards/alerts specific to WM prompt fallback and WM competition metadata.

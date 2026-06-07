# WM26 Model Configuration Onboarding

Updated: 2026-06-07

This ledger tracks which FIFA World Cup 2026 model configurations are
onboarded, where they are wired, and whether their full-competition match
prediction costs are documented.

The guarded `matchday-dev` and `bonus-dev` `gpt-5-nano` / `minimal` defaults
are for dev work and low-cost manual testing only. They are not the WM26
production model configuration.
The selected WM26 production path is `o3` with `reasoning_effort: "high"` and
`max_output_tokens: 40000`, using `rabetrabauken2026` as the primary reference
community and `ehonda-ai-arena` as the secondary copy-posting target.
The scheduled self-contained `ehonda-ai-arena` `gpt-5-nano` / `minimal`
entrypoint remains active alongside that production path because it uses
`community_context: "ehonda-ai-arena"` and its own model-specific Kicktipp
posting credentials.
The `ehonda-ai-arena` `gpt-5.5` / `none`, `gpt-5.5` / `xhigh`,
`gpt-5.4-nano` / `none`, and `o3` / `medium` entries below are additional
self-contained manual-only onboarding comparisons. They are not the selected
WM26 production configuration.
Langfuse prompt lookup on 2026-06-02 resolved
`kicktippai/wm26/predict-one-match` label `latest` to version `2`; label
`production` was not present, so `latest` is the configured hosted WM route for
this ledger.

Use one row per effective model configuration: model, reasoning effort, prompt
route, prompt label/version policy, community, and workflow status. Before
activating a scheduled prediction workflow, make sure the matching cost estimate
is present in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md).

WM26 context workflows now apply the canonical recent-history date map in
guarded mode after Kicktipp context collection. The guarded step produces
recent-history CSVs with `Played_At`, applies known pre-WM26 played dates,
preserves unmapped rows, and preserves rows whose existing `Played_At` or
legacy `Data_Collected_At` is on or after 2026-06-11 so tournament rows keep
standard collection-date semantics.

## Current WM26 Configurations

| Community / use | Competition | Model config | Prompt route | Where onboarded | Workflow status | Full-competition estimate |
| --- | --- | --- | --- | --- | --- | --- |
| `ehonda-dev-wm26` dev/testing shortcuts | `fifa-world-cup-2026` | `gpt-5-nano` with `minimal` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | Guarded `matchday-dev` and `bonus-dev` defaults in `src/Orchestrator/Commands/Operations/Dev/DevParticipationCommandSupport.cs` and prompt defaults in `src/Orchestrator/Infrastructure/CompetitionResolver.cs`; docs in `docs/onboarding-wm26/README.md` | Manual dev commands only; no GitHub Actions schedule activated; not a production config | Documented preliminary match estimate: `N=104: $0.008894080000` in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md). Base row uses hosted WM prompt label `latest` version `2` and historical `pes-squad` repeated-match-slice fixtures. |
| `ehonda-ai-arena` scheduled self-contained workflow path | `fifa-world-cup-2026` | `gpt-5-nano` with `minimal` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | `.github/workflows/wm26-ehonda-ai-arena-context-collection.yml` collects `community_context: "ehonda-ai-arena"`; `.github/workflows/wm26-ehonda-ai-arena-gpt-5-nano-minimal-matchday.yml` and `.github/workflows/wm26-ehonda-ai-arena-gpt-5-nano-minimal-bonus.yml` pass `model: "gpt-5-nano"`, `reasoning_effort: "minimal"`, `max_output_tokens: 10000`, and `community_context: "ehonda-ai-arena"`; posting credentials use `EHONDA_AI_ARENA_GPT_5_NANO_MINIMAL_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_NANO_MINIMAL_KICKTIPP_PASSWORD`; `src/Orchestrator/Infrastructure/CompetitionResolver.cs` resolves `ehonda-ai-arena` to WM26; lineup seed now uses FIFA's official final squads | Manual dispatch and scheduled cadence enabled for the self-contained onboarding path: context at `47 23,6,11 * * *`, matchday at `37 0,7,12 * * *`, bonus at `47 0,7,12 * * *`; remains active alongside `o3 high`; not the selected production config | Documented preliminary match estimate: `N=104: $0.008894080000` in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md). The estimate is suitable for onboarding workflow testing and low-cost monitoring, not as the selected production model decision. |
| `ehonda-ai-arena` self-contained workflow test | `fifa-world-cup-2026` | `gpt-5.5` with `none` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | Shared self-contained context workflow `.github/workflows/wm26-ehonda-ai-arena-context-collection.yml`; `.github/workflows/wm26-ehonda-ai-arena-gpt-5-5-none-matchday.yml` and `.github/workflows/wm26-ehonda-ai-arena-gpt-5-5-none-bonus.yml` pass `model: "gpt-5.5"`, `reasoning_effort: "none"`, `max_output_tokens: 10000`, and `community_context: "ehonda-ai-arena"`; posting credentials use `EHONDA_AI_ARENA_GPT_5_5_NONE_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_5_NONE_KICKTIPP_PASSWORD` | Manual dispatch only; no prediction schedule activated; shared `wm26-ehonda-ai-arena-context-collection.yml` remains the self-contained context source; not a production config | Documented provisional match estimate: `N=104: $0.905060000000` in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md). The exact model/reasoning row exists, but its current base estimate uses the generic `langfuse-o3-poc` prompt route at `10000` max output tokens rather than a WM-hosted base sample. Suitable for testing-only onboarding, not a production model decision. |
| `ehonda-ai-arena` self-contained workflow test | `fifa-world-cup-2026` | `gpt-5.5` with `xhigh` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | Shared self-contained context workflow `.github/workflows/wm26-ehonda-ai-arena-context-collection.yml`; `.github/workflows/wm26-ehonda-ai-arena-gpt-5-5-xhigh-matchday.yml` and `.github/workflows/wm26-ehonda-ai-arena-gpt-5-5-xhigh-bonus.yml` pass `model: "gpt-5.5"`, `reasoning_effort: "xhigh"`, `max_output_tokens: 40000`, and `community_context: "ehonda-ai-arena"`; posting credentials use `EHONDA_AI_ARENA_GPT_5_5_XHIGH_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_5_XHIGH_KICKTIPP_PASSWORD` | Manual dispatch only; no prediction schedule activated; shared `wm26-ehonda-ai-arena-context-collection.yml` remains the self-contained context source; not a production config | Documented provisional match estimate: `N=104: $9.845420000000` in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md). The exact model/reasoning row exists, and this workflow now passes the required non-default `max_output_tokens: 40000`; the current base estimate still uses the generic `langfuse-o3-poc` prompt route rather than a WM-hosted base sample. Suitable for testing-only onboarding, not a production model decision. |
| `ehonda-ai-arena` self-contained workflow test | `fifa-world-cup-2026` | `gpt-5.4-nano` with `none` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | Shared self-contained context workflow `.github/workflows/wm26-ehonda-ai-arena-context-collection.yml`; `.github/workflows/wm26-ehonda-ai-arena-gpt-5-4-nano-none-matchday.yml` and `.github/workflows/wm26-ehonda-ai-arena-gpt-5-4-nano-none-bonus.yml` pass `model: "gpt-5.4-nano"`, `reasoning_effort: "none"`, `max_output_tokens: 10000`, and `community_context: "ehonda-ai-arena"`; posting credentials use `EHONDA_AI_ARENA_GPT_5_4_NANO_NONE_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_4_NANO_NONE_KICKTIPP_PASSWORD` | Manual dispatch only; no prediction schedule activated; shared `wm26-ehonda-ai-arena-context-collection.yml` remains the self-contained context source; not a production config | Documented provisional match estimate: `N=104: $0.037315720000` in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md). The exact model/reasoning row exists, but its current base estimate uses the generic `langfuse-o3-poc` prompt route at `10000` max output tokens rather than a WM-hosted base sample. Suitable for testing-only onboarding, not a production model decision. |
| `ehonda-ai-arena` self-contained workflow comparison | `fifa-world-cup-2026` | `o3` with `medium` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | Shared self-contained context workflow `.github/workflows/wm26-ehonda-ai-arena-context-collection.yml`; `.github/workflows/wm26-ehonda-ai-arena-o3-medium-matchday.yml` and `.github/workflows/wm26-ehonda-ai-arena-o3-medium-bonus.yml` pass `model: "o3"`, `reasoning_effort: "medium"`, `max_output_tokens: 10000`, and `community_context: "ehonda-ai-arena"`; posting credentials use `EHONDA_AI_ARENA_O3_MEDIUM_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_O3_MEDIUM_KICKTIPP_PASSWORD` | Manual dispatch only; no prediction schedule activated; shared `wm26-ehonda-ai-arena-context-collection.yml` remains the self-contained context source; not a production config | Documented provisional match estimate: `N=104: $1.085406400000` in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md). The exact model/reasoning row exists, but its current base estimate reuses generic local `prompt-v1` evidence at `10000` max output tokens rather than a WM-hosted base sample. Suitable for comparison planning, not exact runtime-cost coverage. |
| WM26 production predictions: `rabetrabauken2026` primary/reference model, `ehonda-ai-arena` secondary copy-posting target | `fifa-world-cup-2026` | `o3` with `high` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | Scheduled reference context workflow `.github/workflows/rabetrabauken2026-context-collection.yml`; primary `.github/workflows/wm26-rabetrabauken2026-o3-high-matchday.yml` and `.github/workflows/wm26-rabetrabauken2026-o3-high-bonus.yml` pass `model: "o3"`, `reasoning_effort: "high"`, `max_output_tokens: 40000`, and `community_context: "rabetrabauken2026"` with `RABETRABAUKEN2026_O3_HIGH_KICKTIPP_USERNAME` / `RABETRABAUKEN2026_O3_HIGH_KICKTIPP_PASSWORD`; secondary `.github/workflows/wm26-ehonda-ai-arena-o3-high-matchday.yml` and `.github/workflows/wm26-ehonda-ai-arena-o3-high-bonus.yml` use the same model/reasoning/cap with `community: "ehonda-ai-arena"` and `community_context: "rabetrabauken2026"` plus `EHONDA_AI_ARENA_O3_HIGH_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_O3_HIGH_KICKTIPP_PASSWORD` | Scheduled and manual dispatch enabled: reference context at `47 23,6,11 * * *`, primary matchday at `37 0,7,12 * * *`, primary bonus at `47 0,7,12 * * *`, and secondary copy-posting matchday plus bonus at `7 1,8,13 * * *` | Documented provisional match estimate: `N=104: $2.499338400000` in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md). The selected workflow cap is `40000`, but the current estimate row still reuses generic local `prompt-v1` evidence at `10000` max output tokens, so treat it as planning coverage rather than exact WM-hosted cap-matched proof. |

WM26 production prediction workflows are now onboarded for `o3 high`. The
scheduled self-contained `ehonda-ai-arena` `gpt-5-nano` / `minimal` workflows
remain active alongside them because they use a separate posting identity and
keep `community_context: "ehonda-ai-arena"`. The `gpt-5.5 none`,
`gpt-5.5 xhigh`, `gpt-5.4-nano none`, and `o3 medium` workflows remain
manual-only comparison entrypoints. This onboarding wave is otherwise finished
for now.

For the selected secondary-community copy pattern, only `o3 high` uses
`community_context: "rabetrabauken2026"`. Its primary `rabetrabauken2026`
workflow must run first, and the matching `ehonda-ai-arena` workflow must run
later with the same model configuration so it reuses the stored reference
prediction before posting to `ehonda-ai-arena`. Self-contained
`ehonda-ai-arena` workflows, dev shortcuts, and unrelated WM26 model tests must
keep `community_context` aligned with their own collected context unless a new
row here explicitly documents a copy-posting setup.

## 2026-06-07 Recent-History Played_At Header Repair

- `wm26-recent-history apply-date-map --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --input data/wm26/recent-history/recent-history-match-dates.csv --dry-run --verbose` found 48 recent-history documents with complete strict map coverage. The dry-run would save all 48 documents because the prompt-facing header needed to migrate from legacy `Data_Collected_At` to `Played_At`; `recent-history-kanada.csv` also had one played-date value correction.
- The same `ehonda-dev-wm26` command without `--dry-run` saved 48 migrated documents. A follow-up strict dry-run reported `0` documents would be saved and `48` unchanged.
- `wm26-recent-history apply-date-map --community-context ehonda-ai-arena --competition fifa-world-cup-2026 --input data/wm26/recent-history/recent-history-match-dates.csv --dry-run --verbose` found 16 recent-history documents with complete strict map coverage. The dry-run would save all 16 documents for the same `Played_At` header migration.
- The same `ehonda-ai-arena` command without `--dry-run` saved 16 migrated documents. A follow-up strict dry-run reported `0` documents would be saved and `16` unchanged.
- `rabetrabauken2026` still had no WM26 recent-history context documents. Guarded workflow mode with `--apply-known-only --preserve-collected-on-or-after 2026-06-11 --dry-run --verbose` exited successfully with `No recent-history documents found`.

## 2026-06-06 Recent-History Date Repair

- `wm26-recent-history apply-date-map --community-context ehonda-ai-arena --competition fifa-world-cup-2026 --input data/wm26/recent-history/recent-history-match-dates.csv --dry-run --verbose` found 16 recent-history documents with complete strict map coverage.
- The same command without `--dry-run` saved corrected `ehonda-ai-arena` versions for those 16 documents. A follow-up strict dry-run reported `0` documents would be saved and `16` unchanged.
- `rabetrabauken2026` had no WM26 recent-history context documents to repair. Strict mode reported `No recent-history documents found`; guarded workflow mode with `--apply-known-only --preserve-collected-on-or-after 2026-06-11 --dry-run --verbose` exited successfully with the same no-doc status.

## 2026-06-03 Final Lineup Refresh Validation

This validation used the official FIFA final lineup seed collected on
2026-06-03 and the local `data/wm26/lineups/private/data/transfermarkt-datasets.duckdb`
snapshot for supplemental data.

- `collect-context lineups --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --duckdb-path data/wm26/lineups/private/data/transfermarkt-datasets.duckdb --verbose` uploaded 48 final lineup context documents and KPI document `lineups` version 6. The report showed `Header-only lineup context payloads: none`; remaining `Missing lineup source data` entries are unresolved supplemental age/market-value gaps from the allowed DuckDB source.
- `matchday-dev -c ehonda-dev-wm26 --verbose` generated and submitted 8 matchday 1 dev predictions. Langfuse trace `3cba2deccd0cc2da28eb4b456625afe5` was in `development`, used hosted prompt `kicktippai/wm26/predict-one-match` version 2, had `langfusePromptFallback=false`, and `openaiReasoningEffort=minimal`. All 8 `predict-match` generation observations included only the two participating teams' `lineup-*` documents.
- `bonus-dev -c ehonda-dev-wm26 --verbose` generated and submitted 15 dev bonus predictions. Langfuse trace `709b512d4c76cb9e7dc95a1d3b75a6f3` was in `development`, used hosted prompt `kicktippai/wm26/predict-bonus` version 1, had `langfusePromptFallback=false`, and `openaiReasoningEffort=minimal`. Observation `1639778769167f17` for `Welche Mannschaft stellt den Spieler mit den meisten Toren?` included KPI documents `fifa-rankings` and `lineups`; the other 14 bonus generation observations included `fifa-rankings` only.

## 2026-06-02 Preliminary Validation

The `rabetrabauken2026` checks below are historical context collected before
the selected `o3 high` production copy-posting workflows were added. They are
not a reusable setup for the scheduled self-contained `ehonda-ai-arena`
`gpt-5-nano` / `minimal` path, which uses self-contained `ehonda-ai-arena`
context.

- `collect-context kicktipp --community-context rabetrabauken2026 --competition fifa-world-cup-2026 --verbose` authenticated and collected matchday 1 outcome rows, but the Kicktipp submission page exposed no current match table, so no standings, rules, or recent-history context documents were uploaded.
- `collect-context fifa --community-context rabetrabauken2026 --competition fifa-world-cup-2026 --verbose` uploaded 48 per-team ranking documents and KPI document `fifa-rankings`.
- `collect-context lineups --community-context rabetrabauken2026 --competition fifa-world-cup-2026 --duckdb-path data/wm26/lineups/private/data/transfermarkt-datasets.duckdb --verbose` uploaded 48 provisional lineup documents and KPI document `lineups` at that time; the seed has since been replaced with official final FIFA squads.
- `wm26-recent-history apply-date-map --community-context rabetrabauken2026 --competition fifa-world-cup-2026 --input data/wm26/recent-history/recent-history-match-dates.csv --dry-run` found no recent-history documents to update.
- `verify gpt-5-nano --community ehonda-ai-arena --community-context rabetrabauken2026 --competition fifa-world-cup-2026 --reasoning-effort minimal --init-matchday --check-outdated --agent --verbose` authenticated and resolved the intended model config, but Kicktipp exposed no match table.
- `verify-bonus gpt-5-nano --community ehonda-ai-arena --community-context rabetrabauken2026 --competition fifa-world-cup-2026 --reasoning-effort minimal --check-outdated --agent --verbose` authenticated and resolved the intended model config, but Kicktipp exposed no open bonus questions.

## Activation Checklist

- Confirm the community resolves to `fifa-world-cup-2026`.
- Record the exact model, reasoning effort, prompt source, prompt name, prompt
  label or version, max output token cap, and service tier policy.
- Document where the configuration is wired: code defaults, workflow files,
  manual command docs, or community-specific configuration.
- Verify scheduled prediction workflows pass both the selected model and
  `reasoning_effort` input explicitly.
- When a configuration requires a non-default output cap, pass
  `max_output_tokens` explicitly in the workflow file.
- Confirm the model-specific Kicktipp posting identity has manual membership in
  every target community before trusting scheduled runs.
- Check [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md)
  for the same model and reasoning effort. If it is absent, run the
  `estimate-experiment-cost-skill` workflow before activation.
- Keep context collection scheduled before prediction workflows. During
  testing-only onboarding, either leave schedules inactive or record the exact
  scheduled onboarding cadence here.
- Verify WM26 context workflows apply the guarded recent-history date map after
  Kicktipp collection and before prediction workflows rely on refreshed context.
- Once schedules are activated, update this ledger with the workflow file paths
  and activation date.

# WM26 Model Configuration Onboarding

Updated: 2026-06-06

This ledger tracks which FIFA World Cup 2026 model configurations are
onboarded, where they are wired, and whether their full-competition match
prediction costs are documented.

The guarded `matchday-dev` and `bonus-dev` `gpt-5-nano` / `minimal` defaults
are for dev work and low-cost manual testing only. They are not the WM26
production model configuration.
Production is still TBD and must be added here explicitly before scheduled
prediction workflows are activated.
The `ehonda-ai-arena` `gpt-5-nano` / `minimal` entry below is a preliminary
manual workflow onboarding test. It is not the WM26 production model decision.
The `ehonda-ai-arena` `gpt-5.5` / `none`, `gpt-5.5` / `xhigh`, and
`gpt-5.4-nano` / `none` entries below are additional self-contained manual-only
onboarding tests. They are not the WM26 production model decision.
Langfuse prompt lookup on 2026-06-02 resolved
`kicktippai/wm26/predict-one-match` label `latest` to version `2`; label
`production` was not present, so `latest` is the configured hosted WM route for
the preliminary estimate.

Use one row per effective model configuration: model, reasoning effort, prompt
route, prompt label/version policy, community, and workflow status. Before
activating a scheduled prediction workflow, make sure the matching cost estimate
is present in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md).

## Current WM26 Configurations

| Community / use | Competition | Model config | Prompt route | Where onboarded | Workflow status | Full-competition estimate |
| --- | --- | --- | --- | --- | --- | --- |
| `ehonda-dev-wm26` dev/testing shortcuts | `fifa-world-cup-2026` | `gpt-5-nano` with `minimal` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | Guarded `matchday-dev` and `bonus-dev` defaults in `src/Orchestrator/Commands/Operations/Dev/DevParticipationCommandSupport.cs` and prompt defaults in `src/Orchestrator/Infrastructure/CompetitionResolver.cs`; docs in `docs/onboarding-wm26/README.md` | Manual dev commands only; no GitHub Actions schedule activated; not a production config | Documented preliminary match estimate: `N=104: $0.008894080000` in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md). Base row uses hosted WM prompt label `latest` version `2` and historical `pes-squad` repeated-match-slice fixtures. |
| `ehonda-ai-arena` preliminary self-contained workflow test | `fifa-world-cup-2026` | `gpt-5-nano` with `minimal` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | `.github/workflows/wm26-ehonda-ai-arena-context-collection.yml` collects `community_context: "ehonda-ai-arena"`; `.github/workflows/wm26-ehonda-ai-arena-gpt-5-nano-minimal-matchday.yml` and `.github/workflows/wm26-ehonda-ai-arena-gpt-5-nano-minimal-bonus.yml` pass `model: "gpt-5-nano"`, `reasoning_effort: "minimal"`, and `community_context: "ehonda-ai-arena"`; posting credentials use `EHONDA_AI_ARENA_GPT_5_NANO_MINIMAL_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_NANO_MINIMAL_KICKTIPP_PASSWORD`; `src/Orchestrator/Infrastructure/CompetitionResolver.cs` resolves `ehonda-ai-arena` to WM26; lineup seed now uses FIFA's official final squads | Manual dispatch and scheduled cadence enabled for preliminary onboarding: context at `47 23,6,11 * * *`, matchday at `37 0,7,12 * * *`, bonus at `47 0,7,12 * * *`; not a production config | Documented preliminary match estimate: `N=104: $0.008894080000` in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md). The estimate is suitable for onboarding workflow testing, not a production model decision. |
| `ehonda-ai-arena` self-contained workflow test | `fifa-world-cup-2026` | `gpt-5.5` with `none` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | Shared self-contained context workflow `.github/workflows/wm26-ehonda-ai-arena-context-collection.yml`; `.github/workflows/wm26-ehonda-ai-arena-gpt-5-5-none-matchday.yml` and `.github/workflows/wm26-ehonda-ai-arena-gpt-5-5-none-bonus.yml` pass `model: "gpt-5.5"`, `reasoning_effort: "none"`, `max_output_tokens: 10000`, and `community_context: "ehonda-ai-arena"`; posting credentials use `EHONDA_AI_ARENA_GPT_5_5_NONE_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_5_NONE_KICKTIPP_PASSWORD` | Manual dispatch only; no prediction schedule activated; shared `wm26-ehonda-ai-arena-context-collection.yml` remains the self-contained context source; not a production config | Documented provisional match estimate: `N=104: $0.905060000000` in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md). The exact model/reasoning row exists, but its current base estimate uses the generic `langfuse-o3-poc` prompt route at `10000` max output tokens rather than a WM-hosted base sample. Suitable for testing-only onboarding, not a production model decision. |
| `ehonda-ai-arena` self-contained workflow test | `fifa-world-cup-2026` | `gpt-5.5` with `xhigh` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | Shared self-contained context workflow `.github/workflows/wm26-ehonda-ai-arena-context-collection.yml`; `.github/workflows/wm26-ehonda-ai-arena-gpt-5-5-xhigh-matchday.yml` and `.github/workflows/wm26-ehonda-ai-arena-gpt-5-5-xhigh-bonus.yml` pass `model: "gpt-5.5"`, `reasoning_effort: "xhigh"`, `max_output_tokens: 40000`, and `community_context: "ehonda-ai-arena"`; posting credentials use `EHONDA_AI_ARENA_GPT_5_5_XHIGH_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_5_XHIGH_KICKTIPP_PASSWORD` | Manual dispatch only; no prediction schedule activated; shared `wm26-ehonda-ai-arena-context-collection.yml` remains the self-contained context source; not a production config | Documented provisional match estimate: `N=104: $9.845420000000` in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md). The exact model/reasoning row exists, and this workflow now passes the required non-default `max_output_tokens: 40000`; the current base estimate still uses the generic `langfuse-o3-poc` prompt route rather than a WM-hosted base sample. Suitable for testing-only onboarding, not a production model decision. |
| `ehonda-ai-arena` self-contained workflow test | `fifa-world-cup-2026` | `gpt-5.4-nano` with `none` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | Shared self-contained context workflow `.github/workflows/wm26-ehonda-ai-arena-context-collection.yml`; `.github/workflows/wm26-ehonda-ai-arena-gpt-5-4-nano-none-matchday.yml` and `.github/workflows/wm26-ehonda-ai-arena-gpt-5-4-nano-none-bonus.yml` pass `model: "gpt-5.4-nano"`, `reasoning_effort: "none"`, `max_output_tokens: 10000`, and `community_context: "ehonda-ai-arena"`; posting credentials use `EHONDA_AI_ARENA_GPT_5_4_NANO_NONE_KICKTIPP_USERNAME` / `EHONDA_AI_ARENA_GPT_5_4_NANO_NONE_KICKTIPP_PASSWORD` | Manual dispatch only; no prediction schedule activated; shared `wm26-ehonda-ai-arena-context-collection.yml` remains the self-contained context source; not a production config | Documented provisional match estimate: `N=104: $0.037315720000` in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md). The exact model/reasoning row exists, but its current base estimate uses the generic `langfuse-o3-poc` prompt route at `10000` max output tokens rather than a WM-hosted base sample. Suitable for testing-only onboarding, not a production model decision. |
| WM26 production predictions: `rabetrabauken2026` primary/reference model, `ehonda-ai-arena` secondary copy-posting target | `fifa-world-cup-2026` | TBD; must be selected separately from the preliminary testing fallback | TBD | Reference context collection is wired in `.github/workflows/rabetrabauken2026-context-collection.yml`; production model-specific matchday/bonus workflows are intentionally not activated yet. The copy-from-primary pattern is valid only for the selected `rabetrabauken2026` production model and its matching `ehonda-ai-arena` posting workflow. | Context workflow is manual-only; production prediction schedules are not activated | Missing until production model and reasoning effort are selected. |

No WM26 production prediction workflow is currently activated. The preliminary
`ehonda-ai-arena` `gpt-5-nano` / `minimal` workflows are scheduled onboarding
entrypoints, and the `gpt-5.5 none`, `gpt-5.5 xhigh`, and `gpt-5.4-nano none`
workflows are manual-only onboarding entrypoints. None of them is the WM26
production model decision. When adding bulk activation workflows, list every
community/model pair here before enabling schedules.

For the planned secondary-community copy pattern, the selected production model
must run first against `rabetrabauken2026`. The matching `ehonda-ai-arena`
workflow must run later with the same model configuration and
`community_context: rabetrabauken2026`, so it reuses the stored reference
prediction before posting to `ehonda-ai-arena`. This rule applies only to the
yet-undetermined `rabetrabauken2026` production model path. Preliminary
`ehonda-ai-arena` workflows, dev shortcuts, and any unrelated WM26 model tests
must keep `community_context` aligned with their own collected context unless a
new row here explicitly documents a copy-posting production setup.

## 2026-06-03 Final Lineup Refresh Validation

This validation used the official FIFA final lineup seed collected on
2026-06-03 and the local `data/wm26/lineups/private/data/transfermarkt-datasets.duckdb`
snapshot for supplemental data.

- `collect-context lineups --community-context ehonda-dev-wm26 --competition fifa-world-cup-2026 --duckdb-path data/wm26/lineups/private/data/transfermarkt-datasets.duckdb --verbose` uploaded 48 final lineup context documents and KPI document `lineups` version 6. The report showed `Header-only lineup context payloads: none`; remaining `Missing lineup source data` entries are unresolved supplemental age/market-value gaps from the allowed DuckDB source.
- `matchday-dev -c ehonda-dev-wm26 --verbose` generated and submitted 8 matchday 1 dev predictions. Langfuse trace `3cba2deccd0cc2da28eb4b456625afe5` was in `development`, used hosted prompt `kicktippai/wm26/predict-one-match` version 2, had `langfusePromptFallback=false`, and `openaiReasoningEffort=minimal`. All 8 `predict-match` generation observations included only the two participating teams' `lineup-*` documents.
- `bonus-dev -c ehonda-dev-wm26 --verbose` generated and submitted 15 dev bonus predictions. Langfuse trace `709b512d4c76cb9e7dc95a1d3b75a6f3` was in `development`, used hosted prompt `kicktippai/wm26/predict-bonus` version 1, had `langfusePromptFallback=false`, and `openaiReasoningEffort=minimal`. Observation `1639778769167f17` for `Welche Mannschaft stellt den Spieler mit den meisten Toren?` included KPI documents `fifa-rankings` and `lineups`; the other 14 bonus generation observations included `fifa-rankings` only.

## 2026-06-02 Preliminary Validation

The `rabetrabauken2026` checks below are historical context for the
not-yet-selected production copy-posting path. They are not a reusable setup for
the preliminary `ehonda-ai-arena` `gpt-5-nano` / `minimal` test, which now uses
self-contained `ehonda-ai-arena` context.

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
- Check [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md)
  for the same model and reasoning effort. If it is absent, run the
  `estimate-experiment-cost-skill` workflow before activation.
- Keep context collection scheduled before prediction workflows. During
  testing-only onboarding, either leave schedules inactive or record the exact
  scheduled onboarding cadence here.
- Once schedules are activated, update this ledger with the workflow file paths
  and activation date.

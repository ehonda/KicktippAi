# WM26 Model Configuration Onboarding

Updated: 2026-06-02

This ledger tracks which FIFA World Cup 2026 model configurations are
onboarded, where they are wired, and whether their full-competition match
prediction costs are documented. Scheduled activation is intentionally pending
while testing starts.

The guarded `matchday-dev` and `bonus-dev` `gpt-5-nano` / `minimal` defaults
are for dev work and low-cost manual testing only. They are not the WM26
production model configuration.
Production is still TBD and must be added here explicitly before scheduled
prediction workflows are activated.
The `ehonda-ai-arena` `gpt-5-nano` / `minimal` entry below is a preliminary
manual workflow onboarding test. It is not the WM26 production model decision.

Use one row per effective model configuration: model, reasoning effort, prompt
route, prompt label/version policy, community, and workflow status. Before
activating a scheduled prediction workflow, make sure the matching cost estimate
is present in [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md).

## Current WM26 Configurations

| Community / use | Competition | Model config | Prompt route | Where onboarded | Workflow status | Full-competition estimate |
| --- | --- | --- | --- | --- | --- | --- |
| `ehonda-dev-wm26` dev/testing shortcuts | `fifa-world-cup-2026` | `gpt-5-nano` with `minimal` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | Guarded `matchday-dev` and `bonus-dev` defaults in `src/Orchestrator/Commands/Operations/Dev/DevParticipationCommandSupport.cs` and prompt defaults in `src/Orchestrator/Infrastructure/CompetitionResolver.cs`; docs in `docs/onboarding-wm26/README.md` | Manual dev commands only; no GitHub Actions schedule activated; not a production config | Missing. `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104 --model gpt-5-nano --reasoning-effort minimal` reported no matching base estimate row on 2026-06-02. |
| `ehonda-ai-arena` preliminary secondary-posting workflow test | `fifa-world-cup-2026` | `gpt-5-nano` with `minimal` reasoning | Langfuse `kicktippai/wm26/predict-one-match` and `kicktippai/wm26/predict-bonus`, label `latest`; fallback model `wm26` | `.github/workflows/ehonda-ai-arena-gpt-5-nano-matchday.yml` and `.github/workflows/ehonda-ai-arena-gpt-5-nano-bonus.yml` pass `model: "gpt-5-nano"`, `reasoning_effort: "minimal"`, and `community_context: "rabetrabauken2026"`; `src/Orchestrator/Infrastructure/CompetitionResolver.cs` resolves `ehonda-ai-arena` to WM26; provisional lineup seed stays in use until official final squads are available | Manual dispatch enabled for preliminary testing; cron schedules remain disabled; run and validate `rabetrabauken2026` context first; not a production config | Missing. The same estimator command reported no matching base estimate row on 2026-06-02, so no dollar estimate is recorded. |
| WM26 production predictions: `rabetrabauken2026` reference, `ehonda-ai-arena` secondary copy-posting target | `fifa-world-cup-2026` | TBD; must be selected separately from the preliminary testing fallback | TBD | Reference context collection is wired in `.github/workflows/rabetrabauken2026-context-collection.yml`; production model-specific matchday/bonus workflows are intentionally not activated yet | Context workflow is manual-only; production prediction schedules are not activated | Missing until production model and reasoning effort are selected. |

No WM26 production prediction workflow is currently activated. The preliminary
`ehonda-ai-arena` `gpt-5-nano` / `minimal` workflows are manual-only testing
entrypoints. When adding bulk activation workflows, list every community/model
pair here before enabling schedules.

For the planned secondary-community copy pattern, the selected production model
must run first against `rabetrabauken2026`. The matching `ehonda-ai-arena`
workflow must run later with the same model configuration and
`community_context: rabetrabauken2026`, so it reuses the stored reference
prediction before posting to `ehonda-ai-arena`.

## 2026-06-02 Preliminary Validation

- `collect-context kicktipp --community-context rabetrabauken2026 --competition fifa-world-cup-2026 --verbose` authenticated and collected matchday 1 outcome rows, but the Kicktipp submission page exposed no current match table, so no standings, rules, or recent-history context documents were uploaded.
- `collect-context fifa --community-context rabetrabauken2026 --competition fifa-world-cup-2026 --verbose` uploaded 48 per-team ranking documents and KPI document `fifa-rankings`.
- `collect-context lineups --community-context rabetrabauken2026 --competition fifa-world-cup-2026 --duckdb-path data/wm26/lineups/private/data/transfermarkt-datasets.duckdb --verbose` uploaded 48 provisional lineup documents and KPI document `lineups`; the provisional seed still has header-only docs for Algeria, Australia, Ecuador, Mexico, Paraguay, and Uruguay.
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
- Check [whole-season-cost-estimates.md](../experiments/whole-season-cost-estimates.md)
  for the same model and reasoning effort. If it is absent, run the
  `estimate-experiment-cost-skill` workflow before activation.
- Keep context collection scheduled before prediction workflows. During
  testing-only onboarding, leave schedules inactive and record that status here.
- Once schedules are activated, update this ledger with the workflow file paths
  and activation date.

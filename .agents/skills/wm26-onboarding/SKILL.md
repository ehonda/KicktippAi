---
name: wm26-onboarding
description: Onboard KicktippAi FIFA World Cup 2026 communities end to end. Use when preparing WM26 community context, generating and uploading mandatory lineup documents, seeding Firestore context/KPI documents, applying the recent-history played-date map, documenting onboarded model configurations and season-cost estimate coverage, validating matchday-dev or bonus-dev, inspecting Langfuse traces, or committing and pushing WM26 onboarding changes.
---

# WM26 Onboarding

## Overview

This skill coordinates the operational workflow for FIFA World Cup 2026 KicktippAi communities. It keeps the checked-in WM26 data, mandatory lineup documents, Firestore context documents, onboarded model configuration docs, required Kicktipp community membership for the posting identity, full-competition estimate coverage, dev prediction validation, Langfuse trace review, and closeout steps aligned.

## Workflow

1. Confirm the target community and competition.
   - Dev communities must be listed in `CompetitionResolver.SupportedDevCommunities`.
   - WM26 communities should resolve to `fifa-world-cup-2026`.
   - Treat the `matchday-dev` and `bonus-dev` defaults, `gpt-5-nano` with `minimal` reasoning, as low-cost dev/test defaults only. They are not the WM26 production model configuration.
   - Production or scheduled prediction workflows must pass an explicit model and reasoning effort once the production configuration is selected. Use the prediction workflow `reasoning_effort` input for that wiring.
   - Run `dotnet` and `git` commands outside the sandbox in this repository.

2. Seed context for a supported dev community.

```powershell
dotnet run --project src/Orchestrator -- collect-context-dev -c ehonda-dev-wm26 --verbose
```

Use `--matchdays`, `--dry-run`, and `--verbose` as needed. The command collects Kicktipp context, fetches live WM26 FIFA rankings, refreshes WM26 lineup context from the tracked seed and current Transfermarkt DuckDB snapshot, and uploads the required context/KPI documents to Firestore.

3. For non-dev or explicit workflows, run the three collection paths separately.

```powershell
dotnet run --project src/Orchestrator -- collect-context kicktipp --community-context <community-context> --competition fifa-world-cup-2026
dotnet run --project src/Orchestrator -- collect-context fifa --community-context <community-context> --competition fifa-world-cup-2026
dotnet run --project src/Orchestrator -- collect-context lineups --community-context <community-context> --competition fifa-world-cup-2026
```

4. Create and record the workflow activation plan.

For every GitHub Actions-backed WM26 configuration, create all required
workflow entrypoints together: context collection, matchday predictions, and
bonus predictions. Use WM26-specific filenames such as
`wm26-<community>-context-collection.yml` and
`wm26-<community>-<model>-<reasoning-effort>-matchday.yml`, and include a
`🏆` marker in each workflow `name:` so WM26 workflows are visually distinct in
the GitHub Actions UI. Do not reuse ambiguous Bundesliga-era workflow names for
new WM26 configurations.

Every WM26 workflow entrypoint should keep `workflow_dispatch` enabled for
manual validation. If scheduled activation is not requested yet, keep cron
schedules inactive and record that status in
`docs/onboarding-wm26/model-config-onboarding.md`. If scheduled activation is
requested, use the planned WM26 cadence: context collection at
`47 23,6,11 * * *`, main matchday predictions at `37 0,7,12 * * *`, slower or
secondary copy-posting predictions at `7 1,8,13 * * *`, and bonus predictions
at `47 0,7,12 * * *`. Before enabling scheduled matchday or bonus prediction
workflows for a new WM26 community, activate and successfully run the matching
context workflow first. The community context workflow must run Kicktipp context
collection, `collect-context fifa`, and `collect-context lineups` with
`--competition fifa-world-cup-2026`.

5. Document onboarded model configurations.

Update `docs/onboarding-wm26/model-config-onboarding.md` for every effective WM26 configuration. Record the community, competition, model, reasoning effort, prompt source, prompt name, prompt label or version policy, fallback prompt route, where the configuration is wired, workflow files if present, activation status, and season-cost estimate status. Treat code defaults, manual dev shortcuts, and scheduled workflow files as separate onboarding locations when they differ.

6. Manually join the target Kicktipp community with the onboarded model configuration.

Automation cannot accept community invites or join Kicktipp communities on its own. Ensure the exact posting account or prediction identity used by the onboarded model configuration has joined the target community before prediction validation or scheduled activation. Record the join status in `docs/onboarding-wm26/model-config-onboarding.md` and treat any missing join as a blocking manual follow-up.

7. Ensure full-competition match prediction costs are documented.

Use the `whole-season-estimates` skill and the `estimate-experiment-cost-skill` workflow for each onboarded model configuration. Check `docs/experiments/whole-season-cost-estimates.md` for an exact model and reasoning-effort entry before activation. For WM26, estimate `104` match predictions and assume no repredictions unless stronger evidence is documented; bonus-question cost remains excluded unless the user explicitly asks to estimate it. Run estimates only through:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 104 --model <model> --reasoning-effort <effort>
```

If the estimator reports no matching base row, do not invent or hand-calculate a cost. Follow the `estimate-experiment-cost-skill` base-estimate workflow, including confirmation before any spend, persist the row with `upsert-row`, rerun `estimate`, and document the exact command, row details, assumptions, and output in `docs/experiments/whole-season-cost-estimates.md`. Cross-link the result from `docs/onboarding-wm26/model-config-onboarding.md`.

8. Refresh mandatory lineup documents with `collect-context lineups`.

Every WM26 community needs per-team `lineup-*` context documents and the aggregate `lineups` KPI document before prediction validation. Use official FIFA lineup/squad material for membership once available, and use the CC0 `dcaribou/transfermarkt-datasets` DuckDB database as the only supplemental source. The command downloads the latest upstream DuckDB snapshot by default; use `--duckdb-path` only for local/offline runs.

FIFA published the final 26-player squad lists on 2026-06-03 after the 2026-06-02 team submission deadline. The tracked seed now uses official full-squad membership. Refresh the full-squad `lineup-*` context documents plus `lineups` KPI document with `collect-context lineups` whenever onboarding or refreshing a WM26 community. Keep full squads in context; do not switch this workflow to match-starter-only lineups.

Do not run `matchday-dev` until every match team has its required `lineup-{team}.csv` context document. Do not run `bonus-dev` until the `lineups` KPI document exists.

9. Apply the canonical recent-history date map.

```powershell
dotnet run --project src/Orchestrator -- wm26-recent-history apply-date-map --community-context <community-context> --competition fifa-world-cup-2026 --input data/wm26/recent-history/recent-history-match-dates.csv
```

Run with `--dry-run` first when changing the map.

10. Validate predictions.

```powershell
dotnet run --project src/Orchestrator -- matchday-dev -c ehonda-dev-wm26 --verbose
dotnet run --project src/Orchestrator -- bonus-dev -c ehonda-dev-wm26 --verbose
```

Acceptance checks:
- `matchday-dev` finds all required context documents in Firestore with no on-demand fallback warning, including the two participating teams' `lineup-*` docs.
- `bonus-dev` includes KPI context document `fifa-rankings` and includes `lineups` only for the exact top-scorer-team question.
- Langfuse traces show hosted prompts, `langfusePromptFallback=false`, `openaiReasoningEffort=minimal`, ranking context containing `Rank,Team,ELO,Data_Collected_At`, and lineup context containing `Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR`.

11. Inspect Langfuse traces with the repository Langfuse workflow.
   - Use the global `langfuse` skill and installed `langfuse` CLI.
   - Prefer filtering by `environment=development`, the WM26 community tag, and trace/observation names `matchday`, `bonus`, `predict-match`, or `predict-bonus`.

12. Close out.
   - Run focused tests for the changed command/provider areas.
   - Inspect `git diff` and `git status`.
   - Include a "Manual follow-ups" section in the final response for work that cannot be completed autonomously.
   - Explicitly call out whether the Kicktipp community join step is done, not needed, or still required for each onboarded model configuration.
   - Commit the intended changes.
   - Before pushing, verify branch, remotes, status, and latest commit, then push explicitly with `git push origin <branch>`.

## Manual Follow-Up Output

After autonomous onboarding, include a concise manual follow-up section in the final response. Mark each item as done, not needed, or still required. At minimum, cover:

- GitHub Actions secrets and variables: per-community Kicktipp username/password secrets used by the workflow files, `FIREBASE_PROJECT_ID`, `FIREBASE_SERVICE_ACCOUNT_JSON`, `OPENAI_API_KEY`, `LANGFUSE_SECRET_KEY`, and repository variable `LANGFUSE_PUBLIC_KEY`.
- GitHub workflow activation: whether workflows are manual-only/testing, schedules are active, schedules are still disabled, or schedules are ready to enable. Do not activate scheduled prediction workflows until the context workflow has run successfully.
- Workflow model wiring: whether each prediction workflow passes the selected model and `reasoning_effort` input explicitly; only `matchday-dev` and `bonus-dev` may rely on the guarded WM26 dev defaults.
- Production model decision: the chosen production model, reasoning effort, prompt route, and estimate status. If production is still TBD, say so plainly and do not imply that `gpt-5-nano` / `minimal` is production-ready.
- Cost estimate coverage: exact `docs/experiments/whole-season-cost-estimates.md` entry or missing estimate/base-row work for every scheduled model configuration.
- Kicktipp community membership and Firebase access: whether the posting account or prediction identity for each onboarded model configuration has joined the target community, whether it can access the community, and whether Firestore context/KPI writes were validated.
- Langfuse prompt/tracing setup: hosted prompt names/labels, fallback status, trace environment expectation, and whether Langfuse keys are configured for workflows.
- First-run checklist: manually trigger the context workflow, inspect ranking/lineup documents, then manually trigger prediction workflows before enabling cron schedules.

## Data Locations

- Per-team FIFA ranking context documents: generated live by `collect-context fifa` as `fifa-ranking-*.csv` Firestore documents
- Aggregate FIFA ranking KPI document: generated live by `collect-context fifa` as Firestore KPI document `fifa-rankings`
- Per-team lineup context documents: generated with `collect-context lineups` as `lineup-*.csv` Firestore documents
- Aggregate lineup KPI document: generated with `collect-context lineups` as Firestore KPI document `lineups`
- Tracked lineup seed and team manifest: `data/wm26/lineups/`
- Recent-history played-date map: `data/wm26/recent-history/recent-history-match-dates.csv`
- Workflow documentation: `docs/onboarding-wm26/README.md`
- Model configuration onboarding ledger: `docs/onboarding-wm26/model-config-onboarding.md`
- Full-competition cost estimates: `docs/experiments/whole-season-cost-estimates.md`

FIFA ranking CSV payloads must use `Rank,Team,ELO,Data_Collected_At`, format points with two decimal places, and must not contain empty `Data_Collected_At` values.

Lineup CSV payloads must use `Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR`, must include coaches, must use `N/A` instead of `0` for unavailable player market values, and must leave coach market values empty.

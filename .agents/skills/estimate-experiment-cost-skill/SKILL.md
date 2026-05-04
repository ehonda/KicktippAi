---
name: estimate-experiment-cost-skill
description: Estimate KicktippAi Langfuse experiment costs before running slice or repeated-match experiments. Use when Codex needs to project experiment spend from known average cost-per-match data, run or update the 5-by-4 repeated-match base estimate, calculate uncached-input cost estimates from Langfuse token usage, choose max output token caps for cost estimates, or maintain the project-scoped experiment cost estimate table.
---

# Estimate Experiment Cost

Use this skill to estimate KicktippAi `slice` and `repeated-match` experiment costs. Do not use it for `community-to-date`; that mode replays prediction history and does not currently generate model predictions during experiment runs.

Cost estimates must treat every input token as uncached, even when the data-gathering calls had cache hits. This makes estimates suitable for slice predictions and conservative for repeated-match predictions.

## Grounding

Before estimating or running a base estimate:

- Read `AGENTS.md`, `docs/langfuse.md`, and `docs/langfuse/experiments/running-experiments.md`.
- Read [references/base-estimate-table.md](references/base-estimate-table.md) before looking up or updating known values.
- Read [references/seed-evidence.md](references/seed-evidence.md) only when checking the initial seeded rows.
- Use `.agents/skills/langfuse-experiments/` for experiment preparation, execution, export, commit, and push workflow details.
- Use the official `$langfuse` skill or the installed `langfuse` command for Langfuse API inspection.
- Run commands from the repository root so Orchestrator can auto-load external secrets.
- Run every `dotnet` and `git` command outside the sandbox, as required by the repository root instructions. Use `uv` for Python commands.
- Verify the model is represented in `src/OpenAiIntegration/CostCalculationService.cs` before spending API budget. If pricing is missing or stale, update it from the official OpenAI pricing source and add focused cost-calculation coverage before running the estimate.

## Estimate Workflow

1. Normalize the planned experiment size to match predictions `N`. For `slice` and `repeated-match`, this is usually the item count.
2. Identify the model, reasoning effort, prompt source/key, evaluation policy, and max output token cap.
3. Look up the best matching row in [references/base-estimate-table.md](references/base-estimate-table.md). Prefer exact matches for model, reasoning effort, prompt route, and max output token count.
4. Use the bundled script to calculate the final estimate from the table row:

```powershell
uv run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --count 100 --model o3 --reasoning-effort medium
```

5. If no suitable row exists, create one with the base estimate method below, update the table, then run the estimate command.

Report the estimate with the row used, observed sample size, max output token cap, model knowledge cutoff date, sampling cutoff, and any mismatch between the observed row and the planned experiment.

## Base Estimate Method

Use this method only after stating the expected preflight spend and getting user confirmation, unless the user already authorized that exact base estimate.

1. Determine the model knowledge cutoff date. If it is not specified by the user and is not already known from a table row or explicit project doc, ask the user for the date instead of guessing.
2. Add a two-day safety margin to the original model knowledge cutoff date. Use the resulting date as the `prepare-slice --starts-after` sampling cutoff at local midnight in the NodaTime invariant `ZonedDateTime` format, for example `2025-12-01T00:00:00 Europe/Berlin (+01)`. Store the original model knowledge cutoff date in the table, not the safety-margin date.
3. Select five random fixtures from the eligible pool by preparing a 5-item random slice with the sampling cutoff and a recorded seed:

```powershell
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --sample-size 5 --sample-seed 20260503 --starts-after "2025-12-01T00:00:00 Europe/Berlin (+01)" --slice-key random-5-seed-20260503-cost-estimate
```

4. Read the selector manifest and prepare one repeated-match dataset for each selected fixture with `--sample-size 4`. Use clear slice keys such as `cost-estimate-<model>-<effort>-fixture-01`.
5. Sync all five repeated-match datasets.
6. Run the five repeated-match datasets in parallel. Each run must use `--batch-count 1`, the intended model, reasoning effort, prompt route, evaluation policy, and flex processing default. Use one shared UTC run stamp for the family.
7. Start with the default `--max-output-tokens` value of `10000` unless the table or prior preflight evidence already requires a higher cap. If any run item fails because the response hit the output cap, emits `OpenAI response did not contain output text`, or records `outputTokens >= maxOutputTokens`, increase the cap and rerun the complete 5-by-4 base estimate. Keep increasing and rerunning the complete sample until no item hits the cap.
8. Gather compact `predict-match` usage with the bundled script:

```powershell
uv run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py collect --env ..\KicktippAi.Secrets\src\Orchestrator\.env --group "repeated-measured=RUN_NAME_1" --group "repeated-measured=RUN_NAME_2" --group "repeated-measured=RUN_NAME_3" --group "repeated-measured=RUN_NAME_4" --group "repeated-measured=RUN_NAME_5" --expect repeated-measured=20 --output C:\tmp\kicktippai-cost-estimate-usage.json
```

9. Calculate the table row with the bundled script. The script reads model prices from `src/OpenAiIntegration/CostCalculationService.cs`, applies flex pricing, ignores cached-input discounts, validates the 20-observation sample, and fails if any output reached the configured cap:

```powershell
uv run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py base-row --input C:\tmp\kicktippai-cost-estimate-usage.json --model o3 --reasoning-effort medium --prompt-route "local prompt-v1" --model-knowledge-cutoff 2025-11-29 --sampling-cutoff "2025-12-01T00:00:00 Europe/Berlin (+01)" --max-output-tokens 10000 --source "base-estimate run family 2026-05-04"
```

10. Insert the emitted Markdown row into [references/base-estimate-table.md](references/base-estimate-table.md) and add source details to [references/seed-evidence.md](references/seed-evidence.md) or a new directly linked reference when the row needs long run-name evidence.

## Output Token Caps

- Treat `10000` as the default cap because `PredictionServiceOptions` uses `MaxOutputTokenCount = 10_000`.
- Include an explicit `--max-output-tokens` flag, run-name tag, and table value whenever a higher cap is needed.
- For reasoning-heavy configurations, run a one-item preflight with the intended prompt source and reasoning effort before launching the 5-by-4 base estimate. Use the preflight to choose the first non-default cap, then still validate the full 20-item base sample.
- Never update the table from a base estimate that has missing observations, failed items, non-flex service tier, or output-cap hits.

## Quality Checks

- Verify the table row is based on five randomly selected repeated-match fixtures with four repetitions each.
- Verify the sampling cutoff equals the stored model knowledge cutoff date plus two days.
- Verify the estimate uses `N` match predictions, not batches or fixtures.
- Verify the table costs were emitted by `scripts/experiment_cost_estimator.py`.
- Inspect the diff before staging or committing any table or skill change.

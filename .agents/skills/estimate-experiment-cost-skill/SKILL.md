---
name: estimate-experiment-cost-skill
description: Estimate KicktippAi Langfuse experiment costs before running slice or repeated-match experiments. Use when Codex needs to project spend from stored JSON base estimates, collect compact Langfuse usage, run high-reasoning preflights, choose output token caps, calculate or upsert 5-by-4 base estimate rows, or maintain the project-scoped experiment cost estimate store.
---

# Estimate Experiment Cost

Use this skill to estimate KicktippAi `slice` and `repeated-match` experiment costs. Do not use it for `community-to-date`; that mode replays prediction history and does not currently generate model predictions during experiment runs.

Treat every input token as uncached for estimates, even when the observed Langfuse calls had cache hits.

## Hard Rules

- Run commands from the repository root.
- Run bundled Python tooling as `uv --cache-dir .uv-cache run ...`.
- Run every `dotnet` and `git` command outside the sandbox, as required by `AGENTS.md`.
- Do not hand-calculate, spreadsheet, PowerShell-multiply, or mentally estimate reported costs.
- Reported estimates must come from `scripts/experiment_cost_estimator.py estimate` output.
- Final estimate answers must cite the exact `estimate --counts ...` command used.
- Use [references/base-estimates.json](references/base-estimates.json) as the authoritative store. Do not paste new rows into the retired Markdown table.
- Persist new or changed base estimate rows with `upsert-row`.
- Optional `--report-json` failures do not invalidate successful estimator stdout for `base-row` or `estimate`.
- Do not rerun predictions just because Langfuse initially shows too few observations. First rerun `collect --expect ...` and let its ingestion wait complete.

## Grounding

Before estimating or running a base estimate:

- Read `AGENTS.md`, `docs/langfuse.md`, and `docs/langfuse/experiments/running-experiments.md`.
- Read [references/base-estimates.json](references/base-estimates.json) before looking up known values.
- Read [references/preflight-evidence.md](references/preflight-evidence.md) when checking prior high-reasoning output-cap evidence.
- Read [references/seed-evidence.md](references/seed-evidence.md) only when checking initial seeded rows.
- Use `.agents/skills/langfuse-experiments/` for experiment preparation, execution, export, commit, and push workflow details.
- Do not use `docs/research/estimate-experiment-cost-skill/` for routine estimates. Treat it as historical design context only.
- Verify the model exists in `src/OpenAiIntegration/CostCalculationService.cs` before spending API budget. If pricing is missing or stale, update it from the official OpenAI pricing source and add focused cost-calculation coverage before running an estimate.

## Estimate Workflow

1. Normalize the planned experiment size to match predictions `N`. For `slice` and `repeated-match`, this is usually the item count.
2. Identify the model, reasoning effort, prompt route, evaluation policy, and max output token cap.
3. Estimate one or more counts directly from JSON:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,60,100 --model o3 --reasoning-effort medium
```

4. If the estimator reports no row for `high` or `xhigh`, stop and run the mandatory preflight gate before returning an actionable estimate.
5. If the estimator reports no row for any other config, create one with the base estimate method, persist it with `upsert-row`, then rerun `estimate`.

Report the row used, observed sample size, max output token cap, model knowledge cutoff, sampling cutoff, prompt route, and any mismatch between the observed row and the planned experiment.

If `model + reasoningEffort` matches multiple JSON rows, do not choose manually. Add explicit CLI qualifier support first.

## Mandatory Preflight Gate

Use this gate before any base estimate or actionable estimate for `high` or `xhigh` reasoning when no exact JSON row exists for the intended model and reasoning effort.

1. State the expected one-item preflight spend and get confirmation unless the user already authorized that exact preflight.
2. Use the intended model, prompt source/key, reasoning effort, evaluation policy, service tier default, and a one-item dataset.
3. Start with `--max-output-tokens 10000` only when no JSON row or prior preflight evidence indicates a higher cap. If evidence already requires a higher cap, include that cap in the first preflight command and run name.
4. Collect the preflight usage with ingestion waiting:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py collect --env ..\KicktippAi.Secrets\src\Orchestrator\.env --group "repeated-measured=RUN_NAME" --expect repeated-measured=1 --output C:\tmp\kicktippai-cost-preflight-usage.json
```

5. Report input tokens, output tokens, reasoning tokens, service tier, observed cost, cap used, and whether the output hit or approached the cap. Record reusable cap/cost evidence in [references/preflight-evidence.md](references/preflight-evidence.md), not in the JSON base estimate store.
6. Treat missing observations as failed predictions only if Orchestrator failed, run logs show item failures, cap exhaustion/no-output errors are present, or the collector times out after its ingestion wait.
7. If the preflight fails due to output cap exhaustion, no output text, or `outputTokens >= maxOutputTokens`, increase the cap and rerun the one-item preflight before launching a 5-by-4 base estimate.

## Base Estimate Method

Use this method only after stating the expected spend and getting user confirmation, unless the user already authorized that exact base estimate.

1. Determine the model knowledge cutoff date. If it is not specified by the user and is not already known from JSON or project docs, ask instead of guessing.
2. Add a two-day safety margin to the model knowledge cutoff date. Use that date as `prepare-slice --starts-after` at local midnight in NodaTime invariant `ZonedDateTime` format, for example `2025-12-01T00:00:00 Europe/Berlin (+01)`. Store the original model knowledge cutoff date in JSON.
3. Select five random fixtures from the eligible pool with a recorded seed:

```powershell
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --sample-size 5 --sample-seed 20260503 --starts-after "2025-12-01T00:00:00 Europe/Berlin (+01)" --slice-key random-5-seed-20260503-cost-estimate
```

4. Read the selector manifest's `items` array. Use `homeTeam`, `awayTeam`, and `matchday` to prepare one repeated-match dataset per fixture with `--sample-size 4`.
5. Sync exactly those five emitted repeated-match datasets.
6. Run the five repeated-match manifests in parallel with PowerShell `Start-Job`. Use one shared UTC run stamp, `--batch-count 1`, the intended model, reasoning effort, prompt route, evaluation policy, flex processing default, and `--replace-run`.
7. Use `--evaluation-policy-kind relative --evaluation-policy-offset -12:00:00` for the default slice-like policy, or the intended exact `--evaluation-time` when the planned estimate requires exact-time execution.
8. Start with the default `--max-output-tokens 10000` unless JSON or prior preflight evidence requires a higher cap. If any run item fails because of cap exhaustion, no output text, or `outputTokens >= maxOutputTokens`, increase the cap and rerun the complete 5-by-4 base estimate.
9. Collect compact usage with all five run names and the default ingestion wait:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py collect --env ..\KicktippAi.Secrets\src\Orchestrator\.env --group "repeated-measured=RUN_NAME_1" --group "repeated-measured=RUN_NAME_2" --group "repeated-measured=RUN_NAME_3" --group "repeated-measured=RUN_NAME_4" --group "repeated-measured=RUN_NAME_5" --expect repeated-measured=20 --output C:\tmp\kicktippai-cost-estimate-usage.json
```

10. Persist the row with `upsert-row`. The command validates 20 observations, flex tier, uncached input pricing, and no output-cap hits before writing JSON:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py upsert-row --input C:\tmp\kicktippai-cost-estimate-usage.json --model o3 --reasoning-effort medium --prompt-route "local prompt-v1" --model-knowledge-cutoff 2025-11-29 --sampling-cutoff "2025-12-01T00:00:00 Europe/Berlin (+01)" --max-output-tokens 10000 --source "base-estimate run family 2026-05-04"
```

Use `--replace` only when intentionally updating an existing row for the same model and reasoning effort.

## Output Token Caps

- Treat `10000` as the default cap because `PredictionServiceOptions` uses `MaxOutputTokenCount = 10_000`.
- Include an explicit `--max-output-tokens` flag and run-name tag whenever a higher cap is needed.
- For reasoning-heavy configs, use the preflight to choose the first non-default cap, then still validate the full 20-item base sample.
- Never update JSON from a base estimate with missing observations, failed items, non-flex service tier, or output-cap hits.

## Closeout

If `base-estimates.json`, this skill, or supporting evidence changes, commit and push the intended files before the final response. Do not stage generated experiment artifacts, `C:\tmp` files, or unrelated workspace changes.

Before requesting approval for push, record the exact target:

```powershell
git branch --show-current
git remote -v
git status --short --branch
git log -1 --oneline
```

Push with an explicit remote and branch:

```powershell
git push origin CURRENT_BRANCH
```

## Quality Checks

- Verify each stored row is based on five randomly selected repeated-match fixtures with four repetitions each.
- Verify the sampling cutoff equals the stored model knowledge cutoff date plus two days.
- Verify estimates use `N` match predictions, not batches or fixtures.
- Verify all reported estimate totals come from `experiment_cost_estimator.py estimate`.
- Inspect the diff before staging, committing, or pushing.

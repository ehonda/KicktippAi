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
- Read [references/preflight-evidence.md](references/preflight-evidence.md) when checking prior high-reasoning output-cap evidence.
- Read [references/seed-evidence.md](references/seed-evidence.md) only when checking the initial seeded rows.
- Use `.agents/skills/langfuse-experiments/` for experiment preparation, execution, export, commit, and push workflow details.
- Do not use `docs/research/estimate-experiment-cost-skill/` for routine cost-estimate execution. Treat historical research as design context only when changing this skill or investigating why the workflow exists.
- Use the official `$langfuse` skill or the installed `langfuse` command for Langfuse API inspection.
- Run commands from the repository root so Orchestrator can auto-load external secrets.
- Run every `dotnet` and `git` command outside the sandbox, as required by the repository root instructions. Use `uv` for Python commands.
- Run bundled Python tooling as `uv --cache-dir .uv-cache run ...` from the repository root. The default Windows uv cache can be blocked in the sandbox; if the command still needs network or external permissions, rerun the same command outside the sandbox with approval.
- Verify the model is represented in `src/OpenAiIntegration/CostCalculationService.cs` before spending API budget. If pricing is missing or stale, update it from the official OpenAI pricing source and add focused cost-calculation coverage before running the estimate.

## Estimate Workflow

1. Normalize the planned experiment size to match predictions `N`. For `slice` and `repeated-match`, this is usually the item count.
2. Identify the model, reasoning effort, prompt source/key, evaluation policy, and max output token cap.
3. Look up the best matching row in [references/base-estimate-table.md](references/base-estimate-table.md). Prefer exact matches for model, reasoning effort, prompt route, and max output token count.
4. Use the bundled script to calculate the final estimate from the table row:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --count 100 --model o3 --reasoning-effort medium
```

5. If no suitable row exists for a `high` or `xhigh` reasoning config, stop the estimate workflow and run the mandatory preflight gate below before returning a cost as actionable.
6. If no suitable row exists for any other config, create one with the base estimate method below, update the table, then run the estimate command.

Report the estimate with the row used, observed sample size, max output token cap, model knowledge cutoff date, sampling cutoff, and any mismatch between the observed row and the planned experiment.

## Mandatory Preflight Gate

Use this gate before any base estimate or actionable estimate for `high` or `xhigh` reasoning when no exact table row exists for the intended model, reasoning effort, prompt route, and max output token cap.

Use a short Langfuse run description for scanability in the Experiments UI: `<model> <reasoning-effort>, preflight`. Keep the long run name unchanged for stable identity and usage collection.

1. State the expected one-item preflight spend and get confirmation unless the user has already authorized that exact preflight.
2. Use the intended model, prompt source/key, reasoning effort, evaluation policy, service tier default, and a one-item dataset. Reuse the hosted-prompt POC one-item manifest when it matches the planned prompt route:

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-1-langfuse-poc/slice-dataset.json
dotnet run --project src/Orchestrator -- run-repeated-match gpt-5.5 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-vfb-stuttgart-vs-rb-leipzig/repeat-1-langfuse-poc/slice-manifest.json --run-name "preflight__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-1__exact-time__RUN_STAMP" --run-description "gpt-5.5 xhigh, preflight" --prompt-key langfuse-o3-poc --prompt-source langfuse --langfuse-prompt-name kicktippai/predict-one-match-o3-poc --langfuse-prompt-label poc --reasoning-effort xhigh --max-output-tokens 40000 --evaluation-time "2026-03-15T12:00:00 Europe/Berlin (+01)" --batch-count 1 --replace-run
```

3. Start with `--max-output-tokens 10000` only when no table row or prior preflight evidence indicates a higher cap. If prior evidence for the prompt route needed a higher cap, include that cap in the first preflight command and run name.
4. Collect the preflight usage before estimating the full base run:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py collect --env ..\KicktippAi.Secrets\src\Orchestrator\.env --group "repeated-measured=RUN_NAME" --expect repeated-measured=1 --output C:\tmp\kicktippai-cost-preflight-usage.json
```

5. Report input tokens, output tokens, reasoning tokens, service tier, observed cost, cap used, and whether the output hit or approached the cap. Record reusable cap/cost evidence in [references/preflight-evidence.md](references/preflight-evidence.md), not in the base table.
6. If the preflight fails due to output cap exhaustion, no output text, or `outputTokens >= maxOutputTokens`, increase the cap and rerun the same one-item preflight before launching the 5-by-4 base estimate.

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
6. Run the five repeated-match datasets in parallel with the `Start-Job` pattern in the cookbook below. Do not chain the five `dotnet run` commands with `;`; that is sequential. Each run must use `--batch-count 1`, the intended model, reasoning effort, prompt route, evaluation policy, and flex processing default. Use one shared UTC run stamp for the family.
7. Start with the default `--max-output-tokens` value of `10000` unless the table or prior preflight evidence already requires a higher cap. If any run item fails because the response hit the output cap, emits `OpenAI response did not contain output text`, or records `outputTokens >= maxOutputTokens`, increase the cap and rerun the complete 5-by-4 base estimate. Keep increasing and rerunning the complete sample until no item hits the cap.
8. Gather compact `predict-match` usage with the bundled script:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py collect --env ..\KicktippAi.Secrets\src\Orchestrator\.env --group "repeated-measured=RUN_NAME_1" --group "repeated-measured=RUN_NAME_2" --group "repeated-measured=RUN_NAME_3" --group "repeated-measured=RUN_NAME_4" --group "repeated-measured=RUN_NAME_5" --expect repeated-measured=20 --output C:\tmp\kicktippai-cost-estimate-usage.json
```

9. Calculate the table row with the bundled script. The script reads model prices from `src/OpenAiIntegration/CostCalculationService.cs`, applies flex pricing, ignores cached-input discounts, validates the 20-observation sample, and fails if any output reached the configured cap:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py base-row --input C:\tmp\kicktippai-cost-estimate-usage.json --model o3 --reasoning-effort medium --prompt-route "local prompt-v1" --model-knowledge-cutoff 2025-11-29 --sampling-cutoff "2025-12-01T00:00:00 Europe/Berlin (+01)" --max-output-tokens 10000 --source "base-estimate run family 2026-05-04"
```

10. Insert the emitted Markdown row into [references/base-estimate-table.md](references/base-estimate-table.md) and add source details to [references/seed-evidence.md](references/seed-evidence.md) or a new directly linked reference when the row needs long run-name evidence.
11. If [references/base-estimate-table.md](references/base-estimate-table.md) changed, commit and push the table plus any directly supporting reference files before reporting the workflow complete. Do not stop with an uncommitted table change.

## 5-by-4 Base Estimate Cookbook

Use this concrete pattern for step 4 of the base estimate method. Do not search historical research for prior artifact layouts.

Use short Langfuse run descriptions for scanability in the Experiments UI: `<model> <reasoning-effort>, base estimate (N/5)`. Keep the long run names unchanged for stable identity, grouping, and usage collection.

Read fixture coordinates from the selector manifest's `items` array. Use `homeTeam`, `awayTeam`, and `matchday`; `selectedItemIds` alone is not enough to prepare repeated-match datasets.

```powershell
$selectorManifest = "artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-5-seed-20260503-cost-estimate/slice-manifest.json"
$selector = Get-Content $selectorManifest -Raw | ConvertFrom-Json
$seed = $selector.sampleSeed
$fixtureIndex = 0

$selector.items | ForEach-Object {
  $fixtureIndex += 1
  $fixtureToken = "fixture-{0:00}" -f $fixtureIndex
  $sliceKey = "repeat-4-seed-$seed-$fixtureToken"

  dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context $selector.communityContext --home "$($_.homeTeam)" --away "$($_.awayTeam)" --matchday $_.matchday --sample-size 4 --slice-key $sliceKey --dataset-description "Cost estimate 5-by-4 base sample from selector $($selector.sliceKey), $fixtureToken."
}
```

For each successful `prepare-repeated-match` command, record the emitted `sliceArtifactPath`, `sliceManifestPath`, `sourcePoolKey`, and `sliceKey`. Sync exactly those five emitted datasets:

```powershell
dotnet run --project src/Orchestrator -- sync-dataset --input PATH_TO_FIXTURE_01_SLICE_DATASET_JSON
dotnet run --project src/Orchestrator -- sync-dataset --input PATH_TO_FIXTURE_02_SLICE_DATASET_JSON
dotnet run --project src/Orchestrator -- sync-dataset --input PATH_TO_FIXTURE_03_SLICE_DATASET_JSON
dotnet run --project src/Orchestrator -- sync-dataset --input PATH_TO_FIXTURE_04_SLICE_DATASET_JSON
dotnet run --project src/Orchestrator -- sync-dataset --input PATH_TO_FIXTURE_05_SLICE_DATASET_JSON
```

Run the five repeated-match manifests as one run family. Use one shared UTC run stamp and one run name per fixture. Start all five jobs before waiting for any of them. Use `--evaluation-policy-kind relative --evaluation-policy-offset -12:00:00` for the default slice-like policy, or the intended exact `--evaluation-time` when the planned estimate requires exact-time execution.

```powershell
$runStamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ").ToLowerInvariant()
$runs = @(
  @{
    Fixture = "01"
    Manifest = "PATH_TO_FIXTURE_01_SLICE_MANIFEST_JSON"
    RunName = "repeated-match__pes-squad__MODEL__PROMPT_ROUTE_TAG__reasoning-EFFORT__MAXOUT_TAG__repeat-4-seed-SEED-fixture-01__startsat-12h__$runStamp"
    Description = "MODEL EFFORT, base estimate (1/5)"
  },
  @{
    Fixture = "02"
    Manifest = "PATH_TO_FIXTURE_02_SLICE_MANIFEST_JSON"
    RunName = "repeated-match__pes-squad__MODEL__PROMPT_ROUTE_TAG__reasoning-EFFORT__MAXOUT_TAG__repeat-4-seed-SEED-fixture-02__startsat-12h__$runStamp"
    Description = "MODEL EFFORT, base estimate (2/5)"
  },
  @{
    Fixture = "03"
    Manifest = "PATH_TO_FIXTURE_03_SLICE_MANIFEST_JSON"
    RunName = "repeated-match__pes-squad__MODEL__PROMPT_ROUTE_TAG__reasoning-EFFORT__MAXOUT_TAG__repeat-4-seed-SEED-fixture-03__startsat-12h__$runStamp"
    Description = "MODEL EFFORT, base estimate (3/5)"
  },
  @{
    Fixture = "04"
    Manifest = "PATH_TO_FIXTURE_04_SLICE_MANIFEST_JSON"
    RunName = "repeated-match__pes-squad__MODEL__PROMPT_ROUTE_TAG__reasoning-EFFORT__MAXOUT_TAG__repeat-4-seed-SEED-fixture-04__startsat-12h__$runStamp"
    Description = "MODEL EFFORT, base estimate (4/5)"
  },
  @{
    Fixture = "05"
    Manifest = "PATH_TO_FIXTURE_05_SLICE_MANIFEST_JSON"
    RunName = "repeated-match__pes-squad__MODEL__PROMPT_ROUTE_TAG__reasoning-EFFORT__MAXOUT_TAG__repeat-4-seed-SEED-fixture-05__startsat-12h__$runStamp"
    Description = "MODEL EFFORT, base estimate (5/5)"
  }
)

$jobs = foreach ($run in $runs) {
  Start-Job -Name $run.Fixture -ArgumentList $run.Manifest, $run.RunName, $run.Description -ScriptBlock {
    param([string] $manifest, [string] $runName, [string] $runDescription)

    $ErrorActionPreference = "Stop"
    $PSNativeCommandUseErrorActionPreference = $true

    dotnet run --project src/Orchestrator -- run-repeated-match MODEL --manifest $manifest --run-name $runName --run-description $runDescription --prompt-key PROMPT_KEY PROMPT_SOURCE_FLAGS --reasoning-effort EFFORT MAX_OUTPUT_FLAGS --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
    if ($LASTEXITCODE -ne 0) {
      throw "run-repeated-match failed for $runName with exit code $LASTEXITCODE"
    }
  }
}

$jobs | Wait-Job
$jobs | Receive-Job
$failedJobs = $jobs | Where-Object { $_.State -ne "Completed" }
if ($failedJobs) {
  throw "One or more repeated-match jobs failed: $($failedJobs.Name -join ', ')"
}
$jobs | Remove-Job
Write-Output "RUN_STAMP=$runStamp"
```

Replace fixtures `01` through `05` with the emitted manifest paths, matching run names, and matching run descriptions. Include `MAXOUT_TAG` and `MAX_OUTPUT_FLAGS` only when the cap is above the default 10000, for example `maxout-40000` and `--max-output-tokens 40000`. For Langfuse hosted prompt runs, `PROMPT_SOURCE_FLAGS` is usually `--prompt-source langfuse --langfuse-prompt-name kicktippai/predict-one-match-o3-poc --langfuse-prompt-label poc`; for local prompt runs, omit those hosted-prompt flags.

Collect usage with exactly the five run names and `--expect repeated-measured=20`. If the collector finds fewer or more observations, fix the run set before calculating a table row.

## Reference Table Commit And Push

Use this closeout every time [references/base-estimate-table.md](references/base-estimate-table.md) is modified. Include any directly supporting reference files changed for the same row, such as `preflight-evidence.md`, `seed-evidence.md`, or a run-family evidence note. Do not stage generated experiment artifacts, `C:\tmp` files, or unrelated workspace changes.

Inspect and stage only the intended reference files:

```powershell
git diff -- .agents/skills/estimate-experiment-cost-skill/references
git status --short --branch
$filesToCommit = @(
  ".agents/skills/estimate-experiment-cost-skill/references/base-estimate-table.md"
  # Add every changed supporting reference file here, for example:
  # ".agents/skills/estimate-experiment-cost-skill/references/preflight-evidence.md"
  # ".agents/skills/estimate-experiment-cost-skill/references/seed-evidence.md"
)
git add -- $filesToCommit
git commit -m "Update experiment cost estimates"
```

Before requesting approval for push, record the exact target:

```powershell
git branch --show-current
git remote -v
git status --short --branch
git log -1 --oneline
```

Push with an explicit remote and branch, using the current branch from `git branch --show-current`:

```powershell
git push origin CURRENT_BRANCH
```

## Output Token Caps

- Treat `10000` as the default cap because `PredictionServiceOptions` uses `MaxOutputTokenCount = 10_000`.
- Include an explicit `--max-output-tokens` flag, run-name tag, and table value whenever a higher cap is needed.
- For reasoning-heavy configurations, follow the mandatory preflight gate before launching the 5-by-4 base estimate. Use the preflight to choose the first non-default cap, then still validate the full 20-item base sample.
- Never update the table from a base estimate that has missing observations, failed items, non-flex service tier, or output-cap hits.

## Quality Checks

- Verify the table row is based on five randomly selected repeated-match fixtures with four repetitions each.
- Verify the sampling cutoff equals the stored model knowledge cutoff date plus two days.
- Verify the estimate uses `N` match predictions, not batches or fixtures.
- Verify the table costs were emitted by `scripts/experiment_cost_estimator.py`.
- Inspect the diff before staging or committing any table or skill change.
- Verify every `base-estimate-table.md` change was committed and pushed with its supporting reference files before final response.

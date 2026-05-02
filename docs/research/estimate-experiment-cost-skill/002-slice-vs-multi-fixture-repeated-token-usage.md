# 002 - Slice vs Multi-Fixture Repeated-Match Token Usage

Status: Completed

Executed on: 2026-05-01 UTC run stamp, documented on 2026-05-02

Repository commit used for documentation: `6f56db5 Fix line endings`

## Research Question

Does the average total input and output token count for `o3` with `medium` reasoning effort differ between a 20-item random slice and 20 repeated-match predictions spread across several randomly selected fixtures?

This experiment follows [001](001-slice-vs-repeated-token-usage.md), where a single repeated fixture made total input tokens too fixture-confounded.

## Methodology

This experiment uses the shared token-usage analysis method documented in [token-usage-methodology.md](token-usage-methodology.md): compact Langfuse usage extraction from `predict-match`, two-sided exact permutation tests over difference in means, and seeded bootstrap confidence intervals.

Configuration:

- Community context: `pes-squad`
- Source season: Bundesliga 2025-26
- Match cutoff: all randomly selected items start after `2025-12-01T00:00:00 Europe/Berlin (+01)`
- Model: `o3`
- Reasoning effort: `medium`
- Prompt: local `prompt-v1`
- Evaluation policy: relative `startsat-12h`
- Slice sample size: `N = 20`, seed `20260502`
- Repeated-match measured sample size: `N = 20`
- Repeated-match design: `M = 5` random fixtures, `S = 4` repetitions each, so `N = M * S`

Dataset construction:

- Slice dataset: 20 randomly selected completed matches after the cutoff.
- Repeated fixtures: 5 randomly selected fixtures from the same cutoff pool, seed `20260503`.
- Repeated-match datasets: 5 datasets of size 4.
- All repeated-match observations are included. Cached input is intentionally out of scope for this experiment.

Prepared artifacts:

- Slice manifest: `artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-20-seed-20260502/slice-manifest.json`
- Repeated fixture selector manifest: `artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-5-seed-20260503-repeated-fixtures/slice-manifest.json`
- Repeated-match manifests:
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md19-eintracht-frankfurt-vs-1899-hoffenheim/repeat-4-seed-20260503-fixture-01/slice-manifest.json`
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md21-1-fc-koln-vs-rb-leipzig/repeat-4-seed-20260503-fixture-02/slice-manifest.json`
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md26-borussia-dortmund-vs-fc-augsburg/repeat-4-seed-20260503-fixture-03/slice-manifest.json`
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md26-bayer-04-leverkusen-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-04/slice-manifest.json`
  - `artifacts/langfuse-experiments/repeated-match/pes-squad/md31-fsv-mainz-05-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-05/slice-manifest.json`

Langfuse datasets:

- `match-predictions/bundesliga-2025-26/pes-squad/slices/all-matchdays-after-20251130t230000z/random-20-seed-20260502`
- `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md19-eintracht-frankfurt-vs-1899-hoffenheim/repeat-4-seed-20260503-fixture-01`
- `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md21-1-fc-koln-vs-rb-leipzig/repeat-4-seed-20260503-fixture-02`
- `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md26-borussia-dortmund-vs-fc-augsburg/repeat-4-seed-20260503-fixture-03`
- `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md26-bayer-04-leverkusen-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-04`
- `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md31-fsv-mainz-05-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-05`

Repeated fixtures:

| Fixture | Matchday | Starts at | Actual |
| --- | ---: | --- | --- |
| Eintracht Frankfurt vs 1899 Hoffenheim | 19 | `2026-01-24T15:30:00 UTC+01 (+01)` | `1:3` |
| 1. FC Köln vs RB Leipzig | 21 | `2026-02-08T15:30:00 UTC+01 (+01)` | `1:2` |
| Borussia Dortmund vs FC Augsburg | 26 | `2026-03-14T15:30:00 UTC+01 (+01)` | `2:0` |
| Bayer 04 Leverkusen vs FC Bayern München | 26 | `2026-03-14T15:30:00 UTC+01 (+01)` | `1:1` |
| FSV Mainz 05 vs FC Bayern München | 31 | `2026-04-25T16:30:00 UTC+02 (+02)` | `3:4` |

Run names:

- `slice__pes-squad__o3__prompt-v1__reasoning-medium__random-20-seed-20260502__startsat-12h__2026-05-01t22-14-04z`
- `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-01t22-14-04z`
- `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-01t22-14-04z`
- `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-01t22-14-04z`
- `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-01t22-14-04z`
- `repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-01t22-14-04z`

Commands:

```powershell
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --sample-size 20 --sample-seed 20260502 --starts-after "2025-12-01T00:00:00 Europe/Berlin (+01)"
dotnet run --project src/Orchestrator -- prepare-slice --community-context pes-squad --sample-size 5 --sample-seed 20260503 --starts-after "2025-12-01T00:00:00 Europe/Berlin (+01)" --slice-key random-5-seed-20260503-repeated-fixtures
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "Eintracht Frankfurt" --away "1899 Hoffenheim" --matchday 19 --sample-size 4 --slice-key repeat-4-seed-20260503-fixture-01
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "1. FC Köln" --away "RB Leipzig" --matchday 21 --sample-size 4 --slice-key repeat-4-seed-20260503-fixture-02
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "Borussia Dortmund" --away "FC Augsburg" --matchday 26 --sample-size 4 --slice-key repeat-4-seed-20260503-fixture-03
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "Bayer 04 Leverkusen" --away "FC Bayern München" --matchday 26 --sample-size 4 --slice-key repeat-4-seed-20260503-fixture-04
dotnet run --project src/Orchestrator -- prepare-repeated-match --community-context pes-squad --home "FSV Mainz 05" --away "FC Bayern München" --matchday 31 --sample-size 4 --slice-key repeat-4-seed-20260503-fixture-05
dotnet run --project src/Orchestrator -- run-slice o3 --manifest artifacts/langfuse-experiments/slices/pes-squad/all-matchdays-after-20251130t230000z/random-20-seed-20260502/slice-manifest.json --run-name "slice__pes-squad__o3__prompt-v1__reasoning-medium__random-20-seed-20260502__startsat-12h__2026-05-01t22-14-04z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-size 20 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md19-eintracht-frankfurt-vs-1899-hoffenheim/repeat-4-seed-20260503-fixture-01/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-01t22-14-04z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md21-1-fc-koln-vs-rb-leipzig/repeat-4-seed-20260503-fixture-02/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-01t22-14-04z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-borussia-dortmund-vs-fc-augsburg/repeat-4-seed-20260503-fixture-03/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-01t22-14-04z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md26-bayer-04-leverkusen-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-04/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-01t22-14-04z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
dotnet run --project src/Orchestrator -- run-repeated-match o3 --manifest artifacts/langfuse-experiments/repeated-match/pes-squad/md31-fsv-mainz-05-vs-fc-bayern-munchen/repeat-4-seed-20260503-fixture-05/slice-manifest.json --run-name "repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-01t22-14-04z" --prompt-key prompt-v1 --reasoning-effort medium --evaluation-policy-kind relative --evaluation-policy-offset -12:00:00 --batch-count 1 --replace-run
```

Reproducibility command:

```powershell
uv run python docs/research/estimate-experiment-cost-skill/analyze_token_usage.py --env ..\KicktippAi.Secrets\src\Orchestrator\.env --group "slice-measured=slice__pes-squad__o3__prompt-v1__reasoning-medium__random-20-seed-20260502__startsat-12h__2026-05-01t22-14-04z" --group "repeated-measured=repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-01__startsat-12h__2026-05-01t22-14-04z" --group "repeated-measured=repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-02__startsat-12h__2026-05-01t22-14-04z" --group "repeated-measured=repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-03__startsat-12h__2026-05-01t22-14-04z" --group "repeated-measured=repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-04__startsat-12h__2026-05-01t22-14-04z" --group "repeated-measured=repeated-match__pes-squad__o3__prompt-v1__reasoning-medium__repeat-4-seed-20260503-fixture-05__startsat-12h__2026-05-01t22-14-04z" --output C:\tmp\kicktippai-subexp-b-usage.json --left-group slice-measured --right-group repeated-measured --expect slice-measured=20 --expect repeated-measured=20 --metrics inputTokens outputTokens --seed 20260502 --report-json C:\tmp\kicktippai-subexp-b-analysis.json
```

## Outcome

Run summaries:

| Run | Executions | Batch shape | Aggregate score |
| --- | ---: | --- | ---: |
| Slice | 20 | one batch of 20 | total `28`, avg `1.4` |
| Repeated fixture 01 | 4 | repeated-match batch-count 1 | total `6`, avg `1.5` |
| Repeated fixture 02 | 4 | repeated-match batch-count 1 | total `15`, avg `3.75` |
| Repeated fixture 03 | 4 | repeated-match batch-count 1 | total `10`, avg `2.5` |
| Repeated fixture 04 | 4 | repeated-match batch-count 1 | total `0`, avg `0.0` |
| Repeated fixture 05 | 4 | repeated-match batch-count 1 | total `8`, avg `2.0` |

Primary comparison:

| Metric | Slice mean | Repeated measured mean | Difference, slice - repeated | Permutation p | Bootstrap 95% CI |
| --- | ---: | ---: | ---: | ---: | --- |
| Input tokens | 3438.950 | 3379.000 | 59.950 | 0.100263 | [-5.200, 130.000] |
| Output tokens | 1553.900 | 1764.400 | -210.500 | 0.524548 | [-846.752, 396.650] |

Answer:

- With five random repeated fixtures of size four, neither total input tokens nor total output tokens differed significantly from the 20-item slice sample.
- This supports the proposed reference dataset shape for the requested model configuration: `M` random repeated-match fixtures of size `S`, for `N = M * S` total datapoints.
- The input-token p-value moved from significant in [001](001-slice-vs-repeated-token-usage.md) to non-significant here, consistent with the theory that the first experiment's input result was mostly single-fixture context confounding.
- Cached input remains out of scope for this conclusion. The design is useful for total input/output token estimates; cost estimates still need separate cached-input handling.

## Further Research Directions

- Wait for the user to choose the next model/reasoning-effort experiment.
- Treat `M` random repeated fixtures of size `S` as the preferred low-cost reference dataset design for total token estimates, with `M` high enough to sample fixture/context variation.
- Extend the Orchestrator export or report tooling to include normalized usage details directly in analysis bundles, so future research can avoid a separate Langfuse usage extraction step.

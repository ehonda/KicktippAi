# Dataset Talk Examples

This folder packages the four concrete experiment examples we want to use in the
experiment-design talk:

1. `slice`: `random-40-seed-20260505-prod-plus-o3-effort`
2. `repeated-match`: `repeat-100-knowledge-cutoff-bayern-rbl-md1`
3. `repeated-match-slice`: `random-15x10-seed-20260517-after-20251203`
4. `repeated-match-slice` cost-estimate example:
   `random-5x4-seed-20260517-gpt-54nano-none-cost-estimate`

Shared machine-readable run data lives in
[run-metrics-and-estimates.json](run-metrics-and-estimates.json). The `actual`
numbers were retrieved from Langfuse on 2026-06-01. The `estimated` numbers
were generated on 2026-06-01 with
`.agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py`.
Per the skill, the estimator treats all input tokens as uncached, so the
estimate is intentionally conservative when the real run saw cache hits.

## Slice

Chosen example: `random-40-seed-20260505-prod-plus-o3-effort`

Why we chose it:

- It is the cleanest small `slice` example in the repo.
- It compares a real production baseline against a single controlled variant:
  `o3 medium` versus `o3 high`.
- It already has a strong long-form writeup and a published Pages report.

Files in this folder:

- [slice-dataset.json](slice/random-40-seed-20260505-prod-plus-o3-effort/slice-dataset.json)
- [slice-manifest.json](slice/random-40-seed-20260505-prod-plus-o3-effort/slice-manifest.json)
- [prod-plus-o3-reasoning-effort-random-40.md](slice/random-40-seed-20260505-prod-plus-o3-effort/prod-plus-o3-reasoning-effort-random-40.md)

Runs conducted:

- `o3 medium`:
  `slice__pes-squad__o3__prompt-v1__reasoning-medium__maxout-20000__random-40-seed-20260505-prod-plus-o3-effort__startsat-12h__2026-05-04t22-49-35z`
- `o3 high`:
  `slice__pes-squad__o3__prompt-v1__reasoning-high__maxout-20000__random-40-seed-20260505-prod-plus-o3-effort__startsat-12h__2026-05-04t22-49-35z`

Hosted page:

- https://ehonda.github.io/KicktippAi/experiment-analysis/slices/pes-squad/all-matchdays-after-20251130t230000z/random-40-seed-20260505-prod-plus-o3-effort/random-40-seed-20260505-prod-plus-o3-effort-2026-05-04t22-49-35z.analysis.report.html

Estimator commands used:

- `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,40,100,150 --model o3 --reasoning-effort medium`
- `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 40 --model o3 --reasoning-effort high`

| Config | Predictions | Estimated cost | Actual cost | Uncached input | Cached input | Output | Total tokens | Avg latency | P95 latency |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `o3 medium` | 40 | `$0.417464` | `$0.579057` | 180,741 | 5,632 | 100,635 | 281,376 | 14.4s | 29.3s |
| `o3 high` | 40 | `$0.961284` | `$0.891508` | 162,568 | 37,888 | 189,339 | 351,907 | 71.2s | 403.7s |

Notes:

- The `o3 medium` Langfuse session aggregated `53` prediction observations for a
  prepared sample size of `40`, so this run likely includes retries or extra
  generations at the session level.
- The experiment result itself favored `o3 medium` by 1 total Kicktipp point, so
  this is a good talk example for "clean design, inconclusive outcome".

## Repeated Match

Chosen example: `repeat-100-knowledge-cutoff-bayern-rbl-md1`

Why we chose it:

- It is the clearest `repeated-match` example with a memorable research
  question: knowledge-cutoff contamination.
- It shows the value of repeated predictions on one fixed fixture.
- The 100x follow-up is easier to explain in a talk than the earlier 25x probe.

Files in this folder:

- [slice-dataset.json](repeated-match/repeat-100-knowledge-cutoff-bayern-rbl-md1/slice-dataset.json)
- [slice-manifest.json](repeated-match/repeat-100-knowledge-cutoff-bayern-rbl-md1/slice-manifest.json)
- [knowledge-cutoff-bayern-rbl-repeated-match.md](repeated-match/repeat-100-knowledge-cutoff-bayern-rbl-md1/knowledge-cutoff-bayern-rbl-repeated-match.md)

Runs conducted on this 100x dataset:

- `gpt-5.5 low`:
  `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-low__repeat-100-knowledge-cutoff-bayern-rbl-md1__exact-time__2026-05-10t08-43-34z`
- `o3 medium`:
  `repeated-match__pes-squad__o3__langfuse-o3-poc__reasoning-medium__repeat-100-knowledge-cutoff-bayern-rbl-md1__exact-time__2026-05-30t20-25-39z`

Hosted pages:

- `gpt-5.5 low`: https://ehonda.github.io/KicktippAi/experiment-analysis/repeated-match/pes-squad/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-100-knowledge-cutoff-bayern-rbl-md1/2026-05-10t08-43-34z.analysis.report.html
- `o3 medium`: https://ehonda.github.io/KicktippAi/experiment-analysis/repeated-match/pes-squad/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-100-knowledge-cutoff-bayern-rbl-md1/2026-05-30t20-25-39z.analysis.report.html

Estimator commands used:

- `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 100 --model gpt-5.5 --reasoning-effort low`
- `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,40,100,150 --model o3 --reasoning-effort medium`

| Config | Predictions | Estimated cost | Actual cost | Uncached input | Cached input | Output | Total tokens | Avg latency | P95 latency |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `gpt-5.5 low` | 100 | `$1.167625` | `$0.359366` | 247,100 | 221,184 | 15,952 | 263,052 | 3.7s | 5.9s |
| `o3 medium` | 100 | `$1.043660` | `$0.664692` | 247,100 | 166,144 | 135,550 | 382,650 | 9.9s | 20.2s |

Notes:

- This is the best talk example for why dataset timing matters around knowledge
  cutoff dates.
- Both runs came in below the conservative estimate because the real runs saw
  substantial cached-input usage.

## Repeated Match Slice

Chosen example: `random-15x10-seed-20260517-after-20251203`

Why we chose it:

- It is the cleanest first-class `repeated-match-slice` artifact in the repo.
- It has five runs across model families and reasoning levels on one fixed
  15-fixture x 10-repetition dataset.
- It is the most flexible example for explaining the design tradeoff between
  diversity of fixtures and repeatability per fixture.

Files in this folder:

- [slice-dataset.json](repeated-match-slice/random-15x10-seed-20260517-after-20251203/slice-dataset.json)
- [slice-manifest.json](repeated-match-slice/random-15x10-seed-20260517-after-20251203/slice-manifest.json)
- [gpt54nano-vs-gpt55-none-repeated-match-slice-after-20251203.md](repeated-match-slice/random-15x10-seed-20260517-after-20251203/gpt54nano-vs-gpt55-none-repeated-match-slice-after-20251203.md)
- [gpt55-medium-vs-o3-medium-repeated-match-slice-after-20251203.md](repeated-match-slice/random-15x10-seed-20260517-after-20251203/gpt55-medium-vs-o3-medium-repeated-match-slice-after-20251203.md)
- [gpt55-high-repeated-match-slice-after-20251203.md](repeated-match-slice/random-15x10-seed-20260517-after-20251203/gpt55-high-repeated-match-slice-after-20251203.md)

Runs conducted:

- `gpt-5.4-nano none`:
  `repeated-match-slice__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-none__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-05-16t23-24-24z`
- `gpt-5.5 none`:
  `repeated-match-slice__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-05-16t23-24-24z`
- `gpt-5.5 medium`:
  `repeated-match-slice__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-medium__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-05-16t23-55-32z`
- `o3 medium`:
  `repeated-match-slice__pes-squad__o3__langfuse-o3-poc__reasoning-medium__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-05-16t23-55-32z`
- `gpt-5.5 high`:
  `repeated-match-slice__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-high__random-15x10-seed-20260517-after-20251203__startsat-12h__2026-05-18t22-28-33z`

Hosted pages:

- `gpt-5.4-nano none` vs `gpt-5.5 none`: https://ehonda.github.io/KicktippAi/experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/gpt54nano-vs-gpt55-none-2026-05-16t23-24-24z.analysis.report.html
- `gpt-5.5 medium` vs `o3 medium`: https://ehonda.github.io/KicktippAi/experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/gpt55-medium-vs-o3-medium-2026-05-16t23-55-32z.analysis.report.html
- `gpt-5.5 medium` vs `gpt-5.5 high`: https://ehonda.github.io/KicktippAi/experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/gpt55-medium-vs-gpt55-high-2026-05-18t22-28-33z.analysis.report.html
- `o3 medium` vs `gpt-5.5 high`: https://ehonda.github.io/KicktippAi/experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/o3-medium-vs-gpt55-high-2026-05-18t22-28-33z.analysis.report.html
- all runs: https://ehonda.github.io/KicktippAi/experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20251202t230000z/random-15x10-seed-20260517-after-20251203/all-runs-2026-05-18t22-28-33z.analysis.report.html

Estimator commands used:

- `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,150 --model gpt-5.4-nano --reasoning-effort none`
- `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 150 --model gpt-5.5 --reasoning-effort none`
- `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 150 --model gpt-5.5 --reasoning-effort medium`
- `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 150 --model gpt-5.5 --reasoning-effort high`
- `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,40,100,150 --model o3 --reasoning-effort medium`

| Config | Predictions | Estimated cost | Actual cost | Uncached input | Cached input | Output | Total tokens | Avg latency | P95 latency |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `gpt-5.4-nano none` | 150 | `$0.053821` | `$0.019557` | 516,710 | 374,528 | 2,550 | 519,260 | 1.3s | 2.1s |
| `gpt-5.5 none` | 150 | `$1.305375` | `$0.419369` | 516,710 | 404,736 | 2,550 | 519,260 | 1.4s | 2.8s |
| `gpt-5.5 medium` | 150 | `$2.546138` | `$1.581245` | 516,710 | 410,880 | 80,930 | 597,640 | 9.3s | 15.0s |
| `o3 medium` | 150 | `$1.565490` | `$1.698646` | 516,710 | 111,488 | 316,388 | 833,098 | 13.2s | 27.1s |
| `gpt-5.5 high` | 150 | `$8.017463` | `$15.452459` | 516,710 | 376,832 | 498,521 | 1,015,231 | 61.1s | 118.6s |

Notes:

- This is the best talk example for showing how the same dataset shape supports
  many model and reasoning comparisons.
- `gpt-5.5 high` is the clearest example in this set where the real run cost
  materially exceeded the flex-based conservative estimate.
- `gpt-5.4-nano none` is the opposite: very cheap, very fast, and below the
  already-low estimate.

## Repeated Match Slice Cost-Estimate Example

Chosen example: `random-5x4-seed-20260517-gpt-54nano-none-cost-estimate`

Why we chose it:

- It is the modern, first-class `repeated-match-slice` artifact that the
  estimator skill now wants for base rows.
- It is exactly the canonical base-estimate shape: 5 fixtures x 4 repetitions =
  20 predictions.
- It lets us explain that the cost-estimation workflow converged on the same
  dataset family that later became a first-class experiment type.

Files in this folder:

- [slice-dataset.json](repeated-match-slice/random-5x4-seed-20260517-gpt-54nano-none-cost-estimate/slice-dataset.json)
- [slice-manifest.json](repeated-match-slice/random-5x4-seed-20260517-gpt-54nano-none-cost-estimate/slice-manifest.json)

Run conducted:

- `gpt-5.4-nano none`:
  `repeated-match-slice__pes-squad__gpt-5.4-nano__langfuse-o3-poc__reasoning-none__random-5x4-seed-20260517-gpt-54nano-none-cost-estimate__startsat-12h__2026-05-17t000000z`

Hosted page:

- No dedicated published comparison page exists for this base-estimate run.

Estimator command used:

- `uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,150 --model gpt-5.4-nano --reasoning-effort none`

| Config | Predictions | Estimated cost | Actual cost | Uncached input | Cached input | Output | Total tokens | Avg latency | P95 latency |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `gpt-5.4-nano none` | 20 | `$0.007176` | `$0.003375` | 69,636 | 42,240 | 340 | 69,976 | 2.2s | 3.6s |

Notes:

- No dedicated companion Markdown writeup existed for this exact artifact before
  this talk packaging pass.
- This is the example to use when explaining where the estimator's base rows
  come from.

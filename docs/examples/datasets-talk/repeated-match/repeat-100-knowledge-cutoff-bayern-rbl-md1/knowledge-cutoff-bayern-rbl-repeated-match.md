# Knowledge Cutoff Probe, Bayern vs RB Leipzig Repeated Match

Date: 2026-05-06
Updated: 2026-05-30

This experiment probes whether a model whose knowledge cutoff is after a known
fixture behaves like it has memorized the result, even when the reconstructed
prediction prompt is set before kickoff.

The fixture is the Bundesliga 2025/26 opening match, FC Bayern München vs RB
Leipzig on matchday 1. The match ended 6:0. It is useful for this probe because
6:0 is a memorable scoreline and is an unlikely pre-match prediction if the model
is only optimizing ordinary Kicktipp expected points.

OpenAI's current model docs list `gpt-5.5` with a December 1, 2025 knowledge
cutoff and `gpt-5-nano` with a May 31, 2024 knowledge cutoff:

- [OpenAI models overview](https://developers.openai.com/api/docs/models)
- [OpenAI gpt-5-nano model page](https://developers.openai.com/api/docs/models/gpt-5-nano)

## Dataset

| Field | Value |
| --- | --- |
| 25x dataset | `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-25-knowledge-cutoff-bayern-rbl-md1` |
| 100x follow-up dataset | `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-100-knowledge-cutoff-bayern-rbl-md1` |
| Community | `pes-squad` |
| Fixture | `FC Bayern München vs RB Leipzig` |
| Matchday | 1 |
| Starts at | `2025-08-22T21:30:00 UTC+02 (+02)` |
| Actual result | `6:0` |
| 25x slice | `repeat-25-knowledge-cutoff-bayern-rbl-md1` |
| 100x follow-up slice | `repeat-100-knowledge-cutoff-bayern-rbl-md1` |
| Sample size | 25 repeated predictions for the paired comparison; 100 repeated predictions for the `gpt-5.5 low` and `o3 medium` follow-ups |
| Selected item hash | `86fe3b148f4df8862ca2594deb2edb00925d1e2d8be8bb56a8c600ccf4c176af` |
| Evaluation time | `2025-08-22T12:00:00 Europe/Berlin (+02)` |

Before running the paid experiment, `reconstruct-prompt` was run at the exact
evaluation time. It resolved only context versions from August 19, 2025 and
showed the pre-match standings plus historical context, not the 6:0 outcome.

## Configuration

The paired 25x comparison runs used the same repeated-match manifest, exact
evaluation time, hosted prompt route, and `--batch-count 3`. The 100x
follow-ups used the same source match, selected-item hash, exact evaluation
time, and hosted prompt route, but a separate 100-repetition manifest.

| Config | Prompt | Reasoning | Max output tokens | Run description |
| --- | --- | --- | ---: | --- |
| `gpt-5.5 xhigh` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `xhigh` | 40,000 | `cutoff probe: gpt-5.5 xhigh` |
| `gpt-5.5 none` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `none` | 10,000 default | `cutoff probe: gpt-5.5 none` |
| `gpt-5.5 low` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `low` | 10,000 | `cutoff probe: gpt-5.5 low 100x` |
| `o3 medium` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `medium` | 10,000 | `cutoff probe: o3 medium 100x` |
| `gpt-5-nano` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | model default | 10,000 default | `cutoff probe: gpt-5-nano default` |

The hosted prompt route was used for every run so the comparison changed the
model and reasoning configuration, not the prompt template. `gpt-5.5` does not
have a local prompt directory in this repository.

The stored cost-estimator row for `gpt-5.5 xhigh` estimated 25 predictions at
`$2.366687500000` with this command:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 25 --model gpt-5.5 --reasoning-effort xhigh
```

The stored cost-estimator row for `gpt-5.5 none` estimated 25 predictions at
`$0.217562500000` with this command:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 25 --model gpt-5.5 --reasoning-effort none
```

The stored cost-estimator row for `gpt-5.5 low` estimated 100 predictions at
`$1.167625000000` with this command:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 100 --model gpt-5.5 --reasoning-effort low
```

The stored cost-estimator row for `o3 medium` estimated 100 predictions at
`$1.043660000000` with this command:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 100 --model o3 --reasoning-effort medium
```

The observed 100x `gpt-5.5 low` prediction cost collected from Langfuse after
the run was `$0.359366`. All 100 OpenAI prediction observations requested and
received the flex service tier; no standard-tier fallback was recorded for the
prediction calls.

There was no stored base estimate row for `gpt-5-nano` with omitted/default
reasoning effort, so no formal nano estimate was recorded.

## Langfuse Runs

| Config | Run name |
| --- | --- |
| `gpt-5.5 xhigh` | `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-25-knowledge-cutoff-bayern-rbl-md1__exact-time__2026-05-05t22-40-47z` |
| `gpt-5.5 none` | `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-none__repeat-25-knowledge-cutoff-bayern-rbl-md1__exact-time__2026-05-05t23-01-38z` |
| `gpt-5.5 low` | `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-low__repeat-100-knowledge-cutoff-bayern-rbl-md1__exact-time__2026-05-10t08-43-34z` |
| `o3 medium` | `repeated-match__pes-squad__o3__langfuse-o3-poc__reasoning-medium__repeat-100-knowledge-cutoff-bayern-rbl-md1__exact-time__2026-05-30t20-25-39z` |
| `gpt-5-nano` | `repeated-match__pes-squad__gpt-5-nano__langfuse-o3-poc__repeat-25-knowledge-cutoff-bayern-rbl-md1__exact-time__2026-05-05t22-40-47z` |

## Standard Comparison Result

| Rank | Config | Total Kicktipp points | Average points |
| ---: | --- | ---: | ---: |
| 1 | `gpt-5.5 xhigh` | 54 | 2.160 |
| 2 | `gpt-5-nano` | 50 | 2.000 |
| 3 | `gpt-5.5 none` | 50 | 2.000 |

The three-run Friedman p-value was 0.1353, so the standard performance
comparison is not statistically significant at alpha 0.05.

Pairwise comparisons:

| Run A | Run B | Avg-point delta | Raw p-value | Holm-adjusted p-value | Per-item W/T/L |
| --- | --- | ---: | ---: | ---: | --- |
| `gpt-5.5 xhigh` | `gpt-5-nano` | 0.160 | 0.1573 | 0.4719 | 2/23/0 |
| `gpt-5.5 xhigh` | `gpt-5.5 none` | 0.160 | 0.1573 | 0.4719 | 2/23/0 |
| `gpt-5-nano` | `gpt-5.5 none` | 0.000 | 1.0000 | 1.0000 | 0/25/0 |

## 100x Follow-ups

The `gpt-5.5 low` and `o3 medium` runs are single-run follow-ups rather than
part of the paired 25x statistical report. They repeat the same source match
and evaluation setup on a 100-repetition dataset.

| Config | Predictions | Total Kicktipp points | Average points | Exact 6:0 predictions | Distribution |
| --- | ---: | ---: | ---: | ---: | --- |
| `gpt-5.5 low` | 100 | 210 | 2.100 | 5 / 100 | `3:1` x90, `2:1` x5, `6:0` x5 |
| `o3 medium` | 100 | 200 | 2.000 | 0 / 100 | `2:1` x95, `3:1` x5 |

## Exact 6:0 Count

| Config | 6:0 predictions | Share |
| --- | ---: | ---: |
| `gpt-5.5 xhigh` | 2 / 25 | 8% |
| `gpt-5.5 none` | 0 / 25 | 0% |
| `gpt-5.5 low` | 5 / 100 | 5% |
| `o3 medium` | 0 / 100 | 0% |
| `gpt-5-nano` | 0 / 25 | 0% |

Prediction distributions:

| Config | Distribution |
| --- | --- |
| `gpt-5.5 xhigh` | `3:1` x20, `2:1` x3, `6:0` x2 |
| `gpt-5.5 none` | `3:1` x25 |
| `gpt-5.5 low` | `3:1` x90, `2:1` x5, `6:0` x5 |
| `o3 medium` | `2:1` x95, `3:1` x5 |
| `gpt-5-nano` | `2:1` x20, `3:1` x4, `3:2` x1 |

The two exact `6:0` predictions in the 25x `gpt-5.5 xhigh` run account for the
two paired wins and the four total-point advantage in the standard comparison.
In ordinary pre-match forecasting, `6:0` is not a natural default scoreline for
Bayern vs Leipzig; seeing it appear at all is the strongest signal in this
experiment. The 100x `gpt-5.5 low` follow-up produced the exact known scoreline
5 times, while the spontaneous `gpt-5.5 none` follow-up returned `3:1` in all 25
repetitions. The 100x `o3 medium` follow-up never emitted `6:0` and instead
stayed almost entirely on `2:1`.

## Interpretation

This result supports treating pre-cutoff completed matches as potentially
contaminated for model-quality experiments, but it also suggests the leakage
signal may depend on reasoning configuration. The exact known scoreline appeared
in 8% of `gpt-5.5 xhigh` predictions and 5% of the larger `gpt-5.5 low`
follow-up, while it was absent from `gpt-5.5 none`, `o3 medium`, and
`gpt-5-nano`.

The result does not prove memorization in a strict causal sense: a model could
occasionally choose an extreme Bayern home win from football priors alone, and
this is a repeated sample from one fixture rather than independent fixtures. The
contrast between `gpt-5.5 xhigh`, `gpt-5.5 low`, and `gpt-5.5 none`, however,
makes the exact-score hits more suspicious than a generic model-family tendency
or ordinary Bayern-favored prediction behavior. The 100x low run is not paired
with the 25x comparison and should not be read as a formal statistical contrast,
but it does make the exact-score signal harder to dismiss as a two-hit accident
from the original xhigh sample. The 100x `o3 medium` follow-up strengthens that
reading by showing a production-baseline `o3` configuration with the same hosted
prompt route and evaluation setup, but no exact `6:0` hits at all.

For future benchmark-style experiments, avoid fixtures that fall before a
model's knowledge cutoff, especially memorable high-scoring matches. If the goal
is to quantify contamination rather than avoid it, repeat this design across a
blocked set of memorable and ordinary fixtures on both sides of each model's
cutoff.

## Pages Report

The hosted 25x comparison page is generated at:

`experiment-analysis/repeated-match/pes-squad/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-25-knowledge-cutoff-bayern-rbl-md1/2026-05-05t23-01-38z.analysis.report.html`

Repo-relative link:
[knowledge-cutoff repeated-match analysis report](../../experiment-analysis/repeated-match/pes-squad/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-25-knowledge-cutoff-bayern-rbl-md1/2026-05-05t23-01-38z.analysis.report.html)

The hosted 100x `gpt-5.5 low` follow-up page is generated at:

`experiment-analysis/repeated-match/pes-squad/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-100-knowledge-cutoff-bayern-rbl-md1/2026-05-10t08-43-34z.analysis.report.html`

Repo-relative link:
[gpt-5.5 low 100x knowledge-cutoff follow-up report](../../experiment-analysis/repeated-match/pes-squad/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-100-knowledge-cutoff-bayern-rbl-md1/2026-05-10t08-43-34z.analysis.report.html)

The hosted 100x `o3 medium` follow-up page is generated at:

`experiment-analysis/repeated-match/pes-squad/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-100-knowledge-cutoff-bayern-rbl-md1/2026-05-30t20-25-39z.analysis.report.html`

Repo-relative link:
[o3 medium 100x knowledge-cutoff follow-up report](../../experiment-analysis/repeated-match/pes-squad/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-100-knowledge-cutoff-bayern-rbl-md1/2026-05-30t20-25-39z.analysis.report.html)

The earlier two-run report without `gpt-5.5 none` remains available at:

`experiment-analysis/repeated-match/pes-squad/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-25-knowledge-cutoff-bayern-rbl-md1/2026-05-05t22-40-47z.analysis.report.html`

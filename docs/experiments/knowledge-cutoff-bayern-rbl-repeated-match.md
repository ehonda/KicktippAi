# Knowledge Cutoff Probe, Bayern vs RB Leipzig Repeated Match

Date: 2026-05-06

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
| Dataset | `match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-25-knowledge-cutoff-bayern-rbl-md1` |
| Community | `pes-squad` |
| Fixture | `FC Bayern München vs RB Leipzig` |
| Matchday | 1 |
| Starts at | `2025-08-22T21:30:00 UTC+02 (+02)` |
| Actual result | `6:0` |
| Slice | `repeat-25-knowledge-cutoff-bayern-rbl-md1` |
| Sample size | 25 repeated predictions |
| Selected item hash | `86fe3b148f4df8862ca2594deb2edb00925d1e2d8be8bb56a8c600ccf4c176af` |
| Evaluation time | `2025-08-22T12:00:00 Europe/Berlin (+02)` |

Before running the paid experiment, `reconstruct-prompt` was run at the exact
evaluation time. It resolved only context versions from August 19, 2025 and
showed the pre-match standings plus historical context, not the 6:0 outcome.

## Configuration

Both runs used the same repeated-match manifest, the same exact evaluation time,
the same hosted prompt route, and `--batch-count 3`.

| Config | Prompt | Reasoning | Max output tokens | Run description |
| --- | --- | --- | ---: | --- |
| `gpt-5.5 xhigh` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | `xhigh` | 40,000 | `cutoff probe: gpt-5.5 xhigh` |
| `gpt-5-nano` | `langfuse-o3-poc` / `kicktippai/predict-one-match-o3-poc`, label `poc` | model default | 10,000 default | `cutoff probe: gpt-5-nano default` |

The hosted prompt route was used for both models so the comparison changed the
model and reasoning configuration, not the prompt template. `gpt-5.5` does not
have a local prompt directory in this repository.

The stored cost-estimator row for `gpt-5.5 xhigh` estimated 25 predictions at
`$2.366687500000` with this command:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 25 --model gpt-5.5 --reasoning-effort xhigh
```

There was no stored base estimate row for `gpt-5-nano` with omitted/default
reasoning effort, so no formal nano estimate was recorded.

## Langfuse Runs

| Config | Run name |
| --- | --- |
| `gpt-5.5 xhigh` | `repeated-match__pes-squad__gpt-5.5__langfuse-o3-poc__reasoning-xhigh__maxout-40000__repeat-25-knowledge-cutoff-bayern-rbl-md1__exact-time__2026-05-05t22-40-47z` |
| `gpt-5-nano` | `repeated-match__pes-squad__gpt-5-nano__langfuse-o3-poc__repeat-25-knowledge-cutoff-bayern-rbl-md1__exact-time__2026-05-05t22-40-47z` |

## Standard Head-To-Head Result

| Rank | Config | Total Kicktipp points | Average points |
| ---: | --- | ---: | ---: |
| 1 | `gpt-5.5 xhigh` | 54 | 2.160 |
| 2 | `gpt-5-nano` | 50 | 2.000 |

The paired comparison favored `gpt-5.5 xhigh` by 0.16 Kicktipp points per
prediction. The mean paired difference 95% bootstrap interval was -0.08 to 0.32,
the median paired difference was 0.0, and the Wilcoxon signed-rank p-value was
0.1573. The standard performance comparison is therefore not statistically
significant at alpha 0.05.

The per-item win/tie/loss count was 2/23/0 for `gpt-5.5 xhigh` versus
`gpt-5-nano`.

## Exact 6:0 Count

| Config | 6:0 predictions | Share |
| --- | ---: | ---: |
| `gpt-5.5 xhigh` | 2 / 25 | 8% |
| `gpt-5-nano` | 0 / 25 | 0% |

Prediction distributions:

| Config | Distribution |
| --- | --- |
| `gpt-5.5 xhigh` | `3:1` x20, `2:1` x3, `6:0` x2 |
| `gpt-5-nano` | `2:1` x20, `3:1` x4, `3:2` x1 |

The two exact `6:0` predictions account for the two paired wins and the four
total-point advantage. In ordinary pre-match forecasting, `6:0` is not a
natural default scoreline for Bayern vs Leipzig; seeing it appear twice in 25
attempts is the strongest signal in this experiment.

## Interpretation

This result supports treating pre-cutoff completed matches as contaminated for
model-quality experiments. It does not prove memorization in a strict causal
sense: a model could occasionally choose an extreme Bayern home win from
football priors alone, and this is a repeated sample from one fixture rather
than independent fixtures. But the exact known scoreline appearing in 8% of
`gpt-5.5 xhigh` predictions, while absent from `gpt-5-nano`, is consistent with
knowledge-cutoff leakage.

For future benchmark-style experiments, avoid fixtures that fall before a
model's knowledge cutoff, especially memorable high-scoring matches. If the goal
is to quantify contamination rather than avoid it, repeat this design across a
blocked set of memorable and ordinary fixtures on both sides of each model's
cutoff.

## Pages Report

The hosted comparison page is generated at:

`experiment-analysis/repeated-match/pes-squad/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-25-knowledge-cutoff-bayern-rbl-md1/2026-05-05t22-40-47z.analysis.report.html`

Repo-relative link:
[knowledge-cutoff repeated-match analysis report](../../experiment-analysis/repeated-match/pes-squad/md01-fc-bayern-munchen-vs-rb-leipzig/repeat-25-knowledge-cutoff-bayern-rbl-md1/2026-05-05t22-40-47z.analysis.report.html)

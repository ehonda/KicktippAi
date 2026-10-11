# GPT-6.1 Sol/xhigh Bundesliga production-candidate comparison

Status: Calibration and paired comparison complete; production unchanged.

Executed: 2026-10-11.

The owner requested the design used to select the Bundesliga 2026/27 production
model, now comparing candidate `gpt-6.1-sol`/`xhigh` with incumbent
`gpt-5.6-sol`/`xhigh`. This experiment gathers evidence for a possible midseason
upgrade. It does not select a model, amend the season policy, or change production.

## Frozen design

Both models receive the same ten fixtures with twenty predictions each, for
200 predictions per model. The seed is `20261011`, chosen before either quality
run. The primary metric is the average total Kicktipp points across the ten
fixtures per repetition; the statistical paired unit is a repetition total.
Both models use `xhigh`, a pinned 10,000-token output cap, batch count 7,
parallelism 5, no justification, and the established Flex-first request with one
Standard fallback for retryable Flex unavailability. The comparison axis is model.

[OpenAI's model page](https://developers.openai.com/api/docs/models/gpt-6.1-sol)
specifies an April 30, 2026 knowledge cutoff. The owner approved a new common
cutoff-safe sample rather than replaying the original February-cutoff dataset.
Sampling starts strictly after May 2, 2026 at Europe/Berlin local midnight.
The full historical context-eligible pool contains 27 completed fixtures across
matchdays 32–34. Its sorted-ID SHA-256 is
`07e7b9b95488da9072b75a66f28567bf78c7ebb22262577ec370e1aed45fac28`.
The selected ten-fixture hash is
`6650adb4f316bf0d168791c20b05b412b146b9acbe13c2fcef93a3c98148a20a`.

All historical contexts use the read-only, hash-bound
`bundesliga-2025-26-legacy-id-hash-v1` route and seven exact documents at kickoff
minus twelve hours. New artifacts explicitly pin hosted prompt
`kicktippai/bundesliga-2026-27/predict-one-match`, immutable version 3, required
label `production`, as approved by the owner. The original experiment used v2;
v2 no longer has that label. The first attempted preflight failed closed at
prompt validation before model construction or any paid prediction. The new
v3 artifacts preserve the original v2 constants and historical artifact identities.

The quality manifest's canonical historical-artifact SHA-256 is
`0532f2498d8ade93307809507fe8af8200e404fda7017f15b9f880d792a3b7b2`.
Its raw file SHA-256 is
`997129cfd762a3f66f31653ad0060a21705323295db45fe44ebaa10178b78e1d`;
hosted dataset ID is `cmv3kcp2c00n9ad0eqbn8qhzy`. Each fixture has exactly
twenty repetitions, and all five calibration fixtures are included by the
intentionally shared seed.
The dataset is
`match-predictions/bundesliga-2025-26/pes-squad/repeated-match-slices/all-matchdays-after-20260501t220000z/random-10x20-seed-20261011-gpt-6-1-sol-production-candidate-quality-v3`.
Both models must complete the same 200 stable prepared items before analysis.

| Matchday | Fixture |
| ---: | --- |
| 32 | 1. FC Union Berlin vs 1. FC Köln |
| 32 | Bor. Mönchengladbach vs Borussia Dortmund |
| 33 | Borussia Dortmund vs Eintracht Frankfurt |
| 33 | 1899 Hoffenheim vs Werder Bremen |
| 33 | 1. FC Köln vs 1. FC Heidenheim 1846 |
| 33 | FSV Mainz 05 vs 1. FC Union Berlin |
| 34 | Bor. Mönchengladbach vs 1899 Hoffenheim |
| 34 | FC Bayern München vs 1. FC Köln |
| 34 | FC St. Pauli vs VfL Wolfsburg |
| 34 | SC Freiburg vs RB Leipzig |

## Calibration and budget

[Official pricing](https://developers.openai.com/api/docs/pricing) is
$2/$0.10/$10 per million uncached input/cached input/output tokens for Standard,
and $1/$0.05/$5 for Flex within the short-context boundary. Focused pricing
tests cover both tiers. Estimates treat every input token as uncached.

The serialized program uses a conservative USD 5 admission ceiling. The
one-call preflight full-bound admission was $0.322000000000, produced by:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py budget-gate --planned-preflight .tmp/gpt61-experiment/preflight-plan.json --pricing-source src/OpenAiIntegration/CostCalculationService.cs --ceiling-usd 5 --report-json .tmp/gpt61-experiment/preflight-budget.json
```

The successful v3 preflight used 2,389 input tokens, 1,568 output tokens,
and 1,549 reasoning tokens. Flex returned resource unavailability; the single
Standard fallback succeeded at observed cost $0.020458000000. It showed no
pressure on the 10,000-token cap. Trace: `4302c1a318eb054c88fa1565b4cc70f3`;
dataset run: `a280a464-a532-453a-8789-8e7c72573003`.

The twenty-call calibration admission was $0.204580000000, with cumulative
projected spend $0.225038000000, from:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py budget-gate --provisional-candidate .tmp/gpt61-experiment/preflight-base-row.json,20 --observed-attempt gpt61-preflight=0.020458 --ceiling-usd 5 --report-json .tmp/gpt61-experiment/calibration-budget.json
```

The calibration uses five random fixtures and four repetitions, sharing the
quality seed and eligible pool. Its historical-artifact hash is
`a8b7adc29aa541fb942d6528a9acbff78cfd2473b3c698fa81548b0cfbe7511e`.
The accepted twenty-item run is `c9a41dbb-b48c-48f3-99d6-136d470c16e7`,
dataset `cmv3kbpx701vdad0j12bh3b9n`, with raw prepared-manifest SHA-256
`33901c316f613b0643e9be8e9b4b4cb3f39a76282ba109be1d502fe652694466`.
Exact dataset-run collection verified all twenty immutable item-to-trace links
against that manifest. Maximum output was 2,600 tokens. Six calls used Flex
and fourteen used the documented Standard fallback. Observed total cost was
$0.179244150000. The stored Flex-assumed, all-input-uncached calibration
total was $0.137911000000, or $0.006895550000 per prediction.

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,200,306,493 --model gpt-6.1-sol --reasoning-effort xhigh --report-json .tmp/gpt61-experiment/estimates.json
```

| Predictions | Candidate Flex estimate |
| ---: | ---: |
| 20 | $0.137911000000 |
| 200 | $1.379110000000 |
| 306 | $2.110038300000 |
| 493 | $3.399506150000 |

The incumbent's existing v2/February-cutoff cost row estimates 200 predictions
at $1.705740000000. This is an admission proxy with a known prompt and sample
mismatch; actual v3 quality-run spend is collected separately. Its command is:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 200 --model gpt-5.6-sol --reasoning-effort xhigh
```

Both quality runs plus a candidate-sized contingency reserve were admitted
at a cumulative projected $4.663662150000, including all settled calibration
spend. The reserve covers fallback or retry pressure; it does not authorize
an automatic replacement run. The exact gate command is:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py budget-gate --candidate gpt-6.1-sol,xhigh,200 --candidate gpt-5.6-sol,xhigh,200 --retry-reserve gpt-6.1-sol,xhigh,200 --observed-attempt gpt61-preflight=0.020458 --observed-attempt gpt61-calibration=0.17924415 --ceiling-usd 5 --report-json .tmp/gpt61-experiment/quality-budget.json
```

This seven-document historical cost proxy can understate live eleven-document
2026/27 input cost. A calibration score is not quality evidence.

## Interpretation constraints

The owner accepted the late-season skew of the available pool. Twenty repeats
per fixture are intentional, preserving the original economical design.
Conclusions apply to this fixed ten-fixture sample and prompt v3; they do not
establish superiority throughout a season. Scores are not directly comparable
with the original 27.8-point result on different fixtures and prompt v2.
Knowledge-cutoff screening reduces known-result leakage risk but cannot prove
the complete absence of contamination.

## Paired quality results

Both models completed 200/200 predictions against the same prepared item set.
The normalized export contains 400 rows, 400 distinct traces, ten source
fixtures per run, and twenty paired repetition totals.

| Model and effort | Total points | Average ten-fixture repetition total | 0/2/3/4-point buckets | Accepted dataset run |
| --- | ---: | ---: | --- | --- |
| GPT-5.6 Sol / xhigh | 199 | 9.95 | 120/41/39/0 | `e43d4c54-3024-414a-b29f-14c2857308b7` |
| GPT-6.1 Sol / xhigh | 196 | 9.80 | 122/38/40/0 | `8033412f-82fe-4d8d-a892-22a75e575bbb` |

The incumbent-minus-candidate mean paired difference is `+0.15` points per
ten-fixture repetition, with a 95% bootstrap confidence interval of
`[-0.40, +0.75]`. Median difference is zero. The paired Wilcoxon signed-rank
test gives `p = 0.7630`; incumbent win/tie/loss counts are `5/12/3` across the
twenty repetition totals. There is no clear evidence of a quality advantage
for either model in this fixed sample. This is not an equivalence test and does
not establish that the models perform identically.

The experiment does not provide a quality-based reason to replace the incumbent.
A midseason upgrade may still be an owner policy decision, but model recency
alone is not a measured quality gain here. Further predictions of these same
fixtures would chiefly refine this sample's model variation rather than broaden
the football situations tested.

## Observed costs and execution

Exact dataset-run/manifest-bound collection verified all 200 generations for
each model. No prediction was rerun. One candidate generation had an empty
session ID and serialized experiment attributes: its data existed but was
omitted from session-based observation listing. The opt-in collector recovery
resolved only the trace already linked to the accepted run, expanded its full
attributes metadata, and verified the exact prepared item identity. The single
missing observation ID in the original normalized export was joined from this
validated usage record, with all 400 trace/run/item/model/effort identities
checked. Final browser provenance shows 200 observations for each model.

Recovered generation `ff047b8da37e6e04`, trace
`8bbbba8d7187aaa9de833c08ea65b9d0`, belongs to FC St. Pauli vs VfL Wolfsburg,
fixture 5, repetition 20. The original export was retained in ignored scratch
state before enrichment. Scores and predicted outputs were not modified.

| Model / xhigh | Observed API cost | Flex / Standard fallback calls | Maximum output tokens | Approximate run wall time |
| --- | ---: | --- | ---: | --- |
| GPT-5.6 Sol | $0.831596800000 | 200 / 0 | 1,798 | 9 min 18 sec |
| GPT-6.1 Sol | $1.256403400000 | 149 / 51 | 4,295 | 17 min 12 sec |

Wall times are measured from run-log creation through final summary write and
include setup, API responses, and Langfuse ingestion/rate-limit waits. They
are not isolated model latency benchmarks. Both models used the same cap,
parallelism, batch count, and Flex-first strategy. The candidate cost more and
finished more slowly under this run's capacity conditions despite lower token
prices. Cache hits and fallback frequency mean observed spend should not be
confused with the stored all-input-uncached, Flex-assumed estimate.

Total settled spend including preflight and calibration is **$2.287702350000**.
There are no unresolved charge reservations. The final estimator accounting
command below reports this as `observedSpendToDateUsd`. Its one-call candidate
is an **unexecuted audit contingency**, because the gate requires a candidate;
the extra $0.006895550000 is excluded from actual spend. No subsequent paid
call was made.

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py budget-gate --candidate gpt-6.1-sol,xhigh,1 --observed-attempt gpt61-preflight=0.020458 --observed-attempt gpt61-calibration=0.17924415 --observed-attempt incumbent-quality=0.831596800000 --observed-attempt candidate-quality=1.256403400000 --ceiling-usd 5 --report-json .tmp/gpt61-experiment/closeout-budget.json
```

## Reproduction and browser report

The shared quality run names are:

```text
repeated-match-slice__pes-squad__gpt-6.1-sol__match-v3__reasoning-xhigh__maxout-10000__random-10x20-seed-20261011__quality__startsat-12h__2026-10-11t08-23-46z
repeated-match-slice__pes-squad__gpt-5.6-sol__match-v3__reasoning-xhigh__maxout-10000__random-10x20-seed-20261011__quality__startsat-12h__2026-10-11t08-23-46z
```

Preparation uses `prepare-repeated-match-slice --competition bundesliga-2025-26
--historical-context-compatibility bundesliga-2025-26-legacy-id-hash-v1
--official-knowledge-cutoff 2026-04-30 --historical-prompt-version 3
--community-context pes-squad --match-count 10 --repetitions 20
--sample-seed 20261011 --starts-after "2026-05-02T00:00:00 Europe/Berlin (+02)"`
with the recorded slice key. Both executions use the recorded manifest and
`--prompt-key bundesliga-match-v3 --prompt-source langfuse
--langfuse-prompt-name kicktippai/bundesliga-2026-27/predict-one-match
--langfuse-prompt-label production --langfuse-prompt-version 3
--reasoning-effort xhigh --max-output-tokens 10000
--evaluation-policy-kind relative --evaluation-policy-offset -12:00:00
--batch-count 7 --parallelism 5 --replace-run`.

The tracked standalone browser report is
[comparison.report.html](../../experiment-analysis/repeated-match-slices/pes-squad/gpt-6-1-sol-production-candidate-2026-10-11/comparison.report.html).
It includes the paired statistics, provenance, point buckets, and interpretation
caveats. Its canonical narrative link points to the committed GitHub writeup,
rather than to an unpublished Pages-relative docs path.

The report is published through the repository's Pages workflow at
[the browser comparison](https://ehonda.github.io/KicktippAi/experiment-analysis/repeated-match-slices/pes-squad/gpt-6-1-sol-production-candidate-2026-10-11/comparison.report.html).

Validation covered Standard/Flex pricing (25 passing cost tests), historical
preparation and run binding (41 passing tests, followed by the 33-test runner
class including the new v3 hash test), 29 skill-local estimator tests including
session recovery, and 16 repository estimator tests. Local Pages generation
verified the new report's index entry and the GitHub canonical writeup link.

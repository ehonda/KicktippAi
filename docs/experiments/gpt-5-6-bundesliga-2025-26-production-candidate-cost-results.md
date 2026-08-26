# GPT-5.6 Bundesliga 2025/26 production-candidate cost results

**Status:** Cost phase complete; quality execution contract frozen; quality
artifact preparation and runs not started

**Executed:** 2026-08-26

This report records the cost evidence for the owner's exact nine-configuration
GPT-5.6 matrix. It does not select a production model or arena participant and
does not make a prediction-quality claim. The separate paired quality phase
remains governed by the
[preregistration](gpt-5-6-bundesliga-2025-26-production-candidate-preregistration.md).

## Decision context

The owner supplied these captures when narrowing the candidate surface. They
are preserved decision context, not Kicktipp results. Current model facts and
pricing were independently reverified from official OpenAI sources on the
execution date as described in the preregistration.

![Owner capture of OpenAI Flex pricing for current flagship models](assets/gpt-5-6-production-candidate-selection/openai-api-flex-pricing-owner-capture-2026-08-26.png)

![Owner capture comparing GPT-5.4 and GPT-5.6 Terra benchmarks](assets/gpt-5-6-production-candidate-selection/gpt-5-4-vs-gpt-5-6-terra-benchmarks-owner-capture.png)

![Owner capture comparing GPT-5.4 and GPT-5.6 Terra benchmark-task cost](assets/gpt-5-6-production-candidate-selection/gpt-5-4-vs-gpt-5-6-terra-cost-benchmark-owner-capture.png)

## Method and limitations

All new authoritative rows use the same frozen `5 × 4` manifest, SHA-256
`fcadeeadaadd1356472a1f4f96b7277a05ba3b8a19dcde60e6f2e7d79af577b7`,
dataset ID `cmt86m8gn0awvad0eyx7mn5f6`, hosted match prompt version `2` with
`production` membership, `startsAt -12h`, cutoff `2026-02-16`, and sampling
boundary `2026-02-18T00:00:00 Europe/Berlin (+01)`. Each completed row is
immutably bound to exactly 20 distinct dataset-item-to-trace links.

The historical route contains seven context documents. These results are
preseason cost proxies and may understate live Bundesliga 2026/27 input cost,
where the runtime route contains eleven documents. Authoritative projections
use uncached-input Flex prices even where Langfuse observed cached input. The
observed paid cost is therefore execution evidence, while the uncached Flex
estimate is the deliberately conservative basis for experiment and season
planning.

The [official Bundesliga fixture explainer](https://www.bundesliga.com/en/bundesliga/news/how-the-bundesliga-fixture-list-is-made-bayern-munich-borussia-dortmund-20316)
establishes the `306` scheduled-match baseline. The realistic `493`-call count
reuses the documented `pes-squad` / `o3` evidence of `313` initial calls and
`191` extra repredictions: `306 + round(306 × 191 / 313) = 493`. The complete
derivation is retained in the
[whole-season estimates](whole-season-cost-estimates.md); no new Firestore read
was needed.

## Authoritative cost rows

| Model / effort | Cap | Exact dataset run | Observed tiers | Max output | Observed paid cost | Conservative 20-call cost | Average / prediction | 306 calls | 493 calls |
|---|---:|---|---|---:|---:|---:|---:|---:|---:|
| Sol / `high` | 10000 | `3297864a-93fd-4c05-a153-8cbf738c37f1` | Flex 1; Standard fallback 19 | 306 | `$0.146277600000` | `$0.135904000000` | `$0.006795200000` | `$2.079331200000` | `$3.350033600000` |
| Sol / `medium` | 10000 | `d45fb91a-0fe8-4a99-976d-1340fd448ed9` | Flex 20 | 266 | `$0.048141800000` | `$0.118304000000` | `$0.005915200000` | `$1.810051200000` | `$2.916193600000` |
| Sol / `none` | 10000 | `c5d1ac4b-ee82-4269-85e0-c70613f0da63` | Flex 20 | 17 | `$0.030741800000` | `$0.100904000000` | `$0.005045200000` | `$1.543831200000` | `$2.487283600000` |
| Terra / `xhigh` | 10000 | `d4e1164e-a603-4fda-b8fd-acbcb303678e` | Flex 20 | 438 | `$0.047684900000` | `$0.082766000000` | `$0.004138300000` | `$1.266319800000` | `$2.040181900000` |
| Terra / `medium` | 10000 | `fa4723aa-64b3-4033-8bbd-ecfbf3e5a370` | Flex 20 | 378 | `$0.029846900000` | `$0.064928000000` | `$0.003246400000` | `$0.993398400000` | `$1.600475200000` |
| Terra / `none` | 10000 | `0e698674-d7e7-410f-ab07-596ca3d9b702` | Flex 20 | 17 | `$0.015710900000` | `$0.050792000000` | `$0.002539600000` | `$0.777117600000` | `$1.252022800000` |
| Luna / `max` | 20000 | `0aa5d30c-bba7-4312-b1bf-e0a7bed8f2ad` | Flex 20 | 16690 | `$0.047204090000` | `$0.050490800000` | `$0.002524540000` | `$0.772509240000` | `$1.244598220000` |
| Luna / `medium` | 10000 | `d115ee3b-e00b-4671-90cf-d0744d555522` | Flex 20 | 331 | `$0.003392090000` | `$0.006900200000` | `$0.000345010000` | `$0.105573060000` | `$0.170089930000` |
| Luna / `none` | 10000 | `6a3c4e70-ebb4-4c07-9a1a-7af19c32d995` | Flex 20 | 17 | `$0.000696920000` | `$0.005079200000` | `$0.000253960000` | `$0.077711760000` | `$0.125202280000` |

Luna/`none` is the pre-existing authoritative row and was reused without a new
model call. Sol/`high` completed all 20 cells, but 19 requests used the
runner's documented Standard fallback after Flex 429 responses. Its
authoritative planning row remains priced at Flex as required by the estimate
process; the observed paid amount separately records the mixed-tier execution.

Luna/`max` required cap remediation. The initial cap-`10000` five-by-four
attempt stopped after one response contained no output text, another visible
response reached `8970 / 10000`, and only four of five first-batch calls became
observable. The retained conservative reservation for the unresolved fifth
provider charge is `$0.033200000000`. A reviewed cap-`20000` one-item probe
then succeeded, and the accepted 20-call row completed at that cap. Its maximum
observed output was `16690 / 20000`: below cap, but materially closer than the
one-item probe.

## Preflight evidence

| Model / effort | Cap | Exact dataset run | Input | Output | Reasoning | Tier / fallback | Observed cost |
|---|---:|---|---:|---:|---:|---|---:|
| Terra / `xhigh` | 10000 | `2671c389-40fe-4212-b8b0-4bd7d2fa49d6` | 2463 | 249 | 230 | Standard fallback after Flex 429 | `$0.007914000000` |
| Sol / `high` | 10000 | `85401acb-8196-4df4-9c02-ec7d5313ce5e` | 2463 | 86 | 67 | Flex | `$0.005786000000` |
| Luna / `medium` | 10000 | `b6e74bee-9f65-4c6d-a8e6-ac9a2ba0a9ad` | 2463 | 125 | 106 | Flex | `$0.000321300000` |
| Terra / `medium` | 10000 | `75e0b8df-0a68-4687-8146-610fc7b0dc17` | 2463 | 143 | 124 | Flex | `$0.003321000000` |
| Sol / `medium` | 10000 | `4a0cd8d6-6ae9-41b1-bb74-807dd58e0200` | 2463 | 68 | 49 | Flex | `$0.005606000000` |
| Terra / `none` | 10000 | `1876de2b-4fd3-45da-b0d5-525fc33921a6` | 2463 | 17 | 0 | Flex | `$0.002565000000` |
| Sol / `none` | 10000 | `8d099175-4382-4917-a569-fa3dea66d892` | 2463 | 17 | 0 | Flex | `$0.005096000000` |

The Luna/`max` cap-`10000` and cap-`20000` probe lineage is retained in the
[preflight evidence store](../../.agents/skills/estimate-experiment-cost-skill/references/preflight-evidence.md)
and the preregistration's live checkpoint.

## Program ledger and next-phase projection

The final cost-phase Decimal ledger records 18 settled P0-23 attempts at
`$0.410982080000`. It keeps the unresolved Luna/`max` failed-call reservation
at `$0.033200000000`. The ledger does not add the older reused Luna/`none` row
to new P0-23 spend.

The next preregistered topology is all nine candidates at `10 × 20 = 200`
predictions each. A no-spend `budget-gate` over the nine authoritative rows
reported:

| Machine field | USD |
|---|---:|
| `observedSpendToDateUsd` | `$0.410982080000` |
| `unsettledReservationTotalUsd` | `$0.033200000000` |
| `projectedWaveCostUsd` | `$6.160682000000` |
| `allInProjectedTotalUsd` | `$6.604864080000` |
| `remainingUsd` | `$23.395135920000` |

The result was `allowed` under the strict USD 30 ceiling. Ignored payload-safe
gate artifact
`.tmp/p0-23-budget/full-nine-10x20-quality-topology-budget-gate.json` has
SHA-256
`28f6d471315aaaca27188343052fdbd1445d3d1d541c3b65b2ea9d7d32902c84`.
This is planning evidence only; this cost-phase closeout did not run
the quality wave.

The later reviewed docs-only Phase-B freeze explicitly selects seed `20260821`,
`10 × 20`, slice key
`random-10x20-seed-20260821-gpt-5-6-production-candidate-quality`, batch count
`7`, initial fixture parallelism `5`, one shared UTC run stamp, and serialized
order Sol `high` / `medium` / `none`, Terra `xhigh` / `medium` / `none`, Luna
`max` / `medium` / `none`. Luna/`max` retains cap `20000`; every other
configuration retains cap `10000`. Matching the cost seed is an explicit
reviewed selection, not reuse of a cost artifact. The freeze made no external
read or write and created no quality score.

For each fixture, one warmup plus the seven post-warmup groups
`3, 3, 3, 3, 3, 2, 2` preserve all 20 repetitions. Peak model-call concurrency
is `15` for p5, `9` for p3, and `3` for p1, while candidate serialization and
the 200-item common manifest remain unchanged.

After that freeze is integrated, pushed, and exact-head green, one no-spend
preparation checkpoint must materialize and record the selected ten IDs/hash,
raw dataset/manifest hashes, canonical historical-artifact hash, frozen
`109`-fixture pool identity, and `200` / `200` counts before sync or spend. The
generic route now distinguishes cost evidence from quality correctly: token and
cost rows are not quality evidence, while the separately preregistered
cutoff-safe common-manifest run over completed outcomes is valid scored quality
evidence under ADR-0049. The seven-document route can still understate live
eleven-document cost.

The full-topology JSON remains planning evidence rather than a reusable live
admission. Before every candidate or exact-setting `p3` / `p1` retry, write and
hash a fresh candidate-specific gate containing all 18 settled cost attempts,
all earlier settled quality attempts, the fixed `$0.033200000000` reserve,
exactly one 200-call candidate, strict USD 30, and no speculative retry reserve.
Existing exact rows and caps mean there is no quality preflight. If Langfuse
ingestion lags, recollect instead of rerunning.

## Reproduction

Each row was persisted with `upsert-row` only after exact 20-item collection.
The reported planning counts were regenerated immediately before this report
with these exact commands:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,100,150,200,306,493 --model gpt-5.6-sol --reasoning-effort high
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,100,150,200,306,493 --model gpt-5.6-sol --reasoning-effort medium
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,100,150,200,306,493 --model gpt-5.6-sol --reasoning-effort none
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,100,150,200,306,493 --model gpt-5.6-terra --reasoning-effort xhigh
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,100,150,200,306,493 --model gpt-5.6-terra --reasoning-effort medium
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,100,150,200,306,493 --model gpt-5.6-terra --reasoning-effort none
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,100,150,200,306,493 --model gpt-5.6-luna --reasoning-effort max
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,100,150,200,306,493 --model gpt-5.6-luna --reasoning-effort medium
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,100,150,200,306,493 --model gpt-5.6-luna --reasoning-effort none
```

The full-nine next-phase projection used nine explicit
`--candidate MODEL,EFFORT,200` entries, all 18 named settled attempts, the
named `$0.033200000000` reservation, and `--ceiling-usd 30`. Prompt, context,
prediction, and credential payloads were not retained in tracked evidence.

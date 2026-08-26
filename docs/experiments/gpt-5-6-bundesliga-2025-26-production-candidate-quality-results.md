# GPT-5.6 Bundesliga 2025/26 production-candidate quality results

**Status:** Evidence complete; post-hoc Sol/`max` extension complete

**Executed:** 2026-08-26; post-hoc Sol/`max` extension 2026-08-27 local

This report closes the P0-23 evidence collection without selecting a production
model or an arena participant. Nine configurations completed the same
cutoff-safe `10 × 20` paired sample. The originally preregistered Luna/`max`
configuration did not complete after two transient capacity failures and has no
score or rank. The Owner explicitly stopped the planned p1 retry. After the
original eight accepted scores were visible, the Owner added Sol/`xhigh`; its
cost row and quality run completed. Sol/`xhigh` and every inference from the
resulting nine-run family are therefore exploratory, data-dependent, and not a
preregistered confirmatory test. In the same closeout decision that selected
Sol/`xhigh` for production, the Owner requested one further Sol/`max` run on the
same sample as exploratory arena evidence. It completed but was necessarily
even more post-hoc: it did not inform the production choice and does not reopen
it.

The current browser-friendly ten-run report is
[published under the experiment-analysis tree](../../experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20260217t230000z/random-10x20-seed-20260821-gpt-5-6-production-candidate-quality/gpt-5-6-production-candidate-quality-plus-sol-max-2026-08-26t22-24-45z.analysis.report.html).
The [original nine-run report](../../experiment-analysis/repeated-match-slices/pes-squad/all-matchdays-after-20260217t230000z/random-10x20-seed-20260821-gpt-5-6-production-candidate-quality/gpt-5-6-production-candidate-quality-2026-08-26t12-03-30z.analysis.report.html)
remains the immutable publication of the earlier checkpoint.
The frozen design and cost evidence remain in the
[preregistration](gpt-5-6-bundesliga-2025-26-production-candidate-preregistration.md)
and [cost-results report](gpt-5-6-bundesliga-2025-26-production-candidate-cost-results.md).

## Decision context

The Owner supplied these captures when narrowing the candidate surface. They
are preserved as selection rationale, not as Kicktipp experiment results.

![Owner capture of OpenAI Flex pricing for current flagship models](assets/gpt-5-6-production-candidate-selection/openai-api-flex-pricing-owner-capture-2026-08-26.png)

![Owner capture comparing GPT-5.4 and GPT-5.6 Terra benchmarks](assets/gpt-5-6-production-candidate-selection/gpt-5-4-vs-gpt-5-6-terra-benchmarks-owner-capture.png)

![Owner capture comparing GPT-5.4 and GPT-5.6 Terra benchmark-task cost](assets/gpt-5-6-production-candidate-selection/gpt-5-4-vs-gpt-5-6-terra-cost-benchmark-owner-capture.png)

## Frozen sample and provenance

All accepted runs bind exact Langfuse dataset
`cmta0tdfc00rnad0fnbxelgkk`, dataset name
`match-predictions/bundesliga-2025-26/pes-squad/repeated-match-slices/all-matchdays-after-20260217t230000z/random-10x20-seed-20260821-gpt-5-6-production-candidate-quality`,
hosted prompt `kicktippai/bundesliga-2026-27/predict-one-match` version `2`
with `production` membership, batch count `7`, parallelism `5`, and evaluation
time `startsAt -12h`. The original nine accepted runs used the shared stamp
`2026-08-26t12-03-30z` and cap `10000`. The later Sol/`max` run used its own
post-hoc stamp `2026-08-26t22-24-45z` and its established cap `20000`. The
prompt was not changed by either experiment.

The hardest common knowledge cutoff was `2026-02-16`; eligibility starts
strictly after `2026-02-18T00:00:00 Europe/Berlin (+01)`. Preparation found
`109` completed eligible fixtures, with sorted-ID SHA-256
`6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415`.
Seed `20260821` selected these ten canonical source items exactly once:

- `bundesliga-2025-26__pes-squad__ts1423757253`
- `bundesliga-2025-26__pes-squad__ts1423757257`
- `bundesliga-2025-26__pes-squad__ts1423757259`
- `bundesliga-2025-26__pes-squad__ts1423757263`
- `bundesliga-2025-26__pes-squad__ts1423757265`
- `bundesliga-2025-26__pes-squad__ts1423757286`
- `bundesliga-2025-26__pes-squad__ts1423757292`
- `bundesliga-2025-26__pes-squad__ts1423757328`
- `bundesliga-2025-26__pes-squad__ts1423757333`
- `bundesliga-2025-26__pes-squad__ts1423757341`

Their sorted-newline hash is
`e154d604d1aa63bf837c5361d949f27a57de676bed74c4407ce6ed9ca2140b2c`.
The raw dataset, manifest, and canonical historical-artifact SHA-256 values are
`0011ae5de0434aec303810fefd13070b0e0cdfe6b6c74e05a985682deced0f5b`,
`5953b332fbcfba668223a2071ebc78beac85c0ce66561005fd5a89f4b4a33ca4`,
and `5471467e42133fb3427f0772b3a3135380d6a3eeb31a6f585750263cd181f220`.
The manifest and hosted dataset each contain the same `200` stable items; the
item-set SHA-256 is
`08be7ae653ed52f9fa412a244ec2ab46ffc75adead3183149fb27015d9381daf`.
Every fixture has repetitions `1..20`, all five cost fixtures are included by
the intentionally reused seed, and all repetitions of a fixture share the same
seven-document context snapshot.

## Results

The primary metric is the average Kicktipp-point total per paired repetition.
Each repetition total contains the same ten fixtures, so the inferential sample
size is `20`, not `200`. Total points sum all 200 scored predictions. Observed
cost records this execution; the separately estimated 493-call cost is the
conservative uncached-input Flex season-planning figure.

| Rank | Configuration | Dataset run ID | Total / avg repetition total | Point buckets `0/2/3/4` | Observed tiers | Max output | Observed cost | 493-call estimate | Dataset-run start UTC | Retained execution span |
|---:|---|---|---:|---:|---|---:|---:|---:|---|---:|
| 1 | Sol / `xhigh` | `c78f79b6-f1e1-47da-befe-39704efd1af4` | `556 / 27.8` | `48/16/20/116` | Flex 161; Standard fallback 39 | 1963 | `$1.203109800000` | `$4.204649100000` | `2026-08-26T15:50:12Z` | `536.550s` |
| 2 | Sol / `max` | `444633f8-9e75-4c5d-bafb-6db4dfa41c44` | `552 / 27.6` | `47/20/20/113` | Flex 198; Standard fallback 2 | 17831 | `$3.449498200000` | `$7.903381600000` | `2026-08-26T22:26:06Z` | `1488.634s` observation span |
| 3 | Sol / `high` | `ebeb2218-7f77-4bdc-a415-61a23dfb566d` | `528 / 26.4` | `58/10/20/112` | Flex 69; Standard fallback 131 | 1339 | `$1.165440000000` | `$3.350033600000` | `2026-08-26T12:07:18Z` | not retained |
| 4 | Sol / `medium` | `82bd2eb7-fda4-4247-a924-f37b6b90f790` | `510 / 25.5` | `66/3/20/111` | Flex 200 | 487 | `$0.434544600000` | `$2.916193600000` | `2026-08-26T12:25:46Z` | `414.081s` |
| 5 | Luna / `medium` | `63463a23-2f99-4c48-990b-c5493482265f` | `490 / 24.5` | `70/10/10/110` | Flex 200 | 401 | `$0.025842800000` | `$0.170089930000` | `2026-08-26T15:09:24Z` | `409.105s` |
| 6 | Sol / `none` | `913f2985-e1aa-4111-90d9-22410536641b` | `460 / 23.0` | `79/3/18/100` | Flex 200 | 17 | `$0.198269600000` | `$2.487283600000` | `2026-08-26T12:35:00Z` | not retained |
| 7 | Terra / `xhigh` | `b78e0fca-f785-4d51-994f-5c8f73f8edce` | `448 / 22.4` | `80/6/20/94` | Flex 186; Standard fallback 14 | 1490 | `$0.519026300000` | `$2.040181900000` | `2026-08-26T12:44:28Z` | not retained |
| 8 | Terra / `medium` | `fecfe01c-4109-47d2-95fe-13aeafbc202e` | `404 / 20.2` | `92/4/20/84` | Flex 181; Standard fallback 19 | 353 | `$0.277898000000` | `$1.600475200000` | `2026-08-26T12:53:14Z` | `409.704s` |
| 9 | Terra / `none` | `08806884-75df-4c43-8cb0-46c01fa127bb` | `342 / 17.1` | `107/5/20/68` | Flex 182; Standard fallback 18 | 17 | `$0.134951500000` | `$1.252022800000` | `2026-08-26T13:01:43Z` | `403.031s` |
| 10 | Luna / `none` | `56d2750d-51dc-4a52-8dba-310256a70f72` | `312 / 15.6` | `115/8/12/65` | Flex 199; Standard fallback 1 | 17 | `$0.010064270000` | `$0.125202280000` | `2026-08-26T15:19:09Z` | `650.309s` |

The wrapper duration is the exact local runner interval where its payload-safe
terminal record was retained. For Sol/`max`, the core-only Langfuse observation
boundary is exact: `400` observations span
`2026-08-26T22:26:12.542Z` through `2026-08-26T22:51:01.176Z`, or
`1488.634s`, with no further page. The normalized report records the exact
dataset-run start for all ten runs, so missing wrapper durations do not weaken
run identity, score, usage, or pairing provenance.

## Statistical comparison

The reviewed report uses a Friedman omnibus test over the 20 paired repetition
totals, two-sided paired Wilcoxon signed-rank tests, Holm correction at
`alpha=0.05`, `10,000` bootstrap resamples, `95%` confidence intervals, and
seed `20260406`. In the ten-run exploratory family, the Friedman statistic is
`142.91863765373702`, with `p=2.5743318532557758e-26`. The omnibus gate
therefore permits interpretation of the 45 corrected pairwise comparisons.

The generated rank order is descriptive. In the exploratory ten-run family:

- Sol/`max` is second on the point estimate at `27.6`, immediately behind
  Sol/`xhigh` at `27.8`. Their paired mean difference is only `0.2`, the
  bootstrap 95% interval is `[-1.2, 1.6]`, their repetition-total outcome is
  `6/9/5`, and Holm-adjusted `p=0.8918`; this sample provides no evidence that
  either one outperforms the other.
- Sol/`max` is also not Holm-significantly different from Sol/`high` (`+1.2`,
  adjusted `p=0.7698`) or Sol/`medium` (`+2.1`, adjusted `p=0.2605`). It does
  exceed Luna/`medium` in this exploratory family (`+3.1`, adjusted
  `p=0.0064`).
- Sol/`xhigh` is first on the point estimate, but its difference from
  Sol/`high` is not Holm-significant (`+1.4`, adjusted `p=0.2688`), nor is its
  difference from Sol/`medium` (`+2.3`, adjusted `p=0.0873`).
- Among the original completed configurations, Sol/`high`, Sol/`medium`, and
  Luna/`medium` form the leading point-estimate group. None of their pairwise
  differences is significant in this ten-run corrected family.
- Sol/`xhigh` does exceed Luna/`medium` in this sample (`+3.3`, adjusted
  `p=0.0095`), but that comparison is post-hoc and data-dependent.
- Luna/`medium` combines an average repetition total of `24.5` with a
  conservative 493-call estimate of `$0.170089930000`; Sol/`high` reaches
  `26.4` at `$3.350033600000`, exploratory Sol/`xhigh` reaches `27.8` at
  `$4.204649100000`, and exploratory Sol/`max` reaches `27.6` at
  `$7.903381600000`. This is evidence for the Owner's quality/cost tradeoff,
  not an automatic winner rule.

No inference including Sol/`xhigh` or Sol/`max` is confirmatory. Sol/`max` was
chosen after all earlier scores and the production decision were known, so it
is exploratory arena evidence only. A fresh preregistered sample would be
required for a confirmatory comparison. These 2025/26 post-cutoff fixtures are
the only available preseason outcomes, and the small ten-fixture surface may
not generalize to the 2026/27 season. Repetitions reduce model-output variance
on the same fixtures; they do not create 200 independent football matches.

## Luna/max incomplete operational evidence

The preregistered Luna/`max` candidate used cap `20000`. Its p5 attempt stopped
after 65 linked items, with 64 visible Flex usages costing `$0.154785140000`;
maximum visible output was `18147`. Exact run ID
`5b73a8ae-8fd0-449c-9bb4-6de9ae27fcad` ran from
`2026-08-26T13:10:16.6516712Z` to `2026-08-26T13:52:34.8353612Z`
(`2538.184s`). A separately admitted p3 retry stopped after 21 links, with 20
visible Flex usages costing `$0.065955380000`; maximum visible output was
`18646`. Exact run ID `9acbdad4-e1e2-49cf-9ce7-14c05c31361a` ran from
`2026-08-26T14:24:49.9668881Z` to `2026-08-26T14:50:23.7066474Z`
(`1533.740s`). Each incomplete attempt retains a separate `$0.033200000000`
reservation for one unobservable call.

Both failures were classified as transient capacity failures rather than cap,
identity, pricing, or invalid-score failures. The Owner explicitly overrode the
planned p1 retry and directed that Luna/`max` be abandoned for this matrix.
Consequently it is incomplete and excluded: no score, rank, confidence
interval, or imputed quality value is reported. The two attempts remain useful
only as operational rate/capacity evidence.

## Cost settlement

The final Decimal ledger contains 31 observed attempts:

| Component | Observed USD |
|---|---:|
| Original cost phase, 18 attempts | `$0.410982080000` |
| Quality phase, nine accepted configurations plus two failed Luna/max attempts | `$4.189887390000` |
| Appended Sol/xhigh one-item preflight and five-by-four cost row | `$0.107467800000` |
| **Total observed** | **`$4.708337270000`** |
| Three separately named unresolved-call reservations | **`$0.099600000000`** |
| **Observed plus reservations** | **`$4.807937270000`** |
| **Remaining under USD 30** | **`$25.192062730000`** |

The final settlement JSON has SHA-256
`546b4b8eb48ebe2adddc551f3c6d39b5a38d6132c598cf2eed05d8c7d31d729e`.
The settled-attempt ledger has SHA-256
`3d6860f620397ef694af81970cbb7133c6337f6e385e0f76747c88b0f8e0c9d0`.
That is the immutable P0-23 checkpoint before the later Sol/`max` request.

The post-hoc Sol/`max` extension reused the exact authoritative cost row rather
than creating another row. Before model construction, budget gate
`33fd97f4753edd3ad323d129083a198db2808eb5adcb21ab1b688b21959d0dcb`
machine-projected `$3.206240000000` for 200 calls, `$8.383827070000` all-in,
and `$21.616172930000` remaining under the unchanged USD 30 ceiling. The
accepted quality run cost `$3.449498200000`, including two Standard fallbacks.
Its immutable 200-record usage and summary SHA-256 values are
`800410bdfca342d2f9f04199b583d9a5676c63ab4be08e7ca283dde53e443b8d`
and `5d8b62b9cedaf9b76f9234d61128348b70ce26da7cb38b03b45ae639667490f2`.

The exact pre-spend estimator command was:

```powershell
uv --cache-dir .uv-cache run --no-sync python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py budget-gate --candidate gpt-5.6-sol,max,200 --observed-attempt p0-23-settled-31-attempts=4.708337270000 --observed-attempt sol-max-cost-calibration=0.369649800000 --reservation luna-max-base-row-unobservable-call=0.033200000000 --reservation luna-max-quality-p5-unobservable-call=0.033200000000 --reservation luna-max-quality-p3-unobservable-call=0.033200000000 --ceiling-usd 30 --report-json .tmp/p0-23-budget/gpt-5.6-sol-max-posthoc-quality-10x20-p5-budget-gate.json
```

The final machine ledger now records `$8.527485270000` observed across the
original 31 attempts, the Sol/`max` cost calibration, and this quality run. It
retains the three older `$0.033200000000` reservations. Because `budget-gate`
requires a candidate, the final settlement also includes one explicitly
unexecuted `$0.016031200000` authoritative-row guard: bounded all-in is
`$8.643116470000` and remaining allowance is `$21.356883530000`. No call was
made for that guard. Final settlement SHA-256 is
`61cabad269d292e6d62736db68acaea6c19f433c70aa9a0c771a96430222231b`.

## Analysis artifacts and validation

The normalized export contains exactly nine accepted dataset runs, 1,800 rows,
1,800 distinct trace IDs, 1,800 distinct observation IDs, the exact common
200-item set, and no Luna/`max` row. Its SHA-256 is
`c10dcf6978cc172ad26404f5e4a4556431d600f0563dbcbde464495df8ecb00c`.
The generated JSON, Markdown, and HTML report SHA-256 values are:

- JSON: `fcd9cdc8d43e1e0b5ef01cfa09c5fcd6c49860f637a4c580374c9b1acc51f460`
- Markdown: `8e645d103d438b3fd21a7fc1cb03254c3c2b7f20be207eea8f2a57bfaddd3cb0`
- HTML: `38614b8ea20a782d810d834326af710f9a84f1e5a9a765af6426ba434c47b1e4`

Mechanical validation bound every exact dataset-run ID, rank, score bucket,
fixture/repetition set, trace/observation count, output cap, prompt identity,
and comparison unit. It also proves that the generated Markdown and standalone
Pages HTML visibly disclose the post-hoc Sol/xhigh addition and incomplete
Luna/max exclusion and link this canonical write-up. The export validator and
statistical-report validator evidence SHA-256 values are
`ff6c8100f311ee29db32e5c0088662723664a7e692ea57bdb56741e996e442b3`
and `c9b9ac600ab0584cdbe12a0a3489d774b5899e85e0ec03734727cdb2ddf19e3b`.
One read-only export request received HTTP 429; the exporter honored the
server's 42-second retry delay and converged without rerunning any model.

The later normalized ten-run export contains exactly 2,000 rows, 2,000
distinct trace IDs, 2,000 distinct observation IDs, ten exact dataset-run IDs,
and the same common 200-item set. It still contains no Luna/`max` row; its one
new row is the completed Sol/`max` configuration. The export SHA-256 is
`6fd569ab255ea03a5761cf59ae6a2ac0b39ffab32aef58dda3b28d48d530848c`.
The regenerated JSON, Markdown, and HTML report SHA-256 values are:

- JSON: `bc41bdeb646f7859d60f0e5b31f9e7e447ba339c9034d8835b2d0fdac5839966`
- Markdown: `133f0a39427deff814954b3f41a474a8e67ef58d72b0432db94d11f9bdb47ba7`
- HTML: `fcae684b338d3ad7f5175c21bf2f596d0f3910efaa74826c1f1dbefa857332c3`

Mechanical validation passed all 19 identity, count, pairing, prompt, cap,
score, caveat, and canonical-link checks. The ten-run export encountered one
read-only HTTP 429; the exporter honored its 34-second retry and converged
without another model call.

## Owner decision boundary

P0-23 supplied the original selection evidence. In the same closeout decision,
the Owner chose Sol/`xhigh` for production and requested this non-blocking
Sol/`max` extension. This post-hoc result is exploratory arena evidence only
and does not reopen or replace that selection. No production workflow,
community prediction, participant topology, prompt, or schedule was changed by
this analysis lane.

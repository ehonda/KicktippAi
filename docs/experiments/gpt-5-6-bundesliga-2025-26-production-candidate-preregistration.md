# GPT-5.6 Bundesliga 2025/26 production-candidate preregistration

**Status:** LIVE EXECUTION PAUSED AT CAP GATE — the Luna/`max` one-item
preflight passed at cap `10000`, but its admitted five-by-four row stopped in
the first batch after one response returned no output text. No authoritative
row was upserted and no later candidate or quality run started.

**Verified:** 2026-08-26

This preregistration defines the P0-23 evidence program for the Bundesliga
2026/27 production-model and arena-participant decisions. It replaces the
superseded Terra/`medium`, Sol/`medium`, cap-`10000`, fixed-`15 × 20`, and
separate-phase-budget example with the owner's exact nine-row matrix, the
estimate-row-derived cap workflow, an adaptive paired quality design, and one
cumulative USD 30 ceiling.

USD 30 is a hard ceiling, not a spending target. The program should finish for
less whenever the evidence is sufficient. The authorized one-item dataset sync,
Luna/`max` preflight, and failed first-batch five-by-four attempt below have now
created Langfuse dataset/run/trace/score state and model usage. They did not
mutate hosted-prompt content or labels, select a production model or arena
participant, post a community prediction, dispatch a production workflow, or
activate a schedule. Independent review and budget-tool integration still do
not bypass any remaining gate or the separate admission of every paid attempt.

Related planning and decisions:

- [P0-23 — GPT-5.6 production-candidate evidence](../../plans/bundesliga-2026-27/tasks/p0-23-gpt-5-6-production-candidate-evidence.md)
- [ADR-0006 — stage validation with a cheap test model](../../plans/bundesliga-2026-27/decisions/0006-stage-validation-with-a-cheap-test-model.md)
- [ADR-0033 — pin the validation-model ledger and reserve production selection](../../plans/bundesliga-2026-27/decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md)
- [ADR-0040 — hash-bound Bundesliga 2025/26 experiment compatibility](../../plans/bundesliga-2026-27/decisions/0040-use-hash-bound-2025-26-context-for-preseason-cost-experiments.md)
- [ADR-0043 — freeze historical aliases and the eligible pool](../../plans/bundesliga-2026-27/decisions/0043-freeze-historical-experiment-aliases-and-eligible-pool.md)
- [ADR-0046 — bind cost usage to exact Langfuse dataset runs](../../plans/bundesliga-2026-27/decisions/0046-bind-cost-usage-to-langfuse-dataset-runs.md)
- [ADR-0049 — preregister GPT-5.6 candidate evidence under one program ceiling](../../plans/bundesliga-2026-27/decisions/0049-preregister-gpt-5-6-candidate-evidence.md)
- [Luna base-estimate evidence](../../.agents/skills/estimate-experiment-cost-skill/references/gpt-5.6-luna-none-base-estimate-2026-08-25.md)

## Decision questions

The evidence must let the owner decide:

1. which configuration gives the best measured Kicktipp performance while its
   306-call and realistic 493-call whole-season estimates remain acceptable;
2. which additional configurations provide a useful spread of capability and
   cost for `ehonda-ai-arena`; and
3. whether preliminary evidence is strong enough or another explicitly funded
   experiment round is worthwhile.

P0-23 reports evidence and uncertainty. It does not make any of those owner
decisions.

## Owner rationale and supplied captures

The owner narrowed the surface to GPT-5.6 because GPT-5.5 is too expensive for
this workload and GPT-5.4 is close to Terra in the supplied capability evidence
while Terra is cheaper at current API rates. These captures preserve that
pre-filtering rationale; they are not Kicktipp experiment results. The pricing
facts used by the calculator are independently linked to current official
OpenAI documentation below. The two benchmark captures are owner-supplied
decision context whose original page URLs were not recorded, so this document
does not elevate them to independently verified primary-source evidence.

![Owner capture of OpenAI Flex pricing for current flagship models](assets/gpt-5-6-production-candidate-selection/openai-api-flex-pricing-owner-capture-2026-08-26.png)

![Owner capture comparing GPT-5.4 and GPT-5.6 Terra benchmarks](assets/gpt-5-6-production-candidate-selection/gpt-5-4-vs-gpt-5-6-terra-benchmarks-owner-capture.png)

![Owner capture comparing GPT-5.4 and GPT-5.6 Terra benchmark-task cost](assets/gpt-5-6-production-candidate-selection/gpt-5-4-vs-gpt-5-6-terra-cost-benchmark-owner-capture.png)

Tracked capture integrity:

| Capture | SHA-256 |
|---|---|
| OpenAI Flex pricing | `dc88f9e8b33feb2dd34eb0b7dad1cd543ff555e115ab067540d01cc9616bd938` |
| GPT-5.4 / Terra benchmark table | `4045e59249a80853dba39e4b3a950b395b3e32ba3c808f6b4d9c3491d49f3f67` |
| GPT-5.4 / Terra benchmark-task cost | `38c9d8d5e80d2b9a53927ddcee46f843786302f129c3972d68e1ff1018dcb606` |

## Exact owner-authorized matrix

Every run uses an exact model ID; the unsuffixed `gpt-5.6` alias is excluded
because it currently routes to Sol and would weaken provenance.

| Model | Effort | Evidence role | Cost-row state |
|---|---|---|---|
| `gpt-5.6-sol` | `high` | Sol quality-first candidate | Missing |
| `gpt-5.6-sol` | `medium` | Sol balanced candidate | Missing |
| `gpt-5.6-sol` | `none` | Sol no-reasoning baseline | Missing |
| `gpt-5.6-terra` | `xhigh` | Terra quality-first candidate | Missing |
| `gpt-5.6-terra` | `medium` | Terra balanced candidate | Missing |
| `gpt-5.6-terra` | `none` | Terra no-reasoning baseline | Missing |
| `gpt-5.6-luna` | `max` | Luna quality-first candidate | Missing; cap-`10000` full row invalid, one cap-`20000` remediation preflight pending exact-SHA review |
| `gpt-5.6-luna` | `medium` | Luna balanced candidate | Missing |
| `gpt-5.6-luna` | `none` | Luna no-reasoning baseline and plumbing identity | Reuse exact authoritative row; do not rerun |

The owner explicitly authorized the missing cost rows and the later quality
experiments within the cumulative ceiling. That authorization does not select
any row for production or authorize any community post or schedule.

## Current official facts and local support audit

The official OpenAI model pages were fetched on 2026-08-26 for
[`gpt-5.6-sol`](https://developers.openai.com/api/docs/models/gpt-5.6-sol),
[`gpt-5.6-terra`](https://developers.openai.com/api/docs/models/gpt-5.6-terra),
and [`gpt-5.6-luna`](https://developers.openai.com/api/docs/models/gpt-5.6-luna).
Each page currently records:

- knowledge cutoff `2026-02-16`;
- maximum output `128,000` tokens; and
- efforts `none`, `low`, `medium`, `high`, `xhigh`, and `max`.

The owner matrix uses only officially listed efforts. The experiment CLI and
core prediction identity both accept `none`, `medium`, `high`, `xhigh`, and
`max` and preserve the effort plus output cap in run metadata. No source or test
change is needed for this surface.

Current short-context prices from [official OpenAI API
pricing](https://developers.openai.com/api/docs/pricing), in USD per one million
tokens, are:

| Model | Standard input / cached / write / output | Flex input / cached / write / output |
|---|---:|---:|
| `gpt-5.6-sol` | `$4.00 / $0.40 / $5.00 / $20.00` | `$2.00 / $0.20 / $2.50 / $10.00` |
| `gpt-5.6-terra` | `$2.00 / $0.20 / $2.50 / $12.00` | `$1.00 / $0.10 / $1.25 / $6.00` |
| `gpt-5.6-luna` | `$0.20 / $0.02 / $0.25 / $1.20` | `$0.10 / $0.01 / $0.125 / $0.60` |

`CostCalculationService` contains the current Standard input, cached-input, and
output rates for all three exact IDs. Its `0.5` Flex multiplier produces the
official short-context Flex input, cached-input, and output rates. The current
experiment route does not opt into explicit cache writes. If a run reports
cache-write usage or a cost inconsistent with this assumption, stop before the
next paid run and amend the calculator/evidence process rather than ignoring it.

OpenAI currently states that Sol's promotional price is available at least
through `2026-11-21`. If execution or production-cost interpretation crosses
that date, recheck the applicable Sol price and rerun every affected estimate.

Official prices and cutoffs must be fetched again immediately before live
preparation. Any change pauses execution for an explicit preregistration and
calculator review.

## Cutoff-safe shared historical contract

With the currently common `2026-02-16` cutoff, the exact two-calendar-day
safety boundary is `2026-02-18T00:00:00 Europe/Berlin (+01)`. Only completed
Bundesliga 2025/26 fixtures starting strictly after that instant are eligible.
If the execution-date recheck gives different model cutoffs, use the hardest
cutoff plus two calendar days for every directly compared quality run and
derive each cost-row window explicitly.

When the execution-date cutoffs remain common, all missing-row preflights reuse
one seed-`20260821` one-item manifest and all missing base rows reuse one
seed-`20260821` five-by-four manifest. Prepare a separate cost manifest only for
a candidate whose official cutoff has changed; do not duplicate manifests just
because model or effort differs.

Both phases use only:

- competition `bundesliga-2025-26`;
- compatibility route `bundesliga-2025-26-legacy-id-hash-v1`;
- community context `pes-squad`;
- evaluation at `startsAt -12:00:00`;
- the seven producer-era historical documents, exact versions, and hashes;
- hosted prompt `kicktippai/bundesliga-2026-27/predict-one-match`, exact version
  `2`, with required `production` label; and
- prompt key `bundesliga-match-v2`.

At this boundary the audited complete pool must remain:

| Property | Required value |
|---|---|
| Eligibility policy | `bundesliga-2025-26-completed-after-sampling-cutoff-all-7-context-documents-at-or-before-starts-at-minus-12h-v1` |
| Eligible fixture count | `109` |
| Sorted eligible-source-ID SHA-256 | `6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415` |

Preparation forms and validates the complete eligible pool before applying one
declared seed. An identity, scope, timestamp, context, prompt, count, or hash
mismatch is a stop condition. Never retry a seed or substitute a fixture to
obtain a convenient sample.

The seven-document historical route is a preseason proxy. It may understate
live Bundesliga 2026/27 input cost because the live route has eleven documents.
No historical cost row, token count, output length, or plumbing result is a
prediction-quality claim.

## Output-cap policy

Caps are observations, not owner guesses:

1. Reuse `10000` only for the existing exact Luna/`none` row.
2. For every missing row with no exact reusable preflight evidence, start one
   item at the repository default `10000` cap. This is the prescribed
   estimate-row default, not a candidate-wide cap selection.
3. Collect input, output, reasoning, tier, fallback, termination, and observed
   cost. A cap exhaustion, missing output, or `outputTokens >= maxOutputTokens`
   fails the preflight.
4. A higher cap may be selected only from exact evidence and an explicit
   reviewed amendment. The Luna/`max` five-by-four cap failure below amends the
   plan for exactly one new one-item preflight at cap `20000`, after independent
   review of the frozen checkpoint. The reviewed amendment must then be
   integrated to `main`, explicitly pushed, and pass exact-head green CI before
   a fresh Decimal gate may admit the call. It does not authorize a direct
   20-call rerun, a cap-`40000` probe, or an automatic doubling ladder.
5. The full five-by-four row must complete all 20 items below the selected cap.
   Any cap hit invalidates the row and pauses execution before another paid run.
6. The quality run uses the exact cap established by that candidate's accepted
   row; it cannot inherit another model or effort's cap.

This policy is deliberately stricter for Sol/`high`, Terra/`xhigh`, and
Luna/`max`, but the one-item gate applies to all eight missing rows so the
combined budget cannot be surprised by a nominally cheaper effort.

## One cumulative USD 30 ledger

The owner's later USD 30 amendment replaces the earlier USD 20 statement and
the handoff's separate phase-budget template. It covers all new paid calls made
after this checkpoint for both cost evidence and quality evidence.

The already completed Luna/`none` preflight and base row are prior evidence and
require zero new spend. They do not debit this new authorization because the
owner's current-balance statement was made after those calls. They remain
visible in the evidence report: the accepted row's all-input-uncached estimate
is `$0.005079200000`, while its observed Langfuse cost was `$0.000696920000`.

Ledger rules:

- Initialize new authorized spend at exactly `$0.000000000000` and ceiling at
  `$30.000000000000`.
- Use the integrated `budget-gate` command below for the cumulative
  multi-attempt total. Its machine-readable JSON is mandatory admission
  evidence; a write failure blocks rather than returning `ALLOWED`.
- Machine-project and admit each one-item preflight before its model call with
  `--planned-preflight`; there is no accepted candidate row to reuse at that
  point. This is a conservative full-cap admission bound, not a base-row or
  quality estimate.
- Record every new paid attempt: accepted calls, failed calls with usage,
  cap-retry calls, Flex/Standard fallbacks, replaced dataset runs, and quality
  retries. Do not count only the accepted artifact.
- Track observed cost, unsettled ingestion, and the next proposed run's exact
  tooling projection using Decimal arithmetic in that machine-readable ledger.
- Never launch the next paid run while the preceding attempt's usage is
  unsettled. Do not rerun because Langfuse ingestion is delayed; recollect.
- Serialize candidate runs. Start an initial attempt only when the aggregate
  command admits it strictly within USD 30. Do not reserve an assumed partial
  retry: after the attempt settles, separately gate the parallelism-`3` retry,
  then separately gate the parallelism-`1` retry if it becomes necessary.
- If exact tooling cannot produce a required projection, stop. Do not replace
  it with hand arithmetic.
- Stop before the next call on pricing drift, identity drift, cap pressure,
  malformed output, unexpected cache-write charging, or a material divergence
  between projected and observed cost.
- Never spend the remainder merely because it is available.

Per-row cost estimates reported to the owner come only from
`experiment_cost_estimator.py`; observed Langfuse cost is kept as a separate
ledger field. `budget-gate` enforces program-total admission with exact Decimal
arithmetic and authoritative or explicitly provisional evidence.

Independent review approved the exact integrated gate at main commit
`0b86b11564b9cc7500b7bfaf94301e4e83263f73`. Its focused deterministic suite
passes all `24` tests, and the exact commit's
[`Build and Test` run 32910669112](https://github.com/ehonda/KicktippAi/actions/runs/32910669112)
completed successfully. These are no-spend tooling checks, not permission to
skip any live-action checklist item.

### Executable admission command contract

All artifacts stay under `.tmp/p0-23-budget/`, which is repo-local and ignored.
Run from the repository root. Replace a `SETTLED_*_USD` value only with the
settled Langfuse cost for that named attempt; never pre-sum attempts. Every gate
must emit `ALLOWED` and successfully write its JSON before the corresponding
paid call starts.

For the first missing-row candidate in the frozen order, create
`.tmp/p0-23-budget/gpt-5.6-luna-max-preflight-plan.json` with these exact
contents:

```json
{
  "name": "gpt-5.6-luna-max-preflight",
  "model": "gpt-5.6-luna",
  "reasoningEffort": "max",
  "serviceTier": "flex",
  "inputTokenBound": 272000,
  "maxOutputTokens": 10000,
  "boundEvidence": "Uses the complete 272,000-token input boundary supported by the repository short-context price table; it does not rely on a prior row or historical average.",
  "source": "P0-23 preregistration: first missing-row preflight in the owner-approved matrix"
}
```

Create the ignored artifact directory and admit exactly one bootstrap call with
zero observed attempts and no retry reserve:

```powershell
New-Item -ItemType Directory -Force -Path .tmp/p0-23-budget | Out-Null

uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py budget-gate `
  --planned-preflight .tmp/p0-23-budget/gpt-5.6-luna-max-preflight-plan.json `
  --pricing-source src/OpenAiIntegration/CostCalculationService.cs `
  --ceiling-usd 30 `
  --report-json .tmp/p0-23-budget/gpt-5.6-luna-max-preflight-budget-gate.json
```

For each later missing row, use its exact model, effort, unique name, and
deterministic artifact path in a separate spec. Keep tier `flex`, input bound
`272000`, initial output cap `10000`, the same explicit bound basis and pricing
source, and include every already settled attempt on that gate.

After the one-item Luna/`max` call settles and its compact usage is collected,
produce its exact provisional report, then machine-admit the pending 20-call
five-by-four row:

```powershell
$preflightUsageJson = '.tmp/p0-23-budget/gpt-5.6-luna-max-preflight-usage.json'
$preflightBaseRowJson = '.tmp/p0-23-budget/gpt-5.6-luna-max-preflight-base-row.json'
$settledLunaMaxPreflightUsd = 'SETTLED_LUNA_MAX_PREFLIGHT_USD'

uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py base-row `
  --input $preflightUsageJson `
  --group repeated-match-slice-measured `
  --expect-count 1 `
  --model gpt-5.6-luna `
  --reasoning-effort max `
  --prompt-route "Langfuse Bundesliga match v2; Bundesliga 2025/26 7-document legacy-id-hash-v1 context" `
  --model-knowledge-cutoff 2026-02-16 `
  --sampling-cutoff "2026-02-18T00:00:00 Europe/Berlin (+01)" `
  --max-output-tokens 10000 `
  --source "P0-23 exact one-item preflight DATASET_RUN_ID" `
  --report-json $preflightBaseRowJson

uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py budget-gate `
  --provisional-candidate "$preflightBaseRowJson,20" `
  --observed-attempt "gpt-5.6-luna-max-preflight=$settledLunaMaxPreflightUsd" `
  --ceiling-usd 30 `
  --report-json .tmp/p0-23-budget/gpt-5.6-luna-max-base-row-p5-budget-gate.json
```

The one-item report must have `baseSampleObservations=1` and complete model,
effort, cap, average, and source provenance. The gate hashes and reads it but
does not upsert it. It is valid only for admitting this pending 20-call row;
the later quality call requires the completed authoritative row.

Once the needed authoritative rows exist, admit one serialized quality
candidate with a repeated observed-attempt ledger and no mandatory retry
reserve. This exact example uses the already authoritative Luna/`none` row and
the preferred `10 × 20 = 200` topology:

```powershell
$settledLunaMaxPreflightUsd = 'SETTLED_LUNA_MAX_PREFLIGHT_USD'
$settledLunaMaxBaseRowUsd = 'SETTLED_LUNA_MAX_BASE_ROW_USD'
$settledAttemptArgs = @(
  '--observed-attempt'
  "gpt-5.6-luna-max-preflight=$settledLunaMaxPreflightUsd"
  '--observed-attempt'
  "gpt-5.6-luna-max-base-row-p5=$settledLunaMaxBaseRowUsd"
)
# Append one --observed-attempt/name-value pair for every other settled P0-23 attempt.

uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py budget-gate `
  --candidate gpt-5.6-luna,none,200 `
  @settledAttemptArgs `
  --ceiling-usd 30 `
  --report-json .tmp/p0-23-budget/gpt-5.6-luna-none-quality-10x20-p5-budget-gate.json
```

Do not pre-reserve a speculative retry in this serialized program. If that
quality attempt fails transiently, wait until its cost settles, append the
attempt to the ledger, and admit the same 200-call candidate separately before
the parallelism-`3` retry:

```powershell
$settledLunaNoneQualityP5Usd = 'SETTLED_LUNA_NONE_QUALITY_P5_USD'
$settledAttemptArgs += @(
  '--observed-attempt'
  "gpt-5.6-luna-none-quality-p5=$settledLunaNoneQualityP5Usd"
)

uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py budget-gate `
  --candidate gpt-5.6-luna,none,200 `
  @settledAttemptArgs `
  --ceiling-usd 30 `
  --report-json .tmp/p0-23-budget/gpt-5.6-luna-none-quality-10x20-p3-retry-budget-gate.json
```

If parallelism `3` also fails transiently, first settle and append that attempt,
then run the same candidate gate again with a distinct `p1-retry` report path.
`--retry-reserve` remains available only for an explicit concurrent reserve;
it is deliberately absent from this serialized initial/retry contract.

## Live execution checkpoint — Luna/max cap stop

Execution-date checks on 2026-08-26 reconfirmed the three official cutoffs,
reasoning surfaces, prices, the Sol promotion, the local calculator, and hosted
match prompt version `2` with `production` membership. The exact one-item
dataset sync was unchanged at dataset ID `cmt86fx6o0aeuad0dg99ivamv`.

The first Decimal gate admitted the Luna/`max` cap-`10000` preflight at the
conservative `$0.033200000000` bound. Exact dataset run
`195f2348-bac2-4900-9764-bd35618bd4a3`, trace
`4f617fa3b1972f95f549181656f52dac`, then completed on Flex without fallback:
`2463` input, `535` output, `516` reasoning tokens, and observed cost
`$0.000567300000`. Its provisional report machine-projected and admitted the
20-call row at `$0.011346000000`, with `$0.011913300000` all-in at that gate.

The exact cap-`10000`, parallelism-`5`, manifest
`fcadeeadaadd1356472a1f4f96b7277a05ba3b8a19dcde60e6f2e7d79af577b7`
attempt was
`repeated-match-slice__pes-squad__gpt-5.6-luna__match-v2__reasoning-max__random-5x4-seed-20260821__cost-estimate__startsat-12h__2026-08-26t07-16-37z`.
It exited `1` on source item `ts1423757259` (Hamburger SV vs RB Leipzig,
matchday 24) because the OpenAI response contained no output text. Failed trace
`4dc34c3c55f2425c7da00decc4ddb3e7` contains only spans, no generation usage,
and Langfuse reports zero cost; that is not treated as proof that the provider
charge was zero. Four other calls are observable. One used `8970 / 10000`
output tokens (`8951` reasoning), independently demonstrating cap pressure.
The collector's complete 900-second expectation wait ended `4/5`.
The ignored payload-safe binding artifact is
`.tmp/p0-23-budget/gpt-5.6-luna-max-base-row-p5-failure-evidence.json`, SHA-256
`2d129d123765d0674d1edaa9a3686498bfa59b6aa8f47138fff42c1e285f0157`.
It independently records the run/trace/error, immutable dataset/manifest
hashes, execution settings, collector outcome, and supporting output hashes;
it contains no prompt, context, prediction, credential, or secret payload.

The machine ledger records `$0.010494600000` observable spend: the preflight
plus `$0.009927300000` across the four visible row calls. It carries the missing
fifth call as a conservative `$0.033200000000` full-272k-input/full-10k-output
bound, producing bounded all-in exposure `$0.043694600000`. No row was
upserted, the exact estimator counts were not run, and all later paid work
stopped.

The bounded remediation is exactly one Luna/`max` one-item run against the same
one-item manifest and prompt/evaluation route at cap `20000`, parallelism `1`.
A no-spend Decimal gate carrying both observed attempt groups and the
`$0.033200000000` unresolved reservation admitted that future probe at a
conservative `$0.039200000000`, with bounded all-in `$0.082894600000`. The
probe may run only after independent exact-SHA approval of this amendment,
integration to `main`, an explicit `main` push, reconciliation of green CI for
that exact pushed head, and a fresh machine admission from that exact head. If
it returns no output, reaches `20000`, drifts tier/identity/pricing, or is
otherwise malformed, stop again. There is no automatic cap-`40000` action.

## Phase A — exact cost rows and whole-season estimates

### Reused row

Reuse the authoritative `gpt-5.6-luna` / `none` / cap-`10000` row and its exact
accepted dataset-run binding. The no-spend estimator command re-run at this
checkpoint was:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 1,20,306,493 --model gpt-5.6-luna --reasoning-effort none
```

It returned:

```text
N=1: $0.000253960000
N=20: $0.005079200000
N=306: $0.077711760000
N=493: $0.125202280000
```

The Bundesliga call counts reuse the documented primary-source and historical
reprediction evidence:

- 306 scheduled matches from the [official Bundesliga fixture
  explainer](https://www.bundesliga.com/en/bundesliga/news/how-the-bundesliga-fixture-list-is-made-bayern-munich-borussia-dortmund-20316);
- observed `pes-squad` / `o3` counts: 313 initial calls and 191 extra
  repredictions; and
- projected realistic season count `493`, already derived and recorded in the
  [whole-season estimates](whole-season-cost-estimates.md).

No new Firestore cost read is needed.

### Missing-row preflight sequence

Independent checkpoint approval and Decimal-gate integration are complete.
The shared `1 × 1` historical dataset and manifest are now prepared as ignored
local artifacts and their exact provenance has passed the pre-sync inspection.
After every remaining live-action check below passes, and after the pending
dataset-upload authorization is explicit, sync that exact dataset once and run
these eight preflights serially. It uses seed `20260821` and selected source
fixture `1423757341`; its exact selected source-item ID is
`bundesliga-2025-26__pes-squad__ts1423757341` and its selected-set SHA-256 is
`4a293d4bac8f6406cb88770332a5b85f9084f01d2f2e0227f7d52d63e93c4e16`.
That identity and hash are derived through the repository's canonical dataset
item and sorted-newline SHA-256 functions and reproduce the established
five-item selected-set hash when applied to its recorded IDs.

The prepared `1 × 1` identities are:

- raw `slice-dataset.json` SHA-256
  `389b806e89b08169ea0092667d7fc774f0737c6e235e44b4fbf18c81c412c717`;
- raw `slice-manifest.json` SHA-256
  `b396ffd599c8c79569db656d66e68ebe9169caf9a7e274d1aa0e7a0c8f8017c1`;
- canonical historical-artifact SHA-256
  `a03c31c174e0e0be1723b5214453a3992c2b5d023d125eb75fa658a7503c2946`;
  and
- manifest sample size and item count `1` / `1`.

1. `gpt-5.6-luna` / `max`
2. `gpt-5.6-terra` / `xhigh`
3. `gpt-5.6-sol` / `high`
4. `gpt-5.6-luna` / `medium`
5. `gpt-5.6-terra` / `medium`
6. `gpt-5.6-sol` / `medium`
7. `gpt-5.6-terra` / `none`
8. `gpt-5.6-sol` / `none`

The sequence probes the decision-critical quality-first ladder before the
balanced and no-reasoning ladders, while moving Luna → Terra → Sol inside each
comparable ladder to expose cap/cost anomalies cheaply. Each run waits for an
exact one-item collection and ledger update before the next starts. A finding
may stop the sequence; the order is not authority to skip a failed gate.

For each accepted preflight, follow the exact one-item `base-row` and
provisional `budget-gate` pattern above, using deterministic repo-local
`.tmp/p0-23-budget/<model>-<effort>-...` artifact paths. Replace only the
necessarily runtime dataset-run identity and settled cost. The provisional gate
emits the 20-item projection before admitting the five-by-four row; do not
manually multiply the one-item result. Retain the emitted JSON as admission
evidence.

### Authoritative five-by-four rows

If all applicable preflights are healthy, prepare or exact-reuse one shared
five-fixture-by-four-repetition manifest with seed `20260821`. Under the
unchanged pool it must select:

- `1423757259`
- `1423757286`
- `1423757328`
- `1423757333`
- `1423757341`
- selected-set SHA-256
  `3f5b16efb7901c9536c9e290ea3e9a4d5138e43ef784c26b252437d714e13ad6`

The locally reproduced `5 × 4` artifacts bind exactly that selection and have:

- raw `slice-dataset.json` SHA-256
  `0fbc3e07f926596805a23bbe3241fcf2ec368858f217cb1e05ccbac96c907d18`;
- raw `slice-manifest.json` SHA-256
  `fcadeeadaadd1356472a1f4f96b7277a05ba3b8a19dcde60e6f2e7d79af577b7`;
- canonical historical-artifact SHA-256
  `22dfcab23f063e2fbb7a7fa96df4f2fb5dca384bb1329adc0c33157f5419a105`;
  and
- manifest sample size and item count `20` / `20`.

Run missing rows in the same sequence as the preflights. Within a row use batch
count `1` and fixture parallelism `5`; only Flex/rate failures may retry the
same manifest and settings at `3`, then `1`, and each retry is separately
admitted only after the preceding attempt's cost settles. Bind collection to
the exact dataset ID, accepted dataset-run ID, prepared manifest
SHA-256/sample-size tuple, and expected 20 distinct item-to-trace links. Upsert
only after all 20 items succeed below cap.

After each row, run the estimator for the quality-design counts and the two
season counts:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,100,150,200,306,493 --model MODEL --reasoning-effort EFFORT
```

Every row and estimate records the exact cap, cutoff, prompt route, observed
service tiers/fallbacks, immutable run binding, and the seven-versus-eleven
document limitation. The 306/493 estimates are the production-budget inputs;
they do not themselves rank quality.

## Phase B — adaptive common-manifest quality comparison

Quality execution starts only after every row needed by the proposed surface is
exact and the Decimal aggregate command admits each serialized candidate run.
Cost rows determine the topology before any quality call.

### Topology rule

First test the owner's preferred full-nine-matrix topology of `10` fixtures ×
`20` repetitions = `200` predictions per candidate, using exact per-row
estimator output, the Decimal aggregate gate, and one common manifest for every
compared candidate. It retains the decision-strength target of 20 paired
repetition totals without buying extra fixture coverage merely because ceiling
remains.

If the full matrix cannot fit at `10 × 20`, prefer stronger evidence for one
preliminary subset over covering all nine rows with fewer repetitions. Run
exactly this quality-first family block:

- Sol/`high`;
- Terra/`xhigh`; and
- Luna/`max`.

Select the first affordable preliminary topology in this order:

1. `10 × 20`;
2. exploratory `10 × 15`; or
3. exploratory `10 × 10`.

The owner authorizes the two exploratory fallbacks only after machine estimates
prove no 20-repetition preliminary topology fits the cumulative ceiling under
the separately gated retry policy. Their effective paired `n` is 15 or 10,
with visibly weaker precision; report that limitation and do not overclaim. If
the block cannot fit at `10 × 10`, run no quality matrix. Do not split the block
or choose membership after seeing scores. Return to the owner after its report
before any medium, none, or other follow-up quality configuration, even if
ceiling remains.

Use one shared UTC run stamp for the accepted family. Candidate processes are
serialized so settled cost gates the next candidate; each process still uses
the runner's fixture-level parallelism. This preserves the repository's useful
parallelization without admitting multiple unsettled model totals.

### Immutable pairing and metrics

Langfuse currently assumes one dataset-run item per dataset item. Therefore
every fixture/repetition cell remains a distinct prepared dataset item. All
candidates must run the identical item set from one manifest.

The primary descriptive metric is `avg_kicktipp_points`. For inference, the
paired unit is one repetition index's total Kicktipp points summed across all
selected fixtures. Report:

- average Kicktipp points and the complete 0/2/3/4 point distribution;
- paired mean and median repetition-total deltas with bootstrap 95% confidence
  intervals;
- repetition-total win/tie/loss counts;
- Friedman omnibus results for three or more candidates; and
- only when warranted, Holm-adjusted pairwise Wilcoxon signed-rank results.

Item-level rows are descriptive diagnostics, not independent inferential
samples. Report the 306/493 cost estimate next to quality, but never infer
quality from cost, tokens, output length, plumbing success, or the Luna
validation ladder. For an exploratory 15- or 10-repetition result, label the
effective paired sample and weaker intervals/tests in every summary. The owner
evaluates the quality/cost tradeoff.

### Failure and retry contract

- Never replace fixtures, retry the seed, drop failed cells, or analyze an
  unpaired subset.
- Prompt/context/model/effort/cap/dataset item drift stops before model
  construction when possible and invalidates the run otherwise.
- A cap hit, missing or malformed prediction, missing score, duplicate/missing
  item link, or incomplete candidate run stops the comparison.
- A transient Flex/rate failure may retry only the exact same candidate
  manifest/settings at parallelism `3`, then `1`; all attempt cost stays in the
  cumulative ledger.
- A same-name replacement is accepted only through its immutable dataset-run
  links. Run-name-only trace selection, truncation, and timestamp windows are
  forbidden.
- Do not silently change topology or candidate membership after outcomes are
  visible. Record a reviewed preregistration amendment first.

## Freeze checklist before any new experiment action

- [x] Exact nine-row owner matrix recorded.
- [x] Single cumulative USD 30 ceiling and stop ledger recorded.
- [x] Workflow-derived cap policy recorded; only Luna/`none` currently has a
      selected exact cap.
- [x] Adaptive common-manifest topology, meaningful minimum, immutable metrics,
      and failure rules recorded.
- [x] Owner-supplied captures preserved and embedded.
- [x] Independent review approved this no-spend checkpoint.
- [x] The machine-readable Decimal `budget-gate` and exact command contract are
      integrated; its focused deterministic suite passes all 24 tests and CI is
      green for exact main commit `0b86b11564b9cc7500b7bfaf94301e4e83263f73`.
- [x] Official model pages and pricing were re-fetched on the execution date.
- [x] Current pricing-calculator and CLI support were rechecked after any code
      integration that lands before execution.
- [x] Exact prompt version `2` still carries required `production` membership.
- [x] Prepared pool, manifest, selection, and expected counts pass pre-spend
      inspection.
- [ ] Independent exact-SHA review approves the bounded Luna/`max` cap-`20000`
      one-item amendment before its paid call.
- [ ] Integrate the reviewed amendment to `main` and push `origin main`
      explicitly before the remediation probe.
- [ ] Reconcile an exact-head green `Build and Test` run for that pushed `main`
      SHA before the remediation probe.
- [ ] The Decimal aggregate command separately admits the next initial attempt
      or retry strictly within USD 30 from that exact green head after all
      preceding cost settles.

As of this live checkpoint, the one-item dataset is synchronized unchanged, the
one-item preflight and failed first-batch five-by-four attempt above are the only
new paid P0-23 actions, and all further paid work is paused.

The synced dataset contained only the previously audited public match record;
the local manifest with context names, versions, source IDs, timestamps, and
hashes was not the sync payload. No prompt/context/prediction payload or secret
was retained. Every later immutable-run, quality, production-selection, and
activation gate remains open.

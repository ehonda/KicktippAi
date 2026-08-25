# GPT-5.6 Bundesliga 2025/26 production-candidate preregistration

**Status:** NO-SPEND CHECKPOINT — owner matrix and cumulative ceiling recorded;
live mutation and spend wait for independent review.

**Verified:** 2026-08-26

This preregistration defines the P0-23 evidence program for the Bundesliga
2026/27 production-model and arena-participant decisions. It replaces the
superseded Terra/`medium`, Sol/`medium`, cap-`10000`, fixed-`15 × 20`, and
separate-phase-budget example with the owner's exact nine-row matrix, the
estimate-row-derived cap workflow, an adaptive paired quality design, and one
cumulative USD 30 ceiling.

USD 30 is a hard ceiling, not a spending target. The program should finish for
less whenever the evidence is sufficient. This checkpoint authorizes no
dataset preparation or synchronization, hosted prompt or Langfuse mutation,
model call, production model, arena participant, prediction post, workflow
dispatch, or schedule activation.

Related planning and decisions:

- [P0-23 — GPT-5.6 production-candidate evidence](../../plans/bundesliga-2026-27/tasks/p0-23-gpt-5-6-production-candidate-evidence.md)
- [ADR-0006 — stage validation with a cheap test model](../../plans/bundesliga-2026-27/decisions/0006-stage-validation-with-a-cheap-test-model.md)
- [ADR-0033 — pin the validation-model ledger and reserve production selection](../../plans/bundesliga-2026-27/decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md)
- [ADR-0040 — hash-bound Bundesliga 2025/26 experiment compatibility](../../plans/bundesliga-2026-27/decisions/0040-use-hash-bound-2025-26-context-for-preseason-cost-experiments.md)
- [ADR-0043 — freeze historical aliases and the eligible pool](../../plans/bundesliga-2026-27/decisions/0043-freeze-historical-experiment-aliases-and-eligible-pool.md)
- [ADR-0046 — bind cost usage to exact Langfuse dataset runs](../../plans/bundesliga-2026-27/decisions/0046-bind-cost-usage-to-langfuse-dataset-runs.md)
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
| `gpt-5.6-luna` | `max` | Luna quality-first candidate | Missing |
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
4. A higher cap may be selected only from that exact preflight evidence and an
   explicit reviewed amendment. There is no automatic guessed doubling ladder.
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
- Record every new paid attempt: accepted calls, failed calls with usage,
  cap-retry calls, Flex/Standard fallbacks, replaced dataset runs, and quality
  retries. Do not count only the accepted artifact.
- Track observed cost, an unsettled-ingestion reservation, and the exact
  estimator projection for the next proposed run using decimal arithmetic.
- Never launch the next paid run while the preceding attempt's usage is
  unsettled. Do not rerun because Langfuse ingestion is delayed; recollect.
- Start a run only when cumulative observed spend plus all outstanding
  reservations plus the next run's estimator projection and retry reserve are
  strictly within USD 30. The reserve is the estimator projection for one
  same-manifest retry of the most expensive run in the proposed wave.
- If exact tooling cannot produce a required projection, stop. Do not replace
  it with hand arithmetic.
- Stop before the next call on pricing drift, identity drift, cap pressure,
  malformed output, unexpected cache-write charging, or a material divergence
  between projected and observed cost.
- Never spend the remainder merely because it is available.

Cost estimates reported to the owner come only from
`experiment_cost_estimator.py`; observed Langfuse cost is kept as a separate
ledger field.

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

After independent checkpoint approval, prepare and sync one shared `1 × 1`
historical manifest once, inspect its exact provenance, and run these eight
preflights serially:

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

Run missing rows in the same sequence as the preflights. Within a row use batch
count `1` and fixture parallelism `5`; only Flex/rate failures may retry the
same manifest and settings at `3`, then `1`. Bind collection to the exact
dataset ID, accepted dataset-run ID, prepared manifest SHA-256/sample-size
tuple, and expected 20 distinct item-to-trace links. Upsert only after all 20
items succeed below cap.

After each row, run the estimator for the quality-design counts and the two
season counts:

```powershell
uv --cache-dir .uv-cache run python .agents/skills/estimate-experiment-cost-skill/scripts/experiment_cost_estimator.py estimate --counts 20,100,150,200,240,300,306,493 --model MODEL --reasoning-effort EFFORT
```

Every row and estimate records the exact cap, cutoff, prompt route, observed
service tiers/fallbacks, immutable run binding, and the seven-versus-eleven
document limitation. The 306/493 estimates are the production-budget inputs;
they do not themselves rank quality.

## Phase B — adaptive common-manifest quality comparison

Quality execution starts only after every row needed by the proposed subset is
exact and the complete proposed wave plus retry reserve fits the remaining
ledger. Cost rows may reduce the matrix before any quality call.

### Topology rule

Choose the first affordable topology from this ordered lattice, using exact
per-row estimator output and one common manifest for every compared candidate:

1. `15` fixtures × `20` repetitions = `300` predictions per candidate;
2. `12` fixtures × `20` repetitions = `240` predictions per candidate;
3. `10` fixtures × `20` repetitions = `200` predictions per candidate;
4. `10` fixtures × `15` repetitions = `150` predictions per candidate; or
5. `10` fixtures × `10` repetitions = `100` predictions per candidate.

Shrinking fixture coverage while retaining 20 paired repetition totals is
preferred to immediately shrinking the paired sample. A quality comparison
below `10 × 10` is not considered meaningful for this decision and must not be
run merely to cover more configurations.

Try the full nine-row matrix first. If it does not fit at `10 × 10`, execute
the largest cumulative subset that fits at or above that minimum in these
predeclared blocks:

1. quality-first family coverage: Sol/`high`, Terra/`xhigh`, Luna/`max`;
2. then balanced family coverage: Sol/`medium`, Terra/`medium`, Luna/`medium`;
3. then no-reasoning baselines: Sol/`none`, Terra/`none`, Luna/`none`.

Do not split a three-row block or choose a subset after seeing its match scores.
If even the first block cannot fit the meaningful minimum and retry reserve,
run no quality matrix and return the cost evidence to the owner. After a
preliminary subset report, additional blocks require the owner's next decision,
even if ceiling remains.

Use one shared UTC run stamp for the accepted family. Multiple candidate
processes may run in parallel only after reservations for the complete wave and
retry reserve fit under the ceiling; PowerShell must launch all jobs before
waiting. Each process still uses the runner's fixture parallelism. A cheaper
serialized wave is acceptable when concurrent Flex capacity would increase
fallback risk.

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
validation ladder. The owner evaluates the quality/cost tradeoff.

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
- [ ] Independent review approves this no-spend checkpoint.
- [ ] Official model pages and pricing are re-fetched on the execution date.
- [ ] Current pricing-calculator and CLI support are rechecked after any code
      integration that lands before execution.
- [ ] Exact prompt version `2` still carries required `production` membership.
- [ ] Prepared pool, manifest, selection, and expected counts pass pre-spend
      inspection.
- [ ] Cumulative ledger plus next projection and retry reserve remain strictly
      within USD 30.

As of this checkpoint, no dataset was prepared or synced, no hosted prompt or
Langfuse object was mutated, no model was called, and no new spend occurred.

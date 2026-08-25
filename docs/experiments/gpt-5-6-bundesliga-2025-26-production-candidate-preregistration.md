# GPT-5.6 Bundesliga 2025/26 production-candidate preregistration

**Status:** DRAFT — no experiment or spend is authorized by this document.

**Verified:** 2026-08-25

This preregistration records a no-spend design history for the Bundesliga
2026/27 production-model decision. Its former Terra/`medium`, Sol/`medium`,
cap-`10000`, and `15 × 20` surface is a superseded provisional example, not the
selected experiment surface. The owner will provide a detailed model-experiment
specification after the current autonomous preparation work. Cost measurement
remains separate from prediction-quality comparison, and no missing cost row or
quality evidence may be produced before that exact surface and its phase-specific
spend ceiling are approved.

Related planning and decisions:

- [P0-23 — GPT-5.6 production-candidate evidence](../../plans/bundesliga-2026-27/tasks/p0-23-gpt-5-6-production-candidate-evidence.md)
- [ADR-0006 — stage validation with a cheap test model](../../plans/bundesliga-2026-27/decisions/0006-stage-validation-with-a-cheap-test-model.md)
- [ADR-0033 — pin the validation-model ledger and reserve production selection](../../plans/bundesliga-2026-27/decisions/0033-pin-validation-model-ledger-and-reserve-production-selection.md)
- [ADR-0040 — hash-bound Bundesliga 2025/26 experiment compatibility](../../plans/bundesliga-2026-27/decisions/0040-use-hash-bound-2025-26-context-for-preseason-cost-experiments.md)
- [ADR-0043 — freeze historical aliases and the eligible pool](../../plans/bundesliga-2026-27/decisions/0043-freeze-historical-experiment-aliases-and-eligible-pool.md)
- [Luna base-estimate evidence](../../.agents/skills/estimate-experiment-cost-skill/references/gpt-5.6-luna-none-base-estimate-2026-08-25.md)

## Decisions still reserved for the owner

- [ ] Approve the exact production-candidate model and reasoning-effort matrix.
- [ ] Approve the exact output-token cap for each candidate in the detailed
      model-experiment specification; do not inherit the superseded `10000`
      example.
- [ ] Approve a maximum spend for the missing-cost-row phase.
- [ ] After reviewing the completed cost rows, approve the final quality matrix,
      topology, and a separate quality-phase spend ceiling.
- [ ] Select or waive the final production candidate only after comparative
      quality evidence is available.

No unchecked decision above is implied by the repository changes that create
this draft.

## Official model facts and superseded provisional examples

The official OpenAI model pages and pricing page were checked on 2026-08-25.
All three exact GPT-5.6 model IDs publish a `2026-02-16` knowledge cutoff and
support `medium` as the default reasoning effort. Short-context token prices are
USD per one million tokens.

| Exact model ID | Recorded example effort | Evidence state | Standard input / cached / output | Flex input / cached / output | Official source |
|---|---:|---|---:|---:|---|
| `gpt-5.6-luna` | `none` | Existing authoritative 5x4 cost row; reusable without rerun, but not production selection | `$0.20 / $0.02 / $1.20` | `$0.10 / $0.01 / $0.60` | [Model](https://developers.openai.com/api/docs/models/gpt-5.6-luna), [pricing](https://developers.openai.com/api/docs/pricing) |
| `gpt-5.6-terra` | `medium` | Superseded provisional example; not selected | `$2.00 / $0.20 / $12.00` | `$1.00 / $0.10 / $6.00` | [Model](https://developers.openai.com/api/docs/models/gpt-5.6-terra), [pricing](https://developers.openai.com/api/docs/pricing) |
| `gpt-5.6-sol` | `medium` | Superseded provisional example; not selected | `$4.00 / $0.40 / $20.00` | `$2.00 / $0.20 / $10.00` | [Model](https://developers.openai.com/api/docs/models/gpt-5.6-sol), [pricing](https://developers.openai.com/api/docs/pricing) |

The unsuffixed `gpt-5.6` alias routes to Sol according to the official model
documentation. It is therefore excluded as a duplicate candidate identity;
experiments must use the exact `gpt-5.6-sol` ID for immutable provenance.

This table is not a candidate register or authorization. Terra/`medium` and
Sol/`medium` were not selected, and cap `10000` is not approved for them. Do not
infer a replacement effort, cap, candidate, or topology before the owner's
detailed specification.

## Shared knowledge-cutoff and historical-context contract

The three exact model IDs in the superseded example published the same
`2026-02-16` knowledge cutoff when checked. That fact does not freeze the future
experiment surface. After the owner supplies it, derive every selected model's
current cutoff and apply the established two-day margin before any preparation.
For the completed Luna row, the strict sampling boundary remains
`2026-02-18T00:00:00 Europe/Berlin (+01:00)` and a fixture was eligible only when
its start was strictly after that boundary. An earlier-cutoff manifest must not
be inherited silently by a new surface.

Both phases use the explicit historical compatibility route
`bundesliga-2025-26-legacy-id-hash-v1`, evaluation at match start minus 12 hours,
and the exact seven-document Bundesliga 2025/26 context. The complete eligible
pool is already bound as:

| Property | Required value |
|---|---|
| Eligibility policy | Current complete-pool, strict-after-cutoff historical policy |
| Eligible fixture count | `109` |
| Sorted eligible-source-ID SHA-256 | `6ecb182489b97f9ea389374183f0ef7cfe632ddfba341ea72aa354647593b415` |
| Hosted match prompt | `kicktippai/bundesliga-2026-27/predict-one-match`, exact version `2`, required label `production` |
| Prompt key | `bundesliga-match-v2` |
| Output cap | `10000` only for the completed Luna row; no cap selected for a future surface |
| Service tier | Flex first, Standard only as the recorded fallback |

The seven-document historical route is a preseason cost and comparison proxy.
Its input can understate the live Bundesliga 2026/27 eleven-document input cost.
Cost observations do not establish prediction quality.

## Phase A — cost rows (execution deferred)

No new cost-row execution is selected or authorized. The repository process
below applies only after the owner supplies the detailed surface and approves
the exact cost-phase budget.

### Existing Luna reference

Reuse the existing authoritative `gpt-5.6-luna` / `none` / cap `10000` 5x4 row.
It already uses the strict cutoff, pool count/hash, exact match-prompt route, and
the immutable accepted dataset-run binding. Do not spend to regenerate it merely
to align timestamps with later candidates.

### Missing-candidate preflight

After owner approval of a candidate and the cost-phase spend ceiling, prepare a
new one-item repeated-match-slice preflight for that exact model/effort/cap. Use:

- seed `20260821`, match count `1`, repetition count `1`;
- no alternate seed or silent fixture substitution;
- expected selected source fixture `1423757341` under the unchanged pool;
- the exact cutoff, pool identity, seven document hashes, score, and prompt route;
- batch count `1`, parallelism `1`, Flex first and Standard only as a recorded
  fallback.

Inspect prepared provenance before synchronization. Then synchronize once and run
the single item. Stop before the 5x4 run on any identity drift, item failure,
fallback outside the documented service-tier route, cap hit or near-hit, missing
usage, or material cost anomaly. Wait for ingestion and collect only compact
usage/cost/tier evidence—never prompt, context, or prediction payloads.

### Missing-candidate authoritative 5x4 row

If the preflight is clean, prepare one exact five-fixture-by-four-repetition
manifest with seed `20260821`; do not retry the seed. With the unchanged pool the
selected source IDs and selected-set hash must be:

- `1423757259`
- `1423757286`
- `1423757328`
- `1423757333`
- `1423757341`
- SHA-256 `3f5b16efb7901c9536c9e290ea3e9a4d5138e43ef784c26b252437d714e13ad6`

Synchronize the dataset once. Use one shared UTC run stamp and exact
model/effort/cap/prompt settings. Start with parallelism `5`; retry the same
manifest and settings at `3`, then `1`, only for Flex/rate failures. Preserve all
attempts as retry context and accept exactly one immutable dataset run containing
20 distinct linked items. Collection must be bound to the dataset ID, exact
dataset-run ID, prepared-manifest hash and sample size, and must fail on duplicate,
missing, or extra links.

Upsert the row only through the estimate skill after exactly 20 successful compact
observations have been validated. Run the authoritative estimator for counts
`306,493`; do not substitute manual arithmetic. Record observed service tiers,
fallbacks/retries, cutoff/pool/selection identities, exact collector binding, and
estimator stdout.

## Phase B — quality comparison (SUPERSEDED PROVISIONAL EXAMPLE)

This section preserves the earlier reviewable example for history. It is not an
executable authorization and its matrix/topology were not selected. Execution
waits for the owner's detailed surface and separate quality-phase budget.

### Superseded provisional topology

- The earlier example assumed an owner-approved subset of the provisional table.
- Derive one common manifest from the hardest published model cutoff plus the
  two-day margin; every candidate receives identical fixture/repetition items.
- Use a repeated-match-slice of `15` distinct fixtures × `20` repetitions
  (`300` predictions per candidate), seed `20260821`, with no seed retry.
- Bind the complete eligible-pool identity, selected IDs/hash, context manifests,
  prompt version/label, model ID, effort, cap, and service-tier attempts.
- Prepare and inspect before synchronization. A matrix entry may run only after
  its cost estimate fits within the separately approved quality ceiling.

The `15 × 20` topology is superseded and must not be prepared or executed. It was
intended to provide 20 paired repetition-total observations under the current
reporter while retaining fixture coverage; the owner's detailed specification
will supply the actual topology without inheriting this example.

### Paired analysis and reporting

The primary paired unit is the repetition-total score across the common fixtures,
not an individual prediction call. Report average Kicktipp points for each
candidate, paired deltas, bootstrap confidence intervals, and win/tie/loss counts.
Use a paired Wilcoxon signed-rank comparison for two candidates. For three or more,
use the Friedman omnibus test followed, only when warranted, by Holm-adjusted
pairwise Wilcoxon tests. Item-level results are descriptive diagnostics only and
must not be presented as independent inferential samples.

Report completion/failure and service-tier behavior alongside scores. Do not
select a winner from cost-only evidence, a partial matrix, an unpaired manifest,
or an analysis whose effective paired count differs from the preregistered count
without an explicit documented amendment.

### Failure and retry contract

- Never replace fixtures, retry the seed, or drop failed pairs to obtain a cleaner
  result.
- A transient Flex/rate failure may retry the same immutable manifest/settings at
  lower parallelism or Standard, with every attempt retained as provenance.
- Any model-level item failure, prompt/context drift, cap hit, malformed score, or
  incomplete paired matrix stops the comparison. Resolve the cause and obtain an
  explicit preregistration amendment before rerunning.
- Accept and analyze only exact dataset-run-bound exports with one link per
  expected item and complete manifest provenance.

## Freeze checklist before any new experiment

- [ ] Owner-approved exact model/effort/cap matrix is recorded.
- [ ] Phase-specific spend ceiling is recorded and the authoritative estimate is
      within it.
- [ ] Official cutoffs and prices have been rechecked on the execution date.
- [ ] Hardest-cutoff boundary and eligible-pool count/hash are unchanged or the
      preregistration has been amended explicitly.
- [ ] Exact prompt version `2` still carries the required `production` label.
- [ ] Prepared manifest, selection identity, expected count, and service-tier
      fallback contract have passed pre-spend inspection.

As of 2026-08-25, none of these freeze items authorizes a new call. The Terra/Sol
medium matrix and `15 × 20` topology are not selected. This document adds no
dataset, Langfuse mutation, prompt mutation, model run, spend, production
promotion, or prediction-quality claim. The authoritative Luna row remains
reusable without another model run.

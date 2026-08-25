# ADR-0049: Preregister GPT-5.6 candidate evidence under one program ceiling

- Status: Accepted
- Date: 2026-08-26

## Context

P0-23 must produce cost and cutoff-safe Bundesliga prediction-quality evidence
before the owner selects the Bundesliga 2026/27 production model and arena
participants. The new season has no completed results yet, so the accepted
hash-bound Bundesliga 2025/26 historical route remains the only accepted
cutoff-safe preseason basis. The owner supplied an exact nine-row GPT-5.6 matrix and first
authorized one USD 20 total program ceiling, then raised that same cumulative
ceiling by USD 10 to USD 30 so the estimate rows and a stronger quality sample
can fit. USD 30 is a maximum, not a target.

The candidate caps are not known yet. The estimate-row workflow must derive each
cap from an exact one-item preflight rather than copying Luna/`none` or guessing
from the reasoning label. Langfuse also currently permits only one experiment
item per prepared dataset item within a dataset run, so repetitions must remain
distinct prepared items. A separate implementation lane is adding a
machine-readable Decimal aggregate gate for the cumulative program budget; the
existing per-row estimator cannot by itself enforce a multi-run total.

## Decision

The owner-authorized P0-23 candidate matrix is:

| Exact model ID | Reasoning efforts |
|---|---|
| `gpt-5.6-sol` | `high`, `medium`, `none` |
| `gpt-5.6-terra` | `xhigh`, `medium`, `none` |
| `gpt-5.6-luna` | `max`, `medium`, `none` |

The exact Luna/`none` five-by-four row is reused without another model call.
Every other candidate begins with the estimate-row workflow's one-item default
cap. A candidate cap is an experiment mechanic derived from exact preflight
evidence; it is not a production selection. A failed or cap-bound preflight
stops before another paid run, and any higher cap requires a reviewed
preregistration amendment based on that exact observation.

While the execution-date official cutoffs remain the currently verified common
`2026-02-16` date, all candidates reuse one one-item manifest with seed
`20260821`, expected source fixture `1423757341`, exact selected source-item ID
`bundesliga-2025-26__pes-squad__ts1423757341`, selected-set SHA-256
`4a293d4bac8f6406cb88770332a5b85f9084f01d2f2e0227f7d52d63e93c4e16`,
and the established cutoff, pool, context, and prompt provenance. They also
reuse one exact five-by-four manifest with the same seed, selected source IDs
`1423757259`, `1423757286`, `1423757328`, `1423757333`, and `1423757341`, and
selected-set SHA-256
`3f5b16efb7901c9536c9e290ea3e9a4d5138e43ef784c26b252437d714e13ad6`.
Prepare a separate cost manifest only when an execution-date official cutoff
differs; directly compared quality candidates always use one common manifest
derived from the hardest current cutoff.

The cumulative ceiling for every new P0-23 paid attempt after this authorization
is exactly USD 30. All accepted, failed-with-usage, fallback, cap-retry,
replacement, and quality attempts count. No live action may start until the
machine-readable Decimal ledger and its validated aggregate command from the
budget-tool lane are integrated, their exact invocation is recorded in the
preregistration, and the no-spend checkpoint passes independent review. The
per-row estimator remains authoritative for row and season estimates, but it is
not represented as the cumulative gate.

After each exact one-item collection, run `base-row` with `--expect-count 1`
and machine-readable report output. The Decimal budget tool must consume that
exact report and produce the 20-item projection; manual multiplication is not
valid evidence. Its machine-readable transient projection must also admit the
one-item preflight itself before that first model call. Candidate cost rows are
serialized. Each initial attempt and each permitted parallelism `3` or `1`
retry is separately gated only after all preceding usage and cost have settled.
Fixture workflows within one candidate may still use the runner's parallelism
`5`, then `3`, then `1` policy.

Twenty paired repetition totals are the decision-strength target. After the
exact rows exist, first test the owner's preferred full-nine-matrix topology of
10 fixtures × 20 repetitions against the remaining Decimal ledger and the
separately gated retry policy.

Candidate runs remain serialized while retaining fixture-level runner
parallelism and one shared UTC run stamp. If the full matrix cannot fit at
10-by-20, prefer one stronger preliminary subset over covering all nine rows
with fewer repetitions. Run exactly one preliminary wave containing
Sol/`high`, Terra/`xhigh`, and Luna/`max`. Select its topology in this order:

1. 10 fixtures × 20 repetitions;
2. exploratory fallback: 10 fixtures × 15 repetitions; or
3. exploratory fallback: 10 fixtures × 10 repetitions.

The two exploratory fallbacks are owner-authorized only when the Decimal gate
proves that no 20-repetition topology for the preliminary block fits the
cumulative ceiling and retry policy. Their effective paired sample is only 15
or 10; reports must label the weaker precision and must not make a
decision-strength claim. If the block cannot fit at 10-by-10, run no quality
comparison. After the preliminary report, return to the owner before any
medium, none, or other follow-up quality run.

The Sol price used by this program is promotional and is officially stated to
remain available at least through 2026-11-21. Recheck that caveat together with
all model prices and cutoffs immediately before execution.

This ADR authorizes only the recorded cost and quality evidence mechanics under
the cumulative ceiling. It does not select a production model, set a production
cap or season ceiling, admit an arena participant, authorize a prediction post
or workflow dispatch, or enable a schedule.

## Alternatives considered

- **Keep separate cost and quality budgets:** Rejected because the owner
  explicitly replaced that handoff template with one cumulative ceiling.
- **Copy cap 10000 from Luna/`none` to every row:** Rejected because reasoning
  output is configuration-specific and caps must follow exact evidence.
- **Run every candidate with fewer than 20 repetitions:** Rejected; a stronger
  quality-first subset is preferred. The owner permits 15 or 10 repetitions
  only as a machine-gated exploratory fallback for that preliminary subset.
- **Automatically continue from the quality-first block into medium and none
  blocks:** Rejected because the owner asked to decide whether further spend is
  worthwhile after preliminary results.
- **Use the current per-row estimator as the total-program budget gate:**
  Rejected because it does not validate a cumulative multi-attempt Decimal
  ledger or unsettled cost.

## Consequences

- P0-23 has one durable, owner-authorized experiment contract instead of a
  provisional handoff template.
- The USD 30 maximum is enforceable only after the budget-tool dependency lands;
  until then, live work is blocked even though the matrix and spend are
  authorized.
- Cost evidence can cover all rows economically before the exact quality
  topology is chosen.
- A full-matrix automatic quality result always has 20 paired repetition
  totals. The one preliminary block targets 20 and may fall back to a visibly
  exploratory effective paired sample of 15 or 10 only after the Decimal gate
  proves stronger options unaffordable.
- The owner still makes every production, arena, season-budget, and activation
  decision after reviewing the evidence.

## Affected tasks

- [P0-23](../tasks/p0-23-gpt-5-6-production-candidate-evidence.md)
- [P0-06](../tasks/p0-06-model-ledger-and-cost-baseline.md)
- [P0-19](../tasks/p0-19-community-workflow-triad.md)
- [P0-21](../tasks/p0-21-production-activation.md)

## Supersedes

This decision supersedes the provisional P0-23 candidate, topology, and
separate-phase-budget input template in the active closeout handoff. It does not
supersede an Accepted ADR.
